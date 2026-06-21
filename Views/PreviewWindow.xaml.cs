using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Localizer_App.Models;
using Localizer_App.Services;

namespace Localizer_App.Views
{
    public partial class PreviewWindow : Window, INotifyPropertyChanged
    {
        // Why: Window to preview visual localization using in-memory and cached resources.
        private readonly List<ResourceString> _inMemoryStrings;
        private readonly string _currentCultureCode;
        private readonly RcResourceLoaderService _loader = new RcResourceLoaderService();
        private Dictionary<string, string> _resources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public event PropertyChangedEventHandler? PropertyChanged;

        public PreviewWindow(List<ResourceString> inMemoryStrings, string currentCultureCode)
        {
            // Why: Initialize components, set data context, load languages, and set default selection.
            InitializeComponent();
            _inMemoryStrings = inMemoryStrings;
            _currentCultureCode = currentCultureCode;
            DataContext = this;
            LoadLanguages();
            
            // Set the default selected language in the combo box
            var selectedLang = (LanguageCombo.ItemsSource as List<TargetLanguage>)?
                .FirstOrDefault(x => x.CultureCode.Equals(currentCultureCode, StringComparison.OrdinalIgnoreCase));
            if (selectedLang != null)
            {
                LanguageCombo.SelectedItem = selectedLang;
            }
            else
            {
                LanguageCombo.SelectedIndex = 0;
            }
        }

        private void LoadLanguages()
        {
            // Why: Populate available languages dynamically based on existing localized files and memory state.
            var list = new List<TargetLanguage>
            {
                new TargetLanguage { Name = "English", CultureCode = "en-US" }
            };

            string resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            if (Directory.Exists(resourcesDir))
            {
                var files = Directory.GetFiles(resourcesDir, "*.rc");
                foreach (var file in files)
                {
                    string cultureCode = Path.GetFileNameWithoutExtension(file);
                    if (cultureCode.Equals("en-US", StringComparison.OrdinalIgnoreCase)) continue;
                    if (cultureCode.Equals("ui_strings", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        var cultureInfo = new System.Globalization.CultureInfo(cultureCode);
                        string name = cultureInfo.EnglishName;
                        int parenIndex = name.IndexOf('(');
                        if (parenIndex > 0)
                        {
                            name = name.Substring(0, parenIndex).Trim();
                        }
                        list.Add(new TargetLanguage { Name = name, CultureCode = cultureCode });
                    }
                    catch
                    {
                        list.Add(new TargetLanguage { Name = cultureCode, CultureCode = cultureCode });
                    }
                }
            }

            // Bind to ComboBox (grouping to avoid duplicates)
            LanguageCombo.ItemsSource = list.GroupBy(x => x.CultureCode, StringComparer.OrdinalIgnoreCase)
                                            .Select(g => g.First())
                                            .ToList();
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Why: Trigger reload of localized strings when user selects a different language.
            var selected = LanguageCombo.SelectedItem as TargetLanguage;
            if (selected != null)
            {
                LoadResources(selected.CultureCode);
            }
        }

        private void OnReloadClick(object sender, RoutedEventArgs e)
        {
            // Why: Reload the resources from the current language file.
            var selected = LanguageCombo.SelectedItem as TargetLanguage;
            if (selected != null) LoadResources(selected.CultureCode);
        }

        private void LoadResources(string cultureCode)
        {
            _resources.Clear();

            // 1. Try to load from the corresponding .rc file in Resources/
            string resourcesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
            string file = Path.Combine(resourcesDir, cultureCode + ".rc");
            if (File.Exists(file))
            {
                _resources = _loader.LoadFromFile(file);
            }
            else
            {
                // Fallback to Translation Memory JSON if available
                var tmService = new TranslationMemoryService();
                var cache = tmService.LoadMemory(cultureCode);
                foreach (var item in _inMemoryStrings)
                {
                    if (cache.TryGetValue(item.Key, out var entry) && !string.IsNullOrEmpty(entry.Translated))
                    {
                        _resources[item.Key] = entry.Translated;
                    }
                }
            }

            // 2. If it is the current active culture code, overlay/update with in-memory translated strings
            if (cultureCode.Equals(_currentCultureCode, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in _inMemoryStrings)
                {
                    if (!string.IsNullOrEmpty(item.Translated))
                    {
                        _resources[item.Key] = item.Translated;
                    }
                    else if (!_resources.ContainsKey(item.Key))
                    {
                        _resources[item.Key] = item.Text;
                    }
                }
            }
            // For English, if we didn't load en-US.rc or if keys are missing, we can overlay with original in-memory text
            else if (cultureCode.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in _inMemoryStrings)
                {
                    if (!_resources.ContainsKey(item.Key))
                    {
                        _resources[item.Key] = item.Text;
                    }
                }
            }

            StatusMessage = "Loaded: " + cultureCode + " (" + _resources.Count + " strings)";
            NotifyAllProperties();
        }

        private string GetText(string key, string fallback)
        {
            // Why: Safely read resource values and return default label if not present.
            return _resources.TryGetValue(key, out string? value) ? value : fallback;
        }

        private void OnPropertyChanged(string name)
        {
            // Why: Notify binding engines that a property changed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void NotifyAllProperties()
        {
            // Why: Refresh all UI string bindings using a list iteration.
            string[] names = { 
                nameof(SupplyText), nameof(UpheavalText), nameof(VeryText), 
                nameof(SadlyText), nameof(NowText), nameof(AutoText),
                nameof(NewProjectTool), nameof(OpenTool), nameof(SaveTool), nameof(ExportTool),
                nameof(PropertiesTitle), nameof(NameLabel), nameof(DescriptionLabel), 
                nameof(CategoryLabel), nameof(StatusLabel), nameof(SaveButton), 
                nameof(CancelButton), nameof(ApplyButton), nameof(ReadyStatus) 
            };
            foreach (string name in names) OnPropertyChanged(name);
        }

        // Properties bound in XAML
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(nameof(StatusMessage)); }
        }

        public string SupplyText => GetText("IDS_SUPPLY", string.Empty);
        public string UpheavalText => GetText("IDS_UPHEAVAL", string.Empty);
        public string VeryText => GetText("IDS_VERY", string.Empty);
        public string SadlyText => GetText("IDS_SADLY", string.Empty);
        public string NowText => GetText("IDS_NOW", string.Empty);
        public string AutoText => GetText("IDS_AUTO", string.Empty);

        public string NewProjectTool => GetText("IDS_NEW_PROJECT", string.Empty);
        public string OpenTool => GetText("IDS_OPEN", string.Empty);
        public string SaveTool => GetText("IDS_SAVE", string.Empty);
        public string ExportTool => GetText("IDS_EXPORT", string.Empty);

        public string PropertiesTitle => GetText("IDS_PROPERTIES", string.Empty);
        public string NameLabel => GetText("IDS_NAME", string.Empty);
        public string DescriptionLabel => GetText("IDS_DESCRIPTION", string.Empty);
        public string CategoryLabel => GetText("IDS_CATEGORY", string.Empty);
        public string StatusLabel => GetText("IDS_STATUS", string.Empty);
        public string SaveButton => GetText("IDS_SAVE", string.Empty);
        public string CancelButton => GetText("IDS_CANCEL", string.Empty);
        public string ApplyButton => GetText("IDS_APPLY", string.Empty);
        public string ReadyStatus => GetText("IDS_READY", string.Empty);
    }
}
