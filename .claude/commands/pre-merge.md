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
grep -r "C:\\Users" --include="*.cs" Parley/ Radoub.Formats/ || echo "No hardcoded paths found"
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
- Date is valid:
  - ✅ Today's date or earlier (most PRs merge same day)
  - ⚠️ TBD - warn user to set date before merge
  - ❌ Future date - likely error, warn user

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

### Step 5: Code Review

Run built-in review commands:

**Security Review**:
```
/security-review
```
Checks for: injection vulnerabilities, auth bypass, hardcoded secrets, path traversal, etc.

**General Code Review**:
```
/review
```
Checks for: logic errors, code quality, best practices, potential bugs.

Document findings in the checklist output.

### Step 6: Documentation Check

Scan for:
- New public APIs without XML docs
- README updates needed for new features
- User-facing changes that need wiki documentation
- Claude file updates
- Code_Path_Map updates

**Wiki Updates** (https://github.com/LordOfMyatar/Radoub/wiki):

If changes affect user-facing features, check if wiki pages need updates:

| Change Type | Wiki Pages to Review |
|-------------|---------------------|
| UI/Settings | [Settings](Settings), [Themes](Themes) |
| Dialog editing | [Editing-Dialogs](Editing-Dialogs), [Properties-Panel](Properties-Panel) |
| Scripts | [Script-Browser](Script-Browser) |
| Sound | [Sound-Browser](Sound-Browser) |
| Plugins | [Using-Plugins](Using-Plugins), [Plugin-Development](Plugin-Development) |
| Keyboard shortcuts | [Keyboard-Shortcuts](Keyboard-Shortcuts) |
| Installation | [Getting-Started](Getting-Started) |
| New features | [Home](Home), [Index](Index) |

Wiki repo location: `d:\LOM\workspace\Radoub.wiki\`

### Step 7: Generate Checklist

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

### Code Review

- [x/⚠️] Security review: [✅ No issues / ⚠️ [N] findings]
- [x/⚠️] General review: [✅ No issues / ⚠️ [N] findings]

---

### Documentation

- [x/⚠️] CHANGELOG updated
  - [ ] Version section exists
  - [ ] PR number filled in
  - [ ] Date is today or earlier (not TBD, not future)
- [x/⚠️] README updates: [Needed/Not needed]
- [x/⚠️] Wiki updates: [Needed/Not needed]
  - [ ] Pages reviewed: [list affected pages]

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

### Step 8: Update PR and Epic Content

After generating the checklist, update the PR description on GitHub:

```bash
gh pr edit [number] --body "$(cat <<'EOF'
## Summary
[Brief description from CHANGELOG]

## Changes
[Categorized file list]

## Test Results
- Unit Tests: [✅/❌] [count] passed
- UI Tests: [✅/❌/⏳] [status]

## Checklist
- [x] Build passes
- [x] Tests pass
- [x] CHANGELOG updated
- [x] Wiki updated (if user-facing changes)
- [x] No hardcoded paths

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

**Also check if the related Epic issue needs updating**:
- If this PR closes sprint work, update the Epic with completion status
- If new issues were discovered/created during the sprint, link them to the Epic
- Update Epic checklist items if applicable

```bash
# View Epic to check current state
gh issue view [epic-number]

# Update Epic with sprint completion notes if needed
gh issue comment [epic-number] --body "Sprint [name] completed via PR #[number]. [any notes]"
```

### Step 9: Commit and Push

After updating PR/Epic content, commit and push any local changes:

```bash
git status
# If there are uncommitted changes (e.g., CHANGELOG date fixes, documentation updates)
git add -A
git commit -m "chore: Pre-merge updates"
git push
```

## Flags

- `--run-tests`: Actually execute tests (default: just check if needed)
- `--fix-changelog`: Attempt to auto-fill CHANGELOG PR number
- `--verbose`: Show detailed output for each check
- `--update-pr`: Update PR description with checklist results

## Prerequisites

**IMPORTANT**: Before running `/pre-merge`:
- All changes must be **committed** to the feature branch
- The branch must be **pushed** to the remote repository
- A PR must exist (or be created) for the branch

The pre-merge checklist validates the current PR state on GitHub, not local uncommitted changes.

## Notes

- This command is advisory - it doesn't block merging
- Some checks require human judgment (documentation quality, etc.)
- Failed checks should be addressed before requesting review
