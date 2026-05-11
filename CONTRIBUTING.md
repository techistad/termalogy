# Contributing to Termalogy

Thanks for contributing.

## Golden Rule

Do not push to `main` directly.

Every contribution must come from a separate branch and go through a Pull Request.

## Workflow

One-time local setup:

```powershell
git config core.hooksPath .githooks
```

1. Sync latest `main`.
2. Create a new branch.
3. Make changes and commit.
4. Push branch.
5. Open PR into `main`.
6. Wait for CI to pass.
7. Request review and merge.

## Branching

Use one of these patterns:

- `feature/<topic>`
- `fix/<topic>`
- `docs/<topic>`
- `chore/<topic>`

Examples:

- `feature/command-palette`
- `fix/window-close-index`
- `docs/release-process`

## Local Commands

```powershell
dotnet restore .\Termalogy.sln
dotnet build .\Termalogy.sln -c Release
```

## Commit Messages

Use clear, scoped commits. Conventional Commit style is preferred:

- `feat: add stats command`
- `fix: handle missing alias file`
- `docs: update release steps`

## Pull Request Checklist

- [ ] Branch is not `main`
- [ ] Build passes locally
- [ ] CI passes on GitHub
- [ ] README/docs updated if behavior changed
- [ ] No unrelated files included

## Versioning + Releases

We use SemVer tags (`vMAJOR.MINOR.PATCH`).

- `v0.1.0` first stable release
- `v0.1.1` patch bugfix
- `v0.2.0` new features

Tag push triggers release automation.

## Code of Conduct

By participating, you agree to follow [CODE_OF_CONDUCT.md](./CODE_OF_CONDUCT.md).
