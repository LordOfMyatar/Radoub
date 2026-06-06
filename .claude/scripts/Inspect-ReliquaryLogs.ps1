# Inspect recent Reliquary session logs for diagnostic patterns.
# Usage:
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/Inspect-ReliquaryLogs.ps1" -Sessions 3 -Pattern "startup file check|placeable|error"
param(
    [int]$Sessions = 3,
    [string]$Pattern = "startup file check|Loaded|placeable|error|warn|exception",
    [int]$Max = 20
)

$logRoot = Join-Path $env:USERPROFILE "Radoub\Reliquary\Logs"
if (-not (Test-Path $logRoot)) {
    Write-Host "No Reliquary log directory at $logRoot"
    exit 0
}

$logs = Get-ChildItem $logRoot -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First $Sessions
foreach ($log in $logs) {
    Write-Host "=== $($log.Name)  ($($log.LastWriteTime))  ==="
    $appLog = Get-ChildItem "$($log.FullName)\Application_*.log" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($appLog) {
        Get-Content $appLog.FullName |
            Select-String -Pattern $Pattern |
            Select-Object -First $Max |
            ForEach-Object { $_.Line }
    } else {
        Write-Host "  (no Application log)"
    }
    Write-Host ""
}
