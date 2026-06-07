# Build throwaway test items for Relique item-cost verification (#2235).
# Creates .uti blueprints with a chosen base item + item-property list so the cost
# formula can be validated against the Aurora toolset: open each file in the toolset
# to read its computed Cost, then open the same file in Relique (--file) and read the
# [CostCalc] log lines to compare.
#
# Uses the built Radoub.Formats.dll so the GFF round-trip matches Relique.
#
# Requires PowerShell 7 (net assembly load); Windows PowerShell 5.1 cannot Add-Type a
# net9.0 DLL. Use the full PS7 path, NOT the WindowsApps pwsh stub (Store launcher):
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/New-CostTestUti.ps1" `
#       -Items "cost_ac2=16:torso=4:prop=1,2,9;cost_kama_eb3=40:prop=6,3,1"
#
# -Items format: semicolon-separated item specs. Each spec is colon-separated fields:
#   name = <resref/tag/filename, <=16 chars>          (required, first field)
#   <baseItemIndex>                                    (required, second field, integer)
#   torso=<n>                                          (optional, armor torso part for base AC)
#   addcost=<n>                                        (optional, AddCost gold)
#   prop=<propName>,<costValue>,<costTable>[,<subtype>][,<param>,<paramValue>]
#       (repeatable — one property per prop= field)
#
# Example specs:
#   cost_ac2=16:torso=4:prop=1,2,9           studded-leather-like, +2 AC (prop 1, costtbl 9)
#   cost_eb3=40:prop=6,3,2                    longsword, +3 enhancement (prop 6, costtbl 2)
#   cost_multi=40:prop=6,2,2:prop=16,5,4     two props (enhancement + damage)

param(
    [Parameter(Mandatory = $true)]
    [string] $Items,

    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG')
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'

if (-not (Test-Path $dll)) {
    throw "Radoub.Formats.dll not found at $dll - build Radoub.Formats first (dotnet build)."
}
Add-Type -Path $dll

if (-not (Test-Path $ModuleDir)) {
    throw "Module directory not found: $ModuleDir"
}

function New-Prop([string] $spec) {
    # prop value fields: propName,costValue,costTable[,subtype[,param,paramValue]]
    $f = $spec -split ','
    if ($f.Count -lt 3) { throw "Bad prop '$spec'. Need propName,costValue,costTable[,subtype[,param,paramValue]]." }
    $p = New-Object Radoub.Formats.Uti.ItemProperty
    $p.PropertyName = [uint16]$f[0].Trim()
    $p.CostValue    = [uint16]$f[1].Trim()
    $p.CostTable    = [byte]$f[2].Trim()
    $p.Subtype      = if ($f.Count -ge 4 -and $f[3].Trim() -ne '') { [uint16]$f[3].Trim() } else { [uint16]0 }
    if ($f.Count -ge 6) {
        $p.Param1      = [byte]$f[4].Trim()
        $p.Param1Value = [byte]$f[5].Trim()
    } else {
        $p.Param1 = [byte]255  # 0xFF = no param
    }
    $p.ChanceAppear = [byte]100
    return $p
}

$created = @()

foreach ($itemSpec in ($Items -split ';')) {
    $itemSpec = $itemSpec.Trim()
    if ($itemSpec -eq '') { continue }

    $fields = $itemSpec -split ':'
    $name = $fields[0].Trim()
    if ($name.Length -gt 16) { throw "Item name '$name' exceeds 16 chars (Aurora limit)." }
    $baseItem = [int]$fields[1].Trim()

    $uti = New-Object Radoub.Formats.Uti.UtiFile
    $uti.BaseItem = $baseItem
    $uti.TemplateResRef = $name.ToLowerInvariant()
    $uti.Tag = $name
    $loc = New-Object Radoub.Formats.Gff.CExoLocString
    $loc.SetString(0, "Cost test $name")
    $uti.LocalizedName = $loc
    $uti.Identified = $true
    $uti.StackSize = [uint16]1

    for ($i = 2; $i -lt $fields.Count; $i++) {
        $field = $fields[$i].Trim()
        if ($field -eq '') { continue }
        $kv = $field -split '=', 2
        $key = $kv[0].Trim().ToLowerInvariant()
        $val = if ($kv.Count -gt 1) { $kv[1].Trim() } else { '' }

        switch ($key) {
            'torso'   { $uti.ArmorParts['Torso'] = [byte]$val }
            'addcost' { $uti.AddCost = [uint32]$val }
            'prop'    { $uti.Properties.Add((New-Prop $val)) }
            default   { throw "Unknown field '$key' in '$itemSpec'." }
        }
    }

    $outPath = Join-Path $ModuleDir ($name.ToLowerInvariant() + '.uti')
    [Radoub.Formats.Uti.UtiWriter]::Write($uti, $outPath)
    $created += $outPath
    Write-Host "Wrote $outPath (baseItem=$baseItem, props=$($uti.Properties.Count))"
}

Write-Host ""
Write-Host "Created $($created.Count) test item(s) in $ModuleDir"
Write-Host "Open each in the Aurora toolset to read its Cost, then in Relique:"
foreach ($p in $created) {
    Write-Host "  dotnet run --project Relique/Relique/Relique.csproj -- --file `"$p`""
}
