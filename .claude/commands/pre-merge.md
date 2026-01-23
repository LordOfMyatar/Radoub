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

### Step 1: Get PR Info (single gh call)

```bash
gh pr view --json number,title,state,baseRefName,isDraft,body
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
| `Radoub.*/**` | All affected | Yes |
| Multiple tools | All affected | Yes |

**Auto-detect UI changes** (unless `--no-auto-ui`):

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

**Decision logic**:
1. If `--ui-tests` flag → always include UI tests
2. If `--no-auto-ui` flag → never auto-include UI tests
3. If UI patterns detected → auto-include UI tests
4. Otherwise → unit tests only

**Report auto-detection**:
If UI tests are auto-included, display:
```
ℹ️ UI changes detected - including integration tests
   Changed UI files: [list first 5 files, then "+ N more" if applicable]
```

### Step 3: Build & Test (single script call)

```powershell
# Unit tests only (no UI changes detected, no --ui-tests flag)
.\Radoub.IntegrationTests\run-tests.ps1 -Tool [detected] [-SkipShared] -UnitOnly -TechDebt

# With --ui-tests flag OR UI changes auto-detected (omit -UnitOnly)
.\Radoub.IntegrationTests\run-tests.ps1 -Tool [detected] [-SkipShared] -TechDebt

# With --full-tests flag
.\Radoub.IntegrationTests\run-tests.ps1 -TechDebt
```

**Test scope decision**:
- If `--skip-tests` → skip entirely
- If `--full-tests` → run everything
- If `--ui-tests` OR UI changes detected → include UI tests
- If `--no-auto-ui` → force unit-only even if UI files changed
- Otherwise → unit tests only

The script handles:
- Privacy scan (hardcoded paths)
- Tech debt scan (large files >500 lines)
- Unit tests (tool + shared if needed)
- UI tests (if not -UnitOnly)

### Step 4: CHANGELOG and Version Validation

Read CHANGELOG and verify:
- Version section exists
- PR number filled in (not TBD)
- Date is today or earlier

**Verify .csproj version matches CHANGELOG**:
```bash
# Get latest version from tool's CHANGELOG
grep -E "^\#\# \[" [Tool]/CHANGELOG.md | head -2 | tail -1

# Compare with .csproj Version
grep "<Version>" [Tool]/[Tool]/[Tool].csproj
```

If versions don't match, update the `.csproj` file:
- `<Version>` - Semantic version (e.g., "0.1.44-alpha")
- `<AssemblyVersion>` - Numeric only (e.g., "0.1.44.0")
- `<FileVersion>` - Numeric only (e.g., "0.1.44.0")
- `<InformationalVersion>` - Same as Version

### Step 5: Wiki Freshness Check

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
| Tech debt | ✅/⚠️ |
| Unit tests | ✅ N passed / ❌ N failed |
| UI tests | ⏭️ Skipped / 🔄 Auto-triggered / ✅ N passed / ❌ N failed |

**UI Test Reason**: [Manual --ui-tests / Auto-detected UI changes / Skipped (no UI changes)]

### Validation
| Check | Status |
|-------|--------|
| CHANGELOG | ✅/⚠️ |
| Wiki | ✅ Current / ⚠️ Stale |

### Status
**Ready**: ✅ / ⚠️ [N] warnings / ❌ Blocked
```

### Step 7: Update PR (batched gh commands)

```bash
# Single command with && chaining
gh pr edit [number] --body "[generated body]" && gh pr ready [number] 2>/dev/null || true
```

The `|| true` handles case where PR is already ready.

## Test Script Reference

```powershell
.\Radoub.IntegrationTests\run-tests.ps1
    -Tool [Parley|Quartermaster|Manifest|Fence]
    -SkipShared      # Skip Radoub.* tests
    -UnitOnly        # Skip UI tests (default for pre-merge)
    -TechDebt        # Include large file scan
```

## Notes

- Default: unit tests only (fast), unless UI changes detected
- UI tests auto-triggered when `.axaml`, `Views/`, `Controls/`, `Dialogs/`, or `Windows/` files change
- Use `--no-auto-ui` to disable auto-detection (force unit-only)
- Use `--ui-tests` to force UI tests regardless of what changed
- Shared library changes → include shared tests
- Wiki updates done separately with `/documentation`
- **Hands-off keyboard during UI tests** - focus stealing can cause failures
