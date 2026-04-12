using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NCalc;

namespace ASTTemplateParser
{
    /// <summary>
    /// High-performance AST evaluator with security hardening
    /// </summary>
    public sealed class Evaluator : IAstVisitor
    {
        private readonly StringBuilder _output;
        private IVariableContext _variables;
        private readonly SecurityConfig _security;
        private int _currentLoopIterations;
        private int _currentRecursionDepth;
        
        // Expression cache for NCalc - stores cached parsed LogicalExpression trees
        private static readonly ConcurrentDictionary<string, NCalc.Domain.LogicalExpression> _expressionCache =
            new ConcurrentDictionary<string, NCalc.Domain.LogicalExpression>();

        // StringBuilder pool - tiered by size for adaptive reuse
        private static readonly ConcurrentBag<StringBuilder> _builderPoolSmall = 
            new ConcurrentBag<StringBuilder>();
        private static readonly ConcurrentBag<StringBuilder> _builderPoolLarge = 
            new ConcurrentBag<StringBuilder>();
        private const int MaxPoolSize = 16;
        private const int MaxExpressionCacheSize = 1000;
        private const int SmallBuilderThreshold = 4096;

        // Master expression cache to avoid repeated string parsing and scanning
        private static readonly ConcurrentDictionary<string, CompiledExpression> _masterCache = 
            new ConcurrentDictionary<string, CompiledExpression>();

        private enum ExpressionCategory { Simple, Nested, Ternary, NullCoalescing, Comparison, Filter, NCalc, Literal, Indexer }

        private class CompiledExpression
        {
            public ExpressionCategory Category;
            public object LiteralValue;
            public string SimpleName;
            public string[] PathParts;
            public TernaryParts Ternary;
            public NullCoalescingParts NullCoalescing;
            public ComparisonParts Comparison;
            public bool IsSafe;
        }

        private struct TernaryParts { public string Condition; public string WhenTrue; public string WhenFalse; }
        private struct NullCoalescingParts { public string Left; public string Right; }
        private struct ComparisonParts { public string Left; public string Right; public string Operator; }

        // Template fragments defined with <Define> for inline recursion
        private Dictionary<string, DefineNode> _templateFragments = new Dictionary<string, DefineNode>(StringComparer.OrdinalIgnoreCase);

        // Component loader delegate - set by TemplateEngine
        private Func<string, RootNode> _componentLoader;
        
        // Sections defined in the current template (for layouts)
        private Dictionary<string, List<AstNode>> _sections = new Dictionary<string, List<AstNode>>();
        
        // Body content (for layouts)
        private List<AstNode> _bodyContent;
        
        // Slot content passed from parent Include
        private Dictionary<string, List<AstNode>> _slotContent = new Dictionary<string, List<AstNode>>();
        
        // Master expression cache removed from here and moved up

        // Expression structs moved up

        public Evaluator(IVariableContext variables = null, SecurityConfig security = null, int templateSizeHint = 0)
        {
            _output = GetBuilder(templateSizeHint > 0 ? templateSizeHint / 2 : 0);
            _variables = variables ?? new DictionaryVariableContext(null);
            _security = security ?? SecurityConfig.Default;
            _currentLoopIterations = 0;
            _currentRecursionDepth = 0;
        }

