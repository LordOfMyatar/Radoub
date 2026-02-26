using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using System;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages window position, panel sizes, and UI state persistence.
    /// Uses WindowPositionHelper from Radoub.UI for basic position save/restore (#1447).
    /// Adds Parley-specific panel persistence, debug settings, and startup handling.
    /// Extracted from MainWindow.axaml.cs to separate persistence concerns.
    /// </summary>
    public class WindowPersistenceManager
    {
        private readonly Window _window;
        private readonly Func<string, Control?> _findControl;
        private readonly ISettingsService _settings;
        private bool _isRestoringPosition = false;

        public WindowPersistenceManager(
            Window window,
            Func<string, Control?> findControl,
            ISettingsService settings)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Handle startup tasks including loading file from command line (Issue #9)
        /// </summary>
        public async Task HandleStartupFileAsync(MainViewModel viewModel)
        {
            var cmdLineFile = CommandLineService.Options.FilePath;
            if (!string.IsNullOrEmpty(cmdLineFile))
            {
                if (File.Exists(cmdLineFile))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading file from command line: {UnifiedLogger.SanitizePath(cmdLineFile)}");
                    await viewModel.LoadDialogAsync(cmdLineFile);
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Command line file not found: {UnifiedLogger.SanitizePath(cmdLineFile)}");
                }
            }
        }

        /// <summary>
        /// Restores window position from settings using shared WindowPositionHelper.
        /// Adds async screen validation with delay to prevent saving during restore.
        /// </summary>
        public async Task RestoreWindowPositionAsync()
        {
            _isRestoringPosition = true;

            WindowPositionHelper.Restore(_window, _settings, validateBounds: true);

            // Allow position saving after a short delay (to avoid saving the restore itself)
            await Task.Delay(500);
            _isRestoringPosition = false;
        }

        /// <summary>
        /// Saves current window position and size to settings using shared WindowPositionHelper.
        /// </summary>
        public void SaveWindowPosition()
        {
            if (_isRestoringPosition)
                return;

            WindowPositionHelper.Save(_window, _settings);
        }

        /// <summary>
        /// Restores panel sizes from settings
        /// </summary>
        public void RestorePanelSizes()
        {
            var settings = _settings;
            var mainContentGrid = _findControl("MainContentGrid") as Grid;

            if (mainContentGrid != null && mainContentGrid.ColumnDefinitions.Count > 0 && mainContentGrid.RowDefinitions.Count > 0)
            {
                // Column 0 is left panel (tree+text)
                mainContentGrid.ColumnDefinitions[0].Width = new GridLength(settings.LeftPanelWidth, GridUnitType.Pixel);

                // Row 0 is top panel (dialog tree)
                mainContentGrid.RowDefinitions[0].Height = new GridLength(settings.TopLeftPanelHeight, GridUnitType.Pixel);

                // Watch for splitter changes
                mainContentGrid.PropertyChanged += OnMainContentGridPropertyChanged;
            }

            // Restore dialog browser panel (#1143)
            RestoreDialogBrowserPanel();
        }

        /// <summary>
        /// Restores dialog browser panel state from settings (#1143)
        /// </summary>
        public void RestoreDialogBrowserPanel()
        {
            var settings = _settings;
            var outerContentGrid = _findControl("OuterContentGrid") as Grid;
            var dialogBrowserPanel = _findControl("DialogBrowserPanel") as Control;
            var dialogBrowserSplitter = _findControl("DialogBrowserSplitter") as Control;

            if (outerContentGrid != null && outerContentGrid.ColumnDefinitions.Count >= 2)
            {
                // Column 0 = dialog browser panel, Column 1 = splitter
                var dialogBrowserColumn = outerContentGrid.ColumnDefinitions[0];
                var dialogBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

                if (settings.DialogBrowserPanelVisible)
                {
                    dialogBrowserColumn.Width = new GridLength(settings.DialogBrowserPanelWidth, GridUnitType.Pixel);
                    dialogBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
                    if (dialogBrowserPanel != null) dialogBrowserPanel.IsVisible = true;
                    if (dialogBrowserSplitter != null) dialogBrowserSplitter.IsVisible = true;
                }
                else
                {
                    dialogBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    dialogBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    if (dialogBrowserPanel != null) dialogBrowserPanel.IsVisible = false;
                    if (dialogBrowserSplitter != null) dialogBrowserSplitter.IsVisible = false;
                }

                // Watch for splitter changes
                outerContentGrid.PropertyChanged += OnOuterContentGridPropertyChanged;
            }
        }

        /// <summary>
        /// Saves dialog browser panel size when outer grid layout changes
        /// </summary>
        private void OnOuterContentGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            SaveDialogBrowserPanelSize();
        }

        /// <summary>
        /// Saves dialog browser panel width to settings (#1143)
        /// </summary>
        public void SaveDialogBrowserPanelSize()
        {
            var outerContentGrid = _findControl("OuterContentGrid") as Grid;

            if (outerContentGrid != null && outerContentGrid.ColumnDefinitions.Count > 0)
            {
                var dialogBrowserColumn = outerContentGrid.ColumnDefinitions[0];

                if (dialogBrowserColumn.Width.IsAbsolute && dialogBrowserColumn.Width.Value > 0)
                {
                    _settings.DialogBrowserPanelWidth = dialogBrowserColumn.Width.Value;
                }
            }
        }

        /// <summary>
        /// Sets dialog browser panel visibility (#1143)
        /// </summary>
        public void SetDialogBrowserPanelVisible(bool visible)
        {
            var settings = _settings;
            settings.DialogBrowserPanelVisible = visible;

            var outerContentGrid = _findControl("OuterContentGrid") as Grid;
            var dialogBrowserPanel = _findControl("DialogBrowserPanel") as Control;
            var dialogBrowserSplitter = _findControl("DialogBrowserSplitter") as Control;

            if (outerContentGrid != null && outerContentGrid.ColumnDefinitions.Count >= 2)
            {
                var dialogBrowserColumn = outerContentGrid.ColumnDefinitions[0];
                var dialogBrowserSplitterColumn = outerContentGrid.ColumnDefinitions[1];

                if (visible)
                {
                    dialogBrowserColumn.Width = new GridLength(settings.DialogBrowserPanelWidth, GridUnitType.Pixel);
                    dialogBrowserSplitterColumn.Width = new GridLength(5, GridUnitType.Pixel);
                    if (dialogBrowserPanel != null) dialogBrowserPanel.IsVisible = true;
                    if (dialogBrowserSplitter != null) dialogBrowserSplitter.IsVisible = true;
                }
                else
                {
                    dialogBrowserColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    dialogBrowserSplitterColumn.Width = new GridLength(0, GridUnitType.Pixel);
                    if (dialogBrowserPanel != null) dialogBrowserPanel.IsVisible = false;
                    if (dialogBrowserSplitter != null) dialogBrowserSplitter.IsVisible = false;
                }
            }
        }

        /// <summary>
        /// Saves panel sizes when grid layout changes (splitters dragged)
        /// </summary>
        private void OnMainContentGridPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            SavePanelSizes();
        }

        /// <summary>
        /// Saves current panel sizes to settings
        /// </summary>
        public void SavePanelSizes()
        {
            var mainContentGrid = _findControl("MainContentGrid") as Grid;

            if (mainContentGrid != null && mainContentGrid.ColumnDefinitions.Count > 0 && mainContentGrid.RowDefinitions.Count > 0)
            {
                var leftPanelColumn = mainContentGrid.ColumnDefinitions[0];
                var topLeftPanelRow = mainContentGrid.RowDefinitions[0];

                if (leftPanelColumn.Width.IsAbsolute)
                {
                    _settings.LeftPanelWidth = leftPanelColumn.Width.Value;
                }

                if (topLeftPanelRow.Height.IsAbsolute)
                {
                    _settings.TopLeftPanelHeight = topLeftPanelRow.Height.Value;
                }
            }
        }

        /// <summary>
        /// Restores debug settings (log level filter, debug console visibility)
        /// </summary>
        public void RestoreDebugSettings()
        {
            // Initialize log level filter from saved settings
            var savedFilterLevel = _settings.DebugLogFilterLevel;
            DebugLogger.SetLogLevelFilter(savedFilterLevel);

            // Set ComboBox to match saved filter level
            var logLevelComboBox = _findControl("LogLevelFilterComboBox") as ComboBox;
            if (logLevelComboBox != null)
            {
                var selectedIndex = savedFilterLevel switch
                {
                    LogLevel.ERROR => 0,
                    LogLevel.WARN => 1,
                    LogLevel.INFO => 2,
                    LogLevel.DEBUG => 3,
                    LogLevel.TRACE => 4,
                    _ => 2 // Default to INFO
                };
                logLevelComboBox.SelectedIndex = selectedIndex;
            }

            // Restore debug window visibility from settings
            var debugTab = _findControl("DebugTab") as TabItem;
            if (debugTab != null)
            {
                var savedVisibility = _settings.DebugWindowVisible;
                debugTab.IsVisible = savedVisibility;

                // Update menu item text to match visibility state
                var showDebugMenuItem = _findControl("ShowDebugMenuItem") as MenuItem;
                if (showDebugMenuItem != null)
                {
                    showDebugMenuItem.Header = debugTab.IsVisible ? "Hide _Debug Console" : "Show _Debug Console";
                }
            }
        }

        /// <summary>
        /// Loads animation values into the animation ComboBox
        /// </summary>
        public void LoadAnimationValues()
        {
            try
            {
                var animationComboBox = _findControl("AnimationComboBox") as ComboBox;
                if (animationComboBox != null)
                {
                    animationComboBox.ItemsSource = Enum.GetValues(typeof(Models.DialogAnimation));
                    animationComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load animation values: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets whether currently restoring position (used to skip save during restore)
        /// </summary>
        public bool IsRestoringPosition => _isRestoringPosition;
    }
}
