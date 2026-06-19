# Build a throwaway part-based test creature with WINGS + TAIL set, for QM #1485 verification.
# Clones a base UTC (default Bucky.utc in LNS_DLG), sets a part-based (MODELTYPE=P) Appearance_Type
# (Human=6) plus Wings_New / Tail_New, and writes wingtail.utc into the module.
# Uses the built Radoub.Formats.dll so the GFF round-trip matches QM.
#
# Requires PowerShell 7 (net assembly load). Use the full PS7 path, NOT the WindowsApps pwsh stub:
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-WingTailTestUtc.ps1"

param(
    [uint16] $Appearance = 6,   # Human (MODELTYPE=P)
    [int]    $Wings = 2,        # wingmodel.2da row
    [int]    $Tail = 1,         # tailmodel.2da row
    [string] $OutName = 'wingtail',
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [string] $BaseUtc = 'Bucky.utc'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
if (-not (Test-Path $dll)) { throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first." }
Add-Type -Path $dll

$basePath = Join-Path $ModuleDir $BaseUtc
if (-not (Test-Path $basePath)) { throw "Base UTC not found: $basePath" }

$utc = [Radoub.Formats.Utc.UtcReader]::Read($basePath)
$utc.AppearanceType = $Appearance
$utc.Wings = [byte]$Wings
$utc.Tail = [byte]$Tail
$utc.TemplateResRef = $OutName
$utc.Tag = $OutName
$loc = New-Object Radoub.Formats.Gff.CExoLocString
$loc.SetString(0, "WingTail test (app=$Appearance wings=$Wings tail=$Tail)")
$utc.FirstName = $loc

$outPath = Join-Path $ModuleDir ($OutName + '.utc')
[Radoub.Formats.Utc.UtcWriter]::Write($utc, $outPath)

$check = [Radoub.Formats.Utc.UtcReader]::Read($outPath)
if ($check.AppearanceType -ne $Appearance -or $check.Wings -ne [byte]$Wings -or $check.Tail -ne [byte]$Tail) {
    throw "Round-trip failed: app=$($check.AppearanceType) wings=$($check.Wings) tail=$($check.Tail)"
}
Write-Host "Wrote $outPath  (Appearance=$Appearance Wings=$Wings Tail=$Tail)"
