param([Parameter(Mandatory)] [string]$Mdl)
# For each emitter, compute its world-transform scale (parent-chain SRT) + world position.
# This is the factor Aurora's node hierarchy applies to particle motion. PS7 only.
$fmt = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
$ui  = "d:\LOM\workspace\Radoub\Radoub.UI\Radoub.UI\bin\Debug\net9.0\Radoub.UI.dll"
Add-Type -Path $fmt
Add-Type -Path $ui
$bytes = [System.IO.File]::ReadAllBytes($Mdl)
$reader = New-Object Radoub.Formats.Mdl.MdlBinaryReader
$model = $reader.Parse($bytes)
foreach ($n in $model.EnumerateAllNodes()) {
    if ($n -isnot [Radoub.Formats.Mdl.MdlEmitterNode]) { continue }
    $w = [Radoub.UI.Services.ModelViewController]::GetWorldTransform($n)
    $ok = [System.Numerics.Matrix4x4]::Decompose($w, [ref]([System.Numerics.Vector3]::Zero), [ref]([System.Numerics.Quaternion]::Identity), [ref]([System.Numerics.Vector3]::Zero))
    # Re-decompose capturing outputs
    $scale = [System.Numerics.Vector3]::Zero; $rot = [System.Numerics.Quaternion]::Identity; $trans = [System.Numerics.Vector3]::Zero
    $ok = [System.Numerics.Matrix4x4]::Decompose($w, [ref]$scale, [ref]$rot, [ref]$trans)
    Write-Host "emitter '$($n.Name)':"
    Write-Host ("  worldScale = ({0:F4}, {1:F4}, {2:F4})  decomposeOk={3}" -f $scale.X,$scale.Y,$scale.Z,$ok)
    Write-Host ("  worldPos   = ({0:F4}, {1:F4}, {2:F4})" -f $trans.X,$trans.Y,$trans.Z)
    Write-Host ("  node.Scale = {0}" -f $n.Scale)
}
