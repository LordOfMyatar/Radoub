namespace Radoub.Formats.Logging;

/// <summary>
/// Unified logging service for all Radoub tools.
/// Provides dual output (console + file) with session-based organization and privacy controls.
///
/// Usage:
///   1. Call Configure() at app startup with LoggerConfig
///   2. Use component-specific methods (LogApplication, LogUI, etc.) or Log() directly
///   3. Logs go to ~/Radoub/{AppName}/Logs/Session_{timestamp}/
/// </summary>
public static class UnifiedLogger
{
    // Cold-start instrumentation (#2128). Started at static-init, which happens the first
    // time any UnifiedLogger member is touched — in practice SetAppName() at the very top of
    // Program.Main — so it approximates process start. Measure-only: LogStartupMilestone writes
    // the elapsed time to the session log for before/after profiling; nothing gates on it.
    private static readonly System.Diagnostics.Stopwatch StartupStopwatch =
        System.Diagnostics.Stopwatch.StartNew();

    private static string _appName = "Radoub";
    private static string _baseLogDirectory = string.Empty;
    private static LogLevel _currentLogLevel = LogLevel.INFO;
    private static Action<string>? _debugConsoleCallback;
    private static readonly string SessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
    private static string _sessionDirectory = string.Empty;
    private static bool _initialized = false;
    private static bool _configured = false;
    private static int _retainSessions = 10;

    // Per-file logging context (thread-local)
    [ThreadStatic]
    private static string? _currentFileName;

    /// <summary>
    /// Configure the logger for a specific application.
    /// Must be called once at application startup.
    /// </summary>
    public static void Configure(LoggerConfig config)
    {
        if (_configured)
        {
            LogApplication(LogLevel.WARN, "UnifiedLogger.Configure() called multiple times - ignoring");
            return;
        }

        _appName = config.AppName;
        _currentLogLevel = config.LogLevel;
        _retainSessions = config.RetainSessions;
        _debugConsoleCallback = config.DebugConsoleCallback;

        _baseLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", _appName, "Logs");
        _sessionDirectory = Path.Combine(_baseLogDirectory, $"Session_{SessionId}");

        _configured = true;

        // Session cleanup is NOT done here (#2647). Configure runs in Program.Main before
        // Avalonia starts, so a recursive session-tree delete here blocks first paint.
        // Tools call RunDeferredCleanup() after the window is open instead.
        EnsureInitialized();

        LogApplication(LogLevel.INFO, $"{_appName} logging initialized (level: {_currentLogLevel})");
    }

    /// <summary>
    /// Set the application name before full configuration.
    /// Call this before accessing RadoubSettings or any code that might trigger logging,
    /// so that any pre-configuration log output goes to the correct directory
    /// (~/Radoub/{AppName}/Logs/) instead of the default ~/Radoub/Radoub/Logs/.
    /// </summary>
    public static void SetAppName(string appName)
    {
        if (!_configured)
        {
            _appName = appName;
        }
    }

    /// <summary>
    /// Check if the logger has been configured.
    /// </summary>
    public static bool IsConfigured => _configured;

    /// <summary>
    /// Milliseconds elapsed since the logger's startup stopwatch began (approximately
    /// process start — see <see cref="StartupStopwatch"/>). Cold-start instrumentation
    /// for #2128; measure-only.
    /// </summary>
    public static long StartupElapsedMs => StartupStopwatch.ElapsedMilliseconds;

    /// <summary>
    /// Logs a named startup milestone with the elapsed time since process start (#2128).
    /// Emitted at INFO on the App channel with a stable <c>[Startup]</c> tag so a session
    /// log can be grepped for cold-start timings. Measure-only — nothing gates on it.
    /// </summary>
    public static void LogStartupMilestone(string milestone) =>
        LogApplication(LogLevel.INFO, $"[Startup] {milestone}: {StartupElapsedMs}ms since process start");

    // ========================================================================
    // Standard Component Channels (available to all apps)
    // ========================================================================

