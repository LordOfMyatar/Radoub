# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

## Overview

Radoub releases include all tools (Parley, Manifest, Fence, Trebuchet, Quartermaster) in a single bundle. Individual tool releases are not supported - we release the entire Radoub suite together.

## Versioning Strategy

**Radoub uses a simple incrementing version**: `radoub-vX.Y.Z`

The version is determined by:
1. Find the last `radoub-v*` tag in git
2. Increment the patch version (Z) for the new release
3. User can override to bump minor (Y) or major (X) if needed

This avoids the need for a separate Radoub CHANGELOG - the tool changelogs track what changed, and the radoub version just increments each release.

## Release Notes Source

**Draft source**: `NonPublic/release-notes.md` (gitignored) — accumulated by `/pre-merge` during sprints.
**Synthesis**: User-facing TL;DR is generated from the draft at release time. Per-tool CHANGELOGs hold the per-PR detail.
**Delivery**: Notes ride on the annotated tag message — `git tag -a -F <tempfile>`. The workflow extracts them via `git tag -l --format='%(contents:body)'`. **No file in repo root. No commit to main. No PR.**

When `NonPublic/release-notes.md` exists:
- Read it as the draft input
- Compress the per-PR detail into a TL;DR (paragraph summary + bullet themes)
- User has had time to edit/curate between sprints

Hard rules:
- Release notes are NEVER written to the repo root.
- Per-PR detail belongs in CHANGELOGs, not release notes.
- See #2236 for the rationale.

## Upfront Questions

**IMPORTANT**: Gather user input in phases, not all at once.

### Phase 1: Review Draft Release Notes (if NonPublic/release-notes.md exists)

1. Read `NonPublic/release-notes.md`
2. Show the current draft to the user
3. Ask: "Release notes look good? [y/edit/regenerate]"
   - **y** → proceed to Phase 2
   - **edit** → user edits the file manually, then re-run
   - **regenerate** → fall back to CHANGELOG parsing (old behavior)

### Phase 1 (Fallback): Changelog Highlights Selection (if no NonPublic/release-notes.md)

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

3. **Load Release Notes**

   **If `NonPublic/release-notes.md` exists** (preferred path):
   - Read the file — it contains curated highlights, bug fixes, and tool sections
   - Fill in the Tool Versions table with NBGV-computed versions
   - Show draft to user for review (Phase 1 above)

   **If `NonPublic/release-notes.md` does NOT exist** (fallback):
   - Parse tool CHANGELOGs for entries newer than last release date:
     - `Parley/CHANGELOG.md`
     - `Manifest/CHANGELOG.md`
     - `Quartermaster/CHANGELOG.md`
     - `Fence/CHANGELOG.md`
     - `Relique/CHANGELOG.md`
     - `Trebuchet/CHANGELOG.md`
   - Present numbered list for highlight selection (Phase 1 fallback)
   - Generate release notes from selection

4. **Synthesize Final Notes (TL;DR style — high-level only)**

   Release notes are user-facing and must be short. Per-tool CHANGELOGs hold the
   per-PR detail; release notes are the headline pass.

   Rules:
   - One paragraph summary at the top (the headline story).
   - 4-6 bullet "What changed" themes in plain language.
   - One short "User-reported fixes" list if applicable.
   - Tool versions table.
   - **No** per-PR walls of text. Link to CHANGELOGs for details.

   Write the synthesized notes to a tempfile:

   ```bash
   # Workflow reads release notes from the annotated tag message.
   # NEVER write release-notes.md to the repo root (#2236).
   TMP_NOTES=$(mktemp --suffix=.md)
   # ...write synthesized notes to $TMP_NOTES...
   ```

5. **Reset NonPublic Draft**

   After confirming the notes, reset `NonPublic/release-notes.md` with a fresh template for the next release cycle:
   ```markdown
   # Release Notes (Draft)

   Accumulated since last release: **radoub-vX.Y.Z** (YYYY-MM-DD)
   ...
   ```

6. **Confirm with User**
   - Show the version and generated highlights
   - Show recent commits that will be included
   - Ask user to confirm: "Ready to release radoub-v0.8.4?"

7. **Create and Push Tag** (notes embedded in tag annotation — no main commit required)

   ```bash
   # Embed the TL;DR notes as the tag's annotated message (-F reads from file).
   # The workflow extracts the tag body via `git tag -l --format='%(contents:body)'`
   # and uses it as the GitHub release body. No file in repo root, no PR.
   git tag -a radoub-vX.Y.Z -F "$TMP_NOTES"
   git push origin radoub-vX.Y.Z
   rm "$TMP_NOTES"
   ```

   **Never** write or commit `release-notes.md` to the repo root (#2236).

8. **Provide Release Link**
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
- Parley: [NBGV computed version]
- Manifest: [NBGV computed version]
- Fence: [NBGV computed version]

## Changes Since Last Release
[list of changelog entries from all tools]

## Ready to Release?
Confirm to create tag `radoub-v0.8.4` and trigger release build.
```

**Getting Tool Versions** (NBGV):
```bash
# Preferred (no jq needed):
dotnet nbgv get-version --project Parley -v SemVer2
dotnet nbgv get-version --project Manifest -v SemVer2
dotnet nbgv get-version --project Fence -v SemVer2
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
- Fence merchant/store editor
- Shared .NET runtime and dependencies (bundled version)
- Just the tools (unbundled version - requires .NET 9.0 installed)
