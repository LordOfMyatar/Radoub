# Issue Grooming

Review open issues for formatting, labeling, and relevance. Ensures issues stay current and properly organized.

## Usage

```
/grooming [options]
```

**Options:**
- No args: Review all open issues
- `#[number]`: Review specific issue
- `--tool parley|radoub`: Filter by tool
- `--label [name]`: Filter by label

**Examples:**
- `/grooming` - Review all open issues
- `/grooming #123` - Review specific issue
- `/grooming --tool parley` - Review Parley issues only

## Grooming Checklist

For each issue, verify:

### 1. Relevance Check

**Is this issue still valid?**

- Has the issue been addressed by a recent sprint, refactor, or tech debt cleanup?
- Check recent PRs/commits that might have fixed it incidentally
- Check if referenced code/features still exist

```bash
# Check recent commits mentioning the issue
git log --oneline --all --grep="#[number]" | head -10

# Search for related keywords in recent commits
git log --oneline -20 --all | head -20

# Check CHANGELOG for related fixes
# Parley: Parley/CHANGELOG.md
# Radoub: CHANGELOG.md
```

**Actions:**
- If fixed: Close with comment explaining which PR/commit resolved it
- If obsolete: Close with `wontfix` or `invalid` label
- If still relevant: Continue to next checks

### 2. Title Standards

**Format:** `[Tool] Type: Brief description`

| Type | When to Use |
|------|-------------|
| `Epic:` | Multi-sprint tracking issues |
| `Sprint:` | Bundled work packages |
| `Fix:` | Bug fixes |
| `Feat:` | New features (optional, can omit for enhancements) |
| (none) | Simple enhancements/requests |

**Tool Prefixes:**
- `[Parley]` - Dialog editor issues
- `[Radoub]` - Cross-tool/infrastructure issues
- `[Radoub.Formats]` - Shared library issues

**Examples:**
- ✅ `[Parley] Fix: Remove node from scrap on undo delete`
- ✅ `[Parley] Epic: Complete Core Plugin APIs`
- ✅ `[Parley] Empty terminal nodes should display [END DIALOG]`
- ❌ `Bug in dialog parser` (missing tool prefix)
- ❌ `[Parley] [Bug] Parser issue` (redundant brackets)

**Fix Title:**
```bash
gh issue edit [number] --title "[Tool] Type: Description"
```

### 3. Label Validation

**Required Labels:**

| Category | Labels | Required? |
|----------|--------|-----------|
| Tool | `parley`, `radoub` | Yes |
| Type | `bug`, `enhancement`, `epic`, `sprint`, `tech-debt`, `documentation` | Yes (one) |
| Priority | `priority-high`, `priority-medium`, `priority-low` | Recommended |
| Area | `ui`, `ux`, `performance`, `plugin`, `testing`, etc. | Optional |

**Check Current Labels:**
```bash
gh issue view [number] --json labels -q '.labels[].name'
```

**Add Missing Labels:**
```bash
gh issue edit [number] --add-label "parley,enhancement,priority-medium"
```

### 4. Epic/Sprint Association

**Should this issue belong to an existing epic or sprint?**

**Open Epics:**
```bash
gh issue list --state open --label epic --json number,title
```

**Open Sprints:**
```bash
gh issue list --state open --label sprint --json number,title
```

**Link to Epic/Sprint:**
- Reference in issue body: "Part of #[epic-number]"
- Use GitHub's "Development" section to link
- Add `epic` or `sprint` label to the parent tracking issue

### 5. Body Content Check

**Issue body should include:**
- Clear description of the problem or feature
- Steps to reproduce (for bugs)
- Expected vs actual behavior (for bugs)
- Acceptance criteria (for features)
- Screenshots if UI-related

**If body is sparse:**
- If issue author is `LordOfMyatar`: Ask during session for clarification
- If issue author is someone else: Propose adding a comment starting with "Lord and Claude ask..." or "Lord and Claude comment..."

## Batch Grooming Workflow

When running `/grooming` without args:

### Step 1: Fetch All Open Issues

```bash
gh issue list --state open --limit 100 --json number,title,labels,updatedAt,body
```

### Step 2: Categorize Issues

Group by status:
- **Missing tool label**: No `parley` or `radoub`
- **Missing type label**: No `bug`, `enhancement`, `epic`, etc.
- **Stale**: Not updated in 15+ days
- **Unlabeled**: No labels at all
- **Well-formed**: Passes all checks

### Step 3: Present Summary

