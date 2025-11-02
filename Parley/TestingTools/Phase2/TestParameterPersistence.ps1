# Test Parameter Persistence
# Verifies that script parameters are saved and loaded correctly

param(
    [Parameter(Mandatory=$false)]
    [string]$TestFile = "~\Documents\Neverwinter Nights\modules\LNS_DLG\ashera01.dlg"
)

Write-Host "==================================="
Write-Host "TESTING PARAMETER PERSISTENCE"
Write-Host "==================================="
Write-Host ""

if (-not (Test-Path $TestFile)) {
    Write-Error "Test file not found: $TestFile"
    exit 1
}

# Build the parser test tool
Write-Host "Building parser test tool..."
$projectPath = "$PSScriptRoot\..\..\TestingTools\Core\DumpChildLinks\DumpChildLinks.csproj"
dotnet build $projectPath --nologo -v quiet

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host ""
Write-Host "Loading original file..."
Write-Host "File: $TestFile"
Write-Host ""

# Run the test tool to check for parameters
dotnet run --project $projectPath --no-build -- $TestFile 2>&1 | Select-String -Pattern "(ConditionParams|ActionParams|parameter)"

Write-Host ""
Write-Host "==================================="
Write-Host "Test complete. Check output above for parameter data."
Write-Host "If you see 'Params=0' everywhere, parameters are not being saved."
Write-Host "==================================="