        // Backward compatibility constructor for existing tests/code
        public Evaluator(Dictionary<string, object> variables, SecurityConfig security = null, int templateSizeHint = 0)
            : this(new DictionaryVariableContext(variables), security, templateSizeHint)
        {
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
        /// Sets slot content (for component rendering)
        /// </summary>
        public void SetSlots(List<AstNode> defaultSlot, Dictionary<string, List<AstNode>> namedSlots)
        {
            _slotContent.Clear();
            if (defaultSlot != null)
                _slotContent["default"] = defaultSlot;
            
            if (namedSlots != null)
            {
                foreach (var kvp in namedSlots)
                    _slotContent[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Evaluates AST and returns HTML string
        /// </summary>
        public string Evaluate(RootNode ast)
        {
            _currentRecursionDepth++;

            try
            {
                // Moved inside try so StringBuilder is always returned to pool on exception
                if (_currentRecursionDepth > _security.MaxRecursionDepth)
                {
                    throw new TemplateLimitException("RecursionDepth", _security.MaxRecursionDepth, _currentRecursionDepth);
                }

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
            else if (_security.EnableStrictMode)
            {
                // Strict mode: Show which variable is null/missing for easier debugging
                _output.Append($"<!-- Null/Missing: {node.Expression} -->");
            }
        }

        public void Visit(ElementNode node)
        {
            if (node.IsComponent)
            {
                // Render as component with "element/" prefix
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.JsonPath, node.Parameters, 
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
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.JsonPath, node.Parameters, 
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
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.JsonPath, node.Parameters, 
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
                RenderTypedComponent(node.ComponentPath, node.Name, node.OldName, node.JsonPath, node.Parameters, 
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

            // Try to get total count for loop.last and loop.total (O(1) for ICollection)
            int totalCount = -1;
            if (collection is ICollection col) totalCount = col.Count;

            // PERFORMANCE: Create ONE reusable child context + loop metadata dict
            // We update values in-place each iteration — zero allocation per iteration
            var loopLocalDict = new Dictionary<string, object>(3, StringComparer.OrdinalIgnoreCase);
            var loopMetadata = new Dictionary<string, object>(5, StringComparer.OrdinalIgnoreCase);
            loopLocalDict["loop"] = loopMetadata;
            var childContext = new HierarchicalVariableContext(_variables, loopLocalDict);

            // Save and swap context
            var savedVariables = _variables;
            _variables = childContext;

            int iterations = 0;
            try
            {
                foreach (var item in collection)
                {
                    // Security: limit iterations
                    if (_currentLoopIterations >= _security.MaxLoopIterations)
                    {
                        _output.Append($"<!-- Max loop iterations exceeded: {_security.MaxLoopIterations} -->");
                        break;
                    }

                    // Update in-place — no allocation
                    loopLocalDict[node.VariableName] = item;
                    loopMetadata["index"] = iterations;
                    loopMetadata["count"] = iterations + 1;
                    loopMetadata["first"] = iterations == 0;

                    if (totalCount != -1)
                    {
                        loopMetadata["last"] = iterations == totalCount - 1;
                        loopMetadata["total"] = totalCount;
                    }

                    // Render directly into our own StringBuilder — no child Evaluator needed
                    foreach (var child in node.Body)
                    {
                        child.Accept(this);
                    }

                    iterations++;
                    _currentLoopIterations++;
                }
            }
            finally
            {
                // Restore parent context
                _variables = savedVariables;
            }
        }

        #endregion

        #region Expression Evaluation

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private object ResolveExpression(string expression)
        {
            return ResolveExpression(expression, _variables);
        }

        private object ResolveExpression(string expression, IVariableContext variables)
        {
            if (string.IsNullOrEmpty(expression)) return null;

            // 1. Check Master Cache for pre-parsed expression logic
            if (_masterCache.TryGetValue(expression, out var compiled))
            {
                if (!compiled.IsSafe) return null;
                return EvaluateCompiled(compiled, variables);
            }

            // 2. Not in cache: Perform full analysis and compile
            var newCompiled = CompileExpression(expression);
            
            // Store in cache (limit size)
            if (_masterCache.Count < MaxExpressionCacheSize * 2)
                _masterCache.TryAdd(expression, newCompiled);

            if (!newCompiled.IsSafe) return null;
            return EvaluateCompiled(newCompiled, variables);
        }

        private CompiledExpression CompileExpression(string expression)
        {
            string trimmed = expression.Trim();
            var compiled = new CompiledExpression { IsSafe = true, SimpleName = trimmed };

            // Literal checks
            if (IsStandaloneQuotedLiteral(trimmed))
            {
                compiled.Category = ExpressionCategory.Literal;
                compiled.LiteralValue = CleanResult(trimmed);
                return compiled;
            }

            if (bool.TryParse(trimmed, out bool b)) { compiled.Category = ExpressionCategory.Literal; compiled.LiteralValue = b; return compiled; }
            if (int.TryParse(trimmed, out int intVal)) { compiled.Category = ExpressionCategory.Literal; compiled.LiteralValue = intVal; return compiled; }
            if (decimal.TryParse(trimmed, out decimal decVal)) { compiled.Category = ExpressionCategory.Literal; compiled.LiteralValue = decVal; return compiled; }
            if (double.TryParse(trimmed, out double dblVal)) { compiled.Category = ExpressionCategory.Literal; compiled.LiteralValue = dblVal; return compiled; }
            
            // Re-assign SimpleName as trimmed for identifying variable lookups later
            compiled.SimpleName = trimmed;

            // Security check once
            if (!SecurityUtils.IsExpressionSafe(trimmed, _security))
            {
                compiled.IsSafe = false;
                return compiled;
            }

            // Perform character scan once to determine category
            bool hasBracket = false, hasDot = false, hasParen = false, hasPipe = false;
            bool hasQuestion = false, hasColon = false, hasEquals = false, hasBang = false;
            bool hasGt = false, hasLt = false, hasNullCoalescing = false;
            
            for (int i = 0; i < trimmed.Length; i++)
            {
                switch (trimmed[i])
                {
                    case '[': hasBracket = true; break;
                    case '.': hasDot = true; break;
                    case '(': hasParen = true; break;
                    case '|': hasPipe = true; break;
                    case '?':
                        hasQuestion = true;
                        if (i + 1 < trimmed.Length && trimmed[i + 1] == '?') hasNullCoalescing = true;
                        break;
                    case ':': hasColon = true; break;
                    case '=': hasEquals = true; break;
                    case '!': hasBang = true; break;
                    case '>': hasGt = true; break;
                    case '<': hasLt = true; break;
                }
            }

            bool hasOperators = hasQuestion || hasColon || hasEquals || hasBang || hasGt || hasLt;

            if (hasPipe) { compiled.Category = ExpressionCategory.Filter; return compiled; }
            
            if (hasNullCoalescing)
            {
                int opIdx = FindTopLevelOperator(trimmed, "??");
                if (opIdx >= 0)
                {
                    compiled.Category = ExpressionCategory.NullCoalescing;
                    compiled.NullCoalescing = new NullCoalescingParts { Left = trimmed.Substring(0, opIdx).Trim(), Right = trimmed.Substring(opIdx + 2).Trim() };
                    return compiled;
                }
            }

            if (hasQuestion && hasColon)
            {
                int qIdx = FindTopLevelOperator(trimmed, '?');
                int cIdx = qIdx >= 0 ? FindMatchingTernaryColon(trimmed, qIdx) : -1;
                if (qIdx >= 0 && cIdx >= 0)
                {
                    compiled.Category = ExpressionCategory.Ternary;
                    compiled.Ternary = new TernaryParts { Condition = trimmed.Substring(0, qIdx).Trim(), WhenTrue = trimmed.Substring(qIdx + 1, cIdx - qIdx - 1).Trim(), WhenFalse = trimmed.Substring(cIdx + 1).Trim() };
                    return compiled;
                }
            }

            if (hasEquals || hasBang || hasGt || hasLt)
            {
                string[] ops = { "==", "!=", ">=", "<=", ">", "<" };
                foreach (string op in ops)
                {
                    int opIdx = FindTopLevelOperator(trimmed, op);
                    if (opIdx >= 0)
                    {
                        compiled.Category = ExpressionCategory.Comparison;
                        compiled.Comparison = new ComparisonParts { Left = trimmed.Substring(0, opIdx).Trim(), Right = trimmed.Substring(opIdx + op.Length).Trim(), Operator = op };
                        return compiled;
                    }
                }
            }

            if (hasBracket && !hasOperators) { compiled.Category = ExpressionCategory.Indexer; return compiled; }

            if (hasDot && !hasParen && !hasOperators)
            {
                compiled.Category = ExpressionCategory.Nested;
                compiled.PathParts = trimmed.Split('.');
                return compiled;
            }

            if (!hasDot && !hasBracket && !hasParen && !hasOperators)
            {
                compiled.Category = ExpressionCategory.Simple;
                compiled.SimpleName = trimmed;
                return compiled;
            }

            compiled.Category = ExpressionCategory.NCalc;
            return compiled;
        }

        private object EvaluateCompiled(CompiledExpression compiled, IVariableContext variables)
        {
            switch (compiled.Category)
            {
                case ExpressionCategory.Literal: return compiled.LiteralValue;
                case ExpressionCategory.Simple:
                    if (variables.TryGetValue(compiled.SimpleName, out var val)) return CleanResult(val);
                    return null;
                case ExpressionCategory.Nested:
                    return CleanResult(ResolveNestedPathInternal(compiled.PathParts, variables));
                case ExpressionCategory.Ternary:
                    bool cond = IsTruthy(ResolveExpression(compiled.Ternary.Condition, variables));
                    return ResolveExpression(cond ? compiled.Ternary.WhenTrue : compiled.Ternary.WhenFalse, variables);
                case ExpressionCategory.NullCoalescing:
                    return ResolveExpression(compiled.NullCoalescing.Left, variables) ?? ResolveExpression(compiled.NullCoalescing.Right, variables);
                case ExpressionCategory.Comparison:
                    object l = ResolveExpression(compiled.Comparison.Left, variables);
                    object r = ResolveExpression(compiled.Comparison.Right, variables);
                    return CompareValues(l, r, compiled.Comparison.Operator);
                case ExpressionCategory.Filter: return ResolveFilterExpression(compiled.SimpleName ?? "", variables); // Not fully optimized yet
                case ExpressionCategory.Indexer: return ResolveIndexerAccess(compiled.SimpleName ?? "", variables); // Not fully optimized yet
                case ExpressionCategory.NCalc: return EvaluateNCalcExpression(compiled.SimpleName ?? "", variables);
                default: return null;
            }
        }

        private object ResolveNestedPathInternal(string[] parts, IVariableContext variables)
        {
            if (parts.Length == 0) return null;

            string fullPath = string.Join(".", parts);
            if (variables.TryGetValue(fullPath, out var directMatch))
            {
                if (!_security.BlockedPropertyNames.Contains(fullPath))
                    return directMatch;
            }

            if (variables.TryGetValue(parts[0], out var resolved) && resolved != null)
            {
                for (int i = 1; i < parts.Length && resolved != null; i++)
                {
                    if (!SecurityUtils.IsPropertySafe(parts[i], _security)) return null;
                    resolved = PropertyAccessor.GetValue(resolved, parts[i]);
                }
                return resolved;
            }
            return null;
        }

        private bool TryEvaluateComparisonExpression(string expression, IVariableContext variables, out object result)
        {
            result = null;

            string[] operators = { "==", "!=", ">=", "<=", ">", "<" };
            foreach (string op in operators)
            {
                int operatorIndex = FindTopLevelOperator(expression, op);
                if (operatorIndex < 0)
                    continue;

                string leftText = expression.Substring(0, operatorIndex).Trim();
                string rightText = expression.Substring(operatorIndex + op.Length).Trim();

                object left = ResolveExpression(leftText, variables);
                object right = ResolveExpression(rightText, variables);
                result = CompareValues(left, right, op);
                return true;
            }

            return false;
        }

        private bool IsStandaloneQuotedLiteral(string expression)
        {
            if (expression.Length < 2)
                return false;

            char first = expression[0];
            char last = expression[expression.Length - 1];
            if (!((first == '\'' && last == '\'') || (first == '"' && last == '"')))
                return false;

            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];
                char prev = i > 0 ? expression[i - 1] : '\0';

                if (c == '\'' && !inDoubleQuote && prev != '\\')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote && prev != '\\')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                    continue;

                if (c == '?' || c == ':' || c == '=' || c == '!' || c == '>' || c == '<' || c == '|' || c == '&')
                    return false;
            }

            return true;
        }

        private int FindTopLevelOperator(string expression, char target)
        {
            int bracketDepth = 0;
            int parenDepth = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];
                char prev = i > 0 ? expression[i - 1] : '\0';

                if (c == '\'' && !inDoubleQuote && prev != '\\')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote && prev != '\\')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                    continue;

                switch (c)
                {
                    case '[': bracketDepth++; break;
                    case ']': bracketDepth--; break;
                    case '(': parenDepth++; break;
                    case ')': parenDepth--; break;
                    default:
                        if (c == target && bracketDepth == 0 && parenDepth == 0)
                            return i;
                        break;
                }
            }

            return -1;
        }

        private int FindTopLevelOperator(string expression, string target)
        {
            if (string.IsNullOrEmpty(target))
                return -1;

            int bracketDepth = 0;
            int parenDepth = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int i = 0; i <= expression.Length - target.Length; i++)
            {
                char c = expression[i];
                char prev = i > 0 ? expression[i - 1] : '\0';

                if (c == '\'' && !inDoubleQuote && prev != '\\')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote && prev != '\\')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                    continue;

                switch (c)
                {
                    case '[': bracketDepth++; continue;
                    case ']': bracketDepth--; continue;
                    case '(': parenDepth++; continue;
                    case ')': parenDepth--; continue;
                }

                if (bracketDepth == 0 && parenDepth == 0 && string.CompareOrdinal(expression, i, target, 0, target.Length) == 0)
                    return i;
            }

