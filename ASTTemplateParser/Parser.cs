using System;
using System.Collections.Generic;

namespace ASTTemplateParser
{
    /// <summary>
    /// Parses token stream into AST tree
    /// </summary>
    public sealed class Parser
    {
        private readonly List<Token> _tokens;
        private int _position;

        public Parser(List<Token> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
            _position = 0;
        }

        private Token Current => _position < _tokens.Count ? _tokens[_position] : default;
        private Token Peek(int offset = 1) => 
            _position + offset < _tokens.Count ? _tokens[_position + offset] : default;

        private void Advance() => _position++;
        
        private bool IsAtEnd => Current.Type == TokenType.EOF || _position >= _tokens.Count;

        /// <summary>
        /// Parse tokens into AST
        /// </summary>
        public RootNode Parse()
        {
            var root = new RootNode();
            ParseChildren(root.Children, TokenType.EOF);
            return root;
        }

        private void ParseChildren(List<AstNode> children, params TokenType[] endTypes)
        {
            while (!IsAtEnd && !IsEndType(endTypes))
            {
                var node = ParseNode();
                if (node != null)
                {
                    children.Add(node);
                }
            }
        }

        private bool IsEndType(TokenType[] types)
        {
            foreach (var t in types)
            {
                if (Current.Type == t) return true;
            }
            return false;
        }

        private AstNode ParseNode()
        {
            var token = Current;

            switch (token.Type)
            {
                case TokenType.Text:
                    Advance();
                    return new TextNode(token.Value) { Position = token.Position, Line = token.Line };

                case TokenType.Interpolation:
                    Advance();
                    return new InterpolationNode(token.Value) { Position = token.Position, Line = token.Line };

                case TokenType.ElementStart:
                case TokenType.ElementSelfClosing:
                    return ParseElement(token);

                case TokenType.DataStart:
                    return ParseDataNode(token);

                case TokenType.NavStart:
                    return ParseNavNode(token);

                case TokenType.BlockStart:
                    return ParseBlockNode(token);

                case TokenType.If:
                    return ParseIfNode(token);

                case TokenType.ForEach:
                    return ParseForEachNode(token);

                // Component System
                case TokenType.Include:
                    return ParseIncludeNode(token);

                // Layout System
                case TokenType.Layout:
                    return ParseLayoutNode(token);

                case TokenType.Section:
                    return ParseSectionNode(token);

                case TokenType.RenderSection:
                    return ParseRenderSectionNode(token);

                case TokenType.RenderBody:
                    Advance();
                    return new RenderBodyNode { Position = token.Position, Line = token.Line };

                // Slots
                case TokenType.Slot:
                    return ParseSlotNode(token);

                case TokenType.OpenBrace:
                    Advance();
                    return null; // Skip standalone braces

                case TokenType.CloseBrace:
                    Advance();
                    return null; // Skip standalone braces

                default:
                    Advance(); // Skip unknown tokens
                    return null;
            }
        }

        private ElementNode ParseElement(Token token)
        {
            var node = new ElementNode
            {
                TagName = token.Value,
                Attributes = token.Metadata,
                IsSelfClosing = token.Type == TokenType.ElementSelfClosing,
                Position = token.Position,
                Line = token.Line
            };

            Advance();

            if (!node.IsSelfClosing)
            {
                ParseChildren(node.Children, TokenType.ElementEnd, TokenType.EOF);
                
                if (Current.Type == TokenType.ElementEnd)
                    Advance();
            }

            return node;
        }

        private DataNode ParseDataNode(Token token)
        {
            var node = new DataNode
            {
                Attributes = token.Metadata,
                Position = token.Position,
                Line = token.Line
            };

            Advance();
            ParseChildren(node.Children, TokenType.DataEnd, TokenType.EOF);
            
            if (Current.Type == TokenType.DataEnd)
                Advance();

            return node;
        }

        private NavNode ParseNavNode(Token token)
        {
            var node = new NavNode
            {
                Attributes = token.Metadata,
                Position = token.Position,
                Line = token.Line
            };

            Advance();
            ParseChildren(node.Children, TokenType.NavEnd, TokenType.EOF);
            
            if (Current.Type == TokenType.NavEnd)
                Advance();

            return node;
        }

        private BlockNode ParseBlockNode(Token token)
        {
            var node = new BlockNode
            {
                Attributes = token.Metadata,
                Position = token.Position,
                Line = token.Line
            };

            Advance();
            ParseChildren(node.Children, TokenType.BlockEnd, TokenType.EOF);
            
            if (Current.Type == TokenType.BlockEnd)
                Advance();

            return node;
        }

        private IfNode ParseIfNode(Token token)
        {
            // HTML-like syntax: <If condition="..."> - condition is in Metadata
            var node = new IfNode
            {
                Condition = token.Metadata ?? token.Value,
                Position = token.Position,
                Line = token.Line
            };

            Advance();

            // Skip opening brace if present
            if (Current.Type == TokenType.OpenBrace)
                Advance();

            // Parse then-branch
            ParseIfBranchChildren(node.ThenBranch);

            // Parse else-if branches: <ElseIf condition="...">
            while (Current.Type == TokenType.ElseIf)
            {
                var elseIfBranch = new ElseIfBranch { Condition = Current.Metadata ?? Current.Value };
                Advance();
                
                if (Current.Type == TokenType.OpenBrace)
                    Advance();
                    
                ParseIfBranchChildren(elseIfBranch.Body);
                node.ElseIfBranches.Add(elseIfBranch);
            }

            // Parse else branch
            if (Current.Type == TokenType.Else)
            {
                Advance();
                
                if (Current.Type == TokenType.OpenBrace)
                    Advance();
                    
                ParseIfBranchChildren(node.ElseBranch);
            }

            // Skip @endif if present
            if (Current.Type == TokenType.EndIf)
                Advance();

            return node;
        }

        private void ParseIfBranchChildren(List<AstNode> children)
        {
            while (!IsAtEnd)
            {
                var type = Current.Type;
                
                // End of this branch
                if (type == TokenType.CloseBrace || 
                    type == TokenType.ElseIf || 
                    type == TokenType.Else || 
                    type == TokenType.EndIf)
                {
                    if (type == TokenType.CloseBrace)
                        Advance();
                    break;
                }

                var node = ParseNode();
                if (node != null)
                    children.Add(node);
            }
        }

        private ForEachNode ParseForEachNode(Token token)
        {
            var node = new ForEachNode
            {
                Position = token.Position,
                Line = token.Line
            };

            // Parse metadata: "varName|collectionExpr"
            if (!string.IsNullOrEmpty(token.Metadata))
            {
                var parts = token.Metadata.Split('|');
                if (parts.Length == 2)
                {
                    node.VariableName = parts[0];
                    node.CollectionExpression = parts[1];
                }
            }

            Advance();

            // Skip opening brace if present
            if (Current.Type == TokenType.OpenBrace)
                Advance();

            // Parse body
            while (!IsAtEnd)
            {
                var type = Current.Type;
                
                if (type == TokenType.CloseBrace || type == TokenType.EndForEach)
                {
                    Advance();
                    if (type == TokenType.CloseBrace && Current.Type == TokenType.EndForEach)
                        Advance();
                    break;
                }

                var childNode = ParseNode();
                if (childNode != null)
                    node.Body.Add(childNode);
            }

            return node;
        }

        #region Component System Parsing

        private IncludeNode ParseIncludeNode(Token token)
        {
            var node = new IncludeNode
            {
                Position = token.Position,
                Line = token.Line
            };

            // Extract component path from metadata (e.g., component="path/to/component")
            node.ComponentPath = ExtractAttributeFromMetadata(token.Metadata, "component");
            
            // Extract name for cache key (e.g., name="about_us_header_f3ec29c90de74908a7f38b758dd94b81_un")
            node.Name = ExtractAttributeFromMetadata(token.Metadata, "name");

            Advance();

            // Parse children (Param nodes and slot content)
            while (!IsAtEnd && Current.Type != TokenType.EndInclude)
            {
                if (Current.Type == TokenType.Param)
                {
                    // Parse parameter
                    var paramToken = Current;
                    var paramName = ExtractAttributeFromMetadata(paramToken.Metadata, "name");
                    var paramValue = ExtractAttributeFromMetadata(paramToken.Metadata, "value");
                    node.Parameters.Add(new ParamData { Name = paramName, Value = paramValue });
                    Advance();
                }
                else if (Current.Type == TokenType.Slot)
                {
                    // Parse named slot content
                    var slotName = ExtractAttributeFromMetadata(Current.Metadata, "name") ?? "default";
                    Advance();
                    var slotContent = new List<AstNode>();
                    while (!IsAtEnd && Current.Type != TokenType.EndSlot)
                    {
                        var slotChild = ParseNode();
                        if (slotChild != null)
                            slotContent.Add(slotChild);
                    }
                    if (Current.Type == TokenType.EndSlot) Advance();
                    node.NamedSlots[slotName] = slotContent;
                }
                else
                {
                    // Default slot content
                    var child = ParseNode();
                    if (child != null)
                        node.SlotContent.Add(child);
                }
            }

            if (Current.Type == TokenType.EndInclude)
                Advance();

            return node;
        }

        #endregion

        #region Layout System Parsing

        private LayoutNode ParseLayoutNode(Token token)
        {
            var node = new LayoutNode
            {
                LayoutName = ExtractAttributeFromMetadata(token.Metadata, "name"),
                Position = token.Position,
                Line = token.Line
            };

            Advance();

            // Parse children until </Layout>
            while (!IsAtEnd && Current.Type != TokenType.EndLayout)
            {
                var child = ParseNode();
                if (child != null)
                    node.Children.Add(child);
            }

            if (Current.Type == TokenType.EndLayout)
                Advance();

            return node;
        }

        private SectionNode ParseSectionNode(Token token)
        {
            var node = new SectionNode
            {
                Name = ExtractAttributeFromMetadata(token.Metadata, "name"),
                Position = token.Position,
                Line = token.Line
            };

            Advance();

            // Parse children until </Section>
            while (!IsAtEnd && Current.Type != TokenType.EndSection)
            {
                var child = ParseNode();
                if (child != null)
                    node.Children.Add(child);
            }

            if (Current.Type == TokenType.EndSection)
                Advance();

            return node;
        }

        private RenderSectionNode ParseRenderSectionNode(Token token)
        {
            var node = new RenderSectionNode
            {
                Name = ExtractAttributeFromMetadata(token.Metadata, "name"),
                Position = token.Position,
                Line = token.Line
            };

            // Check for required attribute
            var requiredAttr = ExtractAttributeFromMetadata(token.Metadata, "required");
            node.Required = requiredAttr?.ToLower() == "true";

            Advance();
            return node;
        }

        private SlotNode ParseSlotNode(Token token)
        {
            var node = new SlotNode
            {
                Name = ExtractAttributeFromMetadata(token.Metadata, "name") ?? "default",
                Position = token.Position,
                Line = token.Line
            };

            Advance();

            // Parse default content until </Slot>
            while (!IsAtEnd && Current.Type != TokenType.EndSlot)
            {
                var child = ParseNode();
                if (child != null)
                    node.DefaultContent.Add(child);
            }

            if (Current.Type == TokenType.EndSlot)
                Advance();

            return node;
        }

        #endregion

        #region Utility Methods

        private string ExtractAttributeFromMetadata(string metadata, string attrName)
        {
            if (string.IsNullOrEmpty(metadata))
                return null;

            // Look for attrName="value" or attrName='value'
            int nameStart = metadata.IndexOf(attrName, StringComparison.OrdinalIgnoreCase);
            if (nameStart < 0) return null;

            int eqPos = metadata.IndexOf('=', nameStart);
            if (eqPos < 0) return null;

            int valueStart = eqPos + 1;
            while (valueStart < metadata.Length && char.IsWhiteSpace(metadata[valueStart]))
                valueStart++;

            if (valueStart >= metadata.Length) return null;

            char quote = metadata[valueStart];
            if (quote != '"' && quote != '\'')
            {
                // No quote - read until whitespace or end
                int end = valueStart;
                while (end < metadata.Length && !char.IsWhiteSpace(metadata[end]))
                    end++;
                return metadata.Substring(valueStart, end - valueStart);
            }

            // Find closing quote
            int closeQuote = metadata.IndexOf(quote, valueStart + 1);
            if (closeQuote < 0) return null;

            return metadata.Substring(valueStart + 1, closeQuote - valueStart - 1);
        }

        #endregion
    }
}
