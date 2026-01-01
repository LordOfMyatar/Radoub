# Sprint Planning Agent

Help plan the next sprint by reviewing open issues, PRs, and current work.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before analyzing issues, collect these answers in ONE interaction:

1. **Tool Focus** (optional): "Focus on a specific tool? [all/parley/radoub/manifest/quartermaster]"
2. **Create Issues**: "After planning, auto-create sprint issues on GitHub?" [y/n/ask-after]"
3. **Project Board**: "Add created sprints to project boards?" [y/n]"

After collecting answers, proceed through all analysis and output without further prompts. If "ask-after" selected for issue creation, prompt once at the end.

## Grouping Methodology

**Epics**: Large feature areas numbered 0-N
- Epics 3-7 represent idealized order but don't always pan out in practice
- Issues should be tagged with their epic when applicable
- Sprints can pull from multiple epics based on what fits together

**Sprint Composition**: Group by natural fit
- What features + cleanup work well together?
- Look for related issues that share code paths or concepts
- Combine a feature with related tech debt when it makes sense
- Small fixes that touch the same area as a feature = good sprint combo

## Prioritization Tactics

1. **Blockers First**: Issues blocking other work get priority
2. **Natural Groupings**: Features + related cleanup > isolated tasks
3. **User-Facing vs Infrastructure**: Balance visible progress with foundation work
4. **Dependency Chains**: Clear blockers before blocked items
5. **Sprint Size**: Aim for achievable scope (1-3 main items + related cleanup)

## Instructions

1. **Check Current State**
   - Run `git status` to see current branch and changes
   - Run `git log --oneline -5` to see recent commits
   - Check for any uncommitted work

2. **Review GitHub Issues**
   - List open issues with age: `gh issue list --state open --limit 30 --json number,title,labels,updatedAt`
   - Calculate days since last update for each issue
   - Flag stale issues (15+ days without activity)
   - Group by epic label when present
   - Note blocked/blocking relationships

3. **Review Open PRs**
   - List open PRs: `gh pr list --state open`
   - Check PR status and review state

4. **Check Active Sprints/Epics**
   - Read `Parley/CHANGELOG.md` for active work
   - Identify any [Unreleased] items
   - Note deferred items and their blockers

5. **Identify Natural Groupings**
   Look for issues that fit together:
   - Same feature area or code path
   - Feature + related tech debt
   - Blocked item + its blocker
   - Quick wins in the same neighborhood

6. **Recommend Sprint Options**
   Present 2-3 sprint options with rationale:
   - What's the theme/focus?
   - Why do these items fit together?
   - What's the complexity estimate?

## Output Format

The sprint planning output should be displayed to the user AND saved to `NonPublic/sprintplanning.md` (clobber/rebuild each time).

```markdown
# Sprint Planning Summary

**Date**: [YYYY-MM-DD]

## Current State
- **Branch**: [current branch]
- **Status**: [Clean/Dirty]
- **Last commit**: `[hash]` - [message]

## In Progress
- [PR #X - [title] - [status] | No open PRs]

---

## By Epic

### Epic #N: [Name] - Priority: [High/Medium/Low]

| Issue | Title | Days | Priority |
|-------|-------|------|----------|
| #X | [title] | N | [priority] |

### [Category Name] - Priority: [priority]

| Issue | Title | Days |
|-------|-------|------|
| #X | [title] | N |

---

## Blocked Items

| Issue | Title | Blocked By |
|-------|-------|------------|
| #X | [title] | [reason] |

---

## Stale Issues (15+ days)

| Issue | Title | Days |
|-------|-------|------|
| #X | [title] | N |

[Commentary on backlog freshness]

---

## Sprint Options

### Option A: [Theme]

- #X - [title]
- #Y - [title]

**Rationale**: [why these fit together]

**Complexity**: [Small/Medium/Large]

---

### Option B: [Theme]

- #X - [title]
- #Y - [title]

**Rationale**: [why these fit together]

**Complexity**: [Small/Medium/Large]

---

## Recommendation

**Option [X]: [Theme]** is the recommended next step:

1. [Reason 1]
2. [Reason 2]

[Alternative recommendation if applicable]
```

