<#
.SYNOPSIS
    Check live open/closed state of one or more GitHub issues.

.DESCRIPTION
    The github-data.json cache only contains OPEN issues. Search results from
    Get-CacheData.ps1 may match OPEN issues that *reference* other issues in
    their body — and those referenced issues may have since been closed.

    This script bypasses the cache and queries gh directly so /init-item can
    verify state of related issues before presenting them to the user.

.PARAMETER Numbers
    Comma-separated issue numbers to check (e.g. "1902,1903,1905").

.EXAMPLE
    .\Test-IssueState.ps1 -Numbers "1902,1903,1905"
    # Outputs JSON array: [{number:1902, state:"CLOSED", title:"..."}, ...]
#>

param(
    [Parameter(Mandatory)]
    [string]$Numbers
)

$numberList = $Numbers -split ',' | ForEach-Object { [int]$_.Trim() } | Where-Object { $_ -gt 0 }

$results = @()
foreach ($n in $numberList) {
    $json = gh issue view $n --json number,state,title,closedAt 2>$null
    if ($LASTEXITCODE -eq 0 -and $json) {
        $results += ($json | ConvertFrom-Json)
    } else {
        $results += [PSCustomObject]@{
            number = $n
            state = "UNKNOWN"
            title = $null
            closedAt = $null
        }
    }
}

if ($results.Count -eq 1) {
    # Force single-element array in JSON output for consistent parsing
    "[" + ($results[0] | ConvertTo-Json -Depth 3 -Compress) + "]"
} else {
    $results | ConvertTo-Json -Depth 3
}
