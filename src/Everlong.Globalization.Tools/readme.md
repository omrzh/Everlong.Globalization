# Everlong.Globalization.Tools

CLI tool for i18n JSON management and code generation — the companion to the `Everlong.Globalization` runtime library.

```
dotnet tool install --global Everlong.Globalization.Tools
```

---

## Quick start

```bash
# 1. Scaffold i18n folder structure in your project
cd YourProject
dotnet elg init

# 2. Add <Elg> to your .csproj
# <PropertyGroup>
#   <Elg>Properties/i18n/i18n.jsonc</Elg>
# </PropertyGroup>

# 3. Edit Properties/i18n/en/App.json, then generate the Lang class
dotnet elg gen

# 4. Add more locales (copy structure from default locale)
dotnet elg add zh-CN
dotnet elg add fr
```

---

## Commands

| Command | Description |
|---|---|
| `init` | Scaffold i18n folder structure with sample files |
| `gen` | Generate `Lang.g.cs` from locale JSON files |
| `add <locale>` | Add a new locale (copy from default) |
| `check` | Validate key consistency across all locales |
| `sync [locale]` | Sync missing keys and ordering from the default locale |

### `init`

```
dotnet elg init [--project <csproj>] [--locale <bcp47>]
```

Creates `Properties/i18n/` with a sample config file and default locale folder.
Auto-detects `<NeutralLanguage>` from the `.csproj`.

### `gen`

```
dotnet elg gen [--project <csproj>]
```

Reads all JSON/JSONC files under each locale subdirectory, merges them into
per-locale dictionaries, and emits `Lang.g.cs` (and optionally
`Lang.Locales.g.cs`, `Lang.{Module}.g.cs`, `Lang.Coordinator.g.cs`).

### `add`

```
dotnet elg add <locale> [--project <csproj>] [--force]
```

Copies all JSON files from the default locale into a new locale folder.
Use `--force` to overwrite an existing folder.

### `check`

```
dotnet elg check [--project <csproj>]
```

Checks every non-default locale against the default for missing and orphan
keys. Also validates `<SatelliteResourceLanguages>` consistency.
Exits with code 1 if any issues found.

### `sync`

```
dotnet elg sync [locale] [--project <csproj>]
```

Adds missing keys (with default values from the default locale) to locale
files and re-orders keys to match the default locale. Preserves existing
translations. When `locale` is omitted, syncs all non-default locales.

---

## Configuration

The `i18n.jsonc` file controls code generation:

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
  }
}
```

Full reference: [github.com/omrzh/Everlong.Globalization](https://github.com/omrzh/Everlong.Globalization)

---

## License

MIT — Copyright © 2026 Everlong Technology
