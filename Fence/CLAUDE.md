# CLAUDE.md - Fence (Merchant Editor)

Tool-specific guidance for Claude Code sessions working on Fence.

---

## Overview

**Fence** is a merchant/store editor for Neverwinter Nights UTM (Store Blueprint) files. Part of the Radoub toolset.

### Key Information
- **Tool Name**: Fence
- **File Type**: `.utm` (Store Blueprint)
- **Internal Namespace**: `MerchantEditor`
- **Parent Epic**: #555 (Merchant Editor Tool)

---

## Architecture

### Project Structure
```
Fence/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── Fence/
│   ├── Fence.csproj
│   ├── Program.cs
│   ├── App.axaml[.cs]
│   ├── Services/
│   │   ├── CommandLineService.cs
│   │   └── SettingsService.cs
│   ├── Views/
│   │   ├── MainWindow.axaml[.cs]
│   │   └── SettingsWindow.axaml[.cs]
│   ├── ViewModels/
│   │   ├── StoreItemViewModel.cs
│   │   └── PaletteItemViewModel.cs
│   └── Themes/
└── Fence.Tests/ (future)
```

### Key Design Decisions

1. **No Tabs** - Unlike Aurora Toolset, Fence uses search/filters instead of 5 inventory tabs
2. **Double-click transfer** - Double-click adds/removes items from store
3. **Non-modal dialogs** - All windows (Settings, About, warnings) are non-blocking
4. **Checkbox restrictions** - WillOnlyBuy/WillNotBuy are checkboxes, not dual-list pickers

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

## Development Guidelines

### Build Commands
```bash
# Build Fence only
dotnet build Fence/Fence/Fence.csproj

# Build all tools
dotnet build Radoub.sln
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

**IMPORTANT**: This tool uses non-modal windows exclusively:
```csharp
// ❌ WRONG - blocks main window
await dialog.ShowDialog(this);

// ✅ CORRECT - non-blocking
dialog.Show(this);
```

---

## Current Status

### Implemented
- [x] Project scaffold with theming
- [x] UTM file loading
- [x] Store properties editing
- [x] Inventory display
- [x] Item palette (structure only)
- [x] File operations
- [x] Double-click transfer
- [x] Settings window
- [x] WillOnlyBuy/WillNotBuy checkboxes
- [x] Item name resolution from UTI files
- [x] Price calculation based on markup/markdown

### TODO
- [ ] Item palette population from game data
- [ ] Search/filter implementation
- [ ] Unit tests

---

## Resources

- **BioWare Store Format**: `Documentation/BioWare_Markdown_Docs/`
- **UTM Parser**: `Radoub.Formats/Radoub.Formats/Utm/`
- **Shared UI**: `Radoub.UI/`

---
