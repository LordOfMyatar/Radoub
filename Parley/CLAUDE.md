# CLAUDE.md - Parley

Project guidance for Claude Code sessions working with Parley, the dialog editor for Neverwinter Nights.

## Get-Date
- Be sure to check what date it is. Get-Date on windows. It is not January 2025

## Logging
- Make sure logs are scrubbed for privacy
- Don't ask the user to log-dive. You review the logs.

## Session Continuity System

### Starting a New Session
**ALWAYS read these files first to understand current state:**

1. **`CLAUDE_SESSION_CHECKLIST.md`** - Session start/end checklist (if present)
2. **`Documentation/CODE_PATH_MAP.md`** - Active code paths for read/write operations (prevents working in dead code)
3. **Recent git commits** - Check latest progress with `git log --online -10`
4. **This file (CLAUDE.md)** - Project structure and commands

**Session Checklist enforces**:
- "3 Strikes Rule" for debugging (external validation after 3 failed attempts)
- Documentation updates after intensive debugging
- Binary format testing protocol (ask before NWN testing needed)
- Dead-end commit tracking (avoid re-walking abandoned paths)

### Current Focus Areas
Check recent commits and GitHub issues for active priorities.

**Project Status**:


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
- `Parley/Parsers/DialogParser.cs` - Core DLG parser with Aurora compatibility
- `Parley/Models/` - Dialog, DialogNode, DialogPtr data structures
- `Parley/ViewModels/MainViewModel.cs` - MVVM pattern with deletion logic and **orphan handling**
- `Parley/Handlers/` - UI event handlers (refactored from MainWindow for maintainability)
- `Parley/Services/` - Sound, Script, Settings services

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
- always scrub user info from logs and UI  use ~ even for windows for user path.
- Always be theme aware.  We do not want to overwrite user color preferences

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

## File Format & Documentation

### Aurora Engine Binary Format
- **Format**: Neverwinter Nights DLG v3.28+ (Aurora GFF binary)
- **Official Docs**: `Documentation/BioWare_Original_PDFs/`
- **Format Analysis**: `Documentation/DLG_FORMAT_SPECIFICATION.md`

### Key Models
- `Dialog` - Root conversation container
- `DialogNode` - Individual entries/replies with text and metadata
- `DialogPtr` - Pointer structures linking dialog nodes

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
- **Handler Pattern**: UI concerns separated into specialized handler classes (Oct 2025 refactor)
- **Cross-platform Paths**: Platform-agnostic file/directory handling
- **Quality First**: It uses fewer tokens to do it right the first time than to do it quick and fix it later

## Nature of Conversation Files

### Content Characteristics
- **Storytelling Tool**: Dialog files vary significantly based on narrative content
- **Conversation Loops**: Many files resemble 'phone trees' with intentional branching
- **Dynamic Structure**: Field counts and complexity depend on conversation design

### Aurora Compatibility Requirements
- Exact binary format adherence for game engine compatibility
- Proper conversation flow order preservation
- Complete field structure matching Aurora's expectations

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
- Create PRs for all merges (feature → develop, develop → main)
- Fill out PR template checklist completely
- Test thoroughly before merging
- Delete feature branches after merge
- Remind user of the process if they forget

**❌ NEVER**:
- Commit directly to `main` (production-only)
- Commit directly to `develop` (use feature branches)
- Skip PR process (even for "small" changes)
- Merge without testing
- Leave feature branches open after merge

**Claude's Role**:
- **Remind user** if about to commit to main/develop
- **Suggest** creating feature branch instead
- **Warn** if PR checklist incomplete
- **Encourage** proper commit messages
- **Be good faith enforcer** of workflow discipline

**Quick Start**:
```bash
git checkout develop
git pull origin develop
git checkout -b feature/my-feature
# ... do work, commit ...
git push origin feature/my-feature
# Create PR on GitHub: feature/my-feature → develop
```

## Aurora Engine Specifics

### Critical Discoveries
- Complex field index mapping (not simple 4:1 sequential)
- Root struct must use type 65535 (not 0)
- All 9 BioWare required fields must be present in correct order

## Session Management

### Progress Tracking
- Use `TodoWrite` tool frequently for task tracking
- Commit regularly with technical context

### Debugging Tools
- Comprehensive logging system for binary format analysis
- Hex dump comparison tools for Aurora compatibility
- Round-trip testing for validation
- Boundary analysis for format compliance

## Architecture Notes

### MainWindow Refactoring (October 2025)
**IMPORTANT: MainWindow.xaml.cs has been refactored to ~370 lines (down from 1736 lines)**

Handler classes in `Parley/Handlers/`:
- **FileOperationsHandler** - Open, save, recent files operations
- **ThemeAndSettingsHandler** - Theme switching, font size, game directories, script cache
- **TreeViewHandler** - Tree expand/collapse, selection, copy operations
- **PropertiesPanelHandler** - Properties panel population, parameters, script preview
- **NodePropertiesHelper** - Static helpers for node property string building

MainWindow.xaml.cs now acts as a thin coordinator between XAML events and handler classes.

**DO NOT move logic back into MainWindow.xaml.cs - keep it delegating to handlers.**

## Important Reminders

### Binary Format Development
- Never trust "it looks right" - require byte-perfect validation
- Round-trip testing is essential for catching silent corruption
- Field-by-field validation prevents mystery bugs

### Project Organization
- Keep test files in `TestingTools/`
- Archive outdated documentation in `OldDocumentation/`
- Maintain clean separation between core code and testing
- Use unified debugger for consistent logging

