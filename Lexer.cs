using System;
using System.Collections.Generic;
using System.Text;
using WindowsFormsApp1;

namespace WindowsFormsApp1
{
    public class Lexer
    {
        private string _sourceCode;
        private int _position;
        private int _line;
        private int _column;
        private char _currentChar;

        // Ключевые слова Pascal
        private static readonly HashSet<string> _pascalKeywords = new HashSet<string>
        {
            "program", "begin", "end", "var", "const", "type", "procedure",
            "function", "if", "then", "else", "record", "case", "of", "while", "do",
            "for", "to", "downto", "repeat", "until", "with", "goto", "label",
            "array", "record", "set", "file", "string", "integer", "real",
            "boolean", "char", "true", "false", "and", "or", "not", "div",
            "mod", "in", "packed", "nil", "uses", "interface", "implementation",
            "unit", "library", "exports", "initialization", "finalization",
            "inherited", "class", "object", "constructor", "destructor",
            "public", "private", "protected", "published", "property", "read",
            "write", "default", "stored", "nodefault", "override", "virtual",
            "abstract", "reintroduce", "overload", "dynamic", "message"
        };

        public Lexer(string sourceCode)
        {
            _sourceCode = sourceCode;
            _position = 0;
            _line = 1;
            _column = 1;
            _currentChar = _sourceCode.Length > 0 ? _sourceCode[0] : '\0';
        }

        private void Advance()
        {
            if (_currentChar == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }

            _position++;
            _currentChar = _position < _sourceCode.Length ? _sourceCode[_position] : '\0';
        }

        private void SkipWhitespace()
        {
            while (_currentChar != '\0' && char.IsWhiteSpace(_currentChar) && _currentChar != '\n')
            {
                Advance();
            }
        }

        private char Peek()
        {
            return _position + 1 < _sourceCode.Length ? _sourceCode[_position + 1] : '\0';
        }

        // Основной метод - получаем следующий токен
        public Token GetNextToken()
        {
            while (_currentChar != '\0')
            {
                // Пропускаем пробелы (но не новые строки)
                if (char.IsWhiteSpace(_currentChar) && _currentChar != '\n')
                {
                    SkipWhitespace();
                    continue;
                }

                int currentLine = _line;
                int currentColumn = _column;

                // Новая строка
                if (_currentChar == '\n')
                {
                    Advance();
                    return new Token(TokenType.NewLine, "\\n", currentLine, currentColumn);
                }

                // Pascal комментарии: { ... } или (* ... *)
                if (_currentChar == '{')
                {
                    return ReadPascalCommentBrace(currentLine, currentColumn);
                }

                if (_currentChar == '(' && Peek() == '*')
                {
                    return ReadPascalCommentParen(currentLine, currentColumn);
                }

                // Комментарии // (нестандартные для Pascal, но часто используются)
                if (_currentChar == '/' && Peek() == '/')
                {
                    return ReadLineComment(currentLine, currentColumn);
                }

                // Идентификаторы и ключевые слова
                if (char.IsLetter(_currentChar) || _currentChar == '_')
                {
                    return ReadIdentifierOrKeyword(currentLine, currentColumn);
                }

                // Числа
                if (char.IsDigit(_currentChar))
                {
                    return ReadNumber(currentLine, currentColumn);
                }

                // Строки (в Pascal используются одинарные кавычки)
                if (_currentChar == '\'')
                {
                    return ReadPascalString(currentLine, currentColumn);
                }

                // Операторы и пунктуация
                switch (_currentChar)
                {
                    case ';':
                        Advance();
                        return new Token(TokenType.Semicolon, ";", currentLine, currentColumn);

                    case ':':
                        if (Peek() == '=')
                        {
                            Advance(); // :
                            Advance(); // =
                            return new Token(TokenType.Assign, ":=", currentLine, currentColumn);
                        }
                        else
                        {
                            Advance();
                            return new Token(TokenType.Colon, ":", currentLine, currentColumn);
                        }

                    case ',':
                        Advance();
                        return new Token(TokenType.Comma, ",", currentLine, currentColumn);

                    case '.':
                        Advance();
                        return new Token(TokenType.Dot, ".", currentLine, currentColumn);

                    case '(':
                        Advance();
                        return new Token(TokenType.OpenParen, "(", currentLine, currentColumn);

                    case ')':
                        Advance();
                        return new Token(TokenType.CloseParen, ")", currentLine, currentColumn);

                    case '[':
                        Advance();
                        return new Token(TokenType.OpenBracket, "[", currentLine, currentColumn);

                    case ']':
                        Advance();
                        return new Token(TokenType.CloseBracket, "]", currentLine, currentColumn);

                    case '=':
                    case '<':
                    case '>':
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                        return ReadPascalOperator(currentLine, currentColumn);
                }

                // Неизвестный символ
                char unknown = _currentChar;
                Advance();
                return new Token(TokenType.Unknown, unknown.ToString(), currentLine, currentColumn);
            }

            return new Token(TokenType.EndOfFile, "", _line, _column);
        }

