# Changelog

What changed per release, newest first.

## Unreleased

### Added

- Config validation against a single option table. An unknown group, an unknown key, a value of the
  wrong JSON kind and a value outside the option's allowed set all fail the run, and the error names the
  config file, the JSON path and — when one is close — the key that was probably meant. Previously a
  mistyped `output.lineEndings` silently regenerated `Lang.g.cs` at the default ending, `"xmlDoc":
  "yes"` read as absent, a non-string option value threw a bare `InvalidOperationException` with no file
  or JSON path, and `"privte"` for a visibility reached the generated declaration and broke the
  consumer's build. A config carrying an option this version predates now fails instead of being
  half-read — update `dotnet-elg` when that happens. Locale files keep their permissive contract: any
  key inside one is a string entry.

### Changed

- `output.lineEnding` now defaults to `"lf"` instead of `"platform"`, so regeneration is byte-identical
  on every platform without configuring anything. A CRLF repository declares `"crlf"`; `"platform"`
  stays available as the explicit opt-in of the running OS's convention. A repository that relied on the
  unset field following the OS has to say so now.
- `sync` applies the configured ending to a file whose keys are already in sync instead of leaving it
  with the endings it happened to have, so `sync` and `gen` agree about the bytes of a tree.
- `init`'s scaffold carries every option: it now includes the `coordinator` group (it omitted two keys
  the reader accepted), and it writes `locale.sourceDir` with a forward slash so the same config
  resolves on every OS.

## 0.2.0 — 2026-09-15

### Added

- `output.lineEnding` — pick the line endings of every file the tool writes: `"lf"`, `"crlf"`, or
  `"platform"` (the default) to follow the convention of the running OS. Configuring `"lf"` makes a
  regeneration byte-identical across platforms, so running `gen` or `sync` on Windows no longer
  rewrites LF files as CRLF.

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
