# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

- **Parley**: Dialog editor (`.dlg` files) - See `Parley/CLAUDE.md` for tool-specific guidance

### Planned Tools

Future tools will be added as subdirectories with their own README, CLAUDE.md, and development infrastructure.

---

## Repository Structure

```
Radoub/
├── README.md (landing page)
├── LICENSE
├── CHANGELOG.md (repo-level changes)
├── CLAUDE.md (this file - repo-level guidance)
├── .gitignore
├── .claude/commands/ (slash commands for Claude Code)
├── Documentation/ (Aurora Engine format specs - shared across tools)
│   ├── BioWare_Original_PDFs/
│   └── Bioware_Aurora_*.md
├── About/ (project history and AI collaboration documentation)
│   ├── CLAUDE_DEVELOPMENT_TIMELINE.md
│   └── ON_USING_CLAUDE.md
├── Parley/ (dialog editor)
│   ├── README.md
│   ├── CLAUDE.md (Parley-specific guidance)
│   ├── CHANGELOG.md (Parley-specific changes)
│   ├── Parley/ (source code)
│   ├── TestingTools/
│   ├── Documentation/ (Approved Parley-specific docs)
│   ├── NonPublic/ (To be approved documents)
└── [Future tools will be added here]
```

---

## Working with Multiple Tools

### Tool-Specific Work

When working on a specific tool (e.g., Parley):
1. **Always read the tool's CLAUDE.md first** (`Parley/CLAUDE.md`)
2. Follow tool-specific conventions and workflows
3. Tool-specific issues/PRs reference the tool in title: `[Parley] Fix parser bug`
4. Run tool-specific tests before committing

### Shared Resources

**Public Documentation** (`Documentation/`):
- Shared across all tools
- Contains BioWare format specifications
- Read-only reference material
- Updates only for format corrections or additions
- Original and Markdown versions of Bioware File Format documentation

**About Documentation** (`About/`):
- Project history and development experience
- AI collaboration documentation
- Updated when major project milestones occur
- Never edit ON_USING_CLAUDE.md; only Lord makes changes to that.

**NonPublic Docs**
- When you right something put it in the project's NonPublic docs folder.
- The human will move it to public after review

### Cross-Tool Work

If work affects multiple tools:
- Create separate commits per tool when possible
- Use clear commit messages: `[Parley] Update parser` + `[FutureTool] Update importer`
- Test all affected tools before committing
- Document cross-tool dependencies

---

## Commit Standards

Use tool prefixes in commit messages:

```
[Parley] fix: Resolve parser buffer overflow
[Parley] feat: Add script parameter preview
[Radoub] docs: Update main README
[Radoub] chore: Update shared BioWare documentation
```

### Commit Types
- `feat:` - New features
- `fix:` - Bug fixes
- `docs:` - Documentation only
- `refactor:` - Code organization without feature changes
- `test:` - Test additions or improvements
- `chore:` - Maintenance tasks

---

## Branch Workflow

**Single main branch approach** (solo developer, rapid iteration):

**Main Branch**:
- `main` - Production-ready releases (protected by PR review process)

**Feature Branches**:
- Tool-specific: `parley/feature/name` or `parley/fix/name`
- Cross-tool: `radoub/feature/name`
- Documentation: `docs/name`

**Workflow**:
```
main (production)
  └── feature/fix branches → PR → main
```

**Important**:
- All work via Pull Requests, even for "small" changes
- PRs protect main branch (review before merge)
- GitHub releases mark stable milestones
- If project grows to multiple contributors, reconsider adding a `develop` integration branch

---

## Starting a New Feature Branch

**Command**: "Init a new feature for [tool] epic/issue #[number]"

**Example**: "Init a new feature for Parley epic #37"

**Process**:
1. Sync with main branch (`git checkout main && git pull`)
2. Create feature branch following naming convention:
   - Epic: `[tool]/feat/epic-[N]-[short-name]`
   - Feature: `[tool]/feat/[short-name]`
   - Fix: `[tool]/fix/[short-name]`
3. Update tool's CHANGELOG.md:
   - Add new version section after `[Unreleased]`
   - Include branch name and `PR: #TBD` placeholder
   - Add epic/feature heading
4. Commit and push branch
5. Create draft PR on GitHub
6. Update CHANGELOG.md with actual PR number
7. Commit and push PR number update

**Example CHANGELOG Section**:
```markdown
## [0.1.5-alpha] - TBD
**Branch**: `parley/feat/epic-0-plugins` | **PR**: #84

### Epic 0: Plugin Foundation

---
```

**Benefits**:
- CHANGELOG tracks branch/PR numbers to prevent version collisions
- Draft PR created early for visibility and discussion
- Clear connection between version, branch, PR, and epic/issue
- Prevents accidental version number reuse across branches

---

## Adding New Tools

When adding a new tool to Radoub:

1. **Create tool directory** with standard structure:
   ```
   ToolName/
   ├── README.md (tool-specific)
   ├── CLAUDE.md (tool-specific guidance)
   ├── CHANGELOG.md (tool changelog)
   ├── ToolName/ (source code)
   ├── TestingTools/ (if applicable)
   ├── Documentation/ (tool-specific docs)
   └── .github/ (tool-specific workflows)
   ```

