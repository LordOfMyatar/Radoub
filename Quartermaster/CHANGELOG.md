# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.56-alpha] - 2026-01-25
**Branch**: `quartermaster/issue-1055` | **PR**: #1129

### Feature: Migrate 3D Renderer to Silk.NET/OpenGL (#1055)

- [ ] Implement `OpenGlControlBase` subclass for GPU-accelerated rendering
- [ ] Create GLSL shaders for textured rendering
- [ ] Support PLT textures (pre-rendered to RGBA)
- [ ] Add proper depth testing
- [ ] Maintain existing rotation/zoom controls
- [ ] Cross-platform compatibility (Windows, Linux, macOS)
- [ ] Remove dead SkiaSharp renderer code (`ModelRenderOperation`, `RenderFacesInDepthOrder`, etc.)

---

## [0.1.55-alpha] - 2026-01-24
**Branch**: `radoub/issue-1096` | **PR**: #1101

### Sprint: Custom File Browsers (#1096)

- [x] #1083 - Use custom file browser for .utc/.bic files
  - File > Open now uses `CreatureBrowserWindow` from Radoub.UI
  - Shows creatures from current module directory
  - Filter by file type: All, Creature Blueprints (.utc), Player Characters (.bic)
  - Browse button to select different module folder
  - Consistent UX with other Radoub tools

---

## [0.1.54-alpha] - 2026-01-23
**Branch**: `quartermaster/issue-1046` | **PR**: #1062

### Tech Debt: Large Files Needing Refactoring (#1046)

- [x] MainWindow.axaml.cs - Removed unused `ResolveConversationPath` method and consolidated `StripCharacterToLevelOne` duplicate
- [x] MainWindow.Inventory.cs - Added null checks to fix CS8604 warnings
- [x] ModelService.cs - Removed 3 unused methods: `LoadBodyPartModel`, `GetRaceModelRef`, `IsPartBasedAppearance` (duplicated in AppearanceService)
- [x] Reviewed MainWindow.FileOps.cs, AdvancedPanel.axaml.cs, UtcFile.cs - already well-organized, no extraction needed

---

## [0.1.53-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-1050` | **PR**: #1056

### Sprint: Bug Bash - UI Fixes (#1050)

- [x] #938 - Portrait resizes with font scaling (uses dynamic PortraitWidth/PortraitHeight resources)
- [x] #1021 - Re-level confirmation dialog now has OK/Cancel buttons and scrollable content
- [x] #1026 - Bad strref values filtered in item type names (shared fix in Radoub.UI)
- [x] #1049 - Item Palette shows formatted type names instead of raw BaseRef values

---

## [0.1.52-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-746` | **PR**: #1054

### Feature: Appearance Preview Rendering (#746)

- Add visual preview of creature appearance in the Appearance panel

---

## [0.1.51-alpha] - 2026-01-22
**Branch**: `quartermaster/issue-1051` | **PR**: #1052

### Sprint: Creature Editing - Stats & Abilities (#1051)

- #588 - Add item details panel with icon display
- #728 - Edit Movement Speed
- #730 - Edit Base Saving Throws
- #733 - Edit Levelup Package
- #735 - Edit Special Abilities (Spell-Like Abilities)
- #739 - Metamagic Support for Spells

---

## [0.1.50-alpha] - 2026-01-21
**Branch**: `quartermaster/issue-1016` | **PR**: #1045

### Sprint: Variables & Metadata Fields (#1016)

