# Pre-Merge Checklist

Validate the current PR is ready to merge. Runs tests, checks code quality, verifies documentation.

## Usage

```
/pre-merge [--skip-tests] [--ui-tests] [--full-tests] [--no-auto-ui]
```

**Flags**:
- `--skip-tests` - Skip test execution (validation only)
- `--ui-tests` - Force include UI integration tests
- `--full-tests` - Run all tests regardless of what changed
- `--no-auto-ui` - Disable auto-detection of UI changes (use unit tests only)

## Workflow

### Step 0: Check for Uncommitted Changes

```bash
git status --porcelain
```

**If output is not empty**:
- ⚠️ STOP and warn: "Uncommitted changes detected. Commit or stash before running pre-merge."
- List the uncommitted files
- Do not proceed with checklist

This prevents running pre-merge with local changes that aren't reflected in the PR.

### Step 0b: Check for Unpushed Commits

```bash
git log origin/$(git branch --show-current)..HEAD --oneline
```

**If output is not empty**:
- ⚠️ STOP and warn: "Unpushed commits detected. Push before running pre-merge."
- List the unpushed commits
- Do not proceed with checklist

This prevents validating a PR that doesn't contain all local commits.

### Step 0c: Verify init-item Was Not Skipped (mandatory gate)

`/init-item` is the mandatory start-of-work step (sync main → convention branch → seed CHANGELOG → draft PR linking the issue → board for sprints/epics). Work sometimes starts without it (spec/plan-first sessions, hand-rolled branches). This gate catches a skipped init-item before merge. **Warn loudly; do not hard-block.**

Check two signals:

1. **Branch naming convention** — the branch should match `[tool]/issue-[N]`, `[tool]/feat/[name]`, `[tool]/fix/[name]`, or `[tool]/sprint/[name]`:
   ```bash
   git branch --show-current
   ```

2. **Linked issue** — the PR body should reference an issue (`Closes #N`, `Fixes #N`, `Relates to #N`, or an issue URL). Read the PR body (cache, or `gh pr view --json body -q '.body'`) and the branch name for any `#N` / `issue-N`.

**Decision:**

