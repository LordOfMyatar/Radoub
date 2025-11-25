using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using Parley.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages window position, panel sizes, and UI state persistence.
    /// Handles save/restore of window geometry and screen boundary validation.
    /// Also handles startup tasks like loading command line files.
    /// Extracted from MainWindow.axaml.cs to separate persistence concerns.
    /// </summary>
    public class WindowPersistenceManager
    {
        private readonly Window _window;
        private readonly Func<string, Control?> _findControl;
        private bool _isRestoringPosition = false;

        public WindowPersistenceManager(
            Window window,
            Func<string, Control?> findControl)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
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
        /// Restores window position from settings with screen validation
        /// </summary>
        public async Task RestoreWindowPositionAsync()
        {
            _isRestoringPosition = true;
            var settings = SettingsService.Instance;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Restoring window position: Left={settings.WindowLeft}, Top={settings.WindowTop}, Current={_window.Position.X},{_window.Position.Y}");

            if (settings.WindowLeft >= 0 && settings.WindowTop >= 0)
            {
                var targetPos = new PixelPoint((int)settings.WindowLeft, (int)settings.WindowTop);

                // Validate position is on a visible screen
                if (IsPositionOnScreen(targetPos))
                {
                    _window.Position = targetPos;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Position restored to ({targetPos.X}, {targetPos.Y})");
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        $"Saved position ({targetPos.X}, {targetPos.Y}) is off-screen, using default");
                }
            }

            // Allow position saving after a short delay (to avoid saving the restore itself)
            await Task.Delay(500);
            _isRestoringPosition = false;
        }

        /// <summary>
        /// Saves current window position and size to settings
        /// </summary>
        public void SaveWindowPosition()
        {
            // Don't save position during initial restore
            if (_isRestoringPosition)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Position changed during restore, skipping save: ({_window.Position.X}, {_window.Position.Y})");
                return;
            }

            var settings = SettingsService.Instance;
            if (_window.Position.X >= 0 && _window.Position.Y >= 0)
            {
                settings.WindowLeft = _window.Position.X;
                settings.WindowTop = _window.Position.Y;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Saved window position: ({_window.Position.X}, {_window.Position.Y})");
            }

            // Width/Height already bound to settings with TwoWay, but ensure they're saved
            if (_window.Width > 0 && _window.Height > 0)
            {
                settings.WindowWidth = _window.Width;
                settings.WindowHeight = _window.Height;
            }
        }

        /// <summary>
        /// Checks if a position is visible on any screen
        /// </summary>
        private bool IsPositionOnScreen(PixelPoint position)
        {
            var screens = _window.Screens.All;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Checking position ({position.X}, {position.Y}) against {screens.Count} screens");

            foreach (var screen in screens)
            {
                var bounds = screen.Bounds;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  Screen: X={bounds.X}, Y={bounds.Y}, W={bounds.Width}, H={bounds.Height}, Primary={screen.IsPrimary}");

                // Check if the top-left corner is within screen bounds (with some tolerance)
                if (position.X >= bounds.X - 50 &&
                    position.X < bounds.X + bounds.Width &&
                    position.Y >= bounds.Y - 50 &&
                    position.Y < bounds.Y + bounds.Height)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "  Position is ON this screen");
                    return true;
                }
            }
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "  Position is OFF all screens");
            return false;
        }

        /// <summary>
        /// Restores panel sizes from settings
        /// </summary>
        public void RestorePanelSizes()
        {
            var settings = SettingsService.Instance;
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
                    SettingsService.Instance.LeftPanelWidth = leftPanelColumn.Width.Value;
                }

                if (topLeftPanelRow.Height.IsAbsolute)
                {
                    SettingsService.Instance.TopLeftPanelHeight = topLeftPanelRow.Height.Value;
                }
            }
        }

        /// <summary>
        /// Restores debug settings (log level filter, debug console visibility)
        /// </summary>
        public void RestoreDebugSettings()
        {
            // Initialize log level filter from saved settings
            var savedFilterLevel = SettingsService.Instance.DebugLogFilterLevel;
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
                var savedVisibility = SettingsService.Instance.DebugWindowVisible;
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
