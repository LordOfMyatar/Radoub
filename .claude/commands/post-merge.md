# Post-Merge Cleanup

Perform cleanup tasks after a PR has been merged to main.

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

# Find most recent merge commit
git log --merges -1 --format="%H %s"

# Or if PR number provided
gh pr view [number] --json mergedAt,mergeCommit,number,title
```

Verify the PR was actually merged (not closed without merge).

### Step 2: Update Local Repository

```bash
# Ensure we're on main with latest
git checkout main
git pull origin main
```

### Step 3: Clean Up Feature Branch

Check if feature branch should be deleted:
```bash
# List merged branches
git branch --merged main

# Check if remote branch was deleted
git fetch --prune
```

**Ask user before deleting:**
> "The feature branch `[branch-name]` has been merged. Would you like me to delete the local branch?"

Only delete if user confirms:
```bash
git branch -d [branch-name]
```

If user declines, note in summary that branch was kept.

### Step 4: Verify CHANGELOG

Read the CHANGELOG and confirm:
- Version section has correct date (should be merge date or earlier)
- PR number matches the merged PR
- All changes from the PR are documented

If date was TBD, warn user to update it.

### Step 5: Create GitHub Release (if applicable)

Check if this merge warrants a release:
- New features (`feat:` commits)
- Breaking changes
- Significant bug fixes
- Version bump in CHANGELOG

If release criteria met, **ask user:**
> "This merge includes [features/fixes]. Would you like me to run `/release` to create a GitHub release for v[version]?"

If user confirms, invoke the release command:
```
/release
```

The `/release` command handles:
- Extracting CHANGELOG section for release notes
- Creating GitHub release with proper tagging
- Generating release assets if configured

If user declines, note in summary that release was skipped.

### Step 6: Update Related Issues

Check for issues that should be closed:
```bash
# Find "closes #X" or "fixes #X" in PR
gh pr view [number] --json body | grep -oE "(closes|fixes|resolves) #[0-9]+"

# Check individual issues from CHANGELOG work items
gh issue view [number] --json state
```

Verify those issues were auto-closed. If not, close them with reference to the PR:
```bash
gh issue close [number] --comment "Completed in PR #[pr-number]"
```

### Step 7: Update Parent Epic (if applicable)

If the merged PR was part of an epic:
1. Identify the parent epic from PR title or CHANGELOG
2. View current epic state:
   ```bash
   gh issue view [epic-number] --json body
   ```
3. Update epic body to:
   - Mark completed phase/work items as checked `[x]`
   - Update status (e.g., "Phase 1 Complete ✅")
   - Add implementation notes documenting what was delivered
   - Update any technical stack details based on actual implementation
   ```bash
   gh issue edit [epic-number] --body "$(cat <<'EOF'
   [Updated epic body with checked items and notes]
   EOF
   )"
   ```

### Step 8: Notify About Follow-ups

Check for:
- TODO comments added in this PR
- Issues created during development
- Deferred work mentioned in PR comments

Report any follow-up items to user.

### Step 9: Generate Summary

Output format:

```markdown
## Post-Merge Summary

**PR**: #[number] - [title]
**Merged**: [date/time]
**Version**: [version from CHANGELOG]

---

### Cleanup Completed

- [x/⏭️] Local branch: `[branch-name]` [deleted / kept per user request]
- [x] Remote branch pruned
- [x] CHANGELOG verified
- [x] Related issues closed
- [x] Parent epic updated (if applicable)

### Release Status

[✅ Release created via /release: v[version] / ⏭️ User declined release / ⏳ No release needed]

### Follow-up Items

| Type | Description | Action |
|------|-------------|--------|
| TODO | [location] - [text] | Create issue |
| Issue | #[number] | Needs attention |
| Deferred | [description] | Future PR |

### Next Steps

1. [Any remaining manual tasks]
2. [Suggested next issues to work on]
```

## Flags

- `--keep-branch`: Don't delete the local feature branch
- `--create-release`: Force creation of GitHub release
- `--no-release`: Skip release consideration
- `--verbose`: Show detailed git operations

## Notes

- Run this after confirming PR was merged successfully
- Safe to run multiple times (idempotent operations)
- Branch deletion is local only by default
- Release creation requires appropriate permissions
