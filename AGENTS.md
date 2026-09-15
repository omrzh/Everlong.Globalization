# AGENTS.md — Everlong.Globalization

Solo-maintained repo (`github.com/omrzh/Everlong.Globalization`). Keep processes light.

## MANDATORY: check `dotnet-skills` before diving into the task

Find the skill that matches the task and read it. Avoid "this is simple, I can do it myself" thinking.

## Build & test commands

```bash
# Reduce noise if possible:
dotnet build Everlong.Globalization.slnx -v:q --nologo -clp:ErrorsOnly

# Run a single test project (`dotnet test` accepts only ONE project per invocation):
dotnet test tests/Everlong.Globalization.Tests/Everlong.Globalization.Tests.csproj
dotnet test tests/Everlong.Globalization.Tools.Tests/Everlong.Globalization.Tools.Tests.csproj

# Update Verify snapshots when generator output intentionally changes:
dotnet test tests/Everlong.Globalization.Tools.Tests/Everlong.Globalization.Tools.Tests.csproj
# Then rename *.received.* → *.verified.* in the `Verified/` subfolder.
```

Linting is analyzer-driven: `dotnet build` surfaces diagnostics. `.editorconfig` holds the style
baseline (2-space indent, LF, file-scoped namespaces, `csharp_prefer_braces`, …).

## Branching & releases

- **`master` is the trunk for real work.** Experiment branches are fine (`git switch -c experiment/...`) — but only *prerelease* tags may point to them (policy below).
- **Releases are tag-driven**: bump `GlobalizationVersion` in `EverlongGlobalizationVersion.props` → `git tag v<GlobalizationVersion>` → `git push origin v<GlobalizationVersion>`. The `release` workflow builds, tests, packs **both packages** (`Everlong.Globalization` + `Everlong.Globalization.Tools`) and pushes to nuget.org automatically.
- **No API keys anywhere.** nuget.org uses Trusted Publishing (OIDC): `NuGet/login@v1` exchanges a GitHub OIDC token for a short-lived key. The `user` input comes from the `NUGET_USER` Actions **variable** in the `production` environment — the nuget.org account that *created* the trust policy (not the package owner). It is a Variable, not a Secret, and not a credential. Never ask for or invent a NuGet API key.
- **Release branch policy (enforced by the workflow, do not bypass)**:
  - The tag MUST exactly match the props version: `v0.2.0` ↔ `0.2.0`. Mismatch → the release run fails.
  - **Stable** version (`0.2.0`, no `-` suffix) → the tag must point to a commit on `master`. Experiment-branch tags fail.
  - **Prerelease** version (`0.2.0-preview.1`) → may point to ANY branch. This is the sanctioned way to publish previews from experiment branches; prerelease packages are invisible to `dotnet add package` without `--prerelease`, so they never pollute stable consumers.

## CI/CD

- `.github/workflows/ci.yml` — build + `dotnet test` on every push/PR (ubuntu, .NET 8 + 10 SDKs; dual SDK because tests target net8.0 and `LangVersion preview` needs a new compiler).
- `.github/workflows/release.yml` — on `v*` tag: verify tag↔props sync + branch policy → Release build → test → `dotnet pack` both projects → `NuGet/login@v1` → push with `--skip-duplicate`.
- `dotnet test` accepts only ONE project per invocation — CI runs the two test projects as separate steps.

## Testing & verification

- `dotnet test tests/Everlong.Globalization.Tests tests/Everlong.Globalization.Tools.Tests` — unit + codegen snapshot (Verify) tests. Keep them green; they are the release gate.
- Changing generated output means promoting `.received.txt` to `.verified.txt` after reviewing the diff.
- **Every suite is xunit v3** — `xunit.v3`, plus `Verify.XunitV3` where a snapshot is verified; the v2
  packages (`xunit`, `Verify.Xunit`) are referenced nowhere. The xunit targets make a v3 test project an
  *executable*, so `dotnet test` drives it through the VSTest adapter and the `.verified.txt` baselines
  are untouched. A call that takes a `CancellationToken` gets `TestContext.Current.CancellationToken`
  (`xUnit1051`), never a suppression.
- Verify diff-tool popups are disabled (`DiffRunner.Disabled = true` in `ModuleInitializer.cs`) — on a
  snapshot mismatch, read the `*.received.*` file; no popup will appear.
- Post-release smoke verification (from outside the repo, nuget.org only): `dotnet new console`, `dotnet add package Everlong.Globalization --version <v>`, add `LangVersion preview` for partial-property consumers, then `Lang.Provider.Use(...)` + `GetString` and run.

## Code formatting (mandatory before commit)

