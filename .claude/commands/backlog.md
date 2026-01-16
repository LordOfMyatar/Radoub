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
- `#N`: Focus on specific epic's issues

**Examples:**
- `/backlog` - Quick review + sprint options
- `/backlog --tool quartermaster` - Quartermaster issues only
- `/backlog #544` - Focus on epic #544
- `/backlog --full` - Include detailed grooming pass

## Upfront Questions

Collect in ONE interaction:

1. **Create Issues**: "After planning, create sprint issue on GitHub? [y/n/ask-after]"
2. **Project Board**: "Add created sprint to project board? [y/n]" (only if creating issues)

## Workflow

### Step 1: Fetch Issues (Single Query)

```bash
gh issue list --state open --limit 100 --json number,title,labels,updatedAt,body,milestone
```

Filter by tool label if `--tool` specified.

### Step 2: Quick Hygiene Summary

Count issues with problems (don't detail each one unless `--full`):

```markdown
## Hygiene Summary

- **Missing tool label**: 3 issues
- **Missing type label**: 2 issues
- **Stale (15+ days)**: 5 issues
- **Well-formed**: 18 issues

[Run `/backlog --full` or `/grooming` for detailed fixes]
```

If `--full` flag: Include per-issue recommendations like `/grooming` does.

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

### Step 4: Generate Sprint Options

Present 2-3 sprint options based on natural groupings:

```markdown
## Sprint Options

### Option A: [Theme]

**Issues:**
- #X - [title]
- #Y - [title]

**Rationale**: [why these fit together - shared code path, feature + cleanup, etc.]

**Complexity**: Small/Medium/Large

---

### Option B: [Theme]

**Issues:**
- #X - [title]
- #Y - [title]

**Rationale**: [why these fit together]

**Complexity**: Small/Medium/Large

---

## Recommendation

**Option [X]** recommended because:
1. [reason]
2. [reason]
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

**Project Selection:**
| Tool | Project | Number |
|------|---------|--------|
| parley | Parley | 2 |
| radoub, quartermaster, manifest, fence | Radoub | 3 |

**Project IDs for status update:**
- Parley: `--project-id PVT_kwHOAotjYs4BHFCR --field-id PVTSSF_lAHOAotjYs4BHFCRzg37-KA --single-select-option-id 47fc9ee4`
- Radoub: `--project-id PVT_kwHOAotjYs4BHbMq --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk --single-select-option-id 47fc9ee4`

## Output Format

```markdown
# Backlog Review

**Date**: YYYY-MM-DD
**Tool Filter**: [all/specific tool]

## Hygiene Summary

- **Missing tool label**: N issues
- **Missing type label**: N issues
- **Stale (15+ days)**: N issues
- **Well-formed**: N issues

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

### Option A: [Theme]
...

### Option B: [Theme]
...

---

## Recommendation

**Option [X]** recommended because:
...

---

## Next Steps

- To start selected sprint: `/init-item #[sprint-issue-number]`
- For detailed grooming: `/grooming` or `/backlog --full`
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

**Prioritization:**
1. Blockers first (issues blocking other work)
2. User-facing bugs over internal cleanup
3. Aging issues
4. Natural groupings over isolated tasks
5. Balance visible progress with foundation work
