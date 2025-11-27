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
