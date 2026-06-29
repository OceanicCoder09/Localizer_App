using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    // QA Validation output details mapped from JSON response
    public class ValidationOutputItem
    {
        public string Key { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
    }

    // Evaluates translations using LLM-based QA quality checks
    public class AiValidationService
    {
        private readonly GeminiService _geminiService = new GeminiService();
        private const int BatchSize = 25; // Smaller batch size because checks are highly detailed
        private readonly GlossaryService? _glossaryService;

        public AiValidationService(GlossaryService? glossaryService = null)
        {
            _glossaryService = glossaryService;
        }

        public async Task<List<ValidationOutputItem>> ValidateAsync(List<ResourceString> items, string languageName, string languageCode, string apiKey, string model, bool useGlossary = true)
        {
            var results = new List<ValidationOutputItem>();
            for (int i = 0; i < items.Count; i += BatchSize)
            {
                var batch = items.Skip(i).Take(BatchSize).ToList();
                var batchResults = await ValidateBatchAsync(batch, languageName, languageCode, apiKey, model, useGlossary);
                results.AddRange(batchResults);
            }
            return results;
        }

        // Submits batch validation checks and processes model fallbacks
        private async Task<List<ValidationOutputItem>> ValidateBatchAsync(List<ResourceString> batch, string languageName, string languageCode, string apiKey, string model, bool useGlossary)
        {
            string glossaryContext = "";
            if (useGlossary && _glossaryService != null)
            {
                var matchedKVs = new List<KeyValuePair<string, string>>();
                foreach (var item in batch)
                {
                    var matches = _glossaryService.GetMatchedTerms(item.Text);
                    foreach (var match in matches)
                    {
                        if (!matchedKVs.Any(m => m.Key.Equals(match.Key, StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedKVs.Add(match);
                        }
                    }
                }

                if (matchedKVs.Count > 0)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("\nGlossary Definitions (Semantic Context):");
                    sb.AppendLine("The following glossary definitions describe official CAD/BIM concepts.");
                    sb.AppendLine("Preserve the technical meaning of these concepts.");
                    sb.AppendLine("Avoid generic dictionary interpretations when a technical CAD/BIM meaning is more appropriate.");
                    sb.AppendLine("Maintain terminology consistency across translations whenever possible.");
                    sb.AppendLine("These definitions provide semantic context, not mandatory target-language translations.\n");
                    foreach (var kv in matchedKVs)
                    {
                        sb.AppendLine($"* {kv.Key}: {kv.Value}");
                    }
                    glossaryContext = sb.ToString();
                }
            }

            string systemInstruction = GetSystemInstruction(languageName, languageCode) + glossaryContext;
            string prompt = GetPrompt(batch);

            var modelsToTry = new List<string> { model };
            var fallbackOrder = new[] 
            { 
                "gemini-2.5-flash", 
                "gemini-2-flash", 
                "gemini-2.5-flash-lite", 
                "gemini-2-flash-lite", 
                "gemini-3.1-flash-lite", 
                "gemini-3.5-flash", 
                "gemini-3-flash", 
                "gemini-2.5-pro", 
                "gemini-3.1-pro" 
            };
            foreach (var m in fallbackOrder)
            {
                if (!modelsToTry.Contains(m))
                {
                    modelsToTry.Add(m);
                }
            }

            var errors = new List<string>();
            foreach (var currentModel in modelsToTry)
            {
                try
                {
                    string jsonResponse = await _geminiService.CallApiAsync(systemInstruction, prompt, apiKey, currentModel);
                    return ParseResults(jsonResponse);
                }
                catch (Exception ex)
                {
                    errors.Add($"* {currentModel}: {ex.Message}");
                    if (GeminiService.IsFatalException(ex))
                    {
                        throw;
                    }
                }
            }

            string detailedErrors = string.Join("\n", errors);
            throw new Exception($"Validation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        // AI QA check rules covering placeholders, format, contexts, duplicates, untranslated terms, and lengths
        private string GetSystemInstruction(string languageName, string languageCode)
        {
            return $"You are a strict, professional software localization QA system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software.\n" +
                   $"Evaluate translation quality from English to {languageName} ({languageCode}) for the provided JSON input, assigning a score (0 to 100) and status based on these strict checks. Assign 'status' as 'Excellent' (90-100), 'Good' (80-89), or 'Needs Review' (0-79).\n\n" +
                   "Run the following 10 Validation Checks:\n" +
                   "1. CHECK 1 — PLACEHOLDER INTEGRITY (CRITICAL): Verify all placeholders like %1, %2, %s, %d, {0}, {1}, $(VAR) in original are exactly intact in translation (not converted to literal, omitted, or duplicated). Failure: Score 0-50, status 'Needs Review'.\n" +
                   "2. CHECK 2 — FORMATTING CHARACTER INTEGRITY (CRITICAL/MINOR): Verify trailing ': ', trailing '...', [Bracket] structure/content, <Angle bracket> structure/content, and spaces. Brackets failure: CRITICAL (Score 0-50). Spacing/punctuation: MINOR (Score 70-79), status 'Needs Review'.\n" +
                   "3. CHECK 3 — KEY NAME VS TRANSLATION CONTEXT MISMATCH (MAJOR/MINOR): Split key by underscore. Verify translation matches context (e.g. _ERR must have error tone, _WARN cautionary tone, _PROMPT instruction/question, _BTN < 25 chars, _TOOLTIP informative, _VP/_VIEWPORT scoped to current viewport, _ALL/_GLOBAL global scope, _CURRENT selected item scope, _DEF template definition, _REF reference pointer). Failure: MAJOR for scope/tone mismatch (Score 50-70), MINOR for role mismatch (Score 70-79), status 'Needs Review'.\n" +
                   "4. CHECK 4 — DUPLICATE TRANSLATION ACROSS DIFFERENT KEYS (MAJOR): Flag if two keys share the same translation but key names imply different contexts (e.g. geometric constraint vs button label). Failure: Score 50-70, status 'Needs Review'.\n" +
                   "5. CHECK 5 — UNTRANSLATED SEGMENTS (MAJOR): Flag if English segments remain untranslated (unless recognized command names or proper nouns). Failure: Score 50-70, status 'Needs Review'.\n" +
                   "6. CHECK 6 — OVER-TRANSLATION (MINOR): Flag if translation adds words, explanations, or context not present in original. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "7. CHECK 7 — LENGTH VIOLATION (MINOR): Flag if original is < 25 chars and translation is > 2.5x original character count. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "8. CHECK 8 — IDENTICAL FEEDBACK ACROSS KEYS (MINOR): Ensure distinct feedback reasoning for each key in batch. Flag if identical. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "9. CHECK 9 — SCOPE SUFFIX IGNORED (MAJOR): Flag if scope modifier (_VP, _ALL, _GLOBAL, _CURRENT, _LAYER etc.) is absent from target semantics. Failure: Score 50-70, status 'Needs Review'.\n" +
                   "10. CHECK 10 — CAD/BIM SEMANTIC ALIGNMENT (MAJOR): Verify that if a glossary term appears in the source string, the translated text preserves the technical CAD/BIM meaning described by the glossary definition. Detect obvious semantic drift or generic translations that weaken technical meaning (e.g. translating \"Viewport\" as a generic window instead of CAD viewport, or \"Feature\" as a generic element instead of geometric operation). Suggest terminology review in the feedback if multiple valid translations may exist, but do not assume a specific target-language translation is strictly required. If the translation preserves the CAD meaning, you MUST provide feedback like: \"The translation preserves the glossary-defined CAD meaning of the source term. Terminology consistency should be reviewed if organization-specific terminology standards exist.\" If semantic drift or weak/generic translation is found, reduce the score (score 50-70) and explain the terminology violation in the feedback. Failure: Score 50-70, status 'Needs Review'.\n\n" +
                   "Provide detailed, unique, and actionable feedback in the 'feedback' field specifying exactly what check failed and how to fix it. If the translation passes all checks, set status to Excellent/Good and explain why.\n" +
                   "Output MUST be a valid JSON array of objects with keys 'key', 'score', 'status', and 'feedback'. Do not add markdown formatting or wrapper code.";
        }

        private string GetPrompt(List<ResourceString> batch)
        {
            var inputs = new List<object>();
            foreach (var item in batch)
            {
                inputs.Add(new { key = item.Key, source = item.Text, translation = item.Translated });
            }
            return JsonSerializer.Serialize(inputs);
        }

        private List<ValidationOutputItem> ParseResults(string jsonResponse)
        {
            string cleaned = GeminiService.CleanJson(jsonResponse);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<ValidationOutputItem>>(cleaned, options);
            return list ?? new List<ValidationOutputItem>();
        }
    }
}
