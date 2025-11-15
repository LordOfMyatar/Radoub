# Compare fox.dlg and __fox.dlg to see orphan container differences
# Usage: .\CompareFoxFiles.ps1 -NwnDocsPath "path\to\Neverwinter Nights" -ModuleName "LNS_DLG"

param(
    [string]$NwnDocsPath = "$env:USERPROFILE\Documents\Neverwinter Nights",
    [string]$ModuleName = "LNS_DLG"
)

$foxOriginal = Join-Path $NwnDocsPath "modules\$ModuleName\fox.dlg"
$foxDeleted = Join-Path $NwnDocsPath "modules\$ModuleName\__fox.dlg"

if (-not (Test-Path $foxOriginal)) {
    Write-Host "ERROR: fox.dlg not found at: $foxOriginal" -ForegroundColor Red
    Write-Host "Use -NwnDocsPath and -ModuleName parameters to specify location" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path $foxDeleted)) {
    Write-Host "ERROR: __fox.dlg not found at: $foxDeleted" -ForegroundColor Red
    exit 1
}

Write-Host "=== Comparing fox.dlg (original) vs __fox.dlg (after deletion) ===" -ForegroundColor Cyan
Write-Host ""

# Use Parley to parse both files
$parleyPath = Split-Path -Parent $PSScriptRoot
$parleyExe = Join-Path $parleyPath "Parley\bin\Debug\net9.0\Parley.exe"

if (-not (Test-Path $parleyExe)) {
    Write-Host "Building Parley..." -ForegroundColor Yellow
    Push-Location $parleyPath
    dotnet build --verbosity quiet
    Pop-Location
}

Write-Host "Run this command to check fox.dlg structure:" -ForegroundColor Yellow
Write-Host "  cd `"$parleyPath`"" -ForegroundColor Gray
Write-Host "  dotnet run -- `"$foxOriginal`"" -ForegroundColor Gray
Write-Host ""
Write-Host "Then check __fox.dlg:" -ForegroundColor Yellow
Write-Host "  dotnet run -- `"$foxDeleted`"" -ForegroundColor Gray
Write-Host ""
Write-Host "Look in the Application log for:" -ForegroundColor Yellow
Write-Host "  - 'Found X orphaned nodes'" -ForegroundColor Gray
Write-Host "  - 'Created new orphan root container'" -ForegroundColor Gray
Write-Host "  - Orphan container should appear in START list" -ForegroundColor Gray
