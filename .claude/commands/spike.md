# Spike Solution

Timeboxed throwaway prototype to reduce uncertainty before committing to an implementation approach.

Spikes produce **findings, not production code**. The branch is deleted after research is captured.

## Usage

```
/spike [topic or #issue-number]
```

**Examples:**
- `/spike #1314` (explore HAK scanning approaches)
- `/spike 2DA inheritance resolution` (investigate a technical question)
- `/spike Avalonia TreeDataGrid performance` (evaluate a library/pattern)

## Upfront Questions

Before starting, collect these answers in ONE interaction:

1. **Scope**: "What specific question(s) should this spike answer?"
2. **Timebox**: "How long? [1h / 2h / 4h] (default: 2h)"
3. **Tool**: "Which tool is this for? [parley/radoub/manifest/quartermaster/fence/trebuchet]" (if not obvious)

## When to Spike

| Situation | Spike? |
|-----------|--------|
| New file format parser (unknown edge cases) | Yes |
| Unfamiliar 2DA interaction pattern | Yes |
| UI pattern not yet used in Radoub | Yes |
| Library evaluation (performance, compatibility) | Yes |
| Well-understood feature addition | No |
| Bug fix with clear reproduction | No |
| Test-only work | No |
| Documentation changes | No |

## Workflow

### Step 1: Create Spike Branch

```bash
git stash  # if needed
git checkout main
git checkout -b spike/[short-topic]
```

Branch naming: `spike/hak-scanning`, `spike/treedatagrid-perf`, `spike/2da-inheritance`

### Step 2: Set Context

Log the spike parameters:

```markdown
## Spike Started

**Topic**: [topic]
**Question(s)**: [what we're trying to answer]
**Timebox**: [duration]
**Started**: [timestamp]
```

Display this to the user so they can track time.

### Step 3: Explore and Prototype

- Write throwaway code to test approaches
- Focus on answering the specific question(s)
- Don't worry about code quality, tests, or conventions
- Take notes on findings as you go
- **Commit freely** on the spike branch (these commits will be deleted)

### Step 4: Generate Findings

When the timebox expires or the question is answered, create a findings document:

```markdown
# Spike: [Topic]

**Date**: [YYYY-MM-DD]
**Duration**: [actual time spent]
**Issue**: #[number] (if applicable)
**Decision**: [one-line summary of what we learned]

## Question(s)

- [What we were trying to answer]

## Findings

### Approach A: [Name]
- **Description**: [what was tried]
- **Result**: [what happened]
- **Pros**: [list]
- **Cons**: [list]

### Approach B: [Name]
[Same structure]

## Recommendation

[Proceed with approach X / Need more investigation / Abandon this direction]

## Code Snippets Worth Preserving

[Any patterns or code worth keeping for the real implementation]

## Open Questions

- [Anything still unresolved]
```

Save to: `[Tool]/NonPublic/Research/spike-[topic].md`

### Step 5: Clean Up

```bash
# Switch back to previous branch
git checkout -  # or git checkout main

# Delete the spike branch (local and remote if pushed)
git branch -D spike/[short-topic]
git push origin --delete spike/[short-topic] 2>/dev/null  # ignore if not pushed
```

### Step 6: Report Summary

```markdown
## Spike Complete

**Topic**: [topic]
**Duration**: [time]
**Decision**: [recommendation]
**Findings**: [Tool]/NonPublic/Research/spike-[topic].md

### Key Takeaway
[1-2 sentences on what we learned and how it affects the implementation]
```

## Important Rules

- **Never merge a spike branch** - spikes are throwaway
- **Always capture findings** before deleting the branch
- **Respect the timebox** - if time runs out, document what you have and stop
- **Spike != Research** - `/research` gathers information (reading); `/spike` writes throwaway code (prototyping)
- **One question at a time** - if the spike reveals new questions, log them as open questions for future spikes

## Spike vs Research

| | `/research` | `/spike` |
|---|---|---|
| **Activity** | Reading, searching, analyzing | Writing throwaway code |
| **Output** | Research notes, recommendations | Findings + code snippets |
| **Branch** | No branch needed | Throwaway spike branch |
| **Code changes** | None (read-only) | Prototype code (deleted) |
| **When** | "What options exist?" | "Will this approach actually work?" |