| Finding | Action |
|---------|--------|
| Branch follows convention AND PR links an issue | ✅ init-item satisfied — proceed silently. |
| PR links an issue but branch name is off-convention | ⚠️ Warn (cosmetic — branch already exists, don't rename). Note in checklist, proceed. |
| **No issue linked anywhere** (PR body + branch) | ⚠️ **Warn loudly: "init-item was skipped — no linked issue."** Then **create a tracking issue unless the user says this work is intentionally issueless.** Search the cache first to avoid duplicates (per the existing issue-dedup habit); if none exists, `gh issue create` with the right `[Tool]` + type labels, then add `Closes #N` to the PR body and backfill the CHANGELOG PR/issue reference. Only skip issue creation if the user explicitly says "no issue needed." |
| Sprint/Epic PR not on the project board | ⚠️ Warn and offer to add it (init-item does this for sprints/epics only). |

Report in the Step 6 checklist under a new **init-item** row (see Step 6).

After any mutation here (issue created, PR body edited), the Step 7b cache refresh covers it.

### Step 1: Get PR Info (from cache)

**Cache-first**: Refresh cache once here, then read from cache for all subsequent steps (tech debt dedup, stale unreleased check, etc.). Do NOT call the refresh script again during reads — only Step 7b refreshes again if mutations occurred.

```bash
# Ensure cache is fresh (ONE call for the entire pre-merge run)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
```

Match the current branch name against cached PRs to find the PR for this branch. The cache includes: number, title, isDraft, headRefName, reviewDecision.

For PR body (needed for release notes), use:
```bash
# Only if PR body is needed and not in cache
gh pr view --json body -q '.body'
```

If no PR exists, warn and stop.

### Step 2: Analyze Changes

```bash
git diff main...HEAD --name-only
```

**Determine tool and test scope**:

| Changed Path | Tool | Include Shared |
|--------------|------|----------------|
| `Parley/**` only | Parley | No |
| `Manifest/**` only | Manifest | No |
| `Quartermaster/**` only | Quartermaster | No |
| `Fence/**` only | Fence | No |
| `Trebuchet/**` only | Trebuchet | No |
| `Radoub.*/**` | All affected | Yes |
| Multiple tools | All affected | Yes |

**Auto-detect UI changes and target FlaUI tests** (unless `--no-auto-ui`):

Check if ANY changed files match UI patterns:
```bash
git diff main...HEAD --name-only | grep -E '\.(axaml|axaml\.cs)$|/Views/|/Controls/|/Dialogs/'
```

**UI test auto-trigger rules**:

| Pattern Match | Auto-include UI Tests |
|---------------|----------------------|
| `*.axaml` | Yes |
| `*.axaml.cs` | Yes |
| `*/Views/*` | Yes |
| `*/Controls/*` | Yes |
| `*/Dialogs/*` | Yes |
| `*/Windows/*` | Yes |
| Other `.cs` files | No (unit tests only) |

**Targeted FlaUI test mapping**:

When UI changes are detected, map changed files to specific FlaUI test categories or methods instead of running all UI tests. This keeps FlaUI runs fast and focused.

| Changed File Pattern | UIFilter | Rationale |
|---------------------|----------|-----------|
| `LaunchTestPanel*` | `LaunchTab` | Only tests that interact with the Launch & Test tab |
| `ModuleEditorPanel*` | `Category=Workspace` | Module tab workspace tests |
| `FactionEditorPanel*` | `Category=Workspace` | Faction tab workspace tests |
| `MainWindow.axaml*` | `Category=Smoke` | Smoke tests verify top-level layout |
| `*Toolbar*` | `Toolbar` | Toolbar-specific tests |
| `*Sidebar*` | `Sidebar` | Sidebar-specific tests |
| Multiple UI areas | (no filter — run all tool UI tests) | Broad changes need full coverage |
| New dialog/view only | (skip FlaUI — no tests exist yet) | Flag for manual verification |

**UIFilter syntax**: Plain keywords (e.g., `LaunchTab`) auto-expand to `FullyQualifiedName~LaunchTab`. For trait-based filters, use `Category=Smoke` or `DisplayName~pattern` explicitly. `Name~` does NOT work with xUnit VSTest adapter.

**Decision logic**:
1. If `--ui-tests` flag → always include UI tests (all tool tests, no filter)
2. If `--no-auto-ui` flag → never auto-include UI tests
3. If `--full-tests` flag → run everything (no filter)
4. If UI patterns detected → auto-include targeted UI tests with `-UIFilter`
5. Otherwise → unit tests only

**Report auto-detection**:
If UI tests are auto-included, display:
```
ℹ️ UI changes detected - including targeted integration tests
   Changed UI files: [list first 5 files, then "+ N more" if applicable]
   UIFilter: [filter expression or "all" if no targeting]
```

### Step 3: Build & Test (single script call)

**IMPORTANT**: Use full absolute path to run-tests.ps1 since relative paths fail in PowerShell from Bash.

```powershell
# Unit tests only (no UI changes detected, no --ui-tests flag)
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -UnitOnly -TechDebt

# With UI changes auto-detected — targeted filter
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -TechDebt -UIFilter "LaunchTab"

# With --ui-tests flag (all tool UI tests, no filter)
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -Tool [detected] [-SkipShared] -TechDebt

# With --full-tests flag
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" -TechDebt
```

**Test scope decision**:
- If `--skip-tests` → skip entirely
- If `--full-tests` → run everything (no filter)
- If `--ui-tests` → all tool UI tests (no filter)
- If UI changes detected → targeted UI tests with `-UIFilter`
- If `--no-auto-ui` → force unit-only even if UI files changed
- Otherwise → unit tests only

The script handles:
- Privacy scan (hardcoded paths)
- Tech debt scan (warn >800, issue >1000 lines)
- Unit tests (tool + shared if needed)
- UI tests (if not -UnitOnly)

### Step 3b: Tech Debt Issue Verification

The tech debt scan uses two thresholds:
- **>800 lines**: Warning only (logged, no issue required)
- **>1000 lines**: Issue required (must have a tracking issue)

For files exceeding **1000 lines**, do NOT assume they are pre-existing. For each:

1. Search the GitHub issue cache for an existing tech debt issue (includes both open and closed):
   ```bash
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[filename without path]"
   ```

2. **If an open issue exists**: Report as tracked tech debt with issue number
   ```
   ⚠️ Tech debt: MainWindowViewModel.cs (1288 lines) - tracked in #1335
   ```

3. **If a closed issue exists**: The file was previously tracked. Do NOT create a duplicate.
   ```
   ⚠️ Tech debt: [filename] ([N] lines) - previously tracked in #XXXX (closed)
   ```

4. **If NO issue exists** (open or closed): Flag as **untracked tech debt** and create an issue:
   ```bash
   gh issue create --title "[Tool] Tech Debt: Split [filename] ([N] lines)" \
     --label "tech-debt,[Tool]" \
     --body "..."
   ```
   Report in checklist:
   ```
   ⚠️ Tech debt: MainWindowViewModel.cs (1288 lines) - NEW issue created: #XXXX
   ```

For files in the **800-1000 line** warning range, just report them in the checklist — no issue needed.

**Rule**: Always search the cache first. A tech debt warning is only "new" if no GitHub issue (open or closed) tracks it. Never create duplicates of existing issues.

### Step 4: CHANGELOG Validation

Read CHANGELOG and verify:
- Version section exists
- PR number filled in (not TBD)
- Date is today or earlier

**[Unreleased] section check**:

`[Unreleased]` should never contain items — all CHANGELOG entries go directly into versioned sections. Check ALL affected CHANGELOG files for non-empty `[Unreleased]` sections. If any contain bullet points:

1. Report as:
   ```
   ⚠️ [CHANGELOG file] has items in [Unreleased] — move them to a versioned section
   ```
2. Move the items into the most recent versioned section (or create one) as part of this PR

**Note**: Version numbers are managed by NBGV via `version.json` files — no `.csproj` version properties to check. CHANGELOG versions are for human tracking only and don't need to match computed NBGV versions exactly.

### Step 5: Update Release Notes (NonPublic/release-notes.md)

Append this sprint/PR's accomplishments to the running release notes draft.

**Location**: `NonPublic/release-notes.md` (gitignored — editable between sessions)

1. **Read the current release notes file** (create from template if missing)
2. **Read the PR's CHANGELOG entries** (the version section for this branch)
3. **Check for user-reported issues** in this PR:
   ```bash
   # Extract issue numbers from PR body "Closes #N" lines
   # For each, check if labeled 'bug' or created by someone other than LordOfMyatar
   gh issue view [N] --json labels,author -q '{labels: [.labels[].name], author: .author.login}'
   ```
4. **Append new entries** to the appropriate sections:
   - Bug fixes from user reports → `## Bug Fixes` with 🎯 prefix
   - Feature highlights → `## Highlights` (ask user which items to highlight, or auto-add sprint titles)
   - Tool-specific details → `## [Tool Name]` section
5. **Avoid duplicates** — check if the PR/issue numbers already appear in the file
6. **Show the user what was added** so they can edit later

**Template** (if file doesn't exist):

```markdown
# Release Notes (Draft)

Accumulated since last release: **radoub-vX.Y.Z** (YYYY-MM-DD)

Edit freely — this file is in NonPublic (gitignored) and won't be committed.
The `/release` command reads this file to generate GitHub release notes.

---

## Highlights

<!-- Move the most impressive items here. These appear in the "What's New" section. -->
<!-- Mark user-reported fixes with 🎯 to show responsiveness -->

## Bug Fixes

<!-- 🎯 = reported by user/community (shows responsiveness) -->

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

- Items marked with 🎯 were reported by users/community
- This file is updated by `/pre-merge` and manually between sprints
- `/release` reads this file instead of generating from scratch
```

**User-reported detection**:
- Issue has `user-requested` label → 🎯 (primary signal, most reliable)
- Issue has `bug` label AND was NOT created by `LordOfMyatar` → 🎯 (fallback heuristic)
- Issue has `enhancement` label AND was NOT created by `LordOfMyatar` → 🎯 (fallback heuristic)
- Issue was created by `LordOfMyatar` without `user-requested` label → internal (no 🎯)

### Step 5b: Wiki Freshness Check

```bash
grep "Page freshness:" d:\LOM\workspace\Radoub.wiki\[Tool]-Developer-Architecture.md
```

Flag if >30 days old and code changed.

### Step 6: Generate Checklist

```markdown
## Pre-Merge Checklist for PR #[number]

**Branch**: [branch]
**Title**: [title]
**Tool**: [detected]

### Tests
| Check | Status |
|-------|--------|
| Privacy scan | ✅/⚠️ |
| Tech debt | ✅ / ⚠️ Warning (800+) / ⚠️ Tracked (#N) / ⚠️ NEW issue created (#N) |
| Unit tests (local, Windows) | ✅ N passed / ❌ N failed |
| UI tests | ⏭️ Skipped / 🔄 Auto-triggered / ✅ N passed / ❌ N failed |
| CI checks (incl. Linux) | ✅ All pass / ⏳ In progress / ❌ [N] failed (see Step 7c) |

**UI Test Reason**: [Manual --ui-tests / Auto-detected UI changes / Skipped (no UI changes)]
**UIFilter**: [filter expression / "all" / N/A]

### Validation
| Check | Status |
|-------|--------|
| init-item (linked issue + branch convention) | ✅ Satisfied / ⚠️ Skipped — issue #N created / ⚠️ Off-convention branch / ⚠️ No issue (user-confirmed) |
| CHANGELOG | ✅/⚠️ |
| [Unreleased] section | ✅ Empty / ⚠️ Has items (move to versioned section) |
| Wiki | ✅ Current / ⚠️ Stale |

### Status
**Ready**: ✅ / ⚠️ [N] warnings / ❌ Blocked

**PR**: https://github.com/LordOfMyatar/Radoub/pull/[number]
```

### Step 7: Update PR (batched gh commands)

```bash
# Single command with && chaining
gh pr edit [number] --body "[generated body]" && gh pr ready [number] 2>/dev/null || true
```

The `|| true` handles case where PR is already ready.

### Step 7b: Refresh Cache (if mutations occurred)

If any tech debt issues were created (`gh issue create`) or the PR was edited, refresh the cache:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

### Step 7c: Verify CI Checks (catch cross-platform failures)

Local tests only run on Windows. CI also runs the test suite on **ubuntu-latest**, which catches platform-specific bugs the local run cannot — most commonly path-handling tests that assume Windows semantics (`Path.Combine("C:", ...)` is absolute on Windows but **relative** on Linux, so `Path.GetFullPath` resolves it against the runner CWD and string-equality assertions diverge). The Linux/Windows test split is exactly the gap that slipped a green local pre-merge into a red PR.

After marking the PR ready, check the live CI check status:

```bash
gh pr checks [number]
```

**Interpret the result**:

| State | Action |
|-------|--------|
| All checks `pass` | ✅ Report "CI green" in the checklist. |
| Any check `pending`/`in progress` | ⏳ CI still running. Report "CI in progress — re-run `gh pr checks [number]` before merge." Do NOT claim ready. |
| Any check `fail` | ❌ **Block.** Pull the failing job's log and fix before declaring ready (see below). |

**On failure**, get the real assertion (not the runner's summary line — the `##[error]Unable to process file command 'env'` / `Invalid format` annotations are often a downstream symptom, not the cause):

```bash
# List failing checks and their run/job URLs
gh pr checks [number]

# Pull only the failed steps for the job ID from the URL
gh run view --job [jobId] --log-failed 2>&1 | grep -iE "\[FAIL\]|Assert|Expected:|Actual:|error CS|\.cs\(" | head -30
```

Common Linux-only failure: a unit test asserts a literal Windows path. Fix the **test** (root paths at `Path.GetTempPath()` and compute the expected value with the same `Path.GetFullPath` the code under test uses) — not the production code, which is usually correct. Re-run local tests, push, and re-check `gh pr checks` until green.

**Report in the checklist**:

```
| CI checks (incl. Linux) | ✅ All pass / ⏳ In progress / ❌ [N] failed — [job], fix before merge |
```

A pre-merge is only "Ready" when CI is green (or explicitly deferred by the user). Green-local + unknown-CI is **not** ready.

## Test Script Reference

```powershell
powershell -ExecutionPolicy Bypass -File "d:\LOM\workspace\Radoub\Radoub.IntegrationTests\run-tests.ps1" `
    -Tool [Parley|Quartermaster|Manifest|Fence|Trebuchet] `
    -SkipShared      # Skip Radoub.* tests
    -UnitOnly        # Skip UI tests (default for pre-merge)
    -TechDebt        # Include large file scan
```

## Notes

- **`jq` not available**: Use PowerShell (`ConvertFrom-Json`, `Select-String`) or `gh`'s built-in `--jq` flag for JSON parsing. Always use `powershell.exe` (not `pwsh`).
- Default: unit tests only (fast), unless UI changes detected
- UI tests auto-triggered when `.axaml`, `Views/`, `Controls/`, `Dialogs/`, or `Windows/` files change
- Use `--no-auto-ui` to disable auto-detection (force unit-only)
- Use `--ui-tests` to force UI tests regardless of what changed
- Shared library changes → include shared tests
- Wiki updates done separately with `/documentation`
- **Hands-off keyboard during UI tests** - focus stealing can cause failures
- **CI runs on Linux too** (Step 7c) - local pre-merge only runs Windows. Always check `gh pr checks` after marking ready; never claim "Ready" on green-local + unknown-CI. The classic miss is a test asserting a Windows-literal path that fails under Linux `Path.GetFullPath`.
