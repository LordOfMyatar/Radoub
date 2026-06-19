# SCRATCH INVESTIGATION SCRIPT 2 (committed, reusable)
# ---------------------------------------------------------------------------
# Second throwaway investigation slot. Same rules as Scratch-Investigate-1.ps1:
# Claude EDITS THIS IN PLACE (no new-file prompt) for a one-off, READ-ONLY investigation.
#
# RULES (see CLAUDE.md "Scratch Investigation Scripts"):
#   - READ-ONLY / INVESTIGATION ONLY. No writes/deletes/moves/mutation of any file,
#     module, fixture, repo, git, or GitHub state. No Set-Content/Remove-Item/Move-Item/
#     New-Item, no `git`/`gh` mutations.
#   - PS7 ONLY when loading net9.0 Radoub DLLs.
#   - stdout only; keep findings in a NonPublic research doc, not in this script.
#   - Disposable: the next investigation overwrites this body.
# ---------------------------------------------------------------------------
# CURRENT INVESTIGATION: (idle — no active investigation)
#
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File ".claude/scripts/Scratch-Investigate-2.ps1"
# ---------------------------------------------------------------------------

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$dll = Join-Path $repoRoot 'Radoub.Formats\Radoub.Formats\bin\Debug\net9.0\Radoub.Formats.dll'
Add-Type -Path $dll

"Scratch-Investigate-2 is idle. Edit the body to run a read-only investigation."
