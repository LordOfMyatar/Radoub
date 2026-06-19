# SCRATCH INVESTIGATION SCRIPT 1 (committed, reusable)
# ---------------------------------------------------------------------------
# PURPOSE: A throwaway investigation slot that Claude EDITS IN PLACE instead of
#   creating a new file each time (file creation prompts the user; editing does not).
#   Claude rewrites this body for whatever one-off, READ-ONLY investigation is at hand.
#
# RULES (enforced by convention — see CLAUDE.md "Scratch Investigation Scripts"):
#   - READ-ONLY / INVESTIGATION ONLY. No writes, deletes, moves, or mutation of game
#     files, module files, repo files, git state, or GitHub. No Set-Content/Remove-Item/
#     Move-Item/New-Item, no `git`/`gh` mutations, no overwriting fixtures.
#   - PS7 ONLY when loading net9.0 Radoub DLLs (full path to pwsh 7).
#   - Output to stdout only. If a finding is worth keeping, Claude writes it to a
#     NonPublic research doc, not from this script.
#   - Current contents are disposable; the NEXT investigation overwrites this body.
# ---------------------------------------------------------------------------
# CURRENT INVESTIGATION: #2498 — does MeshSkipHeuristic skip Render=true meshes the
#   MDL Render flag would not already hide? (Census across all c_* creature MDLs.)
#
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/Scratch-Investigate-1.ps1"
# ---------------------------------------------------------------------------

param(
    [string] $ModuleDir = (Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Neverwinter Nights\modules\LNS_DLG'),
    [int]    $TinyThreshold = 30,
    [string] $Prefix = 'c_',
    [int]    $Limit = 0
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll

$gameData = [Radoub.Formats.Services.GameDataService]::new()
if (-not $gameData.IsConfigured) { throw "GameDataService not configured." }
$gameData.ConfigureModuleHaks($ModuleDir)

$mdlType  = [Radoub.Formats.Common.ResourceTypes]::Mdl   # NOTE: never name a var $mdl here —
$skinType = 'Radoub.Formats.Mdl.MdlSkinNode'             # PS is case-insensitive, $mdl/$MDL collide.
$reader   = [Radoub.Formats.Mdl.MdlReader]::new()

$all = @($gameData.ListResources($mdlType))
$names = $all | ForEach-Object { $_.ResRef.ToLowerInvariant() } |
    Where-Object { $Prefix -eq '' -or $_.StartsWith($Prefix) } |
    Sort-Object -Unique
if ($Limit -gt 0) { $names = $names | Select-Object -First $Limit }
"MDL resources: $($all.Count) total, $($names.Count) match prefix '$Prefix'`n"

$scanned = 0; $withSkins = 0; $heuristicSkips = 0
$divergent = New-Object System.Collections.Generic.List[string]
$detail    = New-Object System.Collections.Generic.List[string]

foreach ($name in $names) {
    $bytes = $null
    try { $bytes = $gameData.FindResource($name, $mdlType) } catch { continue }
    if ($null -eq $bytes -or $bytes.Length -eq 0) { continue }
    $model = $null
    try { $model = $reader.Parse($bytes) } catch { continue }
    $scanned++
    $meshes = @($model.GetMeshNodes())
    if ($meshes.Count -eq 0) { continue }

    $hasSkins = $false
    $skinBitmaps = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($m in $meshes) {
        if (($m.GetType().FullName -eq $skinType) -and $m.Render -and $m.Vertices.Length -gt 0) {
            $hasSkins = $true
            if (-not [string]::IsNullOrEmpty($m.Bitmap) -and $m.Bitmap.ToLowerInvariant() -ne 'null') { [void]$skinBitmaps.Add($m.Bitmap) }
        }
    }
    if (-not $hasSkins) { continue }
    $withSkins++

    $diverged = $false
    foreach ($m in $meshes) {
        $isSkin = $m.GetType().FullName -eq $skinType
        $vc = $m.Vertices.Length
        if (-not $m.Render) { continue }                    # Gate 1: Render flag (first)
        if ($vc -eq 0 -or $m.Faces.Length -eq 0) { continue }
        if ($hasSkins -and (-not $isSkin) -and ($vc -lt $TinyThreshold)) {   # Gate 2: heuristic
            $bmp = $m.Bitmap
            $hasUnique = (-not [string]::IsNullOrEmpty($bmp)) -and ($bmp.ToLowerInvariant() -ne 'null') -and (-not $skinBitmaps.Contains($bmp))
            if (-not $hasUnique) {
                $heuristicSkips++; $diverged = $true
                $why = if ([string]::IsNullOrEmpty($bmp)) { 'empty bitmap' } elseif ($bmp.ToLowerInvariant() -eq 'null') { 'null bitmap' } else { "shares skin bmp '$bmp'" }
                $detail.Add(("  {0,-18} mesh '{1}' ({2} verts, {3})" -f $name, $m.Name, $vc, $why))
            }
        }
    }
    if ($diverged) { $divergent.Add($name) }
}

"=== CENSUS COMPLETE ==="
"Models scanned (parsed OK)        : $scanned"
"Models with skin meshes           : $withSkins"
"Render=true meshes skipped by heuristic ONLY : $heuristicSkips"
"Models where heuristic diverges   : $($divergent.Count)"
""
if ($heuristicSkips -gt 0) {
    "Heuristic is LOAD-BEARING: it hides $heuristicSkips Render=true meshes the Render flag misses."
    "First 30 detail lines:"; $detail | Select-Object -First 30 | ForEach-Object { $_ }
} else {
    "Heuristic skipped ZERO Render=true meshes — redundant on this content."
}
