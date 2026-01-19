# Changelog - Radoub

All notable changes to Radoub (repository-level) will be documented in this file.

For tool-specific changes, see the individual tool changelogs:
- [Parley CHANGELOG](Parley/CHANGELOG.md)
- [Manifest CHANGELOG](Manifest/CHANGELOG.md)
- [Quartermaster CHANGELOG](Quartermaster/CHANGELOG.md)

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.9.41] - 2026-01-19
**Branch**: `radoub/issue-983` | **PR**: #996

### Sprint: UI Uniformity - Theme & Dictionary (#983)

Parent Epic: #959 - UI Uniformity & Shared Infrastructure

#### Work Items
- [x] #976 - [Radoub] Radoub-level theme configuration
- [x] #977 - [Trebuchet] Theme generator/editor UI
- [ ] #978 - [Radoub.Dictionary] Consolidate spell-check for game-facing text

#### Bundled Dependabot Updates (Avalonia 11.3.10 → 11.3.11)
- [x] #991 - Bump Avalonia and Avalonia.Controls.DataGrid (Radoub.UI)
- [x] #992 - Bump Avalonia and Avalonia.Diagnostics (Parley)
- [x] #993 - Bump Avalonia and Avalonia.Fonts.Inter (Parley)
- [x] #994 - Bump Avalonia.Headless.XUnit (Parley.Tests)
- [x] #995 - Bump Avalonia and Avalonia.Skia (Parley)

#### Changed
- Upgraded Avalonia packages from 11.3.10 to 11.3.11 across all tools:
  - Parley, Manifest, Quartermaster, Fence, Trebuchet (main apps)
  - Radoub.UI (shared library)
  - Test projects (Parley.Tests, Quartermaster.Tests, Fence.Tests)

#### Added (RadoubSettings - #976)
- `SharedThemeId` - Shared theme ID applied to all tools
- `UseSharedTheme` - Toggle for shared vs tool-specific themes (default: true)
- `HasSharedTheme` - Check if shared theme is configured
- `GetSharedThemesPath()` - Returns `~/Radoub/Themes/` for shared themes

#### Added (ThemeManager - #976)
- Radoub-level shared themes folder: `~/Radoub/Themes/`
- `Initialize(toolName, useSharedTheme)` - New overload with shared theme support
- `GetEffectiveThemeId(toolThemeId)` - Returns shared or tool-specific theme ID
- `ApplyEffectiveTheme(toolThemeId)` - Apply effective theme with fallback
- `IsUsingSharedTheme` - Check if currently using shared theme
- Theme discovery now includes: official (app) → shared (`~/Radoub/Themes/`) → tool-specific

#### Added (Trebuchet - #977)
- **Theme Editor Window**: Visual theme creation with live preview
  - Core color editing: Background, Sidebar, Text, Accent, Selection, Border
  - Status colors: Success, Warning, Error, Info
  - Font settings: Primary font, Monospace font, Base size
  - Load from preset themes
  - Export to `~/Radoub/Themes/` for shared use
- **Settings Window**: Added "Create Theme..." button and "Apply to All Tools" checkbox

---

## [0.9.40] - 2026-01-18
**Branch**: `radoub/issue-982` | **PR**: #989

### Sprint: UI Uniformity - Caching & Performance (#982)

Parent Epic: #959 - UI Uniformity & Shared Infrastructure

#### Work Items
- [x] #973 - [Radoub.Formats] Extract shared cache service pattern
- [x] #974 - [Quartermaster] Implement caching for feats/spells/BioWare items
- [x] #975 - [Radoub] Standardize deferred loading patterns

#### Added (Radoub.UI - #973)
- `GameDataCacheService<T>` - Generic file-based caching service
  - JSON serialization with compact format
  - Automatic game path validation (invalidates cache when paths change)
  - Version checking for cache format upgrades
  - Methods: `HasValidCache()`, `LoadCache()`, `SaveCacheAsync()`, `ClearCache()`, `GetCacheInfo()`
  - Cache stored at `~/Radoub/{ToolName}/{cacheName}_cache.json`
- `CacheWrapper<T>` - Generic cache envelope with metadata
- `CacheInfo` - Cache metadata for display in Settings UI

#### Changed (Fence - #973)
- `PaletteCacheService` now wraps shared `GameDataCacheService<CachedPaletteItem>`
- Removed duplicate `CacheInfo` class (uses shared implementation)

#### Added (Quartermaster - #974)
- `FeatCacheService` - File-based caching for feat data
  - Caches feat name, description, category, isUniversal
  - Caches all feat IDs from feat.2da scan
  - Memory cache for class/race granted feats
  - Cache stored at `~/Radoub/Quartermaster/feats_cache.json`
- `FeatService` now uses caching for all lookups
  - `InitializeCacheAsync()` - Load from disk or build from 2DA
  - `RebuildCacheAsync()` - Clear and rebuild cache
  - Direct lookup fallback when cache unavailable
- `CreatureDisplayService.InitializeCachesAsync()` - Initialize all caches on startup
- Cache initialization during `MainWindow.OnWindowOpened` with status feedback

#### Documentation (CLAUDE.md - #975)
- Added "Deferred Loading Patterns" section to UI/UX Guidelines
  - Lifecycle event responsibilities (Constructor vs Loaded vs Opened)
  - Recommended startup pattern with code example
  - Fire-and-forget async pattern (`_` syntax)
  - Cancellation token pattern for interruptible operations
  - Anti-patterns table (blocking constructor, missing cancellation, etc.)

---

## [0.9.39] - 2026-01-18
**Branch**: `radoub/issue-972` | **PR**: #986

### Sprint: UI Uniformity - Browsers & TLK (#981)

Parent Epic: #959 - UI Uniformity & Shared Infrastructure

#### Work Items
- [x] #970 - [Radoub.UI] Consolidate resource browsers (Script, Sound, Portrait)
- [x] #971 - [Radoub.UI] Create shared ITlkService for multilingual support
- [x] #972 - [Radoub.UI] Standardize image sizing and portrait handling

#### Added (Radoub.UI - #970)
- `IPortraitBrowserContext` interface for portrait browser integration
  - `PortraitEntry` model with ID, ResRef, Race, Sex metadata
  - `ListPortraits()` for portraits.2da enumeration
  - `GetPortraitBitmap()` for portrait image loading
  - `GetRaceName()` for race display name resolution
- `ISoundBrowserContext` interface for sound browser integration
  - `SoundEntry` model with ResRef, Source, IsMono, IsFromHak/Bif flags
  - `ListSounds()` with source filtering (Override/HAK/BIF)
  - `ExtractToTemp()` for archived sound playback
  - `Play()`/`Stop()` for audio control
- Note: Existing `IScriptBrowserContext` already in Radoub.UI serves as pattern

#### Added (Radoub.UI - #971)
- `ITlkService` interface for unified TLK string resolution
  - Primary TLK (dialog.tlk) and Custom TLK (module/server) support
  - `GetString()` - Resolve StrRef with automatic custom TLK routing
  - `ResolveLocString()` - Full CExoLocString resolution with fallback chain
  - `DetectAvailableLanguages()` - Scan game installation for languages
  - `GetTlkPath()` - Find TLK file for specific language/gender
- `TlkService` implementation with:
  - Language selection (English, French, German, Italian, Spanish, Polish, Asian languages)
  - Gender-aware string resolution (dialogf.tlk support)
  - Embedded string priority over StrRef
  - Fallback language chain (English → French → German → Italian → Spanish)
- 28 new unit tests for TlkService

