#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Quick regression check for recently saved DLG files

.DESCRIPTION
    Compares original Aurora DLG files with ArcReactor exports using HexAnalysis.ps1
    Checks for critical regressions:
    - Tree structure (StartingList offset)
    - Field count match
    - Struct count match

.PARAMETER OriginalFile
    Path to original Aurora DLG file

.PARAMETER ExportedFile
    Path to ArcReactor exported DLG file

.EXAMPLE
    .\QuickRegressionCheck.ps1 -OriginalFile "lista.dlg" -ExportedFile "lista_test.dlg"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$OriginalFile,

    [Parameter(Mandatory=$true)]
    [string]$ExportedFile
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$hexAnalysisScript = Join-Path $scriptPath "HexAnalysis.ps1"

if (-not (Test-Path $hexAnalysisScript)) {
    Write-Host "ERROR: HexAnalysis.ps1 not found at: $hexAnalysisScript" -ForegroundColor Red
    exit 1
}

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Quick Regression Check" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

Write-Host "Original:  $OriginalFile"
Write-Host "Exported:  $ExportedFile`n"

# Run hex analysis
$output = & powershell -ExecutionPolicy Bypass -File $hexAnalysisScript -OriginalFile $OriginalFile -ExportedFile $ExportedFile 2>&1

# Parse critical metrics
$structCountMatch = $output | Select-String "StructCount: (\d+) -> (\d+)" | ForEach-Object {
    $_.Matches.Groups[1].Value -eq $_.Matches.Groups[2].Value
}

$fieldCountMatch = $output | Select-String "FieldCount: (\d+) -> (\d+)" | ForEach-Object {
    $_.Matches.Groups[1].Value -eq $_.Matches.Groups[2].Value
}

$startingListMatch = $output | Select-String "StartingList Count: (\d+) -> (\d+)" | ForEach-Object {
    $_.Matches.Groups[1].Value -eq $_.Matches.Groups[2].Value
}

# Display results
Write-Host "Critical Checks:" -ForegroundColor Cyan

if ($structCountMatch) {
    Write-Host "  ✅ Struct Count Match" -ForegroundColor Green
} else {
    Write-Host "  ❌ Struct Count Mismatch" -ForegroundColor Red
}

if ($fieldCountMatch) {
    Write-Host "  ✅ Field Count Match" -ForegroundColor Green
} else {
    Write-Host "  ❌ Field Count Mismatch" -ForegroundColor Red
}

if ($startingListMatch) {
    Write-Host "  ✅ StartingList Count Match" -ForegroundColor Green
} else {
    Write-Host "  ❌ StartingList Count Mismatch" -ForegroundColor Red
}

# Show full comparison
Write-Host "`nFull Comparison:" -ForegroundColor Cyan
$comparisonStart = $output | Select-String -Pattern "^.*COMPARISON" -CaseSensitive
if ($comparisonStart) {
    $startIndex = [array]::IndexOf($output, $comparisonStart.Line)
    if ($startIndex -ge 0) {
        $output[$startIndex..($output.Count-1)] | Where-Object { $_ -match "Header Differences|Struct Type Distribution|Type \d+|StartingList Count" }
    }
}

# Overall result
$allPassed = $structCountMatch -and $fieldCountMatch -and $startingListMatch

Write-Host "`n========================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "RESULT: ✅ REGRESSION CHECK PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "RESULT: ❌ REGRESSION DETECTED" -ForegroundColor Red
    exit 1
}