        private Token ReadIdentifierOrKeyword(int line, int column)
        {
            StringBuilder sb = new StringBuilder();

            while (_currentChar != '\0' && (char.IsLetterOrDigit(_currentChar) || _currentChar == '_'))
            {
                sb.Append(_currentChar);
                Advance();
            }

            string value = sb.ToString();
            // Pascal не чувствителен к регистру, но для удобства сохраняем как есть
            TokenType type = _pascalKeywords.Contains(value.ToLower()) ?
                TokenType.Keyword : TokenType.Identifier;

            return new Token(type, value, line, column);
        }

        private Token ReadNumber(int line, int column)
        {
            StringBuilder sb = new StringBuilder();

            while (_currentChar != '\0' && (char.IsDigit(_currentChar) || _currentChar == '.'))
            {
                sb.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.Number, sb.ToString(), line, column);
        }

        private Token ReadPascalString(int line, int column)
        {
            Advance(); // пропускаем открывающую кавычку
            StringBuilder sb = new StringBuilder();

            while (_currentChar != '\0' && _currentChar != '\'')
            {
                // Обработка двух кавычек подряд (экранирование в Pascal)
                if (_currentChar == '\'' && Peek() == '\'')
                {
                    sb.Append('\'');
                    Advance(); // первая кавычка
                    Advance(); // вторая кавычка
                }
                else
                {
                    sb.Append(_currentChar);
                    Advance();
                }
            }

            if (_currentChar == '\'')
            {
                Advance(); // пропускаем закрывающую кавычку
            }

            return new Token(TokenType.String, sb.ToString(), line, column);
        }

        private Token ReadPascalCommentBrace(int line, int column)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_currentChar); // {
            Advance();

            while (_currentChar != '\0' && _currentChar != '}')
            {
                sb.Append(_currentChar);
                Advance();
            }

            if (_currentChar == '}')
            {
                sb.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.Comment, sb.ToString(), line, column);
        }

        private Token ReadPascalCommentParen(int line, int column)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_currentChar); // (
            Advance(); // *
            sb.Append(_currentChar);
            Advance();

            while (_currentChar != '\0')
            {
                if (_currentChar == '*' && Peek() == ')')
                {
                    sb.Append(_currentChar);
                    Advance();
                    sb.Append(_currentChar);
                    Advance();
                    break;
                }
                sb.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.Comment, sb.ToString(), line, column);
        }

        private Token ReadLineComment(int line, int column)
        {
            StringBuilder sb = new StringBuilder();

            while (_currentChar != '\0' && _currentChar != '\n')
            {
                sb.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.Comment, sb.ToString(), line, column);
        }

        private Token ReadPascalOperator(int line, int column)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(_currentChar);
            Advance();

            // Проверяем двухсимвольные операторы
            if ((sb[0] == '<' && _currentChar == '=') ||
                (sb[0] == '>' && _currentChar == '=') ||
                (sb[0] == '<' && _currentChar == '>'))
            {
                sb.Append(_currentChar);
                Advance();
            }

            return new Token(TokenType.Operator, sb.ToString(), line, column);
        }

        // Получить все токены (для отладки)
        public List<Token> GetAllTokens()
        {
            var tokens = new List<Token>();
            Token token;

            do
            {
                token = GetNextToken();
                if (token.Type != TokenType.Whitespace && token.Type != TokenType.NewLine)
                {
                    tokens.Add(token);
                }
            } while (token.Type != TokenType.EndOfFile);

            return tokens;
        }
    }
}