#### Added (Radoub.UI - #972)
- `ImageHelper` static service for consistent image/portrait handling
  - Standard NWN portrait sizes: Tiny (32x40), Small (64x100), Medium (128x200), Large (256x400), Huge (512x800)
  - `CalculateFitSize()` - Aspect-ratio-aware scaling to target bounds
  - `CalculatePortraitSize()` - Calculate portrait height from width
  - `GetNearestPortraitSize()` - Map arbitrary width to nearest standard size
  - `ResizeBitmap()` / `ResizePortrait()` - High-quality SkiaSharp resize
  - `GetMissingPortraitPlaceholder()` - Gray silhouette placeholder for missing portraits
  - `IsValidBitmap()`, `GetAspectRatio()`, `IsPortraitAspect()` - Validation utilities
- `Radoub.UI.Tests` project added to solution with 27 ImageHelper tests

---

## [0.9.38] - 2026-01-18
**Branch**: `radoub/issue-980` | **PR**: #985

### Sprint: UI Uniformity - Shared Utilities (#980)

Parent Epic: #959 - UI Uniformity & Shared Infrastructure

#### Work Items
- [x] #967 - [Radoub.UI] Move VersionHelper to shared library
- [x] #968 - [Radoub.UI] Create shared StatusBar component
- [x] #969 - [Radoub.UI] Standardize progress indicators

#### Added
- `Radoub.UI.Utils.VersionHelper` - Shared version helper for all tools
  - `GetVersion()` - Returns semantic version from assembly metadata
  - `GetEntryAssemblyVersion()` - Returns version for main application
- `Radoub.UI.Controls.StatusBarControl` - Optional shared status bar component
  - `PrimaryText` - Main status text (left)
  - `SecondaryText` - Additional info (optional)
  - `TertiaryText` - Right-side info (optional)
  - `FilePath` - File path with auto-trimming (optional)
  - `ShowProgress` - Indeterminate progress indicator

#### Removed
- `Parley/Utils/VersionHelper.cs` - Replaced by shared implementation

#### Changed
- Parley, Manifest, Quartermaster, Fence, Trebuchet now use shared VersionHelper
- Removed duplicate `GetVersionString()` methods from all tools

#### Documentation
- Added Progress Indicator Standards to CLAUDE.md (duration-based feedback guidelines)

---

## [0.9.37] - 2026-01-18
**Branch**: `radoub/issue-979` | **PR**: #984

### Sprint: UI Uniformity - Critical Fixes (#979)

Parent Epic: #959 - UI Uniformity & Shared Infrastructure

#### Work Items
- [x] #960 - [Parley] Migrate to shared AboutWindow
- [x] #961 - [Quartermaster] Migrate to shared AboutWindow
- [x] #962 - [Fence] Migrate to shared AboutWindow
- [x] #963 - [Manifest] Migrate to shared AboutWindow
- [x] #964 - [Parley] Convert modal dialogs to non-modal
- [x] #965 - [Quartermaster] Convert DialogHelper to non-modal pattern
- [x] #966 - [Radoub.UI] Create centralized BrushManager for theme colors

---

## [0.9.36] - 2026-01-18
**Branch**: `radoub/issue-954` | **PR**: #955

### Feature: Variable Setting Service for GFF Files (#954)

Shared service for reading/writing local variables on GFF-based files.

#### Added (Radoub.Formats)
- `Variable` model class with `VariableType` enum (int, float, string, object, location)
- `VariableLocation` model for location-type variables
- `VarTableHelper` static class for VarTable read/write operations
  - `ReadVarTable()`, `WriteVarTable()` - bulk operations
  - `GetInt()`, `GetFloat()`, `GetString()`, `GetObjectId()`, `GetLocation()` - typed getters
  - `SetInt()`, `SetFloat()`, `SetString()`, `SetObjectId()`, `SetLocation()` - typed setters
  - `DeleteVariable()`, `HasVariable()`, `GetVariableNames()`, `ClearVariables()` - utilities
- 48 unit tests for VarTableHelper

#### Fixed (Radoub.Formats)
- GffWriter now correctly handles nested Struct fields
  - Struct index stored in DataOrDataOffset (not written to FieldData)
  - Required for location-type variables to round-trip correctly

#### Changed (Radoub.Formats)
- UtmFile now has `VarTable` property for local variables
- UtmReader parses VarTable from UTM files
- UtmWriter serializes VarTable to UTM files
- 2 new UTM tests for VarTable parsing/round-trip

#### Added (Fence - #945)
- Local Variables panel in MainWindow (collapsible Expander)
- `VariableViewModel` for UI binding with type-specific value editing
- `MainWindow.Variables.cs` partial class for variable operations
- Add/Remove variable buttons with DataGrid editing
- Variables load/save with UTM files

---

## [0.9.35] - 2026-01-17
**Branch**: `radoub/issue-907` | **PR**: #939

### Epic 907: Trebuchet (Radoub Launcher)

Central hub for the Radoub toolset providing tool launching, global settings management, and module info editing.

**MVP Scope**:
- Tool launcher with discovery
- Global settings manager (game paths, TLK, themes, fonts)
- Module info (IFO) editor

---

## [0.9.34] - 2026-01-17

### Documentation Reorganization

- Move BioWare Aurora format Markdown conversions to Radoub Wiki
- Remove `Documentation/BioWare_Markdown_Docs/` folder (59 files)
- Original BioWare PDFs remain in `Documentation/BioWare_Original_PDFs/`
- Update CLAUDE.md to reference Wiki for Markdown format specs

---

## [0.9.33] - 2026-01-16
**Branch**: `radoub/issue-920` | **PR**: #921

### Refactor: Consolidate Duplicate Services (#920)

- Remove duplicate `AudioService` from Parley (use shared Radoub.UI version)
- Remove inline `MockGameDataService` from UI tests (use shared Radoub.TestUtilities version)

---

## [0.9.32] - 2026-01-16
**Branch**: `parley/issue-916` | **PR**: #918

### Added

- `/cache` skill for GitHub data cache management
- GraphQL-based cache refresh script (`.claude/scripts/Refresh-GitHubCache.ps1`)
- Cache data extraction script with views (`.claude/scripts/Get-CacheData.ps1`)
- `--refresh` option to `/backlog`, `/sprint-planning`, `/grooming` skills
- SSF file parser (`SsfReader`, `SsfFile`, `SsfEntry`) to Radoub.Formats
- Soundset access methods to `IGameDataService` (`GetSoundset`, `GetSoundsetByResRef`)
- Shared `AudioService` in Radoub.UI for cross-tool audio playback

### Changed

- `/backlog`, `/sprint-planning`, `/grooming` now read from local cache instead of making multiple API calls
- Cache auto-refreshes every 4 hours; manual refresh available via `/cache refresh`
- Skills use compact list view (~25KB) instead of full cache (~220KB) to reduce context size

### Enhancement: Release workflow with curated highlights (#893)

Updated `/release` command and all release workflows to support curated release highlights.

**Workflow changes:**
- All workflows check for `release-notes.md` first (curated by `/release` command)
- Falls back to auto-extracting from CHANGELOG if no curated notes exist
- Auto-extraction parses latest versioned section, extracts headers and bullet points

**`/release` command changes:**
- Now presents numbered list of changelog items for the version being released
- User selects which items are "highlights" to feature prominently
- Remaining items summarized as "Other Changes" with link to full changelog
- Generates `release-notes.md` file consumed by workflow

---

## [0.9.31] - 2026-01-15
**Branch**: `radoub/issue-558` | **PR**: #910

### Sprint: Merchant Editor MVP (#558)

Part of Epic #555 (Merchant Editor Tool).

Assemble components into working Merchant Editor. Reuses shared inventory components from Creature Editor.

