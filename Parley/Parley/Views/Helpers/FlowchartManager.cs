using System;
using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all flowchart-related functionality for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 1).
    ///
    /// Handles:
    /// 1. Flowchart layout modes (Floating, Side-by-Side, Tabbed)
    /// 2. PNG/SVG export functionality
    /// 3. Flowchart ↔ TreeView synchronization (node clicks, selection sync)
    /// 4. FlowView collapse/expand event handling
    /// 5. Flowchart panel updates on dialog changes
    ///
    /// Partial class files: Layout, PanelSync, Export, NodeSync.
    /// </summary>
    public partial class FlowchartManager
    {
        private readonly Window _window;
        private readonly SafeControlFinder _controls;
        private readonly WindowLifecycleManager _windows;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Action<TreeViewSafeNode> _setSelectedNode;
        private readonly Action<TreeViewSafeNode> _populatePropertiesPanel;
        private readonly Action _saveCurrentNodeProperties;
        private readonly Func<bool> _getIsSettingSelectionProgrammatically;
        private readonly Action<bool> _setIsSettingSelectionProgrammatically;
        private readonly ISettingsService _settings;
        private readonly KeyboardShortcutManager _shortcutManager; // #809: For FlowView keyboard parity
        private readonly Action<FlowchartContextMenuEventArgs>? _onContextMenuAction; // #461: Context menu parity
        private readonly Action<DialogNode, DialogNode?, int, int>? _onSiblingReorder; // #240: Flowchart drag-drop reorder
        private readonly Action<DialogNode, DialogPtr?, DialogNode?, int>? _onReparent; // #1965: Flowchart drag-drop reparent

        // Track whether embedded/tabbed panels have been wired up
        private bool _embeddedFlowchartWired = false;
        private bool _tabbedFlowchartWired = false;

        public FlowchartManager(
            Window window,
            SafeControlFinder controls,
            WindowLifecycleManager windows,
            ISettingsService settings,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode,
            Action<TreeViewSafeNode> setSelectedNode,
            Action<TreeViewSafeNode> populatePropertiesPanel,
            Action saveCurrentNodeProperties,
            Func<bool> getIsSettingSelectionProgrammatically,
            Action<bool> setIsSettingSelectionProgrammatically,
            KeyboardShortcutManager shortcutManager,
            Action<FlowchartContextMenuEventArgs>? onContextMenuAction = null,
            Action<DialogNode, DialogNode?, int, int>? onSiblingReorder = null,
            Action<DialogNode, DialogPtr?, DialogNode?, int>? onReparent = null)
        {
            _window = window;
            _controls = controls;
            _windows = windows;
            _settings = settings;
            _getViewModel = getViewModel;
            _getSelectedNode = getSelectedNode;
            _setSelectedNode = setSelectedNode;
            _populatePropertiesPanel = populatePropertiesPanel;
            _saveCurrentNodeProperties = saveCurrentNodeProperties;
            _getIsSettingSelectionProgrammatically = getIsSettingSelectionProgrammatically;
            _setIsSettingSelectionProgrammatically = setIsSettingSelectionProgrammatically;
            _shortcutManager = shortcutManager;
            _onContextMenuAction = onContextMenuAction;
            _onSiblingReorder = onSiblingReorder;
            _onReparent = onReparent;
        }

        private MainViewModel ViewModel => _getViewModel();
    }
}
