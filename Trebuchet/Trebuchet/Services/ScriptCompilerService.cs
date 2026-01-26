using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;

namespace RadoubLauncher.Services;

/// <summary>
/// Service for compiling NWScript files using nwn_script_comp.exe from neverwinter.nim.
/// </summary>
public class ScriptCompilerService
{
    private static ScriptCompilerService? _instance;
    private static readonly object _lock = new();

    public static ScriptCompilerService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ScriptCompilerService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Path to the bundled compiler executable.
    /// </summary>
    public string? CompilerPath { get; private set; }

    /// <summary>
    /// Whether the compiler is available for use.
    /// </summary>
    public bool IsCompilerAvailable => !string.IsNullOrEmpty(CompilerPath) && File.Exists(CompilerPath);

    private ScriptCompilerService()
    {
        DiscoverCompiler();
    }

    private void DiscoverCompiler()
    {
        var exeDir = AppContext.BaseDirectory;

        // Determine platform-specific executable name
        var compilerName = GetPlatformCompilerName();

        // Look for bundled compiler in tools/ subfolder relative to executable
        var toolsPath = Path.Combine(exeDir, "tools", compilerName);
        if (File.Exists(toolsPath))
        {
            CompilerPath = toolsPath;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script compiler found: {compilerName}");
            return;
        }

        // Also check same directory as exe (for development)
        var sameDirPath = Path.Combine(exeDir, compilerName);
        if (File.Exists(sameDirPath))
        {
            CompilerPath = sameDirPath;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script compiler found in app directory: {compilerName}");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.WARN, $"Script compiler not found ({compilerName}) - compilation disabled");
    }

    /// <summary>
    /// Get the platform-specific compiler executable name.
    /// </summary>
    private static string GetPlatformCompilerName()
    {
        if (OperatingSystem.IsWindows())
            return "nwn_script_comp.exe";
        else if (OperatingSystem.IsMacOS())
            return "nwn_script_comp_macos";
        else if (OperatingSystem.IsLinux())
            return "nwn_script_comp_linux";
        else
            return "nwn_script_comp";  // Fallback
    }

    /// <summary>
    /// Find all .nss files in a directory that have outdated or missing .ncs files.
    /// </summary>
    /// <param name="workingDirectory">The module working directory</param>
    /// <returns>List of .nss files that need compilation</returns>
    public List<StaleScriptInfo> FindStaleScripts(string workingDirectory)
    {
        var staleScripts = new List<StaleScriptInfo>();

        // Ensure path is expanded (~ -> user home)
        workingDirectory = PathHelper.ExpandPath(workingDirectory);

        if (!Directory.Exists(workingDirectory))
            return staleScripts;

        var nssFiles = Directory.GetFiles(workingDirectory, "*.nss", SearchOption.TopDirectoryOnly);

        foreach (var nssPath in nssFiles)
        {
            var ncsPath = Path.ChangeExtension(nssPath, ".ncs");
            var nssInfo = new FileInfo(nssPath);

            if (!File.Exists(ncsPath))
            {
                // No compiled file exists
                staleScripts.Add(new StaleScriptInfo
                {
                    NssPath = nssPath,
                    NcsPath = ncsPath,
                    Reason = StaleReason.MissingNcs,
                    NssModified = nssInfo.LastWriteTime
                });
            }
            else
            {
                var ncsInfo = new FileInfo(ncsPath);
                if (nssInfo.LastWriteTime > ncsInfo.LastWriteTime)
                {
                    // Source is newer than compiled
                    staleScripts.Add(new StaleScriptInfo
                    {
                        NssPath = nssPath,
                        NcsPath = ncsPath,
                        Reason = StaleReason.SourceNewer,
                        NssModified = nssInfo.LastWriteTime,
                        NcsModified = ncsInfo.LastWriteTime
                    });
                }
            }
        }

        return staleScripts;
    }

    /// <summary>
    /// Compile a single script file.
    /// </summary>
    /// <param name="nssPath">Path to the .nss file</param>
    /// <param name="gamePath">Optional path to NWN installation for includes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Compilation result</returns>
    public async Task<CompilationResult> CompileScriptAsync(
        string nssPath,
        string? gamePath = null,
        CancellationToken cancellationToken = default)
    {
        // Ensure paths are expanded (~ -> user home)
        nssPath = PathHelper.ExpandPath(nssPath);
        gamePath = PathHelper.ExpandPath(gamePath ?? "");
        if (string.IsNullOrEmpty(gamePath)) gamePath = null;

        if (!IsCompilerAvailable)
        {
            return new CompilationResult
            {
                Success = false,
                ScriptPath = nssPath,
                ErrorMessage = "Compiler not available"
            };
        }

        var result = new CompilationResult
        {
            ScriptPath = nssPath
        };

        try
        {
            var args = new StringBuilder();
            args.Append("-c ");
            args.Append($"\"{nssPath}\"");

            // Add game path for includes if available (--root for NWN installation)
            if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
            {
                args.Append($" --root \"{gamePath}\"");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = CompilerPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(nssPath) ?? "."
            };

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Running: {CompilerPath} {args} (cwd: {startInfo.WorkingDirectory})");

            using var process = new Process { StartInfo = startInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Wait for completion with timeout
            var timeoutMs = 30000; // 30 seconds per script
            var completed = await Task.Run(() => process.WaitForExit(timeoutMs), cancellationToken);

            if (!completed)
            {
                try { process.Kill(); } catch { }
                result.Success = false;
                result.ErrorMessage = "Compilation timed out";
                return result;
            }

            // Wait again without timeout to ensure async output handlers finish
            // This is required when using BeginOutputReadLine/BeginErrorReadLine
            await Task.Run(() => process.WaitForExit(), cancellationToken);

            result.Output = output.ToString();
            result.ErrorOutput = error.ToString();
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;

            // Debug: log compilation result
            var scriptName = Path.GetFileName(nssPath);
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Compile {scriptName}: exit={result.ExitCode}, success={result.Success}, stderr={error.ToString().Length} chars");

            if (!result.Success && string.IsNullOrEmpty(result.ErrorMessage))
            {
                result.ErrorMessage = !string.IsNullOrWhiteSpace(error.ToString())
                    ? error.ToString().Trim()
                    : "Compilation failed";
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Script compilation error: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Compile all scripts in a directory.
    /// </summary>
    /// <param name="workingDirectory">The module working directory</param>
    /// <param name="compileAll">If true, compile all scripts. If false, only compile stale scripts.</param>
    /// <param name="progress">Progress callback (current, total, scriptName)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch compilation result</returns>
    public async Task<BatchCompilationResult> CompileAllScriptsAsync(
        string workingDirectory,
        bool compileAll = false,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchCompilationResult();

        // Ensure path is expanded (~ -> user home)
        workingDirectory = PathHelper.ExpandPath(workingDirectory);

        if (!IsCompilerAvailable)
        {
            batchResult.ErrorMessage = "Compiler not available";
            return batchResult;
        }

        var gamePath = PathHelper.ExpandPath(RadoubSettings.Instance.BaseGameInstallPath);

        // Get scripts to compile
        List<string> scriptsToCompile;
        if (compileAll)
        {
            scriptsToCompile = Directory.GetFiles(workingDirectory, "*.nss", SearchOption.TopDirectoryOnly).ToList();
        }
        else
        {
            var staleScripts = FindStaleScripts(workingDirectory);
            scriptsToCompile = staleScripts.Select(s => s.NssPath).ToList();
        }

        batchResult.TotalScripts = scriptsToCompile.Count;

        if (scriptsToCompile.Count == 0)
        {
            batchResult.Success = true;
            return batchResult;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Compiling {scriptsToCompile.Count} scripts...");

        for (int i = 0; i < scriptsToCompile.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var scriptPath = scriptsToCompile[i];
            var scriptName = Path.GetFileName(scriptPath);

            progress?.Invoke(i + 1, scriptsToCompile.Count, scriptName);

            var result = await CompileScriptAsync(scriptPath, gamePath, cancellationToken);
            batchResult.Results.Add(result);

            // Always log the result for each script at INFO level
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Compiled {scriptName}: exit={result.ExitCode}, success={result.Success}");

            if (result.Success)
            {
                batchResult.SuccessCount++;
            }
            else
            {
                batchResult.FailedScripts.Add(scriptName);
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Failed to compile {scriptName}: {result.ErrorMessage}");
            }
        }

        batchResult.Success = batchResult.FailedScripts.Count == 0;

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Compilation complete: {batchResult.SuccessCount}/{batchResult.TotalScripts} succeeded");

        return batchResult;
    }

    /// <summary>
    /// Write compilation results to a log file.
    /// </summary>
    public string WriteCompilationLog(BatchCompilationResult batchResult, string workingDirectory)
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "Trebuchet", "logs");

        Directory.CreateDirectory(logsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logPath = Path.Combine(logsDir, $"build_{timestamp}.log");

        var log = new StringBuilder();
        log.AppendLine($"NWScript Compilation Log - {DateTime.Now}");
        log.AppendLine($"Working Directory: {workingDirectory}");
        log.AppendLine($"Total Scripts: {batchResult.TotalScripts}");
        log.AppendLine($"Succeeded: {batchResult.SuccessCount}");
        log.AppendLine($"Failed: {batchResult.FailedScripts.Count}");
        log.AppendLine();

        if (batchResult.FailedScripts.Count > 0)
        {
            log.AppendLine("=== FAILED SCRIPTS ===");
            log.AppendLine();

            foreach (var result in batchResult.Results.Where(r => !r.Success))
            {
                log.AppendLine($"--- {Path.GetFileName(result.ScriptPath)} ---");
                log.AppendLine($"Error: {result.ErrorMessage}");
                if (!string.IsNullOrWhiteSpace(result.ErrorOutput))
                {
                    log.AppendLine("Output:");
                    log.AppendLine(result.ErrorOutput);
                }
                log.AppendLine();
            }
        }

        File.WriteAllText(logPath, log.ToString());
        UnifiedLogger.LogApplication(LogLevel.INFO, $"Build log written to: {UnifiedLogger.SanitizePath(logPath)}");

        return logPath;
    }
}

/// <summary>
/// Information about a script that needs recompilation.
/// </summary>
public class StaleScriptInfo
{
    public string NssPath { get; set; } = "";
    public string NcsPath { get; set; } = "";
    public StaleReason Reason { get; set; }
    public DateTime NssModified { get; set; }
    public DateTime? NcsModified { get; set; }
}

/// <summary>
/// Reason why a script is considered stale.
/// </summary>
public enum StaleReason
{
    MissingNcs,
    SourceNewer
}

/// <summary>
/// Result of compiling a single script.
/// </summary>
public class CompilationResult
{
    public bool Success { get; set; }
    public string ScriptPath { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public string? Output { get; set; }
    public string? ErrorOutput { get; set; }
    public int ExitCode { get; set; }
}

/// <summary>
/// Result of compiling multiple scripts.
/// </summary>
public class BatchCompilationResult
{
    public bool Success { get; set; }
    public int TotalScripts { get; set; }
    public int SuccessCount { get; set; }
    public List<string> FailedScripts { get; set; } = new();
    public List<CompilationResult> Results { get; set; } = new();
    public string? ErrorMessage { get; set; }
}
