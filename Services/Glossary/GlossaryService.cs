using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Localizer_App.Services
{
    public class GlossaryService
    {
        private readonly Dictionary<string, string> _glossary = new(StringComparer.OrdinalIgnoreCase);

        public void LoadGlossary(string csvPath)
        {
            _glossary.Clear();
            if (!File.Exists(csvPath)) return;

            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                string? header = reader.ReadLine(); // Read header
                while (!reader.EndOfStream)
                {
                    string? line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = ParseCsvLine(line);
                    if (parts.Count >= 2)
                    {
                        string term = parts[0].Trim();
                        string definition = parts[1].Trim();
                        if (!string.IsNullOrEmpty(term))
                        {
                            _glossary[term] = definition;
                        }
                    }
                }
            }
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var currentToken = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentToken.Append('"');
                        i++; // Skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                else
                {
                    currentToken.Append(c);
                }
            }
            result.Add(currentToken.ToString());
            return result;
        }

        public List<KeyValuePair<string, string>> GetMatchedTerms(string text)
        {
            var matched = new List<KeyValuePair<string, string>>();
            if (string.IsNullOrWhiteSpace(text)) return matched;

            foreach (var kvp in _glossary)
            {
                if (ContainsWord(text, kvp.Key))
                {
                    matched.Add(kvp);
                }
            }
            return matched;
        }

        private bool ContainsWord(string text, string word)
        {
            int index = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
            while (index != -1)
            {
                bool startOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                bool endOk = index + word.Length == text.Length || !char.IsLetterOrDigit(text[index + word.Length]);

                if (startOk && endOk) return true;
                index = text.IndexOf(word, index + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }
}
