---
name: everlong-globalization
description: Everlong.Globalization — runtime locale switching, string providers, ICU MessageFormat, and the dotnet elg toolchain. Rules of the road.
---

## 0. Mental Model

Everlong.Globalization has three layers:

```
┌─────────────────────────────────────────────┐
│  dotnet elg CLI  (code generation & checks)  │
│  JSON files → Lang.g.cs                      │
├─────────────────────────────────────────────┤
│  Runtime types (IStringProvider, LangProvider…)
│  GetString(key, fallback) → string           │
├─────────────────────────────────────────────┤
│  Generated code (Lang.g.cs) — typed accessors│
│  Lang.Dialog.Alert.Title → string            │
└─────────────────────────────────────────────┘
```

Each layer builds on the one below it, but the upper layers are optional. You can use `IStringProvider` directly without the generator or CLI.

---

## 1. Runtime Type Rules

### 1.1 Always expose mutable locale via `LangProvider`, never raw `IStringProvider`

| Do | Don't |
|----|-------|
| `public LangProvider Provider { get; }` | `public IStringProvider Provider { get; }` |
| Consumers can subscribe to `ProviderChanged` | Consumers get a frozen reference and miss locale switches |

`LangProvider` is a mutable wrapper. When `Use()` swaps the inner provider, all existing references pick up the change immediately — because every `GetString()` call delegates to the current inner provider.

**Only expose `IStringProvider` directly when a component genuinely needs a static snapshot and doesn't care about subsequent switches.**

### 1.2 Register `LangProvider` as a Singleton

| Host | Pattern |
|------|---------|
| ASP.NET Core | `services.AddSingleton<LangProvider>()` |
| GUI (WPF/Avalonia) | `services.AddSingleton<LangProvider>()` |
| Console | `new` it once and hold it for the process lifetime |

`LangProvider` is thread-safe (all operations are reads). Change-tracking uses events, not scopes.

### 1.3 Don't `new` runtime types other than `DictionaryStringProvider`

| OK to `new` | Don't `new` |
|-------------|-------------|
| `DictionaryStringProvider(dict)` — unit tests | `LangProvider` — use DI or a factory |
| `ChainedStringProvider(primary, secondary)` — advanced composition | `StringSectionBase` — only generated code should inherit from this |
| `IcuMessageFormatter.Format(...)` — static call | `FallbackStringProvider` / `NullStringProvider` — use the `Instance` singleton |

### 1.4 `FallbackStringProvider` and `NullStringProvider` are singletons

Never `new FallbackStringProvider()`. `FallbackStringProvider.Instance` and `NullStringProvider.Instance` are the only legitimate entry points. Both behave identically (returning the fallback value), but their semantics differ:
- **NullStringProvider** = "not configured yet; placeholder"
- **FallbackStringProvider** = "key not found; return fallback"

---

## 2. CLI Toolchain Rules

### 2.1 Opt-in via `<Elg>` in `.csproj`

```xml
<PropertyGroup>
  <Elg>Properties/i18n/i18n.jsonc</Elg>
</PropertyGroup>
```

`<Elg>` points to the i18n config file. Valid values:
- `true` — auto-detect `Properties/i18n/i18n.jsonc` → `i18n.json`
- A relative path — custom config location

Projects without `<Elg>` are ignored by `dotnet elg gen`.

### 2.2 JSON file layout

```
Properties/i18n/
├── i18n.jsonc               # code generation config
├── en/                      # default locale (required)
│   ├── App.json
│   └── Dialog.json
├── zh-CN/                   # translated locale
│   ├── App.json
│   └── Dialog.json
└── fr/
    └── App.json
```

- Each locale is a subdirectory named with its BCP 47 tag
- JSON files inside are split by module (`App.json`, `Dialog.json`)
- The filename (minus extension) determines the generated section class name
- The default locale directory **must exist**, or `dotnet elg gen` will error

### 2.3 Always use the CLI to generate code, never hand-write `Lang.g.cs`

| Do | Don't |
|----|-------|
| `dotnet elg gen` | Hand-write or copy-paste `Lang.g.cs` |
| Config-driven generation | Manual edits on generated files |

Generated files are marked ReadOnly — the IDE will show them as non-editable. This is intentional. Change strings by editing the JSON files, then regenerate.

