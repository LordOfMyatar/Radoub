param([Parameter(Mandatory)] [string]$Mdl)
# Dump every emitter node's key fields from a binary MDL via Radoub.Formats. PS7 only.
$dll = "d:\LOM\workspace\Radoub\Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll"
Add-Type -Path $dll
$bytes = [System.IO.File]::ReadAllBytes($Mdl)
$reader = New-Object Radoub.Formats.Mdl.MdlBinaryReader
$model = $reader.Parse($bytes)
$emitters = $model.EnumerateAllNodes() | Where-Object { $_ -is [Radoub.Formats.Mdl.MdlEmitterNode] }
foreach ($e in $emitters) {
    Write-Host "=== emitter '$($e.Name)' ==="
    foreach ($p in 'Update','RenderMethod','Blend','Texture','SpawnType','Spread','Velocity','RandVel','Mass','Grav','Drag','BirthRate','LifeExp','SizeStart','SizeEnd','ColorStart','ColorEnd','AlphaStart','AlphaEnd','XGrid','YGrid','Inherit','InheritLocal','InheritPart','Random','ParticleRot') {
        $v = $e.$p
        Write-Host ("  {0,-14} = {1}" -f $p, $v)
    }
}
Write-Host "Total emitters: $($emitters.Count)"
