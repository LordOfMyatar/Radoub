# Build throwaway test creatures for QM model-preview verification (#2381).
# Clones a base UTC (default Bucky.utc in LNS_DLG), swaps Appearance_Type, and
# writes aN.utc files into the module so Quartermaster can open them quickly.
# Uses the built Radoub.Formats.dll so the GFF round-trip matches QM.
#
# Requires PowerShell 7 (net assembly load); Windows PowerShell 5.1 cannot Add-Type a
# net9.0 DLL. Use the full PS7 path, NOT the WindowsApps pwsh stub (Store launcher):
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-AppearanceTestUtc.ps1" -Appearances a4=159,a5=3951

param(
    [Parameter(Mandatory = $true)]
    [string[]] $Appearances,

    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),

    [string] $BaseUtc = 'Bucky.utc'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'

if (-not (Test-Path $dll)) {
    throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first (dotnet build)."
}
Add-Type -Path $dll

$basePath = Join-Path $ModuleDir $BaseUtc
if (-not (Test-Path $basePath)) {
    throw "Base UTC not found: $basePath"
}

foreach ($pair in ($Appearances -split ',')) {
    $parts = $pair -split '='
    if ($parts.Count -ne 2) { throw "Bad -Appearances entry '$pair'. Use file=row." }
    $file = $parts[0].Trim()
    $row = [uint16]($parts[1].Trim())

    $utc = [Radoub.Formats.Utc.UtcReader]::Read($basePath)
    $utc.AppearanceType = $row
    $utc.TemplateResRef = $file
    $utc.Tag = $file
    $loc = New-Object Radoub.Formats.Gff.CExoLocString
    $loc.SetString(0, "Test appearance $row ($file)")
    $utc.FirstName = $loc

    $outPath = Join-Path $ModuleDir ($file + '.utc')
    [Radoub.Formats.Utc.UtcWriter]::Write($utc, $outPath)

    $check = [Radoub.Formats.Utc.UtcReader]::Read($outPath)
    if ($check.AppearanceType -ne $row) {
        throw "Round-trip failed for $file - expected $row, got $($check.AppearanceType)"
    }
    Write-Host "Wrote $outPath  (Appearance_Type=$row)"
}
