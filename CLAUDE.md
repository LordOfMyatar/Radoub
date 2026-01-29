# CLAUDE.md - Radoub Toolset

Project guidance for Claude Code sessions working with the Radoub multi-tool repository.

---

## Repository Overview

**Radoub** is a multi-tool repository for Neverwinter Nights modding. Each tool maintains its own codebase, documentation, and development workflow within its subdirectory.

### Current Tools

- **Parley**: Dialog editor (`.dlg` files) - See `Parley/CLAUDE.md` for tool-specific guidance
- **Manifest**: Journal editor (`.jrl` files) - See `Manifest/CLAUDE.md` for tool-specific guidance
- **Quartermaster**: Creature/inventory editor (`.utc`, `.bic` files) - See `Quartermaster/CLAUDE.md` for tool-specific guidance
- **Fence**: Merchant/store editor (`.utm` files) - See `Fence/CLAUDE.md` for tool-specific guidance
- **Trebuchet**: Radoub launcher/hub - See `Trebuchet/CLAUDE.md` for tool-specific guidance

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
│   └── BioWare_Original_PDFs/ (original BioWare PDFs)
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
├── Fence/ (merchant/store editor)
│   ├── CLAUDE.md (Fence-specific guidance)
│   ├── CHANGELOG.md (Fence-specific changes)
│   ├── Fence/ (source code)
│   └── Fence.Tests/ (unit tests)
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

## New Tool Bootstrap Checklist

**CRITICAL**: Before writing any code for a new tool, complete this checklist. This prevents pattern drift and rework.

### Reference Implementation

**Quartermaster is the canonical reference** for new Radoub tools. Study its structure before starting.

### Pre-Coding Checklist

Before writing code, verify you understand these patterns by reading the reference files:

| Component | Reference File | Purpose |
|-----------|---------------|---------|
| Program.cs startup | `Quartermaster/Quartermaster/Program.cs` | Logging init, command line, Avalonia setup |
| CommandLineService | `Quartermaster/Quartermaster/Services/CommandLineService.cs` | `--file`, `--safemode`, `--help` pattern |
| SettingsService | `Quartermaster/Quartermaster/Services/SettingsService.cs` | JSON settings, theme, font persistence |
| MainWindow structure | `Quartermaster/Quartermaster/Views/MainWindow.axaml` | Menu bar, status bar, panel layout |
| Panel controls | `Quartermaster/Quartermaster/Controls/` | BasePanelControl inheritance |
| Theme support | `Radoub.UI/Radoub.UI/Services/ThemeManager.cs` | Dark/light theme, custom themes |

### Required Components (Every Tool)

```
ToolName/
├── ToolName/
│   ├── Program.cs                    # Copy from Quartermaster, update namespace
│   ├── App.axaml + App.axaml.cs      # Avalonia app setup
│   ├── Services/
│   │   ├── CommandLineService.cs     # --file, --safemode, --help
│   │   └── SettingsService.cs        # Tool-specific settings + theme
│   ├── Views/
│   │   ├── MainWindow.axaml          # Standard menu structure
│   │   └── Dialogs/                  # About, Settings windows
│   ├── ViewModels/
│   │   └── MainWindowViewModel.cs    # MVVM pattern
│   └── Controls/                     # Custom controls
├── ToolName.Tests/                   # Unit tests from day 1
├── CLAUDE.md                         # Tool-specific guidance
├── CHANGELOG.md                      # Initialized with first version
└── README.md                         # User-facing documentation
```

### Shared Library References

Every tool should reference these shared libraries:

| Library | Purpose | Required? |
|---------|---------|-----------|
| `Radoub.Formats` | GFF, 2DA, TLK, KEY/BIF parsing | Yes |
| `Radoub.UI` | ThemeManager, ScriptBrowser, AboutWindow, shared controls | Yes |
| `Radoub.Dictionary` | Spell-checking for text fields | If tool has text editing |

**AboutWindow**: Use the universal `AboutWindow` from `Radoub.UI` - don't create tool-specific About dialogs.

### Spell-Check Integration (Radoub.Dictionary)

For tools with game-facing text (dialog, journal entries, descriptions), use `Radoub.Dictionary`:

**User Dictionary Location**: `~/Radoub/Dictionaries/`
- `custom.dic` - JSON format (programmatic)
- `custom_dictionary.txt` - Text format (user-editable, one word per line)

