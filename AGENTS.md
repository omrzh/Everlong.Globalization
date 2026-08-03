# AGENTS.md — Everlong.Globalization

Solo-maintained repo (`github.com/omrzh/Everlong.Globalization`). Keep processes light.

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
- Post-release smoke verification (from outside the repo, nuget.org only): `dotnet new console`, `dotnet add package Everlong.Globalization --version <v>`, add `LangVersion preview` for partial-property consumers, then `Lang.Provider.Use(...)` + `GetString` and run.

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
