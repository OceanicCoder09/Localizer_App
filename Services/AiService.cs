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
    public class GeminiService
    {
        private static readonly HttpClient Client = new HttpClient();

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
            string systemInstruction = GetSystemInstruction();
            string prompt = GetPrompt(batch, languageName, languageCode);
            
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
                }
            }

            string detailedErrors = string.Join("\n", errors);
            throw new Exception($"Translation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        private string GetSystemInstruction()
        {
            return "You are a professional software localization system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software (similar to Autodesk AutoCAD, Revit, Inventor, Fusion 360).\n" +
                   "Strictly adhere to the following rules:\n" +
                   "1. Use industry-standard professional terminology for CAD, engineering, and 3D modeling interfaces in the target language (e.g. translate 'Draft', 'Viewport', 'Extrude', 'Constraint', 'Assembly', 'Dimension Style', 'Render', 'Ribbon' using the exact standard terminology established by major CAD/BIM products like AutoCAD or Revit).\n" +
                   "2. Ensure the translation is concise and space-efficient, suitable for narrow property panels, dialog boxes, sidebars, dockable windows, and command line bars common in Autodesk-like CAD interfaces.\n" +
                   "3. Keep any formatting placeholders (like %s, %d, %f, %1, %2, {0}, {1}, {2}) and escape characters (like \\n or \\t) exactly as they are in their correct syntactic positions.\n" +
                   "4. Do not literally translate idioms or UI shortcuts (e.g., preserve shortcut keys like \\tCtrl+Alt+P or \\tF1 and translate only the user-visible label portion).\n" +
                   "5. Output MUST be a valid JSON array of objects with keys 'key' and 'translated'. Do not add markdown blocks or wrapping code.";
        }

        private string GetPrompt(List<ResourceString> batch, string languageName, string languageCode)
        {
            var inputs = new List<object>();
            foreach (var item in batch)
            {
                inputs.Add(new { key = item.Key, text = item.Text });
            }
            string serialized = JsonSerializer.Serialize(inputs);
            return "Translate to " + languageName + " (" + languageCode + ").\n\nInput JSON:\n" + serialized;
        }

        private void UpdateTranslations(List<ResourceString> batch, string jsonResponse)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var outputs = JsonSerializer.Deserialize<List<TranslationOutput>>(jsonResponse, options);
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
            string systemInstruction = GetSystemInstruction();
            string prompt = GetPrompt(batch, languageName, languageCode);

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
                }
            }

            string detailedErrors = string.Join("\n", errors);
            throw new Exception($"Validation failed after trying all models.\n\nErrors encountered:\n{detailedErrors}");
        }

        private string GetSystemInstruction()
        {
            return "You are a strict, professional software localization QA system specializing in CAD (Computer-Aided Design), BIM (Building Information Modeling), and engineering design software (similar to Autodesk products).\n" +
                   "Evaluate translation quality between English and the target language, assigning a score (0 to 100) and status based on the following tight criteria:\n" +
                   "- 'Excellent' (90-100): The translation is perfectly accurate, uses standard established CAD/engineering software terminology (matching Autodesk standard translations for UI terms like Viewport, Extrude, Assembly, Constraint, etc.), preserves all placeholders and escape characters, fits the tight space constraints of CAD sidebars/property grids, and has zero grammatical or formatting issues.\n" +
                   "- 'Good' (80-89): Minor style or naturalness improvements are possible, but the engineering terminology is standard, meaning is correct, and all placeholders and escape characters are preserved.\n" +
                   "- 'Needs Review' (0-79): Any of the following issues MUST immediately result in a score below 80 and status 'Needs Review':\n" +
                   "  1. Mistranslations, loss of engineering meaning, or incorrect CAD/BIM context (e.g., translating a standard command literally when a standard localized term in Autodesk exists, like translating 'Viewport' literally rather than using its standard CAD translation).\n" +
                   "  2. Any placeholder mismatch, corruption, missing placeholder, or misplaced escape character.\n" +
                   "  3. Truncation risks or excessively long translations compared to the limited UI space in property panels/dockable windows.\n" +
                   "  4. Grammatical errors, spelling mistakes, or awkward phrasing in the target language.\n\n" +
                   "Provide detailed, accurate, and actionable feedback in the 'feedback' field specifying exactly what is wrong or could be improved (e.g., identifying a specific non-standard CAD term, translation error, or grammatical issue). If the translation is Excellent, explain why.\n" +
                   "Output MUST be a valid JSON array of objects with keys 'key', 'score', 'status', and 'feedback'. Do not add markdown formatting or wrapper code.";
        }

        private string GetPrompt(List<ResourceString> batch, string languageName, string languageCode)
        {
            var inputs = new List<object>();
            foreach (var item in batch)
            {
                inputs.Add(new { key = item.Key, source = item.Text, translation = item.Translated });
            }
            string serialized = JsonSerializer.Serialize(inputs);
            return "Validate translations to " + languageName + " (" + languageCode + ").\n\nInput JSON:\n" + serialized;
        }

        private List<ValidationOutputItem> ParseResults(string jsonResponse)
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var list = JsonSerializer.Deserialize<List<ValidationOutputItem>>(jsonResponse, options);
            return list ?? new List<ValidationOutputItem>();
        }
    }
}
