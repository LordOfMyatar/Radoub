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

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll

$p = [Radoub.Formats.Tokens.TokenParser]::new()

# EXACT test lines as given to the user (literal text, NOT escaped bytes).
# Line 2 contained a literal "\xFF\x00\x00" inside a color tag — reproduce verbatim.
$line2_literal = 'Step forward, <Lord/Lady>. A <Boy/Girl> like you, raised by a <Brother/Sister> - the <c\xFF\x00\x00>finest</c> youth this town has seen.'

# What the color tag would look like with REAL bytes (how it should have been written):
$realColor = "Step forward, <Lord/Lady>. A <Boy/Girl> like you - the <c$([char]0xFF)$([char]0x00)$([char]0x00)>finest</c> youth."

$cases = @($line2_literal, $realColor)
foreach ($c in $cases) {
    Write-Host "IN : $c"
    Write-Host "OUT: $($p.GetSpeechText($c))"
    Write-Host "--- segments ---"
    foreach ($seg in $p.Parse($c)) {
        "  [{0}] raw='{1}' display='{2}'" -f $seg.GetType().Name, $seg.RawText, $seg.DisplayText
    }
    Write-Host ""
}
