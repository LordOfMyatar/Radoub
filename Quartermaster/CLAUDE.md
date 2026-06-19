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

### New Character Wizard (11 Steps)

1. **File Type** - UTC/BIC selection with save location picker
2. **Race & Sex** - Searchable race list with racial info panel (modifiers, favored class, size, description)
3. **Identity** - Name, portrait, voice set, age, description, filename validation, UTC palette/faction
4. **Appearance** - Searchable appearance list, phenotype, body parts, colors (skin/hair/tattoo)
5. **Class & Package** - Class selection with detail panel, package defaults, cleric domains, familiar selection
6. **Abilities** - Point-buy allocation with racial modifiers
7. **Feats** - Available/selected feat lists with prereq checking, auto-assign from package prefs, feat descriptions
8. **Skills** - Skill point allocation with class/cross-class costs
9. **Spells** - Spell selection for caster classes (arcane known spells, divine auto-grant)
10. **Equipment** - Package equipment loading
11. **Summary** - Read-only review of all selections with [Edit] buttons to jump back

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
│   ├── ViewModels/
│   │   ├── BindableBase.cs - MVVM property binding base
│   │   ├── RelayCommand.cs - ICommand implementation
│   │   ├── FeatListViewModel.cs
│   │   ├── SpellListViewModel.cs
│   │   ├── SpecialAbilityViewModel.cs
│   │   └── VariableViewModel.cs
│   ├── Services/ (~27 files; see Services table below)
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
│   │   │   ├── NewCharacterWizardWindow.axaml(.cs) - Wizard (13 partials)
│   │   │   ├── NewCharacterWizardWindow.FileType.cs
│   │   │   ├── NewCharacterWizardWindow.Race.cs
│   │   │   ├── NewCharacterWizardWindow.Identity.cs
│   │   │   ├── NewCharacterWizardWindow.Appearance.cs
│   │   │   ├── NewCharacterWizardWindow.ClassSelection.cs
│   │   │   ├── NewCharacterWizardWindow.Abilities.cs
│   │   │   ├── NewCharacterWizardWindow.Skills.cs
│   │   │   ├── NewCharacterWizardWindow.Feats.cs
│   │   │   ├── NewCharacterWizardWindow.Spells.cs
│   │   │   ├── NewCharacterWizardWindow.EquipmentAndSummary.cs
│   │   │   ├── NewCharacterWizardWindow.BuildCreature.cs
│   │   │   ├── NewCharacterWizardWindow.Navigation.cs
│   │   │   ├── LevelUpWizardWindow.axaml(.cs)
│   │   │   ├── ClassBrowserWindow.axaml(.cs)
│   │   │   ├── ClassPickerWindow.axaml(.cs)
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
└── Quartermaster.Tests/ (43 unit-test files — services, wizard logic, round-trip,
                          appearance/HAK-merge, feat/skill/spell, level history)
