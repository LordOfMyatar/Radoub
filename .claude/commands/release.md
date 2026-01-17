# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Overview

Radoub releases include all tools (Parley, Manifest, Quartermaster) in a single bundle. Individual tool releases are not supported - we release the entire Radoub suite together.

## Versioning Strategy

**Radoub uses a simple incrementing version**: `radoub-vX.Y.Z`

The version is determined by:
1. Find the last `radoub-v*` tag in git
2. Increment the patch version (Z) for the new release
3. User can override to bump minor (Y) or major (X) if needed

This avoids the need for a separate Radoub CHANGELOG - the tool changelogs track what changed, and the radoub version just increments each release.

## Upfront Questions

**IMPORTANT**: Gather user input in phases, not all at once.

### Phase 1: Changelog Highlights Selection
After reading changelogs:
1. Parse unreleased changes from each tool's changelog (entries after last release date)
2. Present a **numbered list** of all changelog items
3. Ask user: "Which items are highlights? (Enter numbers, e.g., 1,3,5 or 'all' or 'none')"
4. Selected items become **Highlights** in release notes

### Phase 2: Final Confirmation
1. Show release summary with selected highlights
2. Show the computed next version (e.g., `radoub-v0.8.4`)
3. Ask: "Ready to release? [y/n] or specify version bump [patch/minor/major]"

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
2. Tool CHANGELOGs updated with version sections
3. All tests passing
4. PR merged (post-merge is the typical release point)

## Instructions

1. **Verify Clean State**
   ```bash
   git status
   git log --oneline -3
   ```
   - Must be on `main` branch
   - Working directory must be clean
   - Confirm latest commit is what we want to release

2. **Determine Next Version**
   ```bash
   # Find last radoub release tag
   git tag -l "radoub-v*" --sort=-v:refname | head -1
   ```
   - If last was `radoub-v0.8.3`, next is `radoub-v0.8.4` (patch bump)
   - User can request minor bump (0.9.0) or major bump (1.0.0)

3. **Gather Changelog Entries**

   Find entries in each tool changelog that are newer than the last release:
   - `Parley/CHANGELOG.md`
   - `Manifest/CHANGELOG.md`
   - `Quartermaster/CHANGELOG.md`
   - `CHANGELOG.md` (repo-level, for shared changes)

   Look for version sections with dates after the last release date.

4. **Present Changelog Items for Selection**

   ```
   ## Changes Since Last Release (radoub-v0.8.3)

   ### Parley v0.1.120-alpha
   1. [###] Refactor: Tech debt (#719)
   2. [-] Extracted WindowLayoutService

   ### Quartermaster v0.1.40-alpha
   3. [###] Sprint: Portrait & Character Preview (#922)
   4. [-] Portrait moved to Character panel
   5. [-] Gender dropdown added

   Which items are highlights? (Enter numbers like 1,3 or 'all' or 'none')
   ```

5. **Generate Release Notes**

   Based on selection, create structured release notes:

   ```markdown
   ## What's New

   - **Parley**: Tech debt refactoring (#719)
   - **Quartermaster**: Portrait & Character Preview (#922)

   ## Tool Versions
   - Parley: v0.1.120-alpha
   - Manifest: v0.6.0-alpha
   - Quartermaster: v0.1.40-alpha

   ## Other Changes

   See individual tool CHANGELOGs for complete details.
   ```

6. **Write Release Notes File**

   Write the generated notes to `release-notes.md` in repo root.
   This file is read by the workflow during release.

7. **Confirm with User**
   - Show the version and generated highlights
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release radoub-v0.8.4?"

8. **Create and Push Tag**

   ```bash
   git tag -a radoub-vX.Y.Z -m "Radoub Release vX.Y.Z"
   git push origin radoub-vX.Y.Z
   ```

9. **Provide Release Link**
   - `https://github.com/LordOfMyatar/Radoub/actions/workflows/radoub-release.yml`
   - Note: Build takes ~10-15 minutes for all platforms

## Output Format

```
## Release Checklist - Radoub

- [ ] On main branch
- [ ] Working directory clean
- [ ] Last release: radoub-v0.8.3 (2026-01-15)
- [ ] Next version: radoub-v0.8.4
- [ ] Latest commit: [hash] [message]

## Tool Versions Included
- Parley: v0.1.120-alpha
- Manifest: v0.6.0-alpha
- Quartermaster: v0.1.40-alpha

## Changes Since Last Release
[list of changelog entries from all tools]

## Ready to Release?
Confirm to create tag `radoub-v0.8.4` and trigger release build.
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
