# Create Issue

Create a well-structured GitHub issue with consistent formatting, correct labels, and proper tool prefixes.

## Usage

```
/create-issue [description]
```

**Examples:**
- `/create-issue Fence crashes when opening UTM with empty inventory`
- `/create-issue Add portrait preview to Quartermaster`
- `/create-issue` (interactive — will prompt for details)

## Workflow

### Step 1: Gather Information

If the user provided a description, extract what you can. Otherwise, ask for:

1. **Tool**: Which tool? (Parley, Manifest, Quartermaster, Fence, Relique, Trebuchet, Radoub)
2. **Type**: What kind of issue?
   - `bug` — Something is broken
   - `enhancement` — New feature or improvement
   - `tech-debt` — Code quality, refactoring, cleanup
3. **Title**: Brief description of the issue
4. **Body**: Details (optional — can be minimal for simple issues)

**Auto-detection from description**:
- Words like "crash", "broken", "error", "fails", "wrong" → `bug`
- Words like "add", "new", "improve", "support" → `enhancement`
- Words like "refactor", "cleanup", "split", "rename", "dead code" → `tech-debt`
- Tool name in description → that tool
- If ambiguous, ask

### Step 2: Check for Duplicates

Search the cache for similar issues before creating:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "[key terms from title]"
```

If similar issues found, report them:

```markdown
### Potentially Related Issues

| # | Title | State |
|---|-------|-------|
| #123 | [Similar title] | OPEN |

Create anyway? [y/n]
```

If no matches, proceed silently.

### Step 3: Format and Create

**Title format**: `[Tool] type: Description`

Examples:
- `[Fence] fix: Crash when opening UTM with empty inventory`
- `[Quartermaster] feat: Add portrait preview panel`
- `[Radoub] refactor: Split oversized MainWindowViewModel`

**Type prefixes** (in title):
| Issue Type | Title Prefix |
|------------|-------------|
| bug | `fix:` |
| enhancement | `feat:` |
| tech-debt | `refactor:` |

**Labels**:
| Issue Type | Labels |
|------------|--------|
| bug | `bug`, `[tool]` |
| enhancement | `enhancement`, `[tool]` |
| tech-debt | `tech-debt`, `[tool]` |

Tool labels are lowercase: `parley`, `manifest`, `quartermaster`, `fence`, `relique`, `trebuchet`, `radoub`

**Body template**:

```bash
gh issue create --title "[Tool] type: Title" --label "type-label,tool-label" --body "$(cat <<'EOF'
## Problem

[What's wrong or what's missing]

## Expected Behavior

[What should happen instead — omit for enhancements if obvious]

## Context

[Any relevant details: how discovered, affected files, related issues]

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

For **tech-debt** issues, use this body instead:

```markdown
## Current State

[What exists now and why it's a problem]

## Proposed Change

[What should be done]

## Files Affected

- `path/to/file.cs`

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
```

### Step 4: Refresh Cache

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

### Step 5: Report

```markdown
## Issue Created

**Issue**: #[number] — [title]
**Labels**: [labels]
**URL**: https://github.com/LordOfMyatar/Radoub/issues/[number]
```

## Notes

- Always search for duplicates before creating
- Keep titles concise (under 80 characters)
- Body can be minimal for straightforward issues — don't over-document
- The `[Tool]` prefix and labels must always match
- For issues discovered during active development, mention the context (e.g., "Found during #1234")
