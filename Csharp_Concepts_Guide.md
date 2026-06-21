# C# Technical Concepts & App Features Directory: RcLocalizer

This document details the C# concepts used across the **RcLocalizer** codebase, mapping them to the specific application features they power, where they are used, and how they make each feature possible.

---

## Executive Summary: How C# Powers RcLocalizer Features

| Feature | Key C# Concepts Used | Location (Files) | How it Makes it Possible |
| :--- | :--- | :--- | :--- |
| **Secure API Key Management** | XML DOM Parsing (`XDocument`), File System I/O | [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) | Reads standard configurations and migrates legacy credentials securely to local user environment directories (`%APPDATA%`). |
| **Resource Script Parsing** | Enums, State Machines, Lexical Analysis (Custom Lexer) | [RcParserService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/RcParserService.cs) | Scans raw C++ `.rc` character streams, categorizing tokens to safely extract string values from complex nested structures while ignoring preprocessor directives and comments. |
| **AI-Assisted Translation** | Asynchronous Concurrency (`async`/`await`), HTTP Communication (`HttpClient`), JSON Mode | [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) | Performs non-blocking remote REST calls in batch structures to Gemini. Keeps the desktop UI responsive while retrieving language translations. |
| **Hybrid Double-Layer QA** | Pass-by-Reference (`ref`), Heuristic Checking, String Indexing | [ValidationService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/ValidationService.cs), [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) | Runs instant local verification checks for format placeholders (`%s`, `%d`) and empty fields before querying AI validation metrics. |
| **Translation Memory & Cache** | Dictionaries (Case-Insensitive), JSON Serialization / Deserialization | [TranslationMemoryService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/TranslationMemoryService.cs) | Maps keys to cached translations, loading results in sub-milliseconds to eliminate repeated billing charges for duplicate resources. |
| **Interactive Grid Editor & Live Preview** | WPF Data Binding, `INotifyPropertyChanged`, `ObservableCollection<T>` | [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs), [PreviewWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/PreviewWindow.xaml.cs) | Connects memory models to interactive screen elements, reflecting manual string overrides and live CAD preview overlays in real time. |
| **Safe Resource File Rebuilder** | LINQ, Reverse-Index Sort (Descending), String Builders | [RcGeneratorService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/RcGeneratorService.cs) | Sorts string coordinates from end-to-start (bottom-to-top), replacing text in-place without altering preceding character coordinates. |

---

## Detailed Directory of C# Concepts

### 1. Asynchronous Concurrency (`async`, `await`, `Task`)
* **Where Used**: 
  * [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) (e.g., `ExecuteTaskAsync`, `OnTranslateClick`, `OnValidateClick`)
  * [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) (e.g., `CallApiAsync`, `TranslateAsync`, `ValidateAsync`)
* **How it Makes it Possible**:
  Web requests to remote LLMs can take several seconds depending on payload size and network latency. If run synchronously on the Main/UI thread, the application UI would freeze, turning white and displaying "Not Responding" to the user. 
  By declaring methods with `async` and using `await` on network tasks (like `Client.PostAsync`), the framework relinquishes the thread to process OS window messages, keeping the desktop interface fully active. Once the task finishes, execution resumes on the original thread context.

---

### 2. HTTP REST Communication (`HttpClient`)
* **Where Used**:
  * [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) (inside the `GeminiService` class)
* **How it Makes it Possible**:
  Provides a reusable, high-performance connection pool (`private static readonly HttpClient Client`) to establish socket links to Google Generative Language endpoints. It manages TLS handshake layers, payload encoding (`StringContent`), and standard error diagnostics (`response.IsSuccessStatusCode`).

---

### 3. XML Document DOM Manipulation (`XDocument` / LINQ to XML)
* **Where Used**:
  * [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) (e.g., `LoadApiKeyFromConfig`, `SaveApiKeyToConfig`)
* **How it Makes it Possible**:
  Allows the application to parse, search, and update standard Windows XML config formats (`App.config`) without requiring external package dependencies. Using `XDocument.Load` and LINQ syntax like `.Descendants("add").FirstOrDefault(...)`, the app reads configuration tags dynamically and updates them on disk when a user saves a key.

---

### 4. JSON Serialization & Deserialization (`System.Text.Json.JsonSerializer`)
* **Where Used**:
  * [TranslationMemoryService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/TranslationMemoryService.cs) (e.g., `LoadMemory`, `SaveMemory`)
  * [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) (e.g., `BuildRequestJson`, `UpdateTranslations`, `ParseResults`)
* **How it Makes it Possible**:
  Facilitates data parsing between C# object lists (`List<ResourceString>`) and text formats.
  * **During Translation/Validation**: Serializes C# lists into JSON payloads for the Gemini API, and deserializes JSON arrays returned by the API back into strongly-typed C# objects.
  * **During Cache Storage**: Converts translation dictionaries to readable, indented JSON files on disk and parses them back on startup.

---

### 5. LINQ (Language Integrated Query)
* **Where Used**:
  * [RcGeneratorService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/RcGeneratorService.cs) (inside `SortReplacements`)
  * [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) (inside `TranslateAsync` batching using `.Skip().Take()`)
* **How it Makes it Possible**:
  Allows developers to write SQL-like declarative queries directly against collections. 
  * **Sorting**: `.OrderByDescending(r => r.StartIndex)` orders strings from back-to-front by their character positions.
  * **Filtering**: `.Where(r => r.StartIndex >= 0)` filters out invalid indexes.
  * **Batching**: Combining `.Skip(i).Take(BatchSize)` partitions long lists of keys into clean batch segments, minimizing LLM request overhead.

