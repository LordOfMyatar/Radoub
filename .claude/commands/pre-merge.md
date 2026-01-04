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

### Step 4: CHANGELOG Validation

Read CHANGELOG and verify:
- Version section exists
- PR number filled in (not TBD)
- Date is today or earlier

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
    -Tool [Parley|Quartermaster|Manifest]
    -SkipShared      # Skip Radoub.* tests
    -UnitOnly        # Skip UI tests (default for pre-merge)
    -TechDebt        # Include large file scan
```

## Notes

- Default: unit tests only (fast)
- UI tests require `--ui-tests` (slower, hands-off keyboard)
- Shared library changes → include shared tests
- Wiki updates done separately with `/documentation`
