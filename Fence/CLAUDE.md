# CLAUDE.md - Fence (Merchant Editor)

Tool-specific guidance for Claude Code sessions working on Fence.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Overview

**Fence** is a merchant/store editor for Neverwinter Nights UTM (Store Blueprint) files. Part of the Radoub toolset.

### Key Information
- **Tool Name**: Fence
- **File Type**: `.utm` (Store Blueprint)
- **Internal Namespace**: `MerchantEditor`
- **Current Version**: See `CHANGELOG.md` for latest version

---

## Architecture

### Project Structure
```
Fence/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── README.md
├── version.json
├── Fence/
│   ├── Fence.csproj
│   ├── Program.cs
│   ├── App.axaml[.cs]
│   ├── Assets/ (fence.ico)
│   ├── Controls/             # Fence-local custom controls
│   ├── Converters/
│   │   └── InfiniteToSymbolConverter.cs
│   ├── Services/             # Fence-local only — shared services live in Radoub.UI / Radoub.Formats
│   │   ├── CommandLineService.cs
│   │   ├── FenceScriptBrowserContext.cs  # IScriptBrowserContext adapter
│   │   └── SettingsService.cs
│   ├── Views/
│   │   ├── MainWindow.axaml[.cs] + partials (FileOps, ItemDetails, ItemPalette,
│   │   │                                     LanguageMenu, Scripts, StoreBrowser,
│   │   │                                     StoreOperations, Variables)
│   │   └── SettingsWindow.axaml[.cs]
│   └── ViewModels/
│       ├── PaletteItemViewModel.cs
│       ├── SelectableBaseItemTypeViewModel.cs
│       ├── StoreItemViewModel.cs
│       └── VariableViewModel.cs
└── Fence.Tests/
    ├── BaseItemTypeServiceWithGameDataTests.cs
    ├── CommandLineServiceTests.cs
    ├── SelectableBaseItemTypeViewModelTests.cs
    ├── SettingsServiceTests.cs
    ├── StoreItemIconTests.cs
    ├── StoreItemViewModelTests.cs
    ├── UtmRoundTripTests.cs
    └── VariableValidationTests.cs
```

**Moved to shared libraries (do NOT add to `Fence/Services/`)**:

| Service | Now lives in |
|---------|--------------|
| BaseItemTypeService | `Radoub.Formats/Services/` |
| ItemResolutionService | `Radoub.UI/Services/` |
| SharedPaletteCacheService | `Radoub.UI/Services/` (replaces the old `PaletteCacheService`) |

### Key Design Decisions

1. **No Tabs** - Unlike Aurora Toolset, Fence uses search/filters instead of 5 inventory tabs
2. **Double-click transfer** - Double-click adds/removes items from store
3. **Non-modal dialogs** - All windows (Settings, About, warnings) are non-blocking
4. **Checkbox restrictions** - WillOnlyBuy/WillNotBuy are checkboxes, not dual-list pickers
5. **Name-based panel mapping** - Items assigned to store panels by base item type name (not index) for compatibility

---

## UTM File Format

UTM files are GFF-based store blueprints containing:

### Store Properties
- `LocName` - Localized store name
- `Tag` - Store tag (max 32 chars)
- `MarkUp` - Sell price percentage (100 = base, higher = more expensive)
- `MarkDown` - Buy price percentage (100 = full price, lower = store pays less)
- `IdentifyPrice` - Cost to identify items (-1 = unavailable)
- `BlackMarket` - Accept stolen goods flag
- `BM_MarkDown` - Buy price for stolen items
- `StoreGold` - Gold reserves (-1 = infinite)
- `MaxBuyPrice` - Max price store pays (-1 = unlimited)

### Store Inventory
- `StoreList` - 5 panels (Armor=0, Misc=1, Potions=2, Rings=3, Weapons=4)
- Each panel contains `ItemList` with:
  - `InventoryRes` - Item ResRef
  - `Infinite` - Unlimited stock flag

### Buy Restrictions
- `WillOnlyBuy` - List of base item types to accept
- `WillNotBuy` - List of base item types to reject

---

## Current Features

(Version managed by NBGV — see `version.json` and `CHANGELOG.md`)


### Store Properties
- Edit all store metadata (name, tag, gold, prices)
- Markup/Markdown percentages with pricing preview
- Collapsible panels for organized editing

### Store Inventory
- View and edit store contents
- Editable ResRef column with validation (16 char limit, Aurora-safe characters)
- Infinite stock toggle (∞ symbol)
- Duplicate ResRef detection

### Item Palette
- Load items from BIF archives
- Search and filter by type
- Standard/Custom content filtering
- Double-click to add to store

### Scripts & Variables
- OnOpenStore, OnStoreClose scripts
- Local variable editing

### Buy Restrictions
- WillOnlyBuy/WillNotBuy checkboxes by base item type
- Buy stolen goods option

### Custom File Browser
- File > Open uses `StoreBrowserWindow` from Radoub.UI
- Browse module directories for UTM files

---

## Development Guidelines

### Build Commands
```bash
# Build Fence only
dotnet build Fence/Fence/Fence.csproj

# Build all tools
dotnet build Radoub.sln

# Run Fence
dotnet run --project Fence/Fence/Fence.csproj
```

### Testing
```bash
# Run Fence tests (when available)
dotnet test Fence/Fence.Tests/Fence.Tests.csproj
```

### UI Patterns

Follow Quartermaster patterns:
- Use `SettingsService` for tool-specific settings
- Use `RadoubSettings` for shared game paths
- Use `ThemeManager` for theme support
- Use `UnifiedLogger` for logging

### Modal vs Non-Modal

**IMPORTANT**: Informational windows (Settings, About, warnings, notifications) are
non-modal — they must never block the main window:
```csharp
// ❌ WRONG - blocks main window for an info/notification window
await dialog.ShowDialog(this);

// ✅ CORRECT - non-blocking
dialog.Show(this);
```

**Carve-outs (modal is correct)** — consistent with the root CLAUDE.md UI/UX guidelines:

- **Resource selection pickers** (e.g. `StoreBrowserWindow` open picker) are modal.
  The open flow has no meaningful work to do until the user picks a file or cancels,
  and the selection is read back from the closed window. This matches the rest of the
  toolset's browser pickers.
- **Destructive-action confirmations** (delete, overwrite) may be modal — the user
  must answer before the destructive action proceeds.

---

## Commit Conventions

Use `[Fence]` prefix:

```bash
[Fence] fix: Correct ResRef validation (#123)
[Fence] feat: Add bulk item import (#456)
```

Changes go in `Fence/CHANGELOG.md` (not Radoub CHANGELOG).

---

## Resources

- **UTM Parser**: `Radoub.Formats/Radoub.Formats/Utm/`
- **Shared UI**: `Radoub.UI/`
- **StoreBrowserWindow**: `Radoub.UI/Radoub.UI/Views/StoreBrowserWindow.axaml`

---
