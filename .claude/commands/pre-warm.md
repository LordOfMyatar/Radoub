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

### 1.4 Challenge AI-generated issues (MANDATORY)

AI-filed issues carry fabricated line numbers, invented magnitudes, and premises that no
longer hold. Challenge them before planning, not after. This step is the reason `/init-item`
can skip its own challenge pass — record the results in the plan so the work is not repeated.

**When this applies.** Challenge any issue that is AI-generated tech debt or came from a
`/code-review` sweep — check the body for a `## Source` line citing `/code-review`, a
`🤖 Generated with Claude Code` footer, or `tech-debt`/`performance` labels on a body full of
`file.cs:123` citations.

Skip it for human-written enhancements and feature requests. A feature describes what should
exist, so there is no premise to falsify.

**How.** Dispatch one `Explore` agent per issue, in a single message so they run concurrently.
Instruct each to **refute**, not confirm — an agreeable verifier finds nothing. Give it the
issue's claims as a numbered list and require:

- The actual file, quoted code, and actual current line number for every claim
- A per-claim verdict: CONFIRMED / WRONG / STALE / UNREACHABLE
- `git log` on the cited files since the issue's filing date, since another sprint may have
  already fixed it
- An explicit flag when a cited line number is off by more than ~20 lines or the file does not
  exist — that signals a fabricated citation

Aim each agent at the claim most likely to be wrong. Common failure modes worth naming
directly in the prompt:

| Smell | What to check |
|---|---|
| A big-O or "N calls" perf claim | Whether the data is already cached — a cached lookup collapses the severity |
| A round row count (`~2000`, `~300`) | Whether it came from a `?? fallback` constant rather than a real table |
| "These two have drifted" | Compare character by character; the drift is often bigger or smaller than filed |
| "X is unreachable / theoretical" | Hunt for a real guard; absence of one upgrades severity |
| Duplicated code that "should be unified" | Whether the variants are different-by-design |

**Then act on the results.** Do not plan around a claim that failed verification:

- Materially wrong premise -> ask the user whether to rescope, keep, or defer the item
- Wrong or understated details -> correct the GitHub issue body via
  `gh issue edit N --body-file NonPublic/_scratch/N-body.md`, keeping a dated correction note
  in the body so the next reader knows it was re-verified
- Record every verdict in the plan's Verification Summary table

Verify the active GitHub identity is `LordOfMyatar` (`gh api user --jq .login`) before editing
any issue.

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

The plan must open with a Verification Summary recording the Phase 1.4 results, so
`/init-item` can confirm the challenge was done and skip its own pass:

```markdown
## Verification Summary

Challenged [date] per `/pre-warm` Phase 1.4.

| Issue | Verdict | Action taken |
|---|---|---|
| #N | CONFIRMED — all claims hold | None needed |
| #N | Headline REFUTED | Body corrected: [what] |
| #N | CONFIRMED but understated | Body corrected: [what] |
```

Then report:

```markdown
## Pre-Warm Complete

**Issue**: #[number] — [title]
**Plan**: `NonPublic/Plans/{date}-{number}-plan.md`

### What's Ready
- [N] work items planned, [N] challenged and verified
- Dependencies mapped
- Implementation order defined
- [Any item whose premise no longer holds, and why]
- [Any GitHub issue bodies corrected]

### To Start Work
Run `/init-item #[number]` — it detects the plan and skips re-verification.
```