    public static void LogApplication(LogLevel level, string message)
        => Log(level, message, "Application", "App");

    public static void LogUI(LogLevel level, string message)
        => Log(level, message, "UI", "UI");

    public static void LogSettings(LogLevel level, string message)
        => Log(level, message, "Settings", "Settings");

    // ========================================================================
    // Parser/Format Channels (used by Radoub.Formats and apps)
    // ========================================================================

    public static void LogParser(LogLevel level, string message)
        => Log(level, message, "Parser", "Parser");

    public static void LogGff(LogLevel level, string message)
        => Log(level, message, "GFF", "GFF");

    public static void LogExport(LogLevel level, string message)
        => Log(level, message, "Export", "Export");

    // ========================================================================
    // App-Specific Channels (apps can add their own via Log())
    // ========================================================================

    public static void LogPlugin(LogLevel level, string message)
        => Log(level, message, "Plugin", "Plugin");

    public static void LogTheme(LogLevel level, string message)
        => Log(level, message, "Theme", "Theme");

    public static void LogJournal(LogLevel level, string message)
        => Log(level, message, "Journal", "Journal");

    public static void LogCreature(LogLevel level, string message)
        => Log(level, message, "Creature", "Creature");

    public static void LogInventory(LogLevel level, string message)
        => Log(level, message, "Inventory", "Inventory");

    public static void LogTrace(LogLevel level, string message)
        => Log(level, message, "Trace", "Trace");

    // ========================================================================
    // Core Logging
    // ========================================================================

