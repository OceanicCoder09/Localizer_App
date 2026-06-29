using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    // Coordinates the translation of resource string tables using API fallback cascades
    public class TranslationService
    {
        private readonly GeminiService _geminiService = new GeminiService();
        private const int BatchSize = 50; // Translates in batches of 50 to optimize network limits
        private readonly GlossaryService? _glossaryService;

        public TranslationService(GlossaryService? glossaryService = null)
        {
            _glossaryService = glossaryService;
        }

        public async Task TranslateAsync(List<ResourceString> items, string languageName, string languageCode, string apiKey, string model, bool useGlossary = true)
        {
            for (int i = 0; i < items.Count; i += BatchSize)
            {
                var batch = items.Skip(i).Take(BatchSize).ToList();
                await TranslateBatchAsync(batch, languageName, languageCode, apiKey, model, useGlossary);
            }
        }

        // Sends batch translation requests; falls back to other model versions if the primary fails
        private async Task TranslateBatchAsync(List<ResourceString> batch, string languageName, string languageCode, string apiKey, string model, bool useGlossary)
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
                    UpdateTranslations(batch, jsonResponse);
                    return;
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
            throw new Exception($"Translation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        // Contextual constraints instruction specifying CAD suffixes, placeholder preservation, and feedback correction
        private string GetSystemInstruction(string languageName, string languageCode)
        {
            return $"You are a professional software localization system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software.\n" +
                   $"Translate the provided JSON input from English to {languageName} ({languageCode}).\n\n" +

                   "STRICT RULES:\n" +

                   "1. KEY CONTEXT ANALYSIS (CRITICAL):\n" +
                   "Before translating, analyze the resource key.\n" +
                   "Split the key by underscore '_' and treat each segment as contextual metadata.\n" +
                   "Key segments may indicate:\n" +
                   "- Scope (CURRENT, ALL, GLOBAL, LAYER, OBJECT, BLOCK, VIEWPORT)\n" +
                   "- Geometry (EDGE, FACE, BODY, SURFACE)\n" +
                   "- Entity Type (CONSTRAINT, FEATURE, PART, ASSEMBLY)\n" +
                   "- UI Role (BUTTON, LABEL, TITLE, TOOLTIP)\n" +
                   "- Message Type (ERROR, WARNING, STATUS, INFO, PROMPT, CONFIRM)\n" +
                   "- Lifecycle (NEW, DELETE, REMOVE, CREATE, UPDATE)\n" +
                   "The same English value may require different translations under different keys.\n" +
                   "Never reuse a translation solely because the English text is identical.\n\n" +

                   "2. CAD/BIM TERMINOLOGY:\n" +
                   "When glossary definitions are provided, treat them as official CAD/BIM concept definitions.\n" +
                   "Preserve the technical meaning of the concept.\n" +
                   "Avoid generic dictionary interpretations when a technical engineering meaning is more appropriate.\n" +
                   "Maintain terminology consistency across translations whenever possible.\n\n" +

                   "3. FEEDBACK-DRIVEN CORRECTION:\n" +
                   "If previous_translation and feedback are provided:\n" +
                   "- Identify the issue described in feedback.\n" +
                   "- Correct only the identified issue.\n" +
                   "- Preserve correct portions of the existing translation.\n" +
                   "- Do not introduce unrelated wording changes.\n\n" +

                   "4. PLACEHOLDER INTEGRITY:\n" +
                   "Preserve all placeholders exactly:\n" +
                   "%1, %2, %s, %d, {0}, {1}, $(VAR) and similar patterns.\n" +
                   "Translate only surrounding natural language.\n\n" +

                   "5. FORMATTING PRESERVATION:\n" +
                   "Preserve:\n" +
                   "- Trailing ':'\n" +
                   "- Trailing '...'\n" +
                   "- [Bracketed] content structure\n" +
                   "- <Angle Bracket> structure\n" +
                   "- Leading and trailing spaces\n" +
                   "- Existing punctuation\n\n" +

                   "6. COMMAND IDENTIFIERS:\n" +
                   "Single-word ALL CAPS or CamelCase CAD command identifiers should remain untranslated unless an official localized command name exists.\n\n" +

                   "7. UI AWARENESS:\n" +
                   "Short strings are likely UI labels.\n" +
                   "Keep translations concise.\n" +
                   "Do not add explanatory text.\n" +
                   "ERROR messages must sound firm.\n" +
                   "WARNING messages must sound cautionary.\n" +
                   "PROMPT messages must sound instructional.\n" +
                   "STATUS messages must sound neutral.\n" +
                   "BUTTON text should be short and action-oriented.\n\n" +

                   "Output MUST be a valid JSON array containing only:\n" +
                   "{ key, translated }\n" +
                   "Do not return markdown, explanations, or additional text.";
        }

        // Generates the JSON input containing translation keys, texts, and previous validator feedback
        private string GetPrompt(List<ResourceString> batch)
        {
            var inputs = new List<object>();
            foreach (var item in batch)
            {
                if (!string.IsNullOrEmpty(item.Translated) && !string.IsNullOrEmpty(item.ValidationFeedback))
                {
                    inputs.Add(new
                    {
                        key = item.Key,
                        text = item.Text,
                        previous_translation = item.Translated,
                        feedback = item.ValidationFeedback
                    });
                }
                else
                {
                    inputs.Add(new { key = item.Key, text = item.Text });
                }
            }
            return JsonSerializer.Serialize(inputs);
        }

        private void UpdateTranslations(List<ResourceString> batch, string jsonResponse)
        {
            string cleaned = GeminiService.CleanJson(jsonResponse);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var outputs = JsonSerializer.Deserialize<List<TranslationOutput>>(cleaned, options);
            if (outputs == null) return;
            
            foreach (var item in batch)
            {
                var match = outputs.FirstOrDefault(x => x.Key == item.Key);
                if (match != null) item.Translated = match.Translated;
            }
        }
    }

    internal class TranslationOutput
    {
        public string Key { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
    }
}
