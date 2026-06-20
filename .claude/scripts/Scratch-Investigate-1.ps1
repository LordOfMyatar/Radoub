$ErrorActionPreference = 'Stop'
Add-Type -Path "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
$dir = "d:\LOM\workspace\Radoub\Relique\Relique.Tests\Fixtures"
Get-ChildItem $dir -Filter *.uti | ForEach-Object {
    try {
        $uti = [Radoub.Formats.Uti.UtiReader]::ReadFromFile($_.FullName)
        Write-Host "$($_.Name): $($uti.Properties.Count) properties"
    } catch { Write-Host "$($_.Name): ERR $($_.Exception.Message)" }
}