            return -1;
        }

        private static bool CompareValues(object left, object right, string op)
        {
            if (left is IComparable leftComparable && right != null && TryConvertComparable(right, left.GetType(), out var convertedRight))
            {
                int compare = leftComparable.CompareTo(convertedRight);
                return op switch
                {
                    "==" => compare == 0,
                    "!=" => compare != 0,
                    ">" => compare > 0,
                    "<" => compare < 0,
                    ">=" => compare >= 0,
                    "<=" => compare <= 0,
                    _ => false
                };
            }

            bool equals = object.Equals(left?.ToString(), right?.ToString());
            return op switch
            {
                "==" => equals,
                "!=" => !equals,
                _ => false
            };
        }

        private static bool TryConvertComparable(object value, Type targetType, out object converted)
        {
            converted = null;
            if (value == null)
                return false;

            if (targetType.IsInstanceOfType(value))
            {
                converted = value;
                return true;
            }

            try
            {
                converted = Convert.ChangeType(value, targetType);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private int FindMatchingTernaryColon(string expression, int questionIndex)
        {
            int bracketDepth = 0;
            int parenDepth = 0;
            int nestedTernaryDepth = 0;
            bool inSingleQuote = false;
            bool inDoubleQuote = false;

            for (int i = questionIndex + 1; i < expression.Length; i++)
            {
                char c = expression[i];
                char prev = i > 0 ? expression[i - 1] : '\0';

                if (c == '\'' && !inDoubleQuote && prev != '\\')
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote && prev != '\\')
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (inSingleQuote || inDoubleQuote)
                    continue;

                switch (c)
                {
                    case '[': bracketDepth++; break;
                    case ']': bracketDepth--; break;
                    case '(': parenDepth++; break;
                    case ')': parenDepth--; break;
                    case '?':
                        if (bracketDepth == 0 && parenDepth == 0)
                            nestedTernaryDepth++;
                        break;
                    case ':':
                        if (bracketDepth == 0 && parenDepth == 0)
                        {
                            if (nestedTernaryDepth == 0)
                                return i;
                            nestedTernaryDepth--;
                        }
                        break;
                }
            }

            return -1;
        }

        /// <summary>
        /// Highly optimized indexer resolution for expressions like item[Key] or item.Data[index].Prop
        /// </summary>
        private object ResolveIndexerAccess(string expression, IVariableContext variables)
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
        private object ResolveFilterExpression(string expression, IVariableContext variables)
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

        private object ResolveFromObject(object root, string path, IVariableContext variables)
        {
            if (root == null || string.IsNullOrEmpty(path)) return root;
            
            // Create a temporary context where the root is accessible (Zero-allocation)
            var tempVars = variables.CreateChild(new Dictionary<string, object>(1, StringComparer.OrdinalIgnoreCase)
            {
                ["__this__"] = root
            });
            
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

        private object ResolveNestedPath(string path, IVariableContext variables, object rootObject = null)
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

            // ═══════════════════════════════════════════════════════════
            // FAST PATH: 2 parts (e.g., "item.Name", "loop.index")
            // Covers ~90% of real template expressions — zero allocation
            // ═══════════════════════════════════════════════════════════
            if (parts.Length == 2)
            {
                // Try "parts[0]" as variable, then ".parts[1]" as property
                if (variables.TryGetValue(parts[0], out var root0) && root0 != null)
                {
                    if (!_security.BlockedPropertyNames.Contains(parts[0]) &&
                        SecurityUtils.IsPropertySafe(parts[1], _security))
                    {
                        return PropertyAccessor.GetValue(root0, parts[1]);
                    }
                }
                return null;
            }
            
            // ═══════════════════════════════════════════════════════════
            // FAST PATH: 3 parts (e.g., "item.Address.City")
            // ═══════════════════════════════════════════════════════════
            if (parts.Length == 3)
            {
                // Try "parts[0].parts[1]" as variable first
                // Then try "parts[0]" as variable with ".parts[1].parts[2]" as nested property
                if (variables.TryGetValue(parts[0], out var root1) && root1 != null)
                {
                    if (!_security.BlockedPropertyNames.Contains(parts[0]))
                    {
                        if (SecurityUtils.IsPropertySafe(parts[1], _security))
                        {
                            var mid = PropertyAccessor.GetValue(root1, parts[1]);
                            if (mid != null && SecurityUtils.IsPropertySafe(parts[2], _security))
                                return PropertyAccessor.GetValue(mid, parts[2]);
                        }
                    }
                }
                return null;
            }
            
            // ═══════════════════════════════════════════════════════════
            // GENERAL PATH: 4+ parts — use longest prefix search
            // ═══════════════════════════════════════════════════════════
            object current = null;
            int consumedParts = 0;

            for (int i = parts.Length - 1; i >= 0; i--)
            {
                string prefix;
                if (i == 0)
                {
                    prefix = parts[0];
                }
                else
                {
                    var sb = GetBuilder();
                    for (int j = 0; j <= i; j++)
                    {
                        if (j > 0) sb.Append('.');
                        sb.Append(parts[j]);
                    }
                    prefix = sb.ToString();
                    ReturnBuilder(sb);
                }

                if (variables.TryGetValue(prefix, out current))
                {
                    if (_security.BlockedPropertyNames.Contains(prefix))
                    {
                        current = null;
                        continue;
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
                if (!SecurityUtils.IsPropertySafe(parts[i], _security))
                {
                    return null;
                }
                
                current = PropertyAccessor.GetValue(current, parts[i]);
            }

            return current;
        }

        private object EvaluateNCalcExpression(string expression)
        {
            return EvaluateNCalcExpression(expression, _variables);
        }

        private object EvaluateNCalcExpression(string expression, IVariableContext variables)
        {
            if (string.IsNullOrWhiteSpace(expression)) return null;

            // Convert C#-style indexer syntax item['Prop'] or item["Prop"] to NCalc-compatible [item.Prop]
            // This allows NCalc to treat the entire path as a single parameter name
            expression = Regex.Replace(expression, @"(\w+)\[['""](.+?)['""]\]", "[$1.$2]");
            // Handle multiple levels if needed, e.g., [item.Meta]['Value'] -> [[item.Meta].Value]
            // We'll simplify and just ensure the brackets are correctly placed for NCalc
            expression = Regex.Replace(expression, @"\]\[['""](.+?)['""]\]", ".$1]");

            try
            {
                // Enforce cache size limit before adding
                if (_expressionCache.Count >= MaxExpressionCacheSize)
                    EnforceExpressionCacheLimit();

                // Get or Add parsed logical expression
                var cachedLogicalExpr = _expressionCache.GetOrAdd(expression, exprStr =>
                {
                    return new Expression(exprStr).ParsedExpression;
                });

                var ncalc = new Expression(cachedLogicalExpr);
                

                // Handle parameters manually to support dot notation (nested objects and dictionaries)
                ncalc.EvaluateParameter += (name, args) =>
                {
                    if (variables == null) return;
                    
                    // Priority: try ResolveExpression first (which handles cached paths, literals, etc.)
                    // This is much more robust than just ResolvePropertyPath
                    var val = ResolveExpression(name, variables);
                    args.Result = val;
                };

                return ncalc.Evaluate();
            }
            catch
            {
                return null;
            }
        }

        private object ResolvePropertyPath(IVariableContext variables, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('.');
            
            // Start from the root variable
            object current = variables[parts[0]];
            
            for (int i = 1; i < parts.Length && current != null; i++)
            {
                current = PropertyAccessor.GetValue(current, parts[i]);
            }

            return current;
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
                NCalc.Domain.LogicalExpression _;
                _expressionCache.TryRemove(key, out _);
            }
        }

        private bool EvaluateCondition(string condition)
        {
            if (string.IsNullOrEmpty(condition))
                return false;

            condition = condition.Trim();

            // FAST PATH: Single-pass character scan for condition type detection
            // Replaces 8+ .Contains() calls with one pass
            bool hasSpace = false, hasParen = false, hasBang = false;
            bool hasEquals = false, hasLt = false, hasGt = false;
            bool hasQuote = false, hasDot = false;
            for (int i = 0; i < condition.Length; i++)
            {
                switch (condition[i])
                {
                    case ' ': hasSpace = true; break;
                    case '(': hasParen = true; break;
                    case '!': hasBang = true; break;
                    case '=': hasEquals = true; break;
                    case '<': hasLt = true; break;
                    case '>': hasGt = true; break;
                    case '"': case '\'': hasQuote = true; break;
                    case '.': hasDot = true; break;
                }
            }

            // FAST PATH: Simple variable or nested path (no operators, no quotes, no spaces)
            // Example: "menuNode.HasChildren" or "IsActive"
            if (!hasSpace && !hasParen && !hasBang && !hasEquals && !hasLt && !hasGt && !hasQuote)
            {
                object val;
                if (hasDot)
                {
                    val = ResolveNestedPath(condition);
                }
                else
                {
                    _variables.TryGetValue(condition, out val);
                }
                return IsTruthy(val);
            }

            // Complex expression: use NCalc with CACHED parse tree
            string processedCondition = PreprocessCondition(condition);

            try
            {
                // Use cached NCalc expression tree (same cache as EvaluateNCalcExpression)
                // This avoids re-parsing the condition string on every evaluation
                var result = EvaluateNCalcExpression(processedCondition, _variables);
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
            
            // FAST PATH: ICollection.Count is O(1) for List, Array, Dictionary, HashSet, etc.
            // This avoids allocating and disposing an enumerator just to check emptiness
            if (value is ICollection collection)
            {
                return collection.Count > 0;
            }
            
            // Collections: non-empty is true (fallback for non-ICollection enumerables)
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

        private string ResolveInterpolationsInString(string input, IVariableContext variables)
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

        #region StringBuilder Pool (Adaptive)

        /// <summary>
        /// Gets a StringBuilder from the pool with adaptive sizing.
        /// Uses a tiered pool strategy: small builders (â‰¤4KB) and large builders (>4KB)
        /// to reduce reallocations by matching builder capacity to expected output size.
        /// </summary>
        /// <param name="sizeHint">Expected output size hint (0 = use default 1024)</param>
        private static StringBuilder GetBuilder(int sizeHint = 0)
        {
            if (sizeHint > SmallBuilderThreshold)
            {
                // Try large pool first for large templates
                if (_builderPoolLarge.TryTake(out var largeSb))
                {
                    largeSb.Clear();
                    // Ensure capacity matches the hint to avoid reallocation
                    if (largeSb.Capacity < sizeHint)
                        largeSb.Capacity = sizeHint;
                    return largeSb;
                }
                return new StringBuilder(sizeHint);
            }

            // Small/default path
            if (_builderPoolSmall.TryTake(out var sb))
            {
                sb.Clear();
                return sb;
            }
            return new StringBuilder(sizeHint > 0 ? sizeHint : 1024);
        }

        private static void ReturnBuilder(StringBuilder sb)
        {
            if (sb.Capacity > SmallBuilderThreshold)
            {
                // Return to large pool
                if (_builderPoolLarge.Count < MaxPoolSize)
                {
                    sb.Clear();
                    _builderPoolLarge.Add(sb);
                }
            }
            else
            {
                // Return to small pool
                if (_builderPoolSmall.Count < MaxPoolSize)
                {
                    sb.Clear();
                    _builderPoolSmall.Add(sb);
                }
            }
        }

        #endregion

        #region Component System

        // Maximum component include depth to prevent infinite recursion
        private const int MaxComponentDepth = 10;
        private int _componentDepth = 0;

        public void Visit(IncludeNode node)
        {
            // Delegate to shared component rendering logic
            // For Include: fullPath = componentPath (no type prefix)
            RenderComponentCore(
                fullPath: node.ComponentPath,
                componentPath: node.ComponentPath,
                name: node.Name,
                oldName: node.OldName,
                jsonPath: node.JsonPath,
                parameters: node.Parameters,
                slotContent: node.SlotContent,
                namedSlots: node.NamedSlots,
                componentType: "include");
        }

        /// <summary>
        /// Renders a type-specific component with auto path prefix.
        /// Thin wrapper that delegates to RenderComponentCore.
        /// </summary>
        private void RenderTypedComponent(
            string componentPath, 
            string name, 
            string oldName,
            string jsonPath,
            List<ParamData> parameters,
            List<AstNode> slotContent,
            Dictionary<string, List<AstNode>> namedSlots,
            string typePrefix)
        {
            // For typed components: fullPath = typePrefix/componentPath
            var fullPath = $"{typePrefix}/{componentPath}";
            RenderComponentCore(
                fullPath: fullPath,
                componentPath: componentPath,
                name: name,
                oldName: oldName,
                jsonPath: jsonPath,
                parameters: parameters,
                slotContent: slotContent,
                namedSlots: namedSlots,
                componentType: typePrefix);
        }

        /// <summary>
        /// Shared component rendering logic used by both Visit(IncludeNode) and RenderTypedComponent.
        /// This eliminates ~270 lines of duplicated code and ensures consistent behavior.
        /// </summary>
        /// <param name="fullPath">Full path for loading (e.g., "slider" or "element/button")</param>
        /// <param name="componentPath">Original component path (stored as componentPath variable)</param>
        /// <param name="name">Component instance name</param>
        /// <param name="oldName">Previous component name (for rename tracking)</param>
        /// <param name="jsonPath">JSON path for component data binding</param>
        /// <param name="parameters">Parameters to pass to the component</param>
        /// <param name="slotContent">Inner content passed to the default slot</param>
        /// <param name="namedSlots">Content for named slots</param>
        /// <param name="componentType">Component type identifier (e.g., "include", "element", "data")</param>
        private void RenderComponentCore(
            string fullPath,
            string componentPath,
            string name,
            string oldName,
            string jsonPath,
            List<ParamData> parameters,
            List<AstNode> slotContent,
            Dictionary<string, List<AstNode>> namedSlots,
            string componentType)
        {
            if (_componentLoader == null)
                return;

            if (_componentDepth >= MaxComponentDepth)
            {
                _output.Append($"<!-- Max component depth exceeded: {fullPath} -->");
                return;
            }

            if (string.IsNullOrEmpty(fullPath))
                return;

            // Track variables set by callback (only allocated when callback exists)
            HashSet<string> callbackSetVariables = null;
            Dictionary<string, object> engineSnapshot = null;

            try
            {
                // Resolve names if they contain interpolations
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

                // Fire OnBeforeIncludeRender callback FIRST (before loading component)
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    var currentEngineVars = _engine.GetVariables();
                    engineSnapshot = new Dictionary<string, object>(currentEngineVars, StringComparer.OrdinalIgnoreCase);
                    
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = fullPath,
                        ComponentType = componentType,
                        JsonPath = jsonPath,
                        Parameters = new Dictionary<string, string>()
                    };
                    
                    foreach (var p in parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    
                    _onBeforeIncludeRender(includeInfo, _engine);
                }
                
                // Create a container for parameter resolution that includes callback variables
                IVariableContext paramResolutionVars = _variables;
                
                if (_onBeforeIncludeRender != null && _engine != null)
                {
                    var engineVars = _engine.GetVariables();
                    var callbackDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    callbackSetVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach (var kvp in engineVars)
                    {
                        bool isActuallySetByCallback = true;
                        if (engineSnapshot != null && engineSnapshot.TryGetValue(kvp.Key, out var oldValue))
                        {
                            if (Equals(kvp.Value, oldValue))
                                isActuallySetByCallback = false;
                        }

                        if (isActuallySetByCallback)
                        {
                            callbackDict[kvp.Key] = kvp.Value;
                            callbackSetVariables.Add(kvp.Key);
                        }
                    }

                    if (callbackDict.Count > 0)
                        paramResolutionVars = _variables.CreateChild(callbackDict);
                }
                
                // Load the component AST
                var componentAst = _componentLoader(fullPath);
                if (componentAst == null)
                    return;

                // Create a local dictionary for component-specific variables (Zero-allocation shadowing)
                var localVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                
                // Shadow instance-specific variables INHERITED FROM PARENT with null
                localVars["element"] = null;
                localVars["elementAttributes"] = null;
                localVars["oldname"] = null;
                localVars["name"] = null;

                // Sync callback-set variables for the CHILD
                if (callbackSetVariables != null)
                {
                    var engineVars = _engine.GetVariables();
                    foreach (var varName in callbackSetVariables)
                    {
                        localVars[varName] = engineVars[varName];
                    }
                }

                // Add standard component variables
                localVars["name"] = resolvedInstanceName;
                localVars["oldname"] = resolvedOldName;
                localVars["path"] = fullPath;
                localVars["componentPath"] = componentPath;
                localVars["type"] = componentType;
                
                // Add parameters as variables
                foreach (var param in parameters)
                {
                    if (callbackSetVariables != null && callbackSetVariables.Contains(param.Name))
                        continue;
                    if (!string.IsNullOrEmpty(param.Name))
                    {
                        var paramValue = param.Value;
                        object resolvedValue = null;
                        
                        if (string.IsNullOrEmpty(paramValue))
                        {
                            resolvedValue = null;
                        }
                        else if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                        {
                            var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                            resolvedValue = ResolveExpression(varName, paramResolutionVars);
                        }
                        else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                        {
                            var interpolatedValue = ResolveInterpolationsInString(paramValue, paramResolutionVars);
                            resolvedValue = string.IsNullOrEmpty(interpolatedValue) ? null : interpolatedValue;
                        }
                        else if (!paramValue.Contains(" ") && 
                                 !paramValue.StartsWith("\"") && 
                                 !paramValue.StartsWith("'"))
                        {
                            resolvedValue = ResolveExpression(paramValue, paramResolutionVars);
                            if (resolvedValue == null)
                                resolvedValue = paramValue;
                        }
                        else
                        {
                            resolvedValue = paramValue;
                        }
                        
                        // Apply default value if resolved value is null or empty
                        if (resolvedValue == null || (resolvedValue is string s && string.IsNullOrEmpty(s)))
                        {
                            if (!string.IsNullOrEmpty(param.Default))
                            {
                                resolvedValue = param.Default.Contains("{{")
                                    ? ResolveInterpolationsInString(param.Default, paramResolutionVars)
                                    : param.Default;
                            }
                            else
                            {
                                resolvedValue = string.Empty;
                            }
                        }
                        
                        localVars[param.Name] = resolvedValue;
                    }
                }

                var componentVars = _variables.CreateChild(localVars);
                var componentEvaluator = new Evaluator(componentVars, _security);
                componentEvaluator.SetComponentLoader(_componentLoader);
                componentEvaluator.SetSlots(slotContent, namedSlots);
                componentEvaluator._componentDepth = _componentDepth + 1;
                
                if (_onBeforeIncludeRender != null || _onAfterIncludeRender != null)
                {
                    componentEvaluator.SetIncludeCallback(_onBeforeIncludeRender, _engine, _onAfterIncludeRender);
                }

                var result = componentEvaluator.Evaluate(componentAst);
                
                // Fire OnAfterIncludeRender callback if configured (for wrapping)
                if (_onAfterIncludeRender != null)
                {
                    var includeInfo = new IncludeInfo
                    {
                        Name = resolvedInstanceName,
                        OldName = resolvedOldName,
                        ComponentPath = fullPath,
                        ComponentType = componentType,
                        JsonPath = jsonPath,
                        Parameters = new Dictionary<string, string>()
                    };
                    foreach (var p in parameters)
                    {
                        if (!string.IsNullOrEmpty(p.Name))
                            includeInfo.Parameters[p.Name] = p.Value;
                    }
                    result = _onAfterIncludeRender(includeInfo, result);
                }
                
                _output.Append(result);
            }
            catch (Exception ex)
            {
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

        private Evaluator CreateChildEvaluator(IVariableContext variables)
        {
            var child = new Evaluator(variables, _security);
            child.SetComponentLoader(_componentLoader);
            child._templateFragments = _templateFragments;
            child._sections = _sections;
            child._bodyContent = _bodyContent;
            child._slotContent = _slotContent;
            child._onBeforeIncludeRender = _onBeforeIncludeRender;
            child._onAfterIncludeRender = _onAfterIncludeRender;
            child._engine = _engine;
            child._currentRecursionDepth = _currentRecursionDepth;
            return child;
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
                // Resolve parameters into a local dictionary for the child context
                var fragmentLocalVars = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
                    else if (paramValue.StartsWith("{{") && paramValue.EndsWith("}}"))
                    {
                        var varName = paramValue.Substring(2, paramValue.Length - 4).Trim();
                        resolvedValue = ResolveExpression(varName, _variables);
                    }
                    else if (paramValue.Contains("{{") && paramValue.Contains("}}"))
                    {
                        resolvedValue = ResolveInterpolationsInString(paramValue, _variables);
                    }
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
                        resolvedValue = paramValue;
                    }

                    // Apply default value if resolved value is null or empty
                    if (resolvedValue == null || (resolvedValue is string s && string.IsNullOrEmpty(s)))
                    {
                        if (!string.IsNullOrEmpty(param.Default))
                        {
                            resolvedValue = param.Default.Contains("{{")
                                ? ResolveInterpolationsInString(param.Default, _variables)
                                : param.Default;
                        }
                        else
                        {
                            resolvedValue = string.Empty;
                        }
                    }

                    fragmentLocalVars[param.Name] = resolvedValue;
                }

                // PERFORMANCE: Swap context in-place — no new Evaluator needed
                var savedVariables = _variables;
                _variables = _variables.CreateChild(fragmentLocalVars);

                // Render the fragment body directly into our StringBuilder
                foreach (var child in fragment.Body)
                {
                    child.Accept(this);
                }

                // Restore
                _variables = savedVariables;
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
            // Drain pools - ConcurrentBag.Clear() not available on all target frameworks
            while (_builderPoolSmall.TryTake(out _)) { }
            while (_builderPoolLarge.TryTake(out _)) { }
        }
    }
}

