param()

$data = Get-Content "$PSScriptRoot/../cache/github-data.json" -Raw | ConvertFrom-Json
$toolLabels = @('parley','quartermaster','fence','manifest','radoub','Trebuchet')
$typeLabels = @('bug','enhancement','tech-debt','documentation','refactor','research','testing')

Write-Host "=== MISSING TOOL LABEL ==="
foreach ($issue in $data.issues) {
    $labels = @($issue.labels.nodes | ForEach-Object { $_.name })
    $hasTool = $false
    foreach ($l in $labels) { if ($toolLabels -contains $l) { $hasTool = $true; break } }
    if (-not $hasTool) {
        $labelStr = ($labels -join ', ')
        Write-Host ("  #{0} {1} [{2}]" -f $issue.number, $issue.title, $labelStr)
    }
}

Write-Host ""
Write-Host "=== MISSING TYPE LABEL ==="
foreach ($issue in $data.issues) {
    $labels = @($issue.labels.nodes | ForEach-Object { $_.name })
    $hasType = $false
    foreach ($l in $labels) { if ($typeLabels -contains $l) { $hasType = $true; break } }
    if (-not $hasType) {
        $labelStr = ($labels -join ', ')
        Write-Host ("  #{0} {1} [{2}]" -f $issue.number, $issue.title, $labelStr)
    }
}

Write-Host ""
Write-Host "=== TITLE STANDARDIZATION ISSUES ==="
foreach ($issue in $data.issues) {
    $title = $issue.title
    # Check for missing tool prefix
    if ($title -notmatch '^\[') {
        Write-Host ("  #{0} MISSING PREFIX: {1}" -f $issue.number, $title)
    }
    # Check for inconsistent type prefix (Bug vs bug, feat vs Feature)
    if ($title -match '\] (Bug|bug|feat|Feature|FR|Refactor|Sprint|Tech Debt):?\s') {
        $match = $Matches[1]
        # Standardize check
        if ($match -ceq 'bug' -or $match -ceq 'Bug') {
            # ok
        }
        elseif ($match -eq 'FR') {
            Write-Host ("  #{0} USE 'feat:' NOT 'FR:': {1}" -f $issue.number, $title)
        }
    }
}

Write-Host ""
Write-Host "=== SUPERSEDED TECH DEBT (multiple split issues for same file) ==="
$splitIssues = @{}
foreach ($issue in $data.issues) {
    if ($issue.title -match 'Split (\S+)\s+\((\d+) lines?\)') {
        $file = $Matches[1]
        if (-not $splitIssues.ContainsKey($file)) {
            $splitIssues[$file] = @()
        }
        $splitIssues[$file] += @{number=$issue.number; lines=$Matches[2]; title=$issue.title}
    }
}
foreach ($file in $splitIssues.Keys) {
    $entries = $splitIssues[$file]
    if ($entries.Count -gt 1) {
        Write-Host "  $file has $($entries.Count) split issues:"
        $sorted = $entries | Sort-Object { [int]$_.lines }
        foreach ($e in $sorted) {
            Write-Host ("    #{0} ({1} lines)" -f $e.number, $e.lines)
        }
        $latest = ($sorted | Select-Object -Last 1)
        Write-Host ("    -> Keep #{0}, close others as superseded" -f $latest.number)
    }
}

Write-Host ""
Write-Host ("Total issues: {0}" -f $data.issues.Count)
