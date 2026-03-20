# Backlog Review & Sprint Planning

Review open issues and plan next sprint. Combines light hygiene check with sprint option generation.

## Usage

```
/backlog [options]
```

**Options:**
- No args: Review all open issues, generate sprint options
- `--tool parley|radoub|manifest|quartermaster|fence`: Filter by tool
- `--full`: Include detailed grooming (label fixes, title standardization)
- `--refresh`: Force cache refresh before review
- `#N`: Focus on specific epic's issues

**Examples:**
- `/backlog` - Quick review + sprint options
- `/backlog --tool quartermaster` - Quartermaster issues only
- `/backlog #544` - Focus on epic #544
- `/backlog --full` - Include detailed grooming pass
- `/backlog --refresh` - Force fresh data from GitHub

## Upfront Questions

Collect in ONE interaction:

1. **Create Issues**: "After planning, create sprint issue on GitHub? [y/n/ask-after]"
2. **Project Board**: "Add created sprint to project board? [y/n]" (only if creating issues)

## Workflow

### Step 0: Ensure Cache is Fresh

```bash
# Auto-refresh if cache is stale (>1 hour) or missing
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
```

If `--refresh` flag is passed, force refresh:
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

### Step 1: Load Issues from Cache

- Don't over label.  3 to Five is usually sufficient.

Use the helper script to get a compact view (~25KB instead of 220KB):

```bash
# Get list view (no bodies) - filtered by tool if specified
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View list [-Tool parley]

# Or just summary stats
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View summary
```

The list view includes: number, title, updatedAt, author, labels (comma-separated).
Bodies are omitted to reduce context size - fetch individually if needed.

### Step 2: Quick Hygiene Summary

