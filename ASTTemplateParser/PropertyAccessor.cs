using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ASTTemplateParser
{
    /// <summary>
    /// High-performance property accessor with compiled delegate caching
    /// </summary>
    public static class PropertyAccessor
    {
        // Cache for compiled getters (properties or fields)
        private static readonly ConcurrentDictionary<string, Func<object, object>> _getterCache =
            new ConcurrentDictionary<string, Func<object, object>>();

        // Cache for compiled indexers
        private static readonly ConcurrentDictionary<string, Func<object, object, object>> _indexerCache =
            new ConcurrentDictionary<string, Func<object, object, object>>();

        /// <summary>
        /// Gets a property or field value using cached compiled delegates or dictionary lookup
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object GetValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            // 1. Support IDictionary (Dictionaries, ExpandoObject, etc.)
            if (target is IDictionary dict)
            {
                if (dict.Contains(memberName))
                    return dict[memberName];
                
                // Also try case-insensitive for dictionary keys if it's a string key dictionary
                foreach (var key in dict.Keys)
                {
                    if (key is string s && string.Equals(s, memberName, StringComparison.OrdinalIgnoreCase))
                        return dict[key];
                }
                return null;
            }

            var type = target.GetType();
            var cacheKey = type.FullName + "." + memberName;

            var getter = _getterCache.GetOrAdd(cacheKey, k => CreateAccessor(type, memberName));
            
            return getter?.Invoke(target);
        }

        /// <summary>
        /// Gets a value from an indexer (e.g., obj[key])
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object GetIndexerValue(object target, object index)
        {
            if (target == null || index == null)
                return null;

            // Support IDictionary directly
            if (target is IDictionary dict)
            {
                if (dict.Contains(index))
                    return dict[index];
                return null;
            }

            // Support IList and Arrays directly (numeric index)
            if (target is IList list)
            {
                try
                {
                    int idx = Convert.ToInt32(index);
                    if (idx >= 0 && idx < list.Count)
                        return list[idx];
                }
                catch { }
                return null;
            }

            var type = target.GetType();
            
            // Special case for native arrays that might not match IList
            if (type.IsArray)
            {
                var arr = (Array)target;
                try
                {
                    int idx = Convert.ToInt32(index);
                    if (idx >= 0 && idx < arr.Length)
                        return arr.GetValue(idx);
                }
                catch { }
                return null;
            }

            var indexType = index.GetType();
            var cacheKey = $"{type.FullName}[]_{indexType.FullName}";

            var getter = _indexerCache.GetOrAdd(cacheKey, k => CreateIndexerAccessor(type, indexType));
            
            return getter?.Invoke(target, index);
        }

        /// <summary>
        /// Gets a nested property value using dot notation (e.g., "User.Address.City")
        /// </summary>
        public static object GetNestedValue(object target, string path)
        {
            if (target == null || string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split('.');
            object current = target;

            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                    return null;

                current = GetValue(current, parts[i]);
            }

            return current;
        }

        /// <summary>
        /// Creates a compiled accessor delegate for a property or field
        /// </summary>
        private static Func<object, object> CreateAccessor(Type type, string memberName)
        {
            // Try Property first
            var prop = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null)
            {
                return CreatePropertyGetter(type, prop);
            }

            // Try Field
            var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (field != null)
            {
                return CreateFieldGetter(type, field);
            }

            return null;
        }

        /// <summary>
        /// Creates a compiled accessor for an indexer
        /// </summary>
        private static Func<object, object, object> CreateIndexerAccessor(Type type, Type indexType)
        {
            // The default indexer name in C# is "Item"
            var indexer = type.GetProperty("Item", BindingFlags.Public | BindingFlags.Instance, null, null, new[] { indexType }, null);
            if (indexer == null)
            {
                // Try searching for any indexer that accepts this type (handling potential inheritance/conversion)
                indexer = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(p => p.GetIndexParameters().Length == 1 && 
                                       p.GetIndexParameters()[0].ParameterType.IsAssignableFrom(indexType));
            }

            if (indexer != null)
            {
                try
                {
                    var targetParam = Expression.Parameter(typeof(object), "target");
                    var indexParam = Expression.Parameter(typeof(object), "index");
                    
                    var castTarget = Expression.Convert(targetParam, type);
                    var castIndex = Expression.Convert(indexParam, indexer.GetIndexParameters()[0].ParameterType);
                    
                    var indexAccess = Expression.MakeIndex(castTarget, indexer, new[] { castIndex });
                    var boxed = Expression.Convert(indexAccess, typeof(object));
                    
                    return Expression.Lambda<Func<object, object, object>>(boxed, targetParam, indexParam).Compile();
                }
                catch
                {
                    return (obj, idx) => indexer.GetValue(obj, new[] { idx });
                }
            }

            return null;
        }

        private static Func<object, object> CreatePropertyGetter(Type type, PropertyInfo prop)
        {
            try
            {
                var objParam = Expression.Parameter(typeof(object), "obj");
                var castExpr = Expression.Convert(objParam, type);
                var propAccess = Expression.Property(castExpr, prop);
                var boxed = Expression.Convert(propAccess, typeof(object));
                return Expression.Lambda<Func<object, object>>(boxed, objParam).Compile();
            }
            catch
            {
                return obj => prop.GetValue(obj, null);
            }
        }

        private static Func<object, object> CreateFieldGetter(Type type, FieldInfo field)
        {
            try
            {
                var objParam = Expression.Parameter(typeof(object), "obj");
                var castExpr = Expression.Convert(objParam, type);
                var fieldAccess = Expression.Field(castExpr, field);
                var boxed = Expression.Convert(fieldAccess, typeof(object));
                return Expression.Lambda<Func<object, object>>(boxed, objParam).Compile();
            }
            catch
            {
                return obj => field.GetValue(obj);
            }
        }

        /// <summary>
        /// Clears all cached getters (useful for testing)
        /// </summary>
        public static void ClearCache()
        {
            _getterCache.Clear();
            _indexerCache.Clear();
        }
    }
}
