# Run all Radoub test projects
# Usage: .\run-tests.ps1 [-UIOnly] [-UnitOnly]

param(
    [switch]$UIOnly,
    [switch]$UnitOnly
)

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputDir = "Radoub.UITests\TestOutput"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Radoub Test Suite" -ForegroundColor Cyan
Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

$totalPassed = 0
$totalFailed = 0
$results = @()

# Unit/Headless Tests (fast, no UI required)
$unitTests = @(
    @{ Name = "Radoub.Formats.Tests"; Path = "Radoub.Formats\Radoub.Formats.Tests" },
    @{ Name = "Radoub.Dictionary.Tests"; Path = "Radoub.Dictionary\Radoub.Dictionary.Tests" },
    @{ Name = "Parley.Tests"; Path = "Parley\Parley.Tests" },
    @{ Name = "Manifest.Tests"; Path = "Manifest\Manifest.Tests" }
)

# UI Tests (slower, requires display)
$uiTests = @(
    @{ Name = "Radoub.UITests"; Path = "Radoub.UITests" }
)

function Invoke-TestProject {
    param($TestInfo)

    $name = $TestInfo.Name
    $path = $TestInfo.Path
    $outputFile = "$outputDir\${name}_$timestamp.output"

    Write-Host "`n--- $name ---" -ForegroundColor Yellow

    $output = dotnet test $path --logger "console;verbosity=normal" 2>&1
    $output | Out-File -FilePath $outputFile

    # Parse results - dotnet test outputs "Total tests: N" and "Passed: N" on separate lines
    $totalLine = $output | Select-String -Pattern "Total tests:\s*(\d+)" | Select-Object -Last 1
    $passedLine = $output | Select-String -Pattern "^\s+Passed:\s*(\d+)" | Select-Object -Last 1
    $failedLine = $output | Select-String -Pattern "^\s+Failed:\s*(\d+)" | Select-Object -Last 1

    if ($totalLine) {
        $total = [int]([regex]::Match($totalLine.Line, "Total tests:\s*(\d+)").Groups[1].Value)
        $passed = if ($passedLine) { [int]([regex]::Match($passedLine.Line, "Passed:\s*(\d+)").Groups[1].Value) } else { 0 }
        $failed = if ($failedLine) { [int]([regex]::Match($failedLine.Line, "Failed:\s*(\d+)").Groups[1].Value) } else { 0 }

        $script:totalPassed += $passed
        $script:totalFailed += $failed

        $status = if ($failed -eq 0) { "PASS" } else { "FAIL" }
        $color = if ($failed -eq 0) { "Green" } else { "Red" }

        Write-Host "  $status - Passed: $passed, Failed: $failed, Total: $total" -ForegroundColor $color
        $script:results += @{ Name = $name; Passed = $passed; Failed = $failed; Status = $status }
    } else {
        Write-Host "  Could not parse results" -ForegroundColor Gray
        $script:results += @{ Name = $name; Passed = 0; Failed = 0; Status = "UNKNOWN" }
    }

    # Show failed tests if any
    $failedTests = $output | Select-String -Pattern "\[FAIL\]"
    if ($failedTests) {
        Write-Host "  Failed tests:" -ForegroundColor Red
        $failedTests | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor Red }
    }
}

# Run unit tests unless UIOnly specified
if (-not $UIOnly) {
    Write-Host "`n=== Unit/Headless Tests ===" -ForegroundColor Magenta
    foreach ($test in $unitTests) {
        Invoke-TestProject $test
    }
}

# Run UI tests unless UnitOnly specified
if (-not $UnitOnly) {
    Write-Host "`n=== UI Integration Tests ===" -ForegroundColor Magenta
    foreach ($test in $uiTests) {
        Invoke-TestProject $test
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

foreach ($r in $results) {
    $color = if ($r.Status -eq "PASS") { "Green" } elseif ($r.Status -eq "FAIL") { "Red" } else { "Gray" }
    Write-Host ("  {0,-30} {1}" -f $r.Name, $r.Status) -ForegroundColor $color
}

Write-Host ""
$overallColor = if ($totalFailed -eq 0) { "Green" } else { "Red" }
Write-Host "Total: Passed $totalPassed, Failed $totalFailed" -ForegroundColor $overallColor
Write-Host "Output files saved to: $outputDir"
