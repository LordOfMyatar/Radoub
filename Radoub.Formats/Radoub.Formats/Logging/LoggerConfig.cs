namespace Radoub.Formats.Logging;

/// <summary>
/// Configuration for UnifiedLogger initialization.
/// Each application sets its own config at startup.
/// </summary>
public class LoggerConfig
{
    /// <summary>
    /// Application name - determines log directory.
    /// Example: "Parley" creates logs in ~/Radoub/Parley/Logs/
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    /// Minimum log level to output. Default is INFO.
    /// </summary>
    public LogLevel LogLevel { get; init; } = LogLevel.INFO;

    /// <summary>
    /// Number of session directories to retain. Default is 10.
    /// Older sessions are deleted on startup.
    /// </summary>
    public int RetainSessions { get; init; } = 10;

    /// <summary>
    /// Optional callback for debug console integration.
    /// Used by GUI apps to display logs in a debug panel.
    /// </summary>
    public Action<string>? DebugConsoleCallback { get; init; }
}
