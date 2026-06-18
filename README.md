# RcLocalizer - C++ Resource (.rc) File Localizer

RcLocalizer is a lightweight desktop Proof of Concept (PoC) application built with **C# .NET 10 (compatible with .NET 8)** and **WPF**. It is designed to extract, translate, validate, and rebuild Windows C++ Resource Script (`.rc`) files using the **Google Gemini API**. 

The application has been simplified into an event-driven **Code-Behind model** (under 200 lines per file and 10 lines per method), making it extremely readable and easy to present to CCTech and Autodesk leadership.

---

## Key Features

1. **Secure API Key Storage**: Stores the Gemini API key in [App.config](file:///d:/AutoDesk_POC/RcLocalizer/App.config). The UI features a password field with a toggle reveal button (👁️) to show or hide the key.
2. **Lexical RC Tokenizer & Parser**: Extracts translatable strings strictly from C++ `STRINGTABLE` blocks while ignoring macros, preprocessor directives, and comments. It tracks exact coordinate offsets to prevent file corruption.
3. **Local & AI Validation (Side-by-Side Panel)**: 
   - **Local Validation**: Counts formatting placeholders (like `%s`, `%d`, `{0}`) to prevent runtime application crashes.
   - **AI QA Validation**: Sends translations to Gemini to evaluate translation accuracy, natural grammar, and UI suitability.
   - **Visual UI**: The validation results appear in an expanding panel on the right side, keeping the main strings list always visible.
4. **Translation Memory (Cache)**: Caches translation key-value pairs in local JSON files to bypass redundant Gemini API calls, optimizing response speed and cost.
5. **Back-to-Front Generator**: Replaces target strings in-place starting from the end of the file, keeping coordinates of preceding text perfectly intact.
6. **Visual Preview (Design Studio)**: A simulated CAD design dialog that dynamically reloads the localized `.rc` file, displaying localized menu bars, button layouts, and properties live.

---

## Directory Structure

```
/RcLocalizer
│   App.xaml
│   App.xaml.cs
│   App.config                    <-- Secure app configuration settings
│   AssemblyInfo.cs
│   Localizer_App.csproj          <-- Project build settings
│   LocalizerApp.slnx             <-- Solution file
│   sample.rc                     <-- Sample English script file for testing
│   sample_de-DE.rc               <-- Sample German localized script file for testing
│   README.md                     <-- You are here
│   RcLocalizer_Guide.md          <-- Presentation & developers guide
│   RcLocalizer_Concepts.md       <-- Detailed technical concepts index
│
├───Models
│       PreviewResourceKeys.cs     <-- Constant string keys used by preview UI controls
│       ResourceString.cs          <-- Data model for keys and text coordinates
│       TargetLanguage.cs          <-- Language names and culture code metadata
│       ValidationResult.cs        <-- Local validation summary and error reports
│       ValStats.cs                <-- Counters used during AI QA scoring calculations
│
├───Services
│       RcTokenizer.cs             <-- Lexical scanner converting text to tokens
│       RcParserService.cs         <-- Extractor parsing resource blocks
│       RcGeneratorService.cs      <-- Rebuilds translated resource script
│       TranslationService.cs      <-- Translates lists using Gemini Service
│       AiValidationService.cs     <-- Evaluates language quality using Gemini
│       ValidationService.cs       <-- Local formatting placeholder counts
│       TranslationMemoryService.cs <-- Local JSON translation cache
│       RcResourceLoaderService.cs <-- Loads target script strings for preview
│       GeminiService.cs           <-- Unified network REST client
│
└───Views
        MainWindow.xaml            <-- UI layout with side-by-side panels
        MainWindow.xaml.cs         <-- Code-behind event handlers & properties
        PreviewWindow.xaml         <-- Simulation preview window
        PreviewWindow.xaml.cs      <-- Logic to load preview dropdown values
```

---

## Setup & Running Instructions

### Prerequisites
- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (or compatible .NET 8.0 SDK) installed on your machine.
- A **Gemini API Key** (obtainable from [Google AI Studio](https://aistudio.google.com/)).

### Running the App
1. Open a command prompt or terminal in the project directory.
2. Build and run the project:
   ```bash
   dotnet build Localizer_App.csproj
   dotnet run --project Localizer_App.csproj
   ```

---

## Walkthrough: Testing with `sample.rc`

A test file called `sample.rc` has been pre-packaged in the root folder of the project. Follow this flow to test:

1. **Launch the Application**: Start the app using `dotnet run --project Localizer_App.csproj`.
2. **Select the File**: Click **[Select RC File...]** and choose `sample.rc` in the project root.
3. **Select Language**: Select **Japanese**, **Hindi**, **French**, or **German** from the dropdown menu.
4. **Extract Strings**: Click **[Extract Strings]**. The grid will populate with 9 extracted strings, displaying keys and original English text.
5. **Set API Key**: Enter your Gemini API Key in the top-right field. Toggle the reveal button (👁️) to verify characters. The key is automatically saved to [App.config](file:///d:/AutoDesk_POC/RcLocalizer/App.config).
6. **Translate**: Click **[Translate ]**. Misses are translated in an optimized batch, and translation memory hit rates are updated.
7. **Validate**: Click **[Validate]**. The **Validation Results Panel** will appear on the right side:
   - Green checks (✔) confirm that placeholder counts match and translation strings are not empty.
   - The AI quality panel shows the overall QA score and rating counts.
8. **Visual Preview**: Click **[Open Preview Window]**. Select your target language in the dropdown to see menu bars and properties panels formatted live.
9. **Generate and Save**:
   - Click **[Generate Localized RC]** to merge translations in memory.
   - Click **[Save Localized RC...]** to save the resulting script file (e.g. `ja-JP.rc`).

---

## Technical Resources
For more details, check out the generated documentation in the project root:
- [Presentation & Guide Document (RcLocalizer_Guide.md)](file:///d:/AutoDesk_POC/RcLocalizer/RcLocalizer_Guide.md): Detailed step-by-step developer architecture, button triggers, and execution flow.
- [Technical Concepts Index (RcLocalizer_Concepts.md)](file:///d:/AutoDesk_POC/RcLocalizer/RcLocalizer_Concepts.md): Explanation of C# and WPF concepts (e.g. tokenization, async tasks, observable collections, reverse index traversals) with code examples.
