# CLAUDE.md - Parley

Project guidance for Claude Code sessions working with Parley, the dialog editor for Neverwinter Nights.

## Date Check
- Claude's knowledge cutoff is January 2025. Current date is December 2025
- If you need the date, use `Get-Date` in PowerShell or `date` in bash

## Logging
- Make sure logs are scrubbed for privacy
- Don't ask the user to log-dive. You review the logs.

## Session Continuity System

### Starting a New Session
**ALWAYS read these files first to understand current state:**

1. **`CLAUDE_SESSION_CHECKLIST.md`** - Session start/end checklist (if present)
2. **`Documentation/Developer/CODE_PATH_MAP.md`** - Active code paths for read/write operations (prevents working in dead code)
3. **Recent git commits** - Check latest progress with `git log --oneline -10`
4. **This file (CLAUDE.md)** - Project structure and commands

**Session Checklist enforces**:
- "3 Strikes Rule" for debugging (external validation after 3 failed attempts)
- Documentation updates after intensive debugging
- Binary format testing protocol (ask before NWN testing needed)
- Dead-end commit tracking (avoid re-walking abandoned paths)

### Current Focus Areas
Check recent commits and GitHub issues for active priorities.

## Project Overview

**Parley** - Aurora-compatible dialog editor for Neverwinter Nights DLG files
- Part of the Radoub multi-tool repository for NWN modding
- See parent `../CLAUDE.md` for repository-wide guidance

