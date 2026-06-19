# Diagnostic: dump a range of appearance.2da rows + total row count + which columns exist,
# to figure out why row 3439 (expected zcp_lionmale) is empty. Read-only.
param(
    [int] $Start = 3430,
    [int] $End = 3450
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll
$gameData = [Radoub.Formats.Services.GameDataService]::new()
$twoDA = $gameData.Get2DA('appearance')
"RowCount = $($twoDA.RowCount)"
"Columns  = $($twoDA.Columns -join ', ')"
""
"Rows $Start..$End  (idx | LABEL | RACE | STRING_REF | MODELTYPE):"
for ($i = $Start; $i -le $End -and $i -lt $twoDA.RowCount; $i++) {
    $label = $twoDA.GetValue($i, 'LABEL')
    $race  = $twoDA.GetValue($i, 'RACE')
    $str   = $twoDA.GetValue($i, 'STRING_REF')
    $mt    = $twoDA.GetValue($i, 'MODELTYPE')
    "  [{0,5}] '{1}' | RACE='{2}' | STRING_REF='{3}' | TYPE='{4}'" -f $i, $label, $race, $str, $mt
}
# How many rows have a non-empty RACE vs empty?
$nonEmpty = 0; $empty = 0
for ($i = 0; $i -lt $twoDA.RowCount; $i++) {
    if ([string]::IsNullOrWhiteSpace($twoDA.GetValue($i,'RACE'))) { $empty++ } else { $nonEmpty++ }
}
""
"RACE populated: $nonEmpty   empty/blank: $empty"
# Highest populated row, to see if the 2DA is truncated before 3439.
$maxPop = -1
for ($i = 0; $i -lt $twoDA.RowCount; $i++) {
    if (-not [string]::IsNullOrWhiteSpace($twoDA.GetValue($i,'RACE'))) { $maxPop = $i }
}
"Highest row with non-empty RACE = $maxPop"
