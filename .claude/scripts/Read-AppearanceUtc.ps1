# Quick dump of Appearance_Type + Tag for existing aN.utc test files (#2029 verification).
param(
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG')
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll
Get-ChildItem -Path $ModuleDir -Filter 'a*.utc' | Sort-Object Name | ForEach-Object {
    $utc = [Radoub.Formats.Utc.UtcReader]::Read($_.FullName)
    "{0,-12} Appearance_Type={1,-5} Tag={2}" -f $_.Name, $utc.AppearanceType, $utc.Tag
}
