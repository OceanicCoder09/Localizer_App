using System;
using System.Collections.Generic;
using System.Text;

namespace Localizer_App.Services
{
    // Lexical analyzer that scans Windows C++ Resource Scripts (.rc) into tokens
    public class RcTokenizer
    {
        // Tokenizes the entire string content of an RC file
        public List<Token> Tokenize(string text)
        {
            List<Token> tokens = new List<Token>();
            int index = 0;
            while (index < text.Length)
            {
                index = ParseNext(text, index, tokens);
            }
            return tokens;
        }

        // Skips whitespaces, comments, or directives, then reads content tokens
        private int ParseNext(string text, int index, List<Token> tokens)
        {
            char character = text[index];
            if (char.IsWhiteSpace(character)) return index + 1;

            if (character == '/' && IsCommentStart(text, index))
            {
                return SkipComment(text, index);
            }

            if (character == '#')
            {
                return SkipDirective(text, index);
            }

            if (character == '"')
            {
                return ReadStringLiteral(text, index, tokens);
            }

            if (char.IsLetter(character) || character == '_')
            {
                return ReadKeywordOrIdentifier(text, index, tokens);
            }

            if (char.IsDigit(character))
            {
                return ReadNumber(text, index, tokens);
            }

            return ReadSymbol(text, index, tokens);
        }

        private bool IsCommentStart(string text, int index)
        {
            bool isSlash = text[index] == '/';
            bool hasNext = index + 1 < text.Length;
            char nextChar = hasNext ? text[index + 1] : '\0';
            return isSlash && (nextChar == '/' || nextChar == '*');
        }

        private int SkipComment(string text, int index)
        {
            if (text[index + 1] == '/')
            {
                return SkipSingleLineComment(text, index + 2);
            }
            return SkipBlockComment(text, index + 2);
        }

        private int SkipSingleLineComment(string text, int index)
        {
            int current = index;
            while (current < text.Length && text[current] != '\n' && text[current] != '\r')
            {
                current++;
            }
            return current;
        }

        private int SkipBlockComment(string text, int index)
        {
            int current = index;
            while (current < text.Length)
            {
                bool isStar = text[current] == '*';
                bool nextIsSlash = current + 1 < text.Length && text[current + 1] == '/';
                if (isStar && nextIsSlash) return current + 2;
                current++;
            }
            return current;
        }

        private int SkipDirective(string text, int index)
        {
            int current = index + 1;
            while (current < text.Length && text[current] != '\n' && text[current] != '\r')
            {
                // Handle preprocessor line continuation character '\'
                if (text[current] == '\\' && current + 1 < text.Length)
                {
                    char next = text[current + 1];
                    if (next == '\n' || next == '\r')
                    {
                        current += 2;
                        if (next == '\r' && current < text.Length && text[current] == '\n')
                        {
                            current++;
                        }
                        continue;
                    }
                }
                current++;
            }
            return current;
        }

        private int ReadStringLiteral(string text, int index, List<Token> tokens)
        {
            int current = index + 1;
            while (current < text.Length)
            {
                if (text[current] == '"')
                {
                    // Handle escaped double quote ("") in resource file string tables
                    bool nextIsQuote = current + 1 < text.Length && text[current + 1] == '"';
                    if (nextIsQuote)
                    {
                        current += 2;
                        continue;
                    }
                    // Literal completed
                    string val = text.Substring(index, current - index + 1);
                    tokens.Add(new Token { Type = TokenType.StringLiteral, Value = val, StartIndex = index, EndIndex = current });
                    return current + 1;
                }
                current++;
            }
            // Unclosed string literal fallback
            string fallback = text.Substring(index);
            tokens.Add(new Token { Type = TokenType.StringLiteral, Value = fallback, StartIndex = index, EndIndex = text.Length - 1 });
            return text.Length;
        }

        private int ReadKeywordOrIdentifier(string text, int index, List<Token> tokens)
        {
            int current = index;
            while (current < text.Length && (char.IsLetterOrDigit(text[current]) || text[current] == '_'))
            {
                current++;
            }
            string val = text.Substring(index, current - index);
            TokenType type = GetKeywordType(val);
            tokens.Add(new Token { Type = type, Value = val, StartIndex = index, EndIndex = current - 1 });
            return current;
        }

        private TokenType GetKeywordType(string val)
        {
            if (val.Equals("STRINGTABLE", StringComparison.OrdinalIgnoreCase)) return TokenType.StringTable;
            if (val.Equals("BEGIN", StringComparison.OrdinalIgnoreCase)) return TokenType.Begin;
            if (val.Equals("END", StringComparison.OrdinalIgnoreCase)) return TokenType.End;
            return TokenType.Identifier;
        }

        private int ReadNumber(string text, int index, List<Token> tokens)
        {
            int current = index;
            while (current < text.Length && char.IsDigit(text[current]))
            {
                current++;
            }
            string val = text.Substring(index, current - index);
            tokens.Add(new Token { Type = TokenType.Number, Value = val, StartIndex = index, EndIndex = current - 1 });
            return current;
        }

        private int ReadSymbol(string text, int index, List<Token> tokens)
        {
            char character = text[index];
            TokenType type = GetSymbolType(character);
            tokens.Add(new Token { Type = type, Value = character.ToString(), StartIndex = index, EndIndex = index });
            return index + 1;
        }

        private TokenType GetSymbolType(char character)
        {
            if (character == '{') return TokenType.Begin;
            if (character == '}') return TokenType.End;
            return TokenType.Other;
        }
    }
}
