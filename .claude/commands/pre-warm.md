# Pre-Warm Next Sprint

Prepare an implementation plan for an upcoming sprint or issue while the current one is still
running. **Planning only** — no branches, PRs, or code changes. Those happen in `/init-item`.

## Usage

```
/pre-warm #[issue-number]
```

Plans are written to `NonPublic/Plans/{YYYY-MM-DD}-{issue-number}-plan.md` — gitignored, and
found later by `/init-item` via `NonPublic/Plans/*-{issue-number}-plan.md`.

## Phase 1 — Gather

### 1.1 Check for an existing plan

```bash
ls NonPublic/Plans/*-[number]-plan.md 2>/dev/null
```

If one exists, **do not silently write a second**. Read it and ask how to proceed:

| Choice | Meaning |
|--------|---------|
| Update | The prior plan is the spine; refresh it with new findings |
| Supersede | Write fresh, delete the old |
| Merge | Combine a richer-but-older plan with newer corrected facts — say which leads |

When updating or merging, **re-verify the old plan's hard references**. Code moves between
sessions, so line numbers and file paths drift, and linked issues may have closed. Confirm
before citing.

### 1.2 Fetch the issue

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [number]
```

Take the title, labels, body, and child issues. Fetch each child for a sprint or epic, and
check their live state — a sprint body often lists children that have since closed:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "N,N,N"
```

### 1.3 Research the code

Per work item: find the affected files with Grep and Glob, read enough to understand the
current patterns and dependencies, note any shared-library impact, and flag ordering
dependencies between items. Use the Explore agent for open-ended sweeps.

**Verify each item's premise.** An issue written weeks ago may describe a problem already
fixed, or partly delivered by other work. A plan built on a stale premise wastes the sprint.

## Phase 2 — Design

Invoke the `superpowers:brainstorming` skill and work through scope (in and out), approach
options with trade-offs, ordering and dependencies, and per-item design. Follow it through to
a spec document and review.

## Phase 3 — Write and report

```bash
mkdir -p NonPublic/Plans
```

Save the approved spec to `NonPublic/Plans/{YYYY-MM-DD}-{issue-number}-plan.md`, dated the day
it was written — for example `NonPublic/Plans/2026-03-23-1901-plan.md`.

```markdown
## Pre-Warm Complete

**Issue**: #[number] — [title]
**Plan**: `NonPublic/Plans/{date}-{number}-plan.md`

### What's Ready
- [N] work items planned
- Dependencies mapped
- Implementation order defined
- [Any item whose premise no longer holds, and why]

### To Start Work
Run `/init-item #[number]` — it detects the plan automatically.
```
