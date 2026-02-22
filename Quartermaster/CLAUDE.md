# CLAUDE.md - Quartermaster

Tool-specific guidance for Claude Code sessions working with Quartermaster.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Tool Overview

**Quartermaster** is a creature and inventory editor for Neverwinter Nights. It edits UTC (creature blueprint) and BIC (player character) files.

**Current Version**: See `CHANGELOG.md` for latest version

### Core Features

- Edit creature stats, abilities, skills, feats, spells, inventory
- New Character Wizard with full 10-step character creation workflow
- Level Up Wizard for class progression
- Appearance preview rendering (OpenGL)
- Load items from module directory, Override, HAK, and BIF archives
- Support for both UTC (creature blueprints) and BIC (player characters)
- Sidebar navigation with Stats, Classes, Skills, Feats, Spells, Inventory, Advanced, Appearance, Scripts sections
- Creature browser for module-level file management

### New Character Wizard (10 Steps)

1. **File Type** - UTC/BIC selection with save location picker
2. **Race & Sex** - Searchable race list with racial info panel (modifiers, favored class, size, description)
3. **Appearance** - Searchable appearance list, phenotype, portrait browser, body parts, colors (skin/hair/tattoo)
4. **Class & Package** - Class selection with detail panel, package defaults, cleric domains, familiar selection
5. **Abilities** - Point-buy allocation with racial modifiers
6. **Feats** - Available/selected feat lists with prereq checking, auto-assign from package prefs, feat descriptions
7. **Alignment** - Good/Evil and Law/Chaos axes
8. **Skills** - Skill point allocation with class/cross-class costs
9. **Spells** - Spell selection for caster classes (arcane known spells, divine auto-grant)
10. **Equipment & Summary** - Package equipment loading, character name/tag/resref, faction, palette ID

Key wizard patterns:
- All game data sourced from 2DA files (no hardcoded race/class/feat data)
- Package-based defaults for skills, feats, spells, equipment
- Custom factions loaded from module's `repute.fac`
- Familiar support for Wizard/Sorcerer via `hen_familiar.2da`
- Equipment loaded from `packeq*.2da` referenced by `packages.2da`

---

## Project Structure

