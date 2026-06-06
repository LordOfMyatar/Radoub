# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

| Tool | Abbrev | Description | CLAUDE.md |
|------|--------|-------------|-----------|
| **Parley** | PAR | Dialog editor (`.dlg` files) | `Parley/CLAUDE.md` |
| **Manifest** | MAN | Journal editor (`.jrl` files) | `Manifest/CLAUDE.md` |
| **Quartermaster** | QM | Creature/inventory editor (`.utc`, `.bic` files) | `Quartermaster/CLAUDE.md` |
| **Fence** | FEN | Merchant/store editor (`.utm` files) | `Fence/CLAUDE.md` |
| **Relique** | REL | Item blueprint editor (`.uti` files) | `Relique/CLAUDE.md` |
| **Trebuchet** | TRE | Radoub launcher/hub | `Trebuchet/CLAUDE.md` |
| **Marlinspike** | MAR | Search & replace across files (lives in Trebuchet) | `Trebuchet/CLAUDE.md` |
| **Radoub** | RAD | Repository-level / shared | This file |

### Shared Libraries

- **Radoub.Formats**: Aurora Engine file format parsers (KEY, BIF, etc.) - Shared library for all tools

### Planned Tools

| Tool | Description | Status |
|------|-------------|--------|
| **Reliquary** | (TBD — placeholder for future tool) | Planned; bootstrap FlaUI infra tracked in [#2304](https://github.com/LordOfMyatar/Radoub/issues/2304) |

Future tools land as subdirectories with their own README, CLAUDE.md, and development infrastructure. Bootstrap follows the [New Tool Bootstrap guide](Documentation/NEW_TOOL_BOOTSTRAP.md).

---

## Repository Structure

```
Radoub/
├── README.md (landing page)
├── LICENSE
├── CLAUDE.md (this file - repo-level guidance)
├── Radoub.sln (builds all tools; excludes Windows-only integration tests)
├── .gitignore
├── .claude/commands/ (slash commands for Claude Code)
├── Documentation/ (Aurora Engine format specs - shared across tools)
│   └── BioWare_Original_PDFs/ (original BioWare PDFs)
├── About/ (project history and AI collaboration documentation)
│   ├── CLAUDE_DEVELOPMENT_TIMELINE.md
│   └── ON_USING_CLAUDE.md
├── NonPublic/ (private docs, specs, research — NOT in git)
│   ├── Relique/ (Relique specs, plans, research)
│   ├── Parley/ (Parley specs, plans, research)
│   ├── Quartermaster/ (QM specs, plans, research)
│   ├── Fence/ (Fence assets, research)
│   └── Trebuchet/ (Trebuchet research)
├── Parley/ (dialog editor)
│   ├── README.md
│   ├── CLAUDE.md (Parley-specific guidance)
│   ├── CHANGELOG.md (Parley-specific changes)
│   ├── Parley/ (source code)
│   ├── TestingTools/
│   ├── Documentation/ (Approved Parley-specific docs)
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
├── Fence/ (merchant/store editor)
│   ├── CLAUDE.md (Fence-specific guidance)
│   ├── CHANGELOG.md (Fence-specific changes)
│   ├── Fence/ (source code)
│   └── Fence.Tests/ (unit tests)
├── Relique/ (item blueprint editor)
│   ├── CLAUDE.md (Relique-specific guidance)
│   ├── CHANGELOG.md (Relique-specific changes)
│   ├── Relique/ (source code, namespace: ItemEditor)
│   └── Relique.Tests/ (unit tests)
├── Trebuchet/ (launcher/hub)
│   ├── CLAUDE.md (Trebuchet-specific guidance)
│   ├── CHANGELOG.md (Trebuchet-specific changes)
│   ├── Trebuchet/ (source code)
│   └── Trebuchet.Tests/ (unit tests)
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
- Contains original BioWare PDF format specifications
- Read-only reference material
- Markdown conversions of BioWare docs are now in the Wiki (see Resources section)
- **NEVER read PDF files without explicit user instruction** - PDFs can exceed context limits and cause "prompt too long" errors

**About Documentation** (`About/`):
- Project history and development experience
- AI collaboration documentation
- Updated when major project milestones occur
- Never edit ON_USING_CLAUDE.md; only Lord makes changes to that.

**NonPublic Docs**
- **All NonPublic docs go in the root `NonPublic/` directory**, organized by tool:
  - `NonPublic/Relique/` — Relique specs, plans, follow-ups
  - `NonPublic/Parley/` — Parley specs, research
  - `NonPublic/Quartermaster/` — QM specs, research
  - etc.
- **NEVER create a `NonPublic/` folder inside a tool directory** (e.g., `Parley/NonPublic/` is WRONG)
- The human will move approved docs to public after review

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
3. Update tool's CHANGELOG.md with a new versioned section (highlights only, never use `[Unreleased]`)
4. Commit and push branch
5. Create draft PR on GitHub
6. Update CHANGELOG.md with actual PR number
7. Commit and push PR number update

**Example CHANGELOG Entry**:
```markdown
## [0.1.5-alpha] - 2026-03-28
**Branch**: `parley/feat/epic-0-plugins` | **PR**: #84

### Epic 0: Plugin Foundation
- Plugin discovery and loading framework
- Hot-reload support for development
```

**Benefits**:
- CHANGELOG highlights give users a quick overview of what changed
- Draft PR created early for visibility and discussion
- Git history preserves all implementation details

---

## New Tool Bootstrap

Starting a new tool? Read `Documentation/NEW_TOOL_BOOTSTRAP.md` — the full checklist
(required components, shared-library references, Trebuchet integration, cross-tool dispatch,
UI uniformity, file-browser adoption, versioning, common mistakes). Not auto-loaded every session.

---

## Test-Driven Development (TDD) Policy

**MANDATORY**: Before writing implementation code, check this table. If TDD is required, write the failing test FIRST. Do not skip this step.

| Scenario | TDD Required? | Action |
|----------|--------------|--------|
| New feature / service / parser | **Yes** | Write failing test → implement → verify |
| New shared library or cross-tool code | **Yes** | Write failing test → implement → verify |
| Bug fix (reproducible) | **Yes** | Write failing test that reproduces bug → fix → verify |
| Bug fix (investigation needed) | No | Debug first, add regression test after |
| UI layout / styling / AXAML only | No | Manual verification |
| Config / documentation only | No | No tests needed |

**If you catch yourself writing implementation code without a test for a "Yes" scenario, STOP. Write the test first, then continue.**

**TDD Workflow**:
1. Write a failing test that describes the expected behavior
2. Run the test — confirm it fails for the right reason
3. Write the minimum code to make the test pass
4. Refactor if needed (tests still pass)
5. Repeat

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
- **Sequential execution is enforced (#1526)**: `Radoub.IntegrationTests/AssemblyInfo.cs` carries `[assembly: CollectionBehavior(DisableTestParallelization = true)]` for within-assembly serialization. `FlaUITestBase` acquires a named system mutex (`Global\Radoub.FlaUI.SerialExecution`, 30 s timeout) for cross-process serialization — terminal + IDE Test Explorer collisions block on the mutex with a clear error rather than racing for desktop focus.

**FlaUI Window Focus (CRITICAL)**:
- **NEVER use direct `Keyboard.TypeSimultaneously()` calls** - keystrokes go to focused window, not necessarily test app
- **ALWAYS use focus-safe helpers** from `FlaUITestBase`:
  - `SendCtrlS()`, `SendCtrlZ()`, `SendCtrlY()`, `SendCtrlD()` etc.
  - `SendKeyboardShortcut(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_X)` for custom shortcuts
  - `EnsureFocused()` before any keyboard input if not using helpers
- **Why**: During test runs, VSCode or other apps can steal focus. Keyboard shortcuts like Ctrl+Shift+E open VSCode's file explorer instead of triggering test app actions.
- See `FlaUITestBase.EnsureFocused()` for focus verification pattern

**Test Output (MANDATORY)**:
- **NEVER pipe test output through `tail` or `head`** — this discards failures and leads to false "all tests pass" claims
- Run tests with `run_in_background`, then grep the output file for results:
  ```bash
  # Run tests (output goes to a file automatically)
  dotnet test Radoub.sln --no-build  # with run_in_background=true

  # After completion, grep the output file for summary + failures
  grep -E "^(Failed|Passed|  Failed)" $OUTPUT_FILE
  ```
- The `Failed!` and `Passed!` summary lines plus any `  Failed` detail lines give full visibility without reading thousands of lines
- If failures appear, read the full output file for stack traces and error details

**Before PRs to Main**:
- All tools must build
- All tool tests must pass
- Private documentation to the Private folder
- Public Documentation approved before push
- CHANGELOG updated for affected tools (highlights only — see CHANGELOG Management)
- **CHANGELOG uses versioned sections only** — never use `[Unreleased]`

---

## Sprint Workflow

**Per Sprint Item**: For each item, follow this order:
1. **TDD check** — does this item need tests first? (See TDD Policy table above)
2. **Implement** — write code (after tests if TDD required)
3. **Verify** — run tests, confirm build
4. **Commit and push** — one commit per item

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

**Sprint Completion — Manual Spot-Check List**:

When all sprint items are done (before `/pre-merge`), generate a **manual spot-check list** if the sprint included any changes that need human visual/behavioral verification.

**Include spot-checks when the sprint has:**
- UI visual changes (new icons, color changes, layout, theme updates)
- User-facing behavior (new shortcuts, menu items, dialog behavior)
- Event/notification wiring (events that should trigger visible UI updates)
- File format changes (round-trip with real files)

**Skip spot-checks when:**
- Changes are purely internal (refactoring, test-only, documentation)
- Everything is fully covered by automated tests with no visual component

**Format:**
```markdown
### Manual Spot-Checks

Verify in the running app before `/pre-merge`:

- [ ] [Specific thing to check] — [where/how to verify]
- [ ] [Another thing] — [steps to reproduce]
```

**Rules:**
- Be specific — "check the UI" is not useful
- Include how to trigger the behavior
- Only list things automated tests can't verify
- Keep it short (3-8 items typical)

**Example** (for a sprint with a new FlowView icon + theme fixes):
```markdown
### Manual Spot-Checks

- [ ] 📋 icon appears in FlowView when quest tag is set on a node
- [ ] 📋 icon disappears when quest tag is cleared (✕ button)
- [ ] 📋 icon appears when quest selected via Browse dialog
- [ ] Browser window text readable on dark theme (no white-on-white)
- [ ] Browser window text readable on light theme (no invisible text)
```

---

## Spike Solutions

Spikes are **timeboxed throwaway prototypes** (from XP/Extreme Programming) used to reduce uncertainty before committing to an implementation approach.

**When to Spike**:

| Situation | Spike? |
|-----------|--------|
| New file format parser (unknown edge cases) | Yes |
| Unfamiliar 2DA interaction pattern | Yes |
| UI pattern not yet used in Radoub | Yes |
| Library evaluation (performance, compatibility) | Yes |
| Well-understood feature addition | No |
| Bug fix with clear reproduction | No |
| Test-only work | No |

**Spike vs Research**:
- `/research` = information gathering (reading, no code changes)
- `/research --spike` = hands-on prototyping (throwaway code on a disposable branch)

**Workflow**: Run `/research --spike [topic or #issue]` to start. Creates a throwaway branch, sets a timebox, and on completion generates a findings document in `NonPublic/[Tool]/Research/spike-[topic].md`. The spike branch is **never merged** — it is deleted after findings are captured.

**Rules**:
- Never merge a spike branch
- Always capture findings before deleting the branch
- Respect the timebox — document what you have and stop
- One question at a time — log new questions for future spikes

---

## Documentation Standards

Follow the same standards as Parley (see `Parley/CLAUDE.md`):
- ATX headers (no decorative bold/italic)
- Google Docs compatible formatting (minimal bolding)
- Table of Contents for docs over 2 pages
- Privacy-safe examples (no real usernames/paths)

---

## CHANGELOG Management

**Per-tool CHANGELOGs for tool-specific work, plus a root `/CHANGELOG.md` for shared-library and cross-cutting work.** Git history is the detailed archive.

| Location | Contents |
|----------|----------|
| `/CHANGELOG.md` (repo root) | `Radoub.Formats`, `Radoub.UI`, `Radoub.Dictionary`, and cross-cutting work that does not belong to a single tool |
| `Parley/CHANGELOG.md` | Parley highlights |
| `Manifest/CHANGELOG.md` | Manifest highlights |
| `Quartermaster/CHANGELOG.md` | Quartermaster highlights |
| `Fence/CHANGELOG.md` | Fence highlights |
| `Relique/CHANGELOG.md` | Relique highlights |
| `Trebuchet/CHANGELOG.md` | Trebuchet highlights |

**Routing rules**:
- Pure shared-library change (no immediate tool-facing behavior) → root `/CHANGELOG.md` only
- Shared-library change with tool-visible impact → root `/CHANGELOG.md` is the canonical entry; tool CHANGELOGs may add a one-liner that links the root entry (no duplication of details)
- Tool-specific work → tool CHANGELOG only
- Cross-tool sprint touching multiple tools → each affected tool gets its own one-liner; root `/CHANGELOG.md` only if a shared-library change is also part of the sprint

**Rules**:
- Highlights only — major features, breaking changes, notable fixes
- Git history is the detailed archive
- No cross-references between tool CHANGELOGs (shared-lib changes go in root)
- Never use `[Unreleased]` — all entries go in versioned sections
- One entry per feature, not implementation checklists
- Root `/CHANGELOG.md` versions per shared library (e.g. `[Radoub.Formats 0.2.60-alpha]`) since the shared libraries version independently of tools

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
- **CHANGELOG entries** — highlights only, versioned sections, never `[Unreleased]`

---

## Privacy & Security

**Always**:
- Use `~` for home directory in logs and docs
- Sanitize paths before logging
- No hardcoded user paths in code
- No real usernames in examples
- Keep NonPublic/ out of public repo

---

## Shell Usage on Windows

### Prefer Script Files Over Inline Commands

**DO**: Use `powershell.exe -File` with PowerShell scripts (NEVER use `pwsh` — it launches Microsoft Store)
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View status
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force
```

**AVOID**: Inline PowerShell with `-Command` (escaping nightmare)
```bash
# BAD - requires \$ escaping, breaks easily
powershell.exe -Command "\$data = Get-Content file.json | ConvertFrom-Json; \$data.property"
```

### When to Use Each Shell

| Task | Use | Example |
|------|-----|---------|
| Git operations | Bash | `git status`, `git commit` |
| GitHub CLI | Bash | `gh issue view 123`, `gh pr create` |
| File operations | Bash | `cp`, `mkdir -p`, `rm -f` |
| Complex data processing | PowerShell script | `Get-CacheData.ps1` |
| JSON parsing | PowerShell script | Custom `.ps1` files |

### Bash on Windows (Git Bash)

**Use forward slashes** - they work:
```bash
cp d:/LOM/workspace/Radoub/source/file.txt d:/LOM/workspace/Radoub/dest/
mkdir -p d:/LOM/workspace/Radoub/new/path/
ls -la d:/LOM/workspace/Radoub/some/path/
```

**AVOID**:
- Backslashes in paths (escaping issues)
- `cmd /c` commands (quoting problems)
- `grep` with regex alternation `\|` (use PowerShell search instead)

### PowerShell Script Patterns

**Good patterns** (in `.ps1` files):
```powershell
# Normal variables - no escaping needed
$data = Get-Content $CacheFile | ConvertFrom-Json
$age = (Get-Date) - (Get-Item $CacheFile).LastWriteTime

# String interpolation works naturally
Write-Host "Found $($results.Count) items"
```

**Avoid in slash commands**:
- Inline `powershell.exe -Command "..."` blocks with variable escaping
- Multi-line PowerShell embedded in markdown
- Complex logic that should be in a script file

### Slash Command Best Practices

1. **Keep bash simple**: `gh`, `git`, file ops only
2. **Complex logic → script file**: Add to `.claude/scripts/`
3. **Call scripts with `-File`**: Clean, no escaping issues
4. **Parameters over string building**: Use `-Param value` not string concatenation

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
- **UI handlers that mutate the editor model must wrap the `model.Add(...) → RefreshUI() → MarkDirty()` sequence in try/catch and roll back the model change if the refresh throws.** A populate-time validation filter is not enough — UI controls (ComboBox selections, tree expansion) hold stale state across refreshes, and a bad-state combo can crash deep in the Avalonia render loop instead of in your handler. See `MainWindow.ItemProperties.TryAddProperty` (Relique, #2166) for the canonical single-add pattern: outer catch for `CreateItemProperty`, inner catch for `RefreshAssignedProperties` that removes the just-added entry. Combine with a validation-table recheck at add-time (defense in depth) so each layer covers the other's gaps. **The same rollback discipline applies to batch-add, remove, and clear-all handlers** — these are easy to miss because the mutation is a loop or a single `Clear()`/`RemoveAt()` rather than one `Add()`. Extract the mutate-refresh-rollback logic into a pure helper so it is unit-testable without FlaUI: see `Relique/Services/PropertyListMutator.cs` (`BatchAdd`/`RemoveAt`/`ClearAll`, #2258) for the reusable pattern. **Reliquary and other single-resource blueprint editors derive their handler structure from Relique — fixes to this pattern in Relique should propagate to sibling tools.**

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

**Detailed UI patterns** (button labeling, progress indicators by duration, deferred loading / lifecycle event responsibilities, fire-and-forget, cancellation tokens, async anti-patterns) live in `Documentation/NEW_TOOL_BOOTSTRAP.md` — read it when building a tool's UI.

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

Implementing an Aurora Engine file parser (GFF, ERF, KEY, BIF, TLK, 2DA, SSF)? The reference strategy (neverwinter.nim primary, BioWare specs secondary) lives in `Documentation/NEW_TOOL_BOOTSTRAP.md`.

---

## GitHub Issue Cache

**Strategy**: Cache-first. All commands read GitHub data from the local cache — never call `gh issue view` or `gh pr view` directly for reads. Mutations (`gh issue create`, `gh pr create`, `gh issue close`) trigger a cache refresh afterward.

**Location**: `.claude/cache/github-data.json`

**Refresh Cache**:
```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1"        # Only if stale (>1 hour)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Refresh-GitHubCache.ps1" -Force # Force refresh
```

**Read Cache**:
```bash
# Issue details
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View issue -Number 123

# Search issues
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View search -Query "keyword"

# List view (no bodies, ~25KB)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View list

# Summary stats (~1KB)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Get-CacheData.ps1" -View summary
```

**Cache Contents**:
- Up to 200 open issues with labels, assignees, milestones, comments
- Up to 20 open PRs with status and review state
- Summary stats (stale issues, missing labels)
- Auto-refreshes when >1 hour old

**Verify Live State** (closed issues are not in cache, but their numbers may appear in other issues' bodies):

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Test-IssueState.ps1" -Numbers "1902,1903,1905"
```

Returns JSON array with live `state` (OPEN/CLOSED) and `closedAt`. Use when `/init-item` or `/research` surfaces referenced issue numbers that may have been closed since the cache was last written.

**Commands that use cache**: `/backlog`, `/init-item`, `/pre-merge`, `/research`

**Commands that trigger post-mutation refresh**: `/init-item` (after PR create), `/pre-merge` (after tech debt issue create), `/post-merge` (after issue close/comment)

**Exception**: `/dependabot` uses direct `gh pr list` (infrequent, needs freshest PR state)

---

## Session Log Searching

A script for searching Radoub tool session logs with regex patterns.

**Location**: `.claude/scripts/Search-SessionLogs.ps1`

**Usage**:
```powershell
# List recent sessions
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -ListSessions -MostRecent 5

# Search with regex (supports alternation)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Pattern "error|warn|exception"

# Search specific tool with context
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Tool Fence -Pattern "focus|keyboard" -Context 2 -MostRecent 3

# Case-sensitive search
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\.claude\scripts\Search-SessionLogs.ps1" -Pattern "ERROR" -CaseSensitive
```

**Parameters**:
| Parameter | Default | Description |
|-----------|---------|-------------|
| `-Tool` | Parley | Tool to search (Parley, Manifest, Quartermaster, Fence, Trebuchet) |
| `-Pattern` | (required) | Regex pattern - supports `this\|that\|other` |
| `-MostRecent` | 1 | Number of recent sessions to search |
| `-Context` | 0 | Lines of context before/after matches |
| `-MaxResults` | 50 | Truncate results at this count |
| `-CaseSensitive` | false | Enable case-sensitive matching |
| `-ListSessions` | - | List available sessions without searching |

**Log Location**: `~/Radoub/{Tool}/Logs/Session_YYYYMMDD_HHMMSS/`

---

## Agent Skills (obra/superpowers)

Installed skills in `.agents/skills/` provide structured methodologies. Claude **must** follow these skills when their trigger conditions are met — no user invocation needed, no skipping without explicit user override.

### Installed Skills

| Skill | Location | Trigger |
|-------|----------|---------|
| **systematic-debugging** | `.claude/skills/systematic-debugging/` | Any bug, test failure, unexpected behavior, or build error |
| **test-driven-development** | `.claude/skills/test-driven-development/` | Implementing new features or bug fixes that need tests |
| **verification-before-completion** | `.claude/skills/verification-before-completion/` | Before claiming work is done, tests pass, or build succeeds |

### When to Apply Each Skill

**systematic-debugging** — Apply when:
- Test failures occur (unit, integration, or FlaUI)
- Build errors or warnings appear
- Binary format round-trip validation fails
- Unexpected runtime behavior reported
- **Especially** when tempted to "just try a quick fix"
- Follow all four phases: Root Cause → Pattern Analysis → Hypothesis → Implementation

**test-driven-development** — Apply when:
- Adding new features (format parsers, UI controls, services)
- Fixing bugs (write failing test reproducing the bug first)
- Adding to Radoub.Formats (GFF fields, 2DA parsing, etc.)
- **Exception**: UI layout/styling work, generated code, config files — ask user

**verification-before-completion** — Apply when:
- About to mark a TodoWrite task as completed
- About to commit or create a PR
- About to claim "tests pass" or "build succeeds"
- Moving to the next sprint item
- **Rule**: Run `dotnet test` or `dotnet build` and show the output. No "should work" claims.

### Skill Interaction

The skills chain naturally:
1. Bug reported → **systematic-debugging** (find root cause)
2. Root cause found → **test-driven-development** (write failing test, then fix)
3. Fix implemented → **verification-before-completion** (prove it works with evidence)

### Managing Skills

```bash
# Skills installed via:
npx -y skills add https://github.com/obra/superpowers --skill <name> --yes

# Skill files live in .agents/skills/ (content) with symlinks in .claude/skills/
# Source: https://github.com/obra/superpowers (MIT License)
```

---

## Resources

- **Wiki**: `d:\LOM\workspace\Radoub.wiki\` (local clone of https://github.com/LordOfMyatar/Radoub/wiki)
  - BioWare Aurora format specs (Markdown conversions)
  - Tool architecture documentation
  - Developer guides
- **BioWare Original PDFs**: `Documentation/BioWare_Original_PDFs/`
- **neverwinter.nim Reference**: https://github.com/niv/neverwinter.nim
- **Project History**: `About/CLAUDE_DEVELOPMENT_TIMELINE.md`
- **AI Collaboration Story**: `About/ON_USING_CLAUDE.md`
- **Tool-Specific Guidance**: `ToolName/CLAUDE.md`
- **GitHub Issue Cache**: `.claude/cache/github-data.json`

---

**For detailed tool-specific guidance, always refer to the tool's CLAUDE.md file.**
