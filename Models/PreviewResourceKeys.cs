namespace Localizer_App.Models
{
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
