# Backlog

Known work, deliberately unscheduled: do it when there is time. Nothing here is a promise, and nothing
here blocks a release.

## Automated package smoke test

The post-release smoke check is manual today (`AGENTS.md` §Testing & verification): `dotnet new console`,
`dotnet add package Everlong.Globalization --version <v>`, `Lang.Provider.Use(...)` + `GetString`, run.
Automate it — a CI step or a script that consumes the just-packed nupkg from a local feed — so a broken
package fails here instead of at the consumer.

## Harmonize test package versions

The two test projects pin different versions of the shared test stack (`xunit` 2.7.0 vs 2.9.3,
`Microsoft.NET.Test.Sdk` 17.9.0 vs 17.12.0, `xunit.runner.visualstudio` 2.5.7 vs 2.8.2,
`coverlet.collector` 6.0.0 vs 6.0.4). They work today; aligning them removes a version-conflict surface
and keeps `docs/solution-layout.md` honest.

## A line-ending option for the generated files

`CodeGenerator` builds every `*.g.cs` through `StringBuilder.AppendLine` (129 call sites), so the bytes
follow `Environment.NewLine`: one config over one set of locale files yields CRLF when `gen` runs on
Windows and LF anywhere else, and `LangGenService` writes that string verbatim, so a consumer has
nothing to override.  Both this repo and Nester keep LF as the baseline (`.editorconfig`,
`.gitattributes` `eol=lf`), so a Windows consumer ends up with generated files that disagree with the
working tree until git normalizes them on the next `add`: Nester's template i18n sweep
(`feat(examples): localize the template's user-facing strings`) had to `sed -i 's/\r$//'` four
regenerated files by hand, and an untouched regeneration still reports them as modified.

Determinism is the real defect — identical inputs must produce identical bytes on every platform.  The
field that fits the existing config is `output.lineEnding` (sibling of `dir` / `namespace` /
`className`), taking `"lf"` / `"crlf"` / `"platform"`:

- Parsed in `CsprojLocator.ReadI18nFileConfig`'s `output` group → `I18nFileConfig` → `LangConfig`.
- Applied at the single write site (`LangGenService.GenerateForProjectAsync`) as
  `generatedFile.Content.ReplaceLineEndings(...)`, so `CodeGenerator` keeps its `AppendLine` shape.
- **The default is the decision to make.**  `"platform"` preserves today's behavior; `"lf"` is
  deterministic and matches the baseline — a behavior change for existing consumers only in bytes git
  normalizes anyway.
- `LangInitService.BuildFullTemplate` gains the field with a comment.  The `init` sample and the `add` /
  `sync` JSON writers already emit LF (raw strings and `JsonNode.ToJsonString`), so only codegen needs
  the option.

Also update the `## Configuration` block in `README.md` and the config reference in
`docs/skills/everlong-globalization/SKILL.md` §2, and record it in `docs/CHANGELOG.md` for the release
that ships it.  A Verify snapshot cannot pin these bytes — the committed `.verified.txt` baselines are
LF while Windows generation emits CRLF, so the comparison normalizes line endings; assert on the
written file instead.  Nester then sets `"lineEnding": "lf"` in the one `i18n.jsonc` its four
projects generate from (`examples/Template.Shared/Properties/i18n/i18n.jsonc`) and the `sed` step goes
away.
