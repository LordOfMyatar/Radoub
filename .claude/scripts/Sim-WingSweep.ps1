# Measure the wing emitter's animated world-position sweep across cpause1 (the flap arc).
# A wide sweep relative to particle size means dots trail into a blade; a tiny sweep means a blob.
$fmt = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
$ui  = "d:\LOM\workspace\Radoub\Radoub.UI\Radoub.UI\bin\Debug\net9.0\Radoub.UI.dll"
Add-Type -Path $fmt
Add-Type -Path $ui
$bytes = [System.IO.File]::ReadAllBytes("d:\LOM\workspace\Radoub\NonPublic\Quartermaster\Research\c_fairy.mdl")
$reader = New-Object Radoub.Formats.Mdl.MdlBinaryReader
$model = $reader.Parse($bytes)
$wing = $model.EnumerateAllNodes() | Where-Object { $_.Name -eq 'Wing01' } | Select-Object -First 1
$anim = $model.Animations | Where-Object { $_.Name -eq 'cpause1' } | Select-Object -First 1
$ev = [Radoub.Formats.Mdl.MdlAnimationEvaluator]
$mvc = [Radoub.UI.Services.ModelViewController]
"anim=$($anim.Name) len=$($anim.Length)"

$poseT = [type]"Radoub.UI.Services.ModelViewController+NodePose"
$dictT = [type]"System.Collections.Generic.Dictionary[[string],[Radoub.UI.Services.ModelViewController+NodePose]]"

function Build-Pose($root, $t) {
    $pose = [Activator]::CreateInstance($dictT)
    $stack = New-Object System.Collections.Stack
    $stack.Push($root)
    while ($stack.Count -gt 0) {
        $n = $stack.Pop()
        $hasPos = $n.PositionTimes.Length -gt 1
        $hasOri = $n.OrientationTimes.Length -gt 1
        $hasScl = $n.ScaleTimes.Length -gt 1
        if (($hasPos -or $hasOri -or $hasScl) -and -not [string]::IsNullOrEmpty($n.Name)) {
            $p = if ($hasPos) { $ev::EvaluatePosition($n,$t) } else { $n.Position }
            $o = if ($hasOri) { $ev::EvaluateOrientation($n,$t) } else { $n.Orientation }
            $s = if ($hasScl) { $ev::EvaluateScale($n,$t) } else { $n.Scale }
            $np = [Activator]::CreateInstance($poseT, @($hasPos,$p,$hasOri,$o,$hasScl,[float]$s))
            $pose[$n.Name] = $np
        }
        foreach ($c in $n.Children) { $stack.Push($c) }
    }
    return $pose
}

$samples = 20
$minX=99;$maxX=-99;$minY=99;$maxY=-99;$minZ=99;$maxZ=-99
for ($i=0; $i -lt $samples; $i++) {
    $t = $anim.Length * $i / ($samples-1)
    $pose = Build-Pose $anim.GeometryRoot $t
    $w = $mvc::GetWorldTransform($wing, $pose)
    $wp = $mvc::TransformPosition([System.Numerics.Vector3]::Zero, $w)
    if($wp.X -lt $minX){$minX=$wp.X}; if($wp.X -gt $maxX){$maxX=$wp.X}
    if($wp.Y -lt $minY){$minY=$wp.Y}; if($wp.Y -gt $maxY){$maxY=$wp.Y}
    if($wp.Z -lt $minZ){$minZ=$wp.Z}; if($wp.Z -gt $maxZ){$maxZ=$wp.Z}
}
"Wing01 world sweep over cpause1:"
("  X range = {0:F3}" -f ($maxX-$minX))
("  Y range = {0:F3}" -f ($maxY-$minY))
("  Z range = {0:F3}" -f ($maxZ-$minZ))
"  particle size = 0.5  body width ~0.2"