### Core Architecture
- **Parley/** - Main application (.NET 9.0, Avalonia UI for cross-platform)
- **Documentation/** - Technical specifications and analysis (public)
  - **Developer/** - Technical docs for code maintenance (DELETE_BEHAVIOR.md, CODE_PATH_MAP.md, etc.)
  - **User/** - End-user documentation (plugin guides, script browser, etc.)
- **TestingTools/** - All test projects and debugging tools

### Key Components
- `Parley/Parsers/` - DLG file parsing (DialogParser delegates to DialogBuilder/DialogWriter)
- `Parley/Models/` - Dialog, DialogNode, DialogPtr data structures
- `Parley/ViewModels/MainViewModel.cs` - **ACTIVELY REFACTORING - DO NOT ADD NEW LOGIC HERE**
- `Parley/Handlers/` - UI event handlers (refactored from MainWindow for maintainability)
- `Parley/Services/` - Sound, Script, Settings, File operations (DialogFileService)
- `Parley/Utils/` - DebugLogger (handles log filtering), other utilities

### Critical File Integrity Features
**Orphan Node Handling** - When nodes become unreachable from START points (e.g., deleting a parent node), Parley moves them to a special container instead of deleting them. This prevents data loss in complex dialog structures.

**Key Rules** (see [Documentation/Developer/DELETE_BEHAVIOR.md](Documentation/Developer/DELETE_BEHAVIOR.md)):
- **IsLink=false**: Regular conversation flow (parent → child) - traversed for orphan detection
- **IsLink=true**: Back-reference from link child to shared parent - NEVER traversed for orphan detection
- Orphan detection uses graph traversal from START nodes following ONLY regular pointers
- Only root orphans added to container (prevents duplicates when orphan subtrees contain nested orphans)
- Link parents with IsLink=true back-references become orphaned when owning START is deleted
- See `MainViewModel.cs:CollectReachableNodesForOrphanDetection()` and `IsNodeInSubtree()` for implementation

## UI & Logging Rules
- Always scrub user info from logs and UI. Use `~` even for Windows user paths
- Always be theme and preferences aware. Never overwrite user color preferences
- **Theme System**: Uses JSON theme files in `Themes/` folder. ThemeManager applies colors dynamically
  - PC colors: Cool tones (blue, cyan) - mapped to `tree_reply`
  - Owner/NPC colors: Warm tones (orange, coral) - mapped to `tree_entry`
  - Accessibility themes: Deuteranopia, Protanopia, Tritanopia (colorblind-friendly)
  - Use `GetErrorBrush()`/`GetSuccessBrush()` helpers for validation colors (not hardcoded red/green)

### Path Handling (Privacy & Cross-Platform)
**NEVER use hardcoded user paths** - Use `Environment.GetFolderPath()` with `Environment.SpecialFolder` constants:

```csharp
// ❌ WRONG - Privacy leak and platform-specific
string path = @"C:\Users\...\Documents\file.txt";

// ✅ CORRECT - Cross-platform and privacy-safe
string path = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "file.txt");
```

**Common SpecialFolders**:
- `MyDocuments` - User's Documents folder
- `ApplicationData` - Roaming app data
- `LocalApplicationData` - Local app data
- `UserProfile` - User's home directory

## Quick Commands

### Development
```bash
# Run Parley (Avalonia UI)
dotnet run --project Parley/Parley.csproj

# Build solution
dotnet build Parley.sln

# Run regression tests before parser changes
cd TestingTools/Scripts
./QuickRegressionCheck.ps1 -OriginalFile "path/to/original.dlg" -ExportedFile "path/to/exported.dlg"
```

### Testing & Analysis
```bash
# Test Aurora compatibility
dotnet run --project TestingTools/DiagnoseCompatibility/DiagnoseCompatibility.csproj

# Analyze field patterns
dotnet run --project TestingTools/FieldIndicesProject/FieldIndicesProject.csproj

# Test boundary calculations
dotnet run --project TestingTools/BoundaryProject/BoundaryProject.csproj
```

## Aurora Engine Binary Format
- **Format**: Neverwinter Nights DLG v3.28+ (Aurora GFF binary)
- **Official Docs**: `../Documentation/BioWare_Original_PDFs/` (Radoub repo root)
- **Key Models**: Dialog, DialogNode, DialogPtr

## Technical Stack

### Core Technologies
- .NET 9.0 (cross-platform)
- **UI Framework**: Avalonia UI  - Windows, macOS, Linux support
- Aurora Engine GFF v3.28+ binary format compatibility
- Unified logging to `~\Parley\Logs`

### Development Patterns
- **MVVM Pattern**: ViewModels inherit from `BaseViewModel`
- **Circular Reference Protection**: TreeViewSafeNode prevents infinite loops
- **Session-based Logging**: Organized by date/time for debugging
- **Handler Pattern**: UI concerns separated into specialized handler classes
- **Cross-platform Paths**: Platform-agnostic file/directory handling
- **Plugin System**: Extensible via JSON manifest files in `Plugins/` folder
- **Theme System**: JSON-based themes in `Themes/` with dynamic resource application
- **Quality First**: It uses fewer tokens to do it right the first time than to do it quick and fix it later

## Aurora Compatibility Requirements
- **Exact binary format adherence** for game engine compatibility
- **Proper conversation flow** order preservation
- **Complete field structure** matching Aurora's expectations
- **Filename constraints**: 16 character max (excluding `.dlg`), lowercase, alphanumeric + underscore only
- **Link structures**: IsLink=true pointers create shared content (critical for orphan detection)

## Development Guidelines

### Pre-Commit Testing for Binary Format Changes
**CRITICAL**: Before committing changes to `DialogParser.cs` or `DialogWriter.cs`:
- **Run**: Self-test checklist (build + round-trip + log check)
- **Verify**: No errors/warnings in logs
- **Test Files**: Use lista.dlg (simple), myra.dlg (complex) for round-trip testing
- **DO NOT commit** if logs show invalid struct indices or buffer violations. Check for Warn and Error in logs
- **Tool**: Use `TestingTools/DiagnoseExport/` to validate exports preserve all data

### Console Applications
- **NEVER use Console.ReadKey()** - causes crashes with `dotnet run`
- Programs must exit cleanly without user interaction
- Use unified debugger for logging (console + file output)
- The user cannot see Console.Writeline.  If you need the user to read it, it needs to be in logs.

### Testing Organization
- Unit tests in `Parley.Tests/` (xUnit framework)
- Test projects organized in `TestingTools/`
- Debug scripts in `DebugScripts/`
- Test files in `TestingTools/TestFiles/`
- Run tests: `dotnet test Parley.Tests/Parley.Tests.csproj`

**Critical Tests for Orphan Handling**:
- `OrphanNodeTests.cs:OrphanContainer_ShouldNotDuplicateNestedOrphans` - Tests root orphan filtering (prevents duplicates)
- `OrphanNodeTests.cs:DeletingLinkParent_ShouldOrphanLinkedNodes` - Tests link parent orphaning when START deleted
- `OrphanContainerIntegrationTests.cs:DeletingParentEntry_CreatesOrphanContainer_AndPersistsToFile` - Tests full orphan flow with file persistence

### Testing Workflow (IMPORTANT)
**Feature-by-Feature Testing Approach**:
- User prefers to test each feature completely before moving to next
- Create comprehensive testing checklists for each feature in `Testing/` directory
- **DO NOT skip ahead** until current feature testing is user-verified
- Wait for user to complete testing and provide feedback/results
- Update testing checklists with user results and verification dates
- Use checkbox format for easy tracking (- [ ] for pending, - [x] for complete)
- Include verification section at bottom with date stamps

**Testing Checklist Requirements**:
- Simple, clear pass/fail criteria
- Organized by feature area
- Include Aurora compatibility tests
- Include regression testing section
- Add addendum section for fixes and known issues

**Follow-Up Testing Pattern** (IMPORTANT - Make this a habit):
After fixing issues reported by user:
1. **User reports issues** in testing checklist
2. **Claude fixes** the issues and commits
3. **Claude adds** "Follow-Up Testing" section to same checklist
   - List only the fixed items for re-testing
   - Include regression checks
   - Clear instructions for verification
   - Date stamp the follow-up section
4. **User verifies** fixes in follow-up section
5. **Update** checklist with verification results
6. **Repeat** if more issues found

**Follow-Up Section Format**:
```markdown
## Follow-Up Testing - [Fix Description] (YYYY-MM-DD)

**Instructions**: Restart application and re-test these fixed items

### [Fix Category 1]
- [ ] Test item 1
- [ ] Test item 2

### [Fix Category 2]
- [ ] Test item 3

### Regression Check
- [ ] Core features still work
- [ ] No new crashes
```

### Commit & PR Standards

**Commit Messages**:
- **Less is more**: What changed? Tests passing? Link issue if relevant
- **3 sentences max** unless complex technical detail needed
- **Clinical tone** - no advertising language
- **Emoji allowed** when it adds clarity 💡

Examples:
```
fix: Sanitize paths in settings logs (#49)
Tests: Privacy checks pass ✅
```

```
refactor: Remove CreateMinimalDlgStructure from DialogParser
Method never called - dead code cleanup.
```

**Pull Requests**:
- **15 sentences max** - longer? Consult first
- **What/Why/Tests** - skip fluff
- **Link issues**: `closes #X`, `relates to #Y`
- **Emoji for impact** - use judiciously

Example:
```
Fixes #49 - Path logging privacy leaks

Added SanitizePath() to 6 service files.
File operations now log with ~ paths.

Tests: ✅ Privacy workflow passes
       ✅ No build warnings
```

**Project-Specific**:
- Link commits to GH issues when applicable
- New documents go to the NonPublic folder. The user will move docs to Documentation after manual review.

**CHANGELOG Rules**:
- **Parley CHANGELOG** (`CHANGELOG.md`): Parley-specific changes only
- **Radoub CHANGELOG** (`../CHANGELOG.md`): Repository-level changes (documentation, shared resources, slash commands)
- **DO NOT** add Radoub-level changes to Parley's CHANGELOG

### Branch Workflow
- **Note**: Now part of the Radoub repository - see parent `../CLAUDE.md` for full branch strategy
- `main` branch - production-ready releases (protected by PR process)
- Feature branches: `parley/feature/name` or `parley/fix/name` for Parley-specific work
- All work via Pull Requests to `main`
- PR template: `../.github/PULL_REQUEST_TEMPLATE.md`
- No `develop` branch (solo developer, rapid iteration model)

**Branch Strategy**:
```
main (production)
  └── feature/fix branches → PR → main
```

**IMPORTANT WORKFLOW RULES** (Claude: Enforce these):

**✅ DO**:
- Always work on feature branches
- Create PRs for all merges to main
- Fill out PR template checklist completely
- Test thoroughly before merging
- Delete feature branches after merge
- Remind user of the process if they forget

**❌ NEVER**:
- Commit directly to `main` (production-only)
- Skip PR process (even for "small" changes)
- Merge without testing
- Leave feature branches open after merge

**Claude's Role**:
- **Remind user** if about to commit to main
- **Suggest** creating feature branch instead
- **Warn** if PR checklist incomplete
- **Encourage** proper commit messages
- **Be good faith enforcer** of workflow discipline

**Quick Start**:
```bash
git checkout main
git pull origin main
git checkout -b parley/feat/my-feature
# ... do work, commit ...
git push origin parley/feat/my-feature
# Create PR on GitHub: parley/feat/my-feature → main
```

## Session Management
- Use `TodoWrite` tool frequently for task tracking
- Commit regularly with technical context
- Check logs for Warn/Error before committing parser changes

## Architecture Notes

### MainViewModel Refactoring (Issue #99 - MOSTLY COMPLETE)
**MainViewModel has been significantly refactored - prefer adding logic to services/managers**

**Current Status**:
- MainViewModel.cs is ~1,744 lines (down from ~3,500+)
- Goal: Extract services and managers into separate classes - largely achieved
- Pattern: Services handle business logic, ViewModel coordinates UI state

**Refactoring Progress** (Epic #99):
- ✅ Phase 1: File operations → DialogFileService (PR #122)
- ✅ Phase 2: Undo/redo → UndoManager (PR #128)
- ✅ Phase 3: Clipboard → DialogClipboardService (PR #129)
- ✅ Phase 4: Scrap/orphan → ScrapManager + OrphanNodeManager (PR #130, #132)
- ✅ Phase 5: Properties panel → PropertyPanelPopulator (PR #135)
- ✅ Phase 6: Node operations → NodeOperationsManager (PR #137)
- ✅ Phase 7: Tree navigation → TreeNavigationManager (PR #133)
- ⏳ Phase 8-9: Search, validation (pending)

**RULES FOR NEW FEATURES**:
1. **DO NOT add new methods/logic to MainViewModel**
2. **DO** create new service classes in `Parley/Services/`
3. **DO** create new managers in appropriate locations
4. **DO** keep UI coordination minimal in ViewModel
5. **ASK** user if unsure where new logic should go

**Example Pattern**:
```csharp
// ❌ BAD - Adding logic to MainViewModel
public void DoComplexThing() { /* 100 lines */ }

