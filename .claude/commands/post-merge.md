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

### Step 6: Create Release (if `--release` flag provided)

**Default: No** (skip unless `--release` flag provided)

Invoke:
```
/release
```

The `/release` command handles:
- Extracting CHANGELOG section for release notes
- Creating GitHub release with proper tagging
- Generating release assets if configured

### Step 7: Generate Summary

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
| Release | ✅ Created vX.Y.Z (--release) / ⏭️ Skipped (default) |

---

### Next Steps

1. [Suggested next issues to work on]
2. [If the completed work revealed unexpected complexity, suggest: "Consider `/spike [topic]` before starting [related work]"]
```

## Notes

- Run this after confirming PR was merged successfully
- Safe to run multiple times (idempotent operations)
- Branch deletion is local only
- No validation/tests - that was done in pre-merge
