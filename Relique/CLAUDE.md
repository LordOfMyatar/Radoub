# CLAUDE.md - Relique

Tool-specific guidance for Claude Code sessions working on Relique (ItemEditor namespace).

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Overview

**Relique** is an item blueprint editor for Neverwinter Nights UTI (Item Blueprint) files. Part of the Radoub toolset.

### Key Information
- **Tool Name**: Relique
- **File Type**: `.uti` (Item Blueprint)
- **Internal Namespace**: `ItemEditor`
- **Current Version**: See `CHANGELOG.md` for latest version

---

## Architecture

### Project Structure
```
Relique/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── README.md
├── version.json
├── Relique/                          (namespace: ItemEditor)
│   ├── Relique.csproj
│   ├── Program.cs
│   ├── App.axaml(.cs)
│   ├── app.manifest
│   ├── Services/                     # Relique-local
│   │   ├── ArmorPartCatalogService.cs
│   │   ├── BaseItemCategoryService.cs
│   │   ├── CommandLineService.cs
│   │   ├── CompositeWeaponPartCatalogService.cs
│   │   ├── EditAutoApplyDecider.cs
│   │   ├── IItemPreviewRenderer.cs
│   │   ├── ItemNamingService.cs
│   │   ├── ItemPreviewController.cs
│   │   ├── ItemPropertyService.cs
│   │   ├── ItemStatisticsService.cs
│   │   ├── PropertyCategoryService.cs
│   │   ├── SettingsService.cs
│   │   └── TreeExpansionTracker.cs
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs) + 5 partials (EditorPopulation, FileOps,
│   │   │                                       ItemPreview, ItemProperties,
│   │   │                                       Lifecycle, MenuHandlers)
│   │   ├── BaseItemTypePickerWindow.axaml(.cs)
│   │   ├── ItemIconPickerWindow.axaml(.cs)
│   │   ├── NewItemWizardWindow.axaml(.cs)
│   │   └── SettingsWindow.axaml(.cs)
│   ├── ViewModels/
│   │   ├── ItemViewModel.cs
│   │   └── VariableViewModel.cs
│   └── Assets/
└── Relique.Tests/                    (namespace: ItemEditor.Tests)
    ├── Relique.Tests.csproj
    ├── CommandLineServiceTests.cs
    ├── SettingsServiceTests.cs
    ├── Services/
    │   ├── ArmorPartCatalogServiceTests.cs
    │   ├── BaseItemCategoryServiceTests.cs
    │   ├── CompositeWeaponPartCatalogServiceTests.cs
    │   ├── EditAutoApplyDeciderTests.cs
    │   ├── ItemNamingServiceTests.cs
    │   ├── ItemPreviewControllerTests.cs
    │   ├── ItemPropertyOperationTests.cs
    │   ├── ItemPropertyServiceTests.cs
    │   ├── ItemStatisticsServiceTests.cs
    │   ├── NewItemCommandLineTests.cs
    │   ├── PropertyCategoryServiceTests.cs
    │   └── TreeExpansionTrackerTests.cs
    └── ViewModels/
        ├── ItemEditingRoundTripTests.cs
        ├── ItemViewModelConditionalTests.cs
        ├── ItemViewModelTests.cs
        └── VariableViewModelTests.cs
```

**Note**: `BaseItemTypeService` is a shared service in `Radoub.Formats/Services/`, not local to Relique.

---

## UTI File Format

UTI files are GFF-based item blueprints. The parser/writer is in `Radoub.Formats/Radoub.Formats/Uti/`.

### Core Properties
- `LocName` - Localized item name
- `Tag` - Item tag (max 32 chars)
- `TemplateResRef` - Resource reference (max 16 chars)
- `BaseItem` - Base item type (index into baseitems.2da)
- `Cost` - Item cost in gold
- `AddCost` - Additional cost
- `StackSize` - Stack size for stackable items
- `Charges` - Number of charges (0 = unlimited)

### Flags
- `Plot` - Cannot be dropped or sold
- `Stolen` - Marked as stolen
- `Cursed` - Cannot be unequipped
- `Identified` - Already identified (no lore check needed)

### Item Properties
- `PropertiesList` - List of enchantments/abilities
- Each property: PropertyName, Subtype, CostTable, CostValue, Param1, Param1Value

### Item Property 2DA Cascade
```
PropertyName (ushort) → index into itempropdef.2da
  ├─ SubTypeResRef → iprp_[subtype].2da[Subtype]
  ├─ CostTableResRef → iprp_costtable.2da[CostTable] → iprp_[cost].2da[CostValue]
  └─ Param1ResRef → iprp_paramtable.2da[Param1] → iprp_[param].2da[Param1Value]
```

Key classes in Radoub.Formats:
- `ItemProperty` (UtiFile.cs) — raw property data struct
- `ItemPropertyResolver` — resolves indices to human-readable strings via 2DA/TLK
- `IGameDataService` — interface for 2DA/TLK access (Get2DA, GetString)

### Appearance
- `ModelPart1/2/3` - Model part indices
- `ArmorPart_*` - Armor-specific part indices
- `Cloth1/2Color`, `Leather1/2Color`, `Metal1/2Color` - Color indices

---

## Development Guidelines

### Build Commands
```bash
# Build Relique only
dotnet build Relique/Relique/Relique.csproj

# Build all tools
dotnet build Radoub.sln

# Run Relique
dotnet run --project Relique/Relique/Relique.csproj

# Run tests
dotnet test Relique/Relique.Tests
```

### UI Patterns

Follow Quartermaster/Fence patterns:
- Use `SettingsService` for tool-specific settings
- Use `RadoubSettings` for shared game paths
- Use `ThemeManager` for theme support
- Use `UnifiedLogger` for logging
- Use `DocumentState` for dirty tracking and title bar updates

### Modal vs Non-Modal

**IMPORTANT**: Use non-modal windows for informational messages:
```csharp
// ❌ WRONG - blocks main window
await dialog.ShowDialog(this);

// ✅ CORRECT - non-blocking
dialog.Show(this);
```

Exception: Save confirmation dialogs may be modal.

---

## Commit Conventions

Use `[Relique]` prefix:

```bash
[Relique] fix: Correct item property save (#123)
[Relique] feat: Add enchantment editor (#456)
```

Changes go in `Relique/CHANGELOG.md` (not Radoub CHANGELOG).

---

## Dependencies

| Library | Purpose |
|---------|---------|
| Radoub.Formats | UTI file parsing (UtiReader/UtiWriter), GameDataService, 2DA/TLK |
| Radoub.UI | ThemeManager, AboutWindow, BrushManager, shared styles |

---

## Resources

- [Relique CHANGELOG](CHANGELOG.md)
- [UTI Parser](../Radoub.Formats/Radoub.Formats/Uti/)
- [Epic #1576 - Item Creation & Editing](https://github.com/LordOfMyatar/Radoub/issues/1576)
- NonPublic docs: `NonPublic/Relique/` (specs, plans, research — NOT in tool directory)

---
