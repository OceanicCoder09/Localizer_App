using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Localizer_App.Services
{
    // Handles API connections, model routing, and JSON serialization for Gemini
    public class GeminiService
    {
        private static readonly HttpClient Client = new HttpClient();

        // Performs the HTTP POST call to Gemini generateContent endpoint
        public async Task<string> CallApiAsync(string systemInstruction, string prompt, string apiKey, string model)
        {
            string mappedModel = MapModelToApiName(model);
            string url = "https://generativelanguage.googleapis.com/v1beta/models/" + mappedModel + ":generateContent?key=" + apiKey;
            
            // // Fix URL format (remove duplicate 'gener')
            // url = url.Replace("genergenerative", "generative");

            string requestJson = BuildRequestJson(systemInstruction, prompt);
            StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            
            HttpResponseMessage response = await Client.PostAsync(url, content);
            return await ParseResponseAsync(response);
        }

        // Standardizes UI model selection into Gemini model names
        private string MapModelToApiName(string model)
        {
            if (string.IsNullOrEmpty(model)) return string.Empty;
            string m = model.ToLower().Trim();
            return m switch
            {
                "gemini-2-flash" => "gemini-2.0-flash",
                "gemini-2-flash-lite" => "gemini-2.0-flash-lite",
                "gemini-2.5-flash" => "gemini-2.5-flash",
                "gemini-2.5-flash-lite" => "gemini-2.5-flash-lite",
                "gemini-2.5-pro" => "gemini-2.5-pro",
                "gemini-3-flash" => "gemini-3-flash-preview",
                "gemini-3.5-flash" => "gemini-3.5-flash-preview",
                "gemini-3.1-pro" => "gemini-3.1-pro-preview",
                "gemini-3-1-pro" => "gemini-3.1-pro-preview",
                "gemini-3.1-flash-lite" => "gemini-3.1-flash-lite-preview",
                _ => model
            };
        }

        // Formats payload; setting temperature to 0 guarantees deterministic results
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

        // Processes API response; throws FatalGeminiException on 401/403 to abort early
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

        // Extracts content from Gemini JSON structure candidates[0]->content->parts[0]->text
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

        // Strips markdown wrapping (e.g. ```json ... ```) to yield pure JSON
        public static string CleanJson(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            int firstBracket = input.IndexOf('[');
            int lastBracket = input.LastIndexOf(']');

            if (firstBracket != -1 && lastBracket != -1 && lastBracket > firstBracket)
            {
                return input.Substring(firstBracket, lastBracket - firstBracket + 1);
            }

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

        // Checks for exceptions that prevent retry logic (timeouts, bad keys, network issues)
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
}
