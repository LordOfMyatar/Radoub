# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Prerequisites

Before releasing:
1. All changes committed and pushed to main
2. CHANGELOG.md updated with version section
3. All tests passing
4. PR merged (if applicable)

## Instructions

1. **Verify Clean State**
   ```bash
   git status
   git log --oneline -3
   ```
   - Must be on `main` branch
   - Working directory must be clean
   - Confirm latest commit is what we want to release

2. **Check Current Version**
   - Read `Parley/CHANGELOG.md` to find the version being released
   - Look for the most recent version section (e.g., `[0.1.5-alpha]`)
   - Confirm the version hasn't been released yet

3. **Confirm with User**
   - Show the version to be released
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release vX.Y.Z?"

4. **Create and Push Tag**
   ```bash
   git tag -a vX.Y.Z -m "Release vX.Y.Z"
   git push origin vX.Y.Z
   ```
   - Replace X.Y.Z with actual version
   - This triggers the `parley-release.yml` workflow

5. **Provide Release Link**
   - Show GitHub Actions URL: `https://github.com/LordOfMyatar/Radoub/actions/workflows/parley-release.yml`
   - Note: Build takes ~10 minutes for all platforms

## Output Format

```
## Release Checklist

- [ ] On main branch
- [ ] Working directory clean
- [ ] CHANGELOG version: [version]
- [ ] Latest commit: [hash] [message]

## Commits in this Release
[list of commits since last tag]

## Ready to Release?
Confirm to create tag `vX.Y.Z` and trigger release build.
```

## Safety Checks

- NEVER release from a feature branch
- NEVER release with uncommitted changes
- ALWAYS confirm version with user before tagging
- If anything looks wrong, STOP and ask
