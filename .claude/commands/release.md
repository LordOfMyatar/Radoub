# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Usage

```
/release parley    # Release Parley
/release manifest  # Release Manifest
```

If no tool specified, ask user which tool to release.

## Tag Formats

| Tool | Tag Format | Workflow |
|------|------------|----------|
| Parley | `v0.1.5-alpha` | `parley-release.yml` |
| Manifest | `manifest-v0.6.0-alpha` | `manifest-release.yml` |

## Prerequisites

Before releasing:
1. All changes committed and pushed to main
2. Tool's CHANGELOG.md updated with version section
3. All tests passing
4. PR merged (if applicable)

## Instructions

1. **Determine Tool**
   - Parse argument: `parley` or `manifest`
   - If not provided, ask: "Which tool to release? (parley/manifest)"

2. **Verify Clean State**
   ```bash
   git status
   git log --oneline -3
   ```
   - Must be on `main` branch
   - Working directory must be clean
   - Confirm latest commit is what we want to release

3. **Check Current Version**
   - **Parley**: Read `Parley/CHANGELOG.md`
   - **Manifest**: Read `Manifest/CHANGELOG.md`
   - Look for the most recent version section (e.g., `[0.1.5-alpha]`)
   - Confirm the version hasn't been released yet

4. **Confirm with User**
   - Show the tool and version to be released
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release [tool] [tag]?"

5. **Create and Push Tag**

   **For Parley:**
   ```bash
   git tag -a vX.Y.Z -m "Parley Release vX.Y.Z"
   git push origin vX.Y.Z
   ```

   **For Manifest:**
   ```bash
   git tag -a manifest-vX.Y.Z -m "Manifest Release vX.Y.Z"
   git push origin manifest-vX.Y.Z
   ```

6. **Provide Release Link**
   - **Parley**: `https://github.com/LordOfMyatar/Radoub/actions/workflows/parley-release.yml`
   - **Manifest**: `https://github.com/LordOfMyatar/Radoub/actions/workflows/manifest-release.yml`
   - Note: Build takes ~10 minutes for all platforms

## Output Format

```
## Release Checklist - [Tool]

- [ ] On main branch
- [ ] Working directory clean
- [ ] CHANGELOG version: [version]
- [ ] Latest commit: [hash] [message]

## Commits in this Release
[list of commits since last tag for this tool]

## Ready to Release?
Confirm to create tag `[tag]` and trigger release build.
```

## Safety Checks

- NEVER release from a feature branch
- NEVER release with uncommitted changes
- ALWAYS confirm version with user before tagging
- If anything looks wrong, STOP and ask