**Usage Pattern** (see Parley/Manifest for examples):

```csharp
// In App.axaml.cs or startup
var dictionaryManager = new DictionaryManager();
var spellChecker = new SpellChecker(dictionaryManager);
await spellChecker.LoadBundledDictionaryAsync("en_US", loadNwnDictionary: true);

// Load user's custom words
var userDict = UserDictionaryService.Instance;
foreach (var word in userDict.Words)
{
    dictionaryManager.AddWord(word);
}

// Check spelling
if (!spellChecker.IsCorrect("wrod"))
{
    var suggestions = spellChecker.GetSuggestions("wrod");
}

// Add to custom dictionary
userDict.AddWord("Waterdeep");
```

**Included Dictionaries**:
- Hunspell `en_US` (bundled)
- NWN/D&D terms (bundled)
- User custom words (shared across all tools)

**NWN-Specific Features**:
- Token variables (`<FirstName>`, `<CUSTOM1016>`) are ignored during spell-check
- D&D terminology pre-loaded (races, classes, spell names)

### Implementation Checklist

**Day 1 Requirements** (before writing significant code):

- [ ] **Read Quartermaster/Program.cs** - Understand startup sequence
- [ ] **Copy CommandLineService pattern** - Same flags, same behavior
- [ ] **Copy SettingsService pattern** - JSON storage in `~/Radoub/ToolName/`
- [ ] **Use ThemeManager from Radoub.UI** - Don't reinvent theming
- [ ] **Use IGameDataService** for any 2DA/TLK data - Never hardcode game data
- [ ] **Inherit BasePanelControl** for panel controls - Consistent styling
- [ ] **Add to Radoub.sln** - Root solution builds all tools
- [ ] **Initialize CHANGELOG.md** with branch/PR format
- [ ] **Create CLAUDE.md** with tool-specific patterns
- [ ] **Add dictionary support** if tool has text editing fields

### Trebuchet Integration

New tools must integrate with Trebuchet (the Radoub launcher):

- [ ] **Add to ToolLauncherService** - Register tool for discovery and launch
- [ ] **Support `--file` argument** - Enable launching with a file from Trebuchet's MRU dropdown
- [ ] **Use Radoub.UI file browsers** - Use `ModuleBrowserWindow`, `ScriptBrowserWindow`, `DialogBrowserWindow`, or create resource-specific browser instead of OS file pickers for module resources

### Testing Requirements

- [ ] **Create unit test project** - `ToolName.Tests/` with xUnit
- [ ] **Add smoke test** - Basic UI integration test using FlaUI
- [ ] **Add to run-tests.ps1** - Include tool in `-Tool` ValidateSet parameter
- [ ] **Test file launch** - Verify `--file` argument opens the file correctly

### UI Uniformity Checklist (Epic #959)

**Required for all tools** - ensures consistency, performance, and reliability:

| Category | Requirement | Reference |
|----------|-------------|-----------|
| **AboutWindow** | Use `AboutWindow.Create(AboutWindowConfig)` from Radoub.UI | Trebuchet |
| **Version Display** | Use shared `VersionHelper` (not hardcoded strings) | Parley/Utils/VersionHelper.cs |
| **Modal Dialogs** | Use `Show()` not `ShowDialog()` for info windows | CLAUDE.md UI/UX Guidelines |
| **Theme Brushes** | Use `BrushManager` for Success/Warning/Error colors | Radoub.UI/Services/BrushManager.cs |
| **Deferred Loading** | Heavy I/O in `Loaded`/`Opened` events, not constructor | Fence/Views/MainWindow.axaml.cs |
| **Progress Feedback** | Show status for operations >2 seconds | Fence palette loading pattern |
| **Caching** | Cache game data if applicable (feats, spells, items) | Fence/Services/PaletteCacheService.cs |
| **TLK Support** | Use shared ITlkService for localized strings | Radoub.UI/Services/ITlkService.cs |
| **Spell-Check** | Use Radoub.Dictionary for game-facing text | Parley/Manifest patterns |

**Resource Browsers** (use shared implementations from Radoub.UI):

| Browser | Interface | Use When |
|---------|-----------|----------|
| ScriptBrowserWindow | IScriptBrowserContext | Tool needs script selection |
| SoundBrowserWindow | ISoundBrowserContext | Tool needs sound selection |
| PortraitBrowserWindow | IPortraitBrowserContext | Tool needs portrait selection |

