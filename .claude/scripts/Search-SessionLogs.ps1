<#
.SYNOPSIS
    Searches session log files using regex patterns.

.DESCRIPTION
    Searches for regex patterns in log files from Session_* directories.
    Optimized for Claude Code use - outputs clean, parseable results.

    Default log location: ~/Radoub/{Tool}/Logs/

.PARAMETER Tool
    The Radoub tool to search logs for (Parley, Manifest, Quartermaster, Fence, Trebuchet).
    Defaults to Parley.

.PARAMETER Pattern
    Regex pattern to search for. Supports alternation (this|that|other).

.PARAMETER MostRecent
    Number of most recent Session_* directories to search. Defaults to 1.

.PARAMETER Context
    Number of lines of context before and after matches. Defaults to 0.

.PARAMETER CaseSensitive
    Enable case-sensitive matching. Default is case-insensitive.

.PARAMETER MaxResults
    Maximum number of matches to return. Defaults to 50.

.PARAMETER ListSessions
    List available sessions without searching.

.EXAMPLE
    .\Search-SessionLogs.ps1 -Pattern "focus|keyboard"
    Search for "focus" or "keyboard" in most recent Parley session.

.EXAMPLE
    .\Search-SessionLogs.ps1 -Tool Fence -Pattern "error" -MostRecent 3 -Context 2
    Search last 3 Fence sessions for "error" with 2 lines of context.

.EXAMPLE
    .\Search-SessionLogs.ps1 -ListSessions -MostRecent 5
    Show the 5 most recent Parley sessions.
#>

param(
    [ValidateSet("Parley", "Manifest", "Quartermaster", "Fence", "Trebuchet")]
    [string]$Tool = "Parley",

    [string]$Pattern,

    [int]$MostRecent = 1,

    [int]$Context = 0,

    [switch]$CaseSensitive,

    [int]$MaxResults = 50,

    [switch]$ListSessions
)

$ErrorActionPreference = "Stop"

# Build log path
$UserProfile = [Environment]::GetFolderPath("UserProfile")
$LogPath = Join-Path (Join-Path (Join-Path $UserProfile "Radoub") $Tool) "Logs"

if (-not (Test-Path $LogPath)) {
    Write-Error "Log path not found: $LogPath"
    exit 1
}

# Get Session directories
$Sessions = Get-ChildItem -Path $LogPath -Directory -Filter "Session_*" -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First $MostRecent

if (-not $Sessions) {
    Write-Error "No Session_* directories found in: $LogPath"
    exit 1
}

# List sessions mode
if ($ListSessions) {
    Write-Output "Recent $Tool sessions:"
    Write-Output ""
    foreach ($s in $Sessions) {
        $logCount = (Get-ChildItem -Path $s.FullName -Filter "*.log" -ErrorAction SilentlyContinue).Count
        $timestamp = $s.Name -replace "Session_", ""
        # Parse timestamp: YYYYMMDD_HHMMSS
        if ($timestamp -match "(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})") {
            $formatted = "$($Matches[1])-$($Matches[2])-$($Matches[3]) $($Matches[4]):$($Matches[5]):$($Matches[6])"
        } else {
            $formatted = $timestamp
        }
        Write-Output "  $($s.Name)  ($formatted)  [$logCount logs]"
    }
    exit 0
}

# Require pattern for search mode
if (-not $Pattern) {
    Write-Error "Pattern required. Use -Pattern 'regex' or -ListSessions to see available sessions."
    exit 1
}

# Search
$matchCount = 0
$results = @()

foreach ($session in $Sessions) {
    $logFiles = Get-ChildItem -Path $session.FullName -Filter "*.log" -ErrorAction SilentlyContinue |
                Sort-Object Name

    foreach ($logFile in $logFiles) {
        if ($matchCount -ge $MaxResults) { break }

        $selectParams = @{
            Path = $logFile.FullName
            Pattern = $Pattern
            CaseSensitive = $CaseSensitive.IsPresent
        }

        if ($Context -gt 0) {
            $selectParams.Context = $Context
        }

        $foundMatches = Select-String @selectParams

        foreach ($match in $foundMatches) {
            if ($matchCount -ge $MaxResults) { break }
            $matchCount++

            # Build clean output
            $relativePath = $logFile.Name
            $sessionName = $session.Name

            if ($Context -gt 0 -and $match.Context) {
                # With context, output block format
                $results += ""
                $results += "=== $sessionName/$relativePath :$($match.LineNumber) ==="

                foreach ($pre in $match.Context.PreContext) {
                    $results += "  $pre"
                }
                $results += "> $($match.Line)"
                foreach ($post in $match.Context.PostContext) {
                    $results += "  $post"
                }
            } else {
                # Without context, compact format
                $results += "$sessionName/$relativePath`:$($match.LineNumber): $($match.Line.Trim())"
            }
        }
    }
}

# Output results
if ($results.Count -eq 0) {
    Write-Output "No matches for pattern: $Pattern"
    Write-Output "Searched $($Sessions.Count) session(s) in $LogPath"
} else {
    Write-Output "Found $matchCount match(es) for: $Pattern"
    Write-Output ""
    $results | ForEach-Object { Write-Output $_ }

    if ($matchCount -ge $MaxResults) {
        Write-Output ""
        Write-Output "(Results truncated at $MaxResults - use -MaxResults to increase)"
    }
}
