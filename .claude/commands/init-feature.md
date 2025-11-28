# Initialize New Feature Branch

Automate the workflow for starting a new feature, epic, or fix branch.

## Usage

```
/init-feature [tool] [type] [number] [short-name]
```

**Examples:**
- `/init-feature parley epic 37 sound-browser`
- `/init-feature parley fix 134 focus-management`
- `/init-feature radoub feat formats-library`

## Arguments

- **tool**: `parley`, `radoub`, or future tool names
- **type**: `epic`, `feat`, or `fix`
- **number**: GitHub issue/epic number (optional for standalone features)
- **short-name**: Brief kebab-case description

## Workflow

### Step 1: Validate Current State

```bash
git status
git fetch origin
```

- Ensure working directory is clean
- If dirty, ask user to commit or stash changes first

### Step 2: Sync with Main

```bash
git checkout main
git pull origin main
```

### Step 3: Create Feature Branch

Branch naming convention:
- Epic: `[tool]/feat/epic-[N]-[short-name]`
- Feature: `[tool]/feat/[short-name]`
- Fix: `[tool]/fix/[short-name]`

```bash
git checkout -b [branch-name]
```

### Step 4: Update CHANGELOG

Edit the appropriate CHANGELOG file:
- Parley changes: `Parley/CHANGELOG.md`
- Radoub/shared changes: `CHANGELOG.md`

Add new version section after `[Unreleased]`:

```markdown
## [X.Y.Z-alpha] - YYYY-MM-DD
**Branch**: `[branch-name]` | **PR**: #TBD

### [Epic/Feature/Fix] [N]: [Title from GitHub]

---
```

**Use today's date** (not TBD) - most PRs merge same day. Get today's date:
```bash
date +%Y-%m-%d
```

Get the issue/epic title from GitHub:
```bash
gh issue view [number] --json title -q '.title'
```

### Step 5: Initial Commit

```bash
git add [CHANGELOG file]
git commit -m "[tool] chore: Initialize [type] branch for #[number]"
git push -u origin [branch-name]
```

### Step 6: Create Draft PR

```bash
gh pr create --draft --title "[Tool] [Type]: [Title]" --body "$(cat <<'EOF'
## Summary

[Brief description - will be updated]

## Related Issues

- Closes #[number] (if applicable)
- Relates to #[number] (if applicable)

## Checklist

- [ ] Implementation complete
- [ ] Tests added/updated
- [ ] CHANGELOG updated with date
- [ ] Documentation updated (if needed)

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### Step 7: Update CHANGELOG with PR Number

Get the PR number from the create output, then:

```bash
# Update PR: #TBD to actual PR number in CHANGELOG
git add [CHANGELOG file]
git commit -m "[tool] chore: Add PR number to CHANGELOG"
git push
```

### Step 8: Report Summary

Output to user:
```
## Feature Branch Initialized

**Branch**: [branch-name]
**PR**: #[number] (draft)
**Issue**: #[issue-number]

### Next Steps
1. Implement the feature
2. Add tests
3. Run `/pre-merge` to verify all checks pass
4. Mark PR ready for review

### Useful Commands
- View PR: `gh pr view [number] --web`
- View issue: `gh issue view [issue-number] --web`
```

## Error Handling

- **Dirty working directory**: Prompt user to commit/stash first
- **Branch already exists**: Ask if user wants to checkout existing or create new
- **Issue not found**: Proceed without issue title, warn user
- **PR creation fails**: Provide manual command to run

## Notes

- Always use draft PRs initially
- CHANGELOG version should increment appropriately (check last version)
- For epics, the title format is "Epic N: [Name]"
- For fixes, the title format is "Fix: [Description]"
