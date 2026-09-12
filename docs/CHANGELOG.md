# Changelog

What changed per release, newest first.

## 0.1.0 — 2026-07-23

Initial release.

### Added

- `IStringProvider` — key-based string lookup with a fallback parameter.
- `DictionaryStringProvider`, `ChainedStringProvider`, `FallbackStringProvider`, `NullStringProvider` —
  ready-to-use provider implementations.
- `LangProvider` — mutable, event-driven provider wrapper; `Use()` swaps the active locale at runtime
  and every existing reference follows immediately (`INotifyStringProviderChanged`).
- `LangCoordinator` + `ILangManifest` — multi-assembly locale switching with exact → `CultureInfo`
  parent-chain → language-subtag-prefix resolution, and a `ChainedStringProvider` overlay so an app can
  fill gaps in a library's catalogs.
- `StringSectionBase` — `INotifyPropertyChanged` base for generated typed accessors.
- `IcuMessageFormatter` — minimal ICU MessageFormat: interpolation, `select`, `plural`, `selectordinal`.
- `Everlong.Globalization.Tools` — the `dotnet-elg` CLI (`init`, `gen`, `add`, `check`, `sync`) for i18n
  JSON management and `Lang.g.cs` code generation.
