# Build throwaway test placeables for Reliquary state-selector verification (#2595).
# Clones a base UTP and swaps Appearance to models whose open/close/on/off animations live in a
# SUPERMODEL — the case PlaceableModelLoader now merges. Writing several lets the human confirm the
# State selector appears and the preview poses each state.
#
# Requires PowerShell 7 (net9.0 assembly load); use the full PS7 path, NOT the WindowsApps stub:
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-PlaceableStateTestUtp.ps1"
#
# Generated UTPs are throwaway (not committed); this generator is reusable and committed.

param(
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [string] $BaseUtp = 'chest1.utp'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
if (-not (Test-Path $dll)) { throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first." }
Add-Type -Path $dll

# Base UTP: try the module first, then the committed test fixture.
$basePath = Join-Path $ModuleDir $BaseUtp
if (-not (Test-Path $basePath)) {
    $basePath = Join-Path $repoRoot 'Reliquary\Reliquary.Tests\Fixtures\chest1.utp'
}
if (-not (Test-Path $basePath)) { throw "Base UTP not found (tried module + test fixtures)." }

# file -> @{ Appearance; Label; States } — supermodel-inheriting models confirmed by investigation.
$cases = @(
    @{ File = 'rsp_list2';   Appearance = 830;   Label = 'tnp_list02 (open/close/on/off via tnp_list01)' },
    @{ File = 'rsp_dresser'; Appearance = 8182;  Label = 'zlc_ccp_b93 (open/close via plc_a07)' },
    @{ File = 'rsp_brazier'; Appearance = 57;    Label = 'plc_i05 brazier (on/off emitter — see #2556)' }
)

foreach ($c in $cases) {
    $utp = [Radoub.Formats.Utp.UtpReader]::Read($basePath)
    $utp.Appearance = [uint32]$c.Appearance
    $utp.TemplateResRef = $c.File
    $utp.Tag = $c.File
    $utp.AnimationState = [byte]0   # start at Default; user switches in the selector
    $loc = New-Object Radoub.Formats.Gff.CExoLocString
    $loc.SetString(0, "STATE TEST: $($c.Label)")
    $utp.LocName = $loc

    $out = Join-Path $ModuleDir ($c.File + '.utp')
    [Radoub.Formats.Utp.UtpWriter]::Write($utp, $out)
    "Wrote $out  (appearance $($c.Appearance) — $($c.Label))"
}

"`nOpen these in Reliquary (--file <name>.utp). Expected: the State selector lists Default plus the"
"model's states. rsp_list2 -> Default/Open/Closed/Activated/Deactivated; rsp_dresser -> Default/Open/Closed."
"rsp_brazier's flame on/off is emitter-gated (#2556, out of this sprint) — the selector still appears."
