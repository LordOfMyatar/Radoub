# Test script for orphan node handling (Issue #27)
# Tests that orphaned nodes are correctly detected and containerized
# Tests that container is reused when nodes become orphaned after deletion

param(
    [string]$ParleyExe = "$PSScriptRoot\..\Parley\bin\Debug\net9.0\Parley.exe"
)

Write-Host "=== Orphan Node Handling Test ===" -ForegroundColor Cyan
Write-Host ""

# Test file with known orphans
$testFile = "$PSScriptRoot\TestFiles\test1_link.dlg"

if (-not (Test-Path $testFile)) {
    Write-Host "ERROR: Test file not found: $testFile" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ParleyExe)) {
    Write-Host "ERROR: Parley executable not found: $ParleyExe" -ForegroundColor Red
    Write-Host "Build the project first with: dotnet build" -ForegroundColor Yellow
    exit 1
}

Write-Host "Test File: $testFile" -ForegroundColor Gray
Write-Host "Parley Exe: $ParleyExe" -ForegroundColor Gray
Write-Host ""

# Test 1: Load file and check for orphan container
Write-Host "Test 1: Initial orphan detection" -ForegroundColor Yellow
Write-Host "Opening $testFile..." -ForegroundColor Gray

# Run Parley in headless mode and capture debug output
$env:PARLEY_TEST_MODE = "1"
$output = & $ParleyExe $testFile 2>&1 | Out-String

Write-Host "Checking for orphan detection in logs..." -ForegroundColor Gray

$foundOrphans = $false
$containerCreated = $false
$orphanCount = 0

# Parse output
if ($output -match "Found (\d+) orphaned nodes") {
    $orphanCount = [int]$Matches[1]
    $foundOrphans = $true
    Write-Host "✓ Found $orphanCount orphaned nodes" -ForegroundColor Green
} else {
    Write-Host "✗ No orphan detection logged" -ForegroundColor Red
}

if ($output -match "(Created|Updated) orphan container") {
    $containerCreated = $true
    Write-Host "✓ Orphan container created/updated" -ForegroundColor Green
} else {
    Write-Host "✗ No orphan container creation logged" -ForegroundColor Red
}

if ($output -match "Reusing existing orphan") {
    Write-Host "✓ Container reuse detected" -ForegroundColor Green
} elseif ($containerCreated) {
    Write-Host "→ New container created (expected on first run)" -ForegroundColor Gray
}

if ($output -match "Skipping orphan container START during reachability check") {
    Write-Host "✓ Orphan container START correctly skipped in reachability check" -ForegroundColor Green
} else {
    Write-Host "✗ Orphan container START not being skipped (may cause false negatives)" -ForegroundColor Red
}

Write-Host ""

# Test 2: Manual verification instructions
Write-Host "Test 2: Manual verification steps" -ForegroundColor Yellow
Write-Host "Please verify the following in Parley GUI:" -ForegroundColor Gray
Write-Host "1. Open: $testFile" -ForegroundColor White
Write-Host "2. Look for '!!! Orphaned Nodes' container in dialog tree" -ForegroundColor White
Write-Host "3. Container should have sc_false script (never appears in-game)" -ForegroundColor White
Write-Host "4. Delete a parent node and verify:" -ForegroundColor White
Write-Host "   - Children appear in orphan container" -ForegroundColor White
Write-Host "   - Container is REUSED (not recreated)" -ForegroundColor White
Write-Host "   - Any existing links to container remain valid" -ForegroundColor White
Write-Host ""

# Summary
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
if ($foundOrphans -and $containerCreated) {
    Write-Host "✓ PASS: Orphan detection and containerization working" -ForegroundColor Green
    exit 0
} else {
    Write-Host "✗ FAIL: Orphan handling not working correctly" -ForegroundColor Red
    Write-Host "Check debug logs for details" -ForegroundColor Yellow
    exit 1
}