2. **Update Radoub README.md** to list new tool

3. **Create tool-specific CLAUDE.md** with:
   - Tool overview and architecture
   - Development workflow
   - Testing requirements
   - Tool-specific conventions

4. **Set up CI/CD** in `.github/workflows/` with tool prefix

5. **Initial commit**: `[Radoub] feat: Add ToolName - [brief description]`

---

## Testing Requirements

**Before Committing**:
- Run tool-specific tests (see tool CLAUDE.md)
- Verify affected tool(s) build successfully
- Check for hardcoded paths (privacy)
- Verify cross-platform compatibility if applicable

**Before PRs to Main**:
- All tools must build
- All tool tests must pass
- Private documentation to the Private folder
- Public Documentation approved before push
- CHANGELOG updated for affected tools
- **CHANGELOG version finalized**: Move `[Unreleased]` entries to versioned section with date (e.g., `[0.1.3-alpha] - 2025-11-08`)

---

## Documentation Standards

Follow the same standards as Parley (see `Parley/CLAUDE.md`):
- ATX headers (no decorative bold/italic)
- Google Docs compatible formatting (minimal bolding)
- Table of Contents for docs over 2 pages
- Privacy-safe examples (no real usernames/paths)

---

## CHANGELOG Management

**Two-Level CHANGELOG System**:

| CHANGELOG | Location | Contents |
|-----------|----------|----------|
| **Radoub** | `CHANGELOG.md` | Repository-level changes: shared documentation, slash commands, cross-tool features |
| **Parley** | `Parley/CHANGELOG.md` | Parley-specific changes: features, fixes, UI updates |

**Rules**:
- Tool-specific changes go in tool CHANGELOG only
- Shared documentation updates go in Radoub CHANGELOG
- Slash commands (`.claude/commands/`) go in Radoub CHANGELOG
- When in doubt, ask which CHANGELOG to update

---

## Session Management

**Starting a New Session**:
1. Read this file (Radoub-level guidance)
2. Read tool-specific CLAUDE.md if working on specific tool
3. Check recent git commits for context
4. Check GitHub issues/PRs for active work

**Session Best Practices**:
- Use TodoWrite tool for task tracking
- Commit regularly with clear messages
- Update appropriate CHANGELOG for user-facing changes (see CHANGELOG Management above)
- Mark in-progress work clearly if session ends incomplete

---

## Community Interaction

**Issue Tracking**:
- Use tool labels: `[Parley]`, `[ToolName]`
- Cross-tool issues use `[Radoub]` label
- Apply priority labels consistently
- Link related issues

**Pull Requests**:
- Fill out PR template completely
- Reference related issues (`Closes #X`, `Relates to #Y`)
- Include testing checklist
- Tag tool-specific reviewers if applicable
- **Before creating PR**: Update CHANGELOG to move `[Unreleased]` entries to new version section with date
  - Example: `[0.1.3-alpha] - 2025-11-08`
  - Commit with: `chore: Prepare vX.Y.Z release`
  - This ensures CHANGELOG is ready for tagging/release after merge

---

## Privacy & Security

**Always**:
- Use `~` for home directory in logs and docs
- Sanitize paths before logging
- No hardcoded user paths in code
- No real usernames in examples
- Keep NonPublic/ out of public repo

---

## UI/UX Guidelines

**Dialog and Window Behavior**:
- **NEVER use modal dialogs** (`ShowDialog()`) that block the main application
- **ALWAYS use non-modal windows** (`Show()`) for informational messages
- Users must be able to interact with the main application while notifications/messages are visible
- Exception: Confirmation dialogs for destructive actions (delete, overwrite) may be modal
- Prefer toast notifications or status bar messages over popup windows

**Examples**:
```csharp
// ❌ BAD - Blocks main window
await msgBox.ShowDialog(this);

// ✅ GOOD - Non-blocking
msgBox.Show();

// ✅ ALSO GOOD - Auto-closing notification
ShowToastNotification("Plugin started", 3000);
```

---

## Aurora Engine File Naming Constraints

**CRITICAL**: Aurora Engine (Neverwinter Nights) has strict filename limitations:

- **Maximum filename length**: 12 characters (excluding extension)
- **Case**: Lowercase recommended for compatibility
- **Characters**: Alphanumeric and underscore only
- **Examples**:
  - ✅ `test1_link.dlg` (10 chars)
  - ✅ `merchant_01.dlg` (11 chars)
  - ❌ `Test1_SharedReply.dlg` (17 chars - too long)
  - ❌ `my-dialog.dlg` (hyphen not recommended)

**Why this matters**:
- Parley can open files with long names
- Aurora Engine and NWN game cannot load them
- Files appear "missing" in-game despite being valid

**Tools must**:
- Validate filename length before saving
- Warn users when filenames exceed 12 characters
- Suggest shortened alternatives

---

## Resources

- **BioWare Aurora Specs**: `Documentation/`
- **Project History**: `About/CLAUDE_DEVELOPMENT_TIMELINE.md`
- **AI Collaboration Story**: `About/ON_USING_CLAUDE.md`
- **Tool-Specific Guidance**: `ToolName/CLAUDE.md`

---

**For detailed tool-specific guidance, always refer to the tool's CLAUDE.md file.**
