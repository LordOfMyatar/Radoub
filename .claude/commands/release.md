# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Upfront Questions

**IMPORTANT**: Gather user input in phases, not all at once.

### Phase 1: Release Type
1. **Release Type** (if not specified in args): "Which release? [parley/manifest/radoub]"

### Phase 2: Changelog Highlights Selection
After determining release type and reading changelog:
1. Parse the changelog section for the version being released
2. Present a **numbered list** of all changelog items (bullet points and headers)
3. Ask user: "Which items are highlights? (Enter numbers, e.g., 1,3,5 or 'all' or 'none')"
4. Selected items become **Highlights** in release notes
5. Remaining items are auto-categorized and summarized with link to full changelog

### Phase 3: Final Confirmation
1. Show release summary with selected highlights
2. **Version Confirmation**: "Ready to release [type] v[version] from commit [hash]? [y/n]"

## Usage

```
/release parley    # Release Parley only
/release manifest  # Release Manifest only
/release radoub    # Release bundled Parley + Manifest
```

If no tool specified, ask user which to release.

## Tag Formats

| Release | Tag Format | Workflow |
|---------|------------|----------|
| Parley | `v0.1.5-alpha` | `parley-release.yml` |
| Manifest | `manifest-v0.6.0-alpha` | `manifest-release.yml` |
| Radoub (bundled) | `radoub-v0.8.3` | `radoub-release.yml` |

## Prerequisites

Before releasing:
1. All changes committed and pushed to main
2. CHANGELOG.md updated with version section
3. All tests passing
4. PR merged (if applicable)

## Instructions

1. **Determine Release Type**
   - Parse argument: `parley`, `manifest`, or `radoub`
   - If not provided, ask: "Which release? (parley/manifest/radoub)"

2. **Verify Clean State**
   ```bash
   git status
   git log --oneline -3
   ```
   - Must be on `main` branch
   - Working directory must be clean
   - Confirm latest commit is what we want to release

3. **Check Current Version & Parse Changelog**
   - **Parley**: Read `Parley/CHANGELOG.md`
   - **Manifest**: Read `Manifest/CHANGELOG.md`
   - **Radoub**: Read `CHANGELOG.md` (repo-level) + tool changelogs
   - Look for the most recent version section (e.g., `[0.1.5-alpha]`)
   - Confirm the version hasn't been released yet
   - Extract all items from that version's section

4. **Present Changelog Items for Selection**

   Display items as a numbered list:
   ```
   ## Changelog Items for v0.1.120-alpha

   1. [###] Refactor: Tech debt - Large files needing refactoring (#719)
   2. [-] Extracted WindowLayoutService for window position, panel layout
   3. [-] SettingsService now delegates window-related properties
   4. [-] Removed plugin migration code (PluginSettings.json)
   5. [-] Follows same service extraction pattern as RecentFilesService

   Which items are highlights? (Enter numbers like 1,2,5 or 'all' or 'none')
   ```

   - `[###]` prefix = section header
   - `[-]` prefix = bullet point

5. **Generate Release Notes**

   Based on selection, create structured release notes:

   **If user selects items 1,2:**
   ```markdown
   ## What's New

   - **Refactor: Tech debt** - Large files needing refactoring (#719)
   - Extracted WindowLayoutService for window position, panel layout

   ## Other Changes

   Bug fixes, refactoring, and maintenance. See [CHANGELOG.md](link) for details.
   ```

   **If user selects 'none':**
   ```markdown
   ## What's New

   Bug fixes and maintenance release. See [CHANGELOG.md](link) for details.
   ```

6. **Write Release Notes File**

   Write the generated notes to `release-notes.md` in repo root.
   This file is read by the workflow during release.

   ```bash
   # The workflow reads this file for the release body
   ```

7. **Confirm with User**
   - Show the release type, version, and generated highlights
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release [type] [tag]?"

8. **Create and Push Tag**

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

   **For Radoub (bundled):**
   ```bash
   git tag -a radoub-vX.Y.Z -m "Radoub Bundle Release vX.Y.Z"
   git push origin radoub-vX.Y.Z
   ```

6. **Provide Release Link**
   - **Parley**: `https://github.com/LordOfMyatar/Radoub/actions/workflows/parley-release.yml`
   - **Manifest**: `https://github.com/LordOfMyatar/Radoub/actions/workflows/manifest-release.yml`
   - **Radoub**: `https://github.com/LordOfMyatar/Radoub/actions/workflows/radoub-release.yml`
   - Note: Build takes ~10-15 minutes for all platforms

## Output Format

```
## Release Checklist - [Type]

- [ ] On main branch
- [ ] Working directory clean
- [ ] CHANGELOG version: [version]
- [ ] Latest commit: [hash] [message]

## Commits in this Release
[list of commits since last tag for this type]

## Ready to Release?
Confirm to create tag `[tag]` and trigger release build.
```

## Safety Checks

- NEVER release from a feature branch
- NEVER release with uncommitted changes
- ALWAYS confirm version with user before tagging
- If anything looks wrong, STOP and ask

## Bundle Notes

The `radoub` release creates a combined package with:
- Parley dialog editor
- Manifest journal editor
- Shared .NET runtime and dependencies

Individual tool releases remain available for standalone downloads.
