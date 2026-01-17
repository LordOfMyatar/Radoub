<#
.SYNOPSIS
    Extract specific fields from GitHub cache for skill consumption.

.PARAMETER View
    - summary: Just stats (~1KB)
    - list: Issues without bodies (~40KB)
    - issue: Single issue with body (requires -Number)

.PARAMETER Number
    Issue number for single-issue view.

.PARAMETER Tool
    Filter by tool label.

.EXAMPLE
    .\Get-CacheData.ps1 -View summary
    .\Get-CacheData.ps1 -View list -Tool parley
    .\Get-CacheData.ps1 -View issue -Number 123
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet("summary", "list", "issue")]
    [string]$View,

    [int]$Number,

    [string]$Tool
)

$CacheFile = Join-Path $PSScriptRoot "..\cache\github-data.json"

if (-not (Test-Path $CacheFile)) {
    Write-Error "Cache not found. Run Refresh-GitHubCache.ps1"
    exit 1
}

$data = Get-Content $CacheFile | ConvertFrom-Json

switch ($View) {
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
}