**Anti-Patterns** (do NOT do these):

| Anti-Pattern | Correct Approach |
|--------------|------------------|
| Custom AboutWindow | Use `AboutWindow.Create()` |
| Hardcoded version "0.1.0-alpha" | Use `VersionHelper.GetVersion()` |
| `await dialog.ShowDialog(this)` for info | `dialog.Show(this)` |
| Duplicate brush methods in each file | Use `BrushManager.GetSuccessBrush()` |
| Load game data in constructor | Defer to `Loaded` event |
| Rebuild game data every launch | Cache to `~/Radoub/{Tool}/` |
| Custom TLK loading | Use shared ITlkService |
| Custom portrait browser | Use shared PortraitBrowserWindow |

### Versioning

**Version Location**: Set in `.csproj` file PropertyGroup:

```xml
<!-- Version Configuration -->
<Version>0.1.0-alpha</Version>
<AssemblyVersion>0.1.0.0</AssemblyVersion>
<FileVersion>0.1.0.0</FileVersion>
<InformationalVersion>0.1.0-alpha</InformationalVersion>
```

**Initial Version**: New tools start at `0.1.0-alpha`

**Semantic Versioning Rules**:
| Version | When to Bump |
|---------|--------------|
| **Major** (1.0.0) | Breaking changes, major rewrites, stable release |
| **Minor** (0.2.0) | New features, significant enhancements |
| **Patch** (0.1.1) | Bug fixes, small improvements |
| **Prerelease** (-alpha, -beta) | Development builds before stable |

**Version Coordination**:
- CHANGELOG version sections must match `.csproj` version
- Update both when preparing a release
- GitHub releases use the same version tag (e.g., `v0.1.0-alpha`)

**Alpha vs Beta vs Stable**:
- `alpha`: Active development, features incomplete, may have bugs
- `beta`: Feature complete, testing phase, API may change
- (no suffix): Stable release, production ready

### Common Mistakes to Avoid

| Mistake | Correct Pattern |
|---------|-----------------|
| Hardcoding game data (races, classes, etc.) | Use IGameDataService + 2DA files |
| Custom theme implementation | Use Radoub.UI ThemeManager |
| Modal dialogs for messages | Use non-modal or toast notifications |
| Settings in app folder | Store in `~/Radoub/ToolName/settings.json` |
| Missing SafeMode support | Always implement `--safemode` flag |
| Skipping unit tests | Create ToolName.Tests from day 1 |
| Missing version in .csproj | Set Version, AssemblyVersion, FileVersion |

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
| **Fence** | `Fence/CHANGELOG.md` | Fence-specific changes |
| **Trebuchet** | `Trebuchet/CHANGELOG.md` | Trebuchet-specific changes |

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

## Bash File Operations on Windows

**Use forward slashes and Unix commands** - they work in Git Bash on Windows:

```bash
# Copy files (WORKS)
cp d:/LOM/workspace/Radoub/source/file.txt d:/LOM/workspace/Radoub/dest/

# Create directories (WORKS)
mkdir -p d:/LOM/workspace/Radoub/new/path/

# List files (WORKS)
ls -la d:/LOM/workspace/Radoub/some/path/

# Remove and recreate (WORKS)
rm d:/path/to/file && mkdir d:/path/to/dir
```

**AVOID**:
- Backslashes in paths (escaping issues)
- PowerShell with `$` variables (get stripped)
- `cmd /c` commands (quoting problems)
- `xcopy` with `/Y` flags (parsing issues)

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

**Progress Indicator Standards**:

Operations should provide appropriate feedback based on duration:

| Duration | Feedback Type | Implementation |
|----------|---------------|----------------|
| <2 seconds | Status bar text | Update `StatusText` property |
| 2-5 seconds | Indeterminate progress | `StatusBarControl.ShowProgress = true` |
| >5 seconds | Progress with status updates | Update status text periodically |

**Pattern Examples**:
```csharp
// Brief operation - status text only
StatusText = "Saving...";
await SaveFileAsync();
StatusText = "Ready";

// Medium operation - indeterminate progress
ShowProgress = true;
StatusText = "Loading palette...";
await LoadPaletteAsync();
ShowProgress = false;
StatusText = "Ready";

// Long operation - progress with updates
ShowProgress = true;
for (int i = 0; i < items.Count; i++)
{
    StatusText = $"Processing {i + 1} of {items.Count}...";
    await ProcessItemAsync(items[i]);
}
ShowProgress = false;
StatusText = $"Processed {items.Count} items";
```

