using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Services;
using Parley.Views.Helpers;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for window lifecycle events (open, close, loaded)
    /// and related initialization. Extracted from MainWindow.axaml.cs (#1220).
    /// </summary>
    public partial class MainWindow
    {
        /// <summary>
        /// Handles window opened event - restores state.
        /// </summary>
        private async void OnWindowOpened(object? sender, EventArgs e)
        {
            await _services.WindowPersistence.RestoreWindowPositionAsync();
            PopulateRecentFilesMenu();
            await _services.WindowPersistence.HandleStartupFileAsync(_viewModel);

            // If no file was loaded, try to show module info from RadoubSettings
            if (string.IsNullOrEmpty(_viewModel.CurrentFilePath))
            {
                _controllers.FileMenu.InitializeModuleInfoFromSettings();
            }

            if (SettingsService.Instance.FlowchartVisible)
            {
                _controllers.Flowchart.RestoreOnStartup();
            }

            // Initialize portrait service with game data path (#915)
            InitializePortraitService();

            // Initialize dialog browser panel (#1143)
            InitializeDialogBrowserPanel();

            // #988: Warm up GameDataService in background to avoid lag on first NPC click
            // KEY/BIF/TLK loading is lazy - prime it during startup instead of on first use
            _ = WarmupGameDataServiceAsync();
        }

        /// <summary>
        /// Warms up GameDataService by priming the KEY file and commonly used 2DA files.
        /// This prevents lag on first NPC click when portrait/soundset lookup happens.
        /// Issue #988: Parley startup performance.
        /// </summary>
        private async Task WarmupGameDataServiceAsync()
        {
            await Task.Run(() =>
            {
                try
                {
                    if (!_services.GameData.IsConfigured)
                    {
                        UnifiedLogger.LogApplication(LogLevel.DEBUG, "GameDataService not configured, skipping warmup");
                        return;
                    }

                    // Prime commonly used 2DAs for NPC display
                    // These lookups trigger lazy loading of KEY file + BIF archives
                    _services.GameData.Get2DA("portraits");
                    _services.GameData.Get2DA("soundset");

                    UnifiedLogger.LogApplication(LogLevel.INFO, "GameDataService warmup complete");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"GameDataService warmup failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Initializes portrait service with game data paths for portrait loading (#915).
        /// </summary>
        private void InitializePortraitService()
        {
            var settings = SettingsService.Instance;
            var basePath = settings.BaseGameInstallPath;

            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                var dataPath = Path.Combine(basePath, "data");
                if (Directory.Exists(dataPath))
                {
                    PortraitService.Instance.SetGameDataPath(dataPath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait service initialized with game data path");
                }
            }
        }

        /// <summary>
        /// Initializes dialog browser panel with context and event handlers (#1143).
        /// </summary>
        private void InitializeDialogBrowserPanel()
        {
            var dialogBrowserPanel = this.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel == null)
            {
                UnifiedLogger.LogUI(LogLevel.WARN, "DialogBrowserPanel not found");
                return;
            }

            // Create context for HAK file discovery
            var context = new ParleyScriptBrowserContext(_viewModel.CurrentFilePath, _services.GameData);

            // Set initial module path from RadoubSettings
            var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;
            if (!string.IsNullOrEmpty(modulePath))
            {
                // If it's a .mod file, find the working directory
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                {
                    modulePath = FindWorkingDirectory(modulePath);
                }
                else if (!Directory.Exists(modulePath))
                {
                    modulePath = null;
                }
            }

            if (!string.IsNullOrEmpty(modulePath))
            {
                dialogBrowserPanel.ModulePath = modulePath;
                UnifiedLogger.LogUI(LogLevel.INFO, $"DialogBrowserPanel initialized with module path");
            }

            // Subscribe to file selection events
            dialogBrowserPanel.FileSelected += OnDialogBrowserFileSelected;

            // Subscribe to collapse/expand events (#1143)
            dialogBrowserPanel.CollapsedChanged += OnDialogBrowserCollapsedChanged;

            // Update menu item checkmark
            UpdateDialogBrowserMenuState();
        }

        /// <summary>
        /// Handles collapse/expand button clicks from DialogBrowserPanel (#1143).
        /// </summary>
        private void OnDialogBrowserCollapsedChanged(object? sender, bool isCollapsed)
        {
            _services.WindowPersistence.SetDialogBrowserPanelVisible(!isCollapsed);
            UpdateDialogBrowserMenuState();
        }

        /// <summary>
        /// Updates the DialogBrowserPanel's current file highlight (#1143).
        /// Called by FileMenuController after File > Open loads a file.
        /// </summary>
        private void UpdateDialogBrowserCurrentFile(string filePath)
        {
            var dialogBrowserPanel = this.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel != null)
            {
                dialogBrowserPanel.CurrentFilePath = filePath;
            }
        }

        /// <summary>
        /// Find the unpacked working directory for a .mod file.
        /// </summary>
        private static string? FindWorkingDirectory(string modFilePath)
        {
            var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
            var moduleDir = Path.GetDirectoryName(modFilePath);

            if (string.IsNullOrEmpty(moduleDir))
                return null;

            var candidates = new[]
            {
                Path.Combine(moduleDir, moduleName),
                Path.Combine(moduleDir, "temp0"),
                Path.Combine(moduleDir, "temp1")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "module.ifo")))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Handles file selection in the dialog browser panel (#1143).
        /// </summary>
        private async void OnDialogBrowserFileSelected(object? sender, FileSelectedEventArgs e)
        {
            // Only load on single click (per issue requirements)
            // Double-click could be used for something else in the future
            if (e.Entry == null)
                return;

            var filePath = e.Entry.FilePath;

            // Handle HAK files - they need extraction first
            if (e.Entry.IsFromHak && !string.IsNullOrEmpty(e.Entry.HakPath))
            {
                _viewModel.StatusMessage = $"Cannot open dialogs from HAK directly - use HAK editor to extract first";
                return;
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _viewModel.StatusMessage = $"File not found: {e.Entry.Name}";
                return;
            }

            // Check for unsaved changes and auto-save if needed
            if (_viewModel.HasUnsavedChanges)
            {
                // Auto-save current file before loading new one
                if (!string.IsNullOrEmpty(_viewModel.CurrentFilePath))
                {
                    _viewModel.StatusMessage = "Auto-saving current dialog...";
                    await _viewModel.SaveDialogAsync(_viewModel.CurrentFilePath);
                }
            }

            // Load the selected dialog
            _viewModel.StatusMessage = $"Loading {e.Entry.Name}...";
            await _viewModel.LoadDialogAsync(filePath);

            // Update flowchart after loading (same as File menu pattern)
            UpdateEmbeddedFlowchartAfterLoad();

            // Update the current file highlight in the browser panel
            var dialogBrowserPanel = this.FindControl<DialogBrowserPanel>("DialogBrowserPanel");
            if (dialogBrowserPanel != null)
            {
                dialogBrowserPanel.CurrentFilePath = filePath;
            }

            // Refresh the dialog browser to show the new file as selected
            PopulateRecentFilesMenu();
        }

        /// <summary>
        /// Toggle dialog browser panel visibility (View menu) (#1143).
        /// </summary>
        private void OnToggleDialogBrowserClick(object? sender, RoutedEventArgs e)
        {
            var settings = SettingsService.Instance;
            _services.WindowPersistence.SetDialogBrowserPanelVisible(!settings.DialogBrowserPanelVisible);
            UpdateDialogBrowserMenuState();
        }

        /// <summary>
        /// Updates the dialog browser menu item checkmark state (#1143).
        /// </summary>
        private void UpdateDialogBrowserMenuState()
        {
            var menuItem = this.FindControl<MenuItem>("DialogBrowserMenuItem");
            if (menuItem != null)
            {
                var isVisible = SettingsService.Instance.DialogBrowserPanelVisible;
                menuItem.Icon = isVisible ? new TextBlock { Text = "✓" } : null;
            }
        }

        /// <summary>
        /// Handles window property changes for position/size persistence.
        /// </summary>
        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (!_services.WindowPersistence.IsRestoringPosition)
            {
                if (e.Property.Name == nameof(Width) || e.Property.Name == nameof(Height))
                {
                    _services.WindowPersistence.SaveWindowPosition();
                }
            }
        }

        /// <summary>
        /// Handles debug messages collection changes - auto-scrolls to latest message.
        /// </summary>
        private void OnDebugMessagesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                var debugListBox = this.FindControl<ListBox>("DebugListBox");
                if (debugListBox != null && debugListBox.ItemCount > 0)
                {
                    debugListBox.ScrollIntoView(debugListBox.ItemCount - 1);
                }
            }
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Controls are now available, restore settings
            _services.WindowPersistence.RestoreDebugSettings();
            _services.WindowPersistence.RestorePanelSizes();

            // Initialize menu checkmark states
            _controllers.Flowchart.UpdateLayoutMenuChecks();

            // Initialize NPC speaker visual preference ComboBoxes (Issue #16, #36)
            InitializeSpeakerVisualComboBoxes();

            // #1158: Set up TreeView width tracking for word wrap
            SetupTreeViewWidthTracking();
        }

        /// <summary>
        /// Set up tracking of TreeView width for dynamic word wrap (#1158).
        /// Updates UISettingsService.TreeViewTextMaxWidth when the panel resizes.
        /// </summary>
        private void SetupTreeViewWidthTracking()
        {
            var leftPaneTabControl = this.FindControl<TabControl>("LeftPaneTabControl");
            if (leftPaneTabControl == null) return;

            // Initial width calculation
            UpdateTreeViewTextMaxWidth(leftPaneTabControl.Bounds.Width);

            // Subscribe to size changes
            leftPaneTabControl.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == nameof(leftPaneTabControl.Bounds))
                {
                    UpdateTreeViewTextMaxWidth(leftPaneTabControl.Bounds.Width);
                }
            };
        }

        /// <summary>
        /// Calculate and update the maximum width for TreeView text (#1158).
        /// Accounts for icons, speaker tags, indentation, and scrollbar.
        /// </summary>
        private void UpdateTreeViewTextMaxWidth(double containerWidth)
        {
            // Estimate space used by:
            // - TreeView indentation (varies by depth, estimate ~100px for 4 levels)
            // - Warning icon (~18px)
            // - Node shape icon (~20px)
            // - Speaker tag (~50px average)
            // - Spacing (~20px)
            // - Scrollbar (~20px)
            // - Padding/margins (~20px)
            const double fixedOverhead = 250;

            var textWidth = containerWidth - fixedOverhead;
            if (textWidth > 0)
            {
                var previousWidth = UISettingsService.Instance.TreeViewTextMaxWidth;
                UISettingsService.Instance.TreeViewTextMaxWidth = textWidth;

                // Only refresh if word wrap is enabled and width changed significantly
                if (SettingsService.Instance.TreeViewWordWrap && Math.Abs(textWidth - previousWidth) > 10)
                {
                    // Refresh tree to apply new width
                    _viewModel.RefreshTreeViewColors();
                }
            }
        }

        private async void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Phase 1 Step 4: Check for unsaved changes
            if (_viewModel.HasUnsavedChanges)
            {
                // Cancel the close to show dialog
                e.Cancel = true;

                // Check if auto-save timer is running - complete it first
                if (_autoSaveTimer != null && _autoSaveTimer.Enabled)
                {
                    _viewModel.StatusMessage = "Waiting for auto-save to complete...";
                    _autoSaveTimer.Stop();
                    await AutoSaveToFileAsync();
                }

                // Show unsaved changes prompt
                var fileName = string.IsNullOrEmpty(_viewModel.CurrentFileName)
                    ? "this file"
                    : System.IO.Path.GetFileName(_viewModel.CurrentFileName);

                var shouldSave = await _services.Dialog.ShowConfirmDialogAsync(
                    "Unsaved Changes",
                    $"Do you want to save changes to {fileName}"
                );

                if (shouldSave)
                {
                    // Save before closing
                    if (string.IsNullOrEmpty(_viewModel.CurrentFileName))
                    {
                        // No filename - need Save As dialog
                        _viewModel.StatusMessage = "Cannot auto-save without filename. Use File → Save As first.";
                        return; // Don't close
                    }

                    // Issue #8: Check save result - offer Save As if save fails
                    var saveSuccess = await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);
                    if (!saveSuccess)
                    {
                        // Save failed (e.g., read-only file) - offer Save As
                        var saveAs = await _services.Dialog.ShowSaveErrorDialogAsync(_viewModel.StatusMessage);
                        if (saveAs)
                        {
                            // Show Save As dialog
                            await ShowSaveAsDialogAsync();
                            // Check if save succeeded after Save As
                            if (_viewModel.HasUnsavedChanges)
                            {
                                // User cancelled Save As or it failed - don't close
                                return;
                            }
                        }
                        else
                        {
                            // User chose Cancel - ask if they want to discard
                            var discardChanges = await _services.Dialog.ShowConfirmDialogAsync(
                                "Discard Changes?",
                                "Save failed. Do you want to discard changes and close anyway?");
                            if (!discardChanges)
                            {
                                return; // Don't close
                            }
                        }
                    }
                }

                // Now close (unhook event to prevent recursion, cleanup runs in second close)
                this.Closing -= OnWindowClosing;
                CleanupOnClose();
                this.Close();
                return;
            }

            // Clean up resources when window actually closes
            CleanupOnClose();
        }

        /// <summary>
        /// Clean up all resources when the window closes.
        /// Called from OnWindowClosing in both cancel/reclose path and normal close path.
        /// </summary>
        private void CleanupOnClose()
        {
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();

            // Unsubscribe from events
            _services.SoundPlayback.PlaybackStopped -= OnSoundPlaybackStopped;

            // Dispose services (handles Audio and SoundPlayback)
            _services.Dispose();

            // Issue #343: Close all managed windows (Settings, Flowchart)
            _windows.CloseAll();

            // Close browser windows managed by controllers
            _controllers.ScriptBrowser.CloseActiveScriptBrowser();
            _controllers.ParameterBrowser.CloseActiveParameterBrowser();

            // Save window position on close
            _services.WindowPersistence.SaveWindowPosition();
        }
    }
}