    /// <summary>
    /// Log a message to a specific component channel.
    /// Use this for custom channels not covered by the standard methods.
    /// </summary>
    public static void Log(LogLevel level, string message, string component, string consolePrefix)
    {
        if (level > _currentLogLevel) return;

        EnsureInitialized();

        // Auto-sanitize paths in message
        var sanitizedMessage = PrivacyHelper.AutoSanitizeMessage(message);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().PadRight(5);
        var formattedMessage = $"[{timestamp}] [{levelStr}] [{consolePrefix}] {sanitizedMessage}";
        var consoleMessage = $"[{consolePrefix}] {levelStr}: {sanitizedMessage}";

        // Write to console (for VS debug window)
        Console.WriteLine(consoleMessage);

        // Send to debug console callback if set
        _debugConsoleCallback?.Invoke(consoleMessage);

        // Write to file
        try
        {
            // Per-file log if context is set, otherwise session-wide
            var logFileName = !string.IsNullOrEmpty(_currentFileName)
                ? $"{component}_{_currentFileName}_{SessionId}.log"
                : $"{component}_{SessionId}.log";
            var logPath = Path.Combine(_sessionDirectory, logFileName);

            File.AppendAllText(logPath, formattedMessage + Environment.NewLine);

            // Also write to session-wide log if we're in a file context
            if (!string.IsNullOrEmpty(_currentFileName))
            {
                var sessionWideLogFileName = $"{component}_{SessionId}.log";
                var sessionWideLogPath = Path.Combine(_sessionDirectory, sessionWideLogFileName);
                File.AppendAllText(sessionWideLogPath, formattedMessage + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logger] ERROR: Failed to write to log file: {ex.Message}");
        }
    }

    // ========================================================================
    // File Context (for per-file logging)
    // ========================================================================

    /// <summary>
    /// Set the current file context for per-file logging.
    /// Logs will go to both per-file and session-wide logs.
    /// </summary>
    public static void SetFileContext(string filePath)
    {
        _currentFileName = Path.GetFileNameWithoutExtension(filePath);
    }

    /// <summary>
    /// Get the current file context (for propagating across threads).
    /// </summary>
    public static string? GetFileContext() => _currentFileName;

    /// <summary>
    /// Clear the current file context.
    /// Logs go to session-wide files only.
    /// </summary>
    public static void ClearFileContext()
    {
        _currentFileName = null;
    }

    // ========================================================================
    // Configuration & Utilities
    // ========================================================================

    /// <summary>
    /// Change the log level at runtime.
    /// </summary>
    public static void SetLogLevel(LogLevel level)
    {
        _currentLogLevel = level;
        LogApplication(LogLevel.INFO, $"Log level set to {level}");
    }

    /// <summary>
    /// Get the current log level.
    /// </summary>
    public static LogLevel CurrentLogLevel => _currentLogLevel;

    /// <summary>
    /// Set callback for debug console integration.
    /// </summary>
    public static void SetDebugConsoleCallback(Action<string>? callback)
    {
        _debugConsoleCallback = callback;
    }

    /// <summary>
    /// Get the current session ID.
    /// </summary>
    public static string GetCurrentSessionId() => SessionId;

    /// <summary>
    /// Get the session log directory path.
    /// </summary>
    public static string GetSessionDirectory()
    {
        EnsureInitialized();
        return _sessionDirectory;
    }

    /// <summary>
    /// Creates a log file path in the session directory for external components.
    /// </summary>
    public static string CreateSessionLogPath(string logFileName)
    {
        EnsureInitialized();
        return Path.Combine(_sessionDirectory, logFileName);
    }

    // ========================================================================
    // Privacy Helpers (delegated to PrivacyHelper for convenience)
    // ========================================================================

    /// <summary>
    /// Sanitize a file path for logging - replaces home directory with ~
    /// </summary>
    public static string? SanitizePath(string? path) => PrivacyHelper.SanitizePath(path);

    // ========================================================================
    // Internal
    // ========================================================================

    private static void EnsureInitialized()
    {
        if (_initialized) return;

        // If not configured, use defaults (for library usage without app init)
        if (!_configured)
        {
            _baseLogDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub", _appName, "Logs");
            _sessionDirectory = Path.Combine(_baseLogDirectory, $"Session_{SessionId}");
        }

        try
        {
            Directory.CreateDirectory(_sessionDirectory);
            _initialized = true;

            // Log session start
            var sessionInfo = $"Session started: {SessionId}";
            var logPath = Path.Combine(_sessionDirectory, $"Application_{SessionId}.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO ] [App] {sessionInfo}{Environment.NewLine}");

            // Apply retention cleanup even for unconfigured loggers (e.g. test runs)
            if (!_configured)
            {
                CleanupOldSessions(_retainSessions);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Logger] ERROR: Failed to initialize session directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Cleans up old log sessions based on session count retention.
    /// Keeps the N most recent sessions and deletes older ones.
    /// </summary>
    /// <param name="retainSessionCount">Number of recent sessions to keep.</param>
    /// <param name="baseDirectory">Log root to sweep. Defaults to the configured session root.</param>
    /// <param name="protectedSessionDirectory">
    /// A session directory that must never be deleted regardless of age, and which does not
    /// consume a retention slot. Defaults to the live session (#2647): cleanup now runs
    /// concurrently with active logging rather than before it, so the directory currently
    /// open for appending has to be excluded explicitly.
    /// </param>
    public static void CleanupOldSessions(
        int retainSessionCount,
        string? baseDirectory = null,
        string? protectedSessionDirectory = null)
    {
        try
        {
            var logRoot = baseDirectory ?? _baseLogDirectory;
            var protectedDir = protectedSessionDirectory
                ?? (baseDirectory == null ? _sessionDirectory : null);

            if (!Directory.Exists(logRoot))
                return;

            var sessionDirs = Directory.GetDirectories(logRoot, "Session_*");
            var sessions = new List<(string Path, DateTime Timestamp)>();

            foreach (var sessionDir in sessionDirs)
            {
                try
                {
                    // Never sweep the live session — it is open for appending.
                    if (protectedDir != null &&
                        string.Equals(
                            Path.GetFullPath(sessionDir).TrimEnd(Path.DirectorySeparatorChar),
                            Path.GetFullPath(protectedDir).TrimEnd(Path.DirectorySeparatorChar),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