// ✅ GOOD - Extract to service
private readonly ComplexThingService _complexService = new();
public void DoComplexThing() => _complexService.Execute(CurrentDialog);
```

### MainWindow Architecture
**MainWindow.axaml.cs is currently ~4,329 lines** - handles UI events and coordination

**Key Components**:
- **Handler classes** in `Parley/Handlers/` for complex operations:
  - FileOperationsHandler, ThemeAndSettingsHandler, TreeViewHandler
  - PropertiesPanelHandler, NodePropertiesHelper
- **FlowchartPanel** - Separate UserControl for visual flowchart view
- **ScriptParameterUIManager** - Dynamic script parameter UI generation

**UI Architecture Notes**:
- MainWindow coordinates UI events and delegates to handlers/services
- FlowchartPanel handles zoom, pan, node click events for visual flowchart
- Theme changes propagate through ThemeManager.ThemeApplied event

## Important Reminders

### Binary Format Development
- Never trust "it looks right" - require byte-perfect validation
- Round-trip testing catches silent corruption
- Field-by-field validation prevents mystery bugs

## Project Status

**Current Version**: v0.1.65-alpha (December 2025)

**Completed Features**:
- ✅ Core dialog editing with full Aurora compatibility
- ✅ DLG import/export (Aurora-compatible binary format)
- ✅ Resource browsers (sounds, scripts, characters, journals)
- ✅ Copy/paste/delete operations with orphan handling
- ✅ Undo/redo system
- ✅ Plugin system (custom themes, extensions)
- ✅ Theme system with colorblind accessibility themes
- ✅ Visual flowchart view with zoom/pan
- ✅ Script parameter editing with validation
- ✅ NPC speaker color preferences

**In Progress**:
- Shared GFF library (Radoub.Formats) integration
- JRL (Journal) reader/writer for Manifest tool

---

## GitHub Issue Tracking

### Issue Templates
Located in `../.github/ISSUE_TEMPLATE/` (Radoub repository):
- **Bug Report** (`bug_report.yml`) - Structured bug reporting with priority, repro steps
- **Testing Checklist** (`testing_checklist.yml`) - Track testing progress per feature

### Labels
Configured in `../.github/labels.yml` (Radoub repository):
- **Priority**: `priority-critical`, `priority-high`, `priority-medium`, `priority-low`
- **Types**: `bug`, `enhancement`, `aurora-compatibility`, `testing`, `checklist`
- **Status**: `needs-retest`, `verified-fixed`, `blocked`
- **Areas**: `ui`, `ux`, `performance`, `documentation`

**Note**: Tool-specific labels use `[Parley]` prefix in Radoub repository

### GitHub Actions Workflows
Located in `../.github/workflows/` (Radoub repository level):

**PR Build Check** (`pr-build.yml`):
- Triggers on PRs to main
- Validates solution builds successfully
- Catches build breaks before merge

**PR Test Suite** (`pr-tests.yml`):
- Triggers on PRs to main
- Runs all tests in TestingTools/
- Ensures no regressions introduced

**Release Build** (`release.yml`):
- Triggers on version tags (v*)
- Builds + full test suite
- Creates GitHub release with binaries
- Automated release notes

### Workflow Tips
- Use issue templates for consistent bug tracking
- **Link issues in commits**: Use magic keywords (`Closes #123`, `Fixes #456`, `Resolves #789`) to auto-close issues when PR merges
- **CHANGELOG references**: Reference both commit hash and GitHub issue (e.g., `Fixed #123 in commit abc1234`)
- Apply labels for easy filtering
- PRs auto-run build/test checks
- Tag releases: `git tag v1.0.0 && git push --tags`
- **Create GitHub issues for new work**: Track all tasks, bugs, and features as issues (not just TODOs in code)
- **Verify issues closed**: Before closing epics, ensure all related issues are closed (unless explicitly discussed)

