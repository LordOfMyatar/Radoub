# Run Radoub test projects
# Usage: .\run-tests.ps1 [-Tool <name>] [-SkipShared] [-UnitOnly] [-UIOnly] [-SkipPrivacy] [-TechDebt]
#
# Examples:
#   .\run-tests.ps1                           # All tests
#   .\run-tests.ps1 -Tool Quartermaster       # Quartermaster + shared library tests
#   .\run-tests.ps1 -Tool Parley -SkipShared  # Parley tests only (no shared)
#   .\run-tests.ps1 -Tool Manifest -UnitOnly  # Manifest unit tests only
#   .\run-tests.ps1 -Tool Fence               # Fence + shared library tests
#   .\run-tests.ps1 -UnitOnly                 # All unit tests, no UI tests
#   .\run-tests.ps1 -TechDebt                 # Include tech debt scan (large files)

param(
    [ValidateSet("Parley", "Quartermaster", "Manifest", "Fence")]
    [string]$Tool,
    [switch]$SkipShared,
    [switch]$UIOnly,
    [switch]$UnitOnly,
    [switch]$SkipPrivacy,
    [switch]$TechDebt,
    # Legacy flags (deprecated, use -Tool instead)
    [switch]$ParleyOnly,
    [switch]$QuartermasterOnly
)

# Handle legacy flags
if ($ParleyOnly) { $Tool = "Parley" }
if ($QuartermasterOnly) { $Tool = "Quartermaster" }

$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputDir = "Radoub.IntegrationTests\TestOutput"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Radoub Test Suite" -ForegroundColor Cyan
Write-Host "Started: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
if ($Tool) {
    Write-Host "Tool: $Tool $(if ($SkipShared) { '(skip shared)' } else { '(+ shared)' })" -ForegroundColor Cyan
}
Write-Host "========================================" -ForegroundColor Cyan

$script:totalPassed = 0
$script:totalFailed = 0
$script:results = @()

# Privacy scan for hardcoded paths
function Invoke-PrivacyScan {
    Write-Host "`n=== Privacy Scan ===" -ForegroundColor Magenta
    Write-Host "Checking for hardcoded paths..." -ForegroundColor Yellow

    $searchDirs = @("Parley", "Radoub.Formats", "Radoub.UI", "Radoub.Dictionary", "Manifest", "Quartermaster", "Fence")

    $patterns = @(
        'C:\\Users\\[A-Za-z]',
        'C:/Users/[A-Za-z]',
        '"/Users/[A-Za-z]',
        '"/home/[A-Za-z]'
    )

    $violations = @()

    foreach ($dir in $searchDirs) {
        if (Test-Path $dir) {
            $files = Get-ChildItem -Path $dir -Recurse -Include "*.cs" -ErrorAction SilentlyContinue
            foreach ($file in $files) {
                $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
                if ($content) {
                    foreach ($pattern in $patterns) {
                        if ($content -match $pattern) {
                            $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")
                            if ($relativePath -notmatch "\.Tests\\|TestData\\|\.test\.cs$") {
                                $violations += "$relativePath matches pattern '$pattern'"
                            }
                        }
                    }
                }
            }
        }
    }

    if ($violations.Count -eq 0) {
        Write-Host "  PASS - No hardcoded paths found" -ForegroundColor Green
        return $true
    } else {
        Write-Host "  FAIL - Found hardcoded paths:" -ForegroundColor Red
        foreach ($v in $violations) {
            Write-Host "    $v" -ForegroundColor Red
        }
        return $false
    }
}

# Tech debt scan - check for large files in changed code
function Invoke-TechDebtScan {
    Write-Host "`n=== Tech Debt Scan ===" -ForegroundColor Magenta
    Write-Host "Checking for large files (>500 lines)..." -ForegroundColor Yellow

    $largeFiles = @()
    $threshold = 500

    # Get changed files compared to main
    $changedFiles = git diff main...HEAD --name-only 2>$null | Where-Object { $_ -match "\.cs$" }

    if (-not $changedFiles) {
        Write-Host "  No changed .cs files to scan" -ForegroundColor Gray
        return
    }

    foreach ($file in $changedFiles) {
        if (Test-Path $file) {
            $lineCount = (Get-Content $file | Measure-Object -Line).Lines
            if ($lineCount -gt $threshold) {
                $largeFiles += [PSCustomObject]@{ File = $file; Lines = $lineCount }
            }
        }
    }

    if ($largeFiles.Count -eq 0) {
        Write-Host "  PASS - No large files (>$threshold lines)" -ForegroundColor Green
    } else {
        Write-Host "  WARN - Large files found:" -ForegroundColor Yellow
        foreach ($f in $largeFiles | Sort-Object Lines -Descending) {
            Write-Host ("    {0,5} lines: {1}" -f $f.Lines, $f.File) -ForegroundColor Yellow
        }
        Write-Host "  Consider refactoring or creating tech-debt issues" -ForegroundColor Gray
    }
}

# Test project definitions
$sharedUnitTests = @(
    @{ Name = "Radoub.Formats.Tests"; Path = "Radoub.Formats\Radoub.Formats.Tests" },
    @{ Name = "Radoub.UI.Tests"; Path = "Radoub.UI\Radoub.UI.Tests" },
    @{ Name = "Radoub.Dictionary.Tests"; Path = "Radoub.Dictionary\Radoub.Dictionary.Tests" }
)