## Development Roadmap

### Project Status

**Released**: v0.1.0-alpha (November 2, 2025)
- Core dialog editing - ✅ **COMPLETE**
- DLG import/export (Aurora-compatible) - ✅ **COMPLETE**
- Resource browsers (sounds, scripts, characters, journals) - ✅ **COMPLETE**
- Copy/paste/delete operations - ✅ **COMPLETE** (Issue #6 fixed)
- Undo/redo system - ✅ **COMPLETE**

**Current Focus**: Bug fixes and stability improvements based on community feedback

---

## Important Reminders

### Aurora File Constraints
- Aurora files have file name limit for compatibility with FAT16 - use compliant names
- Entry and reply structs will be different conversation file to conversation file
- Conversations can be very long, loopy, and have lots of links
- Link structures create shared content (IsLink=true pointers) - critical for orphan detection logic

### Cross-Platform Considerations (NEW)
- File paths must work on Windows, macOS, and Linux
- Default game locations vary by platform:
  - Windows: `C:\Users\...\Neverwinter Nights`
  - macOS: `~/Library/Application Support/Neverwinter Nights`
  - Linux: `~/.local/share/Neverwinter Nights`
- Keyboard shortcuts differ (Ctrl vs Cmd)
- Path separators handled by .NET automatically

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
- Triggers on PRs to main/develop
- Validates solution builds successfully
- Catches build breaks before merge

**PR Test Suite** (`pr-tests.yml`):
- Triggers on PRs to main/develop
- Runs all tests in TestingTools/
- Ensures no regressions introduced

**Release Build** (`release.yml`):
- Triggers on version tags (v*)
- Builds + full test suite
- Creates GitHub release with binaries
- Automated release notes

### GitHub MCP Integration
Via foxxy-bridge:
- Create/manage issues programmatically
- Auto-update checklists from test results
- Link commits to issues
- Generate testing reports

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
- Fix failing tests before creating PR (LazyLoadingPerformanceTests currently failing)
- Address all compiler warnings before PR
- No hardcoded test paths in production code

### Build Warnings to Fix

**Current warnings that need attention**:
1. `CA2022` - Inexact read in CreatureParser.cs - use ReadExactly()
2. `AVLN3001` - Missing public constructors for XAML windows
3. `CS0219` - Unused variable in TimeoutTests.cs
4. `CS8625/CS8600` - Nullable reference warnings in tests

---

**Testing**: Use `TestingTools/Scripts/QuickRegressionCheck.ps1` before parser changes


### File Naming Constraints (CRITICAL FOR TEST FILE GENERATION)
**Aurora Engine enforces strict filename limitations - violations cause silent file rejection:**

**Filename Rules**:
- **Maximum**: 12 characters (excluding `.dlg` extension)
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

**Real-World Example** (2025-11-08 debugging session):
```csharp
// ❌ BAD - Will fail in-game if no creature tagged "Merchant" exists
var entry = new DialogNode {
    Type = DialogNodeType.Entry,
    Text = new LocString(),
    Speaker = "Merchant"  // Aurora validates this!
};

// ✅ GOOD - Works in any conversation context
var entry = new DialogNode {
    Type = DialogNodeType.Entry,
    Text = new LocString(),
    Speaker = ""  // No validation required
};
```

**Debugging Symptoms**:
- Dialog loads in Parley ✅
- Dialog appears in Aurora Toolset ✅
- Conversation **does not appear in-game** ❌ (silent failure)
- No error messages or logs
- Root cause: Invalid speaker tag validation

### Test File Generation Best Practices
When creating test dialogs in `TestingTools/CreateTest*Dialog/`:

1. **Filename validation**:
   ```csharp
   // ALWAYS check filename length before saving
   string filename = "test1_link.dlg";  // 10 chars ✅
   if (Path.GetFileNameWithoutExtension(filename).Length > 12) {
       Console.WriteLine("❌ Filename too long for Aurora Engine!");
   }
   ```

2. **Speaker tag safety**:
   ```csharp
   // Use empty speaker for test files
   var node = new DialogNode {
       Speaker = ""  // Safe default
   };
   ```

3. **Multiple entry points and in-game testing**:
   - Dialogs with multiple starting entries (disconnected branches) create independent conversation flows
   - NWN fires one starting entry per conversation interaction sequentially
   - Comprehensive testing requires state management scripts (.nss) to cycle through all branches
   - **Pattern**: "Do-once" system using local variables (GetLocalInt/SetLocalInt)
   - **Example**: Track which starting entry was shown, fire next entry on subsequent interactions
   - **Future improvement**: Create reusable test scaffolding scripts in `TestingTools/Scripts/`
   ```csharp
   // Multiple starting entries - each fires once per conversation interaction
   dialog.Starts.Add(new DialogPtr { Node = entry0, ... });
   dialog.Starts.Add(new DialogPtr { Node = entry2, ... });  // Requires .nss to test both
   dialog.Starts.Add(new DialogPtr { Node = entry3, ... });
   ```

4. **Validation checklist**:
   - [ ] Filename ≤12 characters (excluding `.dlg`)
   - [ ] Lowercase, alphanumeric + underscore only
   - [ ] All speaker tags empty OR valid creatures exist in test area
   - [ ] Multiple entry points documented for tester (requires state management scripts)
   - [ ] Test in actual NWN game, not just Parley/Aurora Toolset

