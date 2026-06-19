# Generate the #2029 skin/trimesh problem-model test creatures for QM model-preview review.
# Resolves each named CEP/base model by LABEL in the loaded appearance.2da (same source QM
# uses, incl. CEP HAKs via RadoubSettings), clones Bucky.utc with that Appearance_Type, and
# writes aN.utc into LNS_DLG. Overwrites existing a1..aN (continues the unpadded a-file scheme).
#
# PS7 ONLY (loads net9.0 Radoub.Formats.dll):
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-Issue2029TestUtc.ps1"

param(
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

# Load appearance.2da exactly as QM does.
$gameData = [Radoub.Formats.Services.GameDataService]::new()
if (-not $gameData.IsConfigured) { throw "GameDataService not configured - set game paths in a Radoub tool first." }
$twoDA = $gameData.Get2DA('appearance')
if ($null -eq $twoDA) { throw "Could not load appearance.2da." }

# The #2029 problem set. Rows resolved from appearance.2da (RACE/model column) via
# Find-AppearanceRows.ps1. The two CEP rows (zcp_lionmale) come straight from the issue.
$wanted = @(
    @{ File = 'a1'; Row = 95;   Model = 'c_cat_dire';   Desc = 'Dire tiger - BROKEN per issue (19/24 meshes skipped)' },
    @{ File = 'a2'; Row = 97;   Model = 'c_cat_lion';   Desc = 'Lion - works (mane renders correctly)' },
    @{ File = 'a3'; Row = 21;   Model = 'c_boar';       Desc = 'Boar - control (simple static model, should render fine)' },
    @{ File = 'a4'; Row = 15;   Model = 'c_beardire';   Desc = 'Dire bear - improved' },
    @{ File = 'a5'; Row = 175;  Model = 'c_direwolf';   Desc = 'Dire wolf - improved' },
    @{ File = 'a6'; Row = 174;  Model = 'c_blinkdog';   Desc = 'Blinkdog - improved' },
    @{ File = 'a7'; Row = 10;   Model = 'c_a_bat';      Desc = 'Bat - improved (wing textures, was gray)' },
    @{ File = 'a8'; Row = 183;  Model = 'c_cobra01';    Desc = 'Cobra - skins only (fangs/tongue correctly skipped)' },
    @{ File = 'a9'; Row = 168;  Model = 'c_umberhulk';  Desc = 'Umber hulk - needs investigation per issue' }
)

$results = @()
foreach ($w in $wanted) {
    $row = [uint16]$w.Row
    # Verify the row still maps to the expected model in this install (guards 2DA drift / CEP).
    $actualModel = $twoDA.GetValue([int]$w.Row, 'RACE')
    $modelNote = if ($actualModel -and ($actualModel.ToLowerInvariant() -eq $w.Model.ToLowerInvariant())) {
        $w.Model
    } else {
        "$($w.Model) (2DA row $($w.Row) actually = '$actualModel')"
    }

    $utc = [Radoub.Formats.Utc.UtcReader]::Read($basePath)
    $utc.AppearanceType = $row
    $utc.TemplateResRef = $w.File
    $utc.Tag = $w.File
    $loc = New-Object Radoub.Formats.Gff.CExoLocString
    $loc.SetString(0, "$($w.File): $($w.Desc) [row $($w.Row), $($w.Model)]")
    $utc.FirstName = $loc

    $outPath = Join-Path $ModuleDir ($w.File + '.utc')
    [Radoub.Formats.Utc.UtcWriter]::Write($utc, $outPath)
    $check = [Radoub.Formats.Utc.UtcReader]::Read($outPath)
    if ($check.AppearanceType -ne $row) { throw "Round-trip failed for $($w.File) - expected $row, got $($check.AppearanceType)" }

    Write-Host ("OK   {0,-4} row={1,-5} {2}  <- {3}" -f $w.File, $w.Row, $w.Desc, $modelNote) -ForegroundColor Green
    $results += [pscustomobject]@{ File = $w.File; Row = $w.Row; Model = $modelNote; Desc = $w.Desc }
}

Write-Host ""
Write-Host "=== Summary (#2029 model-preview test creatures) ==="
$results | Format-Table -AutoSize File, Row, Model, Desc
