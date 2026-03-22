# Run Radoub test projects
# Usage: .\run-tests.ps1 [-Tool <name>] [-SkipShared] [-UnitOnly] [-UIOnly] [-SkipPrivacy] [-TechDebt]
#
# Examples:
#   .\run-tests.ps1                           # All tests
#   .\run-tests.ps1 -Tool Quartermaster       # Quartermaster + shared library tests
#   .\run-tests.ps1 -Tool Parley -SkipShared  # Parley tests only (no shared)
#   .\run-tests.ps1 -Tool Manifest -UnitOnly  # Manifest unit tests only
#   .\run-tests.ps1 -Tool Fence               # Fence + shared library tests
#   .\run-tests.ps1 -Tool Trebuchet           # Trebuchet UI tests + shared library tests
#   .\run-tests.ps1 -Tool Relique             # Relique + shared library tests
#   .\run-tests.ps1 -Tool Radoub              # Shared library tests only (Formats, UI, Dictionary)
#   .\run-tests.ps1 -UnitOnly                 # All unit tests, no UI tests
#   .\run-tests.ps1 -TechDebt                 # Include tech debt scan (large files)

param(
    [ValidateSet("Parley", "Quartermaster", "Manifest", "Fence", "Trebuchet", "Relique", "Radoub")]
    [string]$Tool,
    [switch]$SkipShared,
    [switch]$UIOnly,
    [switch]$UnitOnly,
    [switch]$SkipPrivacy,
    [switch]$TechDebt,
    # Optional custom filter for UI tests (overrides default tool namespace filter)
    # Example: "Category=Workspace" or "Name~LaunchTab"
    [string]$UIFilter,
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

    $searchDirs = @("Parley", "Radoub.Formats", "Radoub.UI", "Radoub.Dictionary", "Manifest", "Quartermaster", "Fence", "Trebuchet", "Relique")

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
    Write-Host "Checking for large files (warn >800, issue >1000 lines)..." -ForegroundColor Yellow

    $warnFiles = @()
    $issueFiles = @()
    $warnThreshold = 800
    $issueThreshold = 1000

    # Get changed files compared to main
    $changedFiles = git diff main...HEAD --name-only 2>$null | Where-Object { $_ -match "\.cs$" }

    if (-not $changedFiles) {
        Write-Host "  No changed .cs files to scan" -ForegroundColor Gray
        return
    }

    foreach ($file in $changedFiles) {
        if (Test-Path $file) {
            $lineCount = (Get-Content $file | Measure-Object -Line).Lines
            if ($lineCount -gt $issueThreshold) {
                $issueFiles += [PSCustomObject]@{ File = $file; Lines = $lineCount }
            } elseif ($lineCount -gt $warnThreshold) {
                $warnFiles += [PSCustomObject]@{ File = $file; Lines = $lineCount }
            }
        }
    }

    if ($warnFiles.Count -eq 0 -and $issueFiles.Count -eq 0) {
        Write-Host "  PASS - No large files (>$warnThreshold lines)" -ForegroundColor Green
    } else {
        if ($issueFiles.Count -gt 0) {
            Write-Host "  ISSUE - Files exceeding $issueThreshold lines (needs tech debt issue):" -ForegroundColor Red
            foreach ($f in $issueFiles | Sort-Object Lines -Descending) {
                Write-Host ("    {0,5} lines: {1}" -f $f.Lines, $f.File) -ForegroundColor Red
            }
        }
        if ($warnFiles.Count -gt 0) {
            Write-Host "  WARN - Files exceeding $warnThreshold lines:" -ForegroundColor Yellow
            foreach ($f in $warnFiles | Sort-Object Lines -Descending) {
                Write-Host ("    {0,5} lines: {1}" -f $f.Lines, $f.File) -ForegroundColor Yellow
            }
        }
    }
}

