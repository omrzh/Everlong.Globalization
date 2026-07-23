# Everlong.Globalization

Zero-dependency .NET library for runtime locale switching, string lookup, and ICU MessageFormat formatting — plus a CLI tool for i18n JSON management and code generation.

No UI framework, no DI container, no MVVM toolkit — pure BCL.

```
dotnet add package Everlong.Globalization
```

---

## Features

- **`IStringProvider`** — key-based string lookup with fallback
- **`LangProvider`** — mutable provider wrapper; swap locale at runtime, all existing references pick up the change instantly
- **`LangCoordinator`** — coordinate locale switching across multiple assemblies
- **`StringSectionBase`** — `INotifyPropertyChanged` base class for generated typed accessors
- **`DictionaryStringProvider`**, **`ChainedStringProvider`**, **`FallbackStringProvider`**, **`NullStringProvider`** — ready-to-use provider implementations
- **`IcuMessageFormatter`** — minimal ICU MessageFormat engine (supports `{name}`, `{name, select, …}`, `{name, plural, …}`)
- **`dotnet elg` CLI** — init, code-gen, add locale, check consistency, sync keys

---

## Quick Start

### 1. Install the CLI tool

```bash
dotnet tool install --global Everlong.Globalization.Tools
```

### 2. Scaffold i18n in your project

```bash
cd YourProject
dotnet elg init
```

This creates `Properties/i18n/` with a sample `en/App.json` and a `i18n.jsonc` config file.

### 3. Add `<Elg>` to your `.csproj`

```xml
<PropertyGroup>
  <Elg>Properties/i18n/i18n.jsonc</Elg>
</PropertyGroup>
```

### 4. Edit your strings

Edit `Properties/i18n/en/App.json`:

```json
{
  "Greeting": "Hello, {name}!",
  "Button": {
    "Submit": "Submit",
    "Cancel": "Cancel"
  }
}
```

### 5. Generate the `Lang` class

```bash
dotnet elg gen
```

This produces `Properties/Lang.g.cs` with:
- `Lang.Provider` — the mutable locale provider
- `Lang.Locales.En` — compiled string dictionaries
- `Lang.App.Greeting` — typed property accessor
- `Lang.App.FormatGreeting(name)` — ICU MessageFormat helper
- `Lang.Manifest` — multi-assembly coordinator support

### 6. Use it in code

```csharp
using Everlong.Globalization;
using YourProject.Properties;

// Read a string
string msg = Lang.App.FormatGreeting("World");

// Switch locale at runtime
Lang.Provider.Use(Lang.Locales.ZhCn);
string chinese = Lang.App.FormatGreeting("世界");

// Or use the raw provider
string raw = Lang.Provider.GetString("App.Greeting", "Hello!");
```

### 7. Add more locales

```bash
dotnet elg add zh-CN
dotnet elg add fr
```

Translate the JSON files, then re-run `dotnet elg gen`.

---

## Runtime Library

### `IStringProvider`

```csharp
public interface IStringProvider
{
    string GetString(string key, string fallback = "");
}
```

| Implementation | Description |
|---|---|
| `DictionaryStringProvider` | Backed by `IReadOnlyDictionary<string, string>` |
| `ChainedStringProvider` | Primary → secondary chain; empty from primary falls through |
| `FallbackStringProvider` | Singleton — always returns `fallback` parameter |
| `NullStringProvider` | Singleton — always returns `fallback` (useful for testing) |
| `LangProvider` | Mutable wrapper; swap inner provider at runtime |

### `LangProvider`

The central mutable provider. Swap locale at any time — all components holding a reference see the new strings immediately.

```csharp
var provider = new LangProvider(Locales.En);
provider.Use(Locales.ZhCn);           // all consumers update instantly
```

Implements `INotifyStringProviderChanged` for integration with data binding.

### `LangCoordinator`

Coordinates locale switching across multiple assemblies, each with its own `ILangManifest`. Resolution algorithm: exact match → `CultureInfo` parent chain → language-subtag prefix → no-op.

### `StringSectionBase`

`INotifyPropertyChanged` base class for generated typed accessors. Automatically raises `PropertyChanged` with `null` (bindings refresh all) when the provider switches locale.

### `IcuMessageFormatter`

Minimal ICU MessageFormat implementation supporting:
- Simple interpolation: `{name}`
- Select: `{gender, select, male {He} female {She} other {They}}`
- Plural: `{count, plural, one {# item} other {# items}}`
- `selectordinal`: `{place, selectordinal, one {1st} two {2nd} few {3rd} other {#th}}`

---

## CLI Reference

```
dotnet elg [command] [options]

Commands:
  init             Scaffold i18n folder structure
  gen              Generate Lang.g.cs from locale JSON
  add <locale>     Add a new locale (copy from default)
  check            Validate key consistency across locales
  sync [locale]    Sync keys/order from default locale
```

### `init`

```
dotnet elg init [--project <csproj>] [--locale <bcp47>]
```

Creates `Properties/i18n/` with a sample config file and default locale folder. Auto-detects `<NeutralLanguage>` from the `.csproj`.

### `gen`

```
dotnet elg gen [--project <csproj>]
```

Reads all JSON/JSONC files under each locale subdirectory, merges them into per-locale dictionaries, and emits `Lang.g.cs` (and optionally `Lang.Locales.g.cs`, `Lang.{Module}.g.cs`, `Lang.Coordinator.g.cs`).

### `add`

```
dotnet elg add <locale> [--project <csproj>] [--force]
```

Copies all JSON files from the default locale into a new locale folder. Use `--force` to overwrite an existing folder.

### `check`

```
dotnet elg check [--project <csproj>]
```

Checks every non-default locale against the default for missing and orphan keys. Also validates `<SatelliteResourceLanguages>` consistency. Exits with code 1 if any issues found.

### `sync`

```
dotnet elg sync [locale] [--project <csproj>]
```

Adds missing keys (with default values) to locale files and re-orders keys to match the default locale. Preserves existing translations. When `locale` is omitted, syncs all non-default locales.

---

## Configuration

The `i18n.jsonc` (or `.json`) file controls code generation:

```jsonc
{
  "locale": {
    "default": "en",
    "sourceDir": "Properties\\i18n"
  },
  "output": {
    "dir": "Properties",
    "namespace": "MyApp.Properties",
    "className": "Lang"
  },
  "types": {
    "classVisibility": "public",
    "memberVisibility": "public",
    "localesVisibility": "public",
    "suffix": "Strings"
  },
  "codegen": {
    "xmlDoc": false,
    "formattingMethod": true,
    "globalizationNamespace": "Everlong.Globalization",
    "localesPartial": false,
    "sectionsPartial": false
  }
}
```

---

## Project Structure

```
Everlong.Globalization/
├── src/
│   ├── Everlong.Globalization/          # Runtime library (net8.0)
│   └── Everlong.Globalization.Tools/     # dotnet-elg CLI tool (net8.0)
├── tests/
│   ├── Everlong.Globalization.Tests/     # Runtime library tests
│   └── Everlong.Globalization.Tools.Tests/ # CLI tool tests
├── EverlongGlobalizationVersion.props
├── .gitignore
├── .gitattributes
└── README.md
```

---

## License

Copyright © 2026 Everlong Technology. MIT license.
