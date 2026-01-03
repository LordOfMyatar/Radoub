# Changelog - Quartermaster

All notable changes to Quartermaster (Creature Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

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