---

### 6. Case-Insensitive Dictionary Collections
* **Where Used**:
  * [TranslationMemoryService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/TranslationMemoryService.cs) (inside `TryReadMemory` and `CreateNewMemory`)
  * [ValidationService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/ValidationService.cs) (inside `CheckTranslations` mapper)
* **How it Makes it Possible**:
  By default, C# dictionaries match keys using exact binary case matches (`IDS_FILE` $\neq$ `ids_file`). Setting up dictionaries with `StringComparer.OrdinalIgnoreCase` forces the hashing engine to compare string entries case-insensitively. This ensures cached translations are successfully resolved regardless of key capitalization differences.

---

### 7. WPF Data Binding & `INotifyPropertyChanged`
* **Where Used**:
  * [MainWindow.xaml](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml)
  * [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) (implementation of `INotifyPropertyChanged`)
* **How it Makes it Possible**:
  Maintains clean separation of concerns by letting UI controls (like `TextBlock` or `DataGrid`) "bind" directly to C# variables. 
  When the application updates backing fields (e.g., `_statusMessage = "Translating..."`), calling the `PropertyChanged` event notifies the WPF window framework to fetch the updated value and redraw the visual elements on screen.

---

### 8. `ObservableCollection<T>`
* **Where Used**:
  * [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) (the `ResourceStrings` property)
* **How it Makes it Possible**:
  A regular `List<T>` does not notify the UI when elements are added or cleared. `ObservableCollection<T>` implements the `INotifyCollectionChanged` interface. Whenever strings are extracted or loaded, items are added to this collection, which automatically notifies the bound WPF `DataGrid` to insert, update, or delete matching table rows dynamically.

---

### 9. Pass-by-Reference (`ref` Parameter Modifier)
* **Where Used**:
  * [ValidationService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/ValidationService.cs) (inside `CheckList` and `CheckItem`)
  * [MainWindow.xaml.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Views/MainWindow.xaml.cs) (inside `SetProperty`)
* **How it Makes it Possible**:
  * **In Verification**: Standard value types (like `bool`) are passed by copy. Changing them inside nested loops has no effect outside. Passing flags like `ref bool allNotEmpty` allows deep nested validation routines to set the overall pass/fail status variables of a parent function directly.
  * **In Binding Helpers**: Passing backing variables as references (`ref T storage`) in code-behind allows a single template helper `SetProperty<T>` to modify backing values directly on the stack while automatically firing property change signals.

---

### 10. Reverse-Index Traversal (Bottom-to-Top replacement)
* **Where Used**:
  * [RcGeneratorService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/RcGeneratorService.cs) (inside `Generate` and `ReplaceAllStrings`)
* **How it Makes it Possible**:
  When editing a raw script file, replacing a 5-character word (`"File"`) with a 10-character translation (`"ファイル"`) increases the file length by 5 characters. This shifts all subsequent character offsets forward, invalidating all pre-parsed string coordinates.
  By sorting replacements descending by their file offset (`StartIndex`), the generator processes replacements from the **end** of the script to the **beginning**. Shifts in length only affect sections of the file that have *already* been updated, leaving preceding offsets untouched.

---

### 11. Lexical Tokenization & Parser State Machines
* **Where Used**:
  * [RcParserService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/RcParserService.cs) (classes `RcTokenizer`, `RcParserService`, `ParserState`)
* **How it Makes it Possible**:
  Windows C++ `.rc` resource files are source code files containing preprocessor directives, block/inline comments, macro definitions, dialog geometry arrays, and string tables. Standard Regex or simple text parsing is highly brittle and easily corrupts files.
  * `RcTokenizer` walks through characters, grouping them into structured tokens like `StringLiteral`, `Identifier`, and `Number` while cleanly bypassing comments.
  * `RcParserService` uses `ParserState` to track if it is inside a `STRINGTABLE` block and handles nesting levels (`BEGIN` / `END` blocks), ensuring only target UI strings are extracted.

---

### 12. Switch Expressions & Pattern Matching
* **Where Used**:
  * [AiService.cs](file:///f:/Drive%20I/CCtech_Documents/MCP/Localizer_Final/Localizer_App/Services/AiService.cs) (inside `MapModelToApiName`)
* **How it Makes it Possible**:
  A modern, concise switch syntax introduced in C# 8.0. It maps user-facing dropdown selections directly to the exact API model codes used by the Gemini API endpoint.
  ```csharp
  return m switch
  {
      "gemini-2-flash" => "gemini-2.0-flash",
      "gemini-2-flash-lite" => "gemini-2.0-flash-lite",
      "gemini-3-flash" => "gemini-3-flash-preview",
      "gemini-3.1-pro" => "gemini-3.1-pro-preview",
      _ => model
  };
  ```

---

### 13. Nullable Reference Types & Coalescing Operator (`??`, `?`)
* **Where Used**:
  * Throughout the codebase (e.g., `Models.cs` validation properties, `MainWindow.xaml.cs` DOM queries)
* **How it Makes it Possible**:
  Enhances safety by preventing `NullReferenceException` crashes.
  * `int? ValidationScore`: Nullable integers represent keys that haven't undergone validation yet.
  * `string? ValidationFeedback`: Nullable strings represent items with no issues.
  * `element?.Attribute("value") ?? ""`: Uses the null-conditional and null-coalescing operators to safely fall back to an empty string if config nodes are missing.
