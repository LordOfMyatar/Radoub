# Release the Hounds

Create and push a version tag to trigger the GitHub Actions release workflow.

Radoub ships as one bundle — Parley, Manifest, Fence, Trebuchet, Quartermaster, Relique,
Reliquary together. Per-tool releases are not supported.

## Usage

```
/release           # Release the Radoub bundle
```

## How versioning works

Tags are `radoub-vX.Y.Z`. Find the last one, bump the patch, unless the user asks for minor
or major. Tool CHANGELOGs record what changed, so the bundle version just increments — no
separate Radoub CHANGELOG needed.

```bash
git tag -l "radoub-v*" --sort=-v:refname | head -1
```

## How release notes work

The draft lives in `NonPublic/release-notes.md` (gitignored), accumulated by `/pre-merge`
across sprints and editable by the user in between. At release time it is compressed into a
user-facing TL;DR and delivered **on the annotated tag message** — the workflow extracts it
with `git tag -l --format='%(contents:body)'`.

Three hard rules (#2236):

- Release notes are **never** written to the repo root.
- No commit to main, no PR.
- Per-PR detail belongs in CHANGELOGs. Release notes are the headline pass.

> **Order hazard.** The NonPublic draft is the only persisted copy of the accumulated
> highlights. Synthesize → push the tag → **then** reset the draft. Resetting first means a
> failed push leaves nothing to retry from.

## Phase 1 — Verify

```bash
git status
git log --oneline -3
```

Must be on `main`, clean, with the latest commit being what should ship. Never release from a
feature branch or with uncommitted changes. If anything looks off, stop and ask.

Prerequisites: everything merged and pushed, tool CHANGELOGs updated with version sections,
tests passing. Post-merge is the usual release point.

## Phase 2 — Draft the notes

### 2.1 With an existing draft (preferred)

Read `NonPublic/release-notes.md`, fill the Tool Versions table with NBGV values, and show it
to the user: "Release notes look good? [y/edit/regenerate]". **edit** means they revise the
file and re-run; **regenerate** falls through to 2.2.

### 2.2 Without one (fallback)

Parse each tool's CHANGELOG for entries newer than the last release date — Parley, Manifest,
Quartermaster, Fence, Relique, Reliquary, Trebuchet. Present a numbered list and ask which
items are highlights ("1,3,5", "all", or "none").

### 2.3 Synthesize

Release notes are user-facing and short:

- One paragraph up top — the headline story
- Four to six "What changed" bullets in plain language
- A short "User-reported fixes" list when applicable
- The tool versions table

No per-PR walls of text; link to CHANGELOGs for detail.

Write the result to a temp file. `mktemp --suffix` is a GNU extension — on this Windows box
build the path explicitly instead:

```bash
TMP_NOTES="$TEMP/radoub-release-notes.md"
# ...write synthesized notes to $TMP_NOTES...
```

Tool versions come from NBGV:

```bash
dotnet nbgv get-version --project Parley -v SemVer2
```

## Phase 3 — Confirm

Show the checklist and wait for an explicit yes. Always confirm the version before tagging.

```
## Release Checklist - Radoub

- [ ] On main branch
- [ ] Working directory clean
- [ ] Last release: radoub-v0.8.3 (2026-01-15)
- [ ] Next version: radoub-v0.8.4
- [ ] Latest commit: [hash] [message]

## Tool Versions Included
- Parley: [NBGV version]
- Manifest: [NBGV version]

## Changes Since Last Release
[changelog entries across tools]

## Ready to Release?
Confirm to create tag `radoub-v0.8.4` and trigger the release build.
```

## Phase 4 — Tag and push

```bash
git tag -a radoub-vX.Y.Z -F "$TMP_NOTES"
git push origin radoub-vX.Y.Z
rm "$TMP_NOTES"
```

The notes ride in the tag annotation, so no file lands in the repo and no PR is needed.

## Phase 5 — Reset the draft

**Only after the push succeeds.** If it failed, do not reset — debug and retry.

Rewrite `NonPublic/release-notes.md` from the empty template, with the new version and today's
date on the "Accumulated since last release" line.

Then give the user the build link — roughly 10–15 minutes for all platforms:

```
https://github.com/LordOfMyatar/Radoub/actions/workflows/radoub-release.yml
```

## Artifacts

The `radoub-release.yml` workflow produces two packages:

| Package | Contents |
|---------|----------|
| Bundled | All tools plus the shared .NET runtime |
| Unbundled | Tools only — smaller, requires .NET 9.0 installed |
