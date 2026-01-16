# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

- **Parley**: Dialog editor (`.dlg` files) - See `Parley/CLAUDE.md` for tool-specific guidance
- **Manifest**: Journal editor (`.jrl` files) - See `Manifest/CLAUDE.md` for tool-specific guidance
- **Quartermaster**: Creature/inventory editor (`.utc`, `.bic` files) - See `Quartermaster/CLAUDE.md` for tool-specific guidance

### Shared Libraries

- **Radoub.Formats**: Aurora Engine file format parsers (KEY, BIF, etc.) - Shared library for all tools

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
├── Radoub.sln (builds all tools; excludes Windows-only integration tests)
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
├── Manifest/ (journal editor)
│   ├── CLAUDE.md (Manifest-specific guidance)
│   ├── CHANGELOG.md (Manifest-specific changes)
│   ├── Manifest/ (source code)
│   └── Manifest.Tests/ (unit tests)
├── Quartermaster/ (creature/inventory editor)
│   ├── CLAUDE.md (Quartermaster-specific guidance)
│   ├── CHANGELOG.md (Quartermaster-specific changes)
│   ├── Quartermaster/ (source code)
│   └── Quartermaster.Tests/ (unit tests)
├── Radoub.Formats/ (shared library)
│   ├── Radoub.Formats.sln
│   ├── Radoub.Formats/ (source code)
│   └── Radoub.Formats.Tests/ (unit tests)
└── [Future tools will be added here]
```

---

## Working with Multiple Tools

### Building

**Root-level solution**: Use `Radoub.sln` to build all tools at once:

```bash
# Build all tools (excludes Windows-only integration tests)
dotnet build Radoub.sln

# Build with release configuration
dotnet build Radoub.sln --configuration Release
```

**Individual tool builds**:
```bash
dotnet build Parley/Parley.sln
dotnet build Manifest/Manifest/Manifest.csproj
dotnet build Quartermaster/Quartermaster/Quartermaster.csproj
```

**Note**: `Radoub.IntegrationTests` is excluded from `Radoub.sln` because it targets `net9.0-windows` (FlaUI requires Windows).

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

### Tool-Specific Standards

Tool-specific CLAUDE.md files may add enforcement rules:
- **Parley**: PR length limits (15 sentences max), Claude enforcement role for workflow discipline
- **Binary format tools**: Stricter pre-commit testing requirements

Tool-specific standards take precedence over repository-wide standards when they conflict.

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

**UI Test Stability (FlaUI + Avalonia)**:
- Avalonia apps can crash with `SkiaSharp.SKCanvas.Flush()` errors if closed during mid-render
- Use `App.Close()` to target the specific test process (not Alt+F4, which goes to focused window)
- Add delays before closing to let Avalonia's compositor finish pending renders
- Wait for process to fully exit between tests to prevent resource conflicts
- See `FlaUITestBase.StopApplication()` for reference implementation

**FlaUI Window Focus (CRITICAL)**:
- **NEVER use direct `Keyboard.TypeSimultaneously()` calls** - keystrokes go to focused window, not necessarily test app
- **ALWAYS use focus-safe helpers** from `FlaUITestBase`:
  - `SendCtrlS()`, `SendCtrlZ()`, `SendCtrlY()`, `SendCtrlD()` etc.
  - `SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X)` for custom shortcuts
  - `EnsureFocused()` before any keyboard input if not using helpers
- **Why**: During test runs, VSCode or other apps can steal focus. Keyboard shortcuts like Ctrl+Shift+E open VSCode's file explorer instead of triggering test app actions.
- See `FlaUITestBase.EnsureFocused()` for focus verification pattern

**Before PRs to Main**:
- All tools must build
- All tool tests must pass
- Private documentation to the Private folder
- Public Documentation approved before push
- CHANGELOG updated for affected tools
- **CHANGELOG version finalized**: Move `[Unreleased]` entries to versioned section with date (e.g., `[0.1.3-alpha] - 2025-11-08`)

---

## Sprint Workflow

**Commit Between Sprint Items**:
- Commit after completing each discrete item within a sprint
- This provides clear history and makes rollback easier
- Use descriptive commit messages that reference the sprint issue

**Example Sprint Workflow**:
```
# After completing UTC reader
git add . && git commit -m "[Radoub] feat: Add UTC reader for creature blueprints (#549)"

