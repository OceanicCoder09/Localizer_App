using System;
using System.Collections.Generic;

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

    public class TargetLanguage
    {
        public string Name { get; set; } = string.Empty;
        public string CultureCode { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    public class ValidationResult
    {
        public int TotalStrings { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public bool CountValidationPassed { get; set; }
        public bool EmptyValidationPassed { get; set; }
        public bool PlaceholderValidationPassed { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    internal class ValStats
    {
        // Why: Helper class to hold temporary validation statistics for calculations.
        public double TotalScore { get; set; }
        public int Excellent { get; set; }
        public int Good { get; set; }
        public int NeedsReview { get; set; }
    }

    /// <summary>
    /// Resource key constants used by the localization preview UI.
    /// </summary>
    public static class PreviewResourceKeys
    {
        public const string File = "IDS_FILE";
        public const string Edit = "IDS_EDIT";
        public const string View = "IDS_VIEW";
        public const string Help = "IDS_HELP";

        public const string NewProject = "IDS_NEW_PROJECT";
        public const string Open = "IDS_OPEN";
        public const string Save = "IDS_SAVE";
        public const string Export = "IDS_EXPORT";

        public const string Properties = "IDS_PROPERTIES";
        public const string Name = "IDS_NAME";
        public const string Description = "IDS_DESCRIPTION";
        public const string Category = "IDS_CATEGORY";
        public const string Status = "IDS_STATUS";

        public const string Apply = "IDS_APPLY";
        public const string Cancel = "IDS_CANCEL";

        public const string Ready = "IDS_READY";

        public static readonly string[] All =
        [
            File, Edit, View, Help,
            NewProject, Open, Save, Export,
            Properties, Name, Description, Category, Status,
            Apply, Cancel, Ready
        ];
    }
}