**Guidelines**:
- Always reset status bar to "Ready" when operation completes
- Use `Radoub.UI.Controls.StatusBarControl` for consistent styling
- For cancellable operations, include cancel button or Escape key handling
- Prefer `IsIndeterminate=true` when total progress is unknown

**Deferred Loading Patterns** (Epic #959, Sprint 4):

**Lifecycle Event Responsibilities**:

| Event | Purpose | Example Operations |
|-------|---------|-------------------|
| Constructor | Light synchronous setup | `InitializeComponent()`, create services, register events |
| Loaded | UI restoration | Restore panel sizes, debug settings, combo box selections |
| Opened | Heavy async work | Initialize caches, load palettes, handle startup file |

**Recommended Startup Pattern**:
```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = _viewModel;

    // Fast services only (no I/O)
    _gameDataService = new GameDataService();
    _displayService = new CreatureDisplayService(_gameDataService);

    InitializeUI();  // Fast UI setup
    RestoreWindowPosition();

    Loaded += OnWindowLoaded;
    Opened += OnWindowOpened;
}

private void OnWindowLoaded(object? sender, RoutedEventArgs e)
{
    RestoreDebugSettings();
    RestorePanelSizes();
}

private async void OnWindowOpened(object? sender, EventArgs e)
{
    Opened -= OnWindowOpened;  // Unsubscribe immediately

    UpdateStatus("Loading game data...");
    await InitializeCachesAsync();  // Sequential async
    _ = StartPaletteLoadAsync();     // Fire-and-forget
    await HandleStartupFileAsync();
    UpdateStatus("Ready");
}
```

**Fire-and-Forget Pattern**:
```csharp
// Use underscore to suppress compiler warning
_ = LoadBaseItemTypesAsync();
_ = ScanCreaturesAsync(token);
```

**Cancellation Token Pattern**:
```csharp
private CancellationTokenSource? _loadCts;

private void StartLoad()
{
    _loadCts?.Cancel();  // Cancel previous operation
    _loadCts = new CancellationTokenSource();
    _ = LoadAsync(_loadCts.Token);
}

private async Task LoadAsync(CancellationToken token)
{
    try
    {
        await DoWorkAsync(token);
    }
    catch (OperationCanceledException)
    {
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Load cancelled");
    }
}
```

**Anti-Patterns to Avoid**:

| Anti-Pattern | Problem | Fix |
|--------------|---------|-----|
| `await` in constructor | Blocks window show | Move to `Opened` event |
| No cancellation token | Orphaned tasks | Use `CancellationTokenSource` |
| Status not reset | "Loading..." stuck | Always reset to "Ready" |
| No error handling | Silent failures | Catch and log in async methods |

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

**SECONDARY Reference**: BioWare Aurora Specifications (Wiki or `Documentation/BioWare_Original_PDFs/`)
- Markdown conversions available in the Wiki: https://github.com/LordOfMyatar/Radoub/wiki
- Original PDFs in `Documentation/BioWare_Original_PDFs/`
- Specs are 20 years old and may not reflect modern edge cases
- Good for "why" questions, not "how to handle X" questions

**Implementation Approach**:
1. Write parsers in C# (native to Radoub toolset)
2. Follow neverwinter.nim's edge case handling and validation patterns

---

## GitHub Issue Cache

A local cache of GitHub issues/PRs is available for quick lookups without API calls.

**Location**: `.claude/cache/github-data.json`

**Refresh Cache**:
```powershell
.\.claude\scripts\Refresh-GitHubCache.ps1        # Only if stale (>1 hour)
.\.claude\scripts\Refresh-GitHubCache.ps1 -Force # Force refresh
```

**Use Cases**:
- Check if tech debt issue exists for a file before creating duplicate
- Find related issues when investigating a bug
- Review open PRs before starting new work
- Check issue labels and status

**Cache Contents**:
- Open issues with labels, assignees, milestones
- Open PRs with status and review state
- Summary stats (stale issues, missing labels)
- Refreshes automatically when >1 hour old

**Example**: Before creating a tech debt issue for `CharacterPanel.axaml.cs`, search the cache:
```powershell
Select-String "CharacterPanel" .\.claude\cache\github-data.json
```

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
