# Release Process

1. Merge approved PRs into `main`.
2. Update changelog/release notes.
3. Create a semantic tag:

```powershell
git checkout main
git pull origin main
git tag v0.1.0
git push origin v0.1.0
```

4. GitHub Actions `release` workflow publishes:

- `TermalogySkin-vX.Y.Z-win-x64.zip`
- Release page notes

Users install by downloading the asset from GitHub Releases.
