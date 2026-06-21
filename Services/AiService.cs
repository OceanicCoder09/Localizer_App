using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Localizer_App.Models;

namespace Localizer_App.Services
{
    // =========================================================================
    // 1. Gemini Client Service (Low-Level Network Handler)
    // =========================================================================
    public class FatalGeminiException : Exception
    {
        public FatalGeminiException(string message) : base(message) { }
    }

    public class GeminiService
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public async Task<string> CallApiAsync(string systemInstruction, string prompt, string apiKey, string model)
        {
            string mappedModel = MapModelToApiName(model);
            string url = "https://generativelanguage.googleapis.com/v1beta/models/" + mappedModel + ":generateContent?key=" + apiKey;
            string requestJson = BuildRequestJson(systemInstruction, prompt);
            StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await Client.PostAsync(url, content);
            return await ParseResponseAsync(response);
        }

        private string MapModelToApiName(string model)
        {
            if (string.IsNullOrEmpty(model)) return string.Empty;
            string m = model.ToLower().Trim();
            return m switch
            {
                "gemini-2-flash" => "gemini-2.0-flash",
                "gemini-2-flash-lite" => "gemini-2.0-flash-lite",
                "gemini-3-flash" => "gemini-3-flash-preview",
                "gemini-3.1-pro" => "gemini-3.1-pro-preview",
                "gemini-3-1-pro" => "gemini-3.1-pro-preview",
                _ => model
            };
        }

