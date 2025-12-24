# Post-Merge Cleanup

Perform cleanup tasks after a PR has been merged to main.

## Usage

```
/post-merge
/post-merge #[pr-number]
```

If no PR number provided, uses the most recently merged PR.

## Workflow

### Step 1: Identify Merged PR

```bash
# Get current branch (should be main after merge)
git branch --show-current

# Find most recent merge commit
git log --merges -1 --format="%H %s"

# Or if PR number provided
gh pr view [number] --json mergedAt,mergeCommit,number,title
```

Verify the PR was actually merged (not closed without merge).

### Step 2: Update Local Repository

```bash
# Ensure we're on main with latest
git checkout main
git pull origin main
```

### Step 3: Clean Up Feature Branch

Check if feature branch should be deleted:
```bash
# List merged branches
git branch --merged main

# Check if remote branch was deleted
git fetch --prune
```

**Ask user before deleting:**
> "The feature branch `[branch-name]` has been merged. Would you like me to delete the local branch?"

Only delete if user confirms:
```bash
git branch -d [branch-name]
```

If user declines, note in summary that branch was kept.

### Step 4: Run Full Test Suite and Privacy Check

**⚠️ IMPORTANT: UI TESTS REQUIRE HANDS-OFF KEYBOARD ⚠️**

Before running tests, display this warning to the user:

> **🚨 HANDS OFF KEYBOARD AND MOUSE 🚨**
>
> UI tests (FlaUI) will be launching applications and simulating input.
> **Do NOT touch the keyboard or mouse** until tests complete.
> Any input during UI tests may cause failures.
>
> Press Enter when ready, or Ctrl+C to skip tests.

**Run Privacy Scan First**:
```bash
# Search for potential path leaks in all source directories
grep -r "C:\\Users" --include="*.cs" Parley/ Manifest/ Radoub.Formats/ Radoub.Dictionary/ || echo "No hardcoded Windows paths found"
grep -r "/Users/" --include="*.cs" Parley/ Manifest/ Radoub.Formats/ Radoub.Dictionary/ || echo "No hardcoded Unix paths found"
```

If privacy issues found, **STOP and report to user** before continuing.

**Run Tests Based on OS**:

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

If tests fail, **ask user if they want to proceed** with post-merge cleanup or stop to investigate.

### Step 5: Verify CHANGELOG

Read the CHANGELOG and confirm:
- Version section has correct date (should be merge date or earlier)
- PR number matches the merged PR
- All changes from the PR are documented

If date was TBD, warn user to update it.

### Step 6: Create GitHub Release (if applicable)

Check if this merge warrants a release:
- New features (`feat:` commits)
- Breaking changes
- Significant bug fixes
- Version bump in CHANGELOG

If release criteria met, **ask user:**
> "This merge includes [features/fixes]. Would you like me to run `/release` to create a GitHub release for v[version]?"

If user confirms, invoke the release command:
```
/release
```

The `/release` command handles:
- Extracting CHANGELOG section for release notes
- Creating GitHub release with proper tagging
- Generating release assets if configured

If user declines, note in summary that release was skipped.

### Step 7: Verify Related Issues

**Extract issues referenced in PR body:**
```bash
# Find "closes #X", "fixes #X", or "resolves #X" in PR body
gh pr view [number] --json body -q '.body' | grep -oEi "(closes|fixes|resolves) #[0-9]+" | grep -oE "[0-9]+"
```

**For each issue number found, check its state:**
```bash
gh issue view [issue-number] --json state,title -q '{state: .state, title: .title}'
```

**Report status to user:**
- If CLOSED: ✅ Issue was auto-closed by merge
- If OPEN: ⚠️ Issue is still open

**If issues are still OPEN, ask user:**
> "Issue #[number] '[title]' is still open. Would you like me to close it with a comment referencing PR #[pr-number]?"

Only close if user confirms:
```bash
gh issue close [issue-number] --comment "Completed in PR #[pr-number]"
```

**Note**: Don't auto-close issues. They may be intentionally left open for deferred work or have multiple parts.

### Step 8: Update Parent Epic (if applicable)

**Extract parent epic number from PR body:**
```bash
# Look for "Parent Epic": #XXX or "Epic": #XXX patterns
gh pr view [number] --json body -q '.body' | grep -oEi "(parent )?epic[^#]*#[0-9]+" | grep -oE "[0-9]+" | head -1
```

If an epic number is found:

1. **View current epic state:**
   ```bash
   gh issue view [epic-number] --json body,title,state -q '{title: .title, state: .state}'
   ```

2. **Ask user about epic update:**
   > "Found parent epic #[number]: '[title]'. Would you like me to:
   > - Add a completion comment for this sprint?
   > - Update the epic checklist (if applicable)?"

3. **If user confirms comment**, add completion note:
   ```bash
   gh issue comment [epic-number] --body "Sprint completed via PR #[pr-number]: [PR title]"
   ```

4. **If user wants checklist updates**, read the epic body and:
   - Mark completed sprint/phase as `[x]`
   - Update status sections
   - Add implementation notes for what was delivered
   ```bash
   gh issue edit [epic-number] --body "$(cat <<'EOF'
   [Updated epic body with checked items and notes]
   EOF
   )"
   ```

**Note**: Always ask before modifying the epic. Some sprints may be partial completions or the user may prefer to batch updates.

### Step 9: Sync Wiki (if applicable)

If wiki updates were made during the PR but not yet pushed:

1. **Check wiki repo status:**
   ```bash
   cd d:\LOM\workspace\Radoub.wiki
   git status
   ```

2. **If uncommitted changes exist, ask user:**
   > "The wiki has uncommitted changes. Would you like me to commit and push them?"

3. **If user confirms:**
   ```bash
   cd d:\LOM\workspace\Radoub.wiki
   git add .
   git commit -m "Update wiki for PR #[number]

   🤖 Generated with [Claude Code](https://claude.com/claude-code)

   Co-Authored-By: Claude Opus 4.5 <noreply@anthropic.com>"
   git push
   ```

4. **Verify wiki pages are accessible:**
   - Check https://github.com/LordOfMyatar/Radoub/wiki for updated pages
   - Report any sync issues to user

**Wiki Repo Location**: `d:\LOM\workspace\Radoub.wiki\`

### Step 10: Notify About Follow-ups

Check for:
- TODO comments added in this PR
- Issues created during development
- Deferred work mentioned in PR comments

Report any follow-up items to user.

### Step 11: Append Test Results to PR

Update the PR description with test results from Step 4:

```bash
gh pr edit [number] --body "$(cat <<'EOF'
[existing PR body content]

---

## Post-Merge Test Results

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

[If failures occurred, list failed test names here]

---

🤖 Test results appended by Claude Code post-merge
EOF
)"
```

**Note**: Radoub.UITests shows ⏭️ on Linux/macOS since FlaUI is Windows-only.

### Step 12: Generate Summary

Output format:

```markdown
## Post-Merge Summary

**PR**: #[number] - [title]
**Merged**: [date/time]
**Version**: [version from CHANGELOG]

---

### Test Results

| Project | Status | Passed | Failed |
|---------|--------|--------|--------|
| Radoub.Formats.Tests | ✅/❌ | N | N |
| Radoub.Dictionary.Tests | ✅/❌ | N | N |
| Parley.Tests | ✅/❌ | N | N |
| Manifest.Tests | ✅/❌ | N | N |
| Radoub.UITests | ✅/❌/⏭️ | N | N |

**Privacy Scan**: ✅/❌

---

### Cleanup Completed

- [x/⏭️] Local branch: `[branch-name]` [deleted / kept per user request]
- [x] Remote branch pruned
- [x] CHANGELOG verified
- [x] Tests passed / ⚠️ Tests had failures
- [x] Test results appended to PR
- [x] Related issues closed
- [x] Parent epic updated (if applicable)
- [x/⏭️] Wiki synced: [✅ Pushed / ⏭️ No changes / ⏭️ User declined]

### Release Status

[✅ Release created via /release: v[version] / ⏭️ User declined release / ⏳ No release needed]

### Follow-up Items

| Type | Description | Action |
|------|-------------|--------|
| TODO | [location] - [text] | Create issue |
| Issue | #[number] | Needs attention |
| Deferred | [description] | Future PR |

### Next Steps

1. [Any remaining manual tasks]
2. [Suggested next issues to work on]
```

## Flags

- `--keep-branch`: Don't delete the local feature branch
- `--skip-tests`: Skip test execution (Step 4)
- `--create-release`: Force creation of GitHub release
- `--no-release`: Skip release consideration
- `--verbose`: Show detailed git operations

## Notes

- Run this after confirming PR was merged successfully
- Safe to run multiple times (idempotent operations)
- Branch deletion is local only by default
- Release creation requires appropriate permissions
- **UI tests (FlaUI)**: Windows-only, require hands-off keyboard/mouse during execution
- **Linux/macOS**: Only unit tests run (FlaUI not available)