# Theme compatibility scan - check for hardcoded colors, fonts, brushes
function Invoke-ThemeCompatScan {
    Write-Host "`n=== Theme Compatibility Scan ===" -ForegroundColor Magenta
    Write-Host "Checking for hardcoded colors, fonts, and brushes..." -ForegroundColor Yellow

    # Determine directories to scan based on -Tool parameter
    $scanDirs = @()
    if ($Tool) {
        $scanDirs += $Tool
        if (-not $SkipShared) {
            $scanDirs += @("Radoub.UI", "Radoub.Formats", "Radoub.Dictionary")
        }
    } else {
        $scanDirs = @("Parley", "Manifest", "Quartermaster", "Fence", "Trebuchet", "Relique", "Radoub.UI", "Radoub.Formats", "Radoub.Dictionary")
    }

    # Patterns to detect in .cs files
    $csPatterns = @(
        @{ Pattern = 'Brushes\.(?!Transparent)\w+'; Label = 'Brushes enum' },
        @{ Pattern = '(?<!\.)Colors\.(?!Length|Count|Empty|None|All|Default|Values|Keys)[A-Z][a-z]\w+'; Label = 'Colors enum' },
        @{ Pattern = 'Color\.Parse\("#'; Label = 'Hex color' },
        @{ Pattern = 'Color\.FromRgb\('; Label = 'RGB color' }
    )

    # Patterns to detect in .axaml files
    $axamlPatterns = @(
        @{ Pattern = 'FontSize="[0-9]'; Label = 'Hardcoded FontSize' },
        @{ Pattern = 'Foreground="#'; Label = 'Hardcoded Foreground' },
        @{ Pattern = 'Background="#'; Label = 'Hardcoded Background' },
        @{ Pattern = 'Fill="#'; Label = 'Hardcoded Fill' },
        @{ Pattern = 'Stroke="#'; Label = 'Hardcoded Stroke' },
        @{ Pattern = 'BorderBrush="#'; Label = 'Hardcoded BorderBrush' },
        @{ Pattern = 'Color="#'; Label = 'Hardcoded Color' }
    )

    # File/path exclusions (theme definitions, test files, infrastructure)
    $excludeFileNames = @('BrushManager.cs', 'ThemeManager.cs', 'ThemeEditorViewModel.cs')

    $violations = @()

    foreach ($dir in $scanDirs) {
        if (-not (Test-Path $dir)) { continue }

        # Scan .cs files
        $csFiles = Get-ChildItem -Path $dir -Recurse -Include "*.cs" -ErrorAction SilentlyContinue
        foreach ($file in $csFiles) {
            $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")

            # Skip excluded paths (themes, styles, tests, build output)
            if ($relativePath -match '[\\/](Themes|Styles|TestData|obj|bin)[\\/]') { continue }
            if ($relativePath -match '\.Tests[\\/]') { continue }

            # Skip excluded filenames
            if ($excludeFileNames -contains $file.Name) { continue }

            $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
            if (-not $lines) { continue }

            for ($i = 0; $i -lt $lines.Count; $i++) {
                $line = $lines[$i]
                # Skip lines with theme-ok opt-out comment
                if ($line -match '//\s*theme-ok') { continue }

                foreach ($p in $csPatterns) {
                    if ($line -match $p.Pattern) {
                        $match = [regex]::Match($line, $p.Pattern).Value
                        $violations += [PSCustomObject]@{
                            File = $relativePath
                            Line = $i + 1
                            Type = $p.Label
                            Match = $match
                        }
                    }
                }
            }
        }

        # Scan .axaml files
        $axamlFiles = Get-ChildItem -Path $dir -Recurse -Include "*.axaml" -ErrorAction SilentlyContinue
        foreach ($file in $axamlFiles) {
            $relativePath = $file.FullName.Replace((Get-Location).Path + "\", "")

            # Skip excluded paths (themes, styles, tests, build output)
            if ($relativePath -match '[\\/](Themes|Styles|TestData|obj|bin)[\\/]') { continue }
            if ($relativePath -match '\.Tests[\\/]') { continue }

            # Skip App.axaml (resource dictionary defaults are expected)
            if ($file.Name -eq "App.axaml") { continue }

            $lines = Get-Content $file.FullName -ErrorAction SilentlyContinue
            if (-not $lines) { continue }

            $skipBlock = $false
            for ($i = 0; $i -lt $lines.Count; $i++) {
                $line = $lines[$i]

                # Block-level opt-out: XML comments with theme-ok suppress until blank line
                # (inline <!-- theme-ok --> is invalid XML on attribute lines)
                if ($line -match '<!--.*theme-(ok|independent).*-->') { $skipBlock = $true; continue }
                if ($skipBlock -and $line.Trim() -eq '') { $skipBlock = $false; continue }
                if ($skipBlock) { continue }

                foreach ($p in $axamlPatterns) {
                    if ($line -match $p.Pattern) {
                        $match = [regex]::Match($line, $p.Pattern).Value
                        $violations += [PSCustomObject]@{
                            File = $relativePath
                            Line = $i + 1
                            Type = $p.Label
                            Match = $match
                        }
                    }
                }
            }
        }
    }

    if ($violations.Count -eq 0) {
        Write-Host "  PASS - No hardcoded theme values found" -ForegroundColor Green
    } else {
        Write-Host "  WARN - Found $($violations.Count) hardcoded theme value(s):" -ForegroundColor Yellow
        # Group by file for cleaner output
        $grouped = $violations | Group-Object File
        foreach ($group in $grouped | Sort-Object Name) {
            foreach ($v in $group.Group | Sort-Object Line) {
                Write-Host ("    {0}:{1} - {2} ({3})" -f $v.File, $v.Line, $v.Match, $v.Type) -ForegroundColor Yellow
            }
        }
        Write-Host "  Use DynamicResource or BrushManager for theme compatibility" -ForegroundColor Gray
        Write-Host "  Add '// theme-ok' (.cs) or '<!-- theme-ok -->' (.axaml) to suppress" -ForegroundColor Gray
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
    "Trebuchet" = @{ Name = "Trebuchet.Tests"; Path = "Trebuchet\Trebuchet.Tests" }
    "Relique" = @{ Name = "Relique.Tests"; Path = "Relique\Relique.Tests" }
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
            $entry = $toolUiTests[$Tool].Clone()
            # Override filter if custom UIFilter provided
            # UIFilter values use FullyQualifiedName~ by default (Name~ doesn't work with xUnit VSTest adapter)
            if ($UIFilter) {
                $filterExpr = if ($UIFilter -match '(FullyQualifiedName|DisplayName|Category)') { $UIFilter } else { "FullyQualifiedName~$UIFilter" }
                $entry.Filter = "$($entry.Filter)&$filterExpr"
                $entry.Name = "$($entry.Name) (filtered: $UIFilter)"
            }
            $uiTests += $entry
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
    $safeName = $name -replace '[^a-zA-Z0-9._-]', '_'
    $outputFile = "$outputDir\${safeName}_$timestamp.output"

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

# Run tech debt and theme compatibility scans if requested
if ($TechDebt) {
    Invoke-TechDebtScan
    Invoke-ThemeCompatScan
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
