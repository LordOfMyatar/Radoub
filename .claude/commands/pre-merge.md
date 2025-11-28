# Pre-Merge Checklist

Analyze the current PR and generate a comprehensive pre-merge checklist with automated checks.

## Usage

```
/pre-merge
```

Runs against the current branch, comparing to main.

## Workflow

### Step 1: Identify Current PR

```bash
git branch --show-current
gh pr view --json number,title,state,baseRefName -q '.'
```

If no PR exists, warn user and suggest creating one first.

### Step 2: Analyze Changes

Get list of changed files:
```bash
git diff main...HEAD --name-only
```

Categorize changes:
- **UI files**: `*.axaml`, `*.axaml.cs`, `Views/`, `ViewModels/`
- **Logic files**: `*.cs` (non-UI)
- **Test files**: `*Tests.cs`, `*Tests/`
- **Documentation**: `*.md`, `Documentation/`
- **Config/Build**: `*.csproj`, `*.sln`, `*.json`

### Step 3: Determine Required Tests

Based on changed files:

| Changed | Tests Required |
|---------|---------------|
| `Parley/Parley/Views/*.axaml` | UI tests (Radoub.UITests) |
| `Parley/Parley/ViewModels/*.cs` | UI tests + manual verification |
| `Radoub.Formats/**/*.cs` | Unit tests (Radoub.Formats.Tests) |
| `*.md` only | No automated tests |

### Step 4: Run Automated Checks

**Privacy Scan** - Check for hardcoded paths:
```bash
# Search for potential path leaks
grep -r "C:\\\\Users" --include="*.cs" Parley/ Radoub.Formats/ || echo "No hardcoded paths found"
grep -r "/Users/" --include="*.cs" Parley/ Radoub.Formats/ || echo "No hardcoded paths found"
```

**CHANGELOG Validation**:
```bash
# Check if CHANGELOG has been updated
git diff main...HEAD --name-only | grep -E "CHANGELOG.md"
```

Read the CHANGELOG and verify:
- Version section exists for this PR
- Branch name matches current branch
- PR number is filled in (not TBD)
- Date is set (for release-ready) or TBD (for in-progress)

**Build Check**:
```bash
dotnet build Parley/Parley
dotnet build Radoub.Formats/Radoub.Formats
```

**Test Execution** (based on Step 3 analysis):
```bash
# If UI changes detected
dotnet test Radoub.UITests

# If format library changes detected
dotnet test Radoub.Formats/Radoub.Formats.Tests
```

### Step 5: Documentation Check

Scan for:
- New public APIs without XML docs
- README updates needed for new features
- User-facing changes that need documentation

### Step 6: Generate Checklist

Output format:

```markdown
## Pre-Merge Checklist for PR #[number]

**Branch**: [branch-name]
**Title**: [PR title]
**Changed Files**: [count]

---

### Testing Required

| Test Suite | Needed | Status |
|------------|--------|--------|
| Radoub.UITests | [Yes/No] | [✅ Passed / ❌ Failed / ⏳ Not Run] |
| Radoub.Formats.Tests | [Yes/No] | [✅ Passed / ❌ Failed / ⏳ Not Run] |
| Manual Testing | [Yes/No] | [ ] Verify: [specific items] |

---

### Code Quality

- [x/⚠️] No hardcoded paths found
- [x/⚠️] Build succeeds
- [x/⚠️] No TODO comments in changed files

---

### Documentation

- [x/⚠️] CHANGELOG updated
  - [ ] Version section exists
  - [ ] PR number filled in
  - [ ] Date set (if ready for release)
- [x/⚠️] README updates: [Needed/Not needed]
- [x/⚠️] User docs: [Needed/Not needed]

---

### Files Changed by Category

**UI** ([count]):
- [file list]

**Logic** ([count]):
- [file list]

**Tests** ([count]):
- [file list]

**Docs** ([count]):
- [file list]

---

### Action Items

1. [Any failing checks or missing items]
2. [Specific things to verify manually]

### Ready to Merge?

[✅ All checks pass - ready for review / ⚠️ [N] items need attention]
```

## Flags

- `--run-tests`: Actually execute tests (default: just check if needed)
- `--fix-changelog`: Attempt to auto-fill CHANGELOG PR number
- `--verbose`: Show detailed output for each check

## Notes

- This command is advisory - it doesn't block merging
- Some checks require human judgment (documentation quality, etc.)
- Failed checks should be addressed before requesting review
