using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using ThemeManager = Radoub.UI.Services.ThemeManager;

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
            _services.ResourceBrowser.UpdateRecentCreatureTagsDropdown(); // #1244
            await _services.WindowPersistence.HandleStartupFileAsync(_viewModel);

            // If no file was loaded, try to show module info from RadoubSettings
            if (string.IsNullOrEmpty(_viewModel.CurrentFilePath))
            {
                _controllers.FileMenu.InitializeModuleInfoFromSettings();
            }
            else
            {
                // Scan creatures for portrait/soundset display after startup file load
                // FileMenuController handles this for File > Open, but startup path needs it too
                var moduleDir = Path.GetDirectoryName(_viewModel.CurrentFilePath);
                if (!string.IsNullOrEmpty(moduleDir))
                {
                    await ScanCreaturesForModuleAsync(moduleDir);
                }
            }

            if (_services.Settings.FlowchartVisible)
            {
                _controllers.Flowchart.RestoreOnStartup();
            }

            // Initialize portrait service with game data path (#915)
            InitializePortraitService();

            // Initialize dialog browser panel (#1143)
            _controllers.DialogBrowser.InitializePanel();

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
            var settings = _services.Settings;
            var basePath = settings.BaseGameInstallPath;

            if (!string.IsNullOrEmpty(basePath) && Directory.Exists(basePath))
            {
                var dataPath = Path.Combine(basePath, "data");
                if (Directory.Exists(dataPath))
                {
                    _services.Portrait.SetGameDataPath(dataPath);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Portrait service initialized with game data path");
                }
            }
        }

        /// <summary>
        /// XAML Click handler for View > Dialog Browser menu item (#1143).
        /// Delegates to DialogBrowserController.
        /// </summary>
        private void OnToggleDialogBrowserClick(object? sender, RoutedEventArgs e)
            => _controllers.DialogBrowser.OnToggleClick(sender, e);

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
            _controllers.SpeakerVisual.InitializeComboBoxes();

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
                var previousWidth = _services.UISettings.TreeViewTextMaxWidth;
                _services.UISettings.TreeViewTextMaxWidth = textWidth;

                // Only refresh if word wrap is enabled and width changed significantly
                if (_services.Settings.TreeViewWordWrap && Math.Abs(textWidth - previousWidth) > 10)
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

            // Unsubscribe from singleton events to prevent memory leaks (#1282)
            ThemeManager.Instance.ThemeApplied -= OnThemeApplied;
            _services.Settings.PropertyChanged -= OnSettingsPropertyChanged;
            DialogChangeEventBus.Instance.DialogChanged -= OnDialogChanged;

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
