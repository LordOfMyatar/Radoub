# Run the real fairyDust emitter through our ParticleSystem (no GL) and report the
# spatial spread, to compare against Aurora's hand-computed prediction. PS7 only.
$ui = "d:\LOM\workspace\Radoub\Radoub.UI\Radoub.UI\bin\Debug\net9.0\Radoub.UI.dll"
$fmt = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
Add-Type -Path $fmt
Add-Type -Path $ui

# Real fairyDust controller values (from c_fairy.mdl dump).
$node = New-Object Radoub.Formats.Mdl.MdlEmitterNode
$node.Update = "Fountain"; $node.Spread = 1.05; $node.Velocity = 0.3; $node.RandVel = 0.1
$node.Mass = 0.04; $node.Grav = 0; $node.Drag = 0; $node.BirthRate = 70; $node.LifeExp = 1.6
$node.SizeStart = 0.07; $node.SizeEnd = 0.03
$node.PercentStart = 0; $node.PercentMid = 0.5; $node.PercentEnd = 1

$emitter = [Radoub.UI.Particles.EmitterCompiler]::Compile($node)
# Fairy mesh radius 0.22 (from QM log) * ParticleSizeRadiusFactor 1.1 = motion scale.
$motionScale = [float](0.22 * 1.1)
$sys = New-Object Radoub.UI.Particles.ParticleSystem($emitter, [uint32]7, $motionScale)

# Emitter at origin, no rotation. Step ~2s at 60fps to reach steady state.
$zero = [System.Numerics.Vector3]::Zero
$q = [System.Numerics.Quaternion]::Identity
for ($i = 0; $i -lt 120; $i++) { $sys.Update([float](1.0/60.0), $zero, $q) }

$maxHoriz = 0.0; $minZ = 0.0; $maxZ = 0.0
foreach ($p in $sys.Particles) {
    $h = [math]::Sqrt($p.Position.X*$p.Position.X + $p.Position.Y*$p.Position.Y)
    if ($h -gt $maxHoriz) { $maxHoriz = $h }
    if ($p.Position.Z -lt $minZ) { $minZ = $p.Position.Z }
    if ($p.Position.Z -gt $maxZ) { $maxZ = $p.Position.Z }
}
Write-Host "motionScale     : $([math]::Round($motionScale,3))"
Write-Host "Live particles  : $($sys.LiveCount)   (Aurora ~ birthRate*life = 70*1.6 = 112)"
Write-Host "Max horiz radius: $([math]::Round($maxHoriz,3))   (body radius = 0.22)"
Write-Host "Fall depth (minZ): $([math]::Round($minZ,3))"
Write-Host "Rise (maxZ)     : $([math]::Round($maxZ,3))"
Write-Host "spread/body ratio: $([math]::Round($maxHoriz/0.22,2))x   (Aurora img ~1.5-2x body)"
