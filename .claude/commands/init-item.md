# Initialize Work Item

Start a branch for a GitHub issue. Detects the item type and applies the matching workflow.

## Usage

```
/init-item #[issue-number]
```

The issue number is required. Works for epics, sprints, features, and fixes alike.

**Gather all input up front, then run autonomously.** After fetching the issue, ask in one
interaction: which tool, if the labels are ambiguous; and for an epic, whether to research
first, continue anyway, or cancel. Then proceed through branch, CHANGELOG, commit, PR, and
board without further prompts.

## Phase 1 — Prepare

### 1.1 Validate and sync

```bash
git status
git fetch origin
git checkout main
git pull origin main
```

A dirty working directory stops the command — ask the user to commit or stash first.

### 1.2 Fetch the issue

Cache-first. Never call `gh issue view` for reads.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number [number]
```

Take the title (branch and PR name), labels (item type), body (context), and milestone.

### 1.3 Check for duplicates

Strip prefixes like `[Tool]`, `feat:`, `fix:` from the title and search the cache for the
remaining key terms:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "keyword" [-Tool parley]
```

Look for the same tool plus the same feature area, a repeated error description, overlapping
file paths, or an issue closed and refiled.

The cache holds only OPEN issues but searches body text, so a match may reference issues that
have since closed. Verify the target issue and every number its body calls out:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "1902,1903,1905"
```

Returns live `state` and `closedAt` per number. Use it to fail fast when the target issue is
already closed, to flag a sprint still listing closed children as open, and to tell the user
when a "duplicate" is actually finished work.

Report matches and ask whether to continue:

```markdown
### ⚠️ Potentially Related Issues Found

| # | Title | State |
|---|-------|-------|
| #123 | [Similar title] | OPEN |
| #456 | [Related work] | CLOSED (YYYY-MM-DD) |

