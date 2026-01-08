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
            foreach (var child in node.Children)
            {
                child.Accept(this);
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
            foreach (var child in node.Children)
            {
                child.Accept(this);
            }
        }

        public void Visit(DataNode node)
        {
            foreach (var child in node.Children)
            {
                child.Accept(this);
            }
        }

        public void Visit(NavNode node)
        {
            foreach (var child in node.Children)
            {
                child.Accept(this);
            }
        }

        public void Visit(BlockNode node)
        {
            foreach (var child in node.Children)
            {
                child.Accept(this);
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
            if (string.IsNullOrEmpty(expression))
                return null;

            // Security: Validate expression with full config
            if (!SecurityUtils.IsExpressionSafe(expression, _security))
            {
                throw new TemplateSecurityException(
                    "Unsafe expression detected", "ExpressionInjection", expression);
            }

            // Simple variable lookup
            if (!expression.Contains('.') && !expression.Contains('('))
            {
                // Security: Check variable name
                if (_security.BlockedPropertyNames.Contains(expression))
                    return null;
                    
                object value;
                return _variables.TryGetValue(expression, out value) ? value : null;
            }

            // Nested property access
            if (expression.Contains('.') && !expression.Contains('('))
            {
                // Security: Validate property path
                if (!SecurityUtils.IsPropertyPathSafe(expression, _security))
                {
                    return null; // Silently block unsafe paths
                }
                return ResolveNestedPath(expression);
            }

            // Complex expression - use NCalc (only if method calls allowed)
            if (!_security.AllowMethodCalls)
            {
                return null; // Block NCalc evaluation if method calls disabled
            }
            return EvaluateNCalcExpression(expression);
        }

        private object ResolveNestedPath(string path)
        {
            var parts = path.Split('.');
            
            // Security: Check depth limit
            if (parts.Length > _security.MaxPropertyDepth)
            {
                return null; // Exceed max depth
            }
            
            object current;
            if (!_variables.TryGetValue(parts[0], out current))
                return null;

            for (int i = 1; i < parts.Length && current != null; i++)
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
            try
            {
                // Prevent unbounded cache growth - simple eviction when limit exceeded
                if (_expressionCache.Count >= MaxExpressionCacheSize)
                {
                    EnforceExpressionCacheLimit();
                }

                // Get or create cached Expression (parsing is expensive)
                var ncalc = _expressionCache.GetOrAdd(expression, expr => new Expression(expr));
                
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

            condition = PreprocessCondition(condition);

            try
            {
                var expr = new Expression(condition);
                
                foreach (var kvp in _variables)
                {
                    expr.Parameters[kvp.Key] = kvp.Value;
                }

                var result = expr.Evaluate();
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
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
                            sb.Append(FormatValue(value));
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
            if (value == null) return "null";
            if (value is bool b) return b ? "true" : "false";
            if (value is string s) return "'" + s.Replace("'", "\\'") + "'"; // Escape quotes
            return value.ToString();
        }

        /// <summary>
        /// Resolves interpolations within a string value (e.g., "Hello {{Name}}!")
        /// </summary>
        private string ResolveInterpolationsInString(string input)
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
                            var value = ResolveExpression(varName);
                            sb.Append(value?.ToString() ?? string.Empty);
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

            try
            {
                // Load the component AST
                var componentAst = _componentLoader(node.ComponentPath);
                if (componentAst == null)
                    return;

                // Create a new evaluator for the component with inherited variables
                var componentVars = new Dictionary<string, object>(_variables, StringComparer.OrdinalIgnoreCase);
                
                // Add parameters as variables - resolve variable references
                foreach (var param in node.Parameters)
                {
                    if (!string.IsNullOrEmpty(param.Name))
                    {
                        var paramValue = param.Value;
                        
                        if (string.IsNullOrEmpty(paramValue))
                        {
                            componentVars[param.Name] = string.Empty;
                            continue;
                        }
                        
                        // Check for {{variable}} interpolation syntax
                        if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                        {
                            // Extract variable name from {{variableName}}
                            var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                            var resolved = ResolveExpression(varName);
                            componentVars[param.Name] = resolved ?? string.Empty;
                        }
                        // Check if value contains interpolations for mixed content like "Hello {{Name}}!"
                        else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                        {
                            // Process interpolations within the string
                            var interpolatedValue = ResolveInterpolationsInString(paramValue);
                            componentVars[param.Name] = interpolatedValue;
                        }
                        // Check if value is a simple variable reference (no spaces, quotes)
                        else if (!paramValue.Contains(" ") && 
                                 !paramValue.StartsWith("\"") && 
                                 !paramValue.StartsWith("'"))
                        {
                            // Try to resolve as expression (supports nested paths like item.Children)
                            var resolved = ResolveExpression(paramValue);
                            if (resolved != null)
                            {
                                componentVars[param.Name] = resolved;
                            }
                            else
                            {
                                // Fall back to literal value
                                componentVars[param.Name] = paramValue;
                            }
                        }
                        else
                        {
                            // Use as literal string value
                            componentVars[param.Name] = paramValue;
                        }
                    }
                }

                var componentEvaluator = new Evaluator(componentVars, _security);
                componentEvaluator.SetComponentLoader(_componentLoader);
                componentEvaluator._componentDepth = _componentDepth + 1; // Increment depth
                
                // Don't pass slot content for now to avoid recursion issues
                // Slots will be a v2 feature

                // Evaluate the component
                var result = componentEvaluator.Evaluate(componentAst);
                _output.Append(result);
            }
            catch (Exception)
            {
                // Silently fail in release, log in debug
                #if DEBUG
                // In debug builds, reparse to get exception details
                #endif
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

        /// <summary>
        /// Clears expression cache (useful for security updates)
        /// </summary>
        public static void ClearExpressionCache()
        {
            _expressionCache.Clear();
        }
    }
}
