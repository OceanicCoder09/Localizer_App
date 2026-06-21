using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Localizer_App.Models;
using Localizer_App.Services;
using ValidationResult = Localizer_App.Models.ValidationResult;

namespace Localizer_App.Views
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Why: The main desktop coordinator for extracting, translating, and saving localized strings.
        private readonly RcParserService _parserService = new RcParserService();
        private readonly TranslationService _translationService = new TranslationService();
        private readonly ValidationService _validationService = new ValidationService();
        private readonly RcGeneratorService _rcGeneratorService = new RcGeneratorService();
        private readonly TranslationMemoryService _tmService = new TranslationMemoryService();
        private readonly AiValidationService _aiValidationService = new AiValidationService();
        private string _apiKey = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<ResourceString> ResourceStrings { get; } = new();
        public ObservableCollection<TargetLanguage> TargetLanguages { get; } = new();
        public ObservableCollection<string> AvailableModels { get; } = new();

        public MainWindow()
        {
            // Why: Initialize components, setup bindings, configurations, and load API key.
            InitializeComponent();
            DataContext = this;
            InitializeProperties();
            _apiKey = LoadApiKeyFromAppData();
        }

        private void InitializeProperties()
        {
            // Why: Set initial dropdown selections and languages list.
            TargetLanguages.Add(new TargetLanguage { Name = "Hindi", CultureCode = "hi-IN" });
            TargetLanguages.Add(new TargetLanguage { Name = "Japanese", CultureCode = "ja-JP" });
            TargetLanguages.Add(new TargetLanguage { Name = "French", CultureCode = "fr-FR" });
            TargetLanguages.Add(new TargetLanguage { Name = "German", CultureCode = "de-DE" });
            SelectedLanguage = TargetLanguages.First(x => x.CultureCode == "ja-JP");
            AvailableModels.Add("gemini-2.5-flash");
            AvailableModels.Add("gemini-3.5-flash");
            AvailableModels.Add("gemini-2-flash");
            AvailableModels.Add("gemini-2-flash-lite");
            AvailableModels.Add("gemini-2.5-flash-lite");
            AvailableModels.Add("gemini-2.5-pro");
            AvailableModels.Add("gemini-3-flash");
            AvailableModels.Add("gemini-3.1-pro");
            AvailableModels.Add("gemini-3.1-flash-lite");
            SelectedModel = AvailableModels.First();
        }

        private string GetApiKeyFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string dir = Path.Combine(appData, "LocalizerApp");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "config.env");
        }

        private string LoadApiKeyFromAppData()
        {
            string filePath = GetApiKeyFilePath();
            if (!File.Exists(filePath))
            {
                string legacyKey = LoadApiKeyFromConfig();
                try
                {
                    File.WriteAllText(filePath, $"GEMINI_API_KEY={legacyKey}\n");
                }
                catch { }
                return legacyKey;
            }

            try
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("GEMINI_API_KEY=", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Substring("GEMINI_API_KEY=".Length).Trim();
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        private void OnSelectFileClick(object sender, RoutedEventArgs e)
        {
            // Why: Open file dialog for selecting a C++ Resource Script file.
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "Resource Files (*.rc)|*.rc" };
            if (openFileDialog.ShowDialog() == true)
            {
                SelectedFilePath = openFileDialog.FileName;
                ResourceStrings.Clear();
                ValidationPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnExtractStringsClick(object sender, RoutedEventArgs e)
        {
            // Why: Read, parse, and populate the resource strings grid.
            try
            {
                string content = File.ReadAllText(SelectedFilePath);
                PopulateStrings(_parserService.Parse(content));
            }
            catch (Exception ex)
            {
                ShowError("Failed to extract: " + ex.Message);
            }
        }

        private void PopulateStrings(List<ResourceString> parsed)
        {
            // Why: Clear and insert parsed resource items into the bound list.
            ResourceStrings.Clear();
            foreach (var str in parsed) ResourceStrings.Add(str);
            StatusMessage = "Extracted " + ResourceStrings.Count + " strings. Ready.";
        }

        private async void OnTranslateClick(object sender, RoutedEventArgs e)
        {
            // Why: Execute translation workflow asynchronously.
            await ExecuteTaskAsync(RunTranslationFlow);
        }

        private async Task ExecuteTaskAsync(Func<Task> taskFunc)
        {
            // Why: Async task coordinator that manages loading states and try/catch error handling.
            try
            {
                IsLoading = true;
                await taskFunc();
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            finally { IsLoading = false; }
        }

        private async Task RunTranslationFlow()
        {
            // Why: Reload API key and run string translation using cache memory lookup and API fallback.
            _apiKey = LoadApiKeyFromAppData();
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_gemini_api_key_here")
            {
                throw new Exception($"Gemini API Key is missing or invalid. Please configure GEMINI_API_KEY in config.env:\n{GetApiKeyFilePath()}");
            }

            var cache = _tmService.LoadMemory(SelectedLanguage.CultureCode);
            var misses = ResolveFromCache(cache);
            if (misses.Count > 0)
            {
                StatusMessage = "Translating " + misses.Count + " strings...";
                await _translationService.TranslateAsync(misses, SelectedLanguage.Name, SelectedLanguage.CultureCode, _apiKey, SelectedModel);
                UpdateCacheAndSave(misses, cache);
            }
            else StatusMessage = "All translations loaded from cache.";
            RefreshGrid();
        }

        private List<ResourceString> ResolveFromCache(Dictionary<string, CacheEntry> cache)
        {
            // Why: Separate translated cache hits from miss list.
            var misses = new List<ResourceString>();
            CacheHits = 0;
            foreach (var item in ResourceStrings) CheckCacheItem(item, cache, misses);
            CacheMisses = misses.Count;
            OnPropertyChanged(nameof(HitRateString));
            return misses;
        }

        private void CheckCacheItem(ResourceString item, Dictionary<string, CacheEntry> cache, List<ResourceString> misses)
        {
            // Why: Resolve single string item from translation memory or mark as miss.
            if (cache.TryGetValue(item.Text, out CacheEntry? entry))
            {
                item.Translated = entry.Translated;
                item.ValidationScore = entry.ValidationScore;
                item.ValidationStatus = entry.ValidationStatus;
                item.ValidationFeedback = entry.ValidationFeedback;
                CacheHits++;
            }
            else misses.Add(item);
        }

        private void UpdateCacheAndSave(List<ResourceString> misses, Dictionary<string, CacheEntry> cache)
        {
            // Why: Write translated outputs back to local cache dictionary and save file, excluding Needs Review.
            foreach (var item in misses)
            {
                if (!string.IsNullOrEmpty(item.Translated))
                {
                    if (item.ValidationStatus == "Needs Review" || (item.ValidationScore.HasValue && item.ValidationScore.Value < 80))
                    {
                        cache.Remove(item.Text);
                        continue;
                    }

                    if (!cache.TryGetValue(item.Text, out var entry))
                    {
                        entry = new CacheEntry();
                        cache[item.Text] = entry;
                    }
                    entry.Translated = item.Translated;
                }
            }
            _tmService.SaveMemory(SelectedLanguage.CultureCode, cache);
        }

        private void RefreshGrid()
        {
            // Why: Force grid items view refresh.
            var temp = ResourceStrings.ToList();
            ResourceStrings.Clear();
            foreach (var item in temp) ResourceStrings.Add(item);
        }

        private async void OnValidateClick(object sender, RoutedEventArgs e)
        {
            // Why: Perform AI and structural checks on translation results.
            await ExecuteTaskAsync(RunValidationFlow);
        }

        private async Task RunValidationFlow()
        {
            // Why: Reload API key and run Gemini QA check and local format tests.
            _apiKey = LoadApiKeyFromAppData();
            if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_gemini_api_key_here")
            {
                throw new Exception($"Gemini API Key is missing or invalid. Please configure GEMINI_API_KEY in config.env:\n{GetApiKeyFilePath()}");
            }

            StatusMessage = "Running validations...";

            // Clear previous validations
            foreach (var res in ResourceStrings)
            {
                res.ValidationScore = null;
                res.ValidationStatus = null;
                res.ValidationFeedback = null;
            }

            var list = ResourceStrings.Where(r => !string.IsNullOrEmpty(r.Translated)).ToList();
            var cache = _tmService.LoadMemory(SelectedLanguage.CultureCode);
            var geminiValidateList = new List<ResourceString>();

            foreach (var res in list)
            {
                if (cache.TryGetValue(res.Text, out var entry) && 
                    res.Translated == entry.Translated && 
                    entry.ValidationScore.HasValue && 
                    !string.IsNullOrEmpty(entry.ValidationStatus))
                {
                    res.ValidationScore = entry.ValidationScore;
                    res.ValidationStatus = entry.ValidationStatus;
                    res.ValidationFeedback = entry.ValidationFeedback;
                }
                else
                {
                    geminiValidateList.Add(res);
                }
            }

            if (geminiValidateList.Count > 0)
            {
                var qaResults = await _aiValidationService.ValidateAsync(geminiValidateList, SelectedLanguage.Name, SelectedLanguage.CultureCode, _apiKey, SelectedModel);
                MapQaResultsOnly(qaResults);

                // Update cache with validation results
                foreach (var res in geminiValidateList)
                {
                    if (res.ValidationStatus == "Needs Review" || (res.ValidationScore.HasValue && res.ValidationScore.Value < 80))
                    {
                        cache.Remove(res.Text);
                    }
                    else
                    {
                        if (!cache.TryGetValue(res.Text, out var entry))
                        {
                            entry = new CacheEntry();
                            cache[res.Text] = entry;
                        }
                        entry.Translated = res.Translated;
                        entry.ValidationScore = res.ValidationScore;
                        entry.ValidationStatus = res.ValidationStatus;
                        entry.ValidationFeedback = res.ValidationFeedback;
                    }
                }
            }

            // Double-check sweep: remove any Needs Review entries from TM cache
            foreach (var res in ResourceStrings)
            {
                if (res.ValidationStatus == "Needs Review" || (res.ValidationScore.HasValue && res.ValidationScore.Value < 80))
                {
                    cache.Remove(res.Text);
                }
            }

            _tmService.SaveMemory(SelectedLanguage.CultureCode, cache);

            RunLocalValidation();
            RecalculateAndSaveStats();
            ShowValidationPanel();
        }

        public async void OnRetryTranslationClick(object sender, RoutedEventArgs e)
        {
            // Why: Click handler for the row-level Retry button, triggered for Needs Review strings.
            if (sender is Button button && button.DataContext is ResourceString resourceString)
            {
                await RetryTranslationAndValidationAsync(resourceString);
            }
        }

        private async Task RetryTranslationAndValidationAsync(ResourceString item)
        {
            // Why: Retranslates and re-validates a specific low-scoring / Needs Review translation.
            try
            {
                IsLoading = true;
                StatusMessage = $"Retrying translation for key: {item.Key}...";
                
                // Clear previous validation and translation states to show progress
                item.Translated = string.Empty;
                item.ValidationScore = null;
                item.ValidationStatus = null;
                item.ValidationFeedback = null;

                // 1. Call translation service for this single item
                var singleList = new List<ResourceString> { item };
                _apiKey = LoadApiKeyFromAppData();
                if (string.IsNullOrEmpty(_apiKey) || _apiKey == "your_gemini_api_key_here")
                {
                    throw new Exception($"Gemini API Key is missing or invalid. Please configure GEMINI_API_KEY in config.env:\n{GetApiKeyFilePath()}");
                }

                await _translationService.TranslateAsync(singleList, SelectedLanguage.Name, SelectedLanguage.CultureCode, _apiKey, SelectedModel);

                if (string.IsNullOrEmpty(item.Translated))
                {
                    throw new Exception("Translation service returned empty result.");
                }

                // 2. Call validation service for this single item
                StatusMessage = $"Validating retried translation for key: {item.Key}...";
                var qaResults = await _aiValidationService.ValidateAsync(singleList, SelectedLanguage.Name, SelectedLanguage.CultureCode, _apiKey, SelectedModel);
                
                // Map the QA results to the item
                var match = qaResults.FirstOrDefault(q => q.Key == item.Key);
                if (match != null)
                {
                    item.ValidationScore = match.Score;
                    item.ValidationStatus = match.Status;
                    item.ValidationFeedback = match.Feedback;
                }

                // 3. Perform local validation on the updated list
                RunLocalValidation();

                // 4. Update Translation Memory Cache
                var cache = _tmService.LoadMemory(SelectedLanguage.CultureCode);
                if (item.ValidationStatus == "Needs Review" || (item.ValidationScore.HasValue && item.ValidationScore.Value < 80))
                {
                    cache.Remove(item.Text);
                    StatusMessage = $"Translation updated for {item.Key}, but still needs review. Not saved to cache.";
                }
                else
                {
                    if (!cache.TryGetValue(item.Text, out var entry))
                    {
                        entry = new CacheEntry();
                        cache[item.Text] = entry;
                    }
                    entry.Translated = item.Translated;
                    entry.ValidationScore = item.ValidationScore;
                    entry.ValidationStatus = item.ValidationStatus;
                    entry.ValidationFeedback = item.ValidationFeedback;
                    StatusMessage = $"Successfully retried, validated, and saved translation for key: {item.Key}";
                }
                _tmService.SaveMemory(SelectedLanguage.CultureCode, cache);

                // 5. Recalculate stats
                RecalculateAndSaveStats();
            }
            catch (Exception ex)
            {
                ShowError($"Retry failed: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                RefreshGrid();
            }
        }

        private void MapQaResultsOnly(List<ValidationOutputItem> qaResults)
        {
            // Why: Map Gemini QA results back to resource strings.
            foreach (var res in ResourceStrings)
            {
                var match = qaResults.FirstOrDefault(q => q.Key == res.Key);
                if (match != null)
                {
                    res.ValidationScore = match.Score;
                    res.ValidationStatus = match.Status;
                    res.ValidationFeedback = match.Feedback;
                }
            }
        }

        private void RecalculateAndSaveStats()
        {
            // Why: Recalculate validation statistics based on the final state of all strings.
            var stats = new ValStats();
            int count = 0;
            foreach (var res in ResourceStrings)
            {
                // We only count strings that have a validation score/status (i.e. those that are translated or validated)
                if (res.ValidationScore.HasValue)
                {
                    count++;
                    stats.TotalScore += res.ValidationScore.Value;
                    if (res.ValidationScore.Value >= 90) stats.Excellent++;
                    else if (res.ValidationScore.Value >= 80) stats.Good++;
                    else stats.NeedsReview++;
                }
            }

            AiValTotalStrings = count;
            AiValAverageScore = count > 0 ? (double)stats.TotalScore / count : 0;
            AiValExcellentCount = stats.Excellent;
            AiValGoodCount = stats.Good;
            AiValNeedsReviewCount = stats.NeedsReview;
            IsAiValidationRun = true;
            RefreshGrid();
        }

        private void RunLocalValidation()
        {
            // Why: Perform local checks like empty strings and structural formatting.
            var list = ResourceStrings.ToList();
            ValidationReport = _validationService.Validate(list, list);
        }

        private void ShowValidationPanel()
        {
            // Why: Set the validation results column visibility to visible.
            ValidationPanel.Visibility = Visibility.Visible;
            StatusMessage = "Validation complete.";
        }

        private void OnGenerateRcClick(object sender, RoutedEventArgs e)
        {
            // Why: Generate translated RC file in memory.
            try
            {
                string original = File.ReadAllText(SelectedFilePath);
                GeneratedRcContent = _rcGeneratorService.Generate(original, ResourceStrings.ToList());
                StatusMessage = "Localized resource generated in memory.";
            }
            catch (Exception ex)
            {
                ShowError("Generation failed: " + ex.Message);
            }
        }

        private void OnSaveRcClick(object sender, RoutedEventArgs e)
        {
            // Why: Choose output file location and write generated RC script.
            SaveFileDialog dialog = new SaveFileDialog { Filter = "Resource Files (*.rc)|*.rc", FileName = SelectedLanguage.CultureCode + ".rc" };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, GeneratedRcContent, Encoding.UTF8);
                StatusMessage = "Saved file successfully to: " + dialog.FileName;
            }
        }

        private void OnOpenPreviewClick(object sender, RoutedEventArgs e)
        {
            // Why: Launch the design preview studio window, passing in-memory translated strings.
            var preview = new PreviewWindow(ResourceStrings.ToList(), SelectedLanguage.CultureCode);
            preview.ShowDialog();
        }

        private string LoadApiKeyFromConfig()
        {
            // Why: Read the Gemini API Key from App.config securely.
            string configPath = AppDomain.CurrentDomain.BaseDirectory + "App.config";
            if (!File.Exists(configPath)) return string.Empty;
            var document = System.Xml.Linq.XDocument.Load(configPath);
            var element = document.Descendants("add").FirstOrDefault(e => (string?)e.Attribute("key") == "GeminiApiKey");
            return element != null ? (string?)element.Attribute("value") ?? string.Empty : string.Empty;
        }

        private void SaveApiKeyToConfig(string key)
        {
            // Why: Save the updated Gemini API Key back to App.config securely.
            string configPath = AppDomain.CurrentDomain.BaseDirectory + "App.config";
            if (!File.Exists(configPath)) return;
            var document = System.Xml.Linq.XDocument.Load(configPath);
            var element = document.Descendants("add").FirstOrDefault(e => (string?)e.Attribute("key") == "GeminiApiKey");
            if (element == null) return;
            element.SetAttributeValue("value", key);
            document.Save(configPath);
        }

        private void ShowError(string message)
        {
            // Why: Show error in MessageBox and set status text.
            StatusMessage = "Error: " + message;
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected void OnPropertyChanged(string name)
        {
            // Why: Trigger bound properties updates in XAML.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        protected void SetProperty<T>(ref T storage, T value, string name)
        {
            // Why: Standard property setter and notifier logic.
            if (EqualityComparer<T>.Default.Equals(storage, value)) return;
            storage = value;
            OnPropertyChanged(name);
        }

        // Properties bound in XAML
        private string _selectedFilePath = string.Empty;
        public string SelectedFilePath
        {
            get => _selectedFilePath;
            set { SetProperty(ref _selectedFilePath, value, nameof(SelectedFilePath)); OnPropertyChanged(nameof(IsFileSelected)); }
        }
        public bool IsFileSelected => !string.IsNullOrEmpty(SelectedFilePath);

        private TargetLanguage _selectedLanguage = new();
        public TargetLanguage SelectedLanguage
        {
            get => _selectedLanguage;
            set => SetProperty(ref _selectedLanguage, value, nameof(SelectedLanguage));
        }

        private string _selectedModel = string.Empty;
        public string SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value, nameof(SelectedModel));
        }

        private string _statusMessage = "Ready.";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { SetProperty(ref _isLoading, value, nameof(IsLoading)); OnPropertyChanged(nameof(IsNotLoading)); }
        }
        public bool IsNotLoading => !IsLoading;

        private int _cacheHits;
        public int CacheHits
        {
            get => _cacheHits;
            set => SetProperty(ref _cacheHits, value, nameof(CacheHits));
        }

        private int _cacheMisses;
        public int CacheMisses
        {
            get => _cacheMisses;
            set => SetProperty(ref _cacheMisses, value, nameof(CacheMisses));
        }

        public string HitRateString
        {
            get
            {
                int total = CacheHits + CacheMisses;
                return total > 0 ? ((double)CacheHits / total * 100).ToString("F1") + "%" : "0.0%";
            }
        }

        private string _generatedRcContent = string.Empty;
        public string GeneratedRcContent
        {
            get => _generatedRcContent;
            set { SetProperty(ref _generatedRcContent, value, nameof(GeneratedRcContent)); OnPropertyChanged(nameof(IsRcGenerated)); }
        }
        public bool IsRcGenerated => !string.IsNullOrEmpty(GeneratedRcContent);

        private bool _isAiValidationRun;
        public bool IsAiValidationRun
        {
            get => _isAiValidationRun;
            set => SetProperty(ref _isAiValidationRun, value, nameof(IsAiValidationRun));
        }

        private int _aiValTotalStrings;
        public int AiValTotalStrings
        {
            get => _aiValTotalStrings;
            set => SetProperty(ref _aiValTotalStrings, value, nameof(AiValTotalStrings));
        }

        private double _aiValAverageScore;
        public double AiValAverageScore
        {
            get => _aiValAverageScore;
            set { SetProperty(ref _aiValAverageScore, value, nameof(AiValAverageScore)); OnPropertyChanged(nameof(AiValOverallScoreString)); }
        }
        public string AiValOverallScoreString => Math.Round(AiValAverageScore).ToString() + "%";

        private int _aiValExcellentCount;
        public int AiValExcellentCount
        {
            get => _aiValExcellentCount;
            set => SetProperty(ref _aiValExcellentCount, value, nameof(AiValExcellentCount));
        }

        private int _aiValGoodCount;
        public int AiValGoodCount
        {
            get => _aiValGoodCount;
            set => SetProperty(ref _aiValGoodCount, value, nameof(AiValGoodCount));
        }

        private int _aiValNeedsReviewCount;
        public int AiValNeedsReviewCount
        {
            get => _aiValNeedsReviewCount;
            set => SetProperty(ref _aiValNeedsReviewCount, value, nameof(AiValNeedsReviewCount));
        }

        private ValidationResult? _validationReport;
        public ValidationResult? ValidationReport
        {
            get => _validationReport;
            set => SetProperty(ref _validationReport, value, nameof(ValidationReport));
        }
    }
}
