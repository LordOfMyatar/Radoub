# PowerShell script to integrate KeyboardShortcutManager into MainWindow
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$content = Get-Content $filePath -Raw

# 1. Add IKeyboardShortcutHandler interface to class declaration
$oldClass = "public partial class MainWindow : Window"
$newClass = "public partial class MainWindow : Window, IKeyboardShortcutHandler"
$content = $content.Replace($oldClass, $newClass)

# 2. Add field declaration after ResourceBrowserManager
$oldFields = "        private readonly ResourceBrowserManager _resourceBrowserManager; // Manages resource browser dialogs

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$newFields = "        private readonly ResourceBrowserManager _resourceBrowserManager; // Manages resource browser dialogs
        private readonly KeyboardShortcutManager _keyboardShortcutManager; // Manages keyboard shortcuts

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$content = $content.Replace($oldFields, $newFields)

# 3. Add service initialization after ResourceBrowserManager init
$oldInit = "                getSelectedNode: () => _selectedNode);

            DebugLogger.Initialize(this);"
$newInit = "                getSelectedNode: () => _selectedNode);
            _keyboardShortcutManager = new KeyboardShortcutManager();
            _keyboardShortcutManager.RegisterShortcuts(this);

            DebugLogger.Initialize(this);"
$content = $content.Replace($oldInit, $newInit)

# 4. Replace SetupKeyboardShortcuts() method with simplified version
$content = $content -replace 'private void SetupKeyboardShortcuts\(\)[\s\S]*?(?=\r?\n        private void OnAddContextAwareReply)', @'
private void SetupKeyboardShortcuts()
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "SetupKeyboardShortcuts: Keyboard handler registered");

            // Tunneling event for shortcuts that intercept before TreeView (Ctrl+Shift+Up/Down)
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (_keyboardShortcutManager.HandlePreviewKeyDown(e))
                {
                    e.Handled = true;
                }
            }, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);

            // Standard KeyDown events
            this.KeyDown += (sender, e) =>
            {
                if (_keyboardShortcutManager.HandleKeyDown(e))
                {
                    e.Handled = true;
                }
            };
        }

        #region IKeyboardShortcutHandler Implementation

        // File operations
        void IKeyboardShortcutHandler.OnNew() => OnNewClick(null, null!);
        void IKeyboardShortcutHandler.OnOpen() => OnOpenClick(null, null!);
        void IKeyboardShortcutHandler.OnSave() => OnSaveClick(null, null!);

        // Node operations
        void IKeyboardShortcutHandler.OnAddSmartNode() => OnAddSmartNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnAddContextAwareReply() => OnAddContextAwareReply(null, null!);
        void IKeyboardShortcutHandler.OnDeleteNode() => OnDeleteNodeClick(null, null!);

        // Clipboard operations
        void IKeyboardShortcutHandler.OnCopyNode() => OnCopyNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnCutNode() => OnCutNodeClick(null, null!);
        void IKeyboardShortcutHandler.OnPasteAsDuplicate() => OnPasteAsDuplicateClick(null, null!);
        void IKeyboardShortcutHandler.OnPasteAsLink() => OnPasteAsLinkClick(null, null!);

        // Advanced copy
        void IKeyboardShortcutHandler.OnCopyNodeText() => OnCopyNodeTextClick(null, null!);
        void IKeyboardShortcutHandler.OnCopyNodeProperties() => OnCopyNodePropertiesClick(null, null!);
        void IKeyboardShortcutHandler.OnCopyTreeStructure() => OnCopyTreeStructureClick(null, null!);

        // History
        void IKeyboardShortcutHandler.OnUndo() => OnUndoClick(null, null!);
        void IKeyboardShortcutHandler.OnRedo() => OnRedoClick(null, null!);

        // Tree navigation
        void IKeyboardShortcutHandler.OnExpandSubnodes() => OnExpandSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnCollapseSubnodes() => OnCollapseSubnodesClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeUp() => OnMoveNodeUpClick(null, null!);
        void IKeyboardShortcutHandler.OnMoveNodeDown() => OnMoveNodeDownClick(null, null!);

        #endregion

'@ -replace '\r?\n', "`r`n"

# Write back
$content | Set-Content $filePath -NoNewline

Write-Host "KeyboardShortcutManager integration completed!"
