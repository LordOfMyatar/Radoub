# Build the #2498 visual-check fixtures: writes a1..aN.utc into LNS_DLG, each set to a creature
# that exercises the mesh-visibility change (heuristic removal). Open a1, a2, ... in
# Quartermaster's Appearance panel and eyeball each. Delegates the UTC write to
# New-AppearanceTestUtc.ps1 (same GFF round-trip QM uses).
#
# Requires PS7 (loads net9.0 Radoub.Formats.dll):
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-MeshCheckFixtures.ps1"
#
# Default map (appearance.2da rows resolved 2026-06-19; re-resolve if 2DAs change):
#   a1 = c_cat_dire (95)   dire tiger  — full body should render (the race-fix regression model)
#   a2 = c_antoine (417)   Lord Antoine — hands/neck/hair/medallions must now appear
#   a3 = c_drgred  (49)    red dragon  — body spikes / fins / tail pieces must now appear
#   a4 = c_ani_snake (3137) water snake — tongue must now appear
#   a5 = c_behold  (401)   beholder    — WATCH: construction Box*/Pyramid* now render too (z-fight risk)

param(
    [string]   $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [string]   $BaseUtc   = 'Bucky.utc',
    # file=row pairs; edit to taste. Order = a1..aN.
    [string[]] $Map = @('a1=95','a2=417','a3=49','a4=3137','a5=401')
)
$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
$writer = Join-Path $scriptDir 'New-AppearanceTestUtc.ps1'
if (-not (Test-Path $writer)) { throw "New-AppearanceTestUtc.ps1 not found next to this script." }

# New-AppearanceTestUtc takes a comma-joined file=row list.
$appearances = ($Map -join ',')
& $writer -Appearances $appearances -ModuleDir $ModuleDir -BaseUtc $BaseUtc

""
"=== #2498 visual check — open each in QM Appearance panel ==="
"  a1  dire tiger  — full body renders (not exploded)"
"  a2  Lord Antoine— hands, neck, hair, medallions present"
"  a3  red dragon  — body spikes, fins, tail pieces present"
"  a4  water snake — tongue present"
"  a5  beholder    — WATCH for spiky Box/Pyramid construction artifacts (z-fight regression?)"
