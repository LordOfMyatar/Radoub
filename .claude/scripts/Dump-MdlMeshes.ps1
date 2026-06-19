# Evidence dump for #2029: list every mesh in a creature MDL with the exact fields the
# MeshSkipHeuristic uses (skin vs trimesh, vertex count, Bitmap, Render), and replay the
# heuristic to show which meshes get SKIPPED and WHY. Read-only diagnostic.
#
# PS7 ONLY. Configures module HAKs (LNS_DLG) so CEP models resolve the same way QM does.
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/Dump-MdlMeshes.ps1" -Model c_cat_dire

param(
    [Parameter(Mandatory = $true)] [string] $Model,
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [int] $TinyThreshold = 30
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll

$gameData = [Radoub.Formats.Services.GameDataService]::new()
if (-not $gameData.IsConfigured) { throw "GameDataService not configured." }
# Mirror QM: load the module's HAK list so CEP MDLs resolve.
$gameData.ConfigureModuleHaks($ModuleDir)

$MDL = [Radoub.Formats.Common.ResourceTypes]::Mdl
$bytes = $gameData.FindResource($Model.ToLowerInvariant(), $MDL)
if ($null -eq $bytes) { throw "MDL '$Model' not found in base/HAK resources." }
"Resolved $Model.mdl: $($bytes.Length) bytes"

$reader = [Radoub.Formats.Mdl.MdlReader]::new()
$mdl = $reader.Parse($bytes)
$meshes = @($mdl.GetMeshNodes())
"Model.Name = '$($mdl.Name)'   mesh nodes = $($meshes.Count)`n"

# Recompute hasSkins + skinBitmaps exactly like ModelPreviewGLControl.
$skinType = 'Radoub.Formats.Mdl.MdlSkinNode'
$hasSkins = $false
$skinBitmaps = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($m in $meshes) {
    $isSkin = $m.GetType().FullName -eq $skinType
    if ($isSkin -and $m.Render -and $m.Vertices.Length -gt 0) {
        $hasSkins = $true
        if (-not [string]::IsNullOrEmpty($m.Bitmap) -and $m.Bitmap.ToLowerInvariant() -ne 'null') {
            [void]$skinBitmaps.Add($m.Bitmap)
        }
    }
}
"hasSkins = $hasSkins"
"skinBitmaps = { $([string]::Join(', ', $skinBitmaps)) }`n"

"{0,-3} {1,-22} {2,-9} {3,6} {4,-6} {5,-18} {6}" -f '#','Name','Type','Verts','Render','Bitmap','DECISION'
"{0,-3} {1,-22} {2,-9} {3,6} {4,-6} {5,-18} {6}" -f '---','----','----','-----','------','------','--------'
$skipped = 0; $shown = 0; $i = 0
foreach ($m in $meshes) {
    $i++
    $isSkin = $m.GetType().FullName -eq $skinType
    $type = if ($isSkin) { 'skin' } else { 'trimesh' }
    $vc = $m.Vertices.Length
    $bmp = $m.Bitmap

    $decision = ''
    if (-not $m.Render) { $decision = 'HIDE (Render=false)'; $skipped++ }
    elseif ($vc -eq 0 -or $m.Faces.Length -eq 0) { $decision = 'HIDE (empty)'; $skipped++ }
    else {
        # Replay MeshSkipHeuristic.ShouldSkipTrimesh
        $skip = $false
        if ($hasSkins -and (-not $isSkin) -and ($vc -lt $TinyThreshold)) {
            $hasUnique = (-not [string]::IsNullOrEmpty($bmp)) -and ($bmp.ToLowerInvariant() -ne 'null') -and (-not $skinBitmaps.Contains($bmp))
            $skip = -not $hasUnique
        }
        if ($skip) {
            $why = if ([string]::IsNullOrEmpty($bmp)) { 'empty bitmap' }
                   elseif ($bmp.ToLowerInvariant() -eq 'null') { 'null bitmap' }
                   else { "shares skin bmp '$bmp'" }
            $decision = "SKIP tiny trimesh ($why)"; $skipped++
        } else { $decision = 'render'; $shown++ }
    }
    "{0,-3} {1,-22} {2,-9} {3,6} {4,-6} {5,-18} {6}" -f $i, $m.Name, $type, $vc, $m.Render, $bmp, $decision
}
""
"SUMMARY: $($meshes.Count) meshes  |  rendered=$shown  skipped=$skipped"
