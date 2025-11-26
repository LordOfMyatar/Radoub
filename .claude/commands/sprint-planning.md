# Sprint Planning Agent

Help plan the next sprint by reviewing open issues, PRs, and current work.

## Instructions

1. **Check Current State**
   - Run `git status` to see current branch and changes
   - Run `git log --oneline -5` to see recent commits
   - Check for any uncommitted work

2. **Review GitHub Issues**
   - List open issues: `gh issue list --state open --limit 20`
   - Check for priority labels (P0, P1, P2)
   - Note any blocked issues

3. **Review Open PRs**
   - List open PRs: `gh pr list --state open`
   - Check PR status and review state

4. **Check Active Sprints/Epics**
   - Read `Parley/CHANGELOG.md` for active work
   - Identify any [Unreleased] items
   - Note deferred items and their blockers

5. **Summarize Sprint Options**
   Present a summary with:
   - **In Progress**: Any active branches/PRs that need completion
   - **Ready to Start**: Issues with no blockers, sorted by priority
   - **Blocked**: Issues waiting on dependencies (note what blocks them)
   - **Tech Debt**: Cleanup/refactoring opportunities
   - **Research**: Items needing investigation before implementation

6. **Recommend Next Sprint**
   Based on priorities and dependencies, suggest:
   - Which issue(s) to tackle next
   - Estimated complexity (small/medium/large)
   - Any prep work needed

## Output Format

```
## Sprint Planning Summary

### Current State
- Branch: [current branch]
- Status: [clean/dirty]
- Last commit: [summary]

### In Progress
- [ ] PR #X - [title] - [status]

### Ready to Start (by priority)
1. **P0**: #X - [title] - [complexity]
2. **P1**: #X - [title] - [complexity]
...

### Blocked
- #X - [title] - Blocked by: [reason]

### Tech Debt
- #X - [title]

### Recommendation
[Your recommendation for next sprint focus]
```
