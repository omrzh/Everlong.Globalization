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
