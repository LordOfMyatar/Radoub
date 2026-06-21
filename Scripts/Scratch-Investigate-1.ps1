$ErrorActionPreference = 'Stop'
# READ-ONLY: show full Bash commands that start with 'cd' to see the prefix-drift problem.

$root = Join-Path $env:USERPROFILE '.claude\projects'
$files = Get-ChildItem -Path $root -Recurse -Filter *.jsonl -File |
    Sort-Object LastWriteTime -Descending | Select-Object -First 50

$cmds = @()
foreach ($f in $files) {
    foreach ($line in [System.IO.File]::ReadLines($f.FullName)) {
        if ($line -notmatch 'tool_use') { continue }
        try { $obj = $line | ConvertFrom-Json -ErrorAction Stop } catch { continue }
        $content = $obj.message.content
        if ($null -eq $content) { continue }
        foreach ($c in $content) {
            if ($c.type -ne 'tool_use') { continue }
            if ($c.name -ne 'Bash') { continue }
            $cmd = $c.input.command
            if ($cmd -match '^\s*cd\b') { $cmds += $cmd }
        }
    }
}
Write-Host "=== Commands starting with 'cd' (count=$($cmds.Count)) ==="
$cmds | ForEach-Object { ($_ -split '\r?\n')[0] } | Group-Object | Sort-Object Count -Descending |
    ForEach-Object { "{0,4}  {1}" -f $_.Count, $_.Name }
