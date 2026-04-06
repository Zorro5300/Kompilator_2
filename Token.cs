namespace WindowsFormsApp1
{
    public enum TokenType
    {
        Identifier,
        Keyword,
        Number,
        String,
        Operator,
        Semicolon,
        Colon,
        Assign,
        Dot,
        Comma,
        OpenParen,
        CloseParen,
        OpenBrace,
        CloseBrace,
        OpenBracket,
        CloseBracket,
        Comment,
        Whitespace,
        NewLine,
        Unknown,
        EndOfFile
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public Token(TokenType type, string value, int line, int column)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"{Type}: '{Value}' [строка {Line}, позиция {Column}]";
        }
    }
}