```
Quartermaster/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── Quartermaster/ (source code)
│   ├── App.axaml(.cs) - Application entry, theme/icon setup
│   ├── Program.cs - Entry point, logging init
│   ├── Controls/
│   │   └── ModelPreviewGLControl.cs - OpenGL 3D model preview
│   ├── ViewModels/
│   │   ├── BindableBase.cs - MVVM property binding base
│   │   ├── RelayCommand.cs - ICommand implementation
│   │   ├── FeatListViewModel.cs
│   │   ├── SpellListViewModel.cs
│   │   ├── SpecialAbilityViewModel.cs
│   │   └── VariableViewModel.cs
│   ├── Services/ (18 files)
│   │   ├── CommandLineService.cs - CLI argument parsing
│   │   ├── SettingsService.cs - User preferences
│   │   ├── CreatureDisplayService.cs - Creature display (partial)
│   │   ├── CreatureDisplayService.Combat.cs - Combat stats partial
│   │   ├── CharacterSheetService.cs - Character sheet calculations
│   │   ├── ClassService.cs - NWN class data
│   │   ├── FeatService.cs - Feat lookup and validation
│   │   ├── FeatCacheService.cs - Feat data caching
│   │   ├── SkillService.cs - Skill calculations
│   │   ├── SpellService.cs - Spell lookup
│   │   ├── AppearanceService.cs - Appearance/color data
│   │   ├── ModelService.cs - 3D model loading
│   │   ├── TextureService.cs - Texture loading and caching
│   │   ├── ItemIconService.cs - Item icon management
│   │   ├── LevelHistoryService.cs - Level/class progression
│   │   ├── ModularPaletteCacheService.cs - Multi-source item caching
│   │   ├── PaletteColorService.cs - Palette color utilities
│   │   └── QuartermasterScriptBrowserContext.cs - Script browser
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs) - Main window (9 partial files)
│   │   ├── MainWindow.CreatureBrowser.cs
│   │   ├── MainWindow.FileOps.cs
│   │   ├── MainWindow.FileValidation.cs
│   │   ├── MainWindow.Inventory.cs
│   │   ├── MainWindow.ItemPalette.cs
│   │   ├── MainWindow.ItemResolution.cs
│   │   ├── MainWindow.Lifecycle.cs
│   │   ├── MainWindow.MenuDialogs.cs
│   │   ├── PortraitBrowserWindow.axaml(.cs)
│   │   ├── SoundsetBrowserWindow.axaml(.cs)
│   │   ├── Helpers/
│   │   │   └── DialogHelper.cs
│   │   ├── Dialogs/
│   │   │   ├── SettingsWindow.axaml(.cs) - Settings (2 partials)
│   │   │   ├── SettingsWindow.Paths.cs
│   │   │   ├── NewCharacterWizardWindow.axaml(.cs) - Wizard (9 partials)
│   │   │   ├── NewCharacterWizardWindow.Race.cs
│   │   │   ├── NewCharacterWizardWindow.Appearance.cs
│   │   │   ├── NewCharacterWizardWindow.ClassSelection.cs
│   │   │   ├── NewCharacterWizardWindow.Abilities.cs
│   │   │   ├── NewCharacterWizardWindow.Skills.cs
│   │   │   ├── NewCharacterWizardWindow.Feats.cs
│   │   │   ├── NewCharacterWizardWindow.EquipmentAndSummary.cs
│   │   │   ├── NewCharacterWizardWindow.BuildCreature.cs
│   │   │   ├── LevelUpWizardWindow.axaml(.cs)
│   │   │   ├── ClassBrowserWindow.axaml(.cs)
│   │   │   ├── ClassPickerWindow.axaml(.cs)
│   │   │   ├── ColorPickerWindow.axaml(.cs)
│   │   │   ├── FactionPickerWindow.axaml(.cs)
│   │   │   ├── PackagePickerWindow.axaml(.cs)
│   │   │   └── SpellPickerWindow.axaml(.cs)
│   │   └── Panels/
│   │       ├── BasePanelControl.cs - Base class for panels
│   │       ├── ComboBoxHelper.cs - ComboBox utilities
│   │       ├── StatsPanel.axaml(.cs) - Ability scores, combat, saves (4 partials)
│   │       ├── StatsPanel.Abilities.cs
│   │       ├── StatsPanel.Combat.cs
│   │       ├── StatsPanel.Saves.cs
│   │       ├── ClassesPanel.axaml(.cs) - Class levels, alignment
│   │       ├── SkillsPanel.axaml(.cs) - Skill ranks
│   │       ├── FeatsPanel.axaml(.cs) - Feats (5 partials)
│   │       ├── FeatsPanel.Display.cs
│   │       ├── FeatsPanel.Search.cs
│   │       ├── FeatsPanel.Selection.cs
│   │       ├── FeatsPanel.SpecialAbilities.cs
│   │       ├── SpellsPanel.axaml(.cs) - Spells (4 partials)
│   │       ├── SpellsPanel.DataLoading.cs
│   │       ├── SpellsPanel.EventHandlers.cs
│   │       ├── SpellsPanel.SummaryBuilders.cs
│   │       ├── InventoryPanel.axaml(.cs) - Equipment and backpack
│   │       ├── ScriptsPanel.axaml(.cs) - Event scripts
│   │       ├── AdvancedPanel.axaml(.cs) - Flags, behavior, variables
│   │       ├── AppearancePanel.axaml(.cs) - Visual preview (3 partials)
│   │       ├── AppearancePanel.DataLoading.cs
│   │       ├── AppearancePanel.EventHandlers.cs
│   │       ├── CharacterPanel.axaml(.cs) - Name, portrait, soundset (3 partials)
│   │       ├── CharacterPanel.Dialogs.cs
│   │       ├── CharacterPanel.Soundset.cs
│   │       ├── QuickBarPanel.axaml(.cs) - Quick access bar
│   │       └── PlaceholderPanel.axaml(.cs) - Placeholder
│   └── Assets/
└── Quartermaster.Tests/ (unit tests)
    ├── CommandLineServiceTests.cs
    └── SettingsServiceTests.cs
```

