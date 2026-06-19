# Search appearance.2da by keyword across LABEL + RACE (model prefix) columns to find the
# #2029 problem-model rows. Read-only; prints candidate rows for manual selection.
param(
    [string[]] $Keywords = @('cat','tiger','lion','bear','wolf','blink','bat','snake','cobra','serpent','umber','hulk'),
    [switch] $TrimeshOnly  # exclude part-based (MODELTYPE=P) which already work
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll
$gameData = [Radoub.Formats.Services.GameDataService]::new()
$twoDA = $gameData.Get2DA('appearance')
"RowCount = $($twoDA.RowCount)`n"
foreach ($kw in $Keywords) {
    $k = $kw.ToLowerInvariant()
    $rows = @()
    for ($i = 0; $i -lt $twoDA.RowCount; $i++) {
        $label = ($twoDA.GetValue($i, 'LABEL') ?? '')
        $race  = ($twoDA.GetValue($i, 'RACE')  ?? '')   # model resref prefix, e.g. c_cat_dire
        $mtype = ($twoDA.GetValue($i, 'MODELTYPE') ?? '')
        if ($TrimeshOnly -and $mtype.ToUpperInvariant() -eq 'P') { continue }
        if ($label.ToLowerInvariant().Contains($k) -or $race.ToLowerInvariant().Contains($k)) {
            $rows += "    [{0,5}] LABEL={1,-22} RACE(model)={2,-18} TYPE={3}" -f $i, $label, $race, $mtype
        }
    }
    "=== '$kw' ($($rows.Count)) ==="
    if ($rows.Count -eq 0) { "    (none)" } else { $rows | Select-Object -First 30 }
    ""
}
