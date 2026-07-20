---
name: verification-before-completion
description: Use when about to claim work is complete, fixed, or passing, before committing or creating PRs - requires running verification commands and confirming output before making any success claims; evidence before assertions always
---

# Verification Before Completion

## Overview

Claiming work is complete without verification is dishonesty, not efficiency.

**Core principle:** Evidence before claims, always.

**Violating the letter of this rule is violating the spirit of this rule.**

## The Iron Law

```
NO COMPLETION CLAIMS WITHOUT FRESH VERIFICATION EVIDENCE
```

If you haven't run the verification command in this message, you cannot claim it passes.

## The Gate Function

```
BEFORE claiming any status or expressing satisfaction:

1. IDENTIFY: What command proves this claim?
2. RUN: Execute the FULL command (fresh, complete)
3. READ: Full output, check exit code, count failures
4. VERIFY: Does output confirm the claim?
   - If NO: State actual status with evidence
   - If YES: State claim WITH evidence
5. ONLY THEN: Make the claim

Skip any step = lying, not verifying
```

## Common Failures

| Claim | Requires | Not Sufficient |
|-------|----------|----------------|
| Tests pass | Test command output: 0 failures | Previous run, "should pass" |
| Linter clean | Linter output: 0 errors | Partial check, extrapolation |
| Build succeeds | Build command: exit 0 | Linter passing, logs look good |
| Bug fixed | Test original symptom: passes | Code changed, assumed fixed |
| Regression test works | Red-green cycle verified | Test passes once |
| Agent completed | VCS diff shows changes | Agent reports "success" |
| Requirements met | Line-by-line checklist | Tests passing |
| Shared-lib change is safe | Consuming tools' tests run | Only the shared project's tests |
| UI behaves correctly | Human spot-check, or FlaUI with consent | Unit tests, "the AXAML looks right" |

## Radoub Rules — How to Capture Test Output

**NEVER pipe test output through `tail` or `head`.** It discards failures and produces false
"all tests pass" claims — the exact dishonesty this skill exists to prevent.

Correct pattern: run with `run_in_background`, then read the output file.

```bash
dotnet test Parley/Parley.Tests   # with run_in_background=true
```

Then grep the output file for the summary and any failures:

```bash
grep -E "^(Failed|Passed|  Failed)" "$OUTPUT_FILE"
```

The `Failed!` / `Passed!` summary lines plus `  Failed` detail lines give full visibility. If
failures appear, read the whole file for stack traces — do not report a count you did not read.

**Never poll test/build output with `Monitor` or an until/grep loop.** Use
`run_in_background`, then read when notified.

**Scope the run.** `dotnet test Radoub.sln` is ~30 minutes; prefer the affected project.
Changed `Radoub.Formats` or `Radoub.UI`? They are consumed by every tool — test the consumers
of what you touched.

**Rebuild before asking for a manual test.** Stale shared DLLs have produced a phantom bug
report. Build first, then hand it over.

## Radoub Rules — FlaUI

FlaUI integration tests take over the desktop (keyboard, mouse, focus).

- **Never launch FlaUI without explicit user confirmation.** Not "recommended and proceeding" —
  ask, then wait for a yes.
- **Foreground only, never `run_in_background`.** Backgrounding returns a shell prompt the
  instant it launches, which is indistinguishable from "done".
- Announce `FlaUI STARTING — hands off keyboard/mouse` before launching, and confirm
  pass/fail when it finishes so the machine is known safe to touch.
- Use the canonical runner, never a hand-built `dotnet test --filter`:
  ```
  powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool <Tool> [-UIFilter "<filter>"]
  ```
- FlaUI **cannot see into an OpenGL surface** — it proves nothing about 3D model-preview
  rendering. For those, use a human spot-check plus GL-error logs.

## Radoub Rules — Binary Formats

For any change touching a file-format reader or writer, "tests pass" is not sufficient
evidence on its own. Round-trip the format (read → write → read) and confirm the bytes
survive. Silent corruption passes unit tests and fails in-game.

## Red Flags - STOP

- Using "should", "probably", "seems to"
- Expressing satisfaction before verification ("Great!", "Perfect!", "Done!", etc.)
- About to commit/push/PR without verification
- Trusting agent success reports
- Relying on partial verification
- Thinking "just this once"
- Tired and wanting work over
- **ANY wording implying success without having run verification**

## Rationalization Prevention

| Excuse | Reality |
|--------|---------|
| "Should work now" | RUN the verification |
| "I'm confident" | Confidence ≠ evidence |
| "Just this once" | No exceptions |
| "Linter passed" | Linter ≠ compiler |
| "Agent said success" | Verify independently |
| "I'm tired" | Exhaustion ≠ excuse |
| "Partial check is enough" | Partial proves nothing |
| "Different words so rule doesn't apply" | Spirit over letter |

## Key Patterns

**Tests:**
```
✅ [Run test command] [See: 34/34 pass] "All tests pass"
❌ "Should pass now" / "Looks correct"
```

**Regression tests (TDD Red-Green):**
```
✅ Write → Run (pass) → Revert fix → Run (MUST FAIL) → Restore → Run (pass)
❌ "I've written a regression test" (without red-green verification)
```

**Build:**
```
✅ [Run build] [See: exit 0] "Build passes"
❌ "Linter passed" (linter doesn't check compilation)
```

**Requirements:**
```
✅ Re-read plan → Create checklist → Verify each → Report gaps or completion
❌ "Tests pass, phase complete"
```

**Agent delegation:**
```
✅ Agent reports success → Check VCS diff → Verify changes → Report actual state
❌ Trust agent report
```

## Why This Matters

From 24 failure memories:
- your human partner said "I don't believe you" - trust broken
- Undefined functions shipped - would crash
- Missing requirements shipped - incomplete features
- Time wasted on false completion → redirect → rework
- Violates: "Honesty is a core value. If you lie, you'll be replaced."

## When To Apply

**ALWAYS before:**
- ANY variation of success/completion claims
- ANY expression of satisfaction
- ANY positive statement about work state
- Committing, PR creation, task completion
- Moving to next task
- Delegating to agents

**Rule applies to:**
- Exact phrases
- Paraphrases and synonyms
- Implications of success
- ANY communication suggesting completion/correctness

## The Bottom Line

**No shortcuts for verification.**

Run the command. Read the output. THEN claim the result.

This is non-negotiable.