**Exception:** If you genuinely need to customize generated code behaviour, change the `i18n.jsonc` config, not `Lang.g.cs`.

### 2.4 The `globalizationNamespace` config field

```jsonc
"codegen": {
    "globalizationNamespace": "Everlong.Globalization"
}
```

This tells the generator which `using` directive to emit. The default is `"Everlong.Globalization"`. Only change this if you forked the runtime library and renamed the namespace. **Otherwise leave it alone.**

---

## 3. String Key Naming Conventions

### 3.1 Use dot-separated hierarchical keys

```
"App.Greeting.Title"
"App.Greeting.Message"
"Dialog.Alert.Title"
"Dialog.Alert.ButtonText"
"Nav.Menu.File"
"Nav.Menu.Edit"
```

Not flat: `"app_greeting_title"`, `"AppGreetingTitle"`, `"app.greeting.title"` (all-lowercase is hard to read; PascalCase-with-dots matches C# conventions).

### 3.2 The `$KeyPrefix` mechanism

```json
{
  "$KeyPrefix": "Everlong.Nester.Dialog",
  "Alert": {
    "Title": "Alert",
    "ButtonText": "OK"
  }
}
```

This produces actual lookup keys `"Everlong.Nester.Dialog.Alert.Title"` and `"Everlong.Nester.Dialog.Alert.ButtonText"`.

**Use this when one assembly's strings should live under another assembly's key namespace** (e.g., Nester's dialog resources are embedded under `Everlong.Nester.Dialog.*`, so consumers access them via `Lang.Dialog.Alert.Title`).

### 3.3 The `$DataOnly` flag

```json
{
  "$KeyPrefix": "Everlong.Nester.Dialog",
  "$DataOnly": true,
  "Alert": { "Title": "Alert", "ButtonText": "OK" }
}
```

Modules with `$DataOnly = true` **won't generate a section class**, but their key-value pairs are still merged into the `Locales` dictionary. Use cases:
- Cross-assembly shared strings (contribute key-values only, not types)
- You don't want `Lang.Nester.Dialog.Alert` polluting your API surface

---

## 4. ICU MessageFormat

### 4.1 When to use it

| Use ICU | Don't use ICU |
|---------|---------------|
| Strings with variables: `"Hello, {name}!"` | Static text with no placeholders |
| Plural/gender branching: `"{count} item(s)"` | Simple concatenation |
| `{name, select, male {He} female {She} other {They}}` | Logic that can be split into separate keys without branching |

### 4.2 ICU writing conventions

```
Simple interpolation：      "{name}"
Select：                    "{gender, select, male {He} female {She} other {They}}"
Plural：                    "{count, plural, one {# item} other {# items}}"
Ordinal：                   "{place, selectordinal, one {1st} two {2nd} few {3rd} other {#th}}"
```

- **Variable names are camelCase**: `{userName}` not `{UserName}` or `{user_name}`
- **Write the ICU pattern on a single line**, don't split across lines
- **Don't embed HTML in values** — ICU doesn't escape HTML; the output side should sanitize separately

### 4.3 How the generator handles ICU

The generator detects patterns like `{name}` or `{name, type, ...}` in string values. When found:
- A `FormatXxx()` helper method is generated
- Method parameter names are extracted from the ICU placeholders

```json
{ "RangeHintFormat": "Range: {min} ~ {max}" }
```

→ generates:

```csharp
public string RangeHintFormat => GetString("RangeHintFormat", ...);
public string FormatRangeHint(object? min, object? max)
    => IcuMessageFormatter.Format(RangeHintFormat, ...);
```

### 4.4 Don't force every string into ICU just for consistency

Static text stays as plain text. There's no need to wrap it as `"{text}"` for uniformity. The generator handles both correctly.

---

## 5. Multi-Assembly Coordination

### 5.1 When to use `LangCoordinator`

```
App.exe              → its own Lang.Provider (primary locale)
Everlong.Nester.dll  → its own Lang.Provider (dialog strings)
```

```csharp
var coord = new LangCoordinator();
coord.Register(App.Lang.Manifest);
coord.Register(Nester.Lang.Manifest);
coord.Use("zh-Hans-CN");  // one call switches both assemblies
```

**Only use `LangCoordinator` when you need unified locale switching across assemblies.** If you only have one assembly, `Lang.Provider.Use()` is sufficient.

### 5.2 Collaborative Overlay

When the app supports a locale that a library doesn't, the generated coordinator code automatically chains the app's provider as the primary and the library's fallback as the secondary via `ChainedStringProvider`. This lets the app "overlay" strings onto the library without the library knowing about the app.

This behaviour is controlled by the config:

```jsonc
"coordinator": {
    "generate": true,
    "manifests": ["Everlong.Nester.Properties.Lang"]
}
```

---

## 6. Testing

### 6.1 Runtime layer

```csharp
// Given a dictionary
var dict = new DictionaryStringProvider(new Dictionary<string, string>
{
    ["key"] = "value"
});

// Verify lookup
Assert.Equal("value", dict.GetString("key"));
Assert.Equal("fallback", dict.GetString("missing", "fallback"));
```

`NullStringProvider.Instance` and `FallbackStringProvider.Instance` are good choices for "empty provider" in tests.

### 6.2 LangProvider switching tests

```csharp
var en = new FixedProvider("en");
var zh = new FixedProvider("zh");
var provider = new LangProvider(en);

Assert.Equal("en", provider.GetString("key"));

provider.Use(zh);
Assert.Equal("zh", provider.GetString("key")); // immediate
```

**Important:** `LangProvider.Use()` is synchronous. After switching, all subsequent `GetString()` calls go through the new provider. No need for await or refresh.

### 6.3 Default value tests

When `Lang.Provider.Use(NullStringProvider.Instance)` is called, `Lang.Dialog.Alert.Title` should return `"Alert"` (the fallback). This validates that the generated `GetString` call's fallback parameter is correct.

---

## 7. Red Lines

| Forbidden | Why |
|-----------|-----|
| Hardcoding locale-dependent strings directly in code | Requires code changes to translate instead of editing JSON |
| Using `.resx` files instead of JSON | `dotnet elg` toolchain doesn't understand resx; resx's runtime switching and strong typing are less flexible than `LangProvider` |
| Hand-editing `Lang.g.cs` | The ReadOnly marker exists for a reason |
| Implementing `IStringProvider` without handling `INotifyStringProviderChanged` | Breaks change-event propagation across nested `LangProvider` chains |
| Registering `LangProvider` as Scoped or Transient | Each scope gets a different instance; static access like `Lang.Dialog.Alert.Title` points to the wrong one |
| Storing secrets (passwords, API keys) in i18n JSON files | i18n files get shipped to clients and checked into version control |
| String keys containing spaces or special characters | `GetString("some key")` works functionally but violates naming conventions and the generator can't emit valid C# property names |
| Omitting the fallback parameter | `GetString("key")` returns empty string; blank UI is worse than English text |
| Running multiple generators concurrently on the same project | `Lang.g.cs` is a single file; concurrent writes produce corruption |
| Renaming keys in i18n config after they've been merged to main | Already-generated `Lang.g.cs` property names don't auto-update — rename = API change, requires a new `dotnet elg gen` and syncing all translation files |

---

## 8. Recommended Workflow

### 8.1 New project

```
1. dotnet elg init                    # create Properties/i18n/
2. Edit Properties/i18n/en/App.json   # write strings
3. Add `<Elg>` declaration to .csproj
4. dotnet elg gen                     # generate Lang.g.cs
5. Use Lang.App.Xxx in code
```

### 8.2 Adding a locale

```
1. dotnet elg add zh-CN              # copy en/ → zh-CN/
2. Translate zh-CN/*.json
3. dotnet elg gen                    # regenerate, now includes zh-CN
4. Lang.Provider.Use(Lang.Locales.ZhCn)  # switch
```

### 8.3 Daily iteration

```
1. Edit en/*.json
2. dotnet elg gen
3. dotnet elg check                  # check other locales for missing keys
4. dotnet elg sync                   # auto-fill missing keys (with default values)
5. Translate the new keys
```

### 8.4 CI validation

```bash
dotnet elg check                     # exits with code 1 if any keys are missing/orphaned
dotnet elg gen                       # regenerate, validating config correctness
```

It's recommended to add `dotnet elg check` to your CI pipeline to prevent translation files from falling behind the default locale.
