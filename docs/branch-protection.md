# Branch Protection Setup

This repository expects `main` to be protected.

## Manual Settings (GitHub UI)

In repository settings, configure protection for `main`:

- Require a pull request before merging
- Require at least 1 approval
- Require status checks to pass before merging
  - `branch-policy`
  - `build-windows`
- Require conversation resolution before merging
- Require linear history
- Do not allow force pushes

References:

- https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches
- https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/defining-the-mergeability-of-pull-requests/managing-a-branch-protection-rule

## Scripted Setup (Optional)

Use `scripts/setup-branch-protection.ps1` if you have admin access and authenticated `gh` CLI.
