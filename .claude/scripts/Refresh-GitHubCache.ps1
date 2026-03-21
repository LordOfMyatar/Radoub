<#
.SYNOPSIS
    Fetches GitHub issue and PR data via GraphQL and caches locally.

.DESCRIPTION
    Paginated GraphQL queries fetch all open issues (in pages of 100) and
    PRs with labels, project board status, milestones, etc.
    Saves to .claude/cache/github-data.json.

    Used by /backlog, /init-item, /pre-merge, and /research commands.

.PARAMETER Force
    Refresh even if cache is fresh (< 1 hour old).

.EXAMPLE
    .\Refresh-GitHubCache.ps1
    .\Refresh-GitHubCache.ps1 -Force
#>

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CacheDir = Join-Path (Split-Path -Parent $ScriptDir) "cache"
$CacheFile = Join-Path $CacheDir "github-data.json"

# Ensure cache directory exists
if (-not (Test-Path $CacheDir)) {
    New-Item -ItemType Directory -Path $CacheDir -Force | Out-Null
}

# Check cache freshness (1 hour = 3600 seconds)
$MaxAgeSeconds = 1 * 60 * 60

if (-not $Force -and (Test-Path $CacheFile)) {
    $cacheAge = (Get-Date) - (Get-Item $CacheFile).LastWriteTime
    if ($cacheAge.TotalSeconds -lt $MaxAgeSeconds) {
        $ageMinutes = [math]::Round($cacheAge.TotalMinutes)
        Write-Host "Cache is fresh ($ageMinutes minutes old). Use -Force to refresh anyway."
        exit 0
    }
}

Write-Host "Fetching GitHub data via GraphQL..."

# Helper: execute a GraphQL query via temp file (avoids PowerShell quote-stripping)
function Invoke-GraphQL {
    param([string]$Query)
    $tempFile = [System.IO.Path]::GetTempFileName()
    try {
        [System.IO.File]::WriteAllText($tempFile, $Query, [System.Text.UTF8Encoding]::new($false))
        $response = gh api graphql -F query="@$tempFile" 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "GraphQL query failed: $response"
            exit 1
        }
        return ($response | ConvertFrom-Json)
    }
    finally {
        Remove-Item -Path $tempFile -ErrorAction SilentlyContinue
    }
}

# --- Fetch issues with pagination (100 per page, GitHub max) ---
$allIssues = @()
$hasNextPage = $true
$cursor = $null
$totalCount = 0
$page = 0

while ($hasNextPage) {
    $page++
    $afterClause = if ($cursor) { ", after: `"$cursor`"" } else { "" }

    $query = @"
{
  repository(owner: "LordOfMyatar", name: "Radoub") {
    issues(first: 100, states: OPEN, orderBy: {field: UPDATED_AT, direction: DESC}$afterClause) {
      totalCount
      pageInfo { hasNextPage endCursor }
      nodes {
        number
        title
        body
        state
        createdAt
        updatedAt
        author { login }
        labels(first: 10) { nodes { name } }
        milestone { title number }
        assignees(first: 5) { nodes { login } }
        comments(first: 20, orderBy: {field: UPDATED_AT, direction: DESC}) {
          totalCount
          nodes {
            author { login }
            body
            createdAt
          }
        }
        projectItems(first: 5) {
          nodes {
            project { title number }
            fieldValues(first: 10) {
              nodes {
                ... on ProjectV2ItemFieldSingleSelectValue {
                  name
                  field { ... on ProjectV2SingleSelectField { name } }
                }
              }
            }
          }
        }
      }
    }
  }
}
"@

    $data = Invoke-GraphQL -Query $query
    $issueData = $data.data.repository.issues
    $totalCount = $issueData.totalCount
    $allIssues += $issueData.nodes
    $hasNextPage = $issueData.pageInfo.hasNextPage
    $cursor = $issueData.pageInfo.endCursor

    if ($page -gt 1) {
        Write-Host "  Page ${page}: fetched $($allIssues.Count) of $totalCount issues..."
    }

    # Safety: max 5 pages (500 issues)
    if ($page -ge 5) {
        Write-Host "  Warning: stopped at 500 issues (safety limit)"
        break
    }
}

# --- Fetch PRs (single page, 20 is plenty) ---
$prQuery = @'
{
  repository(owner: "LordOfMyatar", name: "Radoub") {
    pullRequests(first: 20, states: OPEN, orderBy: {field: UPDATED_AT, direction: DESC}) {
      totalCount
      nodes {
        number
        title
        state
        isDraft
        createdAt
        updatedAt
        author { login }
        labels(first: 10) { nodes { name } }
        reviewDecision
        headRefName
      }
    }
  }
}
'@

$prData = Invoke-GraphQL -Query $prQuery
$prs = $prData.data.repository.pullRequests.nodes
$prTotalCount = $prData.data.repository.pullRequests.totalCount

# --- Calculate summary statistics ---
$labelCounts = @{}
$toolLabels = @("parley", "quartermaster", "manifest", "fence", "radoub", "Trebuchet")
$typeLabels = @("bug", "enhancement", "epic", "sprint", "tech-debt", "documentation", "refactor", "testing", "research")

foreach ($issue in $allIssues) {
    foreach ($label in $issue.labels.nodes) {
        $labelName = $label.name
        if (-not $labelCounts.ContainsKey($labelName)) {
            $labelCounts[$labelName] = 0
        }
        $labelCounts[$labelName]++
    }
}

# Count stale issues (15+ days)
$staleThreshold = (Get-Date).AddDays(-15)
$staleCount = ($allIssues | Where-Object {
    [DateTime]::Parse($_.updatedAt) -lt $staleThreshold
}).Count

# Count issues missing tool labels
$missingToolLabel = ($allIssues | Where-Object {
    $issueLabels = $_.labels.nodes.name
    -not ($toolLabels | Where-Object { $issueLabels -contains $_ })
}).Count

# Count issues missing type labels
$missingTypeLabel = ($allIssues | Where-Object {
    $issueLabels = $_.labels.nodes.name
    -not ($typeLabels | Where-Object { $issueLabels -contains $_ })
}).Count

# Build final cache structure
$now = Get-Date -Format "o"
$cache = @{
    fetchedAt = $now
    repository = "LordOfMyatar/Radoub"
    maxAgeHours = 1
    issues = $allIssues
    pullRequests = $prs
    summary = @{
        totalOpenIssues = $totalCount
        totalOpenPRs = $prTotalCount
        staleIssues = $staleCount
        missingToolLabel = $missingToolLabel
        missingTypeLabel = $missingTypeLabel
        byLabel = $labelCounts
    }
}

# Write cache file
$cache | ConvertTo-Json -Depth 20 | Set-Content -Path $CacheFile -Encoding UTF8

Write-Host "Cache updated: $CacheFile"
Write-Host "  Issues: $totalCount open, $($allIssues.Count) cached ($staleCount stale)"
Write-Host "  PRs: $prTotalCount open"
Write-Host "  Missing tool label: $missingToolLabel"
Write-Host "  Missing type label: $missingTypeLabel"