---

## Code Quality & Security Guidelines

### Prevent Common Security Issues

**Path Handling**:
- NEVER hardcode absolute paths (e.g., `D:\LOM\Modules\`)
- ALWAYS use `Environment.GetFolderPath()` with SpecialFolder constants
- ALWAYS validate file paths with `Path.GetFullPath()` for path traversal prevention
- Use `ProcessStartInfo.ArgumentList` instead of string concatenation for process arguments

**Exception Handling**:
- NEVER use bare `catch` blocks - always catch specific exception types
- ALWAYS log exceptions with `UnifiedLogger.LogApplication(LogLevel.WARN, ...)`
- NEVER silently swallow exceptions - at minimum log them

**Input Validation**:
- ALWAYS use `TryParse()` methods instead of `Parse()` for user input
- ALWAYS validate plugin paths stay within plugin directory
- ALWAYS sanitize file paths before logging (use `~` for user directories)

### Prevent Code Quality Issues

**Avoid Dead Code**:
- NEVER commit commented-out code blocks - use git history instead
- ALWAYS remove test/debug code before committing (e.g., `AutoLoadTestFileAsync()`)
- Use `#if DEBUG` directives for debug-only code, not comments

**TODO Management**:
- ALWAYS create GitHub issues for TODOs instead of leaving them in code
- If TODO must stay, include issue number: `// TODO (#123): Implement feature`

**Method Size**:
- Keep methods under 100 lines - extract helper methods for clarity
- Split "God classes" into focused managers (e.g., CopyPasteManager, UndoRedoManager)

**Testing Requirements**:
- Fix failing tests before creating PR
- Address all compiler warnings before PR
- No hardcoded test paths in production code


### File Naming Constraints (CRITICAL FOR TEST FILE GENERATION)
**Aurora Engine enforces strict filename limitations - violations cause silent file rejection:**

**Filename Rules**:
- **Maximum**: 16 characters (excluding `.dlg` extension)
- **Case**: Lowercase recommended for compatibility
- **Characters**: Alphanumeric and underscore only (`a-z`, `0-9`, `_`)
- **Examples**:
  - ✅ `test1_link.dlg` (10 chars)
  - ✅ `merchant_01.dlg` (11 chars)
  - ❌ `Test1_SharedReply.dlg` (17 chars - TOO LONG, game will not load)
  - ❌ `my-dialog.dlg` (hyphen not allowed)

**Why This Matters**:
- Parley can open files with long names or invalid characters
- Aurora Engine silently rejects them - files appear "missing" in-game
- NWN toolset may also fail to display them in resource lists
- This is a **game engine limitation**, not a Parley bug

### Speaker Tag Validation (CRITICAL FOR IN-GAME FUNCTIONALITY)
**Aurora Engine validates speaker tags against creatures in the current area:**

**Speaker Tag Rules**:
1. **Empty speaker (`Speaker = ""`)**: Dialog works with any NPC (safest for test files)
2. **Tagged speaker (`Speaker = "Merchant"`)**: Aurora validates tag exists in area
   - If creature with tag "Merchant" exists → dialog works
   - If creature does NOT exist → **entire conversation discarded by game engine**
   - Engine protection: prevents dead NPCs from speaking, maintains immersion

**For Test Dialog Generation**:
- **ALWAYS use empty speaker tags (`Speaker = ""`)** unless testing specific speaker validation
- Tagged speakers require actual creature placement in test area
- Invalid speaker tags cause silent conversation failure in-game (no error message)

**Example**:
```csharp
// ❌ BAD - Fails if no creature tagged "Merchant"
Speaker = "Merchant"  // Aurora validates this!

// ✅ GOOD - Works in any context
Speaker = ""  // No validation required
```

### Test File Generation Best Practices
When creating test dialogs in `TestingTools/CreateTest*Dialog/`:

**Validation checklist**:
- [ ] Filename ≤16 characters (excluding `.dlg`)
- [ ] Lowercase, alphanumeric + underscore only
- [ ] All speaker tags empty (`Speaker = ""`) for generic tests
- [ ] Multiple entry points documented (requires .nss scripts to test all branches)
- [ ] Test in actual NWN game, not just Parley/Aurora Toolset

