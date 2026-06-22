# Build throwaway emitter-rendering test fixtures for #2544 verification (model preview).
# Writes placeable (.utp) and creature (.utc) blueprints into LNS_DLG whose appearances point at
# models with known emitter Update modes, so the human can spot-check the model-led emission gate:
#
#   emit_expl.utp  -> placeables.2da row 1   (plc_a02, 3x Explosion)  -> #2439: burst+replay, NO plume
#   emit_mix.utp   -> placeables.2da row 57  (plc_i05, 2x Fountain + 3x Explosion) -> mixed: fountains
#                                                                       stream, explosions burst
#   emit_fairy.utc -> appearance.2da row 55  (c_fairy, 3x Fountain)    -> #2395 regression: unchanged
#
# Evidence: NonPublic/Quartermaster/Research/2026-06-21-emitter-update-mode-evidence.md
# Requires PowerShell 7 (net9.0 assembly load). Full PS7 path, NOT the WindowsApps stub:
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-EmitterTestFixtures.ps1"

param(
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [string] $BaseUtp = 'chest1.utp',
    [string] $BaseUtc = 'Bucky.utc'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
if (-not (Test-Path $dll)) { throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first." }
Add-Type -Path $dll

function New-LocName([string] $text) {
    $loc = New-Object Radoub.Formats.Gff.CExoLocString
    $loc.SetString(0, $text)
    return $loc
}

# ---- Placeable fixtures (Appearance = placeables.2da row) ----
$basePlc = Join-Path $ModuleDir $BaseUtp
if (-not (Test-Path $basePlc)) { throw "Base UTP not found: $basePlc" }

$placeables = @(
    @{ File = 'emit_expl'; Row = 1;  Desc = '#2544 plc_a02 (3x Explosion) - burst+replay, no plume' }
    @{ File = 'emit_mix';  Row = 57; Desc = '#2544 plc_i05 (Fountain+Explosion) - mixed modes' }
)
foreach ($p in $placeables) {
    $utp = [Radoub.Formats.Utp.UtpReader]::Read($basePlc)
    $utp.Appearance = [uint]$p.Row
    $utp.TemplateResRef = $p.File
    $utp.Tag = $p.File
    $utp.LocName = New-LocName $p.Desc
    $outPath = Join-Path $ModuleDir ($p.File + '.utp')
    [Radoub.Formats.Utp.UtpWriter]::Write($utp, $outPath)
    $check = [Radoub.Formats.Utp.UtpReader]::Read($outPath)
    if ($check.Appearance -ne [uint]$p.Row) { throw "Round-trip failed for $($p.File): expected $($p.Row), got $($check.Appearance)" }
    Write-Host "Wrote $outPath  (Appearance=$($p.Row))  $($p.Desc)"
}

# ---- Creature fixture (Appearance_Type = appearance.2da row) ----
$baseUtcPath = Join-Path $ModuleDir $BaseUtc
if (-not (Test-Path $baseUtcPath)) { throw "Base UTC not found: $baseUtcPath" }

$utc = [Radoub.Formats.Utc.UtcReader]::Read($baseUtcPath)
$utc.AppearanceType = [uint16]55   # c_fairy, 3x Fountain (#2395 regression guard)
$utc.TemplateResRef = 'emit_fairy'
$utc.Tag = 'emit_fairy'
$utc.FirstName = New-LocName '#2544 c_fairy (3x Fountain) - #2395 regression, unchanged'
$outUtc = Join-Path $ModuleDir 'emit_fairy.utc'
[Radoub.Formats.Utc.UtcWriter]::Write($utc, $outUtc)
$checkUtc = [Radoub.Formats.Utc.UtcReader]::Read($outUtc)
if ($checkUtc.AppearanceType -ne 55) { throw "Round-trip failed for emit_fairy: expected 55, got $($checkUtc.AppearanceType)" }
Write-Host "Wrote $outUtc  (Appearance_Type=55)  c_fairy Fountain regression guard"

Write-Host "`nDone. Open these in Reliquary (.utp) / Quartermaster (.utc) to verify the emission gate."
