using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Localizer_App.Services
{
    public class CacheEntry
    {
        public string Translated { get; set; } = string.Empty;
        public int? ValidationScore { get; set; }
        public string? ValidationStatus { get; set; }
        public string? ValidationFeedback { get; set; }
    }

    public class TranslationMemoryService
    {
        // Why: Local caching service to read/write translations and validation status to avoid redundant API queries.
        private readonly string _tmFolder;

        public TranslationMemoryService()
        {
            // Why: Resolve cache directory inside application directory.
            _tmFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TranslationMemory");
            Directory.CreateDirectory(_tmFolder);
        }

        private string GetFilePath(string cultureCode)
        {
            // Why: Get JSON file path for target culture.
            return Path.Combine(_tmFolder, cultureCode + ".json");
        }

        public Dictionary<string, CacheEntry> LoadMemory(string cultureCode)
        {
            // Why: Load JSON dictionary for culture from file.
            string path = GetFilePath(cultureCode);
            if (!File.Exists(path))
            {
                return CreateNewMemory(path);
            }
            return TryReadMemory(path);
        }

        private Dictionary<string, CacheEntry> CreateNewMemory(string path)
        {
            // Why: Create default empty settings file.
            File.WriteAllText(path, "{}", System.Text.Encoding.UTF8);
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private Dictionary<string, CacheEntry> TryReadMemory(string path)
        {
            // Why: Parse JSON cache and handle exceptions cleanly.
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                var dict = JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json);
                if (dict != null)
                {
                    return new Dictionary<string, CacheEntry>(dict, StringComparer.OrdinalIgnoreCase);
                }
            }
            catch
            {
                // Fallback to legacy string dictionary format migration
                try
                {
                    string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var oldDict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (oldDict != null)
                    {
                        var newDict = new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in oldDict)
                        {
                            newDict[kvp.Key] = new CacheEntry { Translated = kvp.Value };
                        }
                        return newDict;
                    }
                }
                catch
                {
                    return HandleCorrupted(path);
                }
            }
            return CreateNewMemory(path);
        }

        private Dictionary<string, CacheEntry> HandleCorrupted(string path)
        {
            // Why: If JSON is corrupted, move file to a backup name and create empty settings.
            string time = DateTime.Now.ToString("yyyyMMddHHmmss");
            string backupPath = path + ".corrupted." + time + ".bak";
            if (File.Exists(path)) File.Move(path, backupPath);
            return CreateNewMemory(path);
        }

        public void SaveMemory(string cultureCode, Dictionary<string, CacheEntry> memory)
        {
            // Why: Save memory dictionary to JSON file.
            string path = GetFilePath(cultureCode);
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(memory, options);
            File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        }
    }
}
