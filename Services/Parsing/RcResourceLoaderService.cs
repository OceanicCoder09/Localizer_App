using System;
using System.Collections.Generic;
using System.IO;

namespace Localizer_App.Services
{
    // =========================================================================
    // 3. Resource Loader Service (Preview Helper)
    // =========================================================================
    public class RcResourceLoaderService
    {
        private readonly RcParserService _parser = new RcParserService();

        public Dictionary<string, string> LoadFromFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            string content = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            return LoadFromContent(content);
        }

        public Dictionary<string, string> LoadFromContent(string content)
        {
            var parsed = _parser.Parse(content);
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in parsed)
            {
                if (!string.IsNullOrEmpty(item.Key))
                {
                    result[item.Key] = item.Text;
                }
            }
            return result;
        }

        public static string GetDefaultResourcesDirectory()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
        }

        public static string GetResourceFilePath(string resourcesDirectory, string cultureCode)
        {
            return Path.Combine(resourcesDirectory, cultureCode + ".rc");
        }
    }
}
