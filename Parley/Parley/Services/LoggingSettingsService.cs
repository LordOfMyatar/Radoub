using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages logging-related settings for Parley.
    /// Extracted from SettingsService for single responsibility (#1269).
    /// </summary>
    public class LoggingSettingsService : INotifyPropertyChanged
    {
        private int _logRetentionSessions = 3;
        private LogLevel _logLevel = LogLevel.INFO;
        private LogLevel _debugLogFilterLevel = LogLevel.INFO;
        private bool _debugWindowVisible = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(int logRetentionSessions, LogLevel logLevel, LogLevel debugLogFilterLevel, bool debugWindowVisible)
        {
            _logRetentionSessions = Math.Max(1, Math.Min(10, logRetentionSessions > 0 ? logRetentionSessions : 3));
            _logLevel = logLevel;
            _debugLogFilterLevel = debugLogFilterLevel;
            _debugWindowVisible = debugWindowVisible;
        }

        public int LogRetentionSessions
        {
            get => _logRetentionSessions;
            set
            {
                if (SetProperty(ref _logRetentionSessions, Math.Max(1, Math.Min(10, value))))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogSettings(LogLevel.INFO, $"Log retention set to {value} sessions");
                }
            }
        }

        public LogLevel CurrentLogLevel
        {
            get => _logLevel;
            set
            {
                if (SetProperty(ref _logLevel, value))
                {
                    UnifiedLogger.SetLogLevel(value);
                    SettingsChanged?.Invoke();
                }
            }
        }

        public LogLevel DebugLogFilterLevel
        {
            get => _debugLogFilterLevel;
            set
            {
                if (SetProperty(ref _debugLogFilterLevel, value))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogSettings(LogLevel.DEBUG, $"Debug log filter level set to {value}");
                }
            }
        }

        public bool DebugWindowVisible
        {
            get => _debugWindowVisible;
            set
            {
                if (SetProperty(ref _debugWindowVisible, value))
                {
                    SettingsChanged?.Invoke();
                    UnifiedLogger.LogSettings(LogLevel.DEBUG, $"Debug window visibility set to {value}");
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
