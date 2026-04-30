using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ASTTemplateParser
{
    /// <summary>
    /// Represents a read-only variable context for the evaluator.
    /// This allows for hierarchical variable lookup without dictionary copying.
    /// </summary>
    public interface IVariableContext : IEnumerable<KeyValuePair<string, object>>
    {
        bool TryGetValue(string key, out object value);
        object this[string key] { get; }
        bool ContainsKey(string key);
        
        /// <summary>
        /// Creates a child context that can store new variables without affecting the parent.
        /// This is the key to super-fast component and loop rendering.
        /// </summary>
        IVariableContext CreateChild(IDictionary<string, object> localVariables = null);
        
        /// <summary>
        /// Converts to a standard dictionary (used sparingly for NCalc or external APIs)
        /// </summary>
        Dictionary<string, object> ToDictionary();
        
        /// <summary>
        /// Sets a variable in the current local scope.
        /// </summary>
        void SetVariable(string key, object value);
    }

    /// <summary>
    /// Context wrapper for a single dictionary
    /// </summary>
    public sealed class DictionaryVariableContext : IVariableContext
    {
        private readonly IDictionary<string, object> _variables;

        public DictionaryVariableContext(IDictionary<string, object> variables)
        {
            _variables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetValue(string key, out object value) => _variables.TryGetValue(key, out value);

        public object this[string key] => _variables.TryGetValue(key, out var value) ? value : null;

        public bool ContainsKey(string key) => _variables.ContainsKey(key);

        public IVariableContext CreateChild(IDictionary<string, object> localVariables = null)
        {
            return new HierarchicalVariableContext(this, localVariables);
        }

        public Dictionary<string, object> ToDictionary()
        {
            if (_variables is Dictionary<string, object> d && d.Comparer == StringComparer.OrdinalIgnoreCase)
                return new Dictionary<string, object>(d, StringComparer.OrdinalIgnoreCase);
            
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _variables)
                result[kvp.Key] = kvp.Value;
            return result;
        }

        public void SetVariable(string key, object value)
        {
            _variables[key] = PropertyAccessor.ConvertJTokenToNative(value);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _variables.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// Hierarchical context that layers multiple dictionaries without copying them
    /// </summary>
    public sealed class HierarchicalVariableContext : IVariableContext
    {
        private readonly IVariableContext _parent;
        private readonly IDictionary<string, object> _local;

        public HierarchicalVariableContext(IVariableContext parent, IDictionary<string, object> local = null)
        {
            _parent = parent;
            _local = local ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        public bool TryGetValue(string key, out object value)
        {
            // Check local first
            if (_local.TryGetValue(key, out value))
                return true;
            
            // Then check parent
            if (_parent != null)
                return _parent.TryGetValue(key, out value);

            value = null;
            return false;
        }

        public object this[string key] => TryGetValue(key, out var value) ? value : null;

        public bool ContainsKey(string key) => _local.ContainsKey(key) || (_parent != null && _parent.ContainsKey(key));

        public IVariableContext CreateChild(IDictionary<string, object> localVariables = null)
        {
            return new HierarchicalVariableContext(this, localVariables);
        }

        public Dictionary<string, object> ToDictionary()
        {
            var result = _parent != null ? _parent.ToDictionary() : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _local)
                result[kvp.Key] = kvp.Value;
            return result;
        }

        public void SetVariable(string key, object value)
        {
            _local[key] = PropertyAccessor.ConvertJTokenToNative(value);
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            // This is slightly less efficient but rarely used in practice (mostly for NCalc integration)
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _local)
            {
                seenKeys.Add(kvp.Key);
                yield return kvp;
            }
            
            if (_parent != null)
            {
                foreach (var kvp in _parent)
                {
                    if (seenKeys.Add(kvp.Key))
                        yield return kvp;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
