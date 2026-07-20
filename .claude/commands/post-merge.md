# Post-Merge Cleanup

Clean up after a PR merges to main.

## Usage

```
/post-merge [#pr-number] [--noclean] [--release]
```

Without a PR number, uses the most recently merged PR.

| Action | Default | Override |
|--------|---------|----------|
| Delete the local feature branch | Yes | `--noclean` keeps it |
| Create a release | No | `--release` runs `/release` |

No upfront questions — run and go.

## Phase 1 — Sync

### 1.1 Confirm the merge

```bash
git branch --show-current
gh pr view [number] --json mergedAt,mergeCommit,number,title,body,headRefName
```

Verify it actually merged rather than being closed unmerged. Everything downstream depends on
this.

### 1.2 Update main and delete the branch

```bash
git checkout main
git pull origin main
git branch -d [branch-name]      # skip when --noclean
```

`git branch -d` refuses to delete unmerged work, which is the safety net — if it objects,
investigate before forcing.

## Phase 2 — Close out issues

### 2.1 Close what the PR resolved

Pull the linked issues from the PR body:

```bash
gh pr view [number] --json body -q '.body' | grep -oEi "(closes|fixes|resolves) #[0-9]+" | grep -oE "[0-9]+"
```

GitHub auto-closes linked issues on merge, so check live state before acting — closing an
already-closed issue is noise:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "N,N,N"
```

Close any that are still OPEN with `gh issue close [N] --comment "Completed in PR #[pr]"`.

**Comment when the closure needs explanation** — an issue closed without code (already
delivered elsewhere), or one where only part of the scope shipped. A bare auto-close implies
work happened; say so when it did not.

### 2.2 Remove pre-warm plans

```bash
rm -f NonPublic/Plans/*-[number]-plan.md
```

Throwaway artifacts. Deleting them stops a future `/pre-warm` or `/init-item` picking up a
stale plan. Gitignored, so nothing to commit. Report what was removed.

### 2.3 Update the parent epic

```bash
gh pr view [number] --json body -q '.body' | grep -oEi "epic[^#]*#[0-9]+" | grep -oE "[0-9]+" | head -1
```

Check the epic is still open before commenting — a reference may be historical context rather
than this sprint's parent. When it is the parent:

```bash
gh issue comment [epic] --body "Sprint completed via PR #[pr]: [title]"
```

## Phase 3 — Documentation (MANDATORY)

Wiki repo: `d:/LOM/workspace/Radoub.wiki/`. Never skip this phase.

### 3.1 Map changes to pages

Read this PR's CHANGELOG entries and map them:

| Code area | Wiki page |
|-----------|-----------|
| Parley services/viewmodels | Parley-Developer-Architecture |
| Parley copy/paste | Parley-Developer-CopyPaste |
| Parley delete behavior | Parley-Developer-Delete-Behavior |
| Parley scrap system | Parley-Developer-Scrap-System |
| Parley tests | Parley-Developer-Testing |
| Manifest | Manifest-Developer-Architecture |
| Quartermaster | Quartermaster-Developer-Architecture |
| Fence | Fence-Developer-Architecture |
| Trebuchet | Trebuchet-Developer-Architecture |
| Relique | Relique-Developer-Architecture |
| Radoub.Formats | Radoub-Formats plus the specific format page |
| Radoub.UI | Radoub-UI-Developer |

Missing page: create it from the template in the wiki's CLAUDE.md.

### 3.2 Update them

For each mapped page, update the architecture and data-flow sections to match the new code,
refresh Mermaid diagrams when relationships changed, and set `*Page freshness: YYYY-MM-DD*`
to today.

Watch for claims the sprint made false — a page describing behavior this PR changed is worse
than a stale date, because it reads as current.

Style: clinical and terse. Technical accuracy over readability.

### 3.3 30-day freshness sweep

Always run it. Never skip because some tickets already exist — check every stale page
individually, since some are tracked and others are not.

```bash
grep -r "Page freshness:" d:/LOM/workspace/Radoub.wiki/*.md
```

**Developer pages** (`*-Developer-*`) over 30 days: review against current source, update the
content, then re-date. Do not bump the date without reading the page — that defeats the rule.

**User pages** over 30 days: search for an existing tracking issue first.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "stale wiki page"
```

An umbrella backlog issue may already cover the whole set — add a sweep comment to it rather
than filing per-page duplicates. If nothing tracks a page, file
`[Docs] Review stale wiki page: {Page-Name}` labeled `docs` plus the tool.

```
⏭️ {Page-Name} (stale {N} days) — already tracked in #{N}
✅ {Page-Name} (stale {N} days) — NEW issue created: #{N}
```

Wiki changes stay **local**. Do not push.

### 3.4 Refresh the cache

After every mutation — issues closed, epic commented, doc issues filed:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

## Phase 4 — Report

Run `/release` first if `--release` was passed.

```markdown
## Post-Merge Summary

**PR**: #[number] - [title]
**Merged**: [date]
**Version**: [version from CHANGELOG]

| Task | Status |
|------|--------|
| Local branch | ✅ Deleted / ⏭️ Kept (--noclean) |
| Issues closed | ✅ #x, #y / N/A |
| Pre-warm plans | ✅ Removed [files] / ⏭️ None |
| Epic updated | ✅ #z / N/A |
| Dev docs updated | ✅ [pages] / ⏭️ No mapped pages |
| Stale user docs | ✅ Issues cut: #a, #b / ⏭️ None stale |
| Release | ✅ vX.Y.Z / ⏭️ Skipped |

### Next Steps

1. [Suggested next issues]
2. [If the work revealed unexpected complexity, suggest `/research --spike [topic]`]
```

## Notes

- Safe to re-run; the operations are idempotent.
- Branch deletion is local only — the remote branch is GitHub's to clean up.
- No tests here; `/pre-merge` already validated.
- Parsing JSON: use `gh --jq` or PowerShell `ConvertFrom-Json`. There is no `jq` on this box.