#### Refactored (Already Completed)
- **BindableBase migration** (#926) - Migrated `FeatListViewModel` and `SkillViewModel` to inherit from `BindableBase`, replacing ~100+ lines of manual `INotifyPropertyChanged` boilerplate with `SetProperty()` calls
- **Theme color consolidation** (#925) - Consolidated duplicated theme-aware color methods (`GetDisabledBrush()`, `GetSuccessBrush()`, `GetInfoBrush()`, etc.) from FeatsPanel, SkillsPanel, and SpellsPanel into `BasePanelControl`

#### Added
- **Variables panel** (#946) - Add Variables, Category, and Comment fields for creature editing
- **INI script import** (#952) - Import script names from INI file for complex creatures

---

## [0.1.49-alpha] - 2026-01-20
**Branch**: `quartermaster/issue-586` | **PR**: #1034

### Feature: Item Palette Loading and Caching (#586)

Performance improvements for item palette - caching, background loading, and responsive UI.

#### Changed
- **On-demand palette loading** - Item palette now loads only when navigating to Inventory panel, not at app startup
  - Eliminates UI thread blocking from batch ObservableCollection updates
  - Tab navigation is now immediately responsive
- **Disk caching** - Item metadata cached to `~/Radoub/Quartermaster/palette_cache.json`
  - Cache pre-warms in background on startup (no UI impact)
  - Cache invalidates automatically when game paths change
  - Subsequent loads are near-instant from cache
- **Background cache building** - Full cache built on background thread
  - No UI updates during cache build
  - Standard items loaded first (visible immediately with default filter)
- **Batched UI loading** - Items added in batches of 100 with yields
  - UI remains responsive during palette population
  - Progress shown in status bar
- **Filter defaults** - "Show Custom" unchecked by default (CEP adds thousands)

#### Fixed
- **Tab unresponsiveness** - Clicking sidebar tabs during palette loading no longer blocks
  - Root cause: Background item loading was posting batch updates to UI thread
  - Fix: Defer all UI population until user navigates to Inventory panel
- **Palette persists across files** - Palette no longer disappears when opening second creature file

---

## [0.1.48-alpha] - 2026-01-20
**Branch**: `trebuchet/issue-951` | **PR**: #1029

### Feature: Complete Creature Appearance UI (#1028)

Add skin to the creature body - appearance customization for body parts, skin/hair colors, and armor-provided limbs.

#### Added
- **Textured 3D model rendering** - Model preview now renders with proper texture mapping instead of flat shading
  - UV coordinates extracted from MDL mesh data
  - SKVertices-based GPU-accelerated triangle rendering
  - PLT textures rendered with skin/hair/tattoo colors via TextureService
  - Lighting still applied for depth perception
- **Armor-provided body part overrides** - Equipped chest armor now overrides creature body parts
  - Loads armor's `ArmorParts` dictionary from UTI
  - Applies armor-provided models for torso, arms, legs, etc.
  - Head is not overridden (matching NWN behavior)
- **Full body part model loading** - All NWN body part types now load correctly
  - Correct NWN naming convention: bicepl/bicepr, forel/forer, legl/legr, shinl/shinr, footl/footr, shol/shor
  - Human model fallback for race-specific models not found (pme0 → pmh0)
  - Human texture fallback when race-specific textures missing
  - Texture name derivation for empty bitmap fields
- **PLT armor colors** - Full 10-layer PLT color support
  - PltColorIndices class for all layers (skin, hair, metal1/2, cloth1/2, leather1/2, tattoo1/2)
  - Armor colors loaded from equipped chest armor UTI
  - TextureService refactored to pass all color indices

#### Changed
- **Startup performance** - Moved GameDataService and panel initialization from constructor to `Opened` event, runs on background thread. Cache and item loading run in parallel. Window appears in ~1.9s instead of ~18 seconds.

#### Fixed
- **MDL parser** - Added bounds checking to ParseControllers to prevent exceptions on malformed controller data

#### Known Issues
- Head/face geometry not rendering (needs investigation)
- Armor body parts not rendering (armor overrides may not be applying)
- Tattoos not visible on model (#1031)

---

## [0.1.47-alpha] - 2026-01-19
**Branch**: `quartermaster/issue-829` | **PR**: #1017

### Sprint: Level-Up Wizard System (#829)

Wizard-based character leveling that enforces D&D 3.5e/NWN rules during level changes.

- [x] Level Up wizard (Character menu, Ctrl+L)
- [x] Class selection with prestige prerequisite validation
- [x] Feat selection with prerequisite enforcement
- [x] Skill allocation with class/cross-class point costs
- [x] Spell selection step (deferred - shows guidance)
- [x] Re-Level: Strip character to level 1 for rebuild
- [x] Down-Level: Save a level 1 copy to new file

#### Added
- **ClassService**: Comprehensive class data service for prestige prerequisites
  - Parses `cls_pres_*.2da` for all prestige requirement types (FEAT, SKILL, BAB, etc.)
  - Validates alignment restrictions via bitmask columns
  - `GetAvailableClasses()` returns selectable classes with qualification status

- **ClassBrowserWindow**: Class selection browser with prerequisite display
  - Left panel: Filterable class list with prestige badge
  - Right panel: Class description, hit die, skill points, prerequisite checklist

- **LevelUpWizardWindow**: 5-step wizard for adding a level
  - Step 1: Class selection (prestige classes greyed if unqualified)
  - Step 2: Feat selection with [Y]/[N] prerequisite indicators
  - Step 3: Skill allocation with class/cross-class cost tracking
  - Step 4: Spell selection (deferred with guidance message)
  - Step 5: Summary showing all choices and stat changes

- **Character Menu**: New menu with Level Up, Re-Level, Down-Level options
- **Re-Level**: Strips creature to level 1 (first class), removes choosable feats/skills
- **Down-Level (Save Copy)**: Creates a level 1 copy without modifying original

#### Technical Notes
- No hardcoding: All class/feat/skill data from 2DA files via GameDataService
- Prestige prerequisite types supported: FEAT, FEATOR, SKILL, BAB, RACE, ARCSPELL, DIVSPELL, CLASSOR
- Cross-class skills cost 2 points per rank (1 point for class skills)
- Human bonus: +1 feat at level 1, +1 skill point per level
- Feat chains: Selecting a feat re-evaluates prerequisites (e.g., Dodge unlocks Mobility)

---

## [0.1.46-alpha] - 2026-01-19
**Branch**: `quartermaster/issue-884` | **PR**: #1012

### Sprint: Bug Bash - Settings & UI Fixes (#884)

- [x] #873 - Settings: Changing font to system font has no effect

#### Fixed
- **System font selection now applies correctly** (#873)
  - When "(System Default)" is selected, `GlobalFontFamily` is now explicitly set to `FontFamily.Default`
  - Previously, empty font string caused no action, leaving the previous font active
  - Fix applied to all 5 tools: Quartermaster, Trebuchet, Manifest, Fence (Parley already had the fix)
  - Also fixed in ThemeManager for themes specifying `$Default` or empty font

---

## [0.1.45-alpha] - 2026-01-19
**Branch**: `radoub/sprint-988-1010` | **PR**: #1011

### Sprint: Fix and Stabilize Integration Tests (#1010)

- [x] Run full integration test suite and document all failures
- [x] Root cause analysis: Tests were using user's actual NWN installation (82 HAK files scanned = slow startup)
- [x] Fix FlaUITestBase to pre-seed `BaseGameInstallPath` preventing AutoDetect from finding user's game

- [x] Update SpellsPanel test to match current UI (ComboBox instead of radio buttons)
- [x] Skip tests requiring game data (classes.2da, packages.2da) not in test environment
- [x] Add parleypirate.utc to test data for creature-dependent tests

#### Results
- **Before**: 0/15 tests passing (all failed due to slow startup / window detection timeout)
- **After**: 12/15 tests passing, 3 skipped (valid reasons)
- **3 Skipped**: 2 require game data (picker tests), 1 Avalonia Expander issue

---

## [0.1.44-alpha] - 2026-01-18
**Branch**: `quartermaster/issue-715` | **PR**: #948

### Sprint: Core Features & Workflow (#715)

- [x] #690 - Convert between UTC and BIC (Save As) - **Complete**
  - ✅ UTC→BIC conversion initializes QuickBar with 36 empty slots
  - ✅ Set reasonable default Age (25) for converted characters
  - ✅ Ensure HP is valid (dead creatures get CurrentHP = MaxHP)
  - ✅ UI reloads panels after format conversion
  - ✅ BIC→UTC sets PaletteID=1 (Custom category) so creatures appear in toolset palette
  - ✅ Added 19 unit tests for conversion validation (9 ToUtcFile, 10 FromUtcFile)
- [x] #689 - Create new UTC file - **Complete**
  - ✅ File > New menu item with Ctrl+N shortcut
  - ✅ Creates new creature with sensible defaults (Human Commoner level 1)
  - ✅ Prompts to save unsaved changes before creating new file
  - ✅ Ctrl+S on new file redirects to Save As dialog
  - ✅ Sets PaletteID=1 (Custom category) so creatures appear in toolset palette
  - ✅ Palette Category dropdown on Advanced panel for Aurora Toolset compatibility
- [ ] #949 - QuickBar Panel (BIC only) - **Read-only**
  - ✅ QuickBar nav button visible only for BIC files
  - ✅ Displays 36 slots organized as 3 bars × 12 slots (F1-F12)
  - ✅ Shows slot type and ID for assigned slots
  - ❌ Cannot edit/write QuickBar slots yet
- [x] #626 - Wire up themes and preferences/settings
- [x] #643 - Faction Display & Management

### Bug Fixes
- Fixed BIC portrait display (uses Portrait string when PortraitId=0)
- Fixed BIC→UTC conversion preserving Portrait string (character's actual portrait) instead of overwriting with default
- Fixed Aurora Toolset "must specify valid portrait" error on BIC→UTC conversion (looks up PortraitId from Portrait string via portraits.2da)
- Added Portrait ID and Portrait ResRef fields to Character panel for debugging portrait issues
  - UTC files: Portrait ID enabled (primary), Portrait ResRef grayed out
  - BIC files: Portrait ResRef enabled (primary), Portrait ID grayed out
- Fixed bitmap cache crash when switching panels (removed premature disposal)
- Added global exception handlers for crash diagnostics
- Added ITP reader for palette categories

---

## [0.1.43-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-932` | **PR**: #937

### Sprint: Spell System Polish II (#932)

- [x] #775 - Add toggle for class/race spell restrictions
  - "Ignore class restrictions" checkbox in Spells panel filter toolbar
  - When checked: All spells shown as available, uses innate level for non-class spells
  - When unchecked (default): Standard behavior with class spell list restrictions
  - Summary tables update to count all known spells when ignoring restrictions

Note: #776 and #777 completed in v0.1.42-alpha (#936)

---

## [0.1.42-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-885` | **PR**: #936

### Sprint: Spells Panel Polish (#885)

- [x] #816 - Spells panel filter toolbar overflow (completed in v0.1.39)
- [x] #815 - Spells panel class selector overflow (completed in v0.1.39)
- [x] #776 - Support multiple memorizations of same spell
  - Replaced memorized checkbox with +/- buttons and count display
  - Shows memorization count (e.g., "3" for three memorizations)
  - Status column shows "M×3" for multiple memorizations
  - Tracks spell memorization counts correctly in model for save/load
  - Spontaneous casters (Bard, Sorcerer) show dash instead of controls
- [x] #777 - Add Clear All Spells option
  - "Clear All" button in Spell Actions section
  - Confirmation dialog showing count of spells to be cleared
  - Clears both known and memorized spells for selected class

---

## [0.1.41-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-931` | **PR**: #933

### Sprint: Browsers & Selection UX (#931)

- [x] #928 - Portrait browser with race/gender filtering
  - Filter by race and gender, search by resref name
  - Visual preview of selected portrait
  - Browse button next to portrait dropdown in Character panel
- [x] #803 - Soundset browser with filter and preview playback
  - Search/filter soundsets by name and gender (Male/Female/Other)
  - Preview panel with sound type selector (Hello, Goodbye, Attack, etc.)
  - Unavailable sound types greyed out with 0.5 opacity
  - Play/Stop buttons for soundset preview
  - Browse button next to soundset dropdown in Character panel
- [x] #928 - Portrait browser mini icons (32x40 thumbnails in WrapPanel grid)
- [x] CharacterPanel soundset preview: unavailable types greyed out

---

## [0.1.40-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-922` | **PR**: #927

### Sprint: Portrait & Character Preview (#922)

- [x] #919 - Add portrait selection/change functionality
  - Portrait moved from Appearance panel to Character panel (Identity section)
  - Sidebar portrait display refreshes when portrait is changed
  - Phenotype selection wired up to update model preview
- [x] #914 - Display NPC portrait in character preview (completed in #916)
- [x] #768 - Add Sex/Gender column to Appearance panel
  - Added Gender dropdown to Model section (Male/Female options from gender.2da)
  - Gender changes update the 3D model preview
  - Supports non-standard gender values if present in creature files

---

## [0.1.39-alpha] - 2026-01-17
**Branch**: `quartermaster/issue-923` | **PR**: #924

### Sprint: UI Polish & Bug Fixes (#923)

- [x] #801 - Settings window is now non-modal (users can interact with main window)
- [x] #837 - Font family changes now refresh all panels (Feats, Skills, Spells)
- [x] #839 - Path settings now validate in real-time as user types
- [x] #815 - Replaced class radio buttons with ComboBox for narrow screen compatibility
- [x] #816 - Compacted filter toolbar labels and reduced MinWidths for better wrapping

---

## [0.1.38-alpha] - 2026-01-16
**Branch**: `parley/issue-916` | **PR**: #918

### Enhancement: Portrait display and soundset preview (#916)

- Character portrait now displays in the sidebar when a creature is loaded
- Uses `ImageService.GetPortrait()` which loads from BIF archives
- Shows "No Portrait" placeholder when portrait unavailable
- Added soundset preview controls to Voice & Dialog section
- Dropdown allows selecting sound type (Hello, Goodbye, Attack, Death, etc.)
- Play button loads sound from SSF and plays via AudioService

---

## [0.1.37-alpha] - 2026-01-16
**Branch**: `quartermaster/issue-778` | **PR**: #912

### Refactor: SpellsPanel.axaml.cs Tech Debt (#778)

Refactor SpellsPanel.axaml.cs for improved maintainability (1232 lines).

---

## [0.1.36-alpha] - 2026-01-15
**Branch**: `radoub/issue-908` | **PR**: #909

### Backlog Cleanup (#802, #819)

- **#802**: Removed duplicate Conversation section from ScriptsPanel (now only in CharacterPanel)
  - Added browse button functionality to CharacterPanel's conversation field
  - CharacterPanel now receives GameDataService and file path for dialog browsing
- **#819**: Added "Not yet implemented" tooltips to disabled Undo/Redo/Cut/Copy/Paste menu items

---

## [0.1.35-alpha] - 2026-01-11
**Branch**: `quartermaster/sprint-630-871-870` | **PR**: #872

### Sprint: Level-Up Resource Tracking (#630, #871, #870)

Resource calculation and tracking for level-up workflow.

#### Work Items
- [x] #870 - Track Ability Score Increases from Leveling
- [x] #871 - Calculate Available Resources on Level-Up (HP, Skills, Feats, Spells)
- [x] #630 - Feats Panel Filter & UX Tuning

---

## [0.1.34-alpha] - 2026-01-11
**Branch**: `quartermaster/issue-791` | **PR**: #869

### Feat: Classes Panel MVP - Add Class & Level-Up (#791)

Implementation of Classes Panel MVP (Approach D - Phase 1). Add-only workflow: level-up and add class functionality without level-down or class removal.

#### Features
- Wire up + button click handlers for level-up
- Add Class button with ClassPickerWindow
- Level constraint validation (40 total, MaxLevel per class)
- BAB and save recalculation on level-up
- Total level display with cap warning

---

## [0.1.33-alpha] - 2026-01-11
**Branch**: `quartermaster/issue-864` | **PR**: #865

### Sprint: Classes Panel Prerequisites (#864)

Establish prerequisites for Classes Panel MVP implementation.

#### Work Items
- [x] #790 - Audit: Remove hardcoded game data (2DA compliance)
- [x] #733 - Edit Levelup Package (packages.2da integration) - *Already implemented*
- [x] #825 - Export Character Sheet (Text/Markdown)

#### 2DA Compliance (#790)

Refactored UI panels to use service methods instead of duplicated hardcoded fallbacks.

**CreatureDisplayService**
- Added `GetClassHitDie()` method - reads HitDie column from classes.2da

**ClassesPanel.axaml.cs**
- Removed 90+ lines of hardcoded class data (names, hit dice, skill points)
- Now delegates to CreatureDisplayService for all class lookups
- Class features text retained as acceptable hardcoding (display-only flavor text not in 2DA)

**SkillsPanel.axaml.cs**
- Removed 70+ lines of hardcoded skill data (names, key abilities, skill points)
- Now delegates to SkillService via CreatureDisplayService

**Pattern established**: Services (CreatureDisplayService, SkillService, FeatService) try 2DA first with minimal fallbacks. UI panels must use services - no duplicate fallback code.

#### Character Sheet Export (#825)

New CharacterSheetService generates formatted character sheets for reference.

**Features**
- Export to plain text (.txt) with ASCII formatting
- Export to Markdown (.md) with tables
- File > Export Character Sheet menu with submenu options
- Ctrl+Shift+E keyboard shortcut for quick text export

**Content Includes**
- Identity (name, race, gender, alignment, deity, subrace, tag, resref)
- Class progression with hit dice and skill points per level
- Ability scores with racial modifiers
- Combat statistics (HP, AC, BAB, saves)
- Skills with ranks, key ability, and class skill indicator
- Feats grouped by category (Combat, Active, Defensive, Magical, Class/Racial)
- Spells known by class and level (caster classes only)
- Equipment slots with item resrefs
- Scripts (UTC only, not shown for BIC player files)
- Gold and XP (BIC files only)

---

## [0.1.32-alpha] - 2026-01-09
**Branch**: `quartermaster/issue-734` | **PR**: #804

### Feat: Edit Skill Ranks (#734)

Enable skill rank editing via +/- buttons with validation against class skill maximums.

#### Skill Editing
- +/- buttons now functional for each skill row
- Max ranks enforced: class skills = level+3, cross-class = (level+3)/2
- Buttons disable appropriately at min (0) and max rank limits
- Changes immediately update UtcFile.SkillList for save
- SkillsChanged event marks document dirty

#### UI Improvements
- Added "Mod" column showing ability modifier (+X/-X format) with color coding
- Added skill points summary table (left panel) showing:
  - Base skill points per class from 2DA (SkillPointBase column)
  - Points per level calculation (base + INT modifier)
  - Total points spent vs estimated available
  - Warning color when over-allocated
- Summary displays character level and max rank limits
- Tooltips show current max rank or reason for disabled state
- Theme-aware sizing throughout (no hardcoded font sizes)
- Wider columns for better readability
- Footer shows max rank formula reference

---

## [0.1.31-alpha] - 2026-01-07
**Branch**: `quartermaster/issue-799` | **PR**: #800

### Refactor: Hardcoded Colors to Theme System (#799)

Refactor hardcoded colors throughout Quartermaster to use semantic theme colors for colorblind/accessibility support.

#### Theme Infrastructure
- Added `Disabled` color to `ThemeColors` model (auto-registered as `ThemeDisabled`)
- Added `disabled` color to all 8 Quartermaster theme JSON files
- Added derived font size resources: `FontSizeXSmall`, `FontSizeSmall`, `FontSizeNormal`, `FontSizeMedium`, `FontSizeLarge`, `FontSizeXLarge`, `FontSizeTitle`

#### Font Size Accessibility
- Panel headers now use `FontSizeXLarge` (scales with theme font size setting)
- Settings/Appearance headers use `FontSizeLarge`
- DialogHelper About dialog uses `FontSizeTitle`

#### Fixed Panels
- FeatsPanel: Status colors (available/known/granted/unavailable) now use theme colors
- SkillsPanel: Class skill indicators now use theme colors
- SpellsPanel: Spell status colors now use theme colors
- AppearancePanel: Color swatches and model preview container now theme-aware

#### Fixed Dialogs
- SettingsWindow: All border colors now use `ThemeBorder`
- ColorPickerWindow: Swatch borders and selection highlight now theme-aware
- FactionPickerWindow: Border and selection styles now theme-aware
- PackagePickerWindow: Border and selection styles now theme-aware

#### Fixed Controls
- ModelPreviewControl: Background and placeholder text now use theme colors

---

## [0.1.30-alpha] - 2026-01-05
**Branch**: `quartermaster/issue-769` | **PR**: #789

### Refactor: Large File Tech Debt (#769)

Split large files into partial classes for improved maintainability and AI readability.

#### MdlAsciiReader (1083 → 6 files ~200 lines each)

- `MdlAsciiReader.cs` - Core parsing, main loop
- `MdlAsciiReader.NodeParsing.cs` - Node creation and property routing
- `MdlAsciiReader.TrimeshParsing.cs` - Trimesh/mesh properties
- `MdlAsciiReader.ListParsing.cs` - Vertex, face, texture, color, weight lists
- `MdlAsciiReader.TypeParsing.cs` - Light, emitter, reference properties
- `MdlAsciiReader.AnimationParsing.cs` - Animation data
- `MdlAsciiReader.Utilities.cs` - Tokenization and parsing helpers

#### MdlBinaryReader (1071 → 7 files ~150 lines each)

- `MdlBinaryReader.cs` - Core parsing, file header
- `MdlBinaryReader.PointerHelpers.cs` - Memory pointer conversion
- `MdlBinaryReader.NodeParsing.cs` - Node creation, controllers, children
- `MdlBinaryReader.MeshParsing.cs` - Mesh/trimesh data
- `MdlBinaryReader.TypeParsing.cs` - Light, emitter, reference nodes
- `MdlBinaryReader.DataReading.cs` - Vertex, face, texcoord reading
- `MdlBinaryReader.AnimationParsing.cs` - Animation data
- `MdlBinaryReader.Utilities.cs` - Helper methods

#### AppearancePanel (755 → 3 files ~250 lines each)

- `AppearancePanel.axaml.cs` - Fields, constructor, core methods
- `AppearancePanel.DataLoading.cs` - Data loading from services
- `AppearancePanel.EventHandlers.cs` - Event wiring and handlers

#### MainWindow.Inventory (614 → 3 files ~200 lines each)

- `MainWindow.Inventory.cs` - UI population, operations, sync
- `MainWindow.ItemResolution.cs` - UTI file resolution
- `MainWindow.ItemPalette.cs` - Background palette loading

---

## [0.1.29-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-761` | **PR**: #774

### Sprint: Spell System Polish (#761)

Enhance SpellsPanel with multi-class spell slot display and polish spell editing workflow.

- [x] #758 - Add spell slot summary for each caster class
- [x] #737 - Add/Remove Known Spells (already implemented in #756)
- [x] #738 - Add/Remove Memorized Spells (already implemented in #756)

#### Added

- **Spell Slot Table** (left panel) showing spell slots/known limits per class
  - Grid format with spell levels as rows, caster classes as columns
  - Color-coded: gold (full), green (partial), gray (empty/unavailable)
  - Spontaneous casters show "spells known" limits, prepared casters show spell slots
  - Filters out feat-based abilities from slot counts
- **Known Spells List** (left panel, below slot table)
  - Shows ALL caster classes with their spells grouped by level
  - Overlapping spells (same spell in multiple classes) highlighted in gold with ⬥
  - Feat-based abilities marked with asterisk (*) in gray
  - Selected class header highlighted in blue
  - Scrollable for characters with many spells

#### Fixed

- Spell list now loads all spells from spells.2da (fixed early termination bug)
  - Previously stopped at first gap after 100 spells
  - Now scans up to 2000 rows with consecutive-empty detection
  - Supports custom content (CEP, PRC, etc.)

---

## [0.1.28-alpha] - 2026-01-04
**Branch**: `radoub/issue-557` | **PR**: #772

### Sprint: Item Property Search (#557)

Add item property search to ItemFilterPanel. Search/filter items by their properties.

#### Added

- **Property Search** in ItemFilterPanel
  - New "Properties:" text box searches item property strings
  - Debounced input (300ms) for performance
  - Case-insensitive search
  - Clear button to reset property filter
  - Filter state persisted with other filter settings

#### Use Cases

- Find class-restricted items: search "monk" or "wizard"
- Find spell items by level: search "level 3"
- Find items with specific bonuses: search "enhancement" or "+2"
- Find damage types: search "fire" or "cold"

---

## [0.1.27-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-617` | **PR**: #766

### Sprint: 3D Character Model Preview (#617)

Add 3D character model preview to the Appearance tab, showing the character's visual appearance alongside body part configuration.

#### Layout Changes
- Restructure Appearance tab: body parts on left, 3D model preview on right
- Reduce text box widths to respect Aurora Engine's 16-char filename limit

#### 3D Model Rendering
- MDL parser for NWN model format
- PLT (palette texture) rendering with color channels
- Skeleton/animation support (idle pose minimum)
- Real-time model updates when appearance parts change

---

## [0.1.26-alpha] - 2026-01-04
**Branch**: `quartermaster/issue-760` | **PR**: #765

### Sprint: Panel Refactoring (#760)

Reduce code duplication and improve maintainability across Quartermaster panels.

- [x] #686 - Extract common panel patterns to base class
- [x] #632 - Split CreatureDisplayService into focused services

#### Added

- **BasePanelControl** ([BasePanelControl.cs](Quartermaster/Views/Panels/BasePanelControl.cs))
  - Abstract base class for panels with `IsLoading`, `CurrentCreature`, `LoadCreature()`, `ClearPanel()`
  - `DeferLoadingReset()` for suppressing events during data binding
  - Helper methods: `SetText()`, `SetCheckBox()`, `SetTextBox()`

- **ComboBoxHelper** ([ComboBoxHelper.cs](Quartermaster/Views/Panels/ComboBoxHelper.cs))
  - Generic `SelectByTag<T>()` replacing type-specific overloads
  - `GetSelectedTag<T>()` and `GetSelectedTagOrDefault<T>()`

- **BindableBase** ([BindableBase.cs](Quartermaster/ViewModels/BindableBase.cs))
  - INotifyPropertyChanged base with `SetProperty<T>()` helper

- **Focused Services** (extracted from CreatureDisplayService)
  - `SkillService` - skill lookups, class skill calculations
  - `FeatService` - feat lookups, categories, prerequisites
  - `AppearanceService` - appearance, phenotype, portrait, wing, tail, faction lookups
  - `SpellService` - spell lookups, caster class info

#### Changed

- **CreatureDisplayService** - now delegates to focused services
  - Exposes `Skills`, `Feats`, `Appearances`, `Spells` properties
  - Backward-compatible delegation methods preserved
  - Reduced from 1983 to 468 lines

- **Panels refactored to use BasePanelControl**
  - AdvancedPanel, ScriptsPanel, SkillsPanel, ClassesPanel
  - Removed duplicate fields and helper methods

---

## [0.1.25-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-587` | **PR**: #759

### Feature: Extract NWN Item Icons from TGA/DDS (#587)

Add support for extracting and displaying actual NWN item icons from game files.

#### Added

- **Pfim Library Integration**
  - Added Pfim NuGet package (0.11.4) for TGA/DDS decoding
  - Pure managed .NET Standard 2.0 - cross-platform compatible

- **Image Service Infrastructure** (Radoub.Formats)
  - `IImageService` interface for loading NWN image assets
  - `ImageService` implementation with TGA/DDS decoding via Pfim
  - `ImageData` class for RGBA pixel data output
  - Memory cache with LRU eviction (~500 icons, ~2MB)

- **PLT (Palette Texture) Parser** (Radoub.Formats)
  - `PltReader` for NWN's layered texture format
  - 24-byte header parsing (signature, version, dimensions)
  - 2-byte pixel format (grayscale + layer ID)
  - `PltLayers` constants for 10 color layers (Skin, Hair, Metal1/2, Cloth1/2, etc.)
  - `PaletteData` for loading pal_*.tga palette files
  - Rendering with palette color application

- **Item Icon Service** (Quartermaster)
  - `ItemIconService` converts ImageData to Avalonia Bitmap
  - Icon lookup from baseitems.2da (ItemClass, DefaultIcon, MinRange, MaxRange)
  - Icon naming pattern support: i<ItemClass>_<number>.tga
  - Portrait loading support

- **UI Integration**
  - `ItemViewModel.IconBitmap` property for actual game icons
  - `ItemViewModel.HasGameIcon` for conditional rendering
  - `EquipmentSlotControl.axaml` updated to show game icons or SVG placeholders
  - `ItemListView.axaml` updated to show game icons in palette/backpack
  - Graceful fallback to existing SVG placeholders when game data unavailable

#### Fixed

- **Panel Crash on Icon Load** - Fixed crash when loading panels with many items
  - SpellsPanel/FeatsPanel: Converted from non-virtualized `ItemsControl` to virtualized `ListBox`
  - Inventory: Implemented lazy icon loading via delegate pattern (icons load on scroll into view)
  - Prevents loading hundreds of bitmaps simultaneously during panel render

- **Log Level Setting Ignored** - User's log level preference wasn't being applied on startup
  - `SettingsService.LoadSettings()` now calls `UnifiedLogger.SetLogLevel()` after loading saved value

#### Tests

- PLT reader tests (7 tests)
  - Valid PLT parsing and pixel extraction
  - Invalid signature/size handling
  - Grayscale fallback rendering
  - Palette ResRef lookup

---

## [0.1.24-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-756` | **PR**: #757

### Sprint: Spells Foundation (#756)

Enable SpellsPanel to display creature's actual known and memorized spells by implementing the GFF parsing layer.

#### Added

- **Spell List Parsing** (#740, #741)
  - `KnownSpell` model class with Spell, SpellFlags, SpellMetaMagic fields
  - `MemorizedSpell` model class with additional Ready field for game instances
  - `CreatureClass.KnownSpells[10]` - arrays of known spells by spell level (0-9)
  - `CreatureClass.MemorizedSpells[10]` - arrays of memorized spells by spell level (0-9)
  - UtcReader/BicReader: Parse KnownList0-9 and MemorizedList0-9 from creature GFF
  - UtcWriter/BicWriter: Write spell lists back to GFF (round-trip support)
  - BicFile conversion: Deep copy spell lists during UTC/BIC conversion

- **SpellsPanel Integration**
  - Populate _knownSpellIds from parsed KnownSpells data
  - Populate _memorizedSpellIds from parsed MemorizedSpells data
  - Spells now correctly show Known/Memorized status from creature file

- **Spell Editing**
  - Checkboxes now enabled for non-blocked spells
  - Click checkbox to add/remove spells from known list
  - Changes update model data and mark document dirty
  - SpellsChanged event for dirty state tracking

#### Fixed

- **Caster Class Selection** - Radio buttons now enable based on actual spell data in creature file, not just 2DA lookup results

#### Tests

- Round-trip tests for KnownSpells (UtcReaderTests)
- Round-trip tests for MemorizedSpells (UtcReaderTests)
- Parse tests verifying spell ID, flags, and metamagic extraction

---

## [0.1.23-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-751` | **PR**: #755

### Sprint: Character & Alignment (#751)

Add race and alignment editing to Character panel, plus fix the emoji display bug.

#### Added

- **Race Dropdown** (#748)
  - Add GetAllRaces() to CreatureDisplayService (loads from racialtypes.2da)
  - Race ComboBox in Identity section of Character panel
  - Custom races from modules added dynamically if not in 2DA

- **Editable Alignment** (#732)
  - Replace read-only ProgressBars with interactive Sliders
  - Good-Evil and Lawful-Chaotic axes (0-100 scale)
  - Alignment name updates live as sliders change
  - AlignmentChanged event for dirty flag tracking

- **Package Picker**
  - Add GetAllPackages() and GetPackageName() to CreatureDisplayService (loads from packages.2da)
  - PackagePickerWindow dialog for selecting auto-levelup packages
  - Picker button in Levelup Settings section of Classes panel
  - Package names now display from 2DA/TLK instead of hardcoded values

#### Fixed

- **Navigation Button Emojis** (#691)
  - Add NavIcon class to emoji TextBlocks to prevent Foreground inheritance
  - Emojis now maintain consistent appearance regardless of selection state

---

## [0.1.22-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-750` | **PR**: #752

### Sprint: Stats Panel Editing (#750)

Complete Stats panel editing to match Aurora Toolset parity.

#### Added

- **Hit Points Editing** (#726)
  - NumericUpDown control for Base HP (dice rolls)
  - Con Bonus and Max HP displayed as calculated values
  - Base HP changes automatically recalculate Max HP
  - Matches Aurora Toolset behavior (CurrentHP not exposed)
- **Natural AC Editing** (#727)
  - NumericUpDown control for natural armor class (0-255 range)
- **Armor Class Section** (Aurora Toolset parity)
  - Separated Armor Class section from Combat section
  - Display Base AC (10), Dex Bonus, Size Modifier, and Total AC
  - Size Modifier calculated from appearance.2da SIZECATEGORY
  - Total AC recalculates when Natural AC or Dex changes
- **Challenge Rating Editing** (#729)
  - NumericUpDown control for base CR (0-100, 0.25 increment)
  - Works alongside existing CR Adjust control

---

## [0.1.21-alpha] - 2026-01-03
**Branch**: `quartermaster/issue-725` | **PR**: #749

### Feature: Edit Ability Scores (#725)

Add editing capability for the 6 ability scores (STR, DEX, CON, INT, WIS, CHA).

#### Added

- NumericUpDown controls for modifying base ability scores
- Validation for 3-40 range (NWN limits)
- Recalculation of derived stats on ability change:
  - MaxHP recalculated when CON changes
  - Saving throws updated for CON/DEX/WIS changes
  - Dex AC bonus displayed in Combat section
- AbilityScoresChanged event for dirty flag tracking

---

## [0.1.20-alpha] - 2026-01-02
**Branch**: `quartermaster/issue-645` | **PR**: #747

### Epic: Character Color Picker (#645)

Implement character color customization (Skin, Hair, Tattoo1, Tattoo2) for part-based appearances.

#### Added

- Color fields (Skin, Hair, Tattoo1, Tattoo2) in Appearance tab with NumericUpDown controls
- Color swatches displaying actual palette colors next to each color field
- Clickable swatches open ColorPickerWindow dialog
- ColorPickerWindow with 16x11 gradient swatch grid matching Aurora Toolset layout
- PaletteColorService for extracting colors and gradients from NWN palette TGA files
- TgaReader in Radoub.Formats for parsing uncompressed TGA images
- Double-click on swatch confirms selection

#### Fixed

- Handle Aurora Toolset's BodyPart_RFoot bug (incorrectly saved as ArmorPart_RFoot)
- NumericUpDown decimal display (added FormatString="0")

---

## [0.1.19-alpha] - 2026-01-01
**Branch**: `quartermaster/issue-715` | **PR**: #716

### Sprint: Core Features & Workflow (#715)

Complete core Quartermaster functionality to achieve Aurora Toolset feature parity for creature editing workflows.

#### Added

- #643 - Faction Display & Management
  - Created FacFile and FacReader in Radoub.Formats for parsing repute.fac
  - FactionPickerWindow dialog with ListBox showing faction ID and name
  - Display faction name from repute.fac if available, with ID fallback
  - Graceful fallback to 5 default NWN factions when repute.fac not found
- #626 - Wire up themes and preferences/settings
  - Created SettingsWindow with 3 tabs: Resource Paths, UI Settings, Logging
  - Resource Paths: Base game path and user data path with browse/auto-detect
  - UI Settings: Theme selection, font size slider, font family picker with preview
  - Logging: Log level dropdown, log retention slider
  - Wired View > Settings menu item to open SettingsWindow
- #690 - Convert between UTC and BIC (Save As)
  - Added BicFile.FromUtcFile() and BicFile.ToUtcFile() conversion methods
  - SaveFileAs automatically converts creature format based on selected extension
  - UTC→BIC: Creates BicFile copy, sets IsPC=true, initializes BIC-specific fields
  - BIC→UTC: Creates UtcFile copy, sets IsPC=false, discards player-specific data

---

## [0.1.18-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/tech-debt-devex` | **PR**: #698

### Sprint: Tech Debt & DevEx (#697)

Improve developer experience and code reliability.

#### Fixed

- #652 - Command-line arguments to open BIC/UTC files (already implemented, added tests)
- #685 - File marked dirty on initial load due to deferred TextChanged events from Avalonia dispatcher
- #687 - Settings MaxRecentFiles defaults to 10 if 0/corrupt; added logging for missing file cleanup
- #692 - Fixed navigation test to check CharacterPanel (actual default), added Character to Theory tests

---

## [0.1.17-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/inventory-sync` | **PR**: #695

### Sprint: Inventory Sync & Item Operations (#694)

Implement sync layer between inventory UI and data model so item changes persist on save.

#### Added

- Inventory metadata on ItemViewModel (grid position, dropable/pickpocketable flags)
- CreateBackpackItem factory method for items with full inventory metadata
- SyncInventoryToCreature() method syncs UI state to creature model before save
- AddToBackpackRequested event for adding items from palette
- EquipItemsRequested event for equipping items from palette
- AddToBackpack() and RemoveFromBackpack() methods on InventoryPanel
- UnequipToBackpack() method for moving equipped items to backpack

#### Changed

- SaveFile() now calls SyncInventoryToCreature() before writing
- CreatePlaceholderItem() refactored to use shared ResolveUtiFile() helper
- Backpack items now preserve grid position and flags on round-trip

---

## [0.1.16-alpha] - 2025-12-31
**Branch**: `quartermaster/sprint/bic-file-support` | **PR**: #688

### Sprint: BIC File Support (#680)

Enable Quartermaster to properly edit BIC files (player characters) with appropriate UI differences from UTC files (creature blueprints).

#### Added

**BIC-Specific Character Fields (#676)**
- Player Character section with Experience, Gold, and Age fields
- Biography section for character description (shown when examined)
- File type detection automatically shows/hides appropriate sections

**Application Branding**
- Application icon (Quartermaster.ico) displayed in title bar and taskbar

#### Changed

**File Type Handling (#676)**
- Scripts nav button hidden for BIC files (player characters don't have scripts)
- Conversation field hidden for BIC files (not used for player characters)
- Challenge Rating display hidden for BIC files (player characters don't have CR)
- Blueprint ResRef and Comment fields hidden for BIC files (not in BIC format)
- Title bar shows "(Player)" indicator when editing BIC files
- Automatically navigates away from Scripts if loading BIC while on Scripts panel

---

## [0.1.15-alpha] - 2025-12-30
**Branch**: `quartermaster/sprint/ui-polish-panel-reorg` | **PR**: #684

### Sprint: UI Polish & Panel Reorganization (#678)

Reorganize Quartermaster's panel structure for improved usability.

#### Added

**Appearance Panel (#671)**
- New dedicated panel for creature visual configuration
- Appearance Type dropdown with dynamic/static indicator
- Phenotype and Portrait dropdowns
- Body Parts section with all 14 body part controls
- Automatically enables/disables body parts based on appearance type

**Character Panel (#672)**
- New dedicated panel for character identity and roleplay info
- First Name and Last Name text fields with CExoLocString support
- Subrace and Deity text fields (moved from Advanced panel)
- Sound Set dropdown (moved from Advanced panel)
- Conversation field with browse and clear buttons

**Stats Panel Enhancement (#670)**
- Added Challenge Rating adjustment control
- Moved CR Adjust from Advanced panel to Stats panel

#### Changed
- Removed duplicate Identity section from Classes panel (#674)
- Removed separator between Inventory and Advanced navigation buttons (#673)
- Streamlined Advanced panel to focus on blueprint/tag, flags, and behavior settings

---

## [0.1.14-alpha] - 2025-12-30
**Branch**: `quartermaster/sprint/scripts-advanced-props` | **PR**: #669

### Sprint: Scripts & Advanced Properties (#668)

Complete Quartermaster's core creature editing functionality with Scripts panel and remaining Advanced Properties fields.

#### Added

**Scripts Panel (#646)**
- Editable script ResRef fields for all 13 event scripts
- Browse button per script to open ScriptBrowserWindow
- Clear button per script slot to remove assignment
- Browse button for Conversation to open DialogBrowserWindow
- Real-time summary showing assigned script count
- Editable Conversation field with "Open in Parley" integration
- ScriptsChanged event for dirty state tracking
- MaxLength=16 enforced per Aurora Engine constraint

**Shared Browser Windows**
- Moved ScriptBrowserWindow to Radoub.UI shared library
- Created DialogBrowserWindow for dialog file browsing
- Created IScriptBrowserContext interface for tool-specific implementations
- ParleyScriptBrowserContext and QuartermasterScriptBrowserContext implementations

#### Fixed

**Aurora Toolset Compatibility**
- UtcWriter/BicWriter: Always write all 13 script fields (even empty ones)
- Fixed typo: ScriptuserDefine → ScriptUserDefine

**Subrace & Deity Fields (#647)**
- Editable Subrace text field in Identity section
- Editable Deity text field in Identity section
- Changes persist to UTC file

**Challenge Rating Display (#648)**
- CR display showing stored ChallengeRating value
- Editable CR Adjustment spinner (-100 to +100)
- Changes persist to UTC file

---

## [0.1.13-alpha] - 2025-12-30
**Branch**: `quartermaster/fix/flaky-spells-navigation-test` | **PR**: #656

### Fix: Flaky FlaUI Navigation Test (#654)

Fix intermittent test failures in NavigationTests when running as part of full test suite.

**Root Cause**: FlaUI's `Button.Click()` uses simulated mouse clicks at screen coordinates. When VSCode or other windows steal focus, clicks go to the wrong window.

**Solution**: Use UIA Invoke pattern for button clicks, which sends events directly through the automation framework without relying on screen coordinates.

#### Changed
- NavigationTests: Use `button.Patterns.Invoke.Pattern.Invoke()` instead of `button.Click()` for reliable automation
- Added `WaitForPanelVisible()` with retry logic matching SpellsPanelTests pattern
- Added `EnsureFocused()` calls before interactions
- Added 2-second inter-suite delay in run-tests.ps1 between UI test suites

#### Removed
- Removed Spells from Navigation Theory (covered by SpellsPanelTests.SpellsPanel_NavigatesSuccessfully)
- Removed `Navigation_SwitchBetweenPanels_WorksCorrectly` (flaky as first test)

---

## [0.1.12-alpha] - 2025-12-30
**Branch**: `quartermaster/feat/open-in-parley` | **PR**: #651

### Feature: Open Conversation in Parley (#642)

- Add "Open in Parley" button to Scripts tab for creatures with conversation assigned
- Launch Parley with the referenced `.dlg` file via command-line
- Graceful error handling if conversation file not found

---

## [0.1.11-alpha] - 2025-12-29
**Branch**: `quartermaster/feat/appearance-identity` | **PR**: #644

### Feature: Advanced Properties - Appearance & Identity Fields (#641)

Implement the Advanced Properties page with appearance customization and identity field management.

#### Added
- **Appearance Section**
  - Preset appearance dropdown (populated from appearance.2da)
  - Custom appearance component selection (Head, Body, Tail, Wings)
  - Phenotype selection
  - Portrait selection/preview
- **Identity Fields**
  - Blueprint ResRef with copy button
  - Tag with copy button
  - Editable Comment field

---

## [0.1.10-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/spells-panel` | **PR**: #633

### Sprint: Spells Panel with Search & Filter (#616)

Add the Spells panel to Quartermaster with search and filtering - addressing Aurora Toolset's spell selection pain points.

#### Added
- **Spells Panel** - Full spell browsing with search and filtering
  - Real-time text search filtering by spell name
  - Clear button to reset search
- **Spell Level Filter** - Filter by spell level (0-9)
- **Spell School Filter** - Filter by school:
  - All Schools (default)
  - Abjuration / Conjuration / Divination / Enchantment
  - Evocation / Illusion / Necromancy / Transmutation
- **Status Filter** - Filter by spell status:
  - All Spells / Known Only / Memorized Only / Available / Blocked
- **Class Selection** - 8 class radio buttons (Class 1-8)
  - Shows class name, level, and max spell level (e.g., "Wizard (10) - Lvl 5")
  - Non-caster classes disabled
  - Low-level casters without spells yet show "No spells"
- **Detailed Spell List** - Columns:
  - Checkbox (known/memorized status, read-only)
  - Spell Name
  - Spell Level (for selected class)
  - School
  - Innate Level
  - Status (Known/Memorized/Blocked)
- **Meta-Magic Expander** - Placeholder for future metamagic display
- **Spell Actions** - Placeholder buttons for Clear/Save/Load spell lists
- **FlaUI Automation IDs** - Full test coverage for UI elements
- **CreatureDisplayService** - Added spell data methods:
  - `GetAllSpellIds()` - Gets all spells from spells.2da
  - `GetSpellInfo()` - Gets detailed spell info (school, levels by class)
  - `GetSpellSchoolName()` - Converts school enum to display name
  - `GetMaxSpellLevel()` - Gets max spell level for class at given level
  - `IsCasterClass()` - Checks if class has SpellGainTable in classes.2da

#### Note
- Spell lists (known/memorized) are not yet parsed from creature files
- Panel is read-only in this version
- Editing features planned for future sprint

---

## [0.1.9-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/feats-panel` | **PR**: #629

### Sprint: Feats Panel with Search & Filter (#615)

Add the Feats panel with robust search and filtering - addressing Aurora Toolset's major pain point.

#### Added
- **Search Box** - Real-time text search filtering by feat name
  - Clear button to reset search
  - Filters as you type
- **Category Filter Dropdown** - Filter by feat type:
  - All Feats (default)
  - Combat / Active Combat / Defensive / Magical / Class/Racial / Other
  - Assigned Only / Granted Only (class-granted feats)
  - Unassigned Only / Available Only / Unavailable Only
  - Prereqs Met / Prereqs Unmet / Has Prereqs (filter by prerequisite status)
- **All Feats List** - Shows ALL feats from feat.2da, not just assigned
  - Columns: Status | Feat Name | Category | Status Text
  - Tooltip shows feat description
- **Visual Status Distinction**:
  - ✓ Green checkmark + "Assigned" for chosen feats
  - ★ Gold star + "Granted" for class-granted feats
  - ⚠ Orange warning + "Unmet" for feats with unmet prerequisites
  - ○ Blue circle + "Available" for feats with all prerequisites met
  - ✗ Gray X + "Unavailable" for feats not available to class/race
  - Row highlighting: green for assigned, gold for granted, gray for unavailable
- **Special Abilities Section** - Collapsible expander showing spell-like abilities
- **Summary Line** - Shows assigned count, granted count, and filter status
- **Feat Add/Remove** - +/- buttons on each feat row
  - Add any feat not currently assigned
  - Remove user-assigned feats (class-granted feats cannot be removed)
  - Updates creature's feat list in real-time
  - `FeatsChanged` event for dirty state tracking
- **Prerequisite Checking** - Full prerequisite validation with tooltip display:
  - Required feats (AND logic)
  - Or-required feats (OR logic - need at least one)
  - Ability score requirements (STR, DEX, INT, WIS, CON, CHA)
  - Base Attack Bonus requirements
  - Spell level requirements
  - Skill rank requirements
  - Level requirements (character or class-specific)
  - Epic requirement (level 21+)
  - Tooltip shows ✓/✗ for each prerequisite
- **Availability Checking** - Feat availability based on class/race:
  - Universal feats available to all
  - Class-specific feats checked against cls_feat_*.2da tables
- **CreatureDisplayService Integration** - Added feat-related methods:
  - `GetFeatCategory()` - Feat category from TOOLSCATEGORIES column
  - `GetFeatDescription()` - Feat description from TLK
  - `IsFeatUniversal()` - Check ALLCLASSESCANUSE column
  - `GetAllFeatIds()` - Get all valid feat IDs from feat.2da
  - `GetFeatInfo()` - Get complete feat information
  - `GetClassGrantedFeatIds()` - Get feats granted by a class
  - `GetCombinedGrantedFeatIds()` - Combined granted feats for multiclass
  - `IsFeatAvailable()` - Check if feat is available to creature
  - `GetUnavailableFeatIds()` - Get set of unavailable feats
  - `GetFeatPrerequisites()` - Get prerequisite data for a feat
  - `CheckFeatPrerequisites()` - Check prerequisites against creature

---

## [0.1.8-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/skills-panel` | **PR**: #628

### Sprint: Skills Panel (#614)

Add the Skills panel displaying all skill ranks for the character.

#### Added
- **Skills List Display** - Shows all skills from skills.2da with columns:
  - Skill Name with key ability indicator (STR, DEX, INT, WIS, CHA, CON)
  - Rank value (base skill points invested)
  - Total value (Rank + Ability Modifier)
  - +/- buttons as disabled placeholders for future editing
- **Class Skill Highlighting** - Distinguishes class skills from cross-class skills
  - Visual indicator (● for class skill, ○ for cross-class)
  - Light blue background highlight for class skills
  - Uses cls_skill_*.2da for each of character's classes
  - Combined class skills from multiclass characters
- **Sorting Options** - Dropdown to sort skills:
  - Alphabetical (default)
  - By Rank (highest first)
  - Class Skills First
- **Filtering** - "Trained Only" checkbox to show only skills with ranks > 0
- **Summary Display** - Shows count of trained skills, total ranks, and class skill count
- **Unavailable Skill Detection** - Grays out skills the character cannot take
  - Checks `AllClassesCanUse` column in skills.2da
  - Cross-references cls_skill_*.2da for class-restricted skills
  - Shows ✗ indicator for unavailable skills
- **CreatureDisplayService Integration** - Added skill name resolution via 2DA/TLK:
  - `GetSkillName()` - Skill name from skills.2da with TLK lookup
  - `GetSkillKeyAbility()` - Key ability from skills.2da
  - `GetClassSkillsTable()` - Class skills table name from classes.2da
  - `IsClassSkill()` - Check if skill is class skill for a class
  - `GetClassSkillIds()` - Get set of class skill IDs for a class
  - `GetCombinedClassSkillIds()` - Combined class skills for multiclass
  - `IsSkillUniversal()` - Check if all classes can use a skill
  - `IsSkillAvailable()` - Check if skill is available to creature
  - `GetUnavailableSkillIds()` - Get set of unavailable skill IDs

---

## [0.1.7-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/classes-levels-panel` | **PR**: #625

### Sprint: Classes & Levels Panel (#613)

Add the Classes & Levels panel displaying character class progression and alignment.

#### Added
- **Class Slots Display** - Shows active classes only (up to 8 per Beamdog EE)
  - Each class shows name, level, hit die, and skill points per level
  - Key class features displayed (e.g., "Rage, Fast Movement" for Barbarian)
  - "Add Class" placeholder button for future editing
  - Level +/- buttons as disabled placeholders for future editing
  - Total level calculation displayed
- **Alignment Display** - Shows character alignment with visual progress bars
  - Good-Evil axis with numeric value (0-100)
  - Lawful-Chaotic axis with numeric value (0-100)
  - Alignment name calculated from axis values (e.g., "Lawful Good", "True Neutral")
- **Auto-Levelup Package Display** - Shows the creature's StartingPackage
  - Package name resolved from packages.2da (hardcoded fallbacks)
- **Levelup Wizard Placeholder** - Disabled button for future levelup wizard sprint
- **Identity Section** - Race, gender, subrace, and deity display
- **CreatureDisplayService Integration** - ClassesPanel now uses 2DA/TLK lookups when available

#### Changed
- ClassesPanel uses CreatureDisplayService for class/race/gender name resolution
- Hit die display per class (d4-d12 based on class type)

---

## [0.1.6-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/stats-identity-panel` | **PR**: #622

### Sprint: Character Stats & Identity Panel (#612)

Add the Stats & Identity panel displaying character name, portrait, and core statistics.

#### Added
- **CreatureDisplayService** - Centralized service for resolving creature names from 2DA/TLK
  - Race name lookup from `racialtypes.2da` with TLK resolution
  - Gender name lookup from `gender.2da` with TLK resolution
  - Class name lookup from `classes.2da` with TLK resolution
  - Racial ability modifier lookup (StrAdjust, DexAdjust, etc.)
  - Hardcoded fallbacks for common races/genders/classes when 2DA not available
- **Base Attack Bonus calculation** from class levels
  - Reads `AttackBonusTable` from `classes.2da` for each class
  - Looks up BAB from `cls_atk_*.2da` at appropriate level
  - Sums BAB across multiclass levels
  - Falls back to estimated progression (full/3/4/half) when 2DA unavailable
- **Equipment attack bonus** from equipped items
  - Scans item properties for Enhancement Bonus (PropertyName 6) and Attack Bonus (PropertyName 56)
  - Enhancement bonuses don't stack (highest wins)
  - Attack bonuses stack
- **Feat name resolution** - FeatsPanel now uses `feat.2da` + TLK for feat names
  - High feat IDs (e.g., 1089) now resolve correctly instead of showing "Feat 1089"
  - Spell names for special abilities resolved via `spells.2da` + TLK
- **Navigation FlaUI tests** - Sidebar nav button tests for all 8 panels
- **Enhanced StatsPanel** - Complete overhaul with detailed stat breakdown
  - Ability scores: Base | Racial Modifier | Total | Bonus columns
  - Hit points: Base HP (dice rolls) | Max HP (with Con) | Current HP (with %)
  - Combat stats: Natural AC | Base Attack (with breakdown) | Speed | Challenge Rating
  - Saving throws: Base | Ability Modifier | Total columns
- **Sidebar character header** - Now shows resolved race/gender/class names via 2DA/TLK lookup

#### Fixed
- **Walk rate display** - Fixed creaturespeed.2da mapping (was off-by-one)
  - Row 0 = PC, Row 4 = Normal, etc. (not Row 0 = Immobile)

#### Changed
- MainWindow now uses `CreatureDisplayService` for character summary in sidebar
- MainWindow passes equipped items to StatsPanel for BAB calculation
- StatsPanel receives display service via `SetDisplayService()` method
- StatsPanel shows BAB breakdown: "(X base + Y equip)" when equipment contributes

---

## [0.1.5-alpha] - 2025-12-28
**Branch**: `quartermaster/sprint/app-shell-layout` | **PR**: #619

### Sprint: Application Shell & Layout Foundation (#618)

Establishes the Quartermaster application shell with a FlaUI-friendly sidebar + content layout that avoids tab controls.

#### Added
- **Sidebar navigation layout** - Replaced tab-based layout with sidebar + content pattern for FlaUI compatibility
- **Character header** - Portrait placeholder, character name, and race/class summary in sidebar
- **Navigation buttons** - Stats, Classes, Skills, Feats, Spells, Inventory, Advanced, Scripts sections
- **Content panel switching** - Single content area that swaps UserControls based on navigation selection
- **StatsPanel** - Basic stats display showing ability scores, combat stats, and saving throws
- **InventoryPanel** - Extracted inventory UI into standalone UserControl with equipment slots, backpack, and palette
- **PlaceholderPanel** - "Coming Soon" panels for unimplemented sections
- **AutomationIds** - FlaUI-friendly identifiers on all interactive elements (NavButton_*, ContentArea, CharacterName, etc.)
- **SidebarWidth setting** - Persisted sidebar width preference

#### Changed
- MainWindow layout refactored from 3-panel inventory view to sidebar + content pattern
- Settings now saves sidebar width instead of left panel width

---

## [0.1.4-alpha] - 2025-12-27
**Branch**: `quartermaster/fix/flaui-closes-vscode` | **PR**: #606

### Fix: FlaUI tests close VSCode instead of just the app (#593)

#### Fixed
- **StopApplication() window targeting** - Changed from `Alt+F4` keystroke to `App.Close()` method
  - Alt+F4 sends to focused window, which could be VSCode if test app lost focus
  - `App.Close()` sends WM_CLOSE directly to test process, ensuring correct targeting
  - Kept 200ms delay before close to prevent SkiaSharp render crashes

---

## [0.1.3-alpha] - 2025-12-26
**Branch**: `quartermaster/feat/inventory-display-580` | **PR**: #585

### Feature: Wire up Inventory Display and File Search (#580)

#### Added
- **Item palette population** - Loads items from module directory, Override folder, and BIF archives
- **Item filtering** - Filter panel wired to GameDataService for item type dropdown
- **GitHub workflows** - `quartermaster-pr-build.yml` and `quartermaster-pr-tests.yml`
- **External branding** - Window title, About dialog, CLI help, menus use "Quartermaster"
- **Equipment slot display** - Equipped items from BIC files now display in slots with name labels
- **Placeholder icons** - SVG icons from game-icons.net (CC BY 3.0) for equipment slots and item lists
- **ItemIconHelper** - Maps equipment slots and item types to placeholder icons

#### Changed
- PaletteList now displays filtered items from ItemFilterPanel
- ClearInventoryUI clears palette items and selection state
- Equipment slot control now shows slot name on top, item name on bottom
- Item lists (palette, backpack) now show item type icons

#### Fixed
- **BIC equipped items** - Fixed parsing of `EquippedRes` field (was using wrong field name)
- **BIC inventory items** - Fixed parsing of `InventoryRes` field for backpack items

#### Infrastructure
- Workflow parity with Parley for PR checks and CI
- Internal namespace remains `CreatureEditor` (like Parley/DialogEditor pattern)
- Added Avalonia.Svg.Skia package for SVG icon support

---

## [0.1.2-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-cleanup` | **PR**: #584

### Sprint: CreatureEditor Cleanup (#582, #583)

Pre-emptive refactoring before codebase grows. Lessons from Parley's 2,400+ line MainWindow.

#### Refactoring (#582)
- **MainWindow.FileOps.cs** - Extracted file operations (Open/Save/Recent files)
- **MainWindow.Inventory.cs** - Extracted inventory population and UTI resolution
- **DialogHelper.cs** - Static helper for common dialogs (Unsaved/Error/About)
- MainWindow.axaml.cs reduced from 892 to 466 lines (48% reduction)

#### Testing (#583)
- **CreatureEditor.Tests project** - New xUnit test project (21 tests)
- **CommandLineServiceTests** - 9 tests for argument parsing
- **SettingsServiceTests** - 12 tests for property constraints and defaults
- FlaUI integration smoke tests added to Radoub.IntegrationTests

---

## [0.1.1-alpha] - 2025-12-26
**Branch**: `radoub/feat/uti-bif-loading` | **PR**: #581

### Feature: Load UTI Items from BIF Archives (#579)

#### Added

## [0.1.0-alpha] - 2025-12-26
**Branch**: `radoub/sprint/creature-editor-mvp` | **PR**: #578

### Sprint: Creature Editor MVP - Inventory Panel (#554)

Initial release of Quartermaster (Creature Editor).

#### Added
