# Diagnose Offset Mismatch - Export and Load lista.dlg
# Compares pre-calculated offsets vs actual write positions

$ErrorActionPreference = "Stop"

Write-Host "=== Offset Diagnostic Test ===" -ForegroundColor Cyan
Write-Host ""

# Paths
$modulePath = "~\Documents\Neverwinter Nights\modules\LNS_DLG"
$listaOriginal = Join-Path $modulePath "lista.dlg"
$listaExport = Join-Path $modulePath "lista01_offset_test.dlg"
$arcReactor = "d:\LOM\workspace\LNS_DLG\ArcReactor.Avalonia\bin\Debug\net9.0\ArcReactor.Avalonia.exe"
$logDir = "~\ArcReactor\Logs"

# Clear old logs
Write-Host "Clearing old logs..." -ForegroundColor Yellow
Remove-Item "$logDir\*.log" -Force -ErrorAction SilentlyContinue

Write-Host "Loading lista.dlg with ArcReactor..." -ForegroundColor Yellow
# Note: Can't automate GUI, so we'll just check the logs after user manually exports
Write-Host ""
Write-Host "MANUAL STEPS REQUIRED:" -ForegroundColor Red
Write-Host "1. Run ArcReactor.exe"
Write-Host "2. Open lista.dlg"
Write-Host "3. Save As -> lista01_offset_test.dlg"
Write-Host "4. Close ArcReactor"
Write-Host "5. Press ENTER to continue and analyze logs"
Write-Host ""
Read-Host "Press ENTER when done"

# Find most recent session log
$latestSessionLog = Get-ChildItem $logDir -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $latestSessionLog) {
    Write-Host "ERROR: No session logs found" -ForegroundColor Red
    exit 1
}

$parserLog = Get-ChildItem (Join-Path $latestSessionLog.FullName "*.log") | Where-Object { $_.Name -like "*Parser*" } | Select-Object -First 1

if (-not $parserLog) {
    Write-Host "ERROR: No parser log found" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Analyzing log: $($parserLog.FullName)" -ForegroundColor Cyan
Write-Host ""

# Extract pre-calculated offsets
Write-Host "=== PRE-CALCULATED OFFSETS (from CalculateListIndicesOffsets) ===" -ForegroundColor Green
Select-String -Path $parserLog.FullName -Pattern "ActionParams: PRE-CALC offset=(\d+), paramCount=(\d+)" | ForEach-Object {
    $offset = $_.Matches.Groups[1].Value
    $count = $_.Matches.Groups[2].Value
    Write-Host "  PRE-CALC: offset=$offset, count=$count"
}

Write-Host ""

# Extract actual write positions
Write-Host "=== ACTUAL WRITE POSITIONS (from WriteListIndices) ===" -ForegroundColor Green
Select-String -Path $parserLog.FullName -Pattern "ActionParams.*write.*at relative offset (\d+)" | ForEach-Object {
    $offset = $_.Matches.Groups[1].Value
    Write-Host "  ACTUAL WRITE: offset=$offset"
}

Write-Host ""

# Show relativePosition tracking
Write-Host "=== RELATIVE POSITION TRACKING ===" -ForegroundColor Green
Select-String -Path $parserLog.FullName -Pattern "DIAGNOSTIC:.*relative (position|offset) (now )?(\d+)" | ForEach-Object {
    Write-Host "  $_"
}

Write-Host ""
Write-Host "Check the values above to see if PRE-CALC offsets match ACTUAL WRITE offsets" -ForegroundColor Cyan
