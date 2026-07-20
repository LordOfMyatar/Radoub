# Pre-Merge Checklist

Validate the current PR is ready to merge. Runs tests, checks code quality, verifies documentation.

## Usage

```
/pre-merge [--skip-tests] [--ui-tests] [--full-tests] [--no-auto-ui]
```

| Flag | Effect |
|------|--------|
| `--skip-tests` | Validation only, no test execution |
| `--ui-tests` | Force UI integration tests |
| `--full-tests` | Run everything regardless of what changed |
| `--no-auto-ui` | Disable UI-change auto-detection (unit tests only) |

## Phase 1 — Gates

Three checks that stop or warn before any work happens.

### 1.1 Uncommitted changes

```bash
git status --porcelain
```

Non-empty output: STOP. List the files and warn "Commit or stash before running pre-merge."
Validating a PR against a dirty tree tests something the PR does not contain.

### 1.2 Unpushed commits

```bash
# Range syntax (origin/<branch>..HEAD) trips the sandbox path-traversal guard (#2468).
# git cherry takes two refs as separate args, one line per unpushed commit, prefixed '+'.
git cherry -v "origin/$(git branch --show-current)" HEAD
```

Non-empty output: STOP. List the commits and warn "Push before running pre-merge."

### 1.3 Was init-item skipped? (warn, never block)

`/init-item` starts work properly: sync main, branch by convention, seed the CHANGELOG,
open a draft PR linking the issue, add sprints and epics to the board. Spec-first sessions
and hand-rolled branches skip it. Catch that here.

Check the branch name against `[tool]/issue-[N]`, `[tool]/feat/[name]`, `[tool]/fix/[name]`,
or `[tool]/sprint/[name]`, and check the PR body for a linked issue (`Closes #N`, `Fixes #N`,
`Relates to #N`, or an issue URL).

| Finding | Action |
|---------|--------|
| Branch follows convention, PR links an issue | Proceed silently |
| Issue linked, branch off-convention | Warn; do not rename an existing branch. Note it and proceed |
| No issue anywhere | Warn loudly, then create a tracking issue — search the cache first for duplicates, `gh issue create` with `[Tool]` and type labels, add `Closes #N` to the PR body, backfill the CHANGELOG reference. Skip only if the user says the work is intentionally issueless |
| Sprint or epic PR missing from the board | Warn and offer to add it |

Report on the **init-item** row in the Phase 5 checklist. Phase 5.2 refreshes the cache after
any mutation here.

## Phase 2 — Analyze

### 2.1 PR info (cache-first)

