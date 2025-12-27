# CLAUDE.md - Quartermaster (Creature Editor)

Tool-specific guidance for Claude Code sessions working with Quartermaster.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Tool Overview

**Quartermaster** is a creature and inventory editor for Neverwinter Nights. It edits UTC (creature blueprint) and BIC (player character) files.

### Core Features

- View and edit creature inventory (equipment + backpack)
- Load items from module directory, Override, HAK, and BIF archives
- Support for both UTC (creature blueprints) and BIC (player characters)

### Project Structure

```
CreatureEditor/
├── CHANGELOG.md (this tool's changelog)
├── CLAUDE.md (this file)
├── CreatureEditor/ (source code)
│   ├── App.axaml(.cs) - Application entry point
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs) - Main window (partial classes below)
│   │   ├── MainWindow.FileOps.cs - File operations partial
│   │   ├── MainWindow.Inventory.cs - Inventory population partial
│   │   └── Helpers/
│   │       └── DialogHelper.cs - Common dialog helper
│   └── Services/
│       ├── CommandLineService.cs - CLI argument parsing
│       ├── SettingsService.cs - User preferences
│       ├── ThemeManager.cs - Theme switching
│       └── UnifiedLogger.cs - Logging service
└── CreatureEditor.Tests/ (unit tests)
    ├── CommandLineServiceTests.cs
    └── SettingsServiceTests.cs
```

---

## Development Patterns

### Partial Classes

MainWindow uses partial classes to keep code manageable:

| File | Purpose |
|------|---------|
| MainWindow.axaml.cs | Core window logic, event handlers |
| MainWindow.FileOps.cs | Open/Save/Recent files |
| MainWindow.Inventory.cs | Populate inventory from creature data |

When adding new functionality, consider if it belongs in an existing partial or warrants a new one.

### Services

All services follow similar patterns to Parley:

- **Singleton pattern** with `Instance` property
- **Environment variable override** for testing (e.g., `CREATUREEDITOR_SETTINGS_DIR`)
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
dotnet test CreatureEditor/CreatureEditor.Tests
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

Use `[Radoub]` prefix for commits since CreatureEditor changes go in the Radoub CHANGELOG:

```
[Radoub] feat: Add item drag-drop to equipment slots (#XXX)
[Radoub] fix: Correct BIC save corruption (#XXX)
```

---

## Common Tasks

### Adding a New Panel

1. Create control in `Views/` or use shared control from Radoub.UI
2. Add to MainWindow.axaml layout
3. Wire up in MainWindow.axaml.cs or appropriate partial class
4. Add tests if significant logic

### Adding a New Service

1. Create in `Services/` folder
2. Follow singleton pattern with environment variable override
3. Add tests in CreatureEditor.Tests

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

## Known Limitations

Current alpha state:

- Equipment slots show items but visual population needs work
- Item palette not implemented
- No item editing (view-only for now)
- No drag-drop between equipment and backpack

See Epic #544 for roadmap.

---
