using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    // =========================================================================
    // 1. Tokenizer Definitions & Service (Lexical Analyzer)
    // =========================================================================
    public enum TokenType
    {
        StringTable,
        Begin,
        End,
        Identifier,
        Number,
        StringLiteral,
        Other
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; } = string.Empty;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }

    public class RcTokenizer
    {
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

        private int ParseNext(string text, int index, List<Token> tokens)
        {
            char character = text[index];
            if (char.IsWhiteSpace(character)) return index + 1;
            if (IsComment(text, index)) return SkipComment(text, index);
            if (character == '#') return SkipDirective(text, index);
            return ReadContentToken(text, index, tokens);
        }

        private bool IsComment(string text, int index)
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
            return SkipSingleLineComment(text, index + 1);
        }

        private int ReadContentToken(string text, int index, List<Token> tokens)
        {
            char character = text[index];
            if (character == '"') return ReadStringLiteral(text, index, tokens);
            if (char.IsLetter(character) || character == '_') return ReadIdentifier(text, index, tokens);
            if (char.IsDigit(character)) return ReadNumber(text, index, tokens);
            return ReadSymbol(text, index, tokens);
        }

        private int ReadStringLiteral(string text, int index, List<Token> tokens)
        {
            int current = index + 1;
            while (current < text.Length && text[current] != '"')
            {
                current += IsEscapedQuote(text, current) ? 2 : 1;
            }
            return AddStringToken(text, index, current, tokens);
        }

        private bool IsEscapedQuote(string text, int current)
        {
            bool doubleQuotes = text[current] == '"' && current + 1 < text.Length && text[current + 1] == '"';
            bool backslashQuote = text[current] == '\\' && current + 1 < text.Length;
            return doubleQuotes || backslashQuote;
        }

        private int AddStringToken(string text, int start, int end, List<Token> tokens)
        {
            int limit = Math.Min(end - start + 1, text.Length - start);
            string val = text.Substring(start, limit);
            tokens.Add(new Token { Type = TokenType.StringLiteral, Value = val, StartIndex = start, EndIndex = end });
            return end + 1;
        }

        private int ReadIdentifier(string text, int index, List<Token> tokens)
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

    // =========================================================================
    // 2. Parser State & Service (Syntax Parser)
    // =========================================================================
    internal class ParserState
    {
        public bool InStringTable { get; set; }
        public int NestingLevel { get; set; }
        public Token? LastKeyToken { get; set; }
    }

    public class RcParserService
    {
        private readonly RcTokenizer _tokenizer = new RcTokenizer();

        public List<ResourceString> Parse(string fileContent)
        {
            var resourceStrings = new List<ResourceString>();
            if (string.IsNullOrEmpty(fileContent)) return resourceStrings;

            var tokens = _tokenizer.Tokenize(fileContent);
            ParseTokens(tokens, resourceStrings);
            return resourceStrings;
        }

        private void ParseTokens(List<Token> tokens, List<ResourceString> resourceStrings)
        {
            ParserState state = new ParserState();
            foreach (var token in tokens)
            {
                ProcessToken(token, state, resourceStrings);
            }
        }

        private void ProcessToken(Token token, ParserState state, List<ResourceString> resourceStrings)
        {
            if (token.Type == TokenType.StringTable) ResetState(state);
            else if (token.Type == TokenType.Begin) HandleBegin(state);
            else if (token.Type == TokenType.End) HandleEnd(state);
            else HandleContentToken(token, state, resourceStrings);
        }

        private void ResetState(ParserState state)
        {
            state.InStringTable = true;
            state.NestingLevel = 0;
            state.LastKeyToken = null;
        }

        private void HandleBegin(ParserState state)
        {
            if (state.InStringTable) state.NestingLevel++;
        }

        private void HandleEnd(ParserState state)
        {
            if (!state.InStringTable) return;
            state.NestingLevel--;
            if (state.NestingLevel == 0) state.InStringTable = false;
        }

        private void HandleContentToken(Token token, ParserState state, List<ResourceString> resourceStrings)
        {
            if (!state.InStringTable || state.NestingLevel <= 0) return;
            bool isKey = token.Type == TokenType.Identifier || token.Type == TokenType.Number;
            if (isKey) state.LastKeyToken = token;
            else if (token.Type == TokenType.StringLiteral) TryAddLiteral(token, state, resourceStrings);
        }

        private void TryAddLiteral(Token token, ParserState state, List<ResourceString> resourceStrings)
        {
            if (state.LastKeyToken == null) return;
            AddResourceString(token, state.LastKeyToken.Value, resourceStrings);
            state.LastKeyToken = null;
        }

        private void AddResourceString(Token token, string key, List<ResourceString> resourceStrings)
        {
            var text = UnescapeRcString(token.Value);
            resourceStrings.Add(new ResourceString
            {
                Key = key,
                Text = text,
                StartIndex = token.StartIndex,
                EndIndex = token.EndIndex
            });
        }

        public static string UnescapeRcString(string escapedText)
        {
            if (string.IsNullOrEmpty(escapedText)) return string.Empty;
            bool isQuoted = escapedText.StartsWith("\"") && escapedText.EndsWith("\"") && escapedText.Length >= 2;
            if (isQuoted)
            {
                string content = escapedText.Substring(1, escapedText.Length - 2);
                return content.Replace("\"\"", "\"");
            }
            return escapedText;
        }
    }

    // =========================================================================
    // 3. Resource Loader Service (Preview Helper)
    // =========================================================================
    public class RcResourceLoaderService
    {
        private readonly RcParserService _parser = new RcParserService();

        public Dictionary<string, string> LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return LoadFromContent(content);
        }

        public Dictionary<string, string> LoadFromContent(string content)
        {
            var parsed = _parser.Parse(content);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in parsed)
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    result[item.Key] = item.Text;
                }
            }
            return result;
        }

        public static string GetDefaultResourcesDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        }

        public static string GetResourceFilePath(string resourcesDirectory, string cultureCode)
        {
            return Path.Combine(resourcesDirectory, cultureCode + ".rc");
        }
    }
}
