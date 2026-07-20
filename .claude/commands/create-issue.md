# Create Issue

Create a well-structured GitHub issue with consistent formatting, correct labels, and the
right tool prefix.

## Usage

```
/create-issue [description]
```

With no description, prompts for details.

## Phase 1 — Gather

Extract what you can from the description, then ask for the rest: tool, type, title, and
optional body.

| Description contains | Type | Title prefix | Labels |
|----------------------|------|--------------|--------|
| crash, broken, error, fails, wrong | bug | `fix:` | `bug`, `[tool]` |
| add, new, improve, support | enhancement | `feat:` | `enhancement`, `[tool]` |
| refactor, cleanup, split, rename, dead code | tech-debt | `refactor:` | `tech-debt`, `[tool]` |

A tool name in the description sets the tool. Ask when ambiguous.

Tool labels are lowercase: `parley`, `manifest`, `quartermaster`, `fence`, `relique`,
`reliquary`, `trebuchet`, `radoub`.

## Phase 2 — Check for duplicates

Always search before creating.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[key terms]"
```

The cache holds only OPEN issues, so a closed duplicate will not appear. When the search hints
at related work, verify live state:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "N,N"
```

Report matches and ask whether to continue:

```markdown
### Potentially Related Issues

| # | Title | State |
|---|-------|-------|
| #123 | [Similar title] | OPEN |

Create anyway? [y/n]
```

No matches: proceed silently.

## Phase 3 — Create

Title format: `[Tool] type: Description`, under 80 characters.

- `[Fence] fix: Crash when opening UTM with empty inventory`
- `[Quartermaster] feat: Add portrait preview panel`
- `[Radoub] refactor: Split oversized MainWindowViewModel`

The `[Tool]` prefix and the tool label must always agree.

```bash
gh issue create --title "[Tool] type: Title" --label "type-label,tool-label" --body "$(cat <<'EOF'
## Problem

[What is wrong or missing]

## Expected Behavior

[What should happen — omit when obvious for an enhancement]

## Context

[How it was found, affected files, related issues]

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

Tech-debt issues use **Current State** / **Proposed Change** / **Files Affected** instead.

**Before creating**: run the drafted body through the concision pass
(`elements-of-style:writing-clearly-and-concisely`). See Commit & PR Standards in CLAUDE.md.

**Long or pattern-heavy bodies**: stage the text in `NonPublic/_scratch/` and pass
`--body-file`. Heredocs are fine for short bodies, but a body containing `..`, quotes, or
backticks can trip the permission guard, and `--body-file` sidesteps the escaping entirely.

## Phase 4 — Refresh and report

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

```markdown
## Issue Created

**Issue**: #[number] — [title]
**Labels**: [labels]
**URL**: https://github.com/LordOfMyatar/Radoub/issues/[number]
```

## Notes

- Keep bodies proportional — a straightforward issue does not need documentation.
- When something surfaces during other work, say so ("Found during #1234") and note what was
  ruled out. A report that lists what is *not* the cause saves the next person the same dead
  ends.
