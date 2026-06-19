using System;
using System.Collections.Generic;
using System.Linq;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    public class ValidationService
    {
        // Why: Validates translations locally by checking placeholder match and empty strings.
        private static readonly string[] Placeholders = { "%s", "%d", "%f", "%1", "%2", "{0}", "{1}", "{2}" };

        public ValidationResult Validate(List<ResourceString> original, List<ResourceString> translated)
        {
            // Why: Run all local validation checks and return results.
            var result = new ValidationResult { TotalStrings = original.Count, Errors = new List<string>() };
            CheckCount(original.Count, translated.Count, result);
            CheckTranslations(original, translated, result);
            return result;
        }

        private void CheckCount(int originalCount, int translatedCount, ValidationResult result)
        {
            // Why: Verify if original and translated string counts match.
            result.CountValidationPassed = originalCount == translatedCount;
            if (!result.CountValidationPassed)
            {
                result.Errors.Add("[Count Error] Count mismatch: Original has " + originalCount + ", Translated has " + translatedCount);
            }
        }

        private void CheckTranslations(List<ResourceString> original, List<ResourceString> translated, ValidationResult result)
        {
            // Why: Scan each string to perform empty and placeholder validations.
            bool allNotEmpty = true;
            bool allPlaceholders = true;
            var map = translated.ToDictionary(t => t.Key, t => t.Translated, StringComparer.Ordinal);
            CheckList(original, map, result, ref allNotEmpty, ref allPlaceholders);
            result.EmptyValidationPassed = allNotEmpty;
            result.PlaceholderValidationPassed = allPlaceholders;
        }

        private void CheckList(List<ResourceString> original, Dictionary<string, string> map, ValidationResult result, ref bool allNotEmpty, ref bool allPlaceholders)
        {
            // Why: Loop through original list and check translation matches.
            foreach (var item in original)
            {
                map.TryGetValue(item.Key, out string? text);
                bool passed = CheckItem(item, text, result, ref allNotEmpty, ref allPlaceholders);
                if (passed) result.Passed++; else result.Failed++;
            }
        }

        private bool CheckItem(ResourceString originalItem, string? translation, ValidationResult result, ref bool allNotEmpty, ref bool allPlaceholders)
        {
            // Why: Verify a single resource translation item and set its inline feedback.
            var localErrors = new List<string>();

            if (string.IsNullOrEmpty(translation))
            {
                allNotEmpty = false;
                string msg = "[Empty Error] Translation is empty.";
                result.Errors.Add(msg + " (Key: '" + originalItem.Key + "')");
                localErrors.Add(msg);

                originalItem.ValidationScore = 0;
                originalItem.ValidationStatus = "Needs Review";
                originalItem.ValidationFeedback = string.Join(" ", localErrors);
                return false;
            }

            bool passed = true;

            // 1. Check placeholders
            foreach (var placeholder in Placeholders)
            {
                int origCount = CountOccurrences(originalItem.Text, placeholder);
                int transCount = CountOccurrences(translation, placeholder);
                if (origCount != transCount)
                {
                    passed = false;
                    allPlaceholders = false;
                    string msg = $"[Placeholder Mismatch] '{placeholder}' count mismatch (Original: {origCount}, Translated: {transCount}).";
                    result.Errors.Add(msg + " (Key: '" + originalItem.Key + "')");
                    localErrors.Add(msg);
                }
            }

            // 2. Check escape sequences
            var escapes = new[] { "\\n", "\\t" };
            foreach (var escape in escapes)
            {
                int origCount = CountOccurrences(originalItem.Text, escape);
                int transCount = CountOccurrences(translation, escape);
                if (origCount != transCount)
                {
                    passed = false;
                    string msg = $"[Escape Mismatch] '{escape}' count mismatch (Original: {origCount}, Translated: {transCount}).";
                    result.Errors.Add(msg + " (Key: '" + originalItem.Key + "')");
                    localErrors.Add(msg);
                }
            }

            if (!passed)
            {
                originalItem.ValidationScore = 0;
                originalItem.ValidationStatus = "Needs Review";
                originalItem.ValidationFeedback = string.Join(" ", localErrors);
                return false;
            }

            // If local validation passes, and it doesn't have an AI validation status yet, set default local success status.
            if (string.IsNullOrEmpty(originalItem.ValidationStatus))
            {
                originalItem.ValidationScore = 100;
                originalItem.ValidationStatus = "Excellent";
                originalItem.ValidationFeedback = "Local syntax checks passed.";
            }

            return true;
        }

        private int CountOccurrences(string source, string pattern)
        {
            // Why: Count occurrences of a specific placeholder pattern.
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(pattern)) return 0;
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }
    }
}