#### Work Items
- [ ] Store Properties panel (Name, Tag, pricing, stolen goods, identify)
- [ ] Five inventory tabs with shared ItemListView
- [ ] Merchant-specific features (Infinite checkbox, calculated prices)
- [ ] Restrictions editor (WillOnlyBuy/WillNotBuy)
- [ ] Store templates (save/load)
- [ ] File operations (Open/Save UTM, dirty state tracking)

---

## [0.9.30] - 2026-01-15
**Branch**: `radoub/issue-908` | **PR**: #909

### Sprint: Backlog Cleanup (#908)

Quick wins to clean up small bugs and tech debt across multiple tools.

#### Work Items
- [x] #662 - Docs: Update Radoub.sln documentation (tech-debt)
- [x] #597 - Bug: Recent files menu doesn't refresh in realtime (Parley)
- [x] #802 - Bug: Dialog file field appears in both panels (Quartermaster)
- [x] #807 - Status bar file path truncation without ellipsis (Manifest)
- [x] #819 - Disabled Undo/Redo menu items lack explanation (Quartermaster)

---

## [0.9.29] - 2026-01-12
**Branch**: `radoub/issue-886` | **PR**: #894

### Sprint: Testing Foundation (#886, #892)

Build testing infrastructure and address high-priority test gaps. Includes token system test coverage.

#### Work Items
- [x] #855 - Test: Create shared test utilities project (foundation)
- [x] #853 - Test: Add tool-level file round-trip tests
- [x] #849 - Test: Add CreatureDisplayService unit tests
- [x] #847 - Test: Add FlowView ↔ TreeView synchronization tests
- [x] #892 - Add missing token system tests (UserColorConfigLoader, edge cases)

---

## [0.9.28] - 2026-01-10
**Branch**: `radoub/issue-860` | **PR**: #861

### Sprint: Accessibility & Cross-Platform (#860)

Cross-platform polish: responsive breakpoints, consistent emoji rendering, and colorblind-friendly status indicators.

#### Work Items
- [x] #822 - Responsive breakpoint handling for small screens
- [x] #821 - Consistent emoji rendering across platforms
- [x] #824 - Icons/text for colorblind users on status indicators

#### Added
- **StatusIndicatorHelper** in Radoub.UI - Unicode icons (checkmark, X, warning, info) for colorblind-accessible validation messages
- **IconConstants** in Radoub.UI - Cross-platform Unicode icon characters for toolbar buttons
- All SettingsWindow path validations now show icons alongside color (WCAG 2.1 1.4.1 compliance)

#### Changed
- **Responsive layouts** for 1024x768 support:
  - Parley: Main panel uses proportional widths (2*/1*) instead of fixed 800px; speaker row uses WrapPanel
  - Manifest: Category/Entry properties use WrapPanel and flexible widths
  - Quartermaster: SpellsPanel class radios and filters use WrapPanel; reduced minimum widths for panels
- **Replaced all emoji icons with Unicode symbols** for consistent cross-platform rendering:
  - Toolbar buttons: Removed emoji, using text or simple symbols (+, ✖, ▼, ▶)
  - Warning icons: ⚠️ → ⚠
  - Quartermaster nav: Emoji → Unicode symbols (☺, ○, ⚔, ≡, ★, ◎, ✦, ■, ⚙, ☰)

---

## [0.9.27] - 2026-01-10
**Branch**: `radoub/issue-722` | **PR**: #844

### Feat: Minimal Test Module for Cross-Platform Integration Testing (#722)

Create self-contained test environment enabling full integration testing without a real NWN installation.

#### Added (Radoub.IntegrationTests)
- **TestData folder structure** with spoofed NWN environment:
  - `TestData/GameRoot/hak/test1.hak` - Test HAK with valid WAV sounds
  - `TestData/TestModule/` - Unpacked module with dlg, utc, scripts, etc.
- **TestPaths extensions** for test data access:
  - `TestDataRoot`, `TestGameRoot`, `TestHakDirectory`, `TestModuleDirectory`
  - `GetTestHakFile()`, `GetTestModuleFile()` helpers
- **FlaUITestBase** pre-seeds RadoubSettings with test data paths
- **ParleySettings** pre-seeds `SoundBrowserIncludeHakFiles: true` for HAK testing

