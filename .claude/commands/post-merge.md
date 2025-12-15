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

### Step 6: Verify Related Issues

**Extract issues referenced in PR body:**
```bash
# Find "closes #X", "fixes #X", or "resolves #X" in PR body
gh pr view [number] --json body -q '.body' | grep -oEi "(closes|fixes|resolves) #[0-9]+" | grep -oE "[0-9]+"
```

**For each issue number found, check its state:**
```bash
gh issue view [issue-number] --json state,title -q '{state: .state, title: .title}'
```

**Report status to user:**
- If CLOSED: ✅ Issue was auto-closed by merge
- If OPEN: ⚠️ Issue is still open

**If issues are still OPEN, ask user:**
> "Issue #[number] '[title]' is still open. Would you like me to close it with a comment referencing PR #[pr-number]?"

Only close if user confirms:
```bash
gh issue close [issue-number] --comment "Completed in PR #[pr-number]"
```

**Note**: Don't auto-close issues. They may be intentionally left open for deferred work or have multiple parts.

### Step 7: Update Parent Epic (if applicable)

**Extract parent epic number from PR body:**
```bash
# Look for "Parent Epic": #XXX or "Epic": #XXX patterns
gh pr view [number] --json body -q '.body' | grep -oEi "(parent )?epic[^#]*#[0-9]+" | grep -oE "[0-9]+" | head -1
```

If an epic number is found:

1. **View current epic state:**
   ```bash
   gh issue view [epic-number] --json body,title,state -q '{title: .title, state: .state}'
   ```

2. **Ask user about epic update:**
   > "Found parent epic #[number]: '[title]'. Would you like me to:
   > - Add a completion comment for this sprint?
   > - Update the epic checklist (if applicable)?"

3. **If user confirms comment**, add completion note:
   ```bash
   gh issue comment [epic-number] --body "Sprint completed via PR #[pr-number]: [PR title]"
   ```

4. **If user wants checklist updates**, read the epic body and:
   - Mark completed sprint/phase as `[x]`
   - Update status sections
   - Add implementation notes for what was delivered
   ```bash
   gh issue edit [epic-number] --body "$(cat <<'EOF'
   [Updated epic body with checked items and notes]
   EOF
   )"
   ```

**Note**: Always ask before modifying the epic. Some sprints may be partial completions or the user may prefer to batch updates.

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