        private string BuildRequestJson(string systemInstruction, string prompt)
        {
            var payload = new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new { responseMimeType = "application/json", temperature = 0.0 }
            };
            return JsonSerializer.Serialize(payload);
        }

        private async Task<string> ParseResponseAsync(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    throw new FatalGeminiException("Gemini API authentication/authorization error (" + response.StatusCode + "): " + error);
                }
                throw new Exception("Gemini API error (" + response.StatusCode + "): " + error);
            }
            string json = await response.Content.ReadAsStringAsync();
            return ExtractTextFromJson(json);
        }

        private string ExtractTextFromJson(string json)
        {
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                JsonElement candidates = root.GetProperty("candidates");
                JsonElement part = candidates[0].GetProperty("content").GetProperty("parts")[0];
                return part.GetProperty("text").GetString() ?? string.Empty;
            }
        }

        public static string CleanJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            int firstBracket = input.IndexOf('[');
            int lastBracket = input.LastIndexOf(']');

            if (firstBracket != -1 && lastBracket != -1 && lastBracket > firstBracket)
            {
                return input.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

            // Fallback trimming
            string trimmed = input.Trim();
            if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(7).Trim();
            }
            else if (trimmed.StartsWith("```"))
            {
                trimmed = trimmed.Substring(3).Trim();
            }

            if (trimmed.EndsWith("```"))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - 3).Trim();
            }

            return trimmed;
        }

        public static bool IsFatalException(Exception ex)
        {
            Exception? current = ex;
            while (current != null)
            {
                if (current is FatalGeminiException || 
                    current is HttpRequestException || 
                    current is TaskCanceledException || 
                    current is OperationCanceledException)
                {
                    return true;
                }
                current = current.InnerException;
            }
            return false;
        }
    }

    // =========================================================================
    // 2. Translation Service (AI-Assisted Translator)
    // =========================================================================
    public class TranslationService
    {
        private readonly GeminiService _geminiService = new GeminiService();
        private const int BatchSize = 50;

        public async Task TranslateAsync(List<ResourceString> items, string languageName, string languageCode, string apiKey, string model)
        {
            for (int i = 0; i < items.Count; i += BatchSize)
            {
                var batch = items.Skip(i).Take(BatchSize).ToList();
                await TranslateBatchAsync(batch, languageName, languageCode, apiKey, model);
            }
        }

        private async Task TranslateBatchAsync(List<ResourceString> batch, string languageName, string languageCode, string apiKey, string model)
        {
            string systemInstruction = GetSystemInstruction(languageName, languageCode);
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
                    return; // Success!
                }
                catch (Exception ex)
                {
                    errors.Add($"* {currentModel}: {ex.Message}");
                    if (GeminiService.IsFatalException(ex))
                    {
                        throw; // Early abort on network / timeout / auth errors
                    }
                }
            }

            string detailedErrors = string.Join("\n", errors);
            throw new Exception($"Translation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        private string GetSystemInstruction(string languageName, string languageCode)
        {
            return $"You are a professional software localization system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software (similar to Autodesk AutoCAD, Revit, Inventor, Fusion 360).\n" +
                   $"Translate the provided JSON input to {languageName} ({languageCode}).\n" +
                   "Strictly adhere to the following rules:\n" +
                   "1. CONTEXT SEGMENTS: Split every key name by underscore \"_\" and read each segment as a modifier/context:\n" +
                   "   - FILL, COLOR, STYLE -> visual property (may need adjective form)\n" +
                   "   - EDGE, FACE, BODY -> geometric/structural element (may need noun form)\n" +
                   "   - VP, VIEWPORT -> scoped to active viewport only, not global\n" +
                   "   - ALL, GLOBAL -> applies to everything, not scoped\n" +
                   "   - LAYER, OBJECT, BLOCK -> scoped to that specific entity type\n" +
                   "   - CON, CONSTRAINT -> a rule or relationship, not a button label\n" +
                   "   - SNAP, TRACK -> cursor behavior shown as tooltip\n" +
                   "   - DEF, DEFINITION -> a template or blueprint, not an instance\n" +
                   "   - REF, REFERENCE -> an instance pointing to something defined elsewhere\n" +
                   "   - CMD, COMMAND -> a software command name identifier\n" +
                   "   - ERR, ERROR -> error message shown after something failed\n" +
                   "   - WARN, WARNING -> warning shown before something might fail\n" +
                   "   - PROMPT -> a question or instruction asked to user mid-command\n" +
                   "   - INFO, STATUS -> passive status message shown to user\n" +
                   "   - LABEL, TITLE -> heading or field label inside a dialog\n" +
                   "   - BTN, BUTTON -> clickable button (must be short)\n" +
                   "   - TIP, TOOLTIP -> hover text (can be slightly longer)\n" +
                   "   - MSG -> general message to user\n" +
                   "   - CURRENT -> applies to active/selected item only\n" +
                   "   - NEW -> creating something new\n" +
                   "   - DELETE, REMOVE -> destructive action\n" +
                   "   - CONFIRM -> confirmation dialog text\n" +
                   "2. CONTEXT BEFORE VALUE (CRITICAL): Same English value under different keys = potentially different translations. Verify whether the target language uses the same word for both contexts. Never copy a translation from one key to another just because English is identical.\n" +
                   "   - CRITICAL DIFFERENCE RULES:\n" +
                   "     * 'Solid' under IDS_SOLID -> translate as a generic adjective (e.g. ठोस / ソリッド).\n" +
                   "     * 'Solid' under IDS_SOLID_FILL -> translate as a solid pattern/fill (e.g. ठोस भरा हुआ / ソリッド塗りつぶし).\n" +
                   "     * 'Solid' under IDS_SOLID_EDGE -> translate as a continuous solid geometry edge/line (e.g. ठोस किनारा / 実線 or ソリッドエッジ).\n" +
                   "     * 'Freeze' under IDS_FREEZE -> translate as generic action (e.g. फ़्रीज़ करें / フリーズ).\n" +
                   "     * 'Freeze' under IDS_FREEZE_VP -> translate with active viewport scope (e.g. व्यूपोर्ट में फ़्रीज़ करें / ビューポートでフリーズ).\n" +
                   "     * 'Lock' under IDS_LOCK -> translate as generic action (e.g. लॉक करें / ロック).\n" +
                   "     * 'Lock' under IDS_LOCK_LAYER -> translate with layer scope (e.g. लेयर लॉक करें / レイヤーロック).\n" +
                   "     * 'Offset' under IDS_OFFSET -> translate as generic CAD command (e.g. ऑफ़सेट / オフセット).\n" +
                   "     * 'Offset' under IDS_OFFSET_FACE -> translate with face geometry scope (e.g. फलक ऑफ़सेट / 面オフセット).\n" +
                   "3. FEEDBACK-DRIVEN CORRECTION: If a JSON object contains `previous_translation` and `feedback`, it means the translation was flagged as incorrect or low-scoring by a QA validator. Use the provided feedback strictly to correct the translation and return the improved, correct version in the output.\n" +
                   "4. PLACEHOLDERS: Preserve %1, %2, %s, %d, {0}, {1}, $(VAR), and similar patterns exactly as-is. Translate only the surrounding natural language. Reorder them only if target language grammar requires it.\n" +
                   "5. FORMATTING: Preserve trailing ': ', trailing '...', [Option1/Option2] brackets (translate inside words), <Default> angle brackets (translate inside words), and leading/trailing spaces. Do not alter punctuation.\n" +
                   "6. COMMAND NAMES: Single-word ALL CAPS or CamelCase technical identifiers representing command names (e.g. REGEN, XREF, OVERKILL) should NOT be translated unless official localized version exists.\n" +
                   "7. UI LENGTH & TONE AWARENESS:\n" +
                   "   - Short strings under 20 chars are UI labels with limited space. Use concise standard form in target language. Do not add explanatory words.\n" +
                   "   - Tones: _ERR/_ERROR must sound firm/specific; _WARN/_WARNING must sound cautionary; _PROMPT must read as instruction/question; _STATUS/_INFO must read as neutral passive statement; _BTN/_BUTTON must read as action verb or short noun.\n" +
                   "Output MUST be a valid JSON array of objects with keys 'key' and 'translated'. Do not add markdown formatting or wrapper code.";
        }

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

    // =========================================================================
    // 3. AI Validation Service (AI QA Evaluator)
    // =========================================================================
    public class ValidationOutputItem
    {
        public string Key { get; set; } = string.Empty;
        public int Score { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
    }

    public class AiValidationService
    {
        private readonly GeminiService _geminiService = new GeminiService();
        private const int BatchSize = 25;

        public async Task<List<ValidationOutputItem>> ValidateAsync(List<ResourceString> items, string languageName, string languageCode, string apiKey, string model)
        {
            var results = new List<ValidationOutputItem>();
            for (int i = 0; i < items.Count; i += BatchSize)
            {
                var batch = items.Skip(i).Take(BatchSize).ToList();
                var batchResults = await ValidateBatchAsync(batch, languageName, languageCode, apiKey, model);
                results.AddRange(batchResults);
            }
            return results;
        }

        private async Task<List<ValidationOutputItem>> ValidateBatchAsync(List<ResourceString> batch, string languageName, string languageCode, string apiKey, string model)
        {
            string systemInstruction = GetSystemInstruction(languageName, languageCode);
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
                        throw; // Early abort on network / timeout / auth errors
                    }
                }
            }

            string detailedErrors = string.Join("\n", errors);
            throw new Exception($"Validation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        private string GetSystemInstruction(string languageName, string languageCode)
        {
            return $"You are a strict, professional software localization QA system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software.\n" +
                   $"Evaluate translation quality from English to {languageName} ({languageCode}) for the provided JSON input, assigning a score (0 to 100) and status based on these strict checks. Assign 'status' as 'Excellent' (90-100), 'Good' (80-89), or 'Needs Review' (0-79).\n\n" +
                   "Run the following 9 Validation Checks:\n" +
                   "1. CHECK 1 — PLACEHOLDER INTEGRITY (CRITICAL): Verify all placeholders like %1, %2, %s, %d, {0}, {1}, $(VAR) in original are exactly intact in translation (not converted to literal, omitted, or duplicated). Failure: Score 0-50, status 'Needs Review'.\n" +
                   "2. CHECK 2 — FORMATTING CHARACTER INTEGRITY (CRITICAL/MINOR): Verify trailing ': ', trailing '...', [Bracket] structure/content, <Angle bracket> structure/content, and spaces. Brackets failure: CRITICAL (Score 0-50). Spacing/punctuation: MINOR (Score 70-79), status 'Needs Review'.\n" +
                   "3. CHECK 3 — KEY NAME VS TRANSLATION CONTEXT MISMATCH (MAJOR/MINOR): Split key by underscore. Verify translation matches context (e.g. _ERR must have error tone, _WARN cautionary tone, _PROMPT instruction/question, _BTN < 25 chars, _TOOLTIP informative, _VP/_VIEWPORT scoped to current viewport, _ALL/_GLOBAL global scope, _CURRENT selected item scope, _DEF template definition, _REF reference pointer). Failure: MAJOR for scope/tone mismatch (Score 50-70), MINOR for role mismatch (Score 70-79), status 'Needs Review'.\n" +
                   "4. CHECK 4 — DUPLICATE TRANSLATION ACROSS DIFFERENT KEYS (MAJOR): Flag if two keys share the same translation but key names imply different contexts (e.g. geometric constraint vs button label). Failure: Score 50-70, status 'Needs Review'.\n" +
                   "5. CHECK 5 — UNTRANSLATED SEGMENTS (MAJOR): Flag if English segments remain untranslated (unless recognized command names or proper nouns). Failure: Score 50-70, status 'Needs Review'.\n" +
                   "6. CHECK 6 — OVER-TRANSLATION (MINOR): Flag if translation adds words, explanations, or context not present in original. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "7. CHECK 7 — LENGTH VIOLATION (MINOR): Flag if original is < 25 chars and translation is > 2.5x original character count. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "8. CHECK 8 — IDENTICAL FEEDBACK ACROSS KEYS (MINOR): Ensure distinct feedback reasoning for each key in batch. Flag if identical. Failure: Score 70-79, status 'Needs Review'.\n" +
                   "9. CHECK 9 — SCOPE SUFFIX IGNORED (MAJOR): Flag if scope modifier (_VP, _ALL, _GLOBAL, _CURRENT, _LAYER etc.) is absent from target semantics. Failure: Score 50-70, status 'Needs Review'.\n\n" +
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
