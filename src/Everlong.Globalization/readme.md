# Everlong.Globalization

Zero-dependency .NET library for runtime locale switching, string lookup, and ICU MessageFormat formatting.

```
dotnet add package Everlong.Globalization
```

---

## Quick start

```csharp
using Everlong.Globalization;

// Create a provider with your string dictionary
var en = new DictionaryStringProvider(new Dictionary<string, string>
{
    ["App.Greeting"] = "Hello, {name}!",
    ["App.Button.Submit"] = "Submit",
});

var provider = new LangProvider(en);
string msg = provider.GetString("App.Greeting", "Hello!");

// Swap locale at runtime — all existing references update instantly
var zhCn = new DictionaryStringProvider(new Dictionary<string, string>
{
    ["App.Greeting"] = "你好，{name}!",
    ["App.Button.Submit"] = "提交",
});
provider.Use(zhCn);
```

---

## Key types

| Type | Description |
|---|---|
| `IStringProvider` | Key-based string lookup with fallback |
| `DictionaryStringProvider` | Backed by `IReadOnlyDictionary<string, string>` |
| `ChainedStringProvider` | Primary → secondary chain; empty from primary falls through |
| `FallbackStringProvider` | Singleton — always returns the fallback value |
| `NullStringProvider` | Singleton — always returns fallback (for testing) |
| `LangProvider` | Mutable wrapper; swap the inner provider at runtime |
| `LangCoordinator` | Coordinate locale switching across multiple assemblies |
| `ILangManifest` | Multi-assembly coordination contract |
| `StringSectionBase` | `INotifyPropertyChanged` base for generated typed accessors |
| `IcuMessageFormatter` | Minimal ICU MessageFormat engine |

---

## ICU MessageFormat

The built-in `IcuMessageFormatter` supports:

- **Simple interpolation**: `{name}`
- **Select**: `{gender, select, male {He} female {She} other {They}}`
- **Plural**: `{count, plural, one {# item} other {# items}}`
- **Select ordinal**: `{place, selectordinal, one {1st} two {2nd} few {3rd} other {#th}}`

```csharp
var fmt = new IcuMessageFormatter();
string result = fmt.Format("{count, plural, one {# item} other {# items}}",
    new Dictionary<string, object> { ["count"] = 3 });
// → "3 items"
```

---

## Code generation companion

This library is the runtime dependency for the **dotnet-elg** CLI tool
(`Everlong.Globalization.Tools`), which generates `Lang.g.cs` from
locale JSON files — providing typed accessors, format helpers, and
multi-assembly coordinator support.

```
dotnet tool install --global Everlong.Globalization.Tools
```

---

## License

MIT — Copyright © 2026 Everlong Technology
