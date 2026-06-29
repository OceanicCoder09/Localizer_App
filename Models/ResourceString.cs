namespace Localizer_App.Models
{
    public class ResourceString
    {
        public string Key { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string Translated { get; set; } = string.Empty;
        
        // Character start index of the string literal in the file (including opening quote)
        public int StartIndex { get; set; }
        
        // Character end index of the string literal in the file (including closing quote)
        public int EndIndex { get; set; }

        // AI Validation Results
        public int? ValidationScore { get; set; }
        public string? ValidationStatus { get; set; }
        public string? ValidationFeedback { get; set; }
    }
}
