using System;
using System.Collections.Generic;

namespace RadoubLauncher.Models;

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
