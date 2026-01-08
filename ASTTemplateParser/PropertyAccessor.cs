using System;
using System.Collections.Concurrent;
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
        // Cache for compiled property getters
        private static readonly ConcurrentDictionary<string, Func<object, object>> _getterCache =
            new ConcurrentDictionary<string, Func<object, object>>();

        // Cache for PropertyInfo (fallback)
        private static readonly ConcurrentDictionary<string, PropertyInfo> _propertyCache =
            new ConcurrentDictionary<string, PropertyInfo>();

        /// <summary>
        /// Gets a property value using cached compiled delegates
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object GetValue(object target, string propertyName)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
                return null;

            var type = target.GetType();
            var cacheKey = type.FullName + "." + propertyName;

            var getter = _getterCache.GetOrAdd(cacheKey, k => CreateGetter(type, propertyName));
            
            return getter?.Invoke(target);
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
        /// Creates a compiled getter delegate for the property
        /// </summary>
        private static Func<object, object> CreateGetter(Type type, string propertyName)
        {
            var prop = _propertyCache.GetOrAdd(
                type.FullName + "." + propertyName,
                k => type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            );

            if (prop == null)
                return null;

            try
            {
                // Create compiled expression: (object obj) => (object)((TargetType)obj).PropertyName
                var objParam = System.Linq.Expressions.Expression.Parameter(typeof(object), "obj");
                var castExpr = System.Linq.Expressions.Expression.Convert(objParam, type);
                var propAccess = System.Linq.Expressions.Expression.Property(castExpr, prop);
                var boxed = System.Linq.Expressions.Expression.Convert(propAccess, typeof(object));

                var lambda = System.Linq.Expressions.Expression.Lambda<Func<object, object>>(boxed, objParam);
                return lambda.Compile();
            }
            catch
            {
                // Fallback to reflection if expression compilation fails
                return obj => prop.GetValue(obj, null);
            }
        }

        /// <summary>
        /// Clears all cached getters (useful for testing)
        /// </summary>
        public static void ClearCache()
        {
            _getterCache.Clear();
            _propertyCache.Clear();
        }
    }
}
