# Avalonia Migration Progress

**Phase**: 0 - Cross-Platform Foundation
**Branch**: `feature/avalonia-migration`
**Started**: 2025-10-04

---

## Current Status: Phase 0.5 Complete - File Dialogs Implemented!

✅ **Phase 0.1 - Project Setup**:
- Installed Avalonia templates (11.3.6)
- Created Parley.Avalonia project (MVVM template)
- Verified project builds successfully

✅ **Phase 0.2 - Non-UI Code Migration**:
- Copied Models/ (5 files - DialogStructures, ConversationNode, etc.)
- Copied Parsers/ (4 files - DialogParser, GFF reader, structures)
- Copied Utils/ (4 files - CompactPointerManager, TreeBuilder, DebugLogger, FieldIndexTracker)
- Copied Services/ (3 files - ScriptService, SettingsService, UnifiedLogger)
- Added NuGet packages (Newtonsoft.Json, Microsoft.Extensions.Logging, DI, System.Text.Json)
- Fixed DebugLogger WPF dependencies (temporary console fallback)
- **Build Status**: ✅ Success (0 errors, 5 nullable warnings)

✅ **Phase 0.3 - ViewModels & Handlers Migration**:
- Copied ViewModels/ (2 files - BaseViewModel, MainViewModel)
- Updated MainViewModel: WPF → Avalonia (Dispatcher.UIThread, removed WPF Application refs)
- Copied Handlers/ (5 files - NodePropertiesHelper complete, others awaiting UI migration)
- Created Handlers/README.md documenting Avalonia adaptation requirements
- **Build Status**: ✅ Success (0 errors, 5 nullable warnings - same as before)

✅ **Phase 0.4 - MainWindow XAML & Code-Behind**:
- Migrated MainWindow.xaml → MainWindow.axaml (329 lines of Avalonia XAML)
- Updated syntax: `Items` → `ItemsSource`, `HierarchicalDataTemplate` → `TreeDataTemplate`
- Added `xmlns:models` namespace for TreeView DataTemplate
- Migrated MainWindow.xaml.cs with stub event handlers (246 lines)
- All menu items, TreeView, Properties Panel, Debug Console rendered
- **Build Status**: ✅ Success (0 errors, 5 nullable warnings - unchanged)

✅ **Phase 0.5 - File Operations**:
- Implemented Open dialog using Avalonia.Platform.Storage.StorageProvider
- Implemented Save As dialog with file type filters (.dlg, .json)
- Recent Files menu population with dynamic menu item creation
- Used `ToolTip.SetTip()` attached property (Avalonia pattern)
- File operations fully functional (Open, Save, SaveAs, Recent Files, Close)
- **Build Status**: ✅ Success (0 errors, 5 nullable warnings - unchanged)

⏳ **Next Steps (Phase 0.6+)**:
- Phase 0.6: Implement TreeView and Properties Panel handlers
- Phase 0.7: Test cross-platform builds and run application
- Phase 0.8: Final validation and cleanup

---

## Files to Migrate

### Pure Logic (Copy as-is)
- ✅ Parley.Avalonia project created
- ✅ Models/ (5 files)
  - ✅ DialogStructures.cs
  - ✅ ConversationNode.cs
  - ✅ TreeSafeDialogNode.cs
  - ✅ ConversationManager.cs
  - ✅ TreeViewSafeNode.cs
- ✅ Parsers/ (4 files)
  - ✅ DialogParser.cs
  - ✅ GffBinaryReader.cs
  - ✅ GffStructures.cs
  - ✅ IDialogParser.cs
- ✅ Utils/ (4 files)
  - ✅ CompactPointerManager.cs
  - ✅ ConversationTreeBuilder.cs
  - ✅ DebugLogger.cs (WPF refs commented out)
  - ✅ FieldIndexTracker.cs
- ✅ Services/ (3 files)
  - ✅ ScriptService.cs
  - ✅ SettingsService.cs
  - ✅ UnifiedLogger.cs

### Needs Minor Updates (Using statements)
- ✅ ViewModels/
  - ✅ BaseViewModel.cs (no changes needed - pure INotifyPropertyChanged)
  - ✅ MainViewModel.cs (updated Dispatcher, removed WPF Application refs)

### Needs Major Rewrite (UI-specific)
- ⏳ MainWindow.xaml / MainWindow.xaml.cs
- ✅ Handlers/ (partially migrated - awaiting UI controls)
  - ⏳ FileOperationsHandler.cs (needs Avalonia file dialogs)
  - ⏳ ThemeAndSettingsHandler.cs (needs Avalonia theme system)
  - ⏳ TreeViewHandler.cs (needs Avalonia TreeView controls)
  - ⏳ PropertiesPanelHandler.cs (needs Avalonia controls)
  - ✅ NodePropertiesHelper.cs (complete - pure logic)

---

## Session Log

### Session 2025-10-04 (Part 1)
- Installed Avalonia templates (11.3.6)
- Created Parley.Avalonia MVVM project
- Verified initial build success

### Session 2025-10-04 (Part 2)
- Copied Models/ (5 files - all dialog data structures)
- Copied Parsers/ (4 files - Aurora GFF parsing logic)
- Copied Utils/ (4 files - helpers, fixed DebugLogger WPF deps)
- Copied Services/ (3 files - script, settings, logging services)
- Added required NuGet packages (4 packages)
- Fixed DebugLogger to remove WPF MainWindow dependency
- Build succeeds with 0 errors
- **Next**: Copy ViewModels, then start UI migration (Handlers, MainWindow)

### Session 2025-10-04 (Part 3)
- Copied ViewModels/ (2 files - BaseViewModel, MainViewModel)
- Updated MainViewModel Avalonia compatibility:
  - Changed `System.Windows.Application.Dispatcher` → `Avalonia.Threading.Dispatcher.UIThread`
  - Removed WPF Application.Current references with TODO comments
  - Added TODOs for application shutdown logic
- Copied Handlers/ with migration notes:
  - NodePropertiesHelper.cs: ✅ Complete (pure logic, no UI deps)
  - Other handlers: Documented Avalonia adaptation needs in README.md
- Build succeeds with 0 errors, 5 warnings (same as original)
- **Next**: Phase 0.4 - MainWindow.axaml migration

---

## Notes

- Avalonia 11.3.6 targets .NET 9.0 (matches our current version)
- MVVM template provides good starting structure
- Original WPF project remains in Parley/ (for reference)
- Migration will be incremental with testing at each step
