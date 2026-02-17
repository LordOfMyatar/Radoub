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
using RadoubLauncher.Models;

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
    /// Path to the active compiler executable.
    /// </summary>
    public string? CompilerPath { get; private set; }

    /// <summary>
    /// Whether the compiler is available for use.
    /// </summary>
    public bool IsCompilerAvailable => !string.IsNullOrEmpty(CompilerPath) && File.Exists(CompilerPath);

    /// <summary>
    /// Whether the active compiler is a user-configured custom path or bundled.
    /// </summary>
    public bool IsCustomCompiler { get; private set; }

    /// <summary>
    /// Description of the active compiler source for display in UI.
    /// </summary>
    public string CompilerSourceDescription { get; private set; } = "Not found";

    private ScriptCompilerService()
    {
        DiscoverCompiler();
    }

    /// <summary>
    /// Re-run compiler discovery. Call after the user changes the custom compiler path.
    /// </summary>
    public void RefreshCompiler()
    {
        DiscoverCompiler();
    }

    private void DiscoverCompiler()
    {
        // Check user-configured custom path first
        var customPath = SettingsService.Instance.ScriptCompilerPath;
        if (!string.IsNullOrEmpty(customPath))
        {
            var expandedPath = PathHelper.ExpandPath(customPath);
            if (File.Exists(expandedPath))
            {
                CompilerPath = expandedPath;
                IsCustomCompiler = true;
                EnsureExecutablePermission(expandedPath);
                CompilerSourceDescription = $"Custom: {Path.GetFileName(expandedPath)}";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Using custom script compiler: {UnifiedLogger.SanitizePath(expandedPath)}");
                return;
            }

            UnifiedLogger.LogApplication(LogLevel.WARN, $"Custom compiler path not found: {UnifiedLogger.SanitizePath(expandedPath)} — falling back to bundled");
        }

        IsCustomCompiler = false;
        var exeDir = AppContext.BaseDirectory;
        var compilerName = GetPlatformCompilerName();

        // Look for bundled compiler in tools/ subfolder relative to executable
        var toolsPath = Path.Combine(exeDir, "tools", compilerName);
        if (File.Exists(toolsPath))
        {
            CompilerPath = toolsPath;
            EnsureExecutablePermission(toolsPath);
            CompilerSourceDescription = $"Bundled: {compilerName}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script compiler found: {compilerName}");
            return;
        }

        // Also check same directory as exe (for development)
        var sameDirPath = Path.Combine(exeDir, compilerName);
        if (File.Exists(sameDirPath))
        {
            CompilerPath = sameDirPath;
            EnsureExecutablePermission(sameDirPath);
            CompilerSourceDescription = $"Bundled: {compilerName}";
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Script compiler found in app directory: {compilerName}");
            return;
        }

        CompilerPath = null;
        CompilerSourceDescription = "Not found";
        UnifiedLogger.LogApplication(LogLevel.WARN, $"Script compiler not found ({compilerName}) - compilation disabled");
    }

    /// <summary>
    /// Ensure the compiler binary has executable permission on Linux/macOS.
    /// Git does not always preserve the execute bit, so we set it at runtime.
    /// </summary>
    private static void EnsureExecutablePermission(string path)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            return;

        try
        {
            // Check if already executable by looking at Unix file mode
            var fileInfo = new FileInfo(path);
            var mode = File.GetUnixFileMode(path);
            if (mode.HasFlag(UnixFileMode.UserExecute))
                return;

            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Set executable permission on {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to set executable permission: {ex.Message}");
        }
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
            var scriptName = Path.GetFileName(nssPath);

            if (!File.Exists(ncsPath))
            {
                // No .ncs file — flag only if the script has an uncommented entry point.
                // Include/library scripts often have "// void main() {}" as a test stub.
                if (HasUncommentedEntryPoint(nssPath))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Stale script (MissingNcs): {scriptName} — has entry point but no .ncs");
                    staleScripts.Add(new StaleScriptInfo
                    {
                        NssPath = nssPath,
                        NcsPath = ncsPath,
                        Reason = StaleReason.MissingNcs,
                        NssModified = nssInfo.LastWriteTime
                    });
                }
            }
            else
            {
                var ncsInfo = new FileInfo(ncsPath);
                if (nssInfo.LastWriteTime > ncsInfo.LastWriteTime)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"Stale script (SourceNewer): {scriptName} — .nss {nssInfo.LastWriteTime:HH:mm:ss} > .ncs {ncsInfo.LastWriteTime:HH:mm:ss}");
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

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Stale script check: {staleScripts.Count} stale of {nssFiles.Length} total .nss files");

        return staleScripts;
    }

    /// <summary>
    /// Check if a .nss file has an uncommented entry point (void main or int StartingConditional).
    /// Skips entry points inside // line comments and /* block comments */.
    /// Scripts without an entry point are include/library files and cannot be compiled standalone.
    /// </summary>
    public static bool HasUncommentedEntryPoint(string nssPath)
    {
        try
        {
            foreach (var rawLine in File.ReadLines(nssPath))
            {
                var line = rawLine.TrimStart();

                // Skip line comments
                if (line.StartsWith("//"))
                    continue;

                // Strip inline comments: everything after // on the line
                var commentIdx = line.IndexOf("//", StringComparison.Ordinal);
                if (commentIdx >= 0)
                    line = line[..commentIdx];

                if (line.Contains("void main", StringComparison.Ordinal)
                    || line.Contains("int StartingConditional", StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
        catch
        {
            return true; // If we can't read it, assume it has an entry point to be safe
        }
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
                // Best-effort process termination - ignore errors (process may have already exited)
                try { process.Kill(); } catch (Exception) { }
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
    /// Compile a specific list of scripts using a single compiler invocation.
    /// Uses -y (continue on error) and -j (parallel) for maximum throughput.
    /// </summary>
    /// <param name="scriptPaths">Paths to the .nss files to compile</param>
    /// <param name="progress">Progress callback (current, total, scriptName) — called once before compilation starts</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Batch compilation result</returns>
    public async Task<BatchCompilationResult> CompileScriptsAsync(
        List<string> scriptPaths,
        Action<int, int, string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var batchResult = new BatchCompilationResult();

        if (!IsCompilerAvailable)
        {
            batchResult.ErrorMessage = "Compiler not available";
            return batchResult;
        }

        batchResult.TotalScripts = scriptPaths.Count;

        if (scriptPaths.Count == 0)
        {
            batchResult.Success = true;
            return batchResult;
        }

        // Signal start of compilation
        progress?.Invoke(0, scriptPaths.Count, "starting...");

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Compiling {scriptPaths.Count} scripts (batch, parallel)...");

        var gamePath = PathHelper.ExpandPath(RadoubSettings.Instance.BaseGameInstallPath);

        // Build args: -c <files...> -y (continue on error) -j N (parallel)
        // -j requires an explicit thread count; omitting it causes the next flag to be parsed as the integer
        var threadCount = Math.Max(1, Environment.ProcessorCount);
        var args = new StringBuilder();
        args.Append("-c");
        foreach (var scriptPath in scriptPaths)
        {
            args.Append($" \"{scriptPath}\"");
        }
        args.Append($" -y -j {threadCount}");

        // Add game path for includes if available
        if (!string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath))
        {
            args.Append($" --root \"{gamePath}\"");
        }

        // Use the directory of the first script as working directory
        var workingDir = Path.GetDirectoryName(scriptPaths[0]) ?? ".";

        var startInfo = new ProcessStartInfo
        {
            FileName = CompilerPath,
            Arguments = args.ToString(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Running: {Path.GetFileName(CompilerPath!)} -c [{scriptPaths.Count} files] -y -j (cwd: {workingDir})");

        try
        {
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

            // Timeout: 30 seconds per script as upper bound, minimum 60 seconds
            var timeoutMs = Math.Max(60000, scriptPaths.Count * 30000);
            var completed = await Task.Run(() => process.WaitForExit(timeoutMs), cancellationToken);

            if (!completed)
            {
                try { process.Kill(); } catch (Exception) { }
                batchResult.ErrorMessage = "Compilation timed out";
                UnifiedLogger.LogApplication(LogLevel.ERROR, "Batch compilation timed out");
                return batchResult;
            }

            // Wait again to ensure async output handlers finish
            await Task.Run(() => process.WaitForExit(), cancellationToken);

            var outputText = output.ToString();
            var errorText = error.ToString();
            var exitCode = process.ExitCode;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Batch compile: exit={exitCode}, stdout={outputText.Length} chars, stderr={errorText.Length} chars");

            // Log raw stderr so we can diagnose parsing issues
            if (!string.IsNullOrWhiteSpace(errorText))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Compiler stderr:\n{errorText.TrimEnd()}");
            }
            if (!string.IsNullOrWhiteSpace(outputText))
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"Compiler stdout:\n{outputText.TrimEnd()}");
            }

            // Parse error output to find per-script failures
            // nwn_script_comp error formats observed:
            //   "scriptname.nss(line,col): Error: message"
            //   "scriptname.nss: Error: message"
            //   "Error: scriptname.nss(line,col): message"
            var failedScriptErrors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in errorText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // Try to extract script name from error line
                // Look for any .nss reference — handles paths, parens, etc.
                var nssIdx = trimmed.IndexOf(".nss", StringComparison.OrdinalIgnoreCase);
                if (nssIdx >= 0)
                {
                    // Extract everything up to and including .nss
                    var endOfName = nssIdx + 4;

                    // Walk backward from .nss to find start of filename
                    // Stop at space, colon, quote, or start of string
                    var nameStart = nssIdx - 1;
                    while (nameStart >= 0 && trimmed[nameStart] != ' ' && trimmed[nameStart] != '"'
                        && trimmed[nameStart] != '\'' && trimmed[nameStart] != ':')
                    {
                        nameStart--;
                    }
                    nameStart++;

                    var scriptFileName = trimmed[nameStart..endOfName];

                    // Clean up: remove path prefix, keep just the filename
                    scriptFileName = Path.GetFileName(scriptFileName);

                    if (!failedScriptErrors.ContainsKey(scriptFileName))
                        failedScriptErrors[scriptFileName] = new List<string>();
                    failedScriptErrors[scriptFileName].Add(trimmed);
                }
            }

            // Build per-script results
            foreach (var scriptPath in scriptPaths)
            {
                var scriptName = Path.GetFileName(scriptPath);
                var failed = failedScriptErrors.ContainsKey(scriptName);

                var result = new CompilationResult
                {
                    ScriptPath = scriptPath,
                    Success = !failed,
                    ExitCode = failed ? 1 : 0
                };

                if (failed)
                {
                    var errors = failedScriptErrors[scriptName];
                    result.ErrorMessage = errors.FirstOrDefault() ?? "Compilation failed";
                    result.ErrorOutput = string.Join("\n", errors);
                    batchResult.FailedScripts.Add(scriptName);
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed: {scriptName}: {result.ErrorMessage}");
                }
                else
                {
                    batchResult.SuccessCount++;
                }

                batchResult.Results.Add(result);
            }

            // Exit code is the authoritative indicator of success.
            // If the compiler returned non-zero but our stderr parsing didn't identify
            // specific failures, mark the whole batch as failed so we don't silently
            // report success when scripts actually failed to compile.
            if (exitCode != 0 && batchResult.FailedScripts.Count == 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Compiler exit code {exitCode} but no per-script errors parsed from stderr. Marking batch as failed.");

                // We can't identify which specific scripts failed, so check which
                // .ncs files were NOT created/updated as a secondary heuristic
                var anyIdentified = false;
                foreach (var scriptPath in scriptPaths)
                {
                    var ncsPath = Path.ChangeExtension(scriptPath, ".ncs");
                    var nssInfo = new FileInfo(scriptPath);
                    var ncsExists = File.Exists(ncsPath);
                    var ncsStale = ncsExists && new FileInfo(ncsPath).LastWriteTimeUtc < nssInfo.LastWriteTimeUtc;

                    if (!ncsExists || ncsStale)
                    {
                        // This script likely failed — no .ncs or .ncs is older than .nss
                        var scriptName = Path.GetFileName(scriptPath);
                        var existingResult = batchResult.Results.First(r => r.ScriptPath == scriptPath);
                        existingResult.Success = false;
                        existingResult.ExitCode = exitCode;
                        existingResult.ErrorMessage = !string.IsNullOrWhiteSpace(errorText)
                            ? errorText.Trim()
                            : "Compilation failed (compiler returned non-zero exit code)";
                        existingResult.ErrorOutput = errorText;

                        batchResult.FailedScripts.Add(scriptName);
                        batchResult.SuccessCount--;
                        anyIdentified = true;

                        UnifiedLogger.LogApplication(LogLevel.WARN,
                            $"Inferred failure: {scriptName} (no .ncs or stale .ncs)");
                    }
                }

                // If we still can't identify which scripts failed, mark all as failed
                if (!anyIdentified)
                {
                    UnifiedLogger.LogApplication(LogLevel.WARN,
                        "Cannot identify specific failures — marking all scripts as failed");
                    foreach (var result in batchResult.Results)
                    {
                        result.Success = false;
                        result.ExitCode = exitCode;
                        result.ErrorMessage = !string.IsNullOrWhiteSpace(errorText)
                            ? errorText.Trim()
                            : "Compilation failed (compiler returned non-zero exit code)";
                        result.ErrorOutput = errorText;
                    }
                    batchResult.FailedScripts.Clear();
                    batchResult.FailedScripts.AddRange(scriptPaths.Select(Path.GetFileName)!);
                    batchResult.SuccessCount = 0;
                }
            }

            batchResult.Success = exitCode == 0 && batchResult.FailedScripts.Count == 0;

            // Signal completion
            progress?.Invoke(scriptPaths.Count, scriptPaths.Count, "done");

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Batch compilation complete: {batchResult.SuccessCount}/{batchResult.TotalScripts} succeeded");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            batchResult.ErrorMessage = ex.Message;
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Batch compilation error: {ex.Message}");
        }

        return batchResult;
    }

}
