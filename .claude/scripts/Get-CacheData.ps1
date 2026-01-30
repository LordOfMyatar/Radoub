<#
.SYNOPSIS
    Extract specific fields from GitHub cache for skill consumption.

.PARAMETER View
    - status: Human-readable cache status (age, freshness, counts)
    - summary: Just stats as JSON (~1KB)
    - list: Issues without bodies (~40KB)
    - issue: Single issue with body (requires -Number)
    - search: Search issues by keyword (requires -Query)

.PARAMETER Number
    Issue number for single-issue view.

.PARAMETER Tool
    Filter by tool label.

.PARAMETER Query
    Search term for search view (case-insensitive, searches title and body).

.EXAMPLE
    .\Get-CacheData.ps1 -View status
    .\Get-CacheData.ps1 -View summary
    .\Get-CacheData.ps1 -View list -Tool parley
    .\Get-CacheData.ps1 -View issue -Number 123
    .\Get-CacheData.ps1 -View search -Query "script browser"
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet("status", "summary", "list", "issue", "search")]
    [string]$View,

    [int]$Number,

    [string]$Tool,

    [string]$Query
)

$CacheFile = Join-Path $PSScriptRoot "..\cache\github-data.json"

if (-not (Test-Path $CacheFile)) {
    Write-Error "Cache not found. Run Refresh-GitHubCache.ps1"
    exit 1
}

$data = Get-Content $CacheFile | ConvertFrom-Json

switch ($View) {
    "status" {
        $cacheAge = (Get-Date) - (Get-Item $CacheFile).LastWriteTime
        $ageStr = if ($cacheAge.TotalHours -ge 1) { "{0:N1} hours" -f $cacheAge.TotalHours } else { "{0:N0} minutes" -f $cacheAge.TotalMinutes }
        $fresh = if ($cacheAge.TotalHours -lt 1) { "Yes" } else { "No (stale)" }

        Write-Host "Cache Status"
        Write-Host "------------"
        Write-Host "Age: $ageStr"
        Write-Host "Fresh: $fresh"
        Write-Host "Issues: $($data.summary.totalOpenIssues) open ($($data.summary.staleIssues) stale)"
        Write-Host "PRs: $($data.summary.totalOpenPRs) open"
        Write-Host "Missing tool label: $($data.summary.missingToolLabel)"
        Write-Host "Missing type label: $($data.summary.missingTypeLabel)"
    }

    "summary" {
        @{
            fetchedAt = $data.fetchedAt
            summary = $data.summary
        } | ConvertTo-Json -Depth 5
    }

    "list" {
        $issues = $data.issues
        if ($Tool) {
            $issues = $issues | Where-Object { $_.labels.nodes.name -contains $Tool }
        }

        $listIssues = $issues | ForEach-Object {
            @{
                number = $_.number
                title = $_.title
                updatedAt = $_.updatedAt
                author = $_.author.login
                labels = ($_.labels.nodes.name -join ", ")
            }
        }

        @{
            fetchedAt = $data.fetchedAt
            summary = $data.summary
            issueCount = $listIssues.Count
            issues = $listIssues
            pullRequests = $data.pullRequests | ForEach-Object {
                @{
                    number = $_.number
                    title = $_.title
                    isDraft = $_.isDraft
                    branch = $_.headRefName
                }
            }
        } | ConvertTo-Json -Depth 5
    }

    "issue" {
        if (-not $Number) {
            Write-Error "-Number required"
            exit 1
        }

        $issue = $data.issues | Where-Object { $_.number -eq $Number }
        if (-not $issue) {
            Write-Error "Issue #$Number not found"
            exit 1
        }

        @{
            number = $issue.number
            title = $issue.title
            body = $issue.body
            updatedAt = $issue.updatedAt
            author = $issue.author.login
            labels = ($issue.labels.nodes.name -join ", ")
        } | ConvertTo-Json -Depth 5
    }

    "search" {
        if (-not $Query) {
            Write-Error "-Query required for search view"
            exit 1
        }

        $foundIssues = $data.issues | Where-Object {
            $_.title -match $Query -or $_.body -match $Query
        }

        if ($Tool) {
            $foundIssues = $foundIssues | Where-Object { $_.labels.nodes.name -contains $Tool }
        }

        if ($foundIssues.Count -eq 0) {
            Write-Host "No issues found matching '$Query'"
            exit 0
        }

        Write-Host "Found $($foundIssues.Count) issues matching '$Query':"
        Write-Host ""
        $foundIssues | ForEach-Object {
            $labels = ($_.labels.nodes.name -join ", ")
            Write-Host "#$($_.number): $($_.title)"
            if ($labels) { Write-Host "  Labels: $labels" }
        }
    }
}