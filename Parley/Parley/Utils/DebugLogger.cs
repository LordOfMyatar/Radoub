using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Services;

namespace DialogEditor.Utils
{
    public static class DebugLogger
    {
        private static dynamic? _mainWindow;
        private static LogLevel _filterLevel = LogLevel.INFO; // Default filter
        private static List<(string message, LogLevel level)> _allMessages = new();

        public static void Initialize(object mainWindow)
        {
            _mainWindow = mainWindow;

            // Initialize UnifiedLogger callback for UI integration
            UnifiedLogger.SetDebugConsoleCallback(message =>
            {
                try
                {
                    if (message == null) return;

                    // Parse log level from message (format: "[Component] LEVEL: message")
                    var logLevel = ParseLogLevel(message);

                    // Store message with level
                    var timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
                    _allMessages.Add((timestampedMessage, logLevel));

                    // Keep only last 1000 messages
                    if (_allMessages.Count > 1000)
                    {
                        _allMessages.RemoveAt(0);
                    }

                    // Only send to UI if it passes the filter
                    if (ShouldShowMessage(logLevel) && _mainWindow != null)
                    {
                        _mainWindow.AddDebugMessage(timestampedMessage);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DebugLogger] ERROR in callback: {ex.Message}");
                }
            });
        }
        
        public static void Log(string message)
        {
            try
            {
                // Use UnifiedLogger for session-based organization
                UnifiedLogger.LogApplication(LogLevel.DEBUG, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG LOG ERROR] {ex.Message}");
            }
        }
        
        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} - {ex.Message}" : message;
            
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.ERROR, fullMessage);
        }
        
        public static void LogInfo(string message)
        {
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.INFO, message);
        }
        
        public static void LogWarning(string message)
        {
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.WARN, message);
        }
        
        public static void ClearDebugOutput()
        {
            try
            {
                _allMessages.Clear();
                _mainWindow?.ClearDebugOutput();
                Console.WriteLine("[Debug] Clear debug output requested");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG LOG ERROR] Failed to clear debug output: {ex.Message}");
            }
        }

        public static void SetLogLevelFilter(LogLevel filterLevel)
        {
            _filterLevel = filterLevel;
            RefreshDisplay();
        }

        private static LogLevel ParseLogLevel(string message)
        {
            // Parse log level from message (format: "[Component] LEVEL: message")
            // Note: LEVEL is padded to 5 chars (ERROR, "WARN ", "INFO ", DEBUG, TRACE)
            if (string.IsNullOrEmpty(message))
                return LogLevel.INFO;

            // Extract the level string between "] " and ":"
            var bracketEnd = message.IndexOf("] ");
            if (bracketEnd < 0)
                return LogLevel.INFO;

            var colonIndex = message.IndexOf(":", bracketEnd);
            if (colonIndex < 0)
                return LogLevel.INFO;

            var levelStr = message.Substring(bracketEnd + 2, colonIndex - (bracketEnd + 2)).Trim();

            // Parse the level string
            return levelStr switch
            {
                "ERROR" => LogLevel.ERROR,
                "WARN" => LogLevel.WARN,
                "INFO" => LogLevel.INFO,
                "DEBUG" => LogLevel.DEBUG,
                "TRACE" => LogLevel.TRACE,
                _ => LogLevel.INFO // default
            };
        }

        private static bool ShouldShowMessage(LogLevel messageLevel)
        {
            // Show messages at or above the selected filter level
            // ERROR=0, WARN=1, INFO=2, DEBUG=3, TRACE=4
            // So if filter is INFO (2), show ERROR (0), WARN (1), and INFO (2)
            return messageLevel <= _filterLevel;
        }

        private static void RefreshDisplay()
        {
            if (_mainWindow == null)
                return;

            try
            {
                // Clear current display
                _mainWindow.ClearDebugOutput();

                // Re-add filtered messages
                foreach (var (message, level) in _allMessages)
                {
                    if (ShouldShowMessage(level))
                    {
                        _mainWindow.AddDebugMessage(message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DebugLogger.RefreshDisplay] ERROR: {ex.Message}");
            }
        }
    }
}