# Pre-Merge Checklist

Validate the current PR is ready to merge. Runs tests, checks code quality, verifies documentation.

**MAINTAINABILITY IS A HIGH PRIORITY.** We learned hard lessons from Parley's MainWindow growing into an untestable monolith. Every PR should leave the codebase cleaner than we found it. Don't merge technical debt - fix it or create issues.

## Upfront Questions

**IMPORTANT**: Gather ALL required user input at the start, then execute autonomously.

Before running any checks, collect these answers in ONE interaction:

1. **Test Execution**: "Ready to run tests? UI tests require hands-off keyboard/mouse for ~5 minutes." [yes/skip/tool-specific]
2. **Related Epic** (if not auto-detected): "Is this work part of an epic? Enter # or skip."

After collecting answers, proceed through all steps without further prompts unless errors occur.

## Usage

```
/pre-merge
```

Runs against the current branch, comparing to main.

## Workflow

### Step 1: Identify Current PR

```bash
git branch --show-current
gh pr view --json number,title,state,baseRefName,isDraft -q '.'
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
| `Parley/Parley/**/*.cs` | Parley.Tests + UI tests |
| `Manifest/Manifest/**/*.cs` | Manifest.Tests |
| `Quartermaster/Quartermaster/**/*.cs` | Quartermaster.Tests |
| `Radoub.Formats/**/*.cs` | Radoub.Formats.Tests |
| `*.md` only | No automated tests |

### Step 4: Run Automated Checks

**Build Check**:
```bash
dotnet build [affected-project] --no-restore
```

**Run Tests** (based on user's answer):
```bash
dotnet test [affected-project].Tests
```

For UI tests (Windows only, requires hands-off):
```powershell
.\Radoub.IntegrationTests\run-tests.ps1
```

**Privacy Scan** (check for hardcoded paths):
```bash
git diff main...HEAD --name-only | xargs grep -l "C:\\Users\|D:\\LOM\|/home/" 2>/dev/null
```

### Step 5: CHANGELOG Validation

```bash
git diff main...HEAD --name-only | grep -E "CHANGELOG.md"
```

Read the CHANGELOG and verify:
- Version section exists for this PR
- Branch name matches current branch
- PR number is filled in (not TBD)
- Date is valid:
  - ✅ Today's date or earlier
  - ⚠️ TBD - warn user to set date before merge
  - ❌ Future date - likely error

### Step 6: Technical Debt Scan

**Check existing tech debt issues**:
```bash
gh issue list --label "tech-debt" --state open --json number,title
```

**Scan changed files** for:

| Issue | Threshold | Action |
|-------|-----------|--------|
| Large files | >500 lines | Create issue or fix |
| Large methods | >50 lines | Create issue or fix |
| Duplicated code | Obvious patterns | Extract or create issue |
| TODO comments | Any new ones | Should reference issue # |

```bash
# Count lines in changed CS files
git diff main...HEAD --name-only | grep "\.cs$" | while read f; do echo "$(wc -l < "$f") $f"; done | sort -rn
```

**Action based on debt size**:
1. **Small fixes** (< 15 min): Fix now
2. **Medium fixes** (15-60 min): Discuss - fix now or create issue?
3. **Large fixes** (> 1 hour): Create GitHub issue with `tech-debt` label

### Step 7: Documentation Validation

**Wiki freshness check** (validation only - updates should be done before running pre-merge):
```bash
cd d:\LOM\workspace\Radoub.wiki
# Check freshness dates on relevant pages
grep "Page freshness:" [Tool]-Developer-Architecture.md
```

If wiki page freshness is >30 days old and code changed in that area, flag as needing update.

**README check**:
- Version in README matches CHANGELOG
- Feature list reflects current state

**CLAUDE.md check**:
- New patterns documented
- New slash commands documented

### Step 8: Generate Checklist

Output format:

```markdown
## Pre-Merge Checklist for PR #[number]

**Branch**: [branch-name]
**Title**: [PR title]
**Changed Files**: [count]

---

### Build & Tests

| Check | Status |
|-------|--------|
| Build | ✅/❌ |
| [Project].Tests | ✅ N passed / ❌ N failed |
| Privacy scan | ✅ No hardcoded paths / ⚠️ Found |

---

### Code Quality

| Check | Status |
|-------|--------|
| Large files (>500 lines) | ✅ None / ⚠️ [list] |
| Tech debt issues | ✅ None / ⚠️ Created #xxx |

---

### Documentation

| Check | Status |
|-------|--------|
| CHANGELOG | ✅ Updated / ⚠️ Missing version/date |
| Wiki freshness | ✅ Current / ⚠️ Needs update |
| README | ✅ Current / N/A |

---

### Files Changed

**By Category**:
- Logic: [count] files
- UI: [count] files
- Tests: [count] files
- Docs: [count] files

---

### Action Items

1. [Any failing checks]
2. [Any warnings to address]

---

### Ready to Merge?

**Status**: ✅ Ready / ⚠️ [N] items need attention / ❌ Blocked: [reason]
```

### Step 9: Update PR Description

```bash
gh pr edit [number] --body "$(cat <<'EOF'
## Summary
[Brief description from CHANGELOG]

## Test Results
| Project | Passed | Failed |
|---------|--------|--------|
| [Project].Tests | N | N |

## Checklist
- [x] Build passes
- [x] Tests pass
- [x] CHANGELOG updated
- [x] No hardcoded paths

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### Step 10: Mark PR Ready (if draft)

```bash
gh pr view --json isDraft -q '.isDraft'
# If true:
gh pr ready [number]
```

## Notes

- This command validates readiness - it does NOT update wiki/docs
- Run `/documentation` separately if wiki updates are needed
- All changes must be committed and pushed before running
- Some checks require human judgment
