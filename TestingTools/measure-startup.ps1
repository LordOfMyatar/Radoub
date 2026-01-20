# Measure-Startup.ps1
# Measures startup time for Fence and Quartermaster
# Usage: .\measure-startup.ps1 [-Tool fence|quartermaster] [-Iterations 3]

param(
    [ValidateSet("fence", "quartermaster", "both")]
    [string]$Tool = "both",
    [int]$Iterations = 3,
    [switch]$WithFile
)

$RepoRoot = Split-Path -Parent $PSScriptRoot
$FenceExe = Join-Path $RepoRoot "Fence\Fence\bin\Debug\net9.0\Fence.exe"
$QMExe = Join-Path $RepoRoot "Quartermaster\Quartermaster\bin\Debug\net9.0\Quartermaster.exe"
$TestUtm = Join-Path $RepoRoot "Radoub.IntegrationTests\TestData\TestModule\storgenral002.UTM"
$TestUtc = Join-Path $RepoRoot "Radoub.IntegrationTests\TestData\TestModule\earyldor.utc"

function Measure-AppStartup {
    param(
        [string]$ExePath,
        [string]$TestFile,
        [string]$AppName,
        [int]$Iterations
    )

    Write-Host "`n=== $AppName Startup Timing ===" -ForegroundColor Cyan

    if (-not (Test-Path $ExePath)) {
        Write-Host "ERROR: $ExePath not found. Build the project first." -ForegroundColor Red
        return
    }

    $times = @()

    for ($i = 1; $i -le $Iterations; $i++) {
        Write-Host "  Run $i of $Iterations..." -NoNewline

        $sw = [System.Diagnostics.Stopwatch]::StartNew()

        # Start the process
        if ($TestFile -and (Test-Path $TestFile)) {
            $proc = Start-Process -FilePath $ExePath -ArgumentList @("--file", $TestFile) -PassThru
        } else {
            $proc = Start-Process -FilePath $ExePath -PassThru
        }

        # Wait for window to appear (indicates UI is responsive)
        $timeout = 30  # seconds
        $waited = 0
        while ($proc.MainWindowHandle -eq 0 -and $waited -lt $timeout) {
            Start-Sleep -Milliseconds 100
            $waited += 0.1
            $proc.Refresh()
        }

        $windowAppeared = $sw.Elapsed.TotalSeconds

        # Give it a moment to settle, then close
        Start-Sleep -Milliseconds 500

        try {
            $proc.CloseMainWindow() | Out-Null
            $proc.WaitForExit(5000) | Out-Null
            if (-not $proc.HasExited) {
                $proc.Kill()
            }
        } catch {}

        $times += $windowAppeared
        Write-Host " $([math]::Round($windowAppeared, 2))s" -ForegroundColor Yellow
    }

    $avg = ($times | Measure-Object -Average).Average
    $min = ($times | Measure-Object -Minimum).Minimum
    $max = ($times | Measure-Object -Maximum).Maximum

    Write-Host "`n  Results:" -ForegroundColor Green
    Write-Host "    Min: $([math]::Round($min, 2))s"
    Write-Host "    Max: $([math]::Round($max, 2))s"
    Write-Host "    Avg: $([math]::Round($avg, 2))s" -ForegroundColor White

    return @{
        App = $AppName
        Min = $min
        Max = $max
        Avg = $avg
        Iterations = $Iterations
    }
}

Write-Host "Startup Performance Test" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "Iterations: $Iterations"
if ($WithFile) {
    Write-Host "Mode: With file loading"
} else {
    Write-Host "Mode: Empty startup"
}

$results = @()

if ($Tool -eq "fence" -or $Tool -eq "both") {
    $testFile = if ($WithFile) { $TestUtm } else { $null }
    $results += Measure-AppStartup -ExePath $FenceExe -TestFile $testFile -AppName "Fence" -Iterations $Iterations
}

if ($Tool -eq "quartermaster" -or $Tool -eq "both") {
    $testFile = if ($WithFile) { $TestUtc } else { $null }
    $results += Measure-AppStartup -ExePath $QMExe -TestFile $testFile -AppName "Quartermaster" -Iterations $Iterations
}

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
foreach ($r in $results) {
    if ($r) {
        $status = if ($r.Avg -lt 2) { "PASS" } elseif ($r.Avg -lt 5) { "SLOW" } else { "FAIL" }
        $color = if ($status -eq "PASS") { "Green" } elseif ($status -eq "SLOW") { "Yellow" } else { "Red" }
        Write-Host "  $($r.App): $([math]::Round($r.Avg, 2))s avg [$status]" -ForegroundColor $color
    }
}
