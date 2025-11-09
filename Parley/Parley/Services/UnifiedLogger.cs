using System;
using System.Collections.Generic;
using System.IO;

namespace DialogEditor.Services
{
    public enum LogLevel
    {
        ERROR = 0,
        WARN = 1,
        INFO = 2,
        DEBUG = 3
    }

    public static class UnifiedLogger
    {
        private static readonly string BaseLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Parley", "Logs");
        private static LogLevel _currentLogLevel = LogLevel.INFO;
        private static Action<string>? _debugConsoleCallback;
        private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        private static readonly string SessionDirectory = Path.Combine(BaseLogDirectory, $"Session_{SessionId}");
        private static bool _initialized = false;

        // Per-file logging context
        [ThreadStatic]
        private static string? _currentFileName;

        // Component-specific log files with session isolation
        public static void LogParser(LogLevel level, string message)
        {
            Log(level, message, "Parser", "Parser");
        }

        public static void LogExport(LogLevel level, string message)
        {
            Log(level, message, "Export", "Export");
        }

        public static void LogGff(LogLevel level, string message)
        {
            Log(level, message, "GFF", "GFF");
        }

        public static void LogUI(LogLevel level, string message)
        {
            Log(level, message, "UI", "UI");
        }

        public static void LogApplication(LogLevel level, string message)
        {
            Log(level, message, "Application", "App");
        }
        
        public static void LogTheme(LogLevel level, string message)
        {
            Log(level, message, "Theme", "Theme");
        }
        
        public static void LogSettings(LogLevel level, string message)
        {
            Log(level, message, "Settings", "Settings");
        }

        public static void LogJournal(LogLevel level, string message)
        {
            Log(level, message, "Journal", "Journal");
        }

        public static void LogPlugin(LogLevel level, string message)
        {
            Log(level, message, "Plugin", "Plugin");
        }

        /// <summary>
        /// Set the current file context for per-file logging
        /// </summary>
        public static void SetFileContext(string filePath)
        {
            _currentFileName = Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// Get the current file context (for propagating across threads)
        /// </summary>
        public static string? GetFileContext()
        {
            return _currentFileName;
        }

        /// <summary>
        /// Clear the current file context (logs go to session-wide files)
        /// </summary>
        public static void ClearFileContext()
        {
            _currentFileName = null;
        }

        /// <summary>
        /// Sanitize file paths for logging - replace home directory with ~
        /// </summary>
        public static string SanitizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && path.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                return "~" + path.Substring(userProfile.Length);
            }

            return path;
        }

        private static void Log(LogLevel level, string message, string component, string consolePrefix)
        {
            if (level > _currentLogLevel) return;

            EnsureInitialized();

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().PadRight(5);
            var formattedMessage = $"[{timestamp}] [{levelStr}] [{consolePrefix}] {message}";
            var consoleMessage = $"[{consolePrefix}] {levelStr}: {message}";

            // Write to console (unified with file logging)
            Console.WriteLine(consoleMessage);
            
            // Also send to debug console if callback is set
            _debugConsoleCallback?.Invoke(consoleMessage);

            // Write to session-specific component log file
            try
            {
                // If a file context is set, create per-file log in addition to session-wide log
                var logFileName = !string.IsNullOrEmpty(_currentFileName)
                    ? $"{component}_{_currentFileName}_{SessionId}.log"
                    : $"{component}_{SessionId}.log";
                var logPath = Path.Combine(SessionDirectory, logFileName);

                File.AppendAllText(logPath, formattedMessage + Environment.NewLine);

                // Also write to session-wide log if we're in a file context
                if (!string.IsNullOrEmpty(_currentFileName))
                {
                    var sessionWideLogFileName = $"{component}_{SessionId}.log";
                    var sessionWideLogPath = Path.Combine(SessionDirectory, sessionWideLogFileName);
                    File.AppendAllText(sessionWideLogPath, formattedMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger] ERROR: Failed to write to log file: {ex.Message}");
            }
        }
        
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                try
                {
                    Directory.CreateDirectory(SessionDirectory);
                    _initialized = true;
                    
                    // Log session start
                    var sessionInfo = $"Session started: {SessionId}";
                    var logPath = Path.Combine(SessionDirectory, $"Application_{SessionId}.log");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO ] [App] {sessionInfo}{Environment.NewLine}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Logger] ERROR: Failed to initialize session directory: {ex.Message}");
                }
            }
        }

        public static void SetLogLevel(LogLevel level)
        {
            _currentLogLevel = level;
            LogApplication(LogLevel.INFO, $"Log level set to {level}");
        }

        // Set callback for debug console integration
        public static void SetDebugConsoleCallback(Action<string> callback)
        {
            _debugConsoleCallback = callback;
        }

        public static string GetCurrentSessionId()
        {
            return SessionId;
        }
        
        public static string GetSessionDirectory()
        {
            EnsureInitialized();
            return SessionDirectory;
        }
        
        /// <summary>
        /// Creates a log file in the session directory for external components
        /// </summary>
        public static string CreateSessionLogPath(string logFileName)
        {
            EnsureInitialized();
            return Path.Combine(SessionDirectory, logFileName);
        }

        /// <summary>
        /// Cleans up old log sessions based on session count retention.
        /// Keeps the N most recent sessions and deletes older ones.
        /// </summary>
        public static void CleanupOldSessions(int retainSessionCount)
        {
            try
            {
                if (!Directory.Exists(BaseLogDirectory))
                    return;

                var sessionDirs = Directory.GetDirectories(BaseLogDirectory, "Session_*");

                // Parse and sort sessions by timestamp (newest first)
                var sessions = new List<(string Path, DateTime Timestamp)>();

                foreach (var sessionDir in sessionDirs)
                {
                    try
                    {
                        var dirName = Path.GetFileName(sessionDir);
                        // Parse session timestamp from Session_yyyyMMdd_HHmmss format
                        if (dirName.StartsWith("Session_") && dirName.Length >= 23)
                        {
                            var timestampPart = dirName.Substring(8); // Skip "Session_"
                            if (DateTime.TryParseExact(timestampPart, "yyyyMMdd_HHmmss",
                                System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.None,
                                out DateTime sessionDate))
                            {
                                sessions.Add((sessionDir, sessionDate));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Logger] WARN: Failed to parse session directory {sessionDir}: {ex.Message}");
                    }
                }

                // Sort by timestamp descending (newest first)
                sessions.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

                // Delete sessions beyond retention count
                int deletedCount = 0;
                for (int i = retainSessionCount; i < sessions.Count; i++)
                {
                    try
                    {
                        Directory.Delete(sessions[i].Path, true);
                        deletedCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Logger] WARN: Failed to delete session directory {sessions[i].Path}: {ex.Message}");
                    }
                }

                if (deletedCount > 0)
                {
                    LogApplication(LogLevel.INFO, $"Cleaned up {deletedCount} old log session(s), keeping {retainSessionCount} most recent");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger] ERROR: Failed to cleanup old sessions: {ex.Message}");
            }
        }
    }
}