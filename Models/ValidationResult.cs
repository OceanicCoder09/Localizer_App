using System.Collections.Generic;

namespace Localizer_App.Models
{
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
}
