using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        // ============ JToken Support (Newtonsoft.Json via reflection) ============
        // Lazily detected — no hard dependency on Newtonsoft.Json
        private static bool _jTokenChecked;
        private static Type _jTokenType;
        private static Type _jObjectType;
        private static Type _jArrayType;
        private static Type _jValueType;
        private static MethodInfo _jTokenIndexerString;  // JToken this[string]
        private static MethodInfo _jTokenIndexerInt;     // JToken this[int]
        private static PropertyInfo _jTokenTypeProp;     // JToken.Type
        private static PropertyInfo _jValueValueProp;    // JValue.Value

        /// <summary>
        /// Lazily detect Newtonsoft.Json types via reflection (one-time cost)
        /// </summary>
        private static void EnsureJTokenTypes()
        {
            if (_jTokenChecked) return;
            _jTokenChecked = true;

            try
            {
                // Try to find Newtonsoft.Json.Linq.JToken in loaded assemblies
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Newtonsoft.Json") continue;

                    _jTokenType = asm.GetType("Newtonsoft.Json.Linq.JToken");
                    _jObjectType = asm.GetType("Newtonsoft.Json.Linq.JObject");
                    _jArrayType = asm.GetType("Newtonsoft.Json.Linq.JArray");
                    _jValueType = asm.GetType("Newtonsoft.Json.Linq.JValue");

                    if (_jObjectType != null)
                    {
                        // JObject has indexer: JToken this[string propertyName]
                        _jTokenIndexerString = _jObjectType.GetProperty("Item", new[] { typeof(string) })?.GetGetMethod();
                    }

                    if (_jArrayType != null)
                    {
                        // JArray has indexer: JToken this[int index]
                        _jTokenIndexerInt = _jArrayType.GetProperty("Item", new[] { typeof(int) })?.GetGetMethod();
                    }

                    if (_jTokenType != null)
                    {
                        // JToken.Type property (returns JTokenType enum)
                        _jTokenTypeProp = _jTokenType.GetProperty("Type");
                    }

                    if (_jValueType != null)
                    {
                        // JValue.Value property (returns the underlying .NET value)
                        _jValueValueProp = _jValueType.GetProperty("Value");
                    }

                    break;
                }
            }
            catch
            {
                // If reflection fails, JToken support is simply disabled
            }
        }

        /// <summary>
        /// Checks if an object is a Newtonsoft JToken-derived type
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsJToken(object target)
        {
            EnsureJTokenTypes();
            return _jTokenType != null && _jTokenType.IsInstanceOfType(target);
        }

        /// <summary>
        /// Unwraps a JToken to its .NET equivalent value:
        /// - JValue → underlying .NET primitive (string, int, bool, etc.)
        /// - JObject → the JObject itself (for further property access)
        /// - JArray → the JArray itself (for ForEach iteration)
        /// - null JToken → null
        /// </summary>
        private static object UnwrapJToken(object token)
        {
            if (token == null) return null;
            if (_jTokenType == null || !_jTokenType.IsInstanceOfType(token)) return token;

            // Check if it's a JValue (leaf node) — unwrap to .NET value
            if (_jValueType != null && _jValueType.IsInstanceOfType(token))
            {
                return _jValueValueProp?.GetValue(token);
            }

            // JObject or JArray — return as-is for further navigation
            return token;
        }

        /// <summary>
        /// Gets a property from a JObject by name, or an element from a JArray by index
        /// </summary>
        private static object GetJTokenValue(object target, string memberName)
        {
            if (_jObjectType != null && _jObjectType.IsInstanceOfType(target))
            {
                // JObject — use string indexer
                var result = _jTokenIndexerString?.Invoke(target, new object[] { memberName });
                return UnwrapJToken(result);
            }

            if (_jArrayType != null && _jArrayType.IsInstanceOfType(target))
            {
                // JArray — try integer index
                if (int.TryParse(memberName, out int index))
                {
                    var result = _jTokenIndexerInt?.Invoke(target, new object[] { index });
                    return UnwrapJToken(result);
                }
                // JArray also supports .Count etc. — fall through to reflection
            }

            return null;
        }

        /// <summary>
        /// Gets a value from a JObject/JArray by index key
        /// </summary>
        private static object GetJTokenIndexerValue(object target, object index)
        {
            if (_jObjectType != null && _jObjectType.IsInstanceOfType(target) && index is string key)
            {
                var result = _jTokenIndexerString?.Invoke(target, new object[] { key });
                return UnwrapJToken(result);
            }

            if (_jArrayType != null && _jArrayType.IsInstanceOfType(target))
            {
                try
                {
                    int idx = Convert.ToInt32(index);
                    var result = _jTokenIndexerInt?.Invoke(target, new object[] { idx });
                    return UnwrapJToken(result);
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Converts a JToken to a native .NET collection for template engine compatibility.
        /// JArray → List&lt;object&gt; (each element unwrapped)
        /// JObject → Dictionary&lt;string, object&gt; (each value unwrapped)
        /// JValue → underlying .NET value
        /// </summary>
        public static object ConvertJTokenToNative(object token)
        {
            EnsureJTokenTypes();
            if (token == null || _jTokenType == null || !_jTokenType.IsInstanceOfType(token))
                return token;

            // JValue — return underlying value
            if (_jValueType != null && _jValueType.IsInstanceOfType(token))
            {
                return _jValueValueProp?.GetValue(token);
            }

            // JArray — convert to List<object>
            if (_jArrayType != null && _jArrayType.IsInstanceOfType(token))
            {
                var list = new List<object>();
                foreach (var item in (IEnumerable)token)
                {
                    list.Add(ConvertJTokenToNative(item));
                }
                return list;
            }

            // JObject — convert to Dictionary<string, object>
            if (_jObjectType != null && _jObjectType.IsInstanceOfType(token))
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                // JObject when cast to IEnumerable yields JProperty objects (not KeyValuePair)
                // JProperty has .Name (string) and .Value (JToken)
                foreach (var item in (IEnumerable)token)
                {
                    var itemType = item.GetType();
                    
                    // Try JProperty style: .Name and .Value
                    var nameProp = itemType.GetProperty("Name");
                    var valueProp = itemType.GetProperty("Value");
                    
                    if (nameProp != null && valueProp != null)
                    {
                        var key = nameProp.GetValue(item)?.ToString();
                        var value = valueProp.GetValue(item);
                        if (key != null)
                        {
                            dict[key] = ConvertJTokenToNative(value);
                        }
                    }
                    else
                    {
                        // Fallback: try KeyValuePair<string, JToken> style
                        var keyProp = itemType.GetProperty("Key");
                        var valProp2 = itemType.GetProperty("Value");
                        if (keyProp != null && valProp2 != null)
                        {
                            var key = keyProp.GetValue(item)?.ToString();
                            var value = valProp2.GetValue(item);
                            if (key != null)
                            {
                                dict[key] = ConvertJTokenToNative(value);
                            }
                        }
                    }
                }
                return dict;
            }

            return token;
        }

        /// <summary>
        /// Gets a property or field value using cached compiled delegates or dictionary lookup.
        /// Supports: IDictionary, Newtonsoft JObject/JArray/JValue, POCO objects, anonymous types.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object GetValue(object target, string memberName)
        {
            if (target == null || string.IsNullOrEmpty(memberName))
                return null;

            // 1. Support IDictionary (Dictionaries, ExpandoObject, etc.)
            if (target is IDictionary dict)
            {
                // Note: We use the dictionary's own lookup mechanism for performance.
                // For case-insensitivity, the input dictionary should be created with 
                // StringComparer.OrdinalIgnoreCase.
                if (dict.Contains(memberName))
                    return dict[memberName];
                
                return null;
            }

            // 2. Support Newtonsoft.Json JObject/JArray (via reflection — no hard dependency)
            if (IsJToken(target))
            {
                return GetJTokenValue(target, memberName);
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

            // Support Newtonsoft.Json JObject/JArray (via reflection — no hard dependency)
            if (IsJToken(target))
            {
                return GetJTokenIndexerValue(target, index);
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
