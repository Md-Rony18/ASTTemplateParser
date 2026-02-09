using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using NCalc;

namespace ASTTemplateParser
{
    /// <summary>
    /// High-performance AST evaluator with security hardening
    /// </summary>
    public sealed class Evaluator : IAstVisitor
    {
        private readonly StringBuilder _output;
        private readonly Dictionary<string, object> _variables;
        private readonly SecurityConfig _security;
        private int _currentLoopIterations;
        private int _currentRecursionDepth;
        
        // Expression cache for NCalc - stores compiled Expression objects
        private static readonly ConcurrentDictionary<string, Expression> _expressionCache =
            new ConcurrentDictionary<string, Expression>();

        // StringBuilder pool
        private static readonly ConcurrentBag<StringBuilder> _builderPool = 
            new ConcurrentBag<StringBuilder>();
        private const int MaxPoolSize = 16;
        private const int MaxExpressionCacheSize = 1000;

        // Component loader delegate - set by TemplateEngine
        private Func<string, RootNode> _componentLoader;
        
        // Sections defined in the current template (for layouts)
        private Dictionary<string, List<AstNode>> _sections = new Dictionary<string, List<AstNode>>();
        
        // Body content (for layouts)
        private List<AstNode> _bodyContent;
        
        // Slot content passed from parent Include
        private Dictionary<string, List<AstNode>> _slotContent = new Dictionary<string, List<AstNode>>();
        
        // Template fragments defined with <Define> for inline recursion
        private Dictionary<string, DefineNode> _templateFragments = new Dictionary<string, DefineNode>(StringComparer.OrdinalIgnoreCase);

        public Evaluator(Dictionary<string, object> variables = null, SecurityConfig security = null)
        {
            _output = GetBuilder();
            _variables = variables ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _security = security ?? SecurityConfig.Default;
            _currentLoopIterations = 0;
            _currentRecursionDepth = 0;
        }
        
        /// <summary>
        /// Sets the component loader function (called by TemplateEngine)
        /// </summary>
        public void SetComponentLoader(Func<string, RootNode> loader)
        {
            _componentLoader = loader;
        }
        
        // Callback for before Include render
        private Action<IncludeInfo, TemplateEngine> _onBeforeIncludeRender;
        // Callback for after Include render (for wrapping output)
        private Func<IncludeInfo, string, string> _onAfterIncludeRender;
        private TemplateEngine _engine;
        
        /// <summary>
        /// Sets the callbacks that fire before/after each Include component renders
        /// </summary>
        public void SetIncludeCallback(
            Action<IncludeInfo, TemplateEngine> beforeCallback, 
            TemplateEngine engine,
            Func<IncludeInfo, string, string> afterCallback = null)
        {
            _onBeforeIncludeRender = beforeCallback;
            _onAfterIncludeRender = afterCallback;
            _engine = engine;
        }
        
        /// <summary>
        /// Sets section content (for layout rendering)
        /// </summary>
        public void SetSections(Dictionary<string, List<AstNode>> sections)
        {
            _sections = sections ?? new Dictionary<string, List<AstNode>>();
        }
        
        /// <summary>
        /// Sets body content (for layout rendering)
        /// </summary>
        public void SetBodyContent(List<AstNode> body)
        {
            _bodyContent = body;
        }

        /// <summary>
        /// Evaluates AST and returns HTML string
        /// </summary>
        public string Evaluate(RootNode ast)
        {
            _currentRecursionDepth++;
            
            if (_currentRecursionDepth > _security.MaxRecursionDepth)
            {
                throw new TemplateLimitException("RecursionDepth", _security.MaxRecursionDepth, _currentRecursionDepth);
            }

            try
            {
                Visit(ast);
                var result = _output.ToString();
                return result;
            }
            finally
            {
                _currentRecursionDepth--;
                ReturnBuilder(_output);
            }
        }

        #region Visitor Implementation

