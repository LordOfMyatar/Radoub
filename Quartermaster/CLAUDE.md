# CLAUDE.md - Quartermaster

Tool-specific guidance for Claude Code sessions working with Quartermaster.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Tool Overview

**Quartermaster** is a creature and inventory editor for Neverwinter Nights. It edits UTC (creature blueprint) and BIC (player character) files.

### Core Features

- View and edit creature inventory (equipment + backpack)
- Load items from module directory, Override, HAK, and BIF archives
- Support for both UTC (creature blueprints) and BIC (player characters)
- Sidebar navigation with Stats, Classes, Skills, Feats, Spells, Inventory, Advanced, Scripts sections

### Project Structure

```
Quartermaster/
├── CHANGELOG.md (this tool's changelog)
├── CLAUDE.md (this file)
├── Quartermaster/ (source code)
│   ├── App.axaml(.cs) - Application entry point
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs) - Main window (partial classes below)
│   │   ├── MainWindow.FileOps.cs - File operations partial
│   │   ├── MainWindow.Inventory.cs - Inventory population partial
│   │   ├── Helpers/
│   │   │   └── DialogHelper.cs - Common dialog helper
│   │   └── Panels/
│   │       ├── StatsPanel.axaml(.cs) - Character stats display
│   │       ├── InventoryPanel.axaml(.cs) - Equipment and backpack
│   │       └── PlaceholderPanel.axaml(.cs) - Coming soon placeholders
│   └── Services/
│       ├── CommandLineService.cs - CLI argument parsing
│       ├── SettingsService.cs - User preferences
│       └── ThemeManager.cs - Theme switching
└── Quartermaster.Tests/ (unit tests)
    ├── CommandLineServiceTests.cs
    └── SettingsServiceTests.cs
```

---

## Development Patterns

### Partial Classes

MainWindow uses partial classes to keep code manageable:

| File | Purpose |
|------|---------|
| MainWindow.axaml.cs | Core window logic, navigation, event handlers |
| MainWindow.FileOps.cs | Open/Save/Recent files |
| MainWindow.Inventory.cs | Populate inventory from creature data |

When adding new functionality, consider if it belongs in an existing partial or warrants a new one.

### Content Panels

Each section in the sidebar has its own UserControl in `Views/Panels/`:

| Panel | Status |
|-------|--------|
| StatsPanel | Basic display (read-only) |
| InventoryPanel | Functional (equipment + backpack + palette) |
| PlaceholderPanel | "Coming Soon" for unimplemented sections |

To add a new panel:
1. Create UserControl in `Views/Panels/`
2. Add to MainWindow.axaml content grid
3. Add navigation in `NavigateToSection()`
4. Add AutomationId for FlaUI testing

### Services

All services follow similar patterns to Parley:

- **Singleton pattern** with `Instance` property
- **Environment variable override** for testing (e.g., `QUARTERMASTER_SETTINGS_DIR`)
- **INotifyPropertyChanged** for bindable settings

### Item Resolution

Items are resolved in this order:

1. **Module directory** - Same folder as the UTC/BIC file
2. **Override folder** - User's NWN Override directory
3. **HAK files** - Module-specific HAK archives
4. **BIF archives** - Base game data

Use `GameDataService` for steps 2-4. See `CreatePlaceholderItem()` in MainWindow.Inventory.cs.

---

## Testing

### Unit Tests

```bash
dotnet test Quartermaster/Quartermaster.Tests
```

21 tests covering:
- CommandLineService argument parsing
- SettingsService property constraints

### Integration Tests

FlaUI smoke tests in `Radoub.IntegrationTests/Quartermaster/`:

```bash
dotnet test Radoub.IntegrationTests --filter "Category=Smoke&FullyQualifiedName~Quartermaster"
```

---

## Shared Dependencies

Quartermaster uses these shared libraries:

| Library | Purpose |
|---------|---------|
| Radoub.Formats | UTC, BIC, UTI file parsing |
| Radoub.UI | ItemListView, EquipmentSlotsPanel, ItemFilterPanel |
| Radoub.Formats.Services | GameDataService for BIF/TLK access |

---

## Commit Prefixes

Use `[Quartermaster]` prefix for commits:

```
[Quartermaster] feat: Add stats editing (#XXX)
[Quartermaster] fix: Correct BIC save corruption (#XXX)
```

---

## Common Tasks

### Adding a New Panel

1. Create UserControl in `Views/Panels/`
2. Add to MainWindow.axaml content grid with `IsVisible="False"`
3. Add navigation button in sidebar with AutomationId
4. Update `NavigateToSection()` switch statement
5. Add tests if significant logic

### Adding a New Service

1. Create in `Services/` folder
2. Follow singleton pattern with environment variable override
3. Add tests in Quartermaster.Tests

### Working with Creature Data

```csharp
// Load creature
var creature = UtcReader.Read(filePath);  // or BicReader for .bic

// Access inventory
foreach (var item in creature.ItemList) { ... }
foreach (var equipped in creature.EquipItemList) { ... }

// Resolve item data
var utiData = _gameDataService.FindResource(resRef, ResourceTypes.Uti);
var item = UtiReader.Read(utiData);
```

---

## FlaUI Testing

The sidebar layout is designed for FlaUI compatibility:

| Element | AutomationId | Test Action |
|---------|--------------|-------------|
| Stats Button | `NavButton_Stats` | Click → verify StatsPanel visible |
| Inventory Button | `NavButton_Inventory` | Click → verify InventoryPanel visible |
| Character Name | `CharacterName` | Verify text after file load |
| Content Area | `ContentArea` | Verify active panel |

Navigation buttons are NOT tabs - they're styled buttons with `.NavButton` class.

---

## Known Limitations

Current alpha state:

- Stats panel is read-only
- Most panels show "Coming Soon" placeholder
- Item editing not implemented (view-only for inventory)
- No drag-drop between equipment and backpack

See Epic #544 for roadmap.

---
