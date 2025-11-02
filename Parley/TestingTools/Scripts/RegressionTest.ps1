#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Automated regression testing for DLG parser Aurora compatibility

.DESCRIPTION
    Tests round-trip Aurora compatibility by:
    1. Loading reference DLG files
    2. Saving them with ArcReactor
    3. Comparing hex output with HexAnalysis.ps1
    4. Reporting pass/fail for each test

.PARAMETER TestFilesPath
    Path to directory containing reference .dlg test files

.PARAMETER OutputPath
    Path to directory for saving test output files

.PARAMETER ArcReactorExe
    Path to ArcReactor.Avalonia.dll (for dotnet run)

.EXAMPLE
    .\RegressionTest.ps1 -TestFilesPath "TestingTools\TestFiles" -OutputPath "TestingTools\TestOutput"
#>

param(
    [string]$TestFilesPath = "TestingTools\TestFiles",
    [string]$OutputPath = "TestingTools\TestOutput",
    [string]$ArcReactorExe = "ArcReactor.Avalonia\bin\Debug\net9.0\ArcReactor.Avalonia.dll"
)

$ErrorActionPreference = "Stop"

# Colors for output
$Global:PassColor = "Green"
$Global:FailColor = "Red"
$Global:InfoColor = "Cyan"

function Write-TestHeader {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor $InfoColor
    Write-Host $Message -ForegroundColor $InfoColor
    Write-Host "========================================`n" -ForegroundColor $InfoColor
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Details = ""
    )

    $status = if ($Passed) { "✅ PASS" } else { "❌ FAIL" }
    $color = if ($Passed) { $PassColor } else { $FailColor }

    Write-Host "$status : $TestName" -ForegroundColor $color
    if ($Details) {
        Write-Host "        $Details" -ForegroundColor Gray
    }
}

function Test-DLGRoundTrip {
    param(
        [string]$OriginalFile,
        [string]$TestName
    )

    Write-Host "`nTesting: $TestName" -ForegroundColor $InfoColor
    Write-Host "  Original: $OriginalFile"

    # For now, this is a manual test - we need ArcReactor automation
    # This script will evolve as we add command-line support

    Write-Host "  Status: MANUAL TEST REQUIRED" -ForegroundColor Yellow
    Write-Host "  Instructions:"
    Write-Host "    1. Open $OriginalFile in ArcReactor"
    Write-Host "    2. Save As to $OutputPath"
    Write-Host "    3. Run HexAnalysis.ps1 to compare"

    return $false # Manual test, can't auto-pass
}

function Get-TestFiles {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Host "Test files path not found: $Path" -ForegroundColor $FailColor
        return @()
    }

    Get-ChildItem -Path $Path -Filter "*.dlg" | Where-Object {
        $_.Name -notlike "*test*" -and
        $_.Name -notlike "*export*" -and
        $_.Name -notlike "*_0*"
    }
}

# Main test execution
Write-TestHeader "DLG Parser Regression Testing"

Write-Host "Configuration:"
Write-Host "  Test Files: $TestFilesPath"
Write-Host "  Output Path: $OutputPath"
Write-Host "  ArcReactor: $ArcReactorExe"

# Create output directory
if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
    Write-Host "`nCreated output directory: $OutputPath" -ForegroundColor $InfoColor
}

# Get test files
$testFiles = Get-TestFiles -Path $TestFilesPath

if ($testFiles.Count -eq 0) {
    Write-Host "`nNo test files found in $TestFilesPath" -ForegroundColor $FailColor
    exit 1
}

Write-Host "`nFound $($testFiles.Count) test files"

# Test suite
$results = @()

foreach ($file in $testFiles) {
    $testName = $file.BaseName
    $passed = Test-DLGRoundTrip -OriginalFile $file.FullName -TestName $testName

    $results += [PSCustomObject]@{
        Name = $testName
        Passed = $passed
        File = $file.Name
    }
}

# Summary
Write-TestHeader "Test Summary"

$passCount = ($results | Where-Object { $_.Passed }).Count
$failCount = ($results | Where-Object { -not $_.Passed }).Count
$total = $results.Count

Write-Host "Total Tests: $total"
Write-Host "Passed: $passCount" -ForegroundColor $PassColor
Write-Host "Failed: $failCount" -ForegroundColor $FailColor

if ($failCount -gt 0) {
    Write-Host "`nFailed Tests:" -ForegroundColor $FailColor
    $results | Where-Object { -not $_.Passed } | ForEach-Object {
        Write-Host "  - $($_.Name)" -ForegroundColor $FailColor
    }
}

# Exit code
exit $failCount
