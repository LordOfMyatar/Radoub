# Research Task

Investigate a GitHub issue or topic and produce a research summary. Optionally spike with
throwaway code in an isolated worktree.

## Usage

```
/research #[issue-number]
/research [topic description]
/research --spike #[issue-number] [--timebox 2h]
```

| Flag | Effect |
|------|--------|
| `--spike` | Hands-on prototyping in a throwaway worktree, deleted after findings are captured |
| `--timebox` | Spike duration — 1h, 2h (default), or 4h |

Ask once, up front: which aspects to focus on if the topic is broad, depth
(quick-scan / thorough / exhaustive), and whether to save notes. Then run without further
prompts unless a critical ambiguity turns up.

Research is **read-only** — no code changes, except under `--spike`.

## Phase 1 — Set up

### 1.1 Refresh the cache

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
```

### 1.2 Spike worktree (only with `--spike`)

**Use a worktree, never `git checkout -b` in place.** Branching in the main workspace stashes
the user's in-progress work and forces a context switch; they almost always have something
going. Invoke the `superpowers:using-git-worktrees` skill.

Convention: worktree at `../Radoub-spike-<short-topic>/` on branch `spike/<short-topic>`.

```markdown
## Spike Started

**Topic**: [topic]
**Question**: [what we're trying to answer]
**Timebox**: [duration]
**Worktree**: ../Radoub-spike-[topic]
```

## Phase 2 — Investigate

### 2.1 Understand the request

Cache-first; no direct `gh issue view` for reads.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [number]
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[keyword]"
```

Extract the goal, the affected areas, and any stated constraints.

**Verify the premise.** An issue's description may no longer match the code — the work may be
partly done, or the problem may have moved. Check before researching solutions to a problem
that no longer exists.

### 2.2 Explore the codebase

Use the native tools, not bash pipelines: **Glob** for files, **Grep** for content, and the
**Explore agent** for open-ended sweeps. Look for existing patterns worth reusing, integration
points, and conflicts or dependencies.

### 2.3 External research

When evaluating a library, check: last commit or release (stale beyond two years is a red
flag), maintainer responsiveness, .NET and Avalonia compatibility, license (MIT or Apache
preferred), adoption, and whether a better-maintained alternative exists.

Prefer the reference implementations already cloned locally for Aurora-format and rendering
questions — see the Resources section of CLAUDE.md. Document sources either way.

### 2.4 Ask clarifying questions

Before writing conclusions, surface ambiguous requirements, trade-offs between approaches, and
any implementation-style preference. Wait for the answer.

## Phase 3 — Report

### 3.1 Write the notes

`NonPublic/[Tool]/Research/` — NonPublic always lives at the repo root. Use `Radoub/` for
cross-tool or format work. Spikes go to `spike-[topic].md`.

```markdown
# Research: [Title]

**Date**: [YYYY-MM-DD]
**Issue**: #[number]
**Status**: Research Complete / Needs Clarification

## Summary

[2-3 sentences]

## Key Findings

### Approach A: [Name]
- **Pros**: [list]
- **Cons**: [list]
- **Effort**: Low/Medium/High
- **Risk**: Low/Medium/High

### Approach B: [Name]

## Recommendation

[Which and why]

## Open Questions

## Resources

## Code References

- `[file:line]` — [what it does]
```

Keep the notes even when the approach is rejected — it stops the same ground being
re-researched later.

### 3.2 Clean up the spike

Capture findings **first**, then remove the worktree and delete the branch. Never merge a
spike.

```bash
git worktree remove ../Radoub-spike-[topic]
git branch -D spike/[short-topic]
```

Respect the timebox: when it runs out, write up what you have and stop. Log new questions as
open questions rather than chasing them.