# After completing UTC writer
git add . && git commit -m "[Radoub] feat: Add UTC writer for creature blueprints (#549)"

# After completing tests
git add . && git commit -m "[Radoub] test: Add UTC round-trip tests (#549)"
```

**Benefits**:
- Each commit represents a working state
- Easier to review individual changes
- Simpler to bisect if issues arise
- Clear progress tracking in git history

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
| **Radoub** | `CHANGELOG.md` | Repository-level changes: shared documentation, slash commands, cross-tool features, Radoub.Formats |
| **Parley** | `Parley/CHANGELOG.md` | Parley-specific changes: features, fixes, UI updates |
| **Manifest** | `Manifest/CHANGELOG.md` | Manifest-specific changes: features, fixes, UI updates |
| **Quartermaster** | `Quartermaster/CHANGELOG.md` | Quartermaster-specific changes |

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

## Code Quality Standards

**Game Data Sourcing (MANDATORY)**:
- **NEVER hardcode game data** (races, classes, feats, skills, appearances, etc.)
- **ALWAYS populate from 2DA files and TLK strings** via IGameDataService
- Support custom content (CEP, PRC, etc.) that adds/modifies 2DA entries
- Hardcoded fallbacks are acceptable ONLY when 2DA/TLK lookup fails
- If the data exists in a game file, load it from the game file
- This ensures compatibility with all modules, custom content packs, and community expansions

**Path Handling**:
- Use `Environment.GetFolderPath()` with `SpecialFolder` constants
- Validate paths with `Path.GetFullPath()` for traversal prevention
- Use `ProcessStartInfo.ArgumentList` instead of string concatenation

**Exception Handling**:
- Never use bare `catch` blocks - catch specific types
- Always log exceptions (at minimum `LogLevel.WARN`)
- Never silently swallow exceptions

**Code Hygiene**:
- No commented-out code blocks - use git history
- TODOs must reference GitHub issues: `// TODO (#123): description`
- Keep methods under 100 lines
- Remove debug/test code before committing

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

**Button Labeling Standards**:
- **Browse buttons**: Use "Browse..." or "..." (ellipsis indicates dialog will open)
- **Action buttons**: Use verb describing action (e.g., "Save", "Export", "Add")
- **Position**: Browse buttons should be immediately adjacent to their associated field, not right-aligned away from it
- **Consistency**: All tools must follow the same button labeling patterns

See [#868](https://github.com/LordOfMyatar/Radoub/issues/868) for standardization audit.

---

## Aurora Engine File Naming Constraints

**CRITICAL**: Aurora Engine (Neverwinter Nights) has strict filename limitations:

- **Maximum filename length**: 16 characters (excluding extension)
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
- Warn users when filenames exceed 16 characters
- Suggest shortened alternatives

---

## Aurora File Format Implementation

**Reference Strategy** for implementing Aurora Engine file parsers (GFF, ERF, KEY, BIF, TLK, 2DA, SSF):

**PRIMARY Reference**: [neverwinter.nim](https://github.com/niv/neverwinter.nim) (MIT License)

**SECONDARY Reference**: BioWare Aurora Specifications (`Documentation/`)
- Use for understanding structure and design intent
- Specs are 20 years old and may not reflect modern edge cases
- Good for "why" questions, not "how to handle X" questions

**Implementation Approach**:
1. Write parsers in C# (native to Radoub toolset)
2. Follow neverwinter.nim's edge case handling and validation patterns

---

## Resources

- **Wiki**: `d:\LOM\workspace\Radoub.wiki\` (local clone of https://github.com/LordOfMyatar/Radoub/wiki)
- **BioWare Aurora Specs**: `Documentation/`
- **neverwinter.nim Reference**: https://github.com/niv/neverwinter.nim
- **Project History**: `About/CLAUDE_DEVELOPMENT_TIMELINE.md`
- **AI Collaboration Story**: `About/ON_USING_CLAUDE.md`
- **Tool-Specific Guidance**: `ToolName/CLAUDE.md`

---

**For detailed tool-specific guidance, always refer to the tool's CLAUDE.md file.**
