param([Parameter(Mandatory)] [string]$Mdl)
# Dump ALL public properties of every emitter node (full field set). PS7 only.
$fmt = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
Add-Type -Path $fmt
$bytes = [System.IO.File]::ReadAllBytes($Mdl)
$reader = New-Object Radoub.Formats.Mdl.MdlBinaryReader
$model = $reader.Parse($bytes)
foreach ($n in $model.EnumerateAllNodes()) {
    if ($n -isnot [Radoub.Formats.Mdl.MdlEmitterNode]) { continue }
    Write-Host "=== '$($n.Name)' (parent=$($n.Parent.Name)) ==="
    $n | Get-Member -MemberType Property | ForEach-Object {
        $name = $_.Name
        try { $val = $n.$name } catch { $val = '<err>' }
        if ($name -in 'Children','Controllers','Parent') { return }
        Write-Host ("  {0,-16} = {1}" -f $name, $val)
    }
}
