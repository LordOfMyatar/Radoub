# Post-Merge Cleanup

Perform cleanup tasks after a PR has been merged to main.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before running any cleanup, collect these answers in ONE interaction:

1. **Branch Cleanup**: "Delete local feature branch?" [y/n]
2. **Release**: "Create a GitHub release for this version?" [y/n]

After collecting answers, proceed through all steps without further prompts unless errors occur.

## Usage

```
/post-merge
/post-merge #[pr-number]
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

Based on user's answer:
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

### Step 6: Create Release (if requested)

Based on user's answer, invoke:
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
| Local branch | ✅ Deleted / ⏭️ Kept |
| Issues closed | ✅ #x, #y / N/A |
| Epic updated | ✅ #z / N/A |
| Release | ✅ Created vX.Y.Z / ⏭️ Skipped |

---

### Next Steps

1. [Suggested next issues to work on]
```

## Notes

- Run this after confirming PR was merged successfully
- Safe to run multiple times (idempotent operations)
- Branch deletion is local only
- No validation/tests - that was done in pre-merge
