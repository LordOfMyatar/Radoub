# Initialize Work Item

Start a new branch for any GitHub issue - automatically detects item type and applies appropriate workflow.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before initializing, collect these answers in ONE interaction (after fetching issue details):

1. **Tool Confirmation** (if ambiguous): "Which tool is this for? [parley/radoub/manifest/quartermaster/fence]"
2. **Epic Handling** (if epic detected): "This is an epic. Options: [run-research/continue-anyway/cancel]"

After collecting answers, proceed through all steps (branch, CHANGELOG, commit, PR, project board) without further prompts.

## Usage

```
/init-item #[issue-number]
```

**Examples:**
- `/init-item #37` (epic with multiple phases)
- `/init-item #134` (bug fix)
- `/init-item #200` (sprint with bundled work)
- `/init-item #45` (feature request)

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

### Step 3: Fetch Issue Details

```bash
gh issue view [number] --json title,labels,body,milestone
```

Extract:
- **Title**: For branch name and PR title
- **Labels**: To determine item type
- **Body**: For additional context
- **Milestone**: For version targeting

### Step 4: Determine Item Type and Branch Name

**Branch Naming**: Always use `[tool]/issue-[number]` format.
- Simple, predictable, easy to track
- Example: `parley/issue-708`, `radoub/issue-45`

Detect type from labels (in priority order) for PR title and CHANGELOG:

| Label Contains | Type |
|----------------|------|
| `epic` | Epic |
| `sprint` | Sprint |
| `bug`, `fix` | Fix |
| `refactor` | Refactor |
| `enhancement`, `feature` | Feature |
| (none of above) | Feature |

Determine tool from:
- Labels containing `parley`, `radoub`, or tool names
- Title prefix like `[Parley]`
- Default to `parley` if ambiguous (ask user to confirm)

### Step 5: Type-Specific Guidance

#### If Epic Detected

**STOP and inform user:**

