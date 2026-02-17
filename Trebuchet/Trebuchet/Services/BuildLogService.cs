using System;
using System.IO;
using System.Linq;
using System.Text;
using Radoub.Formats.Logging;
using RadoubLauncher.Models;

namespace RadoubLauncher.Services;

/// <summary>
/// Writes compilation results to log files in ~/Temp/Radoub/BuildLogs/.
/// </summary>
public static class BuildLogService
{
    /// <summary>
    /// Write compilation results to a log file.
    /// </summary>
    public static string WriteCompilationLog(BatchCompilationResult batchResult, string workingDirectory)
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "Radoub", "BuildLogs");
        Directory.CreateDirectory(logsDir);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logPath = Path.Combine(logsDir, $"build_{timestamp}.log");

        var log = new StringBuilder();
        log.AppendLine($"NWScript Compilation Log - {DateTime.Now}");
        log.AppendLine($"Working Directory: {UnifiedLogger.SanitizePath(workingDirectory)}");
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
