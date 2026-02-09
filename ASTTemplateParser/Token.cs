namespace ASTTemplateParser
{
    /// <summary>
    /// Lightweight token structure for high-performance parsing
    /// </summary>
    public readonly struct Token
    {
        public readonly TokenType Type;
        public readonly string Value;
        public readonly int Position;
        public readonly int Line;
        
        /// <summary>Additional data like tag name, attributes, condition etc.</summary>
        public readonly string Metadata;

        public Token(TokenType type, string value, int position, int line, string metadata = null)
        {
            Type = type;
            Value = value;
            Position = position;
            Line = line;
            Metadata = metadata;
        }

        public bool IsEOF => Type == TokenType.EOF;
        
        public override string ToString() => $"[{Type}] '{Value}' at {Position}:{Line}";
    }
}
