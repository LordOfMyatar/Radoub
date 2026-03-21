using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

public enum LockResult
{
    Acquired,
    AlreadyOwned,
    LockedByOther
}

public class LockInfo
{
    public int Pid { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
}

/// <summary>
/// Manages lock files for Radoub tools to prevent data corruption
/// when the same file is opened in multiple tool instances.
/// Lock files are JSON sidecars: {filename}.radoub.lock
/// </summary>
public static class FileSessionLockService
{
    private static readonly ConcurrentDictionary<string, bool> _ownedLocks = new(StringComparer.OrdinalIgnoreCase);

    public static string GetLockFilePath(string filePath) => filePath + ".radoub.lock";

    /// <summary>
    /// Attempt to acquire a lock for the given file.
    /// If a stale lock exists (process no longer running), it is cleaned up automatically.
    /// </summary>
    public static LockResult AcquireLock(string? filePath, string toolName)
    {
        if (string.IsNullOrEmpty(filePath))
            return LockResult.Acquired; // Don't block on empty paths

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (ArgumentException)
        {
            return LockResult.Acquired; // Invalid path, don't block
        }

        // Already owned by this process
        if (_ownedLocks.ContainsKey(normalizedPath))
            return LockResult.AlreadyOwned;

        // Check existing lock
        var lockPath = GetLockFilePath(normalizedPath);
        var existing = ReadLockFile(lockPath);
        if (existing != null)
        {
            if (IsLockHolderRunning(existing.Pid, existing.ProcessName))
                return LockResult.LockedByOther;

            // Stale lock — clean it up
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Cleaned up stale lock for {UnifiedLogger.SanitizePath(filePath)} (PID {existing.Pid} no longer running)");
            TryDeleteLockFile(lockPath);
        }

        // Write our lock
        var lockInfo = new LockInfo
        {
            Pid = Environment.ProcessId,
            ToolName = toolName,
            ProcessName = GetCurrentProcessName(),
            Timestamp = DateTime.UtcNow.ToString("O"),
            Machine = Environment.MachineName
        };

        try
        {
            var json = JsonSerializer.Serialize(lockInfo, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(lockPath, json);
            _ownedLocks[normalizedPath] = true;
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Acquired lock: {UnifiedLogger.SanitizePath(filePath)}");
            return LockResult.Acquired;
        }
        catch (IOException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to create lock file: {ex.Message}");
            return LockResult.Acquired; // Don't block editing if lock file can't be created
        }
        catch (UnauthorizedAccessException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to create lock file: {ex.Message}");
            return LockResult.Acquired;
        }
    }

    /// <summary>
    /// Release the lock for the given file.
    /// </summary>
    public static void ReleaseLock(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (ArgumentException)
        {
            return;
        }

        _ownedLocks.TryRemove(normalizedPath, out _);
        TryDeleteLockFile(GetLockFilePath(normalizedPath));
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Released lock: {UnifiedLogger.SanitizePath(filePath)}");
    }

    /// <summary>
    /// Release all locks owned by this process (call on app exit).
    /// </summary>
    public static void ReleaseAllLocks()
    {
        foreach (var path in _ownedLocks.Keys)
        {
            TryDeleteLockFile(GetLockFilePath(path));
        }
        _ownedLocks.Clear();
    }

    /// <summary>
    /// Check if a file is locked. Returns lock info if locked by another process, null if not.
    /// Stale locks (dead processes) are cleaned up automatically.
    /// </summary>
    public static LockInfo? CheckLock(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return null;

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var lockPath = GetLockFilePath(normalizedPath);
        var info = ReadLockFile(lockPath);
        if (info == null) return null;

        if (!IsLockHolderRunning(info.Pid, info.ProcessName))
        {
            TryDeleteLockFile(lockPath);
            return null;
        }

        return info;
    }

    private static LockInfo? ReadLockFile(string lockPath)
    {
        try
        {
            if (!File.Exists(lockPath)) return null;
            var json = File.ReadAllText(lockPath);
            return JsonSerializer.Deserialize<LockInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Check if the process that created the lock is still running.
    /// Uses both PID and process name to guard against PID reuse
    /// (especially on Linux where PIDs are recycled aggressively).
    /// </summary>
    private static bool IsLockHolderRunning(int pid, string? expectedProcessName)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return false;

            // If we have an expected process name, verify it matches.
            // This guards against PID reuse: if the PID now belongs to
            // a different process (e.g., "firefox" instead of "Parley"),
            // the lock is stale.
            if (!string.IsNullOrEmpty(expectedProcessName))
            {
                try
                {
                    var actualName = process.ProcessName;
                    if (!string.Equals(actualName, expectedProcessName, StringComparison.OrdinalIgnoreCase))
                    {
                        UnifiedLogger.LogApplication(LogLevel.INFO,
                            $"PID {pid} is running but process name mismatch: expected '{expectedProcessName}', got '{actualName}' — treating as stale lock");
                        return false;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process exited between checks
                    return false;
                }
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false; // Process doesn't exist
        }
        catch (InvalidOperationException)
        {
            return false; // Process has exited
        }
    }

    private static string GetCurrentProcessName()
    {
        try
        {
            return Process.GetCurrentProcess().ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryDeleteLockFile(string lockPath)
    {
        try { File.Delete(lockPath); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
