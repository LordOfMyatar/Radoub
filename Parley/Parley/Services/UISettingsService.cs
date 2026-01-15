using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages UI-related settings (fonts, themes, flowchart layout, scrollbars).
    /// Extracted from SettingsService for single responsibility (#719).
    /// </summary>
    public class UISettingsService : INotifyPropertyChanged
    {
        public static UISettingsService Instance { get; } = new UISettingsService();

        // Font settings
        private double _fontSize = 14;
        private string _fontFamily = ""; // Empty string = use system default

        // Theme settings
        private bool _isDarkTheme = false; // DEPRECATED: Use CurrentThemeId instead
        private string _currentThemeId = "org.parley.theme.light"; // Default theme

        // Layout settings
        private string _flowchartLayout = "Floating"; // "Floating", "SideBySide", "Tabbed"

        // Scrollbar settings
        private bool _allowScrollbarAutoHide = false; // Default: always visible

        // Flowchart node display settings (#813)
        private int _flowchartNodeMaxLines = 3; // 1-6 lines, default 3

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        private UISettingsService()
        {
            // Initialized by SettingsService.LoadSettings()
        }

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(
            double fontSize,
            string fontFamily,
            bool isDarkTheme,
            string? currentThemeId,
            string flowchartLayout,
            bool allowScrollbarAutoHide,
            int flowchartNodeMaxLines = 3)
        {
            _fontSize = Math.Max(8, Math.Min(24, fontSize));
            _fontFamily = fontFamily ?? "";

            // Migrate from old IsDarkTheme to new CurrentThemeId
            if (!string.IsNullOrEmpty(currentThemeId))
            {
                _currentThemeId = currentThemeId;
                _isDarkTheme = isDarkTheme; // Keep for compatibility
            }
            else
            {
                // Old settings file - migrate
                _isDarkTheme = isDarkTheme;
                _currentThemeId = _isDarkTheme ? "org.parley.theme.dark" : "org.parley.theme.light";
            }

            _flowchartLayout = flowchartLayout ?? "Floating";
            _allowScrollbarAutoHide = allowScrollbarAutoHide;
            _flowchartNodeMaxLines = Math.Max(1, Math.Min(6, flowchartNodeMaxLines));
        }

        public double FontSize
        {
            get => _fontSize;
            set
            {
                if (SetProperty(ref _fontSize, Math.Max(8, Math.Min(24, value))))
                    SettingsChanged?.Invoke();
            }
        }

        public string FontFamily
        {
            get => _fontFamily;
            set
            {
                if (SetProperty(ref _fontFamily, value ?? ""))
                    SettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// DEPRECATED: Use CurrentThemeId instead. Kept for backwards compatibility.
        /// </summary>
        public bool IsDarkTheme
        {
            get => _isDarkTheme;
            set
            {
                if (SetProperty(ref _isDarkTheme, value))
                {
                    // Auto-migrate to new theme system
                    _currentThemeId = value ? "org.parley.theme.dark" : "org.parley.theme.light";
                    OnPropertyChanged(nameof(CurrentThemeId));
                    SettingsChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Current theme plugin ID (e.g., "org.parley.theme.light")
        /// </summary>
        public string CurrentThemeId
        {
            get => _currentThemeId;
            set
            {
                if (SetProperty(ref _currentThemeId, value))
                {
                    // Update legacy IsDarkTheme for compatibility
                    _isDarkTheme = value.Contains("dark", StringComparison.OrdinalIgnoreCase);
                    SettingsChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Flowchart layout mode: "Floating" (separate window), "SideBySide" (split view), "Tabbed" (tab in main area)
        /// </summary>
        public string FlowchartLayout
        {
            get => _flowchartLayout;
            set
            {
                // Validate value
                var validValues = new[] { "Floating", "SideBySide", "Tabbed" };
                var safeValue = validValues.Contains(value) ? value : "Floating";
                if (SetProperty(ref _flowchartLayout, safeValue))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogUI(LogLevel.INFO, $"Flowchart layout set to {safeValue}");
                }
            }
        }

        public bool AllowScrollbarAutoHide
        {
            get => _allowScrollbarAutoHide;
            set
            {
                if (SetProperty(ref _allowScrollbarAutoHide, value))
                {
                    SettingsChanged?.Invoke();
                    OnPropertyChanged(nameof(AllowScrollbarAutoHide));
                }
            }
        }

        /// <summary>
        /// Maximum lines to display in flowchart nodes before truncation (#813).
        /// Range: 1-6 lines, default 3.
        /// </summary>
        public int FlowchartNodeMaxLines
        {
            get => _flowchartNodeMaxLines;
            set
            {
                var clampedValue = Math.Max(1, Math.Min(6, value));
                if (SetProperty(ref _flowchartNodeMaxLines, clampedValue))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogUI(LogLevel.INFO, $"Flowchart node max lines set to {clampedValue}");
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
