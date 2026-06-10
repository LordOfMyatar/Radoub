param(
    [Parameter(Mandatory)] [string]$Hak,
    [Parameter(Mandatory)] [string]$ResRef,
    [Parameter(Mandatory)] [string]$Out
)
# Extract a single MDL (restype 2002) from an ERF/HAK using Radoub.Formats. PS7 only.
$dll = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
Add-Type -Path $dll
$erf = [Radoub.Formats.Erf.ErfReader]::ReadMetadataOnly($Hak)
$entry = $erf.Resources | Where-Object { $_.ResRef -ieq $ResRef -and $_.ResourceType -eq 2002 } | Select-Object -First 1
if (-not $entry) { Write-Error "Not found: $ResRef (mdl) in $Hak"; exit 1 }
$bytes = [Radoub.Formats.Erf.ErfReader]::ExtractResource($Hak, $entry)
[System.IO.File]::WriteAllBytes($Out, $bytes)
Write-Host "Extracted $ResRef -> $Out ($($bytes.Length) bytes)"