$toolUnitTests = @{
    "Parley" = @{ Name = "Parley.Tests"; Path = "Parley\Parley.Tests" }
    "Manifest" = @{ Name = "Manifest.Tests"; Path = "Manifest\Manifest.Tests" }
    "Quartermaster" = @{ Name = "Quartermaster.Tests"; Path = "Quartermaster\Quartermaster.Tests" }
    "Fence" = @{ Name = "Fence.Tests"; Path = "Fence\Fence.Tests" }
}

$toolUiTests = @{
    "Parley" = @{ Name = "Radoub.IntegrationTests.Parley"; Path = "Radoub.IntegrationTests"; Filter = "FullyQualifiedName~Radoub.IntegrationTests.Parley" }
    "Quartermaster" = @{ Name = "Radoub.IntegrationTests.Quartermaster"; Path = "Radoub.IntegrationTests"; Filter = "FullyQualifiedName~Radoub.IntegrationTests.Quartermaster" }
    "Manifest" = @{ Name = "Radoub.IntegrationTests.Manifest"; Path = "Radoub.IntegrationTests"; Filter = "FullyQualifiedName~Radoub.IntegrationTests.Manifest" }
    "Fence" = @{ Name = "Radoub.IntegrationTests.Fence"; Path = "Radoub.IntegrationTests"; Filter = "FullyQualifiedName~Radoub.IntegrationTests.Fence" }
    "Trebuchet" = @{ Name = "Radoub.IntegrationTests.Trebuchet"; Path = "Radoub.IntegrationTests"; Filter = "FullyQualifiedName~Radoub.IntegrationTests.Trebuchet" }
}

# Build test list based on parameters
function Get-TestsToRun {
    $unitTests = @()
    $uiTests = @()

    if ($Tool) {
        # Specific tool requested
        if (-not $SkipShared) {
            $unitTests += $sharedUnitTests
        }
        if ($toolUnitTests.ContainsKey($Tool)) {
            $unitTests += $toolUnitTests[$Tool]
        }
        if ($toolUiTests.ContainsKey($Tool)) {
            $uiTests += $toolUiTests[$Tool]
        }
    } else {
        # All tests
        $unitTests += $sharedUnitTests
        foreach ($key in $toolUnitTests.Keys) {
            $unitTests += $toolUnitTests[$key]
        }
        foreach ($key in $toolUiTests.Keys) {
            $uiTests += $toolUiTests[$key]
        }
    }

    return @{ Unit = $unitTests; UI = $uiTests }
}

function Invoke-TestProject {
    param($TestInfo)

    $name = $TestInfo.Name
    $path = $TestInfo.Path
    $filter = $TestInfo.Filter
    $outputFile = "$outputDir\${name}_$timestamp.output"

    Write-Host "`n--- $name ---" -ForegroundColor Yellow

    if ($filter) {
        $output = dotnet test $path --filter $filter --logger "console;verbosity=normal" 2>&1
    } else {
        $output = dotnet test $path --logger "console;verbosity=normal" 2>&1
    }
    $output | Out-File -FilePath $outputFile

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
        $script:results += [PSCustomObject]@{ Name = $name; Passed = $passed; Failed = $failed; Status = $status }
    } else {
        Write-Host "  Could not parse results" -ForegroundColor Gray
        $script:results += [PSCustomObject]@{ Name = $name; Passed = 0; Failed = 0; Status = "UNKNOWN" }
    }

    $failedTests = $output | Select-String -Pattern "\[FAIL\]"
    if ($failedTests) {
        Write-Host "  Failed tests:" -ForegroundColor Red
        $failedTests | ForEach-Object { Write-Host "    $($_.Line)" -ForegroundColor Red }
    }
}

# Run privacy scan first (unless skipped)
if (-not $SkipPrivacy) {
    Invoke-PrivacyScan | Out-Null
}

# Run tech debt scan if requested
if ($TechDebt) {
    Invoke-TechDebtScan
}

$tests = Get-TestsToRun

# Run unit tests unless UIOnly specified
if (-not $UIOnly) {
    Write-Host "`n=== Unit Tests ===" -ForegroundColor Magenta
    foreach ($test in $tests.Unit) {
        Invoke-TestProject $test
    }
}

# Run UI tests unless UnitOnly specified
if (-not $UnitOnly -and $tests.UI.Count -gt 0) {
    Write-Host "`n=== UI Integration Tests ===" -ForegroundColor Magenta
    $firstUiTest = $true
    foreach ($test in $tests.UI) {
        if (-not $firstUiTest) {
            Write-Host "  [Waiting 2s for system to settle...]" -ForegroundColor Gray
            Start-Sleep -Seconds 2
        }
        $firstUiTest = $false
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
    Write-Host ("  {0,-35} {1}" -f $r.Name, $r.Status) -ForegroundColor $color
}

Write-Host ""
$overallColor = if ($totalFailed -eq 0) { "Green" } else { "Red" }
Write-Host "Total: Passed $totalPassed, Failed $totalFailed" -ForegroundColor $overallColor
Write-Host "Output files saved to: $outputDir"

# Exit with error code if any tests failed
if ($totalFailed -gt 0) {
    exit 1
}