Refresh the cache **once**, here. Every later step reads from it; only Phase 5.2 refreshes
again, and only after mutations.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
```

Match the branch name against cached PRs. The cache holds number, title, isDraft,
headRefName, reviewDecision. For the PR body (needed for release notes):

```bash
gh pr view --json body -q '.body'
```

No PR: warn and stop.

### 2.2 Changed files and test scope

```bash
# Triple-dot (main...HEAD) trips the path-traversal guard (#2468); diff from the merge-base.
git diff --name-only "$(git merge-base main HEAD)" HEAD
```

| Changed path | Tool | Include shared |
|--------------|------|----------------|
| One tool's directory only | That tool | No |
| `Radoub.*/**` | All affected | Yes |
| Multiple tools | All affected | Yes |

### 2.3 UI-change detection

Unless `--no-auto-ui`, check whether any changed file is UI:

```bash
git diff --name-only "$(git merge-base main HEAD)" HEAD | grep -E '\.(axaml|axaml\.cs)$|/Views/|/Controls/|/Dialogs/'
```

Any match in `*.axaml`, `*.axaml.cs`, `Views/`, `Controls/`, `Dialogs/`, or `Windows/`
auto-includes UI tests. Other `.cs` files do not.

Map changed files to a targeted filter so FlaUI runs stay fast:

| Changed file | UIFilter | Why |
|--------------|----------|-----|
| `LaunchTestPanel*` | `LaunchTab` | Launch & Test tab only |
| `ModuleEditorPanel*` | `Category=Workspace` | Module tab workspace |
| `FactionEditorPanel*` | `Category=Workspace` | Faction tab workspace |
| `MainWindow.axaml*` | `Category=Smoke` | Top-level layout |
| `*Toolbar*` | `Toolbar` | Toolbar tests |
| `*Sidebar*` | `Sidebar` | Sidebar tests |
| Multiple UI areas | none — run all tool UI tests | Broad change needs full coverage |
| New dialog or view | skip FlaUI, flag for manual check | No tests exist yet |

Plain keywords expand to `FullyQualifiedName~<keyword>`. For traits write `Category=Smoke` or
`DisplayName~pattern` explicitly. `Name~` does not work with the xUnit VSTest adapter.

Precedence: `--ui-tests` → all UI tests, no filter. `--no-auto-ui` → never auto-include.
`--full-tests` → everything, no filter. Otherwise auto-detect, else unit only.

When UI tests are auto-included, report:

```
ℹ️ UI changes detected - including targeted integration tests
   Changed UI files: [first 5, then "+ N more"]
   UIFilter: [expression or "all"]
```

## Phase 3 — Build, test, review

### 3.1 Run the suite

Use the absolute path — relative paths fail when PowerShell is invoked from Bash.

```powershell
# Unit only (no UI changes, no --ui-tests)
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -UnitOnly -TechDebt

# UI changes auto-detected — targeted
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -TechDebt -UIFilter "LaunchTab"

# --ui-tests (all tool UI tests)
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -TechDebt

# --full-tests
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -TechDebt
```

The script covers the privacy scan, tech-debt scan, unit tests, and UI tests unless
`-UnitOnly`.

Omitting `-Tool` runs every suite including all seven UI suites. `-Tool Radoub` is the
shared-library scope and owns **no** UI tests — pairing it with `-UIOnly` runs nothing and
reports "Passed 0", which is not a pass.

### 3.2 Tech-debt issues

Thresholds: over 800 lines warns, over 1000 requires a tracking issue.

For each file over 1000 lines, search the cache before assuming it is known:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[filename]"
```

| Cache result | Report |
|--------------|--------|
| Open issue | `⚠️ Tech debt: [file] ([N] lines) - tracked in #NNNN` |
| Closed issue | `⚠️ Tech debt: [file] ([N] lines) - previously tracked in #NNNN (closed)` |
| Nothing | Create one, then report `- NEW issue created: #NNNN` |

```bash
gh issue create --title "[Tool] Tech Debt: Split [filename] ([N] lines)" \
  --label "tech-debt,[Tool]" --body "..."
```

Files in the 800–1000 range only need a checklist line. Always search first — a warning is
"new" only when no issue, open or closed, tracks it.

### 3.3 Code review

Once the build and tests pass, run the `code-review` skill over the branch diff versus
`main`. A passing build does not catch correctness bugs or duplication.

Triage: correctness bugs get fixed on this branch now (TDD if the fix needs a test), then
re-run 3.1. Reuse and simplification findings get applied when low-risk and in scope, else
filed as tech debt. Out-of-scope findings get a one-line reason.

Skip only for `--skip-tests` or a docs-only diff. `/code-review ultra` is the human's deeper
multi-agent option; pre-merge uses the fast local review.

## Phase 4 — Documentation

### 4.1 CHANGELOG

Verify the version section exists, the PR number is filled in (not TBD), and the date is
today or earlier.

`[Unreleased]` must always be empty — entries go straight into versioned sections. Check
every affected CHANGELOG; if one has bullets, report it and move them into the most recent
versioned section as part of this PR.

Versions come from NBGV via `version.json`, so no `.csproj` version properties exist to
check. CHANGELOG versions are for humans and need not match NBGV exactly.

### 4.2 Release notes

Append this PR's work to `NonPublic/release-notes.md` (gitignored, edited between sessions).

Read the current file (create from the template below if missing) and this branch's CHANGELOG
entries. For each issue the PR closes, check whether it came from a user:

```bash
gh issue view [N] --json labels,author -q '{labels: [.labels[].name], author: .author.login}'
```

| Signal | Mark |
|--------|------|
| `user-requested` label | 🎯 (most reliable) |
| `bug` or `enhancement` label, author is not LordOfMyatar | 🎯 |
| Authored by LordOfMyatar without `user-requested` | internal, no mark |

Append user-reported fixes to `## Bug Fixes` with 🎯, feature highlights to `## Highlights`,
and tool detail to the tool's section. Check the PR and issue numbers are not already
present, then show the user what was added.

Template:

```markdown
# Release Notes (Draft)

Accumulated since last release: **radoub-vX.Y.Z** (YYYY-MM-DD)

Edit freely — this file is in NonPublic (gitignored) and won't be committed.
The `/release` command reads this file to generate GitHub release notes.

---

## Highlights

<!-- Most impressive items. These appear in "What's New". 🎯 marks user-reported fixes. -->

## Bug Fixes

<!-- 🎯 = reported by user/community -->

## [Tool Name]

## Radoub (Shared)

## Tool Versions

| Tool | Version | Maturity |
|------|---------|----------|
| Parley | TBD | Beta |
| Manifest | TBD | Beta |
| Fence | TBD | Beta |
| Trebuchet | TBD | Alpha |
| Quartermaster | TBD | Alpha |

---

## Notes

- 🎯 = reported by users/community
- Updated by `/pre-merge` and by hand between sprints
- `/release` reads this file instead of generating from scratch
```

### 4.3 Wiki freshness

```bash
grep "Page freshness:" "d:/LOM/workspace/Radoub.wiki/[Tool]-Developer-Architecture.md"
```

Flag when over 30 days old and code changed.

## Phase 5 — Publish

### 5.1 Checklist and PR update

```markdown
## Pre-Merge Checklist for PR #[number]

**Branch**: [branch]
**Title**: [title]
**Tool**: [detected]

### Tests
| Check | Status |
|-------|--------|
| Privacy scan | ✅/⚠️ |
| Tech debt | ✅ / ⚠️ Warning (800+) / ⚠️ Tracked (#N) / ⚠️ NEW issue (#N) |
| Unit tests (local, Windows) | ✅ N passed / ❌ N failed |
| UI tests | ⏭️ Skipped / 🔄 Auto-triggered / ✅ N passed / ❌ N failed |
| CI checks (incl. Linux) | ✅ All pass / ⏳ In progress / ❌ [N] failed |

**UI Test Reason**: [Manual --ui-tests / Auto-detected / Skipped (no UI changes)]
**UIFilter**: [expression / "all" / N/A]

### Validation
| Check | Status |
|-------|--------|
| init-item (linked issue + branch convention) | ✅ Satisfied / ⚠️ Skipped — issue #N created / ⚠️ Off-convention / ⚠️ No issue (user-confirmed) |
| Code review | ✅ Clean / ⚠️ [N] fixed / ⚠️ [N] deferred to #M / ➖ Skipped (docs-only) |
| CHANGELOG | ✅/⚠️ |
| [Unreleased] section | ✅ Empty / ⚠️ Has items |
| Wiki | ✅ Current / ⚠️ Stale |

### Status
**Ready**: ✅ / ⚠️ [N] warnings / ❌ Blocked

**PR**: https://github.com/LordOfMyatar/Radoub/pull/[number]
```

```bash
gh pr edit [number] --body "[generated body]" && gh pr ready [number] 2>/dev/null || true
```

The `|| true` covers an already-ready PR.

### 5.2 Refresh cache after mutations

Only if a tech-debt issue was created or the PR body was edited:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

### 5.3 Verify CI (catches Linux-only failures)

Local tests run on Windows alone. CI also runs ubuntu-latest, which catches platform bugs the
local run cannot — most often a test asserting a Windows-literal path. `Path.Combine("C:", …)`
is absolute on Windows but **relative** on Linux, so `Path.GetFullPath` resolves it against
the runner's CWD and string equality diverges. This gap is exactly what turns a green local
pre-merge into a red PR.

```bash
gh pr checks [number]
```

| State | Action |
|-------|--------|
| All pass | Report "CI green" |
| Any pending | Report "CI in progress — re-run before merge." Do not claim ready |
| Any fail | Block, pull the log, fix |

On failure, get the real assertion rather than the runner's summary — `##[error]Unable to
process file command 'env'` and `Invalid format` annotations are usually downstream symptoms:

```bash
gh run view --job [jobId] --log-failed 2>&1 | grep -iE "\[FAIL\]|Assert|Expected:|Actual:|error CS|\.cs\(" | head -30
```

For the Windows-path case, fix the **test**, not the production code: root paths at
`Path.GetTempPath()` and compute the expected value with the same `Path.GetFullPath` the code
under test uses. Re-run locally, push, re-check until green.

A PR is "Ready" only when CI is green or the user explicitly defers. Green-local plus
unknown-CI is not ready.

## Reference

```powershell
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" `
    -Tool [Parley|Quartermaster|Manifest|Fence|Trebuchet|Relique|Reliquary] `
    -SkipShared      # Skip Radoub.* tests
    -UnitOnly        # Skip UI tests (pre-merge default)
    -TechDebt        # Include large-file scan
```

Notes:

- **No `jq` on this box.** Parse JSON with PowerShell `ConvertFrom-Json` or `gh --jq`. Avoid
  `sed`/`awk`/`xargs` — they differ from Linux here or mangle Windows paths.
- Launch `.ps1` from the Bash tool; PowerShell-tool permission rules never match on Windows.
- Unit tests only by default. UI tests trigger on `.axaml`, `Views/`, `Controls/`, `Dialogs/`,
  or `Windows/` changes.
- Shared-library changes pull in shared tests.
- Wiki updates happen separately via `/documentation`.
- **Hands off the keyboard during UI tests** — stolen focus fails them.
- CI runs Linux too (5.3). Never claim Ready on green-local plus unknown-CI.
