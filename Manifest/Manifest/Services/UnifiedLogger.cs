using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Manifest.Services
{
    /// <summary>
    /// Unified logging service for Manifest.
    /// Logs to ~/Radoub/Manifest/Logs/ with session-based organization.
    /// Adapted from Parley's UnifiedLogger pattern.
    /// </summary>
    public static class UnifiedLogger
    {
        private static readonly string BaseLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Manifest", "Logs");
        private static LogLevel _currentLogLevel = LogLevel.INFO;
        private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        private static readonly string SessionDirectory = Path.Combine(BaseLogDirectory, $"Session_{SessionId}");
        private static bool _initialized = false;

        // Component-specific log files with session isolation
        public static void LogUI(LogLevel level, string message)
        {
            Log(level, message, "UI", "UI");
        }

        public static void LogApplication(LogLevel level, string message)
        {
            Log(level, message, "Application", "App");
        }

        public static void LogSettings(LogLevel level, string message)
        {
            Log(level, message, "Settings", "Settings");
        }

        public static void LogJournal(LogLevel level, string message)
        {
            Log(level, message, "Journal", "Journal");
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

        /// <summary>
        /// Automatically sanitizes paths in a message string
        /// </summary>
        private static string AutoSanitizeMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(userProfile))
                return message;

            // Simple replacement: if message contains the user profile path, sanitize it
            if (message.Contains(userProfile, StringComparison.OrdinalIgnoreCase))
            {
                var startIndex = message.IndexOf(userProfile, StringComparison.OrdinalIgnoreCase);
                while (startIndex != -1)
                {
                    var endIndex = startIndex + userProfile.Length;

                    // Find the end of the path (next space, quote, or end of string)
                    while (endIndex < message.Length &&
                           message[endIndex] != ' ' &&
                           message[endIndex] != '"' &&
                           message[endIndex] != '\'' &&
                           message[endIndex] != '\n' &&
                           message[endIndex] != '\r')
                    {
                        endIndex++;
                    }

                    var fullPath = message.Substring(startIndex, endIndex - startIndex);
                    var sanitized = SanitizePath(fullPath);
                    message = message.Substring(0, startIndex) + sanitized + message.Substring(endIndex);

                    // Look for next occurrence
                    startIndex = message.IndexOf(userProfile, startIndex + sanitized.Length, StringComparison.OrdinalIgnoreCase);
                }
            }

            return message;
        }

        private static void Log(LogLevel level, string message, string component, string consolePrefix)
        {
            if (level > _currentLogLevel) return;

            EnsureInitialized();

            // Automatically sanitize paths in message
            var sanitizedMessage = AutoSanitizeMessage(message);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var levelStr = level.ToString().PadRight(5);
            var formattedMessage = $"[{timestamp}] [{levelStr}] [{consolePrefix}] {sanitizedMessage}";
            var consoleMessage = $"[{consolePrefix}] {levelStr}: {sanitizedMessage}";

            // Write to console
            Console.WriteLine(consoleMessage);

            // Write to session-specific component log file
            try
            {
                var logFileName = $"{component}_{SessionId}.log";
                var logPath = Path.Combine(SessionDirectory, logFileName);
                File.AppendAllText(logPath, formattedMessage + Environment.NewLine);
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
                    catch
                    {
                        // Skip unparseable directories
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
                    catch
                    {
                        // Ignore deletion failures
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
