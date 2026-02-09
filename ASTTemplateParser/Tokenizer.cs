using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ASTTemplateParser
{
    /// <summary>
    /// High-performance single-pass tokenizer for template parsing
    /// Uses HTML-like syntax for all constructs (no @ directives)
    /// </summary>
    public sealed class Tokenizer
    {
        private readonly string _input;
        private readonly int _length;
        private int _position;
        private int _line;
        private readonly StringBuilder _buffer;

        // Known tag prefixes for fast detection - includes all template tags
        private static readonly string[] KnownTags = { 
            // Container tags
            "Element", "Data", "Nav", "Block",
            // Conditional tags
            "If", "ElseIf", "Else",
            // Loop tags
            "ForEach",
            // Component System
            "Include", "Param",
            // Layout System
            "Layout", "Section", "RenderSection", "RenderBody",
            // Slots
            "Slot",
            // Template Fragments (Inline Recursion)
            "Define", "Render"
        };

        public Tokenizer(string input)
        {
            _input = input ?? string.Empty;
            _length = _input.Length;
            _position = 0;
            _line = 1;
            _buffer = new StringBuilder(256);
        }

        /// <summary>
        /// Tokenizes the entire input in a single pass
        /// </summary>
        public List<Token> Tokenize()
        {
            // Estimate ~1 token per 20 chars, minimum 64 for small templates
            var tokens = new List<Token>(Math.Max(64, _length / 20));

            while (_position < _length)
            {
                var token = NextToken();
                if (token.Type != TokenType.EOF)
                {
                    tokens.Add(token);
                }
                else
                {
                    break;
                }
            }

            tokens.Add(new Token(TokenType.EOF, null, _position, _line));
            return tokens;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Current() => _position < _length ? _input[_position] : '\0';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private char Peek(int offset = 1) => 
            _position + offset < _length ? _input[_position + offset] : '\0';

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Advance(int count = 1)
        {
            for (int i = 0; i < count && _position < _length; i++)
            {
                if (_input[_position] == '\n') _line++;
                _position++;
            }
        }

        private Token NextToken()
        {
            if (_position >= _length)
                return new Token(TokenType.EOF, null, _position, _line);

            int startPos = _position;
            int startLine = _line;

            // Check for interpolation: {{...}}
            if (Current() == '{' && Peek() == '{')
            {
                return ReadInterpolation(startPos, startLine);
            }

            // Check for HTML-like tags: <Element>, </Element>, <If>, </If>, etc.
            if (Current() == '<')
            {
                // CRITICAL FIX: Skip HTML comments to prevent parsing tags inside comments
                // This fixes infinite recursion when component files have usage examples in comments
                if (Peek() == '!' && Peek(2) == '-' && Peek(3) == '-')
                {
                    return SkipHtmlComment(startPos, startLine);
                }

                var tag = TryReadTag(startPos, startLine);
                if (tag.HasValue)
                    return tag.Value;
            }

            // Check for block braces (for future use)
            if (Current() == '{')
            {
                Advance();
                return new Token(TokenType.OpenBrace, "{", startPos, startLine);
            }

            if (Current() == '}')
            {
                Advance();
                return new Token(TokenType.CloseBrace, "}", startPos, startLine);
            }

            // Read plain text until next special character
            return ReadText(startPos, startLine);
        }

        private Token ReadInterpolation(int startPos, int startLine)
        {
            Advance(2); // Skip {{
            _buffer.Clear();

            while (_position < _length)
            {
                if (Current() == '}' && Peek() == '}')
                {
                    Advance(2); // Skip }}
                    return new Token(TokenType.Interpolation, _buffer.ToString().Trim(), startPos, startLine);
                }
                _buffer.Append(Current());
                Advance();
            }

            // Unclosed interpolation - treat as text
            return new Token(TokenType.Text, "{{" + _buffer.ToString(), startPos, startLine);
        }

        /// <summary>
        /// Skips HTML comments and returns them as plain text
        /// This prevents template tags inside comments from being parsed
        /// </summary>
        private Token SkipHtmlComment(int startPos, int startLine)
        {
            _buffer.Clear();
            _buffer.Append("<!--");
            Advance(4); // Skip <!--

            while (_position < _length)
            {
                if (Current() == '-' && Peek() == '-' && Peek(2) == '>')
                {
                    _buffer.Append("-->");
                    Advance(3); // Skip -->
                    return new Token(TokenType.Text, _buffer.ToString(), startPos, startLine);
                }
                _buffer.Append(Current());
                Advance();
            }

            // Unclosed comment - return as text anyway
            return new Token(TokenType.Text, _buffer.ToString(), startPos, startLine);
        }

        private Token? TryReadTag(int startPos, int startLine)
        {
            bool isClosing = Peek() == '/';
            int tagStart = isClosing ? _position + 2 : _position + 1;

            foreach (var tag in KnownTags)
            {
                if (MatchTagName(tagStart, tag))
                {
                    return ReadKnownTag(startPos, startLine, tag, isClosing);
                }
            }

            return null; // Not a known tag - will be treated as text
        }

        private bool MatchTagName(int pos, string tagName)
        {
            if (pos + tagName.Length > _length)
                return false;

            // CRITICAL: Template tags MUST be PascalCase (start with uppercase)
            // This distinguishes template <Section> from HTML <section>
            // First character must be uppercase to be a template tag
            if (pos < _length && char.IsLower(_input[pos]))
                return false;

            for (int i = 0; i < tagName.Length; i++)
            {
                if (char.ToUpperInvariant(_input[pos + i]) != char.ToUpperInvariant(tagName[i]))
                    return false;
            }

            // Check that it's followed by whitespace, > or /
            int afterTag = pos + tagName.Length;
            if (afterTag >= _length)
                return false;

            char next = _input[afterTag];
            return next == '>' || next == '/' || next == ' ' || next == '\t' || next == '\r' || next == '\n';
        }

        private Token ReadKnownTag(int startPos, int startLine, string tagName, bool isClosing)
        {
            if (isClosing)
            {
                // </TagName>
                Advance(2 + tagName.Length); // Skip </TagName
                SkipWhitespace();
                if (Current() == '>') Advance();

                return new Token(GetEndTokenType(tagName), tagName, startPos, startLine);
            }

            // <TagName attributes...> or <TagName ... />
            Advance(1 + tagName.Length); // Skip <TagName

            // Read attributes until > or /> (handling quoted strings properly)
            _buffer.Clear();
            bool isSelfClosing = false;
            char inQuote = '\0'; // Track if we're inside a quoted string

            while (_position < _length)
            {
                char c = Current();
                
                // Handle quote state
                if (c == '"' || c == '\'')
                {
                    if (inQuote == '\0')
                        inQuote = c; // Start quote
                    else if (inQuote == c)
                        inQuote = '\0'; // End quote
                }
                
                // Only check for tag end when NOT inside quotes
                if (inQuote == '\0')
                {
                    if (c == '/' && Peek() == '>')
                    {
                        isSelfClosing = true;
                        Advance(2);
                        break;
                    }
                    if (c == '>')
                    {
                        Advance();
                        break;
                    }
                }
                
                _buffer.Append(c);
                Advance();
            }

            var attrs = _buffer.ToString().Trim();
            
            // Handle self-closing and determine token type
            var tokenType = GetStartTokenType(tagName);
            if (isSelfClosing && tagName.Equals("Element", StringComparison.OrdinalIgnoreCase))
            {
                tokenType = TokenType.ElementSelfClosing;
            }

            // For control flow tags, parse attributes into metadata
            string metadata = null;
            switch (tagName.ToUpperInvariant())
            {
                case "IF":
                case "ELSEIF":
                    // Extract condition from condition="..." attribute
                    metadata = ExtractAttributeValue(attrs, "condition");
                    break;
                case "FOREACH":
                    // Extract var and in attributes: var="item" in="Items"
                    var varName = ExtractAttributeValue(attrs, "var");
                    var collection = ExtractAttributeValue(attrs, "in");
                    metadata = varName + "|" + collection;
                    break;
            }

            return new Token(tokenType, tagName, startPos, startLine, metadata ?? attrs);
        }

        /// <summary>
        /// Extracts attribute value from attribute string
        /// </summary>
        private string ExtractAttributeValue(string attrs, string attrName)
        {
            // Look for attrName="value" or attrName='value'
            int nameStart = attrs.IndexOf(attrName, StringComparison.OrdinalIgnoreCase);
            if (nameStart < 0) return string.Empty;

            int eqPos = attrs.IndexOf('=', nameStart);
            if (eqPos < 0) return string.Empty;

            int valueStart = eqPos + 1;
            while (valueStart < attrs.Length && char.IsWhiteSpace(attrs[valueStart]))
                valueStart++;

            if (valueStart >= attrs.Length) return string.Empty;

            char quote = attrs[valueStart];
            if (quote != '"' && quote != '\'')
            {
                // No quote - read until whitespace or end
                int end = valueStart;
                while (end < attrs.Length && !char.IsWhiteSpace(attrs[end]))
                    end++;
                return attrs.Substring(valueStart, end - valueStart);
            }

            // Find closing quote
            int closeQuote = attrs.IndexOf(quote, valueStart + 1);
            if (closeQuote < 0) return string.Empty;

            return attrs.Substring(valueStart + 1, closeQuote - valueStart - 1);
        }

        private Token ReadText(int startPos, int startLine)
        {
            _buffer.Clear();

            while (_position < _length)
            {
                char c = Current();

                // Stop at special characters
                if (c == '{' || c == '}' || c == '<')
                {
                    // Check if it's actually a special sequence
                    if (c == '{' && Peek() == '{') break;
                    if (c == '<' && IsKnownTagStart()) break;
                    if (c == '}') break;
                }

                _buffer.Append(c);
                Advance();
            }

            return new Token(TokenType.Text, _buffer.ToString(), startPos, startLine);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsKnownTagStart()
        {
            int tagStart = Peek() == '/' ? _position + 2 : _position + 1;
            foreach (var tag in KnownTags)
            {
                if (MatchTagName(tagStart, tag))
                    return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SkipWhitespace()
        {
            while (_position < _length && char.IsWhiteSpace(Current()))
            {
                Advance();
            }
        }

        private static TokenType GetStartTokenType(string tagName)
        {
            switch (tagName.ToUpperInvariant())
            {
                case "ELEMENT": return TokenType.ElementStart;
                case "DATA": return TokenType.DataStart;
                case "NAV": return TokenType.NavStart;
                case "BLOCK": return TokenType.BlockStart;
                case "IF": return TokenType.If;
                case "ELSEIF": return TokenType.ElseIf;
                case "ELSE": return TokenType.Else;
                case "FOREACH": return TokenType.ForEach;
                // Component System
                case "INCLUDE": return TokenType.Include;
                case "PARAM": return TokenType.Param;
                // Layout System
                case "LAYOUT": return TokenType.Layout;
                case "SECTION": return TokenType.Section;
                case "RENDERSECTION": return TokenType.RenderSection;
                case "RENDERBODY": return TokenType.RenderBody;
                // Slots
                case "SLOT": return TokenType.Slot;
                // Template Fragments
                case "DEFINE": return TokenType.Define;
                case "RENDER": return TokenType.Render;
                default: return TokenType.ElementStart;
            }
        }

        private static TokenType GetEndTokenType(string tagName)
        {
            switch (tagName.ToUpperInvariant())
            {
                case "ELEMENT": return TokenType.ElementEnd;
                case "DATA": return TokenType.DataEnd;
                case "NAV": return TokenType.NavEnd;
                case "BLOCK": return TokenType.BlockEnd;
                case "IF": return TokenType.EndIf;
                case "FOREACH": return TokenType.EndForEach;
                // Component System
                case "INCLUDE": return TokenType.EndInclude;
                // Layout System
                case "LAYOUT": return TokenType.EndLayout;
                case "SECTION": return TokenType.EndSection;
                // Slots
                case "SLOT": return TokenType.EndSlot;
                // Template Fragments
                case "DEFINE": return TokenType.EndDefine;
                default: return TokenType.ElementEnd;
            }
        }
    }
}
