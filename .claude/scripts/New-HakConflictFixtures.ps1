<#
.SYNOPSIS
  Generate a throwaway pair of conflicting .hak files to manually exercise
  Trebuchet's HAK conflict checker (#1162).

.DESCRIPTION
  Builds two small HAK archives via the built Radoub.Formats.dll (ErfWriter).
  The pair contains a deliberate MIX so detection can be verified end to end:

    conflict_a.hak (intended HIGHER priority — list it first)
    conflict_b.hak (intended LOWER priority)

    shared_scr   .nss   in BOTH  -> CONFLICT, winner = conflict_a
    shared_item  .uti   in BOTH  -> CONFLICT, winner = conflict_a
    dup_diff     .nss in A, .utc in B  -> NOT a conflict (same ResRef, different type)
    only_a       .nss   A only   -> NOT a conflict
    only_b       .utc   B only   -> NOT a conflict

  Expected result in the checker: exactly 2 conflicts (shared_scr, shared_item),
  each won by conflict_a.

  Requires PowerShell 7 (loads the net9.0 Radoub.Formats.dll). Run with:
    & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
      -File ".claude/scripts/New-HakConflictFixtures.ps1" -OutDir "<folder>"

.PARAMETER OutDir
  Folder to write conflict_a.hak / conflict_b.hak into. Created if missing.
  Defaults to NonPublic/HakConflictFixtures.
#>
param(
    [string]$OutDir = "NonPublic/HakConflictFixtures"
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dll = Join-Path $repoRoot "Radoub.Formats/Radoub.Formats/bin/Debug/net9.0/Radoub.Formats.dll"
if (-not (Test-Path $dll)) {
    throw "Radoub.Formats.dll not found at $dll — build the solution first (dotnet build)."
}
Add-Type -Path $dll

if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = Join-Path $repoRoot $OutDir
}
New-Item -ItemType Directory -Force $OutDir | Out-Null

# Resource type ids (from Radoub.Formats.Common.ResourceTypes)
$NSS = [Radoub.Formats.Common.ResourceTypes]::Nss   # 2009
$UTI = [Radoub.Formats.Common.ResourceTypes]::Uti   # 2025
$UTC = [Radoub.Formats.Common.ResourceTypes]::Utc   # 2027

function New-Hak {
    param(
        [string]$Path,
        # Each entry: @{ ResRef=...; Type=...; Body=... }
        [object[]]$Entries
    )

    $erf = [Radoub.Formats.Erf.ErfFile]::new()
    $erf.FileType = "HAK "
    $erf.FileVersion = "V1.0"

    $data = [System.Collections.Generic.Dictionary[System.ValueTuple[string, uint16], byte[]]]::new()

    [uint32]$id = 0
    foreach ($e in $Entries) {
        $res = [Radoub.Formats.Erf.ErfResourceEntry]::new()
        $res.ResRef = $e.ResRef
        $res.ResId = $id
        $res.ResourceType = [uint16]$e.Type
        $erf.Resources.Add($res)

        $bytes = [System.Text.Encoding]::ASCII.GetBytes($e.Body)
        $key = [System.ValueTuple[string, uint16]]::new($e.ResRef, [uint16]$e.Type)
        $data[$key] = $bytes
        $id++
    }

    [Radoub.Formats.Erf.ErfWriter]::Write($erf, $Path, $data)
    Write-Host "  wrote $([System.IO.Path]::GetFileName($Path)) ($($Entries.Count) resources)"
}

Write-Host "Generating HAK conflict fixtures into: $OutDir"

# conflict_a.hak — intended higher priority (winner of the conflicts)
New-Hak -Path (Join-Path $OutDir "conflict_a.hak") -Entries @(
    @{ ResRef = "shared_scr";  Type = $NSS; Body = "// shared_scr from conflict_a (WINNER)" },
    @{ ResRef = "shared_item"; Type = $UTI; Body = "shared_item-A" },
    @{ ResRef = "dup_diff";    Type = $NSS; Body = "// dup_diff as a SCRIPT in A" },
    @{ ResRef = "only_a";      Type = $NSS; Body = "// unique to A" }
)

# conflict_b.hak — intended lower priority (loses the conflicts)
New-Hak -Path (Join-Path $OutDir "conflict_b.hak") -Entries @(
    @{ ResRef = "shared_scr";  Type = $NSS; Body = "// shared_scr from conflict_b (loser)" },
    @{ ResRef = "shared_item"; Type = $UTI; Body = "shared_item-B" },
    @{ ResRef = "dup_diff";    Type = $UTC; Body = "dup_diff as a CREATURE in B" },
    @{ ResRef = "only_b";      Type = $UTC; Body = "unique to B" }
)

Write-Host ""
Write-Host "Done. To exercise the checker:"
Write-Host "  1. Copy both .hak into a module's hak search path (e.g. ~/Documents/Neverwinter Nights/hak)."
Write-Host "  2. Add 'conflict_a' then 'conflict_b' to the module's HAK list (first = higher priority)."
Write-Host "  3. Open the module in Trebuchet -> Module Editor -> HAK Files -> Check Conflicts."
Write-Host ""
Write-Host "Expected: 2 conflicts — shared_scr (.nss) and shared_item (.uti), both won by conflict_a."
Write-Host "Must NOT appear: dup_diff (same ResRef, different type), only_a, only_b."