```markdown
## Grooming Summary

### Issues Needing Attention

| # | Title | Issues |
|---|-------|--------|
| 123 | [Title] | Missing tool label, no priority |
| 456 | [Title] | Stale (180 days), may be resolved |

### Potentially Resolved

These issues may have been fixed by recent work:
- #789 - [Title] - Possibly fixed by PR #XXX

### Well-Formed Issues

[X] issues pass all grooming checks.
```

### Step 4: Interactive Review

For each issue needing attention:

1. Show current state
2. Propose fixes
3. Ask user to confirm before making changes

```markdown
### Issue #123: [Title]

**Current State:**
- Labels: enhancement
- Missing: tool label, priority

**Proposed Changes:**
- Add label: `parley` (based on title prefix)
- Add label: `priority-low` (minor enhancement)

Apply changes? [y/n/skip/edit]
```

## Single Issue Review

When running `/grooming #[number]`:

### Full Analysis Output

```markdown
## Issue #[number] Review

### Current State

| Field | Value |
|-------|-------|
| Title | [current title] |
| Labels | [list] |
| Created | [date] |
| Updated | [date] |
| Body | [preview] |

### Checklist

- [ ] Title follows `[Tool] Type: Description` format
- [ ] Has tool label (parley/radoub)
- [ ] Has type label (bug/enhancement/etc)
- [ ] Has priority label
- [ ] Body has sufficient detail
- [ ] Not stale (updated within 90 days)
- [ ] Not accidentally resolved

### Relevance Check

[Analysis of whether issue is still valid based on recent commits/PRs]

### Recommendations

1. [Specific recommendation]
2. [Specific recommendation]

### Proposed Commands

```bash
gh issue edit [number] --title "[New Title]"
gh issue edit [number] --add-label "label1,label2"
```

Apply recommendations? [y/n/manual]
```

## Label Reference

### Tool Labels
- `parley` - Dialog editor
- `radoub` - Cross-tool infrastructure

### Type Labels
- `bug` - Something isn't working
- `enhancement` - New feature or request
- `epic` - Multi-sprint tracking issue
- `sprint` - Bundled work package
- `tech-debt` - Code quality improvements
- `documentation` - Docs improvements
- `refactor` - Code reorganization

### Priority Labels
- `priority-high` - Blocking or critical
- `priority-medium` - Impairing, workarounds exist
- `priority-low` - Minor, cosmetic

### Status Labels
- `blocked` - External dependency
- `deferred` - Postponed
- `duplicate` - Already exists
- `wontfix` - Won't be addressed
- `known-issue` - Documented limitation

### Area Labels
- `ui` - User interface
- `ux` - User experience
- `performance` - Optimization
- `plugin` - Plugin system
- `testing` - Test framework
- `security` - Security concerns
- `accessibility` - A11y features
- `theme` - Visual styling
- `aurora-compatibility` - Aurora Toolset compat

## GitHub Project Integration

**Only add Sprints and Epics to projects** - individual features/fixes don't go on project boards.

### When to Add to Project

| Issue Type | Add to Project? |
|------------|-----------------|
| Epic (`epic` label) | ✅ Yes |
| Sprint (`sprint` label) | ✅ Yes |
| Bug/Enhancement/Feature | ❌ No |

### Project Selection (for Sprints/Epics only)

| Label/Title | Project | Number |
|-------------|---------|--------|
| `parley` or `[Parley]` | Parley | 2 |
| `radoub` or `[Radoub]` | Radoub | 3 |

### Add Sprint/Epic to Project

```bash
# Add to Parley project (if parley label)
gh project item-add 2 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json

# Add to Radoub project (if radoub label)
gh project item-add 3 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json
```

### After Adding to Project

Report to user:
```
✅ Added #[number] to [Project Name] project
   URL: https://github.com/users/LordOfMyatar/projects/[N]
```

### Batch Grooming Project Updates

When grooming multiple issues, only track sprints/epics added:

```markdown
## Project Board Updates

| Issue | Type | Project | Status |
|-------|------|---------|--------|
| #123 | Sprint | Parley | ✅ Added |
| #456 | Epic | Radoub | ✅ Added |
| #789 | Bug | - | (not added - individual issue) |
```

### Prerequisites

Ensure `project` scope is available:
```bash
gh auth status  # Check for 'project' scope
gh auth refresh -s project  # Add if missing
```

See `.claude/github-projects-reference.md` for project IDs and field details.

## Notes

- Always ask before making changes in batch mode
- Preserve user's original intent when editing titles
- Don't over-label - 3-5 labels is usually sufficient
- Check PR history before closing as "resolved"
- Check CHANGELOG entries - issues may have been fixed and documented there
- Link related issues when discovered during grooming
- For `LordOfMyatar` issues: Ask questions directly in session
- For external contributor issues: Propose comments prefixed with "Lord and Claude ask/comment..."
