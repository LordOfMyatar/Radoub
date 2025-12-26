using System;
using System.Collections.Generic;
using System.IO;

namespace CreatureEditor.Services;

/// <summary>
/// Log levels for filtering log output.
/// </summary>
public enum LogLevel
{
    ERROR = 0,
    WARN = 1,
    INFO = 2,
    DEBUG = 3,
    TRACE = 4
}

/// <summary>
/// Unified logging service for CreatureEditor.
/// Logs to ~/Radoub/CreatureEditor/Logs/ with session-based organization.
/// </summary>
public static class UnifiedLogger
{
    private static readonly string BaseLogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "CreatureEditor", "Logs");
    private static LogLevel _currentLogLevel = LogLevel.INFO;
    private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    private static readonly string SessionDirectory = Path.Combine(BaseLogDirectory, $"Session_{SessionId}");
    private static bool _initialized = false;

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

    public static void LogCreature(LogLevel level, string message)
    {
        Log(level, message, "Creature", "Creature");
    }

    public static void LogInventory(LogLevel level, string message)
    {
        Log(level, message, "Inventory", "Inventory");
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

    private static string AutoSanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrEmpty(userProfile))
            return message;

        if (message.Contains(userProfile, StringComparison.OrdinalIgnoreCase))
        {
            var startIndex = message.IndexOf(userProfile, StringComparison.OrdinalIgnoreCase);
            while (startIndex != -1)
            {
                var endIndex = startIndex + userProfile.Length;

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

                startIndex = message.IndexOf(userProfile, startIndex + sanitized.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        return message;
    }

    private static void Log(LogLevel level, string message, string component, string consolePrefix)
    {
        if (level > _currentLogLevel) return;

        EnsureInitialized();

        var sanitizedMessage = AutoSanitizeMessage(message);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().PadRight(5);
        var formattedMessage = $"[{timestamp}] [{levelStr}] [{consolePrefix}] {sanitizedMessage}";
        var consoleMessage = $"[{consolePrefix}] {levelStr}: {sanitizedMessage}";

        Console.WriteLine(consoleMessage);

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

    public static string GetCurrentSessionId() => SessionId;

    public static string GetSessionDirectory()
    {
        EnsureInitialized();
        return SessionDirectory;
    }

    public static void CleanupOldSessions(int retainSessionCount)
    {
        try
        {
            if (!Directory.Exists(BaseLogDirectory))
                return;

            var sessionDirs = Directory.GetDirectories(BaseLogDirectory, "Session_*");

            var sessions = new List<(string Path, DateTime Timestamp)>();

            foreach (var sessionDir in sessionDirs)
            {
                try
                {
                    var dirName = Path.GetFileName(sessionDir);
                    if (dirName.StartsWith("Session_") && dirName.Length >= 23)
                    {
                        var timestampPart = dirName.Substring(8);
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

            sessions.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

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
