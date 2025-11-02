# Handlers Directory - Avalonia Migration Notes

## Status: Awaiting MainWindow Migration

The handlers in this directory require significant Avalonia-specific adaptations and will be migrated during Phase 0.4-0.6 when MainWindow.axaml is being built.

## Files Needing Avalonia Adaptation

### FileOperationsHandler.cs
- Replace `Microsoft.Win32.OpenFileDialog/SaveFileDialog` with Avalonia equivalents
- Replace `System.Windows.MessageBox` with Avalonia dialogs
- Replace `System.Windows.RoutedEventArgs` with Avalonia event args
- Replace `Window` with Avalonia.Controls.Window

### ThemeAndSettingsHandler.cs
- Replace ModernWpfUI theme system with Avalonia themes
- Replace WPF controls (MenuItem, TextBox, Button, etc.) with Avalonia equivalents
- Replace `Microsoft.Win32.OpenFolderDialog` with Avalonia folder picker
- Rebuild settings dialogs using Avalonia XAML

### TreeViewHandler.cs
- Replace `System.Windows.Controls.TreeView` with Avalonia TreeView
- Replace `TreeViewItem` container generation with Avalonia equivalents
- Replace `Clipboard.SetText` with Avalonia clipboard API
- Update event handling for Avalonia patterns

### PropertiesPanelHandler.cs
- Replace all WPF controls with Avalonia equivalents
- Replace `System.Windows.Media.Brush/Color` with Avalonia brushes
- Replace resource lookup with Avalonia resource system
- Rebuild parameter grid UI using Avalonia controls

## NodePropertiesHelper.cs Status

✅ **COMPLETE** - This file is pure logic with no UI dependencies and works in Avalonia without changes.

## Next Steps

These handlers will be migrated incrementally during MainWindow migration phases:
- Phase 0.4: MainWindow.axaml creation
- Phase 0.5: TreeView and Properties Panel migration
- Phase 0.6: File Operations and Settings migration
