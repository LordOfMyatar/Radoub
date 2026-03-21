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

**Stale [Unreleased] check**:

Check ALL affected CHANGELOG files (root `CHANGELOG.md` + tool-specific) for non-empty `[Unreleased]` sections. If `[Unreleased]` contains bullet points:

1. For each item, search `git log main --oneline --grep="<keyword>"` to find which commit/PR introduced it
2. Check if that PR is already merged to main (i.e., the item should be in a versioned section, not Unreleased)
3. Report as:
   ```
   ⚠️ Stale [Unreleased] items found in [CHANGELOG file]:
     - "[item text]" → should be in [version] (PR #NNN, merged YYYY-MM-DD)
   ```
4. **Do NOT auto-fix** — flag for the user to confirm placement before moving items

This prevents items from being silently left behind when CHANGELOGs are updated during sprints.

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
| Fence | TBD | Alpha |
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
| Unit tests | ✅ N passed / ❌ N failed |
| UI tests | ⏭️ Skipped / 🔄 Auto-triggered / ✅ N passed / ❌ N failed |

**UI Test Reason**: [Manual --ui-tests / Auto-detected UI changes / Skipped (no UI changes)]
**UIFilter**: [filter expression / "all" / N/A]

### Validation
| Check | Status |
|-------|--------|
| CHANGELOG | ✅/⚠️ |
| Unreleased items | ✅ Empty / ⚠️ N stale items need placement |
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
