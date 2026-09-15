# solution-layout

*The authoritative solution layout: the directory set, the csproj-level facts, the folder conventions and the
per-project contribution rules. `AGENTS.md` points here for structure.*

---

# Toc

- `src/Everlong.Globalization/` — the runtime library: string providers, locale switching, ICU MessageFormat, code-gen base types
- `src/Everlong.Globalization.Tools/` — the `dotnet-elg` CLI (PackAsTool): i18n JSON management and `Lang.g.cs` codegen
- `tests/Everlong.Globalization.Tests/` — runtime behaviour tests
- `tests/Everlong.Globalization.Tools.Tests/` — CLI + codegen snapshot (Verify) tests

---

# Repository root

- `EverlongGlobalizationVersion.props` is the single source of the version — `GlobalizationVersion` = **0.3.0**
  (`Version` / `FileVersion` / `AssemblyVersion` derive from it).
- `Everlong.Globalization.slnx` lists the 2 `src/` projects and the 2 `tests/` projects.
- `README.md` is the repo landing page / consumer readme. Each `src/` project packs its **own** `readme.md`
  as `PackageReadmeFile` — the two packages have different audiences (runtime library vs CLI tool).
- `.editorconfig` / `.gitattributes` hold the style baseline: 2-space indentation, LF line endings.
- `docs/CHANGELOG.md` records what changed per release (English, consumer-facing);
  `docs/solution-layout.md` the structure and `docs/TODO.md` the backlog.

---

# src/Everlong.Globalization/

The runtime library: the provider model (`IStringProvider`, `LangProvider`, `DictionaryStringProvider`,
`ChainedStringProvider`, `FallbackStringProvider`, `NullStringProvider`), multi-assembly coordination
(`LangCoordinator` + `ILangManifest`), the `IcuMessageFormatter` and the `StringSectionBase` code-gen base.

`net8.0`, `PackageId Everlong.Globalization`, packable. **Zero external dependencies** — pure BCL, keep it
that way (a new dependency is a design decision, not a convenience). `LangVersion preview`.

`PackageReadmeFile` = the project-local `readme.md`, `PackageIcon` = `icon.png`,
`PackageLicenseExpression` = MIT. `GenerateDocumentationFile=true` and CS1591 is **not** suppressed here —
public members must be documented as they are added. `GeneratePackageOnBuild` fires for any non-Debug
configuration.

---

# src/Everlong.Globalization.Tools/

The `dotnet-elg` CLI: `init`, `gen`, `add`, `check`, `sync`, `normalize` over `Properties/i18n/**/*.json`. It is the
only sanctioned way to generate `Lang.g.cs` — never hand-write the generated file.

`net8.0`, `OutputType=Exe`, `PackageId Everlong.Globalization.Tools`, `PackAsTool=true`,
`ToolCommandName=dotnet-elg`, packable. The one external dependency is `System.CommandLine`
`2.0.0-beta4.22272.1` (beta by design — the CLI is a tool, not a library). `NoWarn` adds `CS1591;CS1573`
(the CLI surface is not a documented API). `InternalsVisibleTo` points at the test project, which pins
the internals below.

The `i18n.jsonc` surface is one table — `Services/Lang/Models/LangOptions.cs` (group, key, JSON kind,
allowed values, default, doc comment). `LangConfigFileReader` validates a config against it,
`LangConfigTemplate` renders `init`'s scaffold from it, `LineEndings` resolves its one value token, and
`LangOptionsTests` compares the docs' key sets to it. `LocaleFile` reads a catalog so a syntax error
names the file, `LangNormalizeService` rewrites every catalog with the configured ending, and
`LangExceptions` carries the failures that are reported as one message rather than a stack trace.

---

# tests/

## Conventions

- Everything runs on Linux and Windows; both projects are `net8.0` and `IsPackable=false`.
- `Everlong.Globalization.Tests` is the runtime suite; `Everlong.Globalization.Tools.Tests` is the
  CLI + codegen suite and is snapshot-driven (Verify).
- Every suite is **xunit v3** (`xunit.v3`, with `Verify.XunitV3` where a snapshot is verified) and the two
  projects pin the same test stack. The xunit targets make a v3 test project an executable, so
  `dotnet test` drives it through the VSTest adapter; a call that takes a `CancellationToken` is given
  `TestContext.Current.CancellationToken` (`xUnit1051`).
- Snapshot baselines live in `tests/Everlong.Globalization.Tools.Tests/Verified/` as `*.verified.txt`
  (one per test method). On a mismatch Verify writes a `*.received.txt` next to it; **diff-tool popups
  are disabled** (`DiffRunner.Disabled = true` in `ModuleInitializer.cs`) — read the `received` file.
  Promote it by renaming to `*.verified.txt`, never edit a baseline by hand.

---

# tests/Everlong.Globalization.Tests/

The runtime behaviour suite (`GlobalizationTests.cs`). References `Everlong.Globalization` (project);
packages: `xunit.v3` 3.2.2, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit.runner.visualstudio` 3.1.5,
`coverlet.collector` 6.0.4.

---

# tests/Everlong.Globalization.Tools.Tests/

The CLI + codegen suite: `CodeGeneratorTests`, `CsprojLocatorTests`, `JsonLangReaderTests` and one test
class per command service (`LangAddServiceTests`, `LangCheckServiceTests`, `LangGenServiceTests`,
`LangInitServiceTests`, `LangSyncServiceTests`). References `Everlong.Globalization.Tools` (project);
packages: `Verify.XunitV3` 31.12.5, `xunit.v3` 3.2.2, `Microsoft.NET.Test.Sdk` 17.14.1,
`xunit.runner.visualstudio` 3.1.5, `coverlet.collector` 6.0.4. `GlobalUsings.cs` carries `using Xunit;`.

---

# docs/

## Conventions

This folder holds the repository's public documentation; the reading map (which doc owns which topic)
lives in `AGENTS.md`. `README.md` is the consumer landing page (first mile); `docs/solution-layout.md`
the structure and csproj facts; `docs/CHANGELOG.md` what changed per release; `docs/TODO.md` the
unscheduled backlog. `docs/skills/everlong-globalization/` is the in-repo skill for the runtime and the
CLI toolchain — the design red lines in `AGENTS.md` are its digest.

`docs/skills/` is for **downstream consumers** (it teaches how to *use* the package), not for this repo's
own maintenance — this repo is upstream and does not need to load a how-to about itself.
