using System;
using System.Collections.Generic;
using System.Linq;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    // Parses tokens using a state machine to extract string keys/values
    public class RcParserService
    {
        private readonly RcTokenizer _tokenizer = new RcTokenizer();

        // Parses RC file text into translatable ResourceString list
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

        // Processes keyword actions (block boundaries BEGIN/END) or content keys
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

        // Inside STRINGTABLE context, pairs keys (identifiers/numbers) with string literals
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

        // Strips quotes and unescapes double quotes ("" -> ") for output strings
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
}
