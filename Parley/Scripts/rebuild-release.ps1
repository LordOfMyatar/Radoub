#!/usr/bin/env pwsh
# Clean rebuild and launch Parley in Release mode
# Stops all instances, clears caches, rebuilds, and launches

$ErrorActionPreference = "Stop"
$ParleyRoot = Split-Path -Parent $PSScriptRoot

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Parley Clean Rebuild - RELEASE MODE" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop all Parley and dotnet processes related to project
Write-Host "[1/5] Stopping all Parley and dotnet processes..." -ForegroundColor Yellow
$parleyProcesses = Get-Process -Name "Parley" -ErrorAction SilentlyContinue
if ($parleyProcesses) {
    Write-Host "  - Found $($parleyProcesses.Count) Parley process(es), stopping..." -ForegroundColor Gray
    $parleyProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.MainWindowTitle -like "*Parley*" -or $_.Path -like "*Radoub*"
}
if ($dotnetProcesses) {
    Write-Host "  - Found $($dotnetProcesses.Count) project-related dotnet process(es), stopping..." -ForegroundColor Gray
    $dotnetProcesses | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}
Write-Host "  ✓ All processes stopped" -ForegroundColor Green
Write-Host ""

# Step 2: Clean bin and obj directories
Write-Host "[2/5] Cleaning bin and obj directories..." -ForegroundColor Yellow
$cleanPaths = @(
    "$ParleyRoot\Parley\bin",
    "$ParleyRoot\Parley\obj",
    "$ParleyRoot\Parley.Tests\bin",
    "$ParleyRoot\Parley.Tests\obj",
    "$ParleyRoot\TestingTools\CompareDialogs\bin",
    "$ParleyRoot\TestingTools\CompareDialogs\obj"
)

foreach ($path in $cleanPaths) {
    if (Test-Path $path) {
        Write-Host "  - Removing $path" -ForegroundColor Gray
        Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
    }
}
Write-Host "  ✓ Build artifacts cleaned" -ForegroundColor Green
Write-Host ""

# Step 3: Clean NuGet cache for project packages
Write-Host "[3/5] Clearing NuGet package cache..." -ForegroundColor Yellow
Push-Location $ParleyRoot
try {
    dotnet clean Parley.sln --verbosity quiet 2>&1 | Out-Null
    Write-Host "  ✓ NuGet cache cleared" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Step 4: Restore and build
Write-Host "[4/5] Restoring and building Parley (Release)..." -ForegroundColor Yellow
Push-Location $ParleyRoot
try {
    Write-Host "  - Restoring packages..." -ForegroundColor Gray
    dotnet restore Parley.sln --verbosity quiet

    Write-Host "  - Building solution (Release)..." -ForegroundColor Gray
    $buildOutput = dotnet build Parley.sln --configuration Release --no-restore 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "BUILD FAILED:" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        exit 1
    }

    # Show warnings if any
    $warnings = $buildOutput | Select-String -Pattern "warning"
    if ($warnings) {
        Write-Host ""
        Write-Host "  Build succeeded with warnings:" -ForegroundColor Yellow
        $warnings | ForEach-Object { Write-Host "    $_" -ForegroundColor Yellow }
    }

    Write-Host "  ✓ Build completed successfully" -ForegroundColor Green
} finally {
    Pop-Location
}
Write-Host ""

# Step 5: Launch Parley
Write-Host "[5/5] Launching Parley (Release)..." -ForegroundColor Yellow
$parleyExe = "$ParleyRoot\Parley\bin\Release\net9.0\Parley.exe"

if (-not (Test-Path $parleyExe)) {
    Write-Host "  ✗ Parley.exe not found at: $parleyExe" -ForegroundColor Red
    exit 1
}

Write-Host "  - Starting Parley from: $parleyExe" -ForegroundColor Gray
Start-Process -FilePath $parleyExe -WorkingDirectory "$ParleyRoot\Parley\bin\Release\net9.0"
Write-Host "  ✓ Parley launched" -ForegroundColor Green
Write-Host ""

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "Rebuild complete! Parley is running in Release mode." -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