**Repository Style Baseline:** 2-space indentation, LF line endings, file-scoped namespaces (as defined
in `.editorconfig`). AI-generated code may use 4-space indentation (the model's default style), but it
**must be reformatted before committing**:

```
dotnet format Everlong.Globalization.slnx whitespace --no-restore --include <scoped_files...>
```

- Run only `whitespace` checks (indentation, line endings, whitespace); do not run `style` or
  `analyzers` (these affect code semantics and require manual review).
- Line endings are LF, enforced by `.editorconfig` and `.gitattributes` (`eol=lf`).

**Clean up unused `using` directives** (IDE0005):

```
dotnet format Everlong.Globalization.slnx style --diagnostics IDE0005
```

> IDE0005 is enabled in `.editorconfig` (`dotnet_diagnostic.IDE0005.severity = warning`); it is silent
> by default and belongs to the `style` group (not `analyzers`). The same command removes duplicate
> `using` directives (CS0105).
>
> **Do not add `--no-restore` here.** IDE0005 needs a complete compilation to tell "unused" from
> "unresolved" — with `--no-restore` (a stale/incomplete load) it silently deletes `using` directives
> that are in fact required (observed: it removed `using Xunit;`, `using DiffEngine;`). Run it only
> after a fresh `dotnet build`; unlike the `whitespace` pass above, `--no-restore` is not safe for it.

## Docs: what to read, what to keep current

| Topic | Owning doc |
|---|---|
| Solution structure, csproj-level facts, packaging, per-project contribution rules | `docs/solution-layout.md` |
| Changelog — what changed per release (English, consumer-facing) | `docs/CHANGELOG.md` |
| Open work, unscheduled | `docs/TODO.md` |
| Consumer landing page | `README.md` |
| Runtime + CLI rules of the road (the full version of the design red lines below) | `docs/skills/everlong-globalization/SKILL.md` |

**Keep the docs current.** A change that moves structure, behavior or a public surface updates the
owning doc in the same change — documentation is part of the deliverable, not a follow-up. Register a
new document in this index and in `docs/solution-layout.md` in the change that creates it.

`docs/skills/` is the **downstream** how-to (it teaches consumers how to *use* the package); this repo
is upstream and does not load a how-to about itself, so there is no `.agents/skills/` here.

## Environment quirks

- NuGet global packages folder is **`D:\AppData\.nuget\packages`**, not `%USERPROFILE%\.nuget` — clear that path when a repacked version is not picked up.
- `Everlong.Globalization` has **zero external dependencies** — keep it that way.
- `Everlong.Globalization.Tools` is a `PackAsTool` CLI (`dotnet-elg`), depends on `System.CommandLine` (beta). The CLI is the only sanctioned way to generate `Lang.g.cs` — never hand-write it.

## Design red lines (from docs/skills/everlong-globalization)

- Strings live in `Properties/i18n/**/*.json` (BCP 47 locale dirs, module-split files), never hardcoded in code, never `.resx`.
- Expose `LangProvider` (mutable, singleton, event-driven), not raw `IStringProvider`; `Lang.Provider.Use()` is synchronous.
- `FallbackStringProvider.Instance` / `NullStringProvider.Instance` are singletons — never `new` them.
- ICU MessageFormat patterns stay on one line, camelCase variables, no HTML inside values.
- Multi-assembly locale switching goes through `LangCoordinator` with `ChainedStringProvider` overlay; no secrets in i18n JSON (they ship to clients).
- `dotnet elg check` must stay green (locales in sync); renaming a key = API change, needs regen + translation sync.

## Contribution gotchas

- `LangVersion preview` is used across the repo (partial-property consumers need it); keep the local SDK
  current (≥ 8.0.4xx; CI installs 8.0.x + 10.0.x).
- XML doc (`///`) on the public surface: the runtime library compiles with `GenerateDocumentationFile=true`
  and does **not** suppress CS1591, so public members must be documented as they are added. The `Tools`
  project suppresses CS1591/CS1573 — its surface is not a documented API.

## FrozenDictionary (`codegen.frozen`) — decided: NOT worth it

Benchmarked on net8.0 (2026-08): for string→string catalogs (100–100k keys), FrozenDictionary is **2–8% LARGER** in memory than `Dictionary` (the "less memory" claim doesn't hold here), and lookup gains (1.3–2.3x at 1000+ keys) are imperceptible in GUI binding scenarios — locale-switch cost is dominated by binding refresh, not dictionary reads. Net value is only immutability semantics (`Lang.Locales.*` can't be tampered with). Do **not** add a `frozen` codegen option; if it ever comes up again, it must default `false` and generated code would require net8.0+ consumers (`ToFrozenDictionary()` is net8+ API).

---

## ⛔ HARD RULES

1. **`dotnet format` ALWAYS with `--include <changed files>`** — a full run reformats unrelated files
   and pollutes the diff.
2. **Read the actual file before editing** — never trust remembered indentation/characters; assert
   against real bytes.
3. **`rg` skips hidden paths and honours `.gitignore` by default.** Any repo-wide claim — "no dangling
   reference anywhere", "this identifier appears nowhere" — needs `--hidden`, plus `--no-ignore` when
   the tree is ignored.
4. **Build and test output is localized** (`已成功生成`, `0 个错误`, `已通过!`, `总共 N 个测试文件`).
   Grepping for the English words `error`/`warning` silently misses everything — filter by diagnostic
   code instead (`CS\d{4}`, `IDE\d{4}`), or pass `-clp:ErrorsOnly` when only failures matter.
5. **A warning count from an incremental build is not comparable across runs.** Use `--no-incremental`
   for a full-build number, and record warnings by code rather than by count.
