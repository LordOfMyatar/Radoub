# Pre-Warm Next Sprint

Prepare an implementation plan for an upcoming sprint or issue while the current sprint is still in progress. **Planning only** — no branches, PRs, or code changes.

## Usage

```
/pre-warm #[issue-number]
```

**Examples:**
- `/pre-warm #1901` (plan a sprint before it starts)
- `/pre-warm #2050` (plan an epic before breaking into sprints)

## Workflow

### Step 1: Fetch Issue Details

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [number]
```

Extract: title, labels, body, child issues (if sprint/epic).

For sprints, also fetch each child issue:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [child-number]
```

### Step 2: Research Codebase

For each work item in the sprint/issue:

1. **Identify affected files** — use Grep/Glob to find relevant code
2. **Read key sections** — understand current patterns and dependencies
3. **Note cross-tool impact** — does this change shared libraries?
4. **Check for blockers** — are there dependencies between items?

Use the Explore agent for deep research when needed.

### Step 3: Invoke Brainstorming Skill

Use the `superpowers:brainstorming` skill to collaborate with the user on:
- Scope decisions (what's in, what's out)
- Approach options (with trade-offs)
- Ordering and dependencies
- Design details for each work item

Follow the brainstorming flow through to spec document creation and review.

### Step 4: Write Plan

Save the approved design spec to:

```
NonPublic/Plans/{YYYY-MM-DD}-{issue-number}-plan.md
```

**Examples:**
- `NonPublic/Plans/2026-03-23-1901-plan.md`
- `NonPublic/Plans/2026-04-01-2050-plan.md`

Ensure the directory exists:
```bash
mkdir -p NonPublic/Plans
```

### Step 5: Report Summary

```markdown
## Pre-Warm Complete

**Issue**: #[number] — [title]
**Plan**: `NonPublic/Plans/{date}-{number}-plan.md`

### What's Ready
- [Number] work items planned
- Dependencies mapped
- Implementation order defined

### To Start Work
Run `/init-item #[number]` — it will detect the pre-warmed plan automatically.
```

## Output Filename Convention

```
NonPublic/Plans/{YYYY-MM-DD}-{issue-number}-plan.md
```

- `{YYYY-MM-DD}` — date the plan was created
- `{issue-number}` — GitHub issue number

## Notes

- **No code changes** — this is planning only
- **No branches or PRs** — those happen when `/init-item` runs
- The plan file lives in NonPublic/ and is not committed to git
- `/init-item` will detect existing plans via glob: `NonPublic/Plans/*-{issue-number}-plan.md`
- Use this while another sprint is in progress to get ahead