> This is an **Epic** (#[number]: [title]).
>
> Epics require planning before implementation:
>
> 1. **Research first**: Run `/research #[number]` to investigate approaches
> 2. **Sprint planning**: Break epic into sprint-sized chunks
> 3. **Create sprint issues**: Each sprint should have its own issue
>
> Would you like me to:
> - [ ] Run `/research #[number]` now
> - [ ] Continue with epic branch anyway (for simple epics)
> - [ ] Cancel and plan first

If user chooses to continue, proceed with epic branch naming.

#### If Sprint Detected

Sprints assume **one PR/branch** for the bundled work:
- Branch: `[tool]/sprint/[milestone-or-name]`
- PR title: `[Tool] Sprint: [Title]`
- CHANGELOG: Group all sprint items under single version

#### If Fix Detected

Standard fix workflow:
- Branch: `[tool]/fix/[short-name]`
- PR title: `[Tool] Fix: [Title] (#[number])`

#### If Feature Detected

Standard feature workflow:
- Branch: `[tool]/feat/[short-name]`
- PR title: `[Tool] Feat: [Title] (#[number])`

### Step 6: Create Branch

Use the simple `[tool]/issue-[number]` format:

```bash
git checkout -b [tool]/issue-[number]
```

Example: `git checkout -b parley/issue-708`

### Step 7: Update CHANGELOG

Edit the appropriate CHANGELOG file:
- Parley changes: `Parley/CHANGELOG.md`
- Radoub/shared changes: `CHANGELOG.md`

Add new version section after `[Unreleased]`:

**For Sprint:**
```markdown
## [X.Y.Z-alpha] - YYYY-MM-DD
**Branch**: `[branch-name]` | **PR**: #TBD

### Sprint: [Sprint Title]

- Item 1 from sprint
- Item 2 from sprint

---
```

**For Epic/Feature/Fix:**
```markdown
## [X.Y.Z-alpha] - YYYY-MM-DD
**Branch**: `[branch-name]` | **PR**: #TBD

### [Type] [N]: [Title from GitHub]

---
```

**Use today's date** (not TBD) - most PRs merge same day.

### Step 8: Initial Commit

```bash
git add [CHANGELOG file]
git commit -m "[tool] chore: Initialize [type] branch for #[number]"
git push -u origin [branch-name]
```

### Step 9: Create Draft PR

```bash
gh pr create --draft --title "[Tool] [Type]: [Title]" --body "$(cat <<'EOF'
## Summary

[Brief description - will be updated]

## Related Issues

- Closes #[number]

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

### Step 10: Update CHANGELOG with PR Number

```bash
# Update PR: #TBD to actual PR number in CHANGELOG
git add [CHANGELOG file]
git commit -m "[tool] chore: Add PR number to CHANGELOG"
git push
```

### Step 11: Report Summary

Output format varies by type:

**For Epics:**
```markdown
## Epic Branch Initialized

**Branch**: [branch-name]
**PR**: #[pr-number] (draft)
**Issue**: #[issue-number]

### Reminder

This is an epic. Consider breaking into sprints if not already done.
Use `/research #[number]` for investigation tasks.

### Next Steps
1. Review epic scope and create sprint issues if needed
2. Implement phase by phase
3. Run `/pre-merge` before each merge
```

**For Sprints:**
```markdown
## Sprint Branch Initialized

**Branch**: [branch-name]
**PR**: #[pr-number] (draft)
**Issue**: #[issue-number]

### Sprint Scope

This branch covers all items in the sprint. Update CHANGELOG as you complete each item.

### Next Steps
1. Work through sprint items
2. Update CHANGELOG entries as completed
3. Run `/pre-merge` when sprint complete
```

**For Features/Fixes:**
```markdown
## [Type] Branch Initialized

**Branch**: [branch-name]
**PR**: #[pr-number] (draft)
**Issue**: #[issue-number]

### Next Steps
1. Implement the [type]
2. Add tests
3. Run `/pre-merge` to verify all checks pass
4. Mark PR ready for review
```

## Error Handling

- **Issue not found**: Error and exit - issue number is required
- **Dirty working directory**: Prompt user to commit/stash first
- **Branch already exists**: Ask if user wants to checkout existing or create new
- **Can't determine tool**: Ask user to specify (parley/radoub)
- **PR creation fails**: Provide manual command to run

## Label Detection Examples

```bash
# Get labels for detection logic
gh issue view 37 --json labels -q '.labels[].name'
```

Common label patterns:
- `epic`, `Epic`, `type:epic` → Epic
- `sprint`, `Sprint`, `type:sprint` → Sprint
- `bug`, `Bug`, `type:bug` → Fix
- `enhancement`, `feature`, `type:feature` → Feature
- `parley`, `Parley`, `tool:parley` → Tool detection
- `radoub`, `Radoub`, `tool:radoub` → Tool detection

## GitHub Project Integration

**Only add Sprints and Epics to projects** - individual features/fixes don't go on project boards unless explicitly requested.

### When to Add to Project

| Item Type | Add to Project? |
|-----------|-----------------|
| Epic | ✅ Yes |
| Sprint | ✅ Yes |
| Feature | ❌ No (unless solo work requested) |
| Fix | ❌ No (unless solo work requested) |

### Project Selection (for Sprints/Epics only)

| Label/Title | Project | Number |
|-------------|---------|--------|
| `parley` or `[Parley]` | Parley | 2 |
| `radoub` or `[Radoub]` | Radoub | 3 |
| `quartermaster` or `[Quartermaster]` | Radoub | 3 |
| `manifest` or `[Manifest]` | Radoub | 3 |
| `fence` or `[Fence]` | Radoub | 3 |

### Add Sprint/Epic to Project and Set In Progress

After Step 9 (Create Draft PR), **only for sprints and epics**:

```bash
# Add to appropriate project (returns item ID)
# For Parley sprints/epics:
ITEM_JSON=$(gh project item-add 2 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json)
ITEM_ID=$(echo "$ITEM_JSON" | jq -r '.id')

# For Radoub sprints/epics:
ITEM_JSON=$(gh project item-add 3 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json)
ITEM_ID=$(echo "$ITEM_JSON" | jq -r '.id')

# Set status to "In Progress"
# For Parley project (2):
gh project item-edit \
  --id "$ITEM_ID" \
  --project-id PVT_kwHOAotjYs4BHFCR \
  --field-id PVTSSF_lAHOAotjYs4BHFCRzg37-KA \
  --single-select-option-id 47fc9ee4

# For Radoub project (3):
gh project item-edit \
  --id "$ITEM_ID" \
  --project-id PVT_kwHOAotjYs4BHbMq \
  --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk \
  --single-select-option-id 47fc9ee4
```

### Updated Summary Output

For **Sprints/Epics**, include project status:

```markdown
## [Type] Branch Initialized

**Branch**: [branch-name]
**PR**: #[pr-number] (draft)
**Issue**: #[issue-number]
**Project**: [Project Name] - Status: In Progress

### Next Steps
...
```

For **Features/Fixes**, omit project line (not added to board).

### Prerequisites

Ensure `project` scope is available:
```bash
gh auth status  # Check for 'project' scope
gh auth refresh -s project  # Add if missing
```

See `.claude/github-projects-reference.md` for project IDs and field details.

## Notes

- Always use draft PRs initially
- CHANGELOG version should increment appropriately (check last version)
- For epics, encourage planning before diving into implementation
- For sprints, all work goes in one PR to keep changes atomic
- Issue number is required - this command won't work without it