```

---

## Partial Class Organization

Quartermaster uses C# partial classes extensively to keep files manageable (~500 lines max).

### Naming Convention

`ClassName.Concern.cs` - e.g., `MainWindow.FileOps.cs`, `CharacterPanel.Soundset.cs`

### Partial Class Map

| Class | Files | Total Lines |
|-------|-------|-------------|
| **MainWindow** | 9 partials | ~4,404 |
| **NewCharacterWizardWindow** | 13 partials | ~4,388 |
| **SettingsWindow** | 2 partials | ~805 |
| **CreatureDisplayService** | 2 partials | ~927 |
| **FeatsPanel** | 5 partials | ~1,109 |
| **SpellsPanel** | 4 partials | ~1,810 |
| **StatsPanel** | 4 partials | ~880 |
| **AppearancePanel** | 3 partials | ~921 |
| **CharacterPanel** | 3 partials | ~959 |

Line counts drift between commits; numbers above are approximate. Confirm with `wc -l` before relying on them.

### MainWindow Partials

| File | Lines | Purpose |
|------|-------|---------|
| MainWindow.axaml.cs | 918 | Core: fields, constructor, panels, navigation, edit ops, UI updates, keyboard shortcuts |
| MainWindow.CreatureBrowser.cs | 501 | Creature browser panel init, visibility, file selection, delete |
| MainWindow.FileOps.cs | 741 | Recent files, menu handlers, export, open/save/new/close |
| MainWindow.FileValidation.cs | 223 | Aurora filename validation, BIC class validation, rename workflow |
| MainWindow.Inventory.cs | 602 | Populate inventory from creature data |
| MainWindow.ItemPalette.cs | 639 | Item palette with modular caching (BIF/Override/HAK) |
| MainWindow.ItemResolution.cs | 157 | UTI resolution from module/Override/HAK/BIF |
| MainWindow.Lifecycle.cs | 225 | Window opened, service init, caches, startup file, closing |
| MainWindow.MenuDialogs.cs | 398 | Settings, About, Level Up, Re-Level, Down-Level dialogs |

### NewCharacterWizardWindow Partials

| File | Lines | Purpose |
|------|-------|---------|
| .axaml.cs | ~695 | Core: wizard chrome, save location, factions |
| .FileType.cs | ~131 | Step 1: UTC/BIC selection with save location picker |
| .Race.cs | ~160 | Step 2: Race selection with info panel |
| .Identity.cs | ~119 | Step 3: Identity — name, portrait, voice set, filename validation |
| .Appearance.cs | ~219 | Step 4: Appearance, phenotype, body parts, colors |
| .ClassSelection.cs | ~719 | Step 5: Class, package, domains, familiar |
| .Abilities.cs | ~425 | Step 6: Point-buy ability allocation |
| .Feats.cs | ~362 | Step 7: Feat selection with prereqs and descriptions |
| .Skills.cs | ~298 | Step 8: Skill assignment |
| .Spells.cs | ~454 | Step 9: Spell selection for caster classes |
| .EquipmentAndSummary.cs | ~418 | Steps 10-11: Equipment and summary review |
| .BuildCreature.cs | ~80 | Build orchestration delegating to CharacterCreationService |
| .Navigation.cs | ~308 | Wizard step navigation, validation, next/prev wiring |
| .EquipmentAndSummary.cs | ~400 | Steps 10-11: Equipment and summary review |
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

~27 service files in the `Services/` directory (including partials):

| Service | Purpose |
|---------|---------|
| AbilityPointBuyService | D&D 3.5e point-buy ability allocation |
| AppearanceFilterHelper | Appearance filtering utilities |
| AppearanceService | Appearance/color data from appearance.2da |
| CharacterCreationService | Character creation logic (extracted from wizard) |
| CharacterSheetService | Character sheet calculations |
| ClassService | NWN class data, levels, abilities |
| CommandLineService | CLI argument parsing (--file, --mod) |
| CreatureDisplayService | Creature display state + combat stats (2 partials) |
| DomainService | Domain info from domains.2da |
| FeatCacheService | Feat data caching |
| FeatService | Feat lookup and validation (+ LevelUp, Prerequisites, Subtypes partials) |
| GameDataWarnOnce | One-shot logger for missing-2DA warnings |
| LevelHistoryService | Level/class progression tracking |
| LevelUpApplicationService | Level-up application (extracted from wizard) |
| ModelService | 3D model loading |
| PathSafety | Path traversal validation helpers |
| QuartermasterScriptBrowserContext | Script browser integration |
| ScriptTemplateService | Script templates for events |
| SettingsService | User preferences, singleton with env var override |
| SkillDisplayHelper | Skill UI helpers |
| SkillService | Skill calculations |
| SpellService | Spell lookup |
| ValidationLevel | Validation helper enum |

**Moved to shared libraries (do NOT add to `Quartermaster/Services/`)**:

| Service | Now lives in |
|---------|--------------|
| ItemIconService | `Radoub.UI/Services/` |
| TextureService | `Radoub.UI/Services/` |
| PaletteColorService | `Radoub.UI/Services/` |
| ModelPreviewGLControl | `Radoub.UI/Controls/` |
| SharedPaletteCacheService | `Radoub.UI/Services/` (replaces the old `ModularPaletteCacheService`) |

Service patterns:
- **Singleton** for IO-fronting services (SettingsService, FeatCacheService) — `Instance` property
- **Stateless static** for pure helpers (AppearanceFilterHelper, PathSafety, SkillDisplayHelper)
- **Per-instance** for orchestration services (CharacterCreationService, LevelUpApplicationService) — constructed where used
- **Environment variable override** for testing (e.g., `QUARTERMASTER_SETTINGS_DIR`)
- **INotifyPropertyChanged** for bindable singletons

---

## Item Resolution

Items are resolved in this order:
1. **Module directory** - Same folder as the UTC/BIC file
2. **Override folder** - User's NWN Override directory
3. **HAK files** - Module-specific HAK archives
4. **BIF archives** - Base game data

---

## Model Preview & Textures

The 3D appearance preview lives in shared `Radoub.UI` (`ModelPreviewGLControl`,
`TextureService`, `MeshVisibility` — all in `Radoub.UI/Controls` + `Radoub.UI/Services`),
not in Quartermaster. Edit there for rendering bugs.

**Mesh visibility (#2498)**: the preview gates on the MDL `Render` flag + non-empty geometry
only (`MeshVisibility.ShouldRender`), matching the Aurora engine (nwnexplorer/borealis). The old
30-vertex `MeshSkipHeuristic` (#1676/#2057) was removed — it hid real parts that reuse the body
texture (hands, hair, dragon spikes, tongues). Do not reintroduce a vertex-count/bitmap skip.

**PBR texture resolution (#1755, #1760)**: NWN:EE creature skins resolve textures two
ways — (1) an `.mtr` material file named by the mesh's `materialname`, or (2) a fixed
suffix convention where the bare `<name>` has companion maps `<name>_d` (diffuse),
`_n` (normal), `_r` (roughness), `_i` (illum). MDL meshes reference the bare name;
`TextureService.LoadTextureWithKind` falls back to `<name>_d` when the bare name misses.
CEP3 creatures ported from NWN2 (Txpple beetles, CEP `una` spiders) rely on this — without
the fallback they render **white**.

**Known gap**: the binary MDL reader declares `MaterialName` but never reads it, and `.mtr`
files (resource type 3007) are not parsed. MTR-driven creatures whose diffuse has a non-`_d`
name still render white. If a white-model bug isn't explained by the `_d` convention, suspect MTR.

---

## Testing

### Unit Tests

```bash
dotnet test Quartermaster/Quartermaster.Tests
```

43 test files covering:
- AbilityPointBuyService, AlignmentRestriction, Appearance (analysis/filter/service/HAK-merge)
- CharacterCreation, CharacterSheet, ClassAlignment, ClassDomain, Combat
- CreatureDisplay, Domain, FeatCache, FeatService (4 variants + prereq-override + subtype)
- LevelHistory, LevelUpApplication, LevelUpSkillDisplay, Metamagic
- ModelNameConstruction, NcwHardening, PaletteColor, PltColorIndices
- PrestigePrerequisite, RoundTripValidation, ScriptBrowserContext, PortraitBrowserContext
- ScriptTemplate, SkillService, SpellService, CommandLineService, SettingsService (+ bool persistence)
- WizardDisplayItem, PathSafety, GameDataWarnOnce

### Integration Tests

FlaUI smoke tests in `Radoub.IntegrationTests/Quartermaster/`:

```bash
dotnet test Radoub.IntegrationTests --filter "Category=Smoke&FullyQualifiedName~Quartermaster"
```

### Manual Model-Preview Fixtures

To eyeball a specific appearance, clone Bucky.utc with a swapped `Appearance_Type` into
LNS_DLG (PS7 required — loads the net9.0 Radoub.Formats.dll; do NOT use Windows PowerShell 5.1
or the WindowsApps `pwsh` stub):

```bash
& "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
  -File ".claude/scripts/New-AppearanceTestUtc.ps1" -Appearances a4=159,a5=3951
```

Open the generated `aN.utc` in QM → Appearance panel to verify rendering.

### Read-Only Model Investigations

For one-off, **read-only** MDL/texture investigations (e.g. replaying `MeshSkipHeuristic`
across all creature models), use the shared scratch slots
`.claude/scripts/Scratch-Investigate-1.ps1` / `-2.ps1` — edit one in place rather than
creating a new script. They are investigation-only and must never mutate files; see the root
CLAUDE.md "Scratch Investigation Scripts" section. `Dump-MdlMeshes.ps1 -Model <resref>` is the
single-model version.

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
