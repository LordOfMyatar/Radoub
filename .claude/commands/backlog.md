# Backlog Review & Sprint Planning

Review open issues and plan the next sprint. Light hygiene check plus sprint options.

## Usage

```
/backlog [--tool <name>] [--full] [--refresh] [#N]
```

| Option | Effect |
|--------|--------|
| (none) | Review all open issues, generate sprint options |
| `--tool <name>` | Filter to one tool |
| `--full` | Add detailed grooming — label fixes, title standardization |
| `--refresh` | Force a cache refresh first |
| `#N` | Focus on one epic's issues |

Ask both up front, in one interaction: create the sprint issue on GitHub afterward
(`y/n/ask-after`), and if so, add it to the project board.

## Phase 1 — Load

### 1.1 Refresh the cache

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" [-Force]
```

Auto-refreshes when stale (over an hour). `--refresh` forces it.

### 1.2 Read issues

The `backlog` view is sorted **oldest-first**, which is what the anti-recency rules below
need. Output is `#NNNN  <age>d  <title>` plus labels.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View backlog [-Tool parley] [-Label "tech-debt|refactor"] [-Query "<title-regex>"]
```

Other views: `-View list` for a compact dump (~25KB versus 220KB — number, title, updatedAt,
author, labels, no bodies), `-View summary` for counts alone, `-View search -Query` for
title-and-body regex. Fetch bodies individually only when needed.

## Phase 2 — Assess

### 2.1 Hygiene summary

Counts only, unless `--full`:

```markdown
## Hygiene Summary

- **Missing tool label**: 3 issues
- **Missing type label**: 2 issues
- **Stale (15+ days)**: 5 issues
- **Well-formed**: 18 issues

[Run `/backlog --full` for detailed fixes]
```

Keep labels sparse — three to five per issue is plenty.

### 2.2 Group by epic and area

Group by epic label, then tool, then type (bug, enhancement, tech-debt):

```markdown
## By Epic

### Epic #544: Quartermaster Core (4 issues)
| # | Title | Days | Priority |
|---|-------|------|----------|
| #686 | Extract common panel patterns | 12 | medium |

### Uncategorized (6 issues)
| # | Title | Days | Type |
|---|-------|------|------|
| #123 | Fix button alignment | 3 | bug |
```

### 2.3 Languishing issues (MANDATORY)

Never skip this. Age alone is a signal.

| Condition | Threshold |
|-----------|-----------|
| No activity — no comments, PR, or label changes | 30+ days |
| Any state, called out explicitly | 60+ days |
| Tagged `bug` | 14+ days |
| Tagged `security` or `tech-debt` | 21+ days |

```markdown
## ⚠️ Languishing Issues

### Critical (60+ days)
| # | Title | Age | Type | Last Activity |
|---|-------|-----|------|---------------|
| #123 | [title] | 72 days | bug | 2025-12-01 |

### Aging (30-59 days)
| # | Title | Age | Type | Last Activity |
|---|-------|-----|------|---------------|

### Overdue Bugs (14+ days)
| # | Title | Age | Assigned? |
|---|-------|-----|-----------|
```

At least one languishing issue must appear in a sprint option below, or the user must
explicitly defer it. When none qualify, say "No languishing issues. 👍".

## Phase 3 — Plan

### 3.1 Generate three sprint options

Always exactly three, one per category:

| Category | Covers | Skip only when |
|----------|--------|----------------|
| Feature | New functionality, enhancements | No feature or enhancement issues exist |
| Bug Fix | Bugs, corrections | No bug issues exist |
| Health | Tech debt, security, testing, refactoring | Never — health work always exists |

Four rules govern selection:

1. **No overlap.** An issue appears in at most one option. If it fits two themes, pick the
   better fit.
2. **Oldest-first.** Each option must include the oldest qualifying issue in its category.
   Sort candidates by age before selecting.
3. **Breadth over momentum.** If the last three sprints hit Quartermaster, favor another tool.
   If the last was a feature, lean bug or health.
4. **Show age everywhere.** Every issue listing carries `(NN days old)`.

Within a category, prioritize blockers, then languishing issues, then user-facing bugs over
internal cleanup, then natural groupings (same code path), then tool balance.

```markdown
## Sprint Options

### Option A: Feature — [Theme]

**Issues:**
- #X - [title] (NN days old)
- #Y - [title] (NN days old)

**Oldest issue included**: #X (NN days)
**Rationale**: [why these fit — shared code path, etc.]
**Complexity**: Small/Medium/Large

### Option B: Bug Fix — [Theme]
...

### Option C: Health — [Theme]
...

## Recommendation

**Option [X]** because:
1. [reason, including age or urgency]
2. [reason]

**Recent sprint history** (last 3):
- [tool] [type] — [date]
```

When the last three sprints share a tool or type, recommend a different one and say why.

Sizing: Small is 1–2 focused items (~1 day), Medium is 2–4 related items (~2–3 days), Large
is 4+ or a complex feature (~a week). Good pairings include a feature with its related tech
debt, a bug with its cleanup, several small issues on one code path, or a blocked issue
alongside its blocker.

Avoid: the same issue in two options, always recommending the most recently active tool,
ignoring anything over 30 days, feature-only slates, grouping by momentum rather than code
area, and never surfacing security or tech-debt work.

## Phase 4 — Create (if requested)

### 4.1 Sprint issue

```bash
gh issue create \
  --title "[Tool] Sprint: [Theme Name]" \
  --label "[tool],sprint" \
  --body "$(cat <<'EOF'
## Sprint: [Theme Name]

**Parent Epic**: #[epic-number] (if applicable)
**Estimated Complexity**: [Small/Medium/Large]

## Work Items

- [ ] #X - [title]
- [ ] #Y - [title]

## Sprint Goals

[What this sprint accomplishes]

## Acceptance Criteria

- [ ] All work items completed
- [ ] Tests passing
- [ ] CHANGELOG updated

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### 4.2 Project board

```bash
gh project item-add 3 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json

gh project item-edit --id "[item-id]" \
  --project-id PVT_kwHOAotjYs4BHbMq \
  --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk \
  --single-select-option-id 47fc9ee4
```

All tools use Radoub project #3.

## Output

Write the full review to `NonPublic/backlog.md`, clobbering each run. Structure: header with
date, tool filter, and recent sprint history; then Hygiene Summary, Languishing Issues, By
Epic, Sprint Options, Recommendation, and Next Steps.

Next steps read:

```markdown
- To start the selected sprint: `/init-item #[sprint-issue-number]`
- For detailed grooming: `/backlog --full`
```
