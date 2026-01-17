# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Overview

Radoub releases include all tools (Parley, Manifest, Quartermaster) in a single bundle. Individual tool releases are not supported - we release the entire Radoub suite together.

## Upfront Questions

**IMPORTANT**: Gather user input in phases, not all at once.

### Phase 1: Changelog Highlights Selection
After reading changelogs:
1. Parse the changelog sections for each tool being released
2. Present a **numbered list** of all changelog items (bullet points and headers)
3. Ask user: "Which items are highlights? (Enter numbers, e.g., 1,3,5 or 'all' or 'none')"
4. Selected items become **Highlights** in release notes
5. Remaining items are auto-categorized and summarized with link to full changelog

### Phase 2: Final Confirmation
1. Show release summary with selected highlights
2. **Version Confirmation**: "Ready to release Radoub v[version] from commit [hash]? [y/n]"

## Usage

```
/release           # Release Radoub bundle
/release radoub    # Same as above
```

## Tag Format

| Release | Tag Format | Workflow |
|---------|------------|----------|
| Radoub (bundled) | `radoub-vX.Y.Z` | `radoub-release.yml` |

The workflow produces two artifacts:
- **Bundled**: Single package with all tools + shared runtime
- **Unbundled**: Separate tool packages without runtime (smaller, requires .NET installed)

## Prerequisites

Before releasing:
1. All changes committed and pushed to main
2. CHANGELOGs updated with version sections for all tools with changes
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

2. **Determine Version**
   - Read `CHANGELOG.md` (repo-level) for the Radoub version
   - Also check tool changelogs for included changes:
     - `Parley/CHANGELOG.md`
     - `Manifest/CHANGELOG.md`
     - `Quartermaster/CHANGELOG.md`
   - Look for the most recent version section (e.g., `[0.8.3]`)
   - Confirm the version hasn't been released yet

3. **Present Changelog Items for Selection**

   Gather items from repo-level and all tool changelogs for current versions:

   ```
   ## Changelog Items for Radoub vX.Y.Z

   ### Radoub (shared)
   1. [###] Feature: Something shared
   2. [-] Some shared change

   ### Parley v0.1.120-alpha
   3. [###] Refactor: Tech debt (#719)
   4. [-] Extracted WindowLayoutService

   ### Quartermaster v0.1.40-alpha
   5. [###] Sprint: Portrait & Character Preview (#922)
   6. [-] Portrait moved to Character panel

   Which items are highlights? (Enter numbers like 1,3,5 or 'all' or 'none')
   ```

   - `[###]` prefix = section header
   - `[-]` prefix = bullet point

4. **Generate Release Notes**

   Based on selection, create structured release notes:

   **If user selects items 1,3,5:**
   ```markdown
   ## What's New

   - **Feature: Something shared** - description
   - **Parley**: Tech debt refactoring (#719)
   - **Quartermaster**: Portrait & Character Preview (#922)

   ## Other Changes

   Bug fixes, refactoring, and maintenance. See tool CHANGELOGs for details.
   ```

   **If user selects 'none':**
   ```markdown
   ## What's New

   Bug fixes and maintenance release. See tool CHANGELOGs for details.
   ```

5. **Write Release Notes File**

   Write the generated notes to `release-notes.md` in repo root.
   This file is read by the workflow during release.

6. **Confirm with User**
   - Show the version and generated highlights
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release Radoub [tag]?"

7. **Create and Push Tag**

   ```bash
   git tag -a radoub-vX.Y.Z -m "Radoub Release vX.Y.Z"
   git push origin radoub-vX.Y.Z
   ```

8. **Provide Release Link**
   - `https://github.com/LordOfMyatar/Radoub/actions/workflows/radoub-release.yml`
   - Note: Build takes ~10-15 minutes for all platforms

## Output Format

```
## Release Checklist - Radoub

- [ ] On main branch
- [ ] Working directory clean
- [ ] Radoub CHANGELOG version: [version]
- [ ] Latest commit: [hash] [message]

## Tool Versions Included
- Parley: [version]
- Manifest: [version]
- Quartermaster: [version]

## Commits in this Release
[list of commits since last radoub tag]

## Ready to Release?
Confirm to create tag `radoub-vX.Y.Z` and trigger release build.
```

## Safety Checks

- NEVER release from a feature branch
- NEVER release with uncommitted changes
- ALWAYS confirm version with user before tagging
- If anything looks wrong, STOP and ask

## Bundle Contents

The Radoub release creates a combined package with:
- Parley dialog editor
- Manifest journal editor
- Quartermaster creature editor
- Shared .NET runtime and dependencies (bundled version)
- Just the tools (unbundled version - requires .NET 9.0 installed)
