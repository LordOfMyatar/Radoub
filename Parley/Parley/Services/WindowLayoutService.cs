using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages window layout and position settings (main window, flowchart window, panel sizes).
    /// Extracted from SettingsService for single responsibility (#719).
    /// </summary>
    public class WindowLayoutService : INotifyPropertyChanged
    {
        public static WindowLayoutService Instance { get; } = new WindowLayoutService();

        // Main window settings
        private double _windowLeft = 100;
        private double _windowTop = 100;
        private double _windowWidth = 1200;
        private double _windowHeight = 800;
        private bool _windowMaximized = false;

        // Panel layout settings - GridSplitter positions (#108)
        private double _leftPanelWidth = 800; // Tree+Text area width (default ~67% at 1200px window)
        private double _topLeftPanelHeight = 400; // Dialog tree height (default 2* of left panels)

        // Flowchart window settings (#377)
        private double _flowchartWindowLeft = 100;
        private double _flowchartWindowTop = 100;
        private double _flowchartWindowWidth = 800;
        private double _flowchartWindowHeight = 600;
        private bool _flowchartWindowOpen = false; // Was flowchart open when app closed?
        private double _flowchartPanelWidth = 400; // Width of embedded flowchart panel (SideBySide mode)
        private bool _flowchartVisible = false; // Is flowchart visible (any mode)?

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        private WindowLayoutService()
        {
            // Initialized by SettingsService.LoadSettings()
        }

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(
            double windowLeft,
            double windowTop,
            double windowWidth,
            double windowHeight,
            bool windowMaximized,
            double leftPanelWidth,
            double topLeftPanelHeight,
            double flowchartWindowLeft,
            double flowchartWindowTop,
            double flowchartWindowWidth,
            double flowchartWindowHeight,
            bool flowchartWindowOpen,
            double flowchartPanelWidth,
            bool flowchartVisible)
        {
            _windowLeft = windowLeft;
            _windowTop = windowTop;
            _windowWidth = Math.Max(400, windowWidth);
            _windowHeight = Math.Max(300, windowHeight);
            _windowMaximized = windowMaximized;

            _leftPanelWidth = Math.Max(350, leftPanelWidth);
            _topLeftPanelHeight = Math.Max(150, topLeftPanelHeight);

            _flowchartWindowLeft = flowchartWindowLeft;
            _flowchartWindowTop = flowchartWindowTop;
            _flowchartWindowWidth = Math.Max(200, flowchartWindowWidth);
            _flowchartWindowHeight = Math.Max(150, flowchartWindowHeight);
            _flowchartWindowOpen = flowchartWindowOpen;
            _flowchartPanelWidth = Math.Max(200, flowchartPanelWidth);
            _flowchartVisible = flowchartVisible;

            UnifiedLogger.LogUI(LogLevel.DEBUG, $"WindowLayoutService initialized: Main={_windowWidth}x{_windowHeight}, Flowchart={_flowchartWindowWidth}x{_flowchartWindowHeight}");
        }

        #region Main Window Properties

        public double WindowLeft
        {
            get => _windowLeft;
            set { if (SetProperty(ref _windowLeft, value)) SettingsChanged?.Invoke(); }
        }

        public double WindowTop
        {
            get => _windowTop;
            set { if (SetProperty(ref _windowTop, value)) SettingsChanged?.Invoke(); }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set { if (SetProperty(ref _windowWidth, Math.Max(400, value))) SettingsChanged?.Invoke(); }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set { if (SetProperty(ref _windowHeight, Math.Max(300, value))) SettingsChanged?.Invoke(); }
        }

        public bool WindowMaximized
        {
            get => _windowMaximized;
            set { if (SetProperty(ref _windowMaximized, value)) SettingsChanged?.Invoke(); }
        }

        #endregion

        #region Panel Layout Properties

        public double LeftPanelWidth
        {
            get => _leftPanelWidth;
            set { if (SetProperty(ref _leftPanelWidth, Math.Max(350, value))) SettingsChanged?.Invoke(); }
        }

        public double TopLeftPanelHeight
        {
            get => _topLeftPanelHeight;
            set { if (SetProperty(ref _topLeftPanelHeight, Math.Max(150, value))) SettingsChanged?.Invoke(); }
        }

        #endregion

        #region Flowchart Window Properties (#377)

        public double FlowchartWindowLeft
        {
            get => _flowchartWindowLeft;
            set { if (SetProperty(ref _flowchartWindowLeft, value)) SettingsChanged?.Invoke(); }
        }

        public double FlowchartWindowTop
        {
            get => _flowchartWindowTop;
            set { if (SetProperty(ref _flowchartWindowTop, value)) SettingsChanged?.Invoke(); }
        }

        public double FlowchartWindowWidth
        {
            get => _flowchartWindowWidth;
            set { if (SetProperty(ref _flowchartWindowWidth, Math.Max(200, value))) SettingsChanged?.Invoke(); }
        }

        public double FlowchartWindowHeight
        {
            get => _flowchartWindowHeight;
            set { if (SetProperty(ref _flowchartWindowHeight, Math.Max(150, value))) SettingsChanged?.Invoke(); }
        }

        public bool FlowchartWindowOpen
        {
            get => _flowchartWindowOpen;
            set { if (SetProperty(ref _flowchartWindowOpen, value)) SettingsChanged?.Invoke(); }
        }

        public double FlowchartPanelWidth
        {
            get => _flowchartPanelWidth;
            set { if (SetProperty(ref _flowchartPanelWidth, Math.Max(200, value))) SettingsChanged?.Invoke(); }
        }

        public bool FlowchartVisible
        {
            get => _flowchartVisible;
            set { if (SetProperty(ref _flowchartVisible, value)) SettingsChanged?.Invoke(); }
        }

        #endregion

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
