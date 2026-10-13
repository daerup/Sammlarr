# Contributing to Sammlarr

This page is for people who work on Sammlarr's code.

## Run it locally

You need:

- the .NET SDK that `global.json` names (a .NET 11 preview today; `dotnetup sdk install <version>` installs it),
- Node.js 24 for the Tailwind build (`dotnet build` runs `npm ci` and `npm run css` by itself),
- Docker for the E2E tests, the staging environment and the optional Radarr + Sonarr containers,
- the [Aspire CLI](https://aspire.dev) for `aspire run`.

```powershell
dotnet build
aspire run
```

Sammlarr then answers on http://localhost:6769 and the Aspire dashboard on http://localhost:15223. The first start
logs a setup code; open `/setup` and enter it to create the first admin.

## How a change gets in

Every change goes through a pull request into `main`, and pull requests are squash-merged. The PR title becomes the
commit on `main` and the line in the changelog, so it follows
[Conventional Commits](https://www.conventionalcommits.org): `feat(feeds): …`, `fix(usage): …`, `docs: …`. A check
on every PR refuses a title without a known type. The commits on your branch can say whatever helps you; only the
title counts.

What a title does to the next release:

| Type | Release | Example |
|---|---|---|
| `fix`, `perf`, `revert` | patch, 0.4.2 to 0.4.3 | `fix(arr): …` |
| `feat` | minor, 0.4.2 to 0.5.0 | `feat(feeds): …` |
| `feat!` or a `BREAKING CHANGE:` footer | minor before 1.0, major after | `feat(auth)!: …` |
| `docs`, `test`, `refactor`, `build`, `ci`, `chore` | none on their own | `test(ui): …` |

## Versions and releases

release-please keeps one Release PR open that collects everything merged since the last release, with the next
version and the changelog. Merging it tags `vX.Y.Z` and publishes a GitHub Release; that tag builds and pushes the
image to `ghcr.io/daerup/sammlarr`, built with the tag's version, so Settings › About shows the exact version. A local
build gets its version from MinVer, which reads the git tags: between releases it reads like `0.1.1-alpha.0.5`, five
commits past the last tag.

`CHANGELOG.md` is written by release-please. Do not edit it by hand; correct a merged PR's line with a
`BEGIN_COMMIT_OVERRIDE` block in that PR's description instead.

## Code style

[CSharpier](https://csharpier.com) formats every C# file. `dotnet tool restore` installs it, and the first build installs
a pre-commit hook that runs it on staged files; `dotnet csharpier check .` checks the whole tree.

Don't add analyzer warnings.

## Tests

Tests use [TUnit](https://tunit.dev) on Microsoft.Testing.Platform. Each tier is its own project under `Tests/` and
runs as an executable:

```powershell
dotnet run --project Tests/Core --no-build
```

[docs/architecture/testing.md](docs/architecture/testing.md) lists the tiers, what belongs in each, the E2E setup and
the environment variables. Every behaviour change comes with a test.

## Database changes

Change the model, then add a migration (`dotnet tool install --global dotnet-ef` if you don't have the tool):

```powershell
dotnet ef migrations add <Name> --project DB --startup-project DB
```

Migrations that shipped in a release are never edited or deleted.

## Architecture

The architecture is described in [docs/architecture](docs/architecture/README.md) and the decisions behind it in
[docs/adr](docs/adr/README.md).
