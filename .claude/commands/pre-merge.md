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

**Privacy Scan**: Included in test suite (run-tests.ps1). No manual step needed.

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

**Run Full Test Suite**:

**⚠️ IMPORTANT: UI TESTS REQUIRE HANDS-OFF KEYBOARD ⚠️**

Before running tests, display this warning to the user:

> **🚨 HANDS OFF KEYBOARD AND MOUSE 🚨**
>
> UI tests (FlaUI) will be launching applications and simulating input.
> **Do NOT touch the keyboard or mouse** until tests complete.
> Any input during UI tests may cause failures.
>
> Press Enter when ready, or Ctrl+C to skip tests.

**Windows** (full suite including UI tests):
```powershell
# Run from repository root
.\Radoub.UITests\run-tests.ps1
```

**Linux/macOS** (unit tests only - FlaUI is Windows-only):
```bash
# Run from repository root
./Radoub.UITests/run-tests.sh
```

**Capture Test Results** for PR update:
- Total tests run
- Passed count
- Failed count
- Any failed test names

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
- **CODE_PATH_MAP updates** (see below)

**README Currency Check**:

Verify that README files reflect the current state:

| Check | How to Verify |
|-------|---------------|
| Version matches CHANGELOG | Compare version in README to latest CHANGELOG entry |
| Feature list current | New features in CHANGELOG should appear in README |
| No outdated information | Screenshots, examples, and paths should be current |

Files to check:
- `README.md` (main repository) - Check Parley/Manifest versions
- `Parley/README.md` - Check version and feature list
- `Manifest/README.md` (if exists) - Check version and feature list

**CLAUDE.md Currency Check**:

If the PR includes architectural changes, verify CLAUDE.md files are updated:

| Change Type | CLAUDE.md Update Needed |
|-------------|------------------------|
| New tool/subdirectory | ✅ Update Radoub CLAUDE.md structure |
| New code patterns | ✅ Document in tool-specific CLAUDE.md |
| New slash commands | ✅ Document in relevant CLAUDE.md |
| Shared library changes | ✅ Update Radoub CLAUDE.md |
| New development workflow | ✅ Add to appropriate CLAUDE.md |
| Bug fixes only | ❌ Not usually needed |
| Test changes only | ❌ Not usually needed |

Files to check:
- `CLAUDE.md` (main repository)
- `Parley/CLAUDE.md`
- Tool-specific CLAUDE.md files if applicable

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
| Parley architecture | [Parley-Developer-Architecture](Parley-Developer-Architecture) |
| Manifest architecture | [Manifest-Developer-Architecture](Manifest-Developer-Architecture) |
| Testing changes | [Parley-Developer-Testing](Parley-Developer-Testing) |
| Delete behavior | [Parley-Developer-Delete-Behavior](Parley-Developer-Delete-Behavior) |
| Copy/Paste | [Parley-Developer-CopyPaste](Parley-Developer-CopyPaste) |
| Scrap/Orphan system | [Parley-Developer-Scrap-System](Parley-Developer-Scrap-System) |

Wiki repo location: `d:\LOM\workspace\Radoub.wiki\`

**Wiki Freshness Dates**:

Developer wiki pages have a freshness date at the bottom. When updating a wiki page:
1. Update the `*Page freshness: YYYY-MM-DD*` line at the bottom
2. Commit and push the wiki changes

### Step 7: Generate Checklist

Output format:

```markdown
## Pre-Merge Checklist for PR #[number]

**Branch**: [branch-name]
**Title**: [PR title]
**Changed Files**: [count]

---

### Test Results

| Project | Status | Passed | Failed |
|---------|--------|--------|--------|
| Radoub.Formats.Tests | ✅/❌ | N | N |
| Radoub.Dictionary.Tests | ✅/❌ | N | N |
| Parley.Tests | ✅/❌ | N | N |
| Manifest.Tests | ✅/❌ | N | N |
| Radoub.UITests | ✅/❌/⏭️ | N | N |

**Total**: Passed N, Failed N
**Manual Testing**: [ ] Verify: [specific items if needed]

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
- [x/⚠️] README current: [✅ Up to date / ⚠️ Needs update]
  - [ ] Version matches CHANGELOG
  - [ ] Feature list reflects current state
- [x/⚠️] CLAUDE.md current: [✅ Up to date / ⚠️ Needs update / N/A]
  - [ ] Architectural changes documented
  - [ ] New workflows documented
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

**Privacy Scan**: ✅ No hardcoded paths found

**Test Suite**: [Windows / Linux/macOS]

| Project | Status | Passed | Failed |
|---------|--------|--------|--------|
| Radoub.Formats.Tests | ✅/❌ | N | N |
| Radoub.Dictionary.Tests | ✅/❌ | N | N |
| Parley.Tests | ✅/❌ | N | N |
| Manifest.Tests | ✅/❌ | N | N |
| Radoub.UITests | ✅/❌/⏭️ | N | N |

**Total**: Passed N, Failed N

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

**Note**: Radoub.UITests shows ⏭️ on Linux/macOS since FlaUI is Windows-only.

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

### Step 10: Mark PR Ready for Review

If the PR is still in draft state, mark it ready for review:

```bash
# Check if PR is draft
gh pr view --json isDraft -q '.isDraft'

# If draft, mark as ready
gh pr ready [number]
```

This removes the draft status and signals the PR is ready for final review and merge.

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