---

## Partial Class Organization

Quartermaster uses C# partial classes extensively to keep files manageable (~500 lines max).

### Naming Convention

`ClassName.Concern.cs` - e.g., `MainWindow.FileOps.cs`, `CharacterPanel.Soundset.cs`

### Partial Class Map

| Class | Files | Total Lines |
|-------|-------|-------------|
| **MainWindow** | 9 partials | ~3,392 |
| **NewCharacterWizardWindow** | 9 partials | ~3,572 |
| **SettingsWindow** | 2 partials | ~805 |
| **CreatureDisplayService** | 2 partials | ~927 |
| **FeatsPanel** | 5 partials | ~1,109 |
| **SpellsPanel** | 4 partials | ~1,810 |
| **StatsPanel** | 4 partials | ~880 |
| **AppearancePanel** | 3 partials | ~921 |
| **CharacterPanel** | 3 partials | ~959 |

### MainWindow Partials

| File | Lines | Purpose |
|------|-------|---------|
| MainWindow.axaml.cs | 742 | Core: fields, constructor, panels, navigation, edit ops, UI updates, keyboard shortcuts |
| MainWindow.CreatureBrowser.cs | 382 | Creature browser panel init, visibility, file selection, delete |
| MainWindow.FileOps.cs | 713 | Recent files, menu handlers, export, open/save/new/close |
| MainWindow.FileValidation.cs | 207 | Aurora filename validation, BIC class validation, rename workflow |
| MainWindow.Inventory.cs | 506 | Populate inventory from creature data |
| MainWindow.ItemPalette.cs | 420 | Item palette with modular caching (BIF/Override/HAK) |
| MainWindow.ItemResolution.cs | 157 | UTI resolution from module/Override/HAK/BIF |
| MainWindow.Lifecycle.cs | 216 | Window opened, service init, caches, startup file, closing |
| MainWindow.MenuDialogs.cs | 249 | Settings, About, Level Up, Re-Level, Down-Level dialogs |

### NewCharacterWizardWindow Partials

| File | Lines | Purpose |
|------|-------|---------|
| .axaml.cs | ~900 | Core: wizard navigation, step management, save location, factions |
| .Race.cs | ~170 | Step 2: Race selection with info panel |
| .Appearance.cs | ~270 | Step 3: Appearance, phenotype, portrait, colors |
| .ClassSelection.cs | ~500 | Step 4: Class, package, domains, familiar |
| .Abilities.cs | ~400 | Step 5: Point-buy ability allocation |
| .Feats.cs | ~420 | Step 6: Feat selection with prereqs and descriptions |
| .Skills.cs | ~350 | Step 8: Skill assignment |
| .EquipmentAndSummary.cs | ~480 | Steps 9-10: Equipment and summary/finalization |
| .BuildCreature.cs | ~450 | Character build and GFF field population |

### When to Split

Split a file when it exceeds ~500 lines. Group by functional concern:
- **Data loading** - reading from services/files
- **Event handlers** - user interaction responses
- **Validation** - input/output checking
- **Display/Summary** - building display strings

Each partial file needs its own `using` statements - they are NOT shared across partials.

---

## Content Panels

Each section in the sidebar has its own UserControl in `Views/Panels/`:

| Panel | Partials | Status |
|-------|----------|--------|
| StatsPanel | 4 files | Editable - ability scores, HP, saves, combat |
| ClassesPanel | 1 file | Editable - class levels, alignment, identity |
| SkillsPanel | 1 file | Editable - skill ranks with progress bars |
| FeatsPanel | 5 files | Editable - add/remove feats and special abilities |
| SpellsPanel | 4 files | Editable - known/memorized spells with metamagic |
| ScriptsPanel | 1 file | Editable - event scripts and conversation resref |
| AdvancedPanel | 1 file | Editable - flags, behavior, appearance, variables |
| AppearancePanel | 3 files | View - appearance preview rendering |
| InventoryPanel | 1 file | View - equipment + backpack + palette |
| CharacterPanel | 3 files | Editable - name, portrait, soundset |
| QuickBarPanel | 1 file | Quick access bar |

