# Changelog - Fence

All notable changes to Fence (Merchant Editor) will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

---

## [Unreleased]

---

## [0.1.0-alpha] - 2026-01-15
**Branch**: `radoub/issue-558` | **PR**: #910

### Initial Release

Fence - Merchant Editor for Neverwinter Nights. Part of Epic #555.

#### Added
- **Project scaffold** - Avalonia UI application with theming and logging
- **MainWindow** - Two-panel layout with Store Inventory and Item Palette
- **Store Properties panel** - Name, Tag, pricing settings, black market flags
- **Buy Restrictions** - WillOnlyBuy/WillNotBuy as collapsible checkbox panel with base item types from baseitems.2da
- **Store Inventory** - DataGrid display with search/filter, infinite checkbox
- **Item Palette** - Item browser with type filtering
- **File operations** - Open/Save UTM files, recent files menu
- **Double-click transfer** - Double-click to add/remove items from store
- **Settings** - Theme selection, font settings, resource paths
- **Non-modal dialogs** - All dialogs are non-blocking (per user requirement)
- **Theme support** - 8 themes including accessibility options

#### Dependencies
- UTM Parser from Radoub.Formats (#556)
- Shared UI components from Radoub.UI

---
