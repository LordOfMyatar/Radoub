# PowerShell script to integrate DebugAndLoggingHandler into MainWindow
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$content = Get-Content $filePath -Raw

# 1. Add field declaration after KeyboardShortcutManager
$oldFields = "        private readonly KeyboardShortcutManager _keyboardShortcutManager; // Manages keyboard shortcuts

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$newFields = "        private readonly KeyboardShortcutManager _keyboardShortcutManager; // Manages keyboard shortcuts
        private readonly DebugAndLoggingHandler _debugAndLoggingHandler; // Handles debug and logging operations

        // DEBOUNCED AUTO-SAVE: Timer for file auto-save after inactivity"
$content = $content.Replace($oldFields, $newFields)

# 2. Add service initialization after KeyboardShortcutManager init
$oldInit = "            _keyboardShortcutManager.RegisterShortcuts(this);

            DebugLogger.Initialize(this);"
$newInit = "            _keyboardShortcutManager.RegisterShortcuts(this);
            _debugAndLoggingHandler = new DebugAndLoggingHandler(
                viewModel: _viewModel,
                findControl: this.FindControl<Control>,
                getStorageProvider: () => this.StorageProvider,
                setStatusMessage: msg => _viewModel.StatusMessage = msg);

            DebugLogger.Initialize(this);"
$content = $content.Replace($oldInit, $newInit)

# 3. Replace OnOpenLogFolderClick
$content = $content -replace 'private void OnOpenLogFolderClick\(object\? sender, RoutedEventArgs e\)[\s\S]*?catch \(Exception ex\)[\s\S]*?UnifiedLogger.LogApplication\(LogLevel.ERROR, \$"Failed to open log folder: \{ex.Message\}"\);[\s\S]*?\}[\s\S]*?\}', @'
private void OnOpenLogFolderClick(object? sender, RoutedEventArgs e)
        {
            _debugAndLoggingHandler.OpenLogFolder();
        }
'@

# 4. Replace OnExportLogsClick
$content = $content -replace 'private async void OnExportLogsClick\(object\? sender, RoutedEventArgs e\)[\s\S]*?catch \(Exception ex\)[\s\S]*?UnifiedLogger.LogApplication\(LogLevel.ERROR, \$"Failed to export logs: \{ex.Message\}"\);[\s\S]*?\}[\s\S]*?\}', @'
private async void OnExportLogsClick(object? sender, RoutedEventArgs e)
        {
            await _debugAndLoggingHandler.ExportLogsAsync(this);
        }
'@

# 5. Replace OnRestoreScrapClick
$content = $content -replace 'private void OnRestoreScrapClick\(object\? sender, RoutedEventArgs e\)[\s\S]*?UnifiedLogger.LogApplication\(LogLevel.DEBUG, \$"Restore result: \{restored\}"\);[\s\S]*?if \(!restored\)[\s\S]*?\{[\s\S]*?// The RestoreFromScrap method will set an appropriate status message[\s\S]*?\}[\s\S]*?\}', @'
private void OnRestoreScrapClick(object? sender, RoutedEventArgs e)
        {
            var treeView = this.FindControl<TreeView>("DialogTreeView");
            var selectedNode = treeView?.SelectedItem as TreeViewSafeNode;
            _debugAndLoggingHandler.RestoreFromScrap(selectedNode);
        }
'@

# 6. Replace OnClearScrapClick
$content = $content -replace 'private async void OnClearScrapClick\(object\? sender, RoutedEventArgs e\)[\s\S]*?await messageBox.ShowDialog\(this\);[\s\S]*?\}', @'
private async void OnClearScrapClick(object? sender, RoutedEventArgs e)
        {
            await _debugAndLoggingHandler.ClearScrapAsync(this);
        }
'@

# 7. Replace AddDebugMessage
$content = $content -replace 'public void AddDebugMessage\(string message\) => _viewModel.AddDebugMessage\(message\);', 'public void AddDebugMessage(string message) => _debugAndLoggingHandler.AddDebugMessage(message);'

# Write back
$content | Set-Content $filePath -NoNewline

Write-Host "DebugAndLoggingHandler integration completed!"
