# Pre-Merge Checklist

Validate the current PR is ready to merge. Runs tests, checks code quality, verifies documentation.

## Usage

```
/pre-merge [--skip-tests] [--ui-tests] [--full-tests]
```

**Flags**:
- `--skip-tests` - Skip test execution (validation only)
- `--ui-tests` - Include UI integration tests (default: unit tests only)
- `--full-tests` - Run all tests regardless of what changed

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

### Step 3: Build & Test (single script call)

```powershell
# Auto-detected scope
.\Radoub.IntegrationTests\run-tests.ps1 -Tool [detected] [-SkipShared] -UnitOnly -TechDebt

# With --ui-tests flag (omit -UnitOnly)
.\Radoub.IntegrationTests\run-tests.ps1 -Tool [detected] [-SkipShared] -TechDebt

# With --full-tests flag
.\Radoub.IntegrationTests\run-tests.ps1 -TechDebt
```

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
| UI tests | ⏭️ / ✅ N passed |

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

- Default: unit tests only (fast)
- UI tests require `--ui-tests` (slower, hands-off keyboard)
- Shared library changes → include shared tests
- Wiki updates done separately with `/documentation`