## Save to File

**IMPORTANT**: After generating the sprint planning output, save it to:

```
NonPublic/sprintplanning.md
```

This file is clobbered each time sprint planning runs. It serves as a snapshot for reference between planning sessions, so the user doesn't need to run sprint planning as often.

## Creating Sprint Issues

After planning is complete, ask the user if they want to create GitHub issues for the selected sprint(s).

### When to Create Sprint Issues

- User selects a sprint option to proceed with
- User explicitly requests sprint issue creation
- User says "create issues", "make tickets", or similar

### Sprint Issue Creation Process

**Step 1: Confirm with User**

```
Would you like me to create GitHub issues for this sprint?

Sprint: [Theme]
Issues to include: #X, #Y, #Z
Parent Epic: #N (if applicable)

[y/n]
```

**Step 2: Create Sprint Tracking Issue**

```bash
gh issue create \
  --title "[Tool] Sprint: [Theme Name]" \
  --label "[tool],sprint" \
  --body "$(cat <<'EOF'
## Sprint: [Theme Name]

**Parent Epic**: #[epic-number] (if applicable)
**Estimated Complexity**: [small/medium/large]

## Work Items

- [ ] #X - [title]
- [ ] #Y - [title]
- [ ] #Z - [title]

## Sprint Goals

[Brief description of what this sprint accomplishes]

## Acceptance Criteria

- [ ] All work items completed
- [ ] Tests passing
- [ ] CHANGELOG updated

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Step 3: Report Created Issue**

```
✅ Created sprint issue: #[new-issue-number]
   Title: [Tool] Sprint: [Theme Name]
   URL: https://github.com/[owner]/[repo]/issues/[number]

Next steps:
- Run `/init-item #[new-issue-number]` to start the sprint branch
- Or continue planning other sprints
```

### Multiple Sprint Creation

If user wants to create issues for multiple sprint options:

```
Creating sprint issues...

✅ Sprint A: #[number] - [Theme A]
✅ Sprint B: #[number] - [Theme B]

All sprint issues created. Use `/init-item #[number]` to start work on a sprint.
```

### Linking to Epics

When a sprint relates to an epic:
- Reference the epic in the sprint body: `**Parent Epic**: #[epic-number]`
- The epic's work items list can be updated to reference the sprint

### Labels

Sprint issues automatically get:
- Tool label (`parley` or `radoub`)
- `sprint` label

### Example Sprint Issue

```markdown
## Sprint: Search & Replace Foundation

**Parent Epic**: #42
**Estimated Complexity**: medium

## Work Items

- [ ] #68 - Multi-file search across module dialogs
- [ ] #69 - Advanced replace functionality

## Sprint Goals

Implement cross-dialog search and replace with regex support for large module maintenance.

## Acceptance Criteria

- [ ] All work items completed
- [ ] Tests passing
- [ ] CHANGELOG updated

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

## GitHub Project Integration

After creating a sprint issue, add it to the appropriate project board.

### Project Selection

| Label/Title | Project | Number |
|-------------|---------|--------|
| `parley` or `[Parley]` | Parley | 2 |
| `radoub` or `[Radoub]` | Radoub | 3 |
| `quartermaster` or `[Quartermaster]` | Radoub | 3 |
| `manifest` or `[Manifest]` | Radoub | 3 |

### Add Sprint to Project and Set In Progress

After creating the sprint issue:

```bash
# Add to project (returns item ID)
ITEM_JSON=$(gh project item-add [PROJECT_NUMBER] --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[NEW_ISSUE_NUMBER] --format json)
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

### Updated Report Format

```
✅ Created sprint issue: #[new-issue-number]
   Title: [Tool] Sprint: [Theme Name]
   URL: https://github.com/LordOfMyatar/Radoub/issues/[number]
   Project: [Project Name] - Status: In Progress
```

### Prerequisites

Ensure `project` scope is available:
```bash
gh auth status  # Check for 'project' scope
gh auth refresh -s project  # Add if missing
```

See `.claude/github-projects-reference.md` for project IDs and field details.
