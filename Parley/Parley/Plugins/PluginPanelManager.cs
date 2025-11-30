using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Plugins.Services;
using DialogEditor.Services;
using DialogEditor.Views;

namespace DialogEditor.Plugins
{
    /// <summary>
    /// Manages plugin panel windows.
    /// Subscribes to PluginUIService events and creates/manages panel windows.
    /// Epic 3 / Issue #225
    /// </summary>
    public class PluginPanelManager : IDisposable
    {
        private readonly ConcurrentDictionary<string, PluginPanelWindow> _panelWindows = new();
        private readonly Window _ownerWindow;
        private bool _isDisposed;

        public PluginPanelManager(Window ownerWindow)
        {
            _ownerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));

            // Subscribe to panel events
            PluginUIService.PanelRegistered += OnPanelRegistered;
            PluginUIService.PanelClosed += OnPanelClosed;

            UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin panel manager initialized");
        }

        private void OnPanelRegistered(object? sender, PanelRegisteredEventArgs e)
        {
            // Only handle webview panels for now
            if (e.PanelInfo.RenderMode != "webview")
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN,
                    $"Panel {e.PanelInfo.FullPanelId} uses unsupported render mode: {e.PanelInfo.RenderMode}");
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                CreatePanelWindow(e.PanelInfo);
            });
        }

        private void OnPanelClosed(object? sender, PanelClosedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                ClosePanelWindow(e.FullPanelId);
            });
        }

        private void CreatePanelWindow(PanelInfo panelInfo)
        {
            if (_isDisposed) return;

            // Check if window already exists
            if (_panelWindows.ContainsKey(panelInfo.FullPanelId))
            {
                // Bring existing window to front
                if (_panelWindows.TryGetValue(panelInfo.FullPanelId, out var existingWindow))
                {
                    existingWindow.Activate();
                }
                return;
            }

            try
            {
                var window = new PluginPanelWindow(panelInfo);

                // Handle window closed event
                window.Closed += (s, e) =>
                {
                    _panelWindows.TryRemove(panelInfo.FullPanelId, out _);
                };

                // Position based on panel info
                switch (panelInfo.Position.ToLower())
                {
                    case "float":
                        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        break;
                    case "right":
                        // Position to the right of the main window
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Position = new Avalonia.PixelPoint(
                            (int)(_ownerWindow.Position.X + _ownerWindow.Width),
                            _ownerWindow.Position.Y);
                        break;
                    case "left":
                        // Position to the left of the main window
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Position = new Avalonia.PixelPoint(
                            (int)(_ownerWindow.Position.X - panelInfo.InitialWidth),
                            _ownerWindow.Position.Y);
                        break;
                    case "bottom":
                        // Position below the main window
                        window.WindowStartupLocation = WindowStartupLocation.Manual;
                        window.Position = new Avalonia.PixelPoint(
                            _ownerWindow.Position.X,
                            (int)(_ownerWindow.Position.Y + _ownerWindow.Height));
                        break;
                    default:
                        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        break;
                }

                _panelWindows[panelInfo.FullPanelId] = window;

                // Show as non-modal (users can interact with main window)
                window.Show();

                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Created panel window: {panelInfo.FullPanelId} - {panelInfo.Title}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Failed to create panel window: {ex.Message}");
            }
        }

        private void ClosePanelWindow(string fullPanelId)
        {
            if (_panelWindows.TryRemove(fullPanelId, out var window))
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Error closing panel window: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Reopen a closed panel window.
        /// </summary>
        public void ReopenPanel(PanelInfo panelInfo)
        {
            if (_isDisposed) return;

            Dispatcher.UIThread.Post(() =>
            {
                CreatePanelWindow(panelInfo);
            });
        }

        /// <summary>
        /// Get list of all registered panels that are currently closed.
        /// </summary>
        public IEnumerable<PanelInfo> GetClosedPanels()
        {
            return PluginUIService.GetAllRegisteredPanels()
                .Where(p => !_panelWindows.ContainsKey(p.FullPanelId));
        }

        /// <summary>
        /// Check if any panels are currently closed.
        /// </summary>
        public bool HasClosedPanels => GetClosedPanels().Any();

        /// <summary>
        /// Close all plugin panel windows.
        /// </summary>
        public void CloseAllPanels()
        {
            foreach (var kvp in _panelWindows)
            {
                try
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        kvp.Value.Close();
                    });
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Error closing panel {kvp.Key}: {ex.Message}");
                }
            }
            _panelWindows.Clear();
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            // Unsubscribe from events
            PluginUIService.PanelRegistered -= OnPanelRegistered;
            PluginUIService.PanelClosed -= OnPanelClosed;

            // Close all panel windows
            CloseAllPanels();

            GC.SuppressFinalize(this);
        }
    }
}
