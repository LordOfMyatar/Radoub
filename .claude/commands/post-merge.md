# Post-Merge Cleanup

Perform cleanup tasks after a PR has been merged to main.

## Defaults & Flags

| Action | Default | Flag to Override |
|--------|---------|------------------|
| Branch cleanup | **Yes** — delete local feature branch | `--noclean` to keep it |
| Release | **No** — skip release creation | `--release` to create one |

No upfront questions needed — just run and go. Flags override defaults.

## Usage

```
/post-merge
/post-merge #[pr-number]
/post-merge --noclean          # keep the local branch
/post-merge --release          # create a GitHub release
/post-merge #123 --release     # specific PR + release
```

If no PR number provided, uses the most recently merged PR.

## Workflow

### Step 1: Identify Merged PR

```bash
# Get current branch (should be main after merge)
git branch --show-current

# Find most recent merge or if PR number provided
gh pr view [number] --json mergedAt,mergeCommit,number,title,body
```

Verify the PR was actually merged (not closed without merge).

### Step 2: Update Local Repository

```bash
git checkout main
git pull origin main
```

### Step 3: Clean Up Local Feature Branch

**Default: Yes** (skip if `--noclean` flag provided)

```bash
git branch -d [branch-name]
```

### Step 4: Close Related Issues

**Extract issues from PR body**:
```bash
gh pr view [number] --json body -q '.body' | grep -oEi "(closes|fixes|resolves) #[0-9]+" | grep -oE "[0-9]+"
```

**For each issue, check and close if still open**:
```bash
gh issue view [issue-number] --json state -q '.state'
# If OPEN:
gh issue close [issue-number] --comment "Completed in PR #[pr-number]"
```

### Step 5: Update Parent Epic (if applicable)

**Extract epic from PR body**:
```bash
gh pr view [number] --json body -q '.body' | grep -oEi "epic[^#]*#[0-9]+" | grep -oE "[0-9]+" | head -1
```

If found, add completion comment:
```bash
gh issue comment [epic-number] --body "Sprint completed via PR #[pr-number]: [PR title]"
```

### Step 6: Update Developer Documentation

**MANDATORY** — do not skip this step.

**Wiki repo**: `d:\LOM\workspace\Radoub.wiki\`

#### 6a: Map CHANGELOG to Wiki Pages

Read the CHANGELOG entries for this PR/sprint. Map changes to wiki pages:

| CHANGELOG / Code Area | Wiki Page |
|---|---|
| Parley services/viewmodels | Parley-Developer-Architecture |
| Parley copy/paste | Parley-Developer-CopyPaste |
| Parley delete behavior | Parley-Developer-Delete-Behavior |
| Parley scrap system | Parley-Developer-Scrap-System |
| Parley tests | Parley-Developer-Testing |
| Manifest services | Manifest-Developer-Architecture |
| Quartermaster | Quartermaster-Developer-Architecture |
| Fence | Fence-Developer-Architecture |
| Trebuchet | Trebuchet-Developer-Architecture |
| Relique | Relique-Developer-Architecture |
| Radoub.Formats | Radoub-Formats (+ specific format pages) |
| Radoub.UI | Radoub-UI-Developer |

If a mapped wiki page does not exist yet, create it from the developer architecture template in the wiki's CLAUDE.md.

#### 6b: Update Mapped Dev Doc Pages

For each mapped page:
1. Read the current wiki page
2. Update architecture/data flow sections to reflect code changes from this sprint
3. Update Mermaid diagrams if component relationships changed
4. Set `*Page freshness: YYYY-MM-DD*` to today's date

**Style**: Clinical, terse. No marketing speak. Technical accuracy over readability.

#### 6c: 30-Day Freshness Sweep

Check ALL wiki pages with freshness dates:

```bash
grep -r "Page freshness:" d:/LOM/workspace/Radoub.wiki/*.md
```

**Dev doc pages** (`*-Developer-*`) older than 30 days:
- Review content against current source code
- Update content if stale, update freshness date to today

**User doc pages** (non-developer pages) older than 30 days:
- Cut a GitHub issue per stale page
- **Title**: `[Docs] Review stale wiki page: {Page-Name}`
- **Labels**: `docs` + tool label (e.g., `parley`)
- **Body**: Page name, current freshness date, days since last update
- **Dedup**: Check for existing open issue with same title before creating

#### 6d: No Push Required

Changes stay local in the wiki repo. Do not push.

### Step 6e: Refresh Cache

After all mutations (issues closed, epic commented, stale doc issues created), refresh the cache:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

### Step 7: Create Release (if `--release` flag provided)

**Default: No** (skip unless `--release` flag provided)

Invoke:
```
/release
```

The `/release` command handles:
- Extracting CHANGELOG section for release notes
- Creating GitHub release with proper tagging
- Generating release assets if configured

### Step 8: Generate Summary

```markdown
## Post-Merge Summary

**PR**: #[number] - [title]
**Merged**: [date]
**Version**: [version from CHANGELOG]

---

### Cleanup Completed

| Task | Status |
|------|--------|
| Local branch | ✅ Deleted (default) / ⏭️ Kept (--noclean) |
| Issues closed | ✅ #x, #y / N/A |
| Epic updated | ✅ #z / N/A |
| Dev docs updated | ✅ [list pages] / ⏭️ No mapped pages |
| Stale user docs | ✅ Issues cut: #a, #b / ⏭️ None stale |
| Release | ✅ Created vX.Y.Z (--release) / ⏭️ Skipped (default) |

---

### Next Steps

1. [Suggested next issues to work on]
2. [If the completed work revealed unexpected complexity, suggest: "Consider `/research --spike [topic]` before starting [related work]"]
```

## Notes

- Run this after confirming PR was merged successfully
- Safe to run multiple times (idempotent operations)
- Branch deletion is local only
- No validation/tests - that was done in pre-merge
