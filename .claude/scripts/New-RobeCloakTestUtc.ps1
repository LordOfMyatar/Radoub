# Build a throwaway robe-cloak test creature for QM model-preview verification (#2398 cloak case).
# Clones a base armor UTI (default x0_cloth005, a known robe armor) and overrides its Robe part to
# a chosen robe number, then clones a base creature (default dananaherin, human female appearance 4)
# and equips the test armor in the chest slot. Writes both into the module so QM can open the UTC.
#
# The default robe 116 (pXh0_robe116) carries cloak_inner/cloak_outer skin meshes — the #2398 cloak
# case. Robe 186 (coat+arms, no cloak) is the duplicate/arm case instead.
#
# Requires PowerShell 7 (net9.0 assembly load). Use the full PS7 path, NOT the WindowsApps stub:
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-RobeCloakTestUtc.ps1" -Robe 116 -OutName robe116t

param(
    [int] $Robe = 116,
    [string] $OutName = 'robe116t',                 # <=16 chars, used for both .uti and .utc
    [string] $BaseArmor = 'x0_cloth005',            # a known robe-bearing armor UTI
    [string] $BaseCreature = 'dananaherin',         # known-good human-female robe wearer (appearance 4)
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
if (-not (Test-Path $dll)) { throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first." }
Add-Type -Path $dll

if ($OutName.Length -gt 16) { throw "OutName '$OutName' exceeds 16-char Aurora ResRef limit." }

$RT = [Radoub.Formats.Common.ResourceTypes]
$gameData = [Radoub.Formats.Services.GameDataService]::new()
$gameData.ConfigureModuleHaks($ModuleDir)

# --- 1. Clone the base armor UTI, override Robe part ---
$armorBytes = $gameData.FindResource($BaseArmor, $RT::Uti)
if (-not $armorBytes -or $armorBytes.Length -eq 0) { throw "Base armor '$BaseArmor' not found." }
$uti = [Radoub.Formats.Uti.UtiReader]::Read($armorBytes)
$uti.TemplateResRef = $OutName
$uti.Tag = $OutName
$uti.ArmorParts['Robe'] = [byte]$Robe
$utiOut = Join-Path $ModuleDir ($OutName + '.uti')
[Radoub.Formats.Uti.UtiWriter]::Write($uti, $utiOut)
Write-Host "Wrote $utiOut  (Robe part = $Robe)"

# --- 2. Clone the base creature, equip the test armor in the chest slot (2) ---
$creatureBytes = $gameData.FindResource($BaseCreature, $RT::Utc)
if (-not $creatureBytes -or $creatureBytes.Length -eq 0) { throw "Base creature '$BaseCreature' not found." }
$utc = [Radoub.Formats.Utc.UtcReader]::Read($creatureBytes)
$utc.TemplateResRef = $OutName
$utc.Tag = $OutName
$loc = New-Object Radoub.Formats.Gff.CExoLocString
$loc.SetString(0, "Robe $Robe cloak test")
$utc.FirstName = $loc

# Replace/add the chest-slot (2) equipped item to point at our test armor.
$chest = $utc.EquipItemList | Where-Object { $_.Slot -eq 2 } | Select-Object -First 1
if ($chest) {
    $chest.EquipRes = $OutName
} else {
    $item = New-Object Radoub.Formats.Utc.EquippedItem
    $item.Slot = 2
    $item.EquipRes = $OutName
    $utc.EquipItemList.Add($item)
}

$utcOut = Join-Path $ModuleDir ($OutName + '.utc')
[Radoub.Formats.Utc.UtcWriter]::Write($utc, $utcOut)

# Round-trip check.
$check = [Radoub.Formats.Utc.UtcReader]::Read($utcOut)
$checkChest = $check.EquipItemList | Where-Object { $_.Slot -eq 2 } | Select-Object -First 1
if ($checkChest.EquipRes -ne $OutName) { throw "Round-trip failed: chest EquipRes = $($checkChest.EquipRes)" }
Write-Host "Wrote $utcOut  (Appearance=$($check.AppearanceType), chest=$($checkChest.EquipRes))"
Write-Host "Open in QM:  --file `"$utcOut`""