Count issues with problems (don't detail each one unless `--full`):

```markdown
## Hygiene Summary

- **Missing tool label**: 3 issues
- **Missing type label**: 2 issues
- **Stale (15+ days)**: 5 issues
- **Well-formed**: 18 issues

[Run `/backlog --full` for detailed fixes]
```

If `--full` flag: Include per-issue recommendations (label fixes, title standardization).

### Step 3: Categorize by Epic/Area

Group issues by:
- Epic label (if present)
- Tool label
- Type (bug, enhancement, tech-debt)

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

### Step 3.5: Languishing Issues Report

**MANDATORY** — before generating sprint options, identify issues that are being neglected.

An issue is **languishing** if:
- Open 30+ days with no activity (no comments, no PR, no label changes)
- Open 60+ days regardless of activity (**call out explicitly**)
- Tagged `bug` and open 14+ days
- Tagged `security` or `tech-debt` and open 21+ days

```markdown
## ⚠️ Languishing Issues

These issues need attention — age alone is a signal.

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

**Action Required**: At least ONE languishing issue must be included in a sprint option below, or the user must explicitly acknowledge and defer it.
```

If no issues are languishing, output: "No languishing issues. 👍"

### Step 4: Generate Sprint Options

**CRITICAL RULES:**

1. **No overlapping issues** — each issue appears in AT MOST ONE sprint option. If an issue fits multiple themes, pick the best fit. Never duplicate.

2. **Mandatory sprint categories** — always generate exactly 3 sprint options from these categories:

| Category | Description | When to Skip |
|----------|-------------|--------------|
| **Feature Sprint** | New functionality, enhancements | Only if zero feature/enhancement issues exist |
| **Bug/Fix Sprint** | Bug fixes, corrections | Only if zero bug issues exist |
| **Health Sprint** | Tech debt, security, testing, refactoring | NEVER skip — there is always health work to do |

3. **Anti-recency bias** — for each sprint option, at least ONE issue must be the **oldest qualifying issue** in that category. Do not exclusively pick recent issues. Sort candidates by age (oldest first) when selecting.

4. **Breadth over momentum** — resist grouping issues that were just worked on. If the last 3 sprints touched Quartermaster, prioritize other tools. If the last sprint was a feature, lean toward bug/health.

Present 3 sprint options (one per category):

```markdown
## Sprint Options

### Option A: Feature — [Theme]

**Issues:**
- #X - [title] (NN days old)
- #Y - [title] (NN days old)

**Oldest issue included**: #X (NN days)
**Rationale**: [why these fit together - shared code path, etc.]
**Complexity**: Small/Medium/Large

---

### Option B: Bug Fix — [Theme]

**Issues:**
- #X - [title] (NN days old)

**Oldest issue included**: #X (NN days)
**Rationale**: [why these fit together]
**Complexity**: Small/Medium/Large

---

### Option C: Health — [Theme]

**Issues:**
- #X - [title] (NN days old)
- #Y - [title] (NN days old)

**Oldest issue included**: #X (NN days)
**Rationale**: [tech debt / security / testing justification]
**Complexity**: Small/Medium/Large

---

## Recommendation

**Option [X]** recommended because:
1. [reason — include age/urgency justification]
2. [reason]

**Recent sprint history** (last 3):
- [tool] [type] — [date]
- [tool] [type] — [date]
- [tool] [type] — [date]

If the last 3 sprints were the same tool/type, explicitly recommend a different tool/type for balance.
```

### Step 5: Create Sprint Issue (if requested)

If user said yes to creating issues:

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

### Step 6: Add to Project Board (if requested)

Only for created sprint issues:

```bash
# Add to project
gh project item-add [PROJECT_NUMBER] --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json

# Set status to "In Progress" (see project IDs below)
```

**Project:** All tools use Radoub project (#3)

**Project IDs for status update:**
- Radoub: `--project-id PVT_kwHOAotjYs4BHbMq --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk --single-select-option-id 47fc9ee4`

## Output Format

```markdown
# Backlog Review

**Date**: YYYY-MM-DD
**Tool Filter**: [all/specific tool]
**Recent Sprint History**: [last 3 sprints: tool + type + date]

## Hygiene Summary

- **Missing tool label**: N issues
- **Missing type label**: N issues
- **Stale (15+ days)**: N issues
- **Well-formed**: N issues

[Run `/backlog --full` for detailed fixes]

---

## ⚠️ Languishing Issues

[Languishing report — see Step 3.5]

---

## By Epic

### Epic #N: [Name] (X issues)
| # | Title | Days | Priority |
|---|-------|------|----------|

### Uncategorized (X issues)
| # | Title | Days | Type |
|---|-------|------|------|

---

## Sprint Options

### Option A: Feature — [Theme]
...

### Option B: Bug Fix — [Theme]
...

### Option C: Health — [Theme]
...

---

## Recommendation

**Option [X]** recommended because:
...

---

## Next Steps

- To start selected sprint: `/init-item #[sprint-issue-number]`
- For detailed grooming: `/backlog --full`
```

## Save to File

Save output to `NonPublic/backlog.md` (clobber each run).

## Grouping Guidelines

**Good sprint combinations:**
- Feature + related tech debt in same area
- Bug fix + related cleanup
- Multiple small issues touching same code path
- Blocked issue + its blocker (clear the blocker first)

**Sprint sizing:**
- Small: 1-2 focused items, ~1 day
- Medium: 2-4 related items, ~2-3 days
- Large: 4+ items or complex feature, ~week

**HARD RULES (non-negotiable):**

1. **No issue overlap between sprint options** — an issue can appear in exactly ONE option
2. **3 categories always** — Feature, Bug Fix, Health (tech debt/security/testing)
3. **Oldest-first selection** — within each category, start candidate selection from the oldest issues
4. **Languishing report is mandatory** — never skip Step 3.5
5. **Include age in all issue listings** — always show "(NN days old)" next to each issue
6. **Recent sprint awareness** — check last 3 merged PRs for tool/type distribution; if skewed, recommend the underrepresented category

**Prioritization (within each sprint category):**
1. Blockers first (issues blocking other work)
2. Languishing issues (30+ days, called out in Step 3.5)
3. User-facing bugs over internal cleanup
4. Natural groupings over isolated tasks (same code path = same sprint)
5. Balance across tools (don't always sprint on the same tool)

**Anti-Patterns to AVOID:**
- Putting the same issue in multiple sprint options
- Always recommending the most recently active tool
- Ignoring issues older than 30 days
- Creating only feature sprints (health work always exists)
- Grouping by "momentum" instead of logical code area
- Never surfacing security or tech-debt issues
