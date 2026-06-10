param([Parameter(Mandatory)] [string]$Mdl)
# Trace each emitter's parent chain + each node's local Position/Orientation, and the final
# world position via ModelViewController. PS7 only.
$fmt = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
$ui  = "d:\LOM\workspace\Radoub\Radoub.UI\Radoub.UI\bin\Debug\net9.0\Radoub.UI.dll"
Add-Type -Path $fmt
Add-Type -Path $ui
$bytes = [System.IO.File]::ReadAllBytes($Mdl)
$reader = New-Object Radoub.Formats.Mdl.MdlBinaryReader
$model = $reader.Parse($bytes)
foreach ($n in $model.EnumerateAllNodes()) {
    if ($n -isnot [Radoub.Formats.Mdl.MdlEmitterNode]) { continue }
    Write-Host "=== emitter '$($n.Name)' ==="
    $cur = $n
    while ($cur -ne $null) {
        $p = $cur.Position
        Write-Host ("  node '{0,-14}' localPos=({1:F3},{2:F3},{3:F3}) orient={4}" -f $cur.Name,$p.X,$p.Y,$p.Z,$cur.Orientation)
        $cur = $cur.Parent
    }
    $w = [Radoub.UI.Services.ModelViewController]::GetWorldTransform($n)
    $t = [System.Numerics.Vector3]::Zero
    $t = [Radoub.UI.Services.ModelViewController]::TransformPosition([System.Numerics.Vector3]::Zero, $w)
    Write-Host ("  => WORLD pos = ({0:F3}, {1:F3}, {2:F3})" -f $t.X,$t.Y,$t.Z)
}