        public void Visit(RootNode node)
        {
            // Pass 1: Hoist definitions (register all <Define> fragments first)
            foreach (var child in node.Children)
            {
                if (child is DefineNode)
                {
                    child.Accept(this);
                }
            }

            // Pass 2: Render remaining nodes
            foreach (var child in node.Children)
            {
                if (!(child is DefineNode))
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(TextNode node)
        {
            _output.Append(node.Content);
        }

        public void Visit(InterpolationNode node)
        {
            var value = ResolveExpression(node.Expression);
            if (value != null)
            {
                // Security: HTML encode output with full protection (XSS, JS injection)
                if (_security.HtmlEncodeOutput)
                {
                    _output.Append(SecurityUtils.HtmlEncode(value, _security));
                }
                else
                {
                    _output.Append(value.ToString());
                }
            }
        }

        public void Visit(ElementNode node)
        {
            if (node.IsComponent)
            {
                // Render as component with "element/" prefix
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.Parameters, 
                    node.SlotContent, node.NamedSlots, "element");
            }
            else
            {
                foreach (var child in node.Children)
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(DataNode node)
        {
            if (node.IsComponent)
            {
                // Render as component with "data/" prefix
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.Parameters, 
                    node.SlotContent, node.NamedSlots, "data");
            }
            else
            {
                foreach (var child in node.Children)
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(NavNode node)
        {
            if (node.IsComponent)
            {
                // Render as component with "navigation/" prefix
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.Parameters, 
                    node.SlotContent, node.NamedSlots, "navigation");
            }
            else
            {
                foreach (var child in node.Children)
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(BlockNode node)
        {
            if (node.IsComponent)
            {
                // Render as component with "block/" prefix
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.Parameters, 
                    node.SlotContent, node.NamedSlots, "block");
            }
            else
            {
                foreach (var child in node.Children)
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(IfNode node)
        {
            // Security: Validate condition with strict mode
            if (!SecurityUtils.IsConditionSafe(node.Condition, _security))
            {
                throw new TemplateSecurityException(
                    $"Unsafe condition detected", "ExpressionInjection", node.Condition);
            }

            if (EvaluateCondition(node.Condition))
            {
                foreach (var child in node.ThenBranch)
                    child.Accept(this);
                return;
            }

            foreach (var elseIf in node.ElseIfBranches)
            {
                if (!SecurityUtils.IsConditionSafe(elseIf.Condition, _security))
                {
                    throw new TemplateSecurityException(
                        $"Unsafe condition detected", "ExpressionInjection", elseIf.Condition);
                }

                if (EvaluateCondition(elseIf.Condition))
                {
                    foreach (var child in elseIf.Body)
                        child.Accept(this);
                    return;
                }
            }

            foreach (var child in node.ElseBranch)
            {
                child.Accept(this);
            }
        }

        public void Visit(ForEachNode node)
        {
            var collection = ResolveExpression(node.CollectionExpression) as IEnumerable;
            if (collection == null)
                return;

            var varName = node.VariableName;
            object previousValue = null;
            bool hadPrevious = _variables.TryGetValue(varName, out previousValue);
            int iterationCount = 0;

            try
            {
                foreach (var item in collection)
                {
                    // Security: Check loop iteration limit
                    iterationCount++;
                    _currentLoopIterations++;
                    
                    if (_currentLoopIterations > _security.MaxLoopIterations)
                    {
                        throw new TemplateLimitException("LoopIterations", 
                            _security.MaxLoopIterations, _currentLoopIterations);
                    }

                    _variables[varName] = item;

                    foreach (var child in node.Body)
                    {
                        child.Accept(this);
                    }
                }
            }
            finally
            {
                _currentLoopIterations -= iterationCount;
                
                if (hadPrevious)
                    _variables[varName] = previousValue;
                else
                    _variables.Remove(varName);
            }
        }

        #endregion

        #region Expression Evaluation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ResolveExpression(string expression)
        {
            return ResolveExpression(expression, _variables);
        }

        private object ResolveExpression(string expression, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(expression))
                return null;

            expression = expression.Trim();

            // Security: Validate expression with full config
            if (!SecurityUtils.IsExpressionSafe(expression, _security))
            {
                string reason = !SecurityConfig.Default.AllowIndexerAccess && expression.Contains("[") ? "Indexer access disabled" : "Safe characters violation or dangerous pattern";
                throw new TemplateSecurityException(
                    $"Unsafe expression detected: '{expression}'. Reason: {reason}", "ExpressionInjection", expression);
            }

            // Indexer support (e.g., item[key] or item["key"]) - FAST PATH
            if (expression.Contains('[') && expression.Contains(']'))
            {
                return CleanResult(ResolveIndexerAccess(expression, variables));
            }

            // Check for direct variable match first (even with dots)
            // This handles cases like engine.SetVariable("Data.Items", items)
            if (!expression.Contains('('))
            {
                if (variables.TryGetValue(expression, out object directValue))
                {
                    // Security: Check if variable name is blocked
                    if (!_security.BlockedPropertyNames.Contains(expression))
                        return CleanResult(directValue);
                }
            }

            // Simple variable lookup (no dots, no parens)
            if (!expression.Contains('.') && !expression.Contains('(') && !expression.Contains('|'))
            {
                // Already checked via TryGetValue above, if we reach here it's not in the dictionary
                // Fall through to NCalc for potential numeric literals or other simple expressions
            }

            // Pipe support for filters (e.g., {{ Name | uppercase }} or {{ Price | currency:"en-GB" }})
            if (expression.Contains('|'))
            {
                return ResolveFilterExpression(expression, variables);
            }
            
            // Nested property access (contains dots, no parens, no pipes)
            if (expression.Contains('.') && !expression.Contains('(') && !expression.Contains('|'))
            {
                // Security: Validate property path
                if (!SecurityUtils.IsPropertyPathSafe(expression, _security))
                {
                    return null; // Silently block unsafe paths
                }
                return CleanResult(ResolveNestedPath(expression, variables));
            }

            // Complex expression - use NCalc (only if method calls allowed)
            if (!_security.AllowMethodCalls && expression.Contains('('))
            {
                return null; // Block NCalc evaluation if method calls disabled and has parens
            }
            
            var result = EvaluateNCalcExpression(expression, variables);
            return CleanResult(result);
        }

        /// <summary>
        /// Highly optimized indexer resolution for expressions like item[Key] or item.Data[index].Prop
        /// </summary>
        private object ResolveIndexerAccess(string expression, Dictionary<string, object> variables)
        {
            // Find the FIRST [
            int openBracket = expression.IndexOf('[');
            if (openBracket < 0) return null;

            // Find matching ] (handling potential nested brackets)
            int closeBracket = -1;
            int depth = 0;
            for (int i = openBracket; i < expression.Length; i++)
            {
                if (expression[i] == '[') depth++;
                else if (expression[i] == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeBracket = i;
                        break;
                    }
                }
            }

            if (closeBracket < 0) return null;

            string targetPath = expression.Substring(0, openBracket).Trim();
            string keyExpression = expression.Substring(openBracket + 1, closeBracket - openBracket - 1).Trim();
            string remainder = expression.Substring(closeBracket + 1).Trim();

            // 1. Resolve target (could be a simple variable or a nested path)
            object current = ResolveExpression(targetPath, variables);
            if (current == null) return null;

            // 2. Resolve index/key (could be another variable or a literal string/number)
            object index = ResolveExpression(keyExpression, variables);
            if (index == null) return null;

            // Security Check for string keys
            if (index is string s && !SecurityUtils.IsPropertySafe(s, _security))
                return null;

            // 3. Get value from indexer
            current = PropertyAccessor.GetIndexerValue(current, index);

            // 4. Handle remainder (e.g., if expression was item[Key].SubProperty or item[Key][OtherKey])
            if (string.IsNullOrEmpty(remainder))
                return current;

            // Strip leading dot if present, as ResolveFromObject adds it if needed
            string cleanRemainder = remainder.StartsWith(".") ? remainder.Substring(1) : remainder;
            return ResolveFromObject(current, cleanRemainder, variables);
        }

        /// <summary>
        /// Resolves expressions with pipes and filters like {{ Value | filter:"arg1" }}
        /// </summary>
        private object ResolveFilterExpression(string expression, Dictionary<string, object> variables)
        {
            var pipes = expression.Split('|');
            if (pipes.Length == 0) return null;

            // First part is the base value/expression
            object currentResult = ResolveExpression(pipes[0].Trim(), variables);

            // Subsequent parts are filters
            for (int i = 1; i < pipes.Length; i++)
            {
                string filterPart = pipes[i].Trim();
                if (string.IsNullOrEmpty(filterPart)) continue;

                string filterName;
                string[] args = Array.Empty<string>();

                if (filterPart.Contains(':'))
                {
                    int colonIndex = filterPart.IndexOf(':');
                    filterName = filterPart.Substring(0, colonIndex).Trim();
                    string argsRaw = filterPart.Substring(colonIndex + 1).Trim();
                    
                    // Simple argument splitting (comma separated, handling potential quotes)
                    // For now, supporting single argument or simple CSV
                    if (argsRaw.StartsWith("\"") && argsRaw.EndsWith("\""))
                        args = new[] { argsRaw.Substring(1, argsRaw.Length - 2) };
                    else if (argsRaw.StartsWith("'") && argsRaw.EndsWith("'"))
                        args = new[] { argsRaw.Substring(1, argsRaw.Length - 2) };
                    else
                        args = argsRaw.Split(',').Select(a => a.Trim()).ToArray();
                }
                else
                {
                    filterName = filterPart;
                }

                currentResult = TemplateEngine.InvokeFilter(filterName, currentResult, args);
            }

            return currentResult;
        }

        private object ResolveFromObject(object root, string path, Dictionary<string, object> variables)
        {
            if (root == null || string.IsNullOrEmpty(path)) return root;
            
            // Create a temporary context where the root is accessible
            var tempVars = new Dictionary<string, object>(variables, StringComparer.OrdinalIgnoreCase);
            tempVars["__this__"] = root;
            
            string fullPath = "__this__" + (path.StartsWith("[") ? "" : ".") + path;
            return ResolveExpression(fullPath, tempVars);
        }

        /// <summary>
        /// Centralized method to clean resolved values from redundant quotes
        /// </summary>
        private object CleanResult(object result)
        {
            if (result is string s && s.Length >= 2)
            {
                // Strip redundant single quotes
                if (s.StartsWith("'") && s.EndsWith("'"))
                    return s.Substring(1, s.Length - 2).Replace("\\'", "'");
                
                // Strip redundant double quotes
                if (s.StartsWith("\"") && s.EndsWith("\""))
                    return s.Substring(1, s.Length - 2);
            }
            return result;
        }

        private object ResolveNestedPath(string path)
        {
            return ResolveNestedPath(path, _variables);
        }

        private object ResolveNestedPath(string path, Dictionary<string, object> variables, object rootObject = null)
        {
            if (string.IsNullOrEmpty(path)) return rootObject;

            // If we have a root object, we start resolving properties directly from it
            if (rootObject != null)
            {
                var segments = path.Split('.');
                object currentObj = rootObject;
                foreach (var seg in segments)
                {
                    if (currentObj == null) return null;
                    if (!SecurityUtils.IsPropertySafe(seg, _security)) return null;
                    currentObj = PropertyAccessor.GetValue(currentObj, seg);
                }
                return currentObj;
            }

            // 1. Try direct match for the entire path first (fast path for variables-with-dots)
            if (variables.TryGetValue(path, out var directMatch))
            {
                if (!_security.BlockedPropertyNames.Contains(path))
                    return directMatch;
            }

            var parts = path.Split('.');
            
            // Security: Check depth limit
            if (parts.Length > _security.MaxPropertyDepth)
            {
                return null; // Exceed max depth
            }
            
            object current = null;
            int consumedParts = 0;

            // 2. Find the longest prefix that matches a variable
            // This is necessary because variables could have dots like "Data.Items"
            // We start from the longest possible prefix to handle overlapping keys correctly
            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string prefix = (i == 0) ? parts[0] : string.Join(".", parts, 0, i + 1);
                if (variables.TryGetValue(prefix, out current))
                {
                    // Security: Check if variable name is blocked
                    if (_security.BlockedPropertyNames.Contains(prefix))
                    {
                        current = null;
                        continue; // Try shorter prefix or fail
                    }
                    
                    consumedParts = i + 1;
                    break;
                }
            }

            if (current == null)
                return null;

            // 3. Resolve remaining parts as properties
            for (int i = consumedParts; i < parts.Length && current != null; i++)
            {
                // Security: Check each property name
                if (!SecurityUtils.IsPropertySafe(parts[i], _security))
                {
                    return null; // Silently block access to sensitive properties
                }
                
                current = PropertyAccessor.GetValue(current, parts[i]);
            }

            return current;
        }

        private object EvaluateNCalcExpression(string expression)
        {
            return EvaluateNCalcExpression(expression, _variables);
        }

        private object EvaluateNCalcExpression(string expression, Dictionary<string, object> variables)
        {
            try
            {
                // Create new Expression to ensure correct parameter context
                var ncalc = new Expression(expression);
                
                foreach(var kvp in variables) 
                {
                    ncalc.Parameters[kvp.Key] = kvp.Value;
                }

                return ncalc.Evaluate();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Enforces expression cache size limit by clearing oldest entries
        /// </summary>
        private static void EnforceExpressionCacheLimit()
        {
            // Simple strategy: clear 25% of entries when limit reached
            var keysToRemove = _expressionCache.Keys.Take(MaxExpressionCacheSize / 4).ToArray();
            foreach (var key in keysToRemove)
            {
                Expression _;
                _expressionCache.TryRemove(key, out _);
            }
        }

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            condition = condition.Trim();

            // FAST PATH: If condition is just a variable or nested path (no ops, no quotes, no spaces)
            // Example: "menuNode.HasChildren" or "IsActive"
            if (!condition.Contains(' ') && !condition.Contains('(') && !condition.Contains('!') && 
                !condition.Contains('=') && !condition.Contains('<') && !condition.Contains('>') && 
                !condition.Contains('"') && !condition.Contains('\''))
            {
                object val;
                if (condition.Contains('.'))
                {
                    val = ResolveNestedPath(condition);
                }
                else
                {
                    _variables.TryGetValue(condition, out val);
                }
                return IsTruthy(val);
            }

            // Complex expression: use NCalc
            string processedCondition = PreprocessCondition(condition);

            try
            {
                var expr = new Expression(processedCondition);
                
                foreach (var kvp in _variables)
                {
                    expr.Parameters[kvp.Key] = kvp.Value;
                }
                
                // Add common literals for safety
                expr.Parameters["null"] = null;
                expr.Parameters["true"] = true;
                expr.Parameters["false"] = false;

                var result = expr.Evaluate();
                return IsTruthy(result);
            }
            catch
            {
                // Fallback: maybe NCalc failed but we can try basic truthiness if it's still a simple-ish path
                try {
                    var val = ResolveNestedPath(condition);
                    return IsTruthy(val);
                } catch {
                    return false;
                }
            }
        }

        private static bool IsTruthy(object value)
        {
            if (value == null) return false;
            if (value is bool b) return b;
            
            // Numbers: non-zero is true
            if (value is int i) return i != 0;
            if (value is long l) return l != 0;
            if (value is double d) return d != 0;
            if (value is decimal dec) return dec != 0;
            if (value is float f) return f != 0;
            
            // Strings: non-empty and not "false"/"0"/"null"
            if (value is string s)
            {
                if (string.IsNullOrEmpty(s)) return false;
                if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(s, "null", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }
            
            // Collections: non-empty is true
            if (value is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    var hasItems = enumerator.MoveNext();
                    return hasItems;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            }

            return true; // Any other non-null object/class is truthy
        }

        private string PreprocessCondition(string condition)
        {
            // Fast path: no dots means no nested paths to resolve
            if (!condition.Contains('.') && !condition.Contains('"'))
                return condition;

            var sb = GetBuilder();
            try
            {
                int i = 0;
                while (i < condition.Length)
                {
                    char c = condition[i];
                    
                    // Convert double quotes to single quotes inline (avoids string allocation)
                    if (c == '"')
                    {
                        sb.Append('\'');
                        i++;
                    }
                    else if (char.IsLetter(c) || c == '_')
                    {
                        int start = i;
                        while (i < condition.Length && 
                               (char.IsLetterOrDigit(condition[i]) || condition[i] == '_' || condition[i] == '.'))
                        {
                            i++;
                        }

                        string token = condition.Substring(start, i - start);
                        
                        if (token.Contains('.'))
                        {
                            var value = ResolveNestedPath(token);
                            if (value is string s)
                            {
                                // NCalc requires strings to be wrapped in single quotes
                                // Escape internal single quotes and wrap
                                sb.Append("'" + s.Replace("'", "\\'") + "'");
                            }
                            else
                            {
                                sb.Append(FormatValue(value));
                            }
                        }
                        else
                        {
                            sb.Append(token);
                        }
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }
                return sb.ToString();
            }
            finally
            {
                ReturnBuilder(sb);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string FormatValue(object value)
        {
            if (value == null) return "null"; // NCalc null keyword
            if (value is bool b) return b ? "true" : "false";
            
            // Handle numeric types for NCalc math/comparisons
            if (value is int || value is long || value is double || value is decimal || value is float)
                return value.ToString();

            if (value is string s) 
            {
                // For conditions, we need to know if we're outputting a literal or a truthiness check
                // Since this is used within PreprocessCondition for NCalc, 
                // returning the raw string might be dangerous if not quoted.
                return s; 
            }
            
            // Handle collections - check if has any items for truthiness
            if (value is IEnumerable enumerable && !(value is string))
            {
                var enumerator = enumerable.GetEnumerator();
                try
                {
                    bool hasItems = enumerator.MoveNext();
                    return hasItems ? "true" : "false";
                }
                finally
                {
                    (enumerator as IDisposable)?.Dispose();
                }
            }
            
            return "true"; // Object is present
        }

        /// <summary>
        /// Resolves interpolations within a string value (e.g., "Hello {{Name}}!")
        /// </summary>
        private string ResolveInterpolationsInString(string input)
        {
            return ResolveInterpolationsInString(input, _variables);
        }

        private string ResolveInterpolationsInString(string input, Dictionary<string, object> variables)
        {
            if (string.IsNullOrEmpty(input) || !input.Contains("{{"))
                return input;

            var sb = GetBuilder();
            try
            {
                int i = 0;
                while (i < input.Length)
                {
                    if (i < input.Length - 1 && input[i] == '{' && input[i + 1] == '{')
                    {
                        // Found {{, look for closing }}
                        int start = i + 2;
                        int end = input.IndexOf("}}", start);
                        if (end > start)
                        {
                            var varName = input.Substring(start, end - start).Trim();
                            var value = ResolveExpression(varName, variables);
                            string strValue = value?.ToString() ?? string.Empty;
                            
                            // Clean up redundant quotes from interpolated expressions
                            if (strValue.Length >= 2)
                            {
                                if (strValue.StartsWith("'") && strValue.EndsWith("'")) 
                                    strValue = strValue.Substring(1, strValue.Length - 2).Replace("\\'", "'");
                                else if (strValue.StartsWith("\"") && strValue.EndsWith("\""))
                                    strValue = strValue.Substring(1, strValue.Length - 2);
                            }
                            
                            sb.Append(strValue);
                            i = end + 2;
                        }
                        else
                        {
                            // No closing }}, just append the {{
                            sb.Append("{{");
                            i += 2;
                        }
                    }
                    else
                    {
                        sb.Append(input[i]);
                        i++;
                    }
                }
                return sb.ToString();
            }
            finally
            {
                ReturnBuilder(sb);
            }
        }

        #endregion

        #region StringBuilder Pool

        private static StringBuilder GetBuilder()
        {
            if (_builderPool.TryTake(out var sb))
            {
                sb.Clear();
                return sb;
            }
            return new StringBuilder(1024);
        }

        private static void ReturnBuilder(StringBuilder sb)
        {
            if (_builderPool.Count < MaxPoolSize)
            {
                sb.Clear();
                _builderPool.Add(sb);
            }
        }

        #endregion

        #region Component System

        // Maximum component include depth to prevent infinite recursion
        private const int MaxComponentDepth = 10;
        private int _componentDepth = 0;

        public void Visit(IncludeNode node)
        {
            if (_componentLoader == null)
            {
                // No component loader - skip
                return;
            }

            // Security: Check component depth to prevent infinite recursion
            if (_componentDepth >= MaxComponentDepth)
            {
                _output.Append($"<!-- Max component depth exceeded: {node.ComponentPath} -->");
                return;
            }

            // Security: Validate component path
            if (string.IsNullOrEmpty(node.ComponentPath))
                return;

            // Track variables set by callback (only allocated when callback exists)
            HashSet<string> variablesBefore = null;
            HashSet<string> callbackSetVariables = null;
            Dictionary<string, object> engineSnapshot = null;

            try
            {
                
                // Resolve names if they contain interpolations
                string resolvedInstanceName = node.Name;
                if (!string.IsNullOrEmpty(node.Name) && node.Name.Contains("{{") && node.Name.Contains("}}"))
                {
                    resolvedInstanceName = ResolveInterpolationsInString(node.Name);
                }

                string resolvedOldName = node.OldName;
                if (!string.IsNullOrEmpty(node.OldName) && node.OldName.Contains("{{") && node.OldName.Contains("}}"))
                {
                    resolvedOldName = ResolveInterpolationsInString(node.OldName);
                }

                // Fire OnBeforeIncludeRender callback FIRST (before loading component)
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    // Take snapshot of ENGINE variables before callback
                    var currentEngineVars = _engine.GetVariables();
                    engineSnapshot = new Dictionary<string, object>(currentEngineVars, StringComparer.OrdinalIgnoreCase);
                    
                    // Also take snapshot of Evaluator variables for sync logic
                    variablesBefore = new HashSet<string>(_variables.Keys, StringComparer.OrdinalIgnoreCase);
                    
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = node.ComponentPath,
                        ComponentType = "include",
                        Parameters = new Dictionary<string, string>()
                    };
                    
                    // Copy parameters for callback
                    foreach (var p in node.Parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    
                    // Call the callback - user can set variables on engine
                    _onBeforeIncludeRender(includeInfo, _engine);
                }
                
                // Create a container for parameter resolution that includes callback variables
                // This context has access to PARENT variables (for resolution) AND CALLBACK variables
                var paramResolutionVars = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
                
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    var engineVars = _engine.GetVariables();
                    callbackSetVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var kvp in engineVars)
                    {
                        bool isActuallySetByCallback = true;
                        if (engineSnapshot != null && engineSnapshot.TryGetValue(kvp.Key, out var oldValue))
                        {
                            if (Equals(kvp.Value, oldValue))
                            {
                                isActuallySetByCallback = false;
                            }
                        }

                        if (isActuallySetByCallback)
                        {
                            paramResolutionVars[kvp.Key] = kvp.Value;
                            callbackSetVariables.Add(kvp.Key);
                        }
                    }
                }
                
                // Load the component AST
                var componentAst = _componentLoader(node.ComponentPath);
                if (componentAst == null)
                    return;

                // Create the final context for the component
                // Start with parent variables
                var componentVars = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
                
                // CRITICAL: Clear instance-specific variables INHERITED FROM PARENT
                // We do this BEFORE adding callback variables and parameters
                componentVars.Remove("element");
                componentVars.Remove("elementAttributes");
                componentVars.Remove("oldname"); 
                componentVars.Remove("name");

                // Sync callback-set variables for the CHILD
                if (callbackSetVariables != null)
                {
                    var engineVars = _engine.GetVariables();
                    foreach (var varName in callbackSetVariables)
                    {
                        componentVars[varName] = engineVars[varName];
                    }
                }

                // Add standard component variables
                componentVars["name"] = resolvedInstanceName;
                componentVars["oldname"] = resolvedOldName;
                componentVars["path"] = node.ComponentPath;
                componentVars["componentPath"] = node.ComponentPath;
                componentVars["type"] = "include";
                
                // Add parameters as variables - resolve variable references
                // Skip variables that were explicitly set by callback
                // Note: Use componentVars for param resolution to access callback-set variables
                foreach (var param in node.Parameters)
                {
                    // Skip if callback explicitly set this variable
                    if (callbackSetVariables != null && callbackSetVariables.Contains(param.Name))
                        continue;
                    if (!string.IsNullOrEmpty(param.Name))
                    {
                        var paramValue = param.Value;
                        object resolvedValue = null;
                        
                        if (string.IsNullOrEmpty(paramValue))
                        {
                            // No value provided - use default if available
                            resolvedValue = null;
                        }
                        // Check for {{variable}} interpolation syntax
                        else if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                        {
                            // Extract variable name from {{variableName}}
                            var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                            // Use componentVars for resolution to access callback-set variables
                            resolvedValue = ResolveExpression(varName, paramResolutionVars);
                        }
                        // Check if value contains interpolations for mixed content like "Hello {{Name}}!"
                        else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                        {
                            // Process interpolations within the string using componentVars context
                            var interpolatedValue = ResolveInterpolationsInString(paramValue, paramResolutionVars);
                            // Check if interpolation resulted in empty/incomplete string
                            resolvedValue = string.IsNullOrEmpty(interpolatedValue) ? null : interpolatedValue;
                        }
                        // Check if value is a simple variable reference (no spaces, quotes)
                        else if (!paramValue.Contains(" ") && 
                                 !paramValue.StartsWith("\"") && 
                                 !paramValue.StartsWith("'"))
                        {
                            // Try to resolve as expression (supports nested paths like item.Children)
                            resolvedValue = ResolveExpression(paramValue, paramResolutionVars);
                            if (resolvedValue == null)
                            {
                                // Fall back to literal value
                                resolvedValue = paramValue;
                            }
                        }
                        else
                        {
                            // Use as literal string value
                            resolvedValue = paramValue;
                        }
                        
                        // Apply default value if resolved value is null or empty
                        if (resolvedValue == null || 
                            (resolvedValue is string s && string.IsNullOrEmpty(s)))
                        {
                            if (!string.IsNullOrEmpty(param.Default))
                            {
                                // Default value can also contain interpolations
                                if (param.Default.Contains("{{"))
                                {
                                    resolvedValue = ResolveInterpolationsInString(param.Default, paramResolutionVars);
                                }
                                else
                                {
                                    resolvedValue = param.Default;
                                }
                            }
                            else
                            {
                                resolvedValue = string.Empty;
                            }
                        }
                        
                        componentVars[param.Name] = resolvedValue;
                    }
                }

                var componentEvaluator = new Evaluator(componentVars, _security);
                componentEvaluator.SetComponentLoader(_componentLoader);
                componentEvaluator._componentDepth = _componentDepth + 1; // Increment depth
                
                // Pass callbacks to child evaluator
                if (_onBeforeIncludeRender != null || _onAfterIncludeRender != null)
                {
                    componentEvaluator.SetIncludeCallback(_onBeforeIncludeRender, _engine, _onAfterIncludeRender);
                }
                
                // Don't pass slot content for now to avoid recursion issues
                // Slots will be a v2 feature

                // Evaluate the component
                var result = componentEvaluator.Evaluate(componentAst);
                
                // Fire OnAfterIncludeRender callback if configured (for wrapping)
                if (_onAfterIncludeRender != null)
                {
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = node.ComponentPath,
                        ComponentType = "include",
                        Parameters = new Dictionary<string, string>()
                    };
                    foreach (var p in node.Parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    
                    // Call callback - user can wrap/modify the output
                    result = _onAfterIncludeRender(includeInfo, result);
                }
                
                _output.Append(result);
            }
            catch (Exception ex)
            {
                // Output error as HTML comment for debugging
                _output.Append($"<!-- Component Error [{node.ComponentPath}]: {ex.Message} -->");
            }
            finally
            {
                // CRITICAL: Restore engine variables to snapshot to prevent leaking to siblings
                if (engineSnapshot != null && _engine != null)
                {
                    var engineVars = _engine.GetVariables();
                    engineVars.Clear();
                    foreach (var kvp in engineSnapshot)
                    {
                        engineVars[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        /// <summary>
        /// Renders a type-specific component with auto path prefix
        /// </summary>
        /// <param name="componentPath">Component path (e.g., "subTitle")</param>
        /// <param name="name">Component instance name for caching</param>
        /// <param name="parameters">Parameters to pass to the component</param>
        /// <param name="slotContent">Default slot content</param>
        /// <param name="namedSlots">Named slot content</param>
        /// <param name="typePrefix">Type prefix (e.g., "element", "block", "data", "navigation")</param>
        private void RenderTypedComponent(
            string componentPath, 
            string name, 
            string oldName,
            List<ParamData> parameters,
            List<AstNode> slotContent,
            Dictionary<string, List<AstNode>> namedSlots,
            string typePrefix)
        {
            if (_componentLoader == null)
            {
                // No component loader - skip
                return;
            }

            // Security: Check component depth to prevent infinite recursion
            if (_componentDepth >= MaxComponentDepth)
            {
                _output.Append($"<!-- Max component depth exceeded: {typePrefix}/{componentPath} -->");
                return;
            }

            // Security: Validate component path
            if (string.IsNullOrEmpty(componentPath))
                return;

            // Build full component path with type prefix
            var fullPath = $"{typePrefix}/{componentPath}";

            // Resolve name if it contains interpolations
            string resolvedInstanceName = name;
            if (!string.IsNullOrEmpty(name) && name.Contains("{{") && name.Contains("}}"))
            {
                resolvedInstanceName = ResolveInterpolationsInString(name);
            }

            string resolvedOldName = oldName;
            if (!string.IsNullOrEmpty(oldName) && oldName.Contains("{{") && oldName.Contains("}}"))
            {
                resolvedOldName = ResolveInterpolationsInString(oldName);
            }

            // Track variables set by callback (only allocated when callback exists)
            HashSet<string> variablesBefore = null;
            HashSet<string> callbackSetVariables = null;
            Dictionary<string, object> engineSnapshot = null;

            try
            {
                
                // Fire OnBeforeIncludeRender callback FIRST (before loading component)
                // This allows user to set data for components that may not have a file
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    // Take snapshot of ENGINE variables before callback
                    var currentEngineVars = _engine.GetVariables();
                    engineSnapshot = new Dictionary<string, object>(currentEngineVars, StringComparer.OrdinalIgnoreCase);
                    
                    // Also take snapshot of Evaluator variables for sync logic
                    variablesBefore = new HashSet<string>(_variables.Keys, StringComparer.OrdinalIgnoreCase);
                    
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = fullPath,
                        ComponentType = typePrefix,
                        Parameters = new Dictionary<string, string>()
                    };
                    
                    // Copy parameters for callback
                    foreach (var p in parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    
                    // Call the callback - user can set variables on engine
                    _onBeforeIncludeRender(includeInfo, _engine);
                }
                    
                // Create a container for parameter resolution that includes callback variables
                // This context has access to PARENT variables (for resolution) AND CALLBACK variables
                var paramResolutionVars = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);

                // Sync ALL engine variables to resolution context before resolving parameters
                // Note: We use engine state after callback to resolve parameters
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    var engineVars = _engine.GetVariables();
                    callbackSetVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var kvp in engineVars)
                    {
                        bool isActuallySetByCallback = true;
                        if (engineSnapshot != null && engineSnapshot.TryGetValue(kvp.Key, out var oldValue))
                        {
                            if (Equals(kvp.Value, oldValue))
                            {
                                isActuallySetByCallback = false;
                            }
                        }

                        if (isActuallySetByCallback)
                        {
                            paramResolutionVars[kvp.Key] = kvp.Value; 
                            callbackSetVariables.Add(kvp.Key);
                        }
                    }
                }
                
                // Load the component AST
                var componentAst = _componentLoader(fullPath);
                if (componentAst == null)
                    return;

                // Create the final context for the component
                var componentVars = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
                
                // CRITICAL: Clear instance-specific variables INHERITED FROM PARENT
                componentVars.Remove("element");
                componentVars.Remove("elementAttributes");
                componentVars.Remove("oldname");
                componentVars.Remove("name");

                // Sync callback-set variables for the CHILD
                if (callbackSetVariables != null)
                {
                    var engineVars = _engine.GetVariables();
                    foreach (var varName in callbackSetVariables)
                    {
                        componentVars[varName] = engineVars[varName];
                    }
                }
                
                // Add standard component variables
                componentVars["name"] = resolvedInstanceName;
                componentVars["oldname"] = resolvedOldName;
                componentVars["path"] = fullPath;
                componentVars["componentPath"] = componentPath;
                componentVars["type"] = typePrefix;
                
                // Add parameters as variables - resolve variable references
                // Skip variables that were explicitly set by callback
                foreach (var param in parameters)
                {
                    // Skip if callback explicitly set this variable
                    if (callbackSetVariables != null && callbackSetVariables.Contains(param.Name))
                        continue;
                    if (!string.IsNullOrEmpty(param.Name))
                    {
                        var paramValue = param.Value;
                        object resolvedValue = null;
                        
                        if (string.IsNullOrEmpty(paramValue))
                        {
                            // No value provided - use default if available
                            resolvedValue = null;
                        }
                        // Check for {{variable}} interpolation syntax
                        else if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                        {
                            // Extract variable name from {{variableName}}
                            var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                            resolvedValue = ResolveExpression(varName, paramResolutionVars);
                        }
                        // Check if value contains interpolations for mixed content like "Hello {{Name}}!"
                        else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                        {
                            // Process interpolations within the string
                            var interpolatedValue = ResolveInterpolationsInString(paramValue, paramResolutionVars);
                            // DEBUG: Log interpolation result
                            System.Diagnostics.Debug.WriteLine($"[DEBUG] Param '{param.Name}' value '{paramValue}' resolved to '{interpolatedValue}'");
                            // Check if interpolation resulted in empty/incomplete string
                            resolvedValue = string.IsNullOrEmpty(interpolatedValue) ? null : interpolatedValue;
                        }
                        // Check if value is a simple variable reference (no spaces, quotes)
                        else if (!paramValue.Contains(" ") && 
                                 !paramValue.StartsWith("\"") && 
                                 !paramValue.StartsWith("'"))
                        {
                            // Try to resolve as expression (supports nested paths like item.Children)
                            resolvedValue = ResolveExpression(paramValue, paramResolutionVars);
                            if (resolvedValue == null)
                            {
                                // Fall back to literal value
                                resolvedValue = paramValue;
                            }
                        }
                        else
                        {
                            // Use as literal string value
                            resolvedValue = paramValue;
                        }
                        
                        // Apply default value if resolved value is null or empty
                        if (resolvedValue == null || 
                            (resolvedValue is string s && string.IsNullOrEmpty(s)))
                        {
                            if (!string.IsNullOrEmpty(param.Default))
                            {
                                // Default value can also contain interpolations
                                if (param.Default.Contains("{{"))
                                {
                                    resolvedValue = ResolveInterpolationsInString(param.Default, paramResolutionVars);
                                }
                                else
                                {
                                    resolvedValue = param.Default;
                                }
                            }
                            else
                            {
                                resolvedValue = string.Empty;
                            }
                        }
                        
                        // DEBUG: Log final resolved value
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Param '{param.Name}' final value: '{resolvedValue}'");
                        componentVars[param.Name] = resolvedValue;
                    }
                }

                var componentEvaluator = new Evaluator(componentVars, _security);
                componentEvaluator.SetComponentLoader(_componentLoader);
                componentEvaluator._componentDepth = _componentDepth + 1; // Increment depth
                
                // Pass callbacks if configured
                if (_onBeforeIncludeRender != null || _onAfterIncludeRender != null)
                {
                    componentEvaluator.SetIncludeCallback(_onBeforeIncludeRender, _engine, _onAfterIncludeRender);
                }
                
                // Don't pass slot content for now to avoid recursion issues
                // Slots will be a v2 feature

                // Evaluate the component
                var result = componentEvaluator.Evaluate(componentAst);
                
                // Fire OnAfterIncludeRender callback if configured (for wrapping)
                if (_onAfterIncludeRender != null)
                {
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = fullPath,
                        ComponentType = typePrefix,
                        Parameters = new Dictionary<string, string>()
                    };
                    foreach (var p in parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    
                    // Call callback - user can wrap/modify the output
                    result = _onAfterIncludeRender(includeInfo, result);
                }
                
                _output.Append(result);
            }
            catch (Exception ex)
            {
                // Output error as HTML comment for debugging
                _output.Append($"<!-- Component Error [{fullPath}]: {ex.Message} -->");
            }
            finally
            {
                // CRITICAL: Restore engine variables to snapshot to prevent leaking to siblings
                if (engineSnapshot != null && _engine != null)
                {
                    var engineVars = _engine.GetVariables();
                    engineVars.Clear();
                    foreach (var kvp in engineSnapshot)
                    {
                        engineVars[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        #endregion

        #region Layout System

        public void Visit(LayoutNode node)
        {
            // Store sections and body, then render the layout
            // This is typically handled by TemplateEngine, not directly
            foreach (var child in node.Children)
            {
                if (child is SectionNode section)
                {
                    _sections[section.Name] = section.Children;
                }
                else
                {
                    // Non-section content becomes body
                    if (_bodyContent == null)
                        _bodyContent = new List<AstNode>();
                    _bodyContent.Add(child);
                }
            }
        }

        public void Visit(SectionNode node)
        {
            // When rendering layout page content directly, just output section content
            foreach (var child in node.Children)
            {
                child.Accept(this);
            }
        }

        public void Visit(RenderSectionNode node)
        {
            // Render the named section if it exists
            if (_sections != null && _sections.TryGetValue(node.Name, out var sectionContent))
            {
                foreach (var child in sectionContent)
                {
                    child.Accept(this);
                }
            }
            else if (node.DefaultContent.Count > 0)
            {
                // Render default content if section not provided
                foreach (var child in node.DefaultContent)
                {
                    child.Accept(this);
                }
            }
            else if (node.Required)
            {
                // Required section missing - could throw or log
                _output.Append($"<!-- Missing required section: {node.Name} -->");
            }
        }

        public void Visit(RenderBodyNode node)
        {
            // Render the body content
            if (_bodyContent != null)
            {
                foreach (var child in _bodyContent)
                {
                    child.Accept(this);
                }
            }
        }

        public void Visit(SlotNode node)
        {
            // Check if slot content was provided from parent Include
            if (_slotContent.TryGetValue(node.Name, out var content) && content.Count > 0)
            {
                foreach (var child in content)
                {
                    child.Accept(this);
                }
            }
            else
            {
                // Use default slot content
                foreach (var child in node.DefaultContent)
                {
                    child.Accept(this);
                }
            }
        }

        #endregion

        #region Template Fragments (Inline Recursion)

        public void Visit(DefineNode node)
        {
            // Store the fragment definition for later use by Render tags
            // Don't render anything - just register the fragment
            if (!string.IsNullOrEmpty(node.Name))
            {
                _templateFragments[node.Name] = node;
            }
        }

        public void Visit(RenderNode node)
        {
            // Find the fragment definition
            if (string.IsNullOrEmpty(node.FragmentName) || 
                !_templateFragments.TryGetValue(node.FragmentName, out var fragment))
            {
                // Fragment not found - output error comment
                _output.Append($"<!-- Fragment '{node.FragmentName}' not defined -->");
                return;
            }

            // Security: Check recursion depth
            _currentRecursionDepth++;
            if (_currentRecursionDepth > _security.MaxRecursionDepth)
            {
                _currentRecursionDepth--;
                throw new TemplateLimitException("RecursionDepth", 
                    _security.MaxRecursionDepth, _currentRecursionDepth);
            }

            try
            {
                // Save current variable context
                var savedVariables = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);

                // Set parameters as variables (similar to component param resolution)
                foreach (var param in node.Parameters)
                {
                    if (string.IsNullOrEmpty(param.Name))
                        continue;

                    object resolvedValue = null;
                    var paramValue = param.Value;

                    if (string.IsNullOrEmpty(paramValue))
                    {
                        resolvedValue = null;
                    }
                    // Check for {{variable}} interpolation syntax
                    else if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                    {
                        var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                        resolvedValue = ResolveExpression(varName, _variables);
                    }
                    // Check if value contains interpolations (mixed content)
                    else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                    {
                        resolvedValue = ResolveInterpolationsInString(paramValue, _variables);
                    }
                    // Check if value is a simple variable/expression reference
                    else if (!paramValue.Contains(" ") && 
                             !paramValue.StartsWith("\"") && 
                             !paramValue.StartsWith("'"))
                    {
                        resolvedValue = ResolveExpression(paramValue, _variables);
                        if (resolvedValue == null)
                        {
                            resolvedValue = paramValue;
                        }
                    }
                    else
                    {
                        // Use as literal value
                        resolvedValue = paramValue;
                    }

                    // Apply default value if resolved value is null or empty
                    if (resolvedValue == null || (resolvedValue is string s && string.IsNullOrEmpty(s)))
                    {
                        if (!string.IsNullOrEmpty(param.Default))
                        {
                            if (param.Default.Contains("{{"))
                            {
                                resolvedValue = ResolveInterpolationsInString(param.Default, _variables);
                            }
                            else
                            {
                                resolvedValue = param.Default;
                            }
                        }
                        else
                        {
                            resolvedValue = string.Empty;
                        }
                    }

                    _variables[param.Name] = resolvedValue;
                }

                // Render the fragment body
                foreach (var child in fragment.Body)
                {
                    child.Accept(this);
                }

                // Restore variable context
                _variables.Clear();
                foreach (var kvp in savedVariables)
                {
                    _variables[kvp.Key] = kvp.Value;
                }
            }
            finally
            {
                _currentRecursionDepth--;
            }
        }

        #endregion

        /// <summary>
        /// Clears expression cache (useful for security updates)
        /// </summary>
        public static void ClearExpressionCache()
        {
            _expressionCache.Clear();
        }
    }
}