#### Changed (Radoub.IntegrationTests)
- **Sound Browser test enabled** - previously skipped due to missing game resources (#701)
- Sound Browser test updated to use AutomationId-based window detection (matches Creature Picker pattern)
- **Creature tag browsing test added** - validates Creature Picker loads UTCs from TestModule

#### Test Data Contents
| Path | Contents |
|------|----------|
| `TestData/GameRoot/hak/` | `test1.hak` (test1.wav + failtest.wav) |
| `TestData/TestModule/` | eay.dlg, bandit002.utc, strength.nss, module.jrl, etc. |

---

## [0.9.26] - 2026-01-10
**Branch**: `radoub/issue-798` | **PR**: #838

### Feat: SafeMode Infrastructure (#798)

Implement proper SafeMode across all tools - reset fonts/themes to defaults and clear caches on startup (not a full preferences reset).

#### Shared
- [x] Added SafeModeService to Radoub.UI for common SafeMode logic

#### Parley
- [x] Refactored SafeMode from "rename prefs folder" to true safe mode
- [x] Auto-resets: theme to Light, fonts to system defaults, FlowView disabled
- [x] Auto-clears: parameter cache, plugin data
- [x] Shows SafeMode dialog with optional scrap cleanup
- [x] Removed abandoned flowchart-view plugin directory

#### Manifest
- [x] Added `--safemode` / `-s` command line argument
- [x] Auto-resets: theme to Light, fonts to system defaults
- [x] Settings persisted so SafeMode changes are saved

#### Quartermaster
- [x] Added `--safemode` / `-s` command line argument
- [x] Auto-resets: theme to Light, fonts to system defaults
- [x] Settings persisted so SafeMode changes are saved

---

## [0.9.25] - 2026-01-07
**Branch**: `radoub/issue-796` | **PR**: #797

### Sprint: Bug Fixes & Theme Cleanup

Cross-tool bug fixes for logging privacy and theme standardization.

- [x] #767 - UnifiedLogger missing path sanitization (bug)
  - Replaced Debug.WriteLine with UnifiedLogger.Log in GameDataService and GameResourceResolver
  - All path logging now uses automatic sanitization via PrivacyHelper
- [x] #787 - Standardize validation colors across tools
  - Added GetErrorBrush/GetSuccessBrush/GetWarningBrush helpers to Manifest and Quartermaster
  - Replaced hardcoded Brushes.Red/Green/Orange with theme-aware helpers
  - Ensures colorblind-accessible validation feedback
- [x] #784 - Refactor hardcoded UI settings to use theme system
  - FeatsPanel: status colors (assigned/granted/prereqs/available/unavailable) now theme-aware
  - SkillsPanel: class skill indicators and row highlights now theme-aware
  - SpellsPanel: spell slot table colors and status indicators now theme-aware
  - All dialogs (SettingsWindow, ColorPicker, FactionPicker, PackagePicker) now use theme resources
  - Added semantic color helpers: GetDisabledBrush, GetSuccessBrush, GetWarningBrush, GetInfoBrush, GetSelectionBrush
  - Fixed fallback colors to use light theme defaults (#D32F2F, #F57C00, #388E3C, #1976D2, #FFC107)
  - All hardcoded CornflowerBlue replaced with ThemeInfo (fixes tritanopia colorblind theme)
  - FontSize hardcoding remains - will address relative sizing in future sprint

### Issues Created During Sprint

- [#798](https://github.com/LordOfMyatar/Radoub/issues/798) - Add SafeMode startup for Manifest and Quartermaster
- [#799](https://github.com/LordOfMyatar/Radoub/issues/799) - Font size hardcoding (FontSize="10/11" throughout panels)

---

## [0.9.24] - 2026-01-05
**Branch**: `radoub/issue-762` | **PR**: #785

### Sprint: Bug Fixes & Polish

Cross-tool bug fixes for quality-of-life improvements.

- [x] #754 - Soundset TLK lookup (Quartermaster)
- [x] #589 - Item type to icon mapping (Radoub.UI)
- [x] #703 - Parameter list scrolling discoverability (Parley)
- [x] Collapsible script previews with themed Expanders (Parley + Radoub.UI)

---

## [0.9.23] - 2026-01-05
**Branch**: `radoub/issue-720` | **PR**: #783

### Unified Theme Infrastructure

- Add 7 themes to Quartermaster matching Parley/Manifest theme set (#720)
  - Light, Dark, Fluent Light, VS Code Dark
  - Accessibility: Deuteranopia, Protanopia, Tritanopia
- Update csproj to properly copy themes to output Themes\ subdirectory
- Add cross-tool Sea-Sick easter egg theme (replaces Angry Fruit Salad)
  - Unlocks when all 3 tools (Parley, Manifest, Quartermaster) have been launched
  - New EasterEggService in Radoub.UI tracks tool launches via `~/Radoub/.easter-eggs.json`

---

## [0.9.22] - 2026-01-04
**Branch**: `quartermaster/issue-760` | **PR**: #765

### Slash Commands & Test Infrastructure

#### run-tests.ps1 Improvements
- Add `-Tool [Parley|Quartermaster|Manifest]` parameter for targeted testing
- Add `-SkipShared` flag to skip Radoub.* library tests
- Add `-TechDebt` flag for large file scan (>500 lines in changed files)
- Exit with error code 1 when tests fail (for CI integration)
- Keep legacy `-ParleyOnly`/`-QuartermasterOnly` for backwards compatibility

#### /pre-merge Streamlined
- Remove upfront questions (didn't work with permission model)
- Batch `gh` commands with `&&` chaining (fewer approvals)
- Auto-detect tool from changed files
- Default to unit tests only (faster feedback)
- Delegate privacy + tech debt scans to test script

#### /post-merge Simplified
- Remove duplicate CHANGELOG/wiki validation (done in pre-merge)
- Focus on cleanup tasks: branch deletion, issue closing, epic update, release
- Reduce from 5 upfront questions to 2

#### /backlog Added
- Combined grooming + sprint planning (replaces separate commands)
- Lighter hygiene check (counts only, not per-issue details)
- Sprint options with complexity estimates

---

## [0.9.21] - 2026-01-01
**Branch**: `radoub/sprint/test-fixes` | **PR**: #721

### Sprint: Test Fixes

UI test stabilization and reliability improvements.

#### Focus-Stealing Fix
- Strengthen startup focus with double-tap SetForeground + Focus pattern
- Add `SendTab()` and `SendShiftTab()` focus-safe keyboard helpers
- Fix NavigationTests to use focus-safe Tab key helpers instead of direct Keyboard.Press calls

#### MultipleUndo Test (#705)
- Fix test to use normalized baseline after round-trip save
- Increase undo attempts and tolerance for serialization differences
- Remove inter-operation clicks that created extra undo states

#### Script Browser Timing (#704)
- Add EnsureFocused before button clicks
- Increase wait time for Script Browser window to open

#### Creature Picker Window Detection (#700)
- Add desktop search by AutomationId as fallback for popup detection
- Fix window detection when Avalonia popup isn't in GetAllTopLevelWindows
- Enable test (was skipped) - uses existing parleypirate.utc test file

#### Sound Browser Test (#701)
- Updated skip message: requires game sound resources (BIF/HAK files)
- Test remains skipped - sound files too large for test data

---

## [0.9.20] - 2026-01-01
**Branch**: `radoub/sprint/refactor-cleanup` | **PR**: #709

### Sprint: Refactor Cleanup

Cross-tool refactoring for consistency and maintainability.

#### Manifest: Rename PreferencesWindow to SettingsWindow (#542)
- Align window naming with Parley for cross-tool consistency
- See [Manifest CHANGELOG](Manifest/CHANGELOG.md) for details

#### Radoub.UI: Split ScriptBrowserWindow.axaml.cs (#683)
- Extract `HakScriptScanner` service - HAK scanning with caching (285 lines)
- Extract `ScriptListManager` - script list filtering and merging (117 lines)
- Extract `ScriptPreviewLoader` - script content loading for preview (161 lines)
- Reduce ScriptBrowserWindow.axaml.cs from 816 to 461 lines (44% reduction)

---

## [0.9.19] - 2025-12-31
**Branch**: `radoub/sprint/ui-testing-improvements` | **PR**: #699

### Sprint: UI Testing Improvements

Cross-tool testing improvements for FlaUI-based UI testing.

#### TestSteps Helper (#693)
- Add `TestSteps` class for consolidated test diagnostics
- Continue after first failure, stop on second (1 = specific issue, 2 = dumpster fire)
- Detailed step-by-step failure output with investigation hints
- Consolidate SpellsPanelTests: 10 tests → 2 consolidated tests
- Consolidate SmokeTests across all 3 tools

#### Parley Navigation Tests (#624)
- Add AutomationIds to TabControls and TabItems for FlaUI testability
- Create `NavigationTests.cs` with consolidated step-based tests
  - PropertiesTabs_NavigationWorks: Scripts, Node, Scrap tab switching
  - LeftPane_DialogTreeTabWorks: Tree elements present
  - BrowseButtons_ExistAndAccessible: All browse buttons findable
  - TabKeyNavigation_MovesForward: Focus movement
- Consolidate TreeEditingTests using TestSteps pattern

#### Browser Window Tests (#441)
- **Unblock #441**: FlaUI can now navigate Avalonia TabControls
- Add AutomationIds to SoundBrowserWindow, ScriptBrowserWindow, CreaturePickerWindow
- Add BrowseSoundButton AutomationId to MainWindow
- Create `BrowserWindowTests.cs` with Script Browser test (working)
- Sound/Creature browser tests skipped pending investigation (#700, #701)

---

## [0.9.18.3] - 2025-12-30
**Branch**: `radoub/fix/skiasharp-version-mismatch` | **PR**: #660 | **Issue**: #661

### Fix: SkiaSharp Version Mismatch

- Fix runtime crashes on Linux: "libSkiaSharp library (88.1) is incompatible"
- Add explicit `Avalonia.Skia 11.3.10` reference to all three tools
- Add explicit `SkiaSharp.NativeAssets.Linux 3.116.1` reference to all three tools
- Create root-level `Radoub.sln` for easier builds
  - Includes all projects except Windows-only integration tests
  - Build all tools with: `dotnet build Radoub.sln`

---

## [0.9.18.2] - 2025-12-30
**Branch**: `radoub/fix/release-version-info` | **PR**: #659

### Fix: Release Version Display

- Fix bundle release showing wrong version in title bar
  - Version showed "v18.1.0" instead of correct "v0.9.18.1"
  - Build showed "dev" instead of actual commit hash
- Add `-p:InformationalVersion` to publish commands to override GitInfo
- Update VersionHelper to extract commit from InformationalVersion suffix
- Format: `version+commit` (e.g., `0.9.18.2+abc1234`)

---

## [0.9.18.1] - 2025-12-30
**Branch**: `radoub/fix/release-workflow-pipe` | **PR**: #658

### Fix: macOS Release Workflow

- Fix broken pipe error on macOS in release workflow
- `ls -la | head -30` returns exit code 1 when pipe closes early
- Added `|| true` to ignore harmless pipe errors

---

## [0.9.18] - 2025-12-30
**Branch**: `radoub/sprint/shared-infra-optimization` | **PR**: #657

### Sprint: Shared Infrastructure Optimization (#627, #543)

- Consolidate ThemeManager to shared Radoub.UI library (#627)
- Optimize bundle workflow to share .NET runtime (#543)
  - Both tools now publish to single `Radoub/` folder
  - Eliminates duplicate DLLs (~33% bundle size reduction)
  - Simplified workflow: single build job instead of separate Parley/Manifest jobs

---

## [0.9.17] - 2025-12-30
**Branch**: `radoub/feat/flaui-focus-verification` | **PR**: #655

### Test: Verify Window Focus in All FlaUI Tests (#623)

Add focus verification to FlaUI test base to prevent tests from interacting with wrong windows.

#### Added (FlaUITestBase)
- `EnsureFocused()` - Verify and restore window focus before keyboard input
- `SendKeyboardShortcut()` - Focus-safe keyboard shortcut helper
- Common shortcuts: `SendCtrlS()`, `SendCtrlZ()`, `SendCtrlY()`, `SendCtrlD()`, etc.

#### Changed (Integration Tests)
- All Parley keyboard shortcuts now use focus-safe helpers
- All Manifest keyboard shortcuts now use focus-safe helpers
- Deprecated direct `Keyboard.TypeSimultaneously()` calls

#### Changed (CLAUDE.md)
- Added "FlaUI Window Focus (CRITICAL)" section with mandatory focus patterns

---

## [0.9.16] - 2025-12-27
**Branch**: `radoub/docs/ui-wiki-page` | **PR**: #607

### Docs: Wiki Updates and CLAUDE.md Optimization (#575)

#### Added (Wiki)
- Radoub-UI-Developer.md - Shared UI components documentation
- Quartermaster-Developer-Architecture.md - CreatureEditor architecture
- Radoub-Formats.md updates (ItemPropertyResolver, UnifiedLogger sections)
- Updated _Sidebar.md and Index.md with new pages

#### Changed (CLAUDE.md Files)
- **Parley/CLAUDE.md** - Major rewrite (543→174 lines, 68% reduction)
  - Removed duplicated standards (now in Radoub CLAUDE.md)
  - Kept: MainViewModel closed rule, orphan handling, Aurora compatibility
- **Manifest/CLAUDE.md** - Cleanup (146→118 lines)
  - Removed stale sprint plan and duplicated path handling
- **CLAUDE.md** (Radoub) - Added sections:
  - Manifest in Current Tools
  - Code Quality Standards (path handling, exceptions, hygiene)
  - Wiki local path in Resources

#### Changed (Slash Commands)
- **pre-merge.md** - Wiki enforcement now BLOCKING (not advisory)
- **post-merge.md** - Simplified branch cleanup (remote auto-prune)
- **sprint-planning.md** - Saves output to NonPublic/sprintplanning.md
- **grooming.md** - Saves output to NonPublic/grooming.md

---

## [Unreleased - IntegrationTests]

### Changed (Radoub.IntegrationTests)
- Test scripts support `-ParleyOnly` / `--parley-only` flag to run only Parley + shared tests
- Test scripts support `-QuartermasterOnly` / `--quartermaster-only` flag to run only Quartermaster + shared tests
- UI integration tests now filtered by namespace for tool-specific runs

---

## [0.9.15] - 2025-12-27
**Branch**: `radoub/feat/unified-logger-extraction` | **PR**: #595

### Feature: Extract UnifiedLogger to Shared Library (#591)

Consolidates logging infrastructure across all Radoub tools.

#### Added (Radoub.Formats)
- **UnifiedLogger** - Shared logging service with session-based organization
- **LogLevel enum** - ERROR, WARN, INFO, DEBUG, TRACE levels
- **PrivacyHelper** - Path sanitization to prevent logging user paths
- **LoggerConfig** - Configuration for app name, log level, session retention

#### Changed (Parley)
- Migrated to shared UnifiedLogger from Radoub.Formats
- Removed duplicate `Parley/Parley/Services/UnifiedLogger.cs`

#### Changed (Quartermaster)
- Migrated to shared UnifiedLogger from Radoub.Formats
- Removed duplicate `CreatureEditor/CreatureEditor/Services/UnifiedLogger.cs`

#### Changed (Manifest)
- Migrated to shared UnifiedLogger from Radoub.Formats
- Removed duplicate `Manifest/Manifest/Services/UnifiedLogger.cs`

---

## [0.9.14] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-cleanup` | **PR**: #584

### Sprint: CreatureEditor Cleanup (#582, #583)

Part of Epic #544 (Creature Editor Tool).

Pre-emptive refactoring before CreatureEditor grows. Lessons from Parley's 2,400+ line MainWindow.

#### Refactoring (#582)
- **MainWindow.FileOps.cs** - Extracted file operations (Open/Save/Recent files, 221 lines)
- **MainWindow.Inventory.cs** - Extracted inventory population and item resolution (130 lines)
- **DialogHelper.cs** - Static helper for common dialogs (Unsaved/Error/About)
- MainWindow.axaml.cs reduced from 892 to 466 lines (48% reduction)

#### Testing (#583)
- **CreatureEditor.Tests project** - New xUnit test project
- **CommandLineServiceTests** - 9 tests for argument parsing
- **SettingsServiceTests** - 12 tests for property constraints and defaults
- **Quartermaster FlaUI tests** - 5 smoke tests in Radoub.IntegrationTests

#### Changed (Radoub.IntegrationTests)
- TestPaths.cs updated with CreatureEditor paths
- FlaUITestBase.cs supports CreatureEditor settings isolation
- run-tests.ps1 includes CreatureEditor.Tests in test suite

---

## [0.9.13] - 2025-12-26
**Branch**: `radoub/feat/uti-bif-loading` | **PR**: #581

### Feature: Load UTI Items from BIF Archives (#579)

Part of Epic #544 (Creature Editor Tool).

#### Added (CreatureEditor)
- **GameDataService integration** - Enables BIF/Override/TLK lookups for base game items
- **UTI loading from BIF archives** - Base game items (e.g., `nw_it_torch001`) now load full data
- **ItemViewModelFactory usage** - Proper name resolution via baseitems.2da and TLK strings
- **Resource resolution order**: Module directory → Override → HAK → BIF archives

#### Changed (CreatureEditor)
- `CreatePlaceholderItem` now uses GameDataService for BIF lookups when module file not found
- Item display names resolved via TLK instead of showing ResRef placeholders
- Base item types resolved via baseitems.2da lookups

---

## [0.9.12] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-mvp` | **PR**: #578

### Sprint: Creature Editor MVP - Inventory Panel (#554)

Part of Epic #544 (Creature Editor Tool).

**Status**: Partial - UI framework complete, BIF support needed for full functionality.

#### Added (CreatureEditor)
- **Project scaffold** - Avalonia UI application with theming and logging
- **MainWindow** - 3-panel layout with Equipment, Backpack, and Palette sections
- **File operations** - Open/Save/Recent Files for UTC and BIC files
- **Inventory display** - DataGrid shows backpack items from creature
- **UTI loading** - Loads item data from module directory

#### Added (Radoub.UI)
- DataGrid styles integration (StyleInclude required for Avalonia DataGrid)

#### Fixed (Radoub.Formats)
- GffReader Struct field parsing - DataOrDataOffset is direct struct index, not offset into FieldData
- Fixes BIC file parsing errors (e.g., MiscVisuals field)

#### Known Limitations
- Base game items (nw_*) show placeholder data - requires BIF support (#579)
- Equipment slots not visually populated (#580)
- Item palette not populated (#580)

---

## [0.9.11] - 2025-12-26
**Branch**: `radoub/sprint/equipment-slots-panel` | **PR**: #576

### Sprint: Equipment Slots Panel Component (#553)

Part of Epic #546 (Shared Inventory UI Components).

#### Added (Radoub.UI)
- **EquipmentSlotsPanel** - Visual equipment slot display for creatures
  - Standard equipment slots (14 slots: Head, Chest, Boots, etc.)
  - Natural equipment slots (4 creature-only slots: Claws, Skin)
  - Grid layout with labeled slots and item icons
  - Validation warnings for invalid equipment
  - Drag-drop support to/from backpack

---

## [0.9.10] - 2025-12-26
**Branch**: `radoub/sprint/item-filter-panel` | **PR**: #573

### Sprint: Item Filter Panel Component (#552)

Part of Epic #546 (Shared Inventory UI Components).

#### Added (Radoub.UI)
- **ItemFilterPanel** - Collapsible filter control for item lists
  - Text search (Name, Tag, ResRef) with debounce
  - Source filter (Standard/Custom items)
  - Type filter (from baseitems.2da categories)
  - Filter state persistence

---

## [0.9.9] - 2025-12-26
**Branch**: `radoub/sprint/item-list-view` | **PR**: #574

### Sprint: Inventory List View Component (#551)

Part of Epic #546 (Shared Inventory UI Components).

#### Added (Radoub.UI)
- **Radoub.UI** - New shared UI component library for Radoub tools
- **ItemListView** - DataGrid-based control for item display
  - Multi-column display (Name, ResRef, Tag, Type, Value, Properties)
  - Built-in sorting via column headers
  - Multi-select with checkboxes (Select All / Select None)
  - Row selection with Extended mode (Ctrl+click, Shift+click)
  - Column width persistence via IColumnSettings interface
  - Row context menu (Open, Edit, Copy ResRef, Copy Tag)
  - Drag source support for drag-drop operations
  - Theme-aware styling with DynamicResource bindings
- **ItemViewModel** - ViewModel for DataGrid item binding
  - Wraps UtiFile with display-friendly properties
  - Observable IsSelected property for checkbox binding
- **ItemViewModelFactory** - Factory for creating ItemViewModels
  - Resolves display names from LocalizedName or TLK
  - Resolves base item type from baseitems.2da
  - Formats item properties from 2DA chain
- **IColumnSettings** - Interface for column width persistence
- 17 unit tests (ItemViewModel, ItemViewModelFactory)

---

## [0.9.8] - 2025-12-26
**Branch**: `radoub/sprint/game-data-service` | **PR**: #572

### Sprint: Game Data Service Foundation (#550)

Part of Epic #545 (Shared Game Data Infrastructure).

#### Added (Radoub.Formats)
- **IGameDataService** interface - Centralized game data access API
  - `Get2DA(name)` - Load and cache 2DA files
  - `Get2DAValue()` - Convenience method for single value lookup
  - `GetString(strRef)` - TLK string resolution with custom TLK support
  - `FindResource()` / `ListResources()` - Resource access via resolver
- **GameDataService** - Implementation with 2DA caching
  - Negative caching for missing resources (avoids repeated lookups)
  - Settings-based configuration via RadoubSettings
  - Cache invalidation via `ClearCache()` and `ReloadConfiguration()`
- 23 new unit tests for GameDataService

---

## [0.9.7] - 2025-12-26
**Branch**: `radoub/sprint/item-property-resolver` | **PR**: #568

### Sprint: Item Property Resolver (#567)

Part of Epic #547 (File Format Parsers).

#### Added (Radoub.Formats)
- **ItemPropertyResolver** - Resolve item property names from 2DA chain
- Parse itempropdef.2da for property definitions
- Subtype 2DA resolution (iprp_*.2da)
- Cost table and param table resolution
- Formatted property strings (e.g., "Enhancement Bonus +3")

---

## [0.9.6] - 2025-12-26
**Branch**: `radoub/sprint/bic-parser-gff-builder` | **PR**: #566

### Sprint: BIC Parser and GffFieldBuilder Refactor (#565)

Part of Epic #547 (File Format Parsers).

#### Added (Radoub.Formats)
- **BIC Parser** - Read/write support for player character files
- `BicFile` extends `UtcFile` with player-specific fields
- `BicReader` / `BicWriter` - Static parser/serializer pair
- Player fields: Experience, Gold, Age, QBList, ReputationList

#### Changed (Radoub.Formats)
- **GffFieldBuilder** - Extracted shared helper methods from format writers (#563)
  - Common field creation: `AddByteField`, `AddIntField`, `AddFloatField`, etc.
  - Reduces duplication across `UtiWriter`, `UtcWriter`, `UtmWriter`, `BicWriter`

---

## [0.9.5] - 2025-12-26
**Branch**: `radoub/sprint/utm-parser-store-model` | **PR**: #562

### Sprint: UTM Parser and Store Model (#556)

Part of Epic #547 (File Format Parsers).

#### Added (Radoub.Formats)
- **UTM Parser** - Read/write support for store/merchant blueprint files
- `UtmFile` - Strongly-typed store representation
- `UtmReader` / `UtmWriter` - Static parser/serializer pair
- `StorePanel`, `StoreItem` - Panel and item models
- `StorePanels` - Panel ID constants with name lookup
- 17 unit tests (reader + round-trip)

#### MVP Fields
- Identity: ResRef, Tag, LocName
- Pricing: MarkUp, MarkDown, StoreGold, MaxBuyPrice, IdentifyPrice
- Black market: BlackMarket flag, BM_MarkDown
- StoreList: 5 inventory panels with item references
- WillOnlyBuy/WillNotBuy: Base item type filters
- Scripts: OnOpenStore, OnStoreClosed

---

## [0.9.4] - 2025-12-25
**Branch**: `radoub/sprint/utc-parser-creature-model` | **PR**: #561

### Sprint: UTC Parser and Creature Model (#549)

Part of Epic #547 (File Format Parsers).

#### Added (Radoub.Formats)
- **UTC Parser** - Read/write support for creature blueprint files
- `UtcFile` - Strongly-typed creature representation
- `UtcReader` / `UtcWriter` - Static parser/serializer pair
- `CreatureClass`, `SpecialAbility` - Class and ability models
- `EquippedItem` - Equipment slot mapping (14 slots)
- `InventoryItem` - Backpack item references
- `EquipmentSlots` - Slot constants with name lookup
- 28 unit tests (reader + round-trip)

#### Added (CLAUDE.md)
- Sprint Workflow section - commit between sprint items

#### MVP Fields
- Identity: FirstName, LastName, Tag
- Basic info: Race, Gender, ClassList
- Inventory: ItemList, Equip_ItemList with parsed UTI data

---

## [0.9.3] - 2025-12-25
**Branch**: `radoub/sprint/uti-parser` | **PR**: #559

### Sprint: UTI Parser and Item Model (#548)

Part of Epic #547 (File Format Parsers).

#### Added (Radoub.Formats)
- **UTI Parser** - Read/write support for item blueprint files
- `UtiFile` - Strongly-typed item representation
- `ItemProperty` - Item property model with defaults
- Comprehensive unit tests (16 tests)

#### Changed (Radoub.Formats)
- **CExoLocString Consolidation** - Single localized string class for all GFF formats
  - Enhanced with `SetString()` method and renamed `GetDefaultString()` to `GetDefault()`
  - Removed duplicate `JrlLocString` and `UtiLocString` classes (~90 lines saved)
  - JrlReader/JrlWriter simplified to use CExoLocString directly

#### Changed (Parley, Manifest)
- Updated to use consolidated `CExoLocString` from Radoub.Formats.Gff

#### Fixed (Radoub.Formats)
- **GffReader**: Label format detection now prioritizes exact size matches
- **UtiWriter**: GffList.Count now correctly set when building lists

---

## [0.9.2] - 2025-12-25
**Branch**: `radoub/sprint/dictionary-language` | **PR**: #539

### Sprint: Dictionary Language Selection (#515)

#### Added (Radoub.Dictionary)
- **DictionaryDiscovery** - Scan for available Hunspell and custom dictionaries (#508)
  - Auto-detects bundled dictionaries (en_US, NWN)
  - Scans `~/Radoub/Dictionaries/` for user-installed languages
  - Hunspell dictionaries: `{lang_code}/{lang_code}.dic` + `.aff`
  - Custom JSON dictionaries: `*.dic` or `*.json` files
- **DictionarySettingsService** - Persist language preferences (#508)
  - Settings stored at `~/Radoub/Dictionaries/settings.json`
  - Events for hot-swap: `PrimaryLanguageChanged`, `CustomDictionaryToggled`
- **DictionaryInfo** model with metadata (name, type, path, bundled flag)

---

## [0.9.1] - 2025-12-23
**Branch**: `manifest/sprint/dictionary-integration` | **PR**: #511

### Test Infrastructure Improvements

#### Added
- `run-tests.ps1` - Unified test runner for all 5 test projects (Windows)
- `run-tests.sh` - Linux/macOS test runner (unit tests only - FlaUI is Windows-only)
- Test script parameters: `-UIOnly`, `-UnitOnly` for selective test execution
- Updated `post-merge.md` command with full test suite execution
  - Privacy scan before tests
  - "Hands off keyboard" warning for UI tests
  - Test results appended to PR description

#### Test Projects
| Project | Tests |
|---------|-------|
| Radoub.Formats.Tests | 165 |
| Radoub.Dictionary.Tests | 54 |
| Parley.Tests | 461 |
| Manifest.Tests | 32 |
| Radoub.IntegrationTests | 52 |
| **Total** | **764** |

---

## [0.9.0] - 2025-12-22
**Branch**: `radoub/sprint/dictionary-library` | **PR**: #507

### Sprint: Dictionary Library Creation (#504)

Build shared `Radoub.Dictionary` library for D&D/NWN spell-checking (Epic #43, Sprint 1 of 3).

#### Added
- `Radoub.Dictionary` project - Shared spell-check library
- `DictionaryManager` - Core dictionary operations (load, save, merge)
- `SpellChecker` - Hybrid spell-check engine combining Hunspell + custom dictionaries
- `TermExtractor` - Extract terms from .2da files and dialogs
- JSON-based custom dictionary format for D&D/NWN terminology
- Bundled en_US Hunspell dictionary (~550KB)
- Unit tests (54 tests)

#### Dependencies
- [WeCantSpell.Hunspell](https://github.com/aarondandy/WeCantSpell.Hunspell) v7.0.1 (MIT)
- [LibreOffice en_US dictionary](https://cgit.freedesktop.org/libreoffice/dictionaries/) (BSD/Public Domain)

---

## [0.8.4] - 2025-12-21
**Branch**: `radoub/chore/release-0.8.4` | **PR**: #500

### Bundle Release: Parley 0.1.84 + Manifest 0.8.0

Bundled release with significant Parley and Manifest updates since 0.8.3.

#### Parley Highlights (0.1.76 → 0.1.84)
- **Conversation Simulator** - Dialog walker with coverage tracking, TTS playback, warnings system
- **Linux TTS** - Piper neural voices + espeak-ng fallback with voice variants
- **Scrap System Redesign** - Settings migration, improved UX
- **TreeView UX** - Drag-drop, collapsible nodes, navigation improvements
- **Architecture** - Controller extraction (5 sprints of MainWindow cleanup)

#### Manifest Highlights (0.7.0 → 0.8.0)
- **CLI Arguments** - `--file`, `--quest`, `--entry` for cross-tool integration
- **UI Polish** - Improved journal entry layout

See tool-specific CHANGELOGs for full details:
- [Parley CHANGELOG](Parley/CHANGELOG.md)
- [Manifest CHANGELOG](Manifest/CHANGELOG.md)

---

## [0.8.3] - 2025-12-17
**Branch**: `radoub/feat/bundle-package` | **PR**: #449

### Feat: Bundle Parley and Manifest into Unified Package (#448)

Create combined release package with shared runtime and dependencies.

#### Added
- `radoub-release.yml` workflow for bundled releases (tag: `radoub-vX.Y.Z`)
- `/release radoub` option in release command
- Combined Parley + Manifest packages for Windows, macOS, Linux

#### Notes
- CEF (plugin WebView) remains separate/optional for plugin developers
- Individual tool releases remain available (`/release parley`, `/release manifest`)

---

## [0.8.2] - 2025-12-16
**Branch**: `radoub/fix/linux-hak-and-script-preview` | **PR**: #440

### Fix: Linux HAK Crash and Cross-Platform Compatibility

Fixes critical Linux issues discovered during cross-platform testing.

#### Fixed
- **ErfReader integer overflow**: Added overflow validation when casting `uint32` offsets to `int`
  - Prevents crashes when reading large ERF/HAK files (>2.1GB offsets)
  - Uses `long` arithmetic to detect overflow before casting
- **GameResourceResolver case-sensitive file lookup**: Fixed override folder lookup on Linux
  - `Directory.GetFiles` pattern matching is case-sensitive on Linux
  - Now uses `Directory.EnumerateFiles` with case-insensitive LINQ comparison
- **Bare exception handlers**: Replaced silent `catch` blocks with proper `catch (Exception ex)` and `Debug.WriteLine` logging

#### Changed
- CI workflows now run tests on both Windows and Linux (ubuntu-latest)
- Test artifacts named per-platform (`test-results-windows-latest`, `test-results-ubuntu-latest`)

---

## [0.8.1] - 2025-12-15
**Branch**: `radoub/docs/dev-docs-to-wiki` | **PR**: #433

### Docs: Move Developer Documentation to Wiki (#372, #424, #213)

Migrate developer documentation from repo to GitHub Wiki.

#### Added
- [Radoub.Formats wiki documentation](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats) (#213)
  - [GFF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-GFF)
  - [KEY Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-KEY)
  - [BIF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-BIF)
  - [ERF Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-ERF)
  - [TLK Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-TLK)
  - [2DA Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-2DA)
  - [JRL Format](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-JRL)
- [Manifest CODE_PATH_MAP](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture) (#424)
- [Script-Parameters](https://github.com/LordOfMyatar/Radoub/wiki/Script-Parameters) feature documentation

#### Changed
- Developer docs moved to wiki (verified against current code):
  - [Parley-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Architecture)
  - [Parley-Developer-Testing](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Testing)
  - [Parley-Developer-Delete-Behavior](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Delete-Behavior)
  - [Parley-Developer-CopyPaste](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-CopyPaste)
  - [Parley-Developer-Scrap-System](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Scrap-System)
  - [Manifest-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture)
- NonPublic folder moved to Radoub root with tool hierarchy
- Research folder moved to NonPublic/Research
- Parley README simplified (full docs in wiki)
- Pre-merge command updated with wiki pages and freshness dates

#### Removed
- `Parley/Documentation/Developer/` - Migrated to wiki
- `Documentation/Research/` - Moved to NonPublic

---

## [0.8.0] - 2025-12-14

### Added
- **Manifest** tool - JRL (journal) editor for Neverwinter Nights
  - See [Manifest CHANGELOG](Manifest/CHANGELOG.md) for tool-specific changes

---

## [0.7.0] - 2025-12-14
**Branch**: `radoub/sprint/jrl-reader` | **PR**: #401

### Sprint: Create JRL Reader/Writer (#397)

Create JRL (journal) reader and writer in Radoub.Formats using the shared GFF parser.

#### Added
- `Radoub.Formats.Jrl` namespace
- `JrlReader` - Static JRL parser wrapping GffReader
- `JrlWriter` - Static JRL writer for round-trip
- `JrlFile` - JRL data models (JournalCategory, JournalEntry)
- JRL round-trip tests

---

## [0.6.0] - 2025-12-14
**Branch**: `radoub/sprint/gff-parser` | **PR**: #399

### Sprint: Move GFF Parser to Radoub.Formats (#396)

Extract static GFF parsing code from Parley to shared library.

#### Added
- `Radoub.Formats.Gff` namespace
- `GffReader` - Static GFF binary parser (3 overloads: path, stream, buffer)
- `GffWriter` - Static GFF binary writer
- `GffFile` - GFF data models (GffStruct, GffField, GffLabel, GffList, CExoLocString)
- GFF round-trip tests

#### Research
- Epic379_SharedInfrastructure_Research.md (moved to NonPublic/Research/)

---


## [0.5.4] - 2025-12-13
**Branch**: `radoub/feat/github-projects-sync` | **PR**: #366

### Enhancement: GitHub Projects CLI Integration (#360)

Integrate GitHub Projects with Claude Code slash commands for automatic project board updates.

#### Added
- `/grooming` - Add groomed issues to appropriate project board
- `/sprint-planning` - Add sprint issues to project and mark in-progress
- `/init-item` - Add issue to project when branch initialized, mark in-progress
- `.claude/github-projects-reference.md` - Quick reference for project IDs and field details
- Documentation for GitHub CLI project scope (`gh auth refresh -s project`)

---

## [0.5.3] - 2025-12-13
**Branch**: `radoub/feat/sprint-issue-creation` | **PR**: #361

### Enhancement: Sprint Planning Issue Creation (#359)

Update `/sprint-planning` command to optionally create GitHub issues for planned sprints.

#### Added
- Sprint issue creation workflow after planning completes
- User confirmation before creating issues
- Automatic `sprint` and tool labels on created issues
- Parent epic linking in sprint issue body
- Work items checklist in sprint body
- Support for creating multiple sprint issues at once

---

## [0.5.2] - 2025-12-13
**Branch**: `radoub/feat/grooming-command` | **PR**: #358

### Feat: /grooming Slash Command (#357)

Added `/grooming` command to review and format open issues.

---

## [0.5.1] - 2025-12-08
**Branch**: `radoub/chore/dependabot-updates-309` | **PR**: #310

### Dependencies

Updated GitHub Actions and NuGet packages (closes #309):
- `gittools/actions` 3 → 4 (GitVersion workflow)
- `actions/checkout` 4 → 6
- `actions/download-artifact` 4 → 6
- `Google.Protobuf` 3.29.3 → 3.33.2 (gRPC plugin communication)

### Added
- Dependabot configuration for automated dependency updates (NuGet + GitHub Actions)

### Fixed
- GitVersion.yml updated to v6.x format (replaced deprecated `tag` with `label`, `is-mainline` with `is-main-branch`, updated `prevent-increment` syntax)
- Release workflow: Build project instead of solution to support RuntimeIdentifier for platform-specific packages
- macOS ARM64: Use RuntimeInformation for ARM64 WebView package selection

---

## [0.5.0] - 2025-11-28
**Branch**: `radoub/sprint/game-file-formats` | **PR**: #214

### Sprint: Game File Formats Integration

GameResourceResolver unified API for Aurora Engine resource resolution.

**Added**:
- `GameResourceConfig` - Configuration for game paths (NWN:EE and Classic factories)
- `GameResourceResolver` - Unified resource lookup across Override/HAK/BIF sources
- `ResourceResult` - Resource data with source tracking (Override, Hak, Bif)
- `ResourceInfo` - Lightweight resource metadata for listings
- TLK string resolution with custom TLK support (StrRef >= 0x1000000)
- NWN:EE and Classic path conventions via factory methods
- Archive caching for performance
- 21 unit tests for GameResourceResolver

---

## [0.4.0] - 2025-11-27
**Branch**: `radoub/feat/issue-170-formats-library-tlk` | **PR**: #209

### Epic #170: Aurora Game Resource Reading Infrastructure (Phase 3 - TLK & 2DA)

TLK (Talk Table) and 2DA (Two-Dimensional Array) file reading support.

**Added - TLK**:
- `TlkFile` - Data model for TLK (Talk Table) files
- `TlkEntry` - Entry model with text, sound ResRef, and duration
- `TlkReader` - Parser for TLK format files
- Flag support: HasText (0x1), HasSound (0x2), HasSoundLength (0x4)
- Legacy artifact stripping (0xC0 bytes from old editors)
- Windows-1252 encoding support for NWN text
- 23 unit tests for TLK reading

**Added - 2DA**:
- `TwoDAFile` - Data model for 2DA game data tables
- `TwoDARow` - Row model with label and cell values
- `TwoDAReader` - Parser for 2DA format (text-based)
- DEFAULT value support for missing cells
- Quoted string handling for values with spaces
- Empty cell (****) support
- Case-insensitive column lookup
- 26 unit tests for 2DA reading

---

## [0.3.0] - TBD
**Branch**: `radoub/feat/issue-90-gui-testing` | **PR**: #204

### Issue #90: Automated GUI Testing Infrastructure

FlaUI-based GUI testing framework for Radoub tools.

**Added**:
- `Radoub.IntegrationTests` project - Shared GUI test infrastructure
- FlaUI integration for Windows desktop testing (no external dependencies)
- Basic Parley launch smoke tests

---

## [0.2.0] - 2025-11-27
**Branch**: `radoub/feat/issue-170-erf-hak` | **PR**: #201

### Epic #170: Aurora Game Resource Reading Infrastructure (Phase 2)

ERF/HAK file reading support for Aurora Engine resource archives.

**Added**:
- `ErfFile` - Data model for ERF, HAK, MOD, SAV, NWM files
- `ErfReader` - Parser for ERF format files
- `ErfLocalizedString` - Localized description string support
- `ErfResourceEntry` - Resource metadata with offset/size tracking
- `ExtractResource()` - Extract individual resources from ERF archives
- 22 unit tests for ERF/HAK reading

---

### Documentation (PR #183)

Split large BioWare format documentation into chapter-based files for easier reading and reference.

**Creature Format** (`Documentation/BioWare_Markdown_Docs/Creature_Format/`):
- Ch1_Introduction.md
- Ch2_Creature_Struct.md
- Ch3_Calculations_and_Procedures.md
- Ch5_Creature_Related_2DA_Files.md
- README.md (index)

**Item Format** (`Documentation/BioWare_Markdown_Docs/Item_Format/`):
- Ch1_Introduction.md
- Ch2_Item_Struct.md
- Ch3_InventoryObject_Struct.md
- Ch4_Calculations_and_Procedures.md
- Ch5_Item_Related_2DA_Files.md
- README.md (index)

Original complete documents preserved.

### Sprint Planning Agent

Added `/sprint-plan` slash command (`.claude/commands/sprint-plan.md`) for automated sprint planning analysis.

---
