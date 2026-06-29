using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    public class RcGeneratorService
    {
        // Replaces translatable strings in-place within the raw C++ resource script.

        public string Generate(string originalContent, List<ResourceString> resourceStrings)
        {
            // Sort replacements from back-to-front to keep index locations consistent.
            if (string.IsNullOrEmpty(originalContent)) return string.Empty;
            var ordered = SortReplacements(resourceStrings);
            var sb = new StringBuilder(originalContent);
            ReplaceAllStrings(sb, ordered);
            return sb.ToString();
        }

        private List<ResourceString> SortReplacements(List<ResourceString> resourceStrings)
        {
            // Filter valid string indices and sort descending by start offset.
            return resourceStrings
                .Where(r => r.StartIndex >= 0 && r.EndIndex >= 0)
                .OrderByDescending(r => r.StartIndex)
                .ToList();
        }

        private void ReplaceAllStrings(StringBuilder sb, List<ResourceString> items)
        {
            // Replace character segments in the text builder using ResourceString entries.
            foreach (var item in items)
            {
                string text = string.IsNullOrEmpty(item.Translated) ? item.Text : item.Translated;
                string escaped = EscapeRcString(text);
                int length = item.EndIndex - item.StartIndex + 1;
                sb.Remove(item.StartIndex, length);
                sb.Insert(item.StartIndex, escaped);
            }
        }

        public static string EscapeRcString(string plainText)
        {
            // Escape double-quotes inside string literals by doubling them.
            if (plainText == null) plainText = string.Empty;
            string escaped = plainText.Replace("\"", "\"\"");
            return "\"" + escaped + "\"";
        }
    }
}
