# GitHub Setup Guide

Quick reference for setting up GitHub issue tracking and workflows for LNS_DLG.

## Initial Repository Setup

### 1. Create Labels
```bash
# Using GitHub CLI
gh label create --file .github/labels.yml
```

Or manually in GitHub UI: Settings → Labels → New label

### 2. Enable Actions
Settings → Actions → General → Allow all actions

### 3. Configure Branch Protection
Settings → Branches → Add rule:
- Branch name pattern: `main`
- Require pull request before merging
- Require status checks to pass (PR Build Check, PR Test Suite)

## Using Issue Templates

### Create Bug Report
1. Go to Issues → New Issue
2. Select "Bug Report" template
3. Fill in:
   - Testing Phase (Phase 0-6)
   - Priority (Critical/High/Medium/Low)
   - Description & repro steps
   - Check testing boxes if applicable

### Create Testing Checklist
1. Go to Issues → New Issue
2. Select "Testing Checklist" template
3. Fill in:
   - Phase being tested
   - Build/commit hash
   - Test files used
4. Check off items as you test
5. Update results summary

## Workflow Usage

### Pull Request Workflow
```bash
# Create feature branch
git checkout -b feature/my-feature

# Make changes, commit
git add .
git commit -m "feat: Add feature description"

# Push and create PR
git push -u origin feature/my-feature
gh pr create --base develop --title "Add feature description"
```

GitHub Actions will automatically:
- Build the solution
- Run all tests
- Report status on PR

### Release Workflow
```bash
# Tag release version
git tag v1.0.0
git push --tags
```

GitHub Actions will automatically:
- Build release version
- Run full test suite
- Create GitHub release
- Attach binaries

## GitHub MCP Integration

Using foxxy-bridge, you can interact with GitHub from Claude Code:

### Create Issue from Chat
```
Create GitHub issue:
Title: Copy/paste crashes with circular references
Phase: phase-1
Priority: priority-high
Labels: bug, aurora-compatibility
```

### Link Commits to Issues
In commit messages:
```bash
git commit -m "fix: Prevent crash on circular paste

Fixes #123
- Added circular reference detection
- Added validation before paste operation
- Updated tests for edge cases"
```

### Query Issues
```
Show open bugs for phase-1
Show testing checklists
Show issues labeled priority-critical
```

## Quick Reference

### Label Syntax
- Priority: `priority-critical`, `priority-high`, `priority-medium`, `priority-low`
- Phase: `phase-0`, `phase-1`, `phase-2`, etc.
- Type: `bug`, `enhancement`, `aurora-compatibility`, `testing`
- Status: `needs-retest`, `verified-fixed`, `blocked`

### Useful GitHub CLI Commands
```bash
# List open issues
gh issue list

# View specific issue
gh issue view 123

# Close issue
gh issue close 123 --comment "Fixed in commit abc123"

# List PRs
gh pr list

# View PR checks
gh pr checks

# Merge PR
gh pr merge 123 --squash
```

## Troubleshooting

### Actions Not Running
- Check Settings → Actions → General → Actions permissions
- Verify workflow files are in `.github/workflows/`
- Check workflow syntax with `gh workflow view`

### Labels Not Appearing
- Verify `.github/labels.yml` syntax
- Run `gh label create --file .github/labels.yml` manually
- Check repo settings for label creation permissions

### PR Checks Failing
- View logs: `gh run view` or in GitHub Actions tab
- Common issues:
  - .NET SDK version mismatch
  - Missing dependencies
  - Test failures in TestingTools/

## Monorepo Future Planning

When adding JournalEditor:
```
neverwinter-editors/
├── ArcReactor/              # Conversation editor (current LNS_DLG)
├── JournalEditor/           # Future journal editor
├── Shared/                  # Shared libraries
│   ├── AuroraParser/
│   └── UI/
├── Testing/4C/              # Unified testing
└── .github/                 # Shared issue tracking & workflows
```

Labels, workflows, and issue templates will work across both editors.
