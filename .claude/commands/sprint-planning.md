# Sprint Planning Agent

Help plan the next sprint by reviewing open issues, PRs, and current work.

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
   - List open issues: `gh issue list --state open --limit 30`
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

```
## Sprint Planning Summary

### Current State
- Branch: [current branch]
- Status: [clean/dirty]
- Last commit: [summary]

### In Progress
- [ ] PR #X - [title] - [status]

### By Epic
**Epic N: [Name]**
- #X - [title] - [blocked?]
- #X - [title]

**Standalone/Tech Debt**
- #X - [title]

### Blocked Items
- #X - [title] - Blocked by: #Y or [reason]

### Sprint Options

**Option A: [Theme]**
- #X, #Y, #Z
- Rationale: [why these fit together]
- Complexity: [small/medium/large]

**Option B: [Theme]**
- #X, #Y
- Rationale: [why these fit together]
- Complexity: [small/medium/large]

### Recommendation
[Which option and why]
```

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
