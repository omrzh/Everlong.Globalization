# Changelog

What changed per release, newest first.

## Unreleased

### Added

- `dotnet elg normalize` — rewrites every catalog with the line endings `output.lineEnding` declares,
  the default locale included, because no other command writes that directory. It is the migration step
  for changing the option on an existing repository: declare the value, run `normalize` then `gen`, and
  land the result as one commit; the command prints the `.git-blame-ignore-revs` step that keeps
  `git blame` readable. Every catalog is validated before any of them is written, so a broken one leaves
  the tree unconverted rather than half converted, and the config file itself is never rewritten (that
  would drop its comments).
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
- `check` fails on a catalog whose line endings are not the declared ones, default locale included. The
  drift used to surface only when something happened to rewrite the file, which meant it could sit in
  the tree and then arrive inside an unrelated commit.
- `gen` exits with code 1 when it had to skip a project, instead of printing `Skipped:` and reporting
  success. A batch that skipped a project has not done its job, and CI cannot tell the difference
  otherwise.
- `init`'s scaffold carries every option: it now includes the `coordinator` group (it omitted two keys
  the reader accepted), and it writes `locale.sourceDir` with a forward slash so the same config
  resolves on every OS.
- `add` writes the copied files with the configured line endings. It used to copy the default locale's
  bytes, so a new locale could land inconsistent with `output.lineEnding` — and with `"lf"` as the
  default, that is now the common case rather than a coincidence of which machine authored the source.

### Fixed

- A translation containing U+2028 or U+2029 produced generated C# that does not compile. C# counts the
  Unicode line and paragraph separators among its line terminators, and the generator escaped only the
  control characters; a raw one reached the string literal and stopped the consumer's build. They are
  escaped as `\u2028` / `\u2029` now, and as `&#8232;` / `&#8233;` — plus `&#133;` for NEL — inside the
  XML doc comments, where a raw separator would have ended the comment line.
- A locale file the tool cannot read is reported with its path: `Invalid locale file '<path>': …` and
  exit code 1, instead of a JSON parser message that knows a line and a byte offset but not which
  catalog it came from. A catalog whose root is not a JSON object is rejected rather than read as
  empty, which is how `sync` would have overwritten the translations still in the file.

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