Continue anyway? [y/n]
```

No matches: proceed silently.

### 1.4 Check for a pre-warmed plan

```bash
ls NonPublic/Plans/*-[number]-plan.md 2>/dev/null
```

If one exists, tell the user it was prepared in an earlier session and should be reviewed
before implementation. Otherwise continue silently.

## Phase 2 — Classify

### 2.1 Item type

First matching label wins:

| Label contains | Type |
|----------------|------|
| `epic` | Epic |
| `sprint` | Sprint |
| `bug`, `fix` | Fix |
| `refactor` | Refactor |
| `enhancement`, `feature` | Feature |
| none of the above | Feature |

Labels may be capitalized or namespaced (`Epic`, `type:epic`) — match case-insensitively.

### 2.2 Tool

From a tool label, a `[Tool]` title prefix, or the user when ambiguous.

### 2.3 Type-specific handling

**Epic** — stop and offer three choices: run `/research #[number]` now, continue with an epic
branch anyway (fine for simple epics), or cancel and plan first. Epics normally want research,
sprint-sized chunks, and an issue per sprint before code.

**Sprint** — one branch and one PR for the bundled work; the CHANGELOG groups every item under
a single version.

**Fix / Feature** — standard flow, PR titled `[Tool] Fix: [Title] (#N)` or
`[Tool] Feat: [Title] (#N)`.

## Phase 3 — Create

### 3.1 Branch

Always `[tool]/issue-[number]` — simple and predictable:

```bash
git checkout -b parley/issue-708
```

### 3.2 CHANGELOG

Edit the tool's CHANGELOG (shared-library work goes in the root one). Highlights only, no
checklists. **Never `[Unreleased]`** — add a versioned section as the first entry after the
header, dated today, since most PRs merge same-day.

```markdown
## [X.Y.Z-alpha] - YYYY-MM-DD
**Branch**: `[branch-name]` | **PR**: #TBD

### Sprint: [Title]

- Item 1
- Item 2

---
```

For an epic, feature, or fix use `### [Type]: [Title from GitHub]` in place of the sprint
heading and its bullet list.

### 3.3 Commit and push

```bash
git add [CHANGELOG file]
git commit -m "[tool] chore: Initialize [type] branch for #[number]"
git push -u origin [branch-name]
```

### 3.4 Draft PR

Always draft initially. Run the PR body through the concision pass
(`elements-of-style:writing-clearly-and-concisely`) before creating it — see Commit & PR
Standards in CLAUDE.md.

```bash
gh pr create --draft --title "[Tool] [Type]: [Title]" --body "$(cat <<'EOF'
## Summary

[Brief description - will be updated]

## Related Issues

- Closes #[number]

## Checklist

- [ ] Implementation complete
- [ ] Tests added/updated
- [ ] CHANGELOG updated with date
- [ ] Documentation updated (if needed)

---

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

### 3.5 Backfill the PR number

```bash
git add [CHANGELOG file]
git commit -m "[tool] chore: Add PR number to CHANGELOG"
git push
```

### 3.6 Project board (sprints and epics only)

Features and fixes do not go on the board unless the user asks. Everything uses Radoub
project #3.

```bash
ITEM_JSON=$(gh project item-add 3 --owner LordOfMyatar --url https://github.com/LordOfMyatar/Radoub/issues/[number] --format json)
ITEM_ID=$(echo "$ITEM_JSON" | gh api --jq '.id' 2>/dev/null || echo "$ITEM_JSON" | powershell.exe -NoProfile -Command '$input | ConvertFrom-Json | Select-Object -ExpandProperty id')

gh project item-edit \
  --id "$ITEM_ID" \
  --project-id PVT_kwHOAotjYs4BHbMq \
  --field-id PVTSSF_lAHOAotjYs4BHbMqzg4Lxyk \
  --single-select-option-id 47fc9ee4
```

Needs the `project` scope — `gh auth status` to check, `gh auth refresh -s project` to add.
Project IDs and field details live in `.claude/github-projects-reference.md`.

**Parsing JSON**: use `gh --jq` or PowerShell `ConvertFrom-Json`. There is no `jq` on this
box, and `grep`/`sed`/`cut` pipelines over JSON break on the first quoted comma.

### 3.7 Refresh the cache

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

## Phase 4 — Report

```markdown
## [Type] Branch Initialized

**Branch**: [branch-name]
**PR**: #[pr-number] (draft)
**Issue**: #[issue-number]
**Project**: Radoub - In Progress          <!-- sprints and epics only -->

### Next Steps
1. Check the TDD Policy table — tests first where required
2. [type-specific line, see below]
3. Run `/pre-merge` when done
```

Step 2 varies: an **epic** reviews scope and files sprint issues, then implements phase by
phase; a **sprint** starts any item immediately unless the user sequences them, updating
CHANGELOG entries as each lands; a **fix or feature** just implements.

## Working a sprint (AI)

This is guidance for Claude, not the human — humans may batch commits however they like.

**Commit and push after each discrete item, then start the next one without asking.** Every
commit stays reviewable, rollback stays cheap, and an interrupted session loses nothing.

Run the full test suite once at the end during `/pre-merge`, not per item. Targeted tests for
the item in hand are still expected. Prompt before FlaUI — it takes over the machine.

## Error handling

| Situation | Response |
|-----------|----------|
| Issue not found | Error and exit; the issue number is required |
| Dirty working directory | Ask the user to commit or stash |
| Branch already exists | Ask whether to check it out or create a new one |
| Tool ambiguous | Ask the user |
| PR creation fails | Print the manual command |

## Notes

- **TDD is mandatory** — check the TDD Policy table in CLAUDE.md before writing implementation
  code. Tests first for features, services, parsers, and reproducible bugs.
- CHANGELOG versions increment from the previous entry; check the last one.
- Launch `.ps1` from the Bash tool — PowerShell-tool permission rules never match on Windows.
