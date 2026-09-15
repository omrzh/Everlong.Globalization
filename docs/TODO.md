# Backlog

Known work, deliberately unscheduled: do it when there is time. Nothing here is a promise, and nothing
here blocks a release.

## Automated package smoke test

The post-release smoke check is manual today (`AGENTS.md` §Testing & verification): `dotnet new console`,
`dotnet add package Everlong.Globalization --version <v>`, `Lang.Provider.Use(...)` + `GetString`, run.
Automate it — a CI step or a script that consumes the just-packed nupkg from a local feed — so a broken
package fails here instead of at the consumer.