To add a new panel:
1. Create UserControl in `Views/Panels/`
2. Add to MainWindow.axaml content grid
3. Add navigation in `NavigateToSection()`
4. Add AutomationId for FlaUI testing

---

## Services

18 services in the `Services/` directory:

| Service | Lines | Purpose |
|---------|-------|---------|
| CommandLineService | 37 | CLI argument parsing (--file, --safemode) |
| SettingsService | 471 | User preferences, singleton with env var override |
| CreatureDisplayService | 655+272 | Creature display state + combat stats (2 partials) |
| CharacterSheetService | 773 | Character sheet calculations |
| ClassService | 873 | NWN class data, levels, abilities |
| FeatService | 920 | Feat lookup and validation |
| FeatCacheService | 178 | Feat data caching |
| SkillService | 262 | Skill calculations |
| SpellService | 339 | Spell lookup |
| AppearanceService | 551 | Appearance/color data |
| ModelService | 554 | 3D model loading |
| TextureService | 303 | Texture loading and caching |
| ItemIconService | 251 | Item icon management |
| LevelHistoryService | 503 | Level/class progression tracking |
| ModularPaletteCacheService | 390 | Multi-source item caching (BIF/Override/HAK) |
| PaletteColorService | 153 | Palette color utilities |
| QuartermasterScriptBrowserContext | 111 | Script browser integration |

Service patterns:
- **Singleton** with `Instance` property
- **Environment variable override** for testing (e.g., `QUARTERMASTER_SETTINGS_DIR`)
- **INotifyPropertyChanged** for bindable settings

---

## Item Resolution

Items are resolved in this order:
1. **Module directory** - Same folder as the UTC/BIC file
2. **Override folder** - User's NWN Override directory
3. **HAK files** - Module-specific HAK archives
4. **BIF archives** - Base game data

---

## Testing

### Unit Tests

```bash
dotnet test Quartermaster/Quartermaster.Tests
```

21+ tests covering:
- CommandLineService argument parsing
- SettingsService property constraints

### Integration Tests

FlaUI smoke tests in `Radoub.IntegrationTests/Quartermaster/`:

```bash
dotnet test Radoub.IntegrationTests --filter "Category=Smoke&FullyQualifiedName~Quartermaster"
```

---

## Shared Dependencies

| Library | Purpose |
|---------|---------|
| Radoub.Formats | UTC, BIC, UTI, IFO file parsing, GameDataService |
| Radoub.UI | ItemListView, EquipmentSlotsPanel, ItemFilterPanel, CreatureBrowserWindow, ThemeManager, BrushManager, AboutWindow |
| Radoub.Dictionary | (not yet integrated - future spell-check for text fields) |

---

## Commit Conventions

Use `[Quartermaster]` prefix:

```bash
[Quartermaster] feat: Add appearance preview (#746)
[Quartermaster] fix: Correct BIC save corruption (#XXX)
```

Changes go in `Quartermaster/CHANGELOG.md` (not Radoub CHANGELOG).

---

## FlaUI Testing

The sidebar layout is designed for FlaUI compatibility:

| Element | AutomationId | Test Action |
|---------|--------------|-------------|
| Stats Button | `NavButton_Stats` | Click -> verify StatsPanel visible |
| Inventory Button | `NavButton_Inventory` | Click -> verify InventoryPanel visible |
| Character Name | `CharacterName` | Verify text after file load |
| Content Area | `ContentArea` | Verify active panel |

Navigation buttons are NOT tabs - they're styled buttons with `.NavButton` class.

---

## Resources

- [Quartermaster CHANGELOG](CHANGELOG.md)
- [UTC Format Spec](https://github.com/LordOfMyatar/Radoub/wiki)
- [BIC Format Spec](https://github.com/LordOfMyatar/Radoub/wiki)

---
