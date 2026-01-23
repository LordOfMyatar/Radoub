using System.Text.Json.Serialization;

namespace Radoub.Formats.Logging;

/// <summary>
/// Shared logging settings that can be embedded in tool-specific settings.
/// Provides consistent logging configuration across all Radoub tools.
/// </summary>
public class LoggingSettings
{
    /// <summary>
    /// Default values for new instances.
    /// </summary>
    public static readonly int DefaultRetentionSessions = 3;
    public static readonly LogLevel DefaultLogLevel = LogLevel.INFO;

    /// <summary>
    /// Number of session log folders to retain (1-10).
    /// Older sessions are deleted on startup.
    /// </summary>
    public int LogRetentionSessions { get; set; } = DefaultRetentionSessions;

    /// <summary>
    /// Minimum log level to output.
    /// </summary>
    public LogLevel LogLevel { get; set; } = DefaultLogLevel;

    /// <summary>
    /// Clamps retention sessions to valid range (1-10).
    /// </summary>
    public void Normalize()
    {
        LogRetentionSessions = Math.Max(1, Math.Min(10, LogRetentionSessions));
    }

    /// <summary>
    /// Applies these settings to the UnifiedLogger.
    /// </summary>
    public void ApplyToLogger()
    {
        UnifiedLogger.SetLogLevel(LogLevel);
    }

    /// <summary>
    /// Creates a LoggerConfig for initialization.
    /// </summary>
    /// <param name="appName">The application name for the log directory.</param>
    /// <param name="debugCallback">Optional callback for debug console integration.</param>
    public LoggerConfig ToLoggerConfig(string appName, Action<string>? debugCallback = null)
    {
        return new LoggerConfig
        {
            AppName = appName,
            LogLevel = LogLevel,
            RetainSessions = LogRetentionSessions,
            DebugConsoleCallback = debugCallback
        };
    }
}

/// <summary>
/// Extended logging settings for tools that support a debug panel (e.g., Parley).
/// </summary>
public class ExtendedLoggingSettings : LoggingSettings
{
    /// <summary>
    /// Filter level for the debug window display.
    /// </summary>
    public LogLevel DebugLogFilterLevel { get; set; } = LogLevel.INFO;

    /// <summary>
    /// Whether the debug window/panel is visible.
    /// </summary>
    public bool DebugWindowVisible { get; set; } = false;
}
