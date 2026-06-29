namespace Localizer_App.Models
{
    public class TargetLanguage
    {
        public string Name { get; set; } = string.Empty;
        public string CultureCode { get; set; } = string.Empty;

        public override string ToString() => Name;
    }
}
