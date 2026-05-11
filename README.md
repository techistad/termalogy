# Termalogy

[![CI](https://github.com/techistad/termalogy/actions/workflows/ci.yml/badge.svg)](https://github.com/techistad/termalogy/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/techistad/termalogy)](https://github.com/techistad/termalogy/releases)
[![Downloads](https://img.shields.io/github/downloads/techistad/termalogy/total)](https://github.com/techistad/termalogy/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](./LICENSE)

Terminal-first skin layer for Windows. GUI apps still run normally, but you access and manage them from a full-screen terminal shell.

## Features

- Full-screen terminal skin UI (safe, non-destructive)
- Panic exit hotkey: `Ctrl+Alt+Backspace`
- Command-driven launcher with fuzzy app matching (`open`, `apps find`)
- Alias persistence in LocalAppData
- Startup-on-login toggle (`startup on|off|status`)
- Stealth mode with taskbar control (`stealth`, `taskbar`)
- Open-source workflow with CI + release tags

## Install (From GitHub Releases)

1. Open [Releases](https://github.com/techistad/termalogy/releases).
2. Download `TermalogySkin-vX.Y.Z-win-x64.zip` from the latest release.
3. Extract and run `TermalogySkin.exe`.

## Developer Setup

```powershell
git clone https://github.com/techistad/termalogy.git
cd termalogy
git config core.hooksPath .githooks
dotnet restore .\Termalogy.sln
dotnet build .\Termalogy.sln
```

Run locally:

```powershell
dotnet run --project .\TermalogySkin\TermalogySkin.csproj
```

## Commands

- `help`
- `open <app|url|path>`
- `apps list`, `apps find <query>`
- `run <cmd>`
- `pwd`, `cd <path>`, `ls [path]`
- `win list`, `win close <index>`
- `alias list|add|remove|path`
- `startup on|off|status`
- `stealth on|off|status`
- `taskbar hide|show|status`
- `version` (app version)
- `stats` (GitHub stars, forks, release downloads)
- `top on|off`
- `exit`

## Git + Contribution Rules

- Never commit directly to `main`.
- Create a new branch for every change.
- Open a Pull Request to merge into `main`.
- CI must pass before merge.
- Configure local hooks once: `git config core.hooksPath .githooks`

See [CONTRIBUTING.md](./CONTRIBUTING.md) for complete contributor workflow.

## Branch Naming Convention

- `feature/<short-topic>`
- `fix/<short-topic>`
- `docs/<short-topic>`
- `chore/<short-topic>`

Example:

```powershell
git checkout -b feature/window-focus-command
```

## Release + Version Tags

Tags follow semantic versioning:

- `v0.1.0`
- `v0.2.0`
- `v0.2.1`

Create release tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

A GitHub Actions workflow builds Windows artifacts and publishes them to Releases.

## Usage Count Strategy

Termalogy estimates user adoption using:

- Total GitHub release downloads
- Repository stars/watchers/forks

This is displayed publicly via badges and in-app `stats` command.

## Remote

Primary repository remote:

`https://github.com/techistad/termalogy.git`

## License

MIT — see [LICENSE](./LICENSE).
