using System;
using System.Collections.Generic;
using Avalonia.Controls;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages window lifecycle for singleton-style windows (one instance at a time).
    /// Centralizes window tracking, creation, and cleanup patterns from MainWindow.
    ///
    /// Issue #343 - Reduces 6+ individual window field declarations to single manager.
    ///
    /// Usage:
    /// 1. GetOrCreate - Get existing window or create new one
    ///    var settings = _windows.GetOrCreate("Settings", () => new SettingsWindow());
    ///
    /// 2. ShowOrActivate - Show window, creating if needed
    ///    _windows.ShowOrActivate("Settings", () => new SettingsWindow());
    ///
    /// 3. Close - Close specific window
    ///    _windows.Close("Settings");
    ///
    /// 4. CloseAll - Close all managed windows (for app shutdown)
    ///    _windows.CloseAll();
    /// </summary>
    public class WindowLifecycleManager
    {
        private readonly Dictionary<string, Window?> _windows = new();
        private readonly Dictionary<string, Action<Window>?> _closedCallbacks = new();

        /// <summary>
        /// Gets an existing window by key, or creates a new one if none exists.
        /// Automatically handles Closed event to clear the reference.
        /// </summary>
        /// <typeparam name="T">Window type</typeparam>
        /// <param name="key">Unique key for this window type</param>
        /// <param name="factory">Factory function to create window if needed</param>
        /// <param name="onClosed">Optional callback when window closes</param>
        /// <returns>The window instance</returns>
        public T GetOrCreate<T>(string key, Func<T> factory, Action<T>? onClosed = null) where T : Window
        {
            if (_windows.TryGetValue(key, out var existing) && existing is T typedExisting && typedExisting.IsVisible)
            {
                return typedExisting;
            }

            var window = factory();

            // Store closed callback for later invocation
            if (onClosed != null)
            {
                _closedCallbacks[key] = w => onClosed((T)w);
            }

            window.Closed += (s, e) =>
            {
                // Invoke custom closed callback if registered
                if (_closedCallbacks.TryGetValue(key, out var callback))
                {
                    callback?.Invoke(window);
                }
                _windows[key] = null;
            };

            _windows[key] = window;
            return window;
        }

        /// <summary>
        /// Shows an existing window or creates and shows a new one.
        /// If window exists and is visible, activates it instead.
        /// </summary>
        /// <typeparam name="T">Window type</typeparam>
        /// <param name="key">Unique key for this window type</param>
        /// <param name="factory">Factory function to create window if needed</param>
        /// <param name="onClosed">Optional callback when window closes</param>
        /// <returns>The window instance</returns>
        public T ShowOrActivate<T>(string key, Func<T> factory, Action<T>? onClosed = null) where T : Window
        {
            if (_windows.TryGetValue(key, out var existing) && existing is T typedExisting && typedExisting.IsVisible)
            {
                typedExisting.Activate();
                return typedExisting;
            }

            var window = GetOrCreate(key, factory, onClosed);
            window.Show();
            window.Activate();
            return window;
        }

        /// <summary>
        /// Gets an existing window by key without creating a new one.
        /// </summary>
        /// <typeparam name="T">Window type</typeparam>
        /// <param name="key">Unique key for this window type</param>
        /// <returns>The window instance, or null if not exists or not visible</returns>
        public T? Get<T>(string key) where T : Window
        {
            if (_windows.TryGetValue(key, out var window) && window is T typedWindow && typedWindow.IsVisible)
            {
                return typedWindow;
            }
            return null;
        }

        /// <summary>
        /// Checks if a window exists and is visible.
        /// </summary>
        public bool IsOpen(string key)
        {
            return _windows.TryGetValue(key, out var window) && window?.IsVisible == true;
        }

        /// <summary>
        /// Closes a specific window if it exists.
        /// </summary>
        /// <param name="key">Unique key for the window</param>
        /// <returns>True if window was closed, false if it didn't exist</returns>
        public bool Close(string key)
        {
            if (_windows.TryGetValue(key, out var window) && window != null)
            {
                window.Close();
                _windows[key] = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Closes all managed windows. Call this during app shutdown.
        /// </summary>
        public void CloseAll()
        {
            foreach (var kvp in _windows)
            {
                if (kvp.Value != null)
                {
                    try
                    {
                        kvp.Value.Close();
                    }
                    catch
                    {
                        // Ignore errors during shutdown
                    }
                }
            }
            _windows.Clear();
            _closedCallbacks.Clear();
        }

        /// <summary>
        /// Performs an action on a window if it exists and is visible.
        /// </summary>
        /// <typeparam name="T">Window type</typeparam>
        /// <param name="key">Unique key for this window type</param>
        /// <param name="action">Action to perform on the window</param>
        /// <returns>True if window existed and action was performed</returns>
        public bool WithWindow<T>(string key, Action<T> action) where T : Window
        {
            var window = Get<T>(key);
            if (window != null)
            {
                action(window);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Well-known window keys for MainWindow's managed windows.
    /// </summary>
    public static class WindowKeys
    {
        public const string Settings = "Settings";
        public const string Flowchart = "Flowchart";
        public const string SoundBrowser = "SoundBrowser";
        public const string ScriptBrowser = "ScriptBrowser";
        public const string ParameterBrowser = "ParameterBrowser";
    }
}
