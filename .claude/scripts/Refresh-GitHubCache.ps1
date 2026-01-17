<#
.SYNOPSIS
    Fetches GitHub issue and PR data via GraphQL and caches locally.

.DESCRIPTION
    Single GraphQL query fetches all open issues and PRs with labels,
    project board status, milestones, etc. Saves to .claude/cache/github-data.json.

    Used by /backlog, /sprint-planning, and /grooming skills.

.PARAMETER Force
    Refresh even if cache is fresh (< 4 hours old).

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

# GraphQL query - fetches everything skills need in one call
$query = @'
{
  repository(owner: "LordOfMyatar", name: "Radoub") {
    issues(first: 100, states: OPEN, orderBy: {field: UPDATED_AT, direction: DESC}) {
      totalCount
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

# Execute GraphQL query
try {
    $response = gh api graphql -f query=$query 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "GraphQL query failed: $response"
        exit 1
    }
}
catch {
    Write-Error "Failed to execute gh api: $_"
    exit 1
}

# Parse response
$data = $response | ConvertFrom-Json

# Build cache object with metadata
$now = Get-Date -Format "o"
$issues = $data.data.repository.issues.nodes
$prs = $data.data.repository.pullRequests.nodes

# Calculate summary statistics
$labelCounts = @{}
$toolLabels = @("parley", "quartermaster", "manifest", "fence", "radoub")
$typeLabels = @("bug", "enhancement", "epic", "sprint", "tech-debt", "documentation", "refactor")

foreach ($issue in $issues) {
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
$staleCount = ($issues | Where-Object {
    [DateTime]::Parse($_.updatedAt) -lt $staleThreshold
}).Count

# Count issues missing tool labels
$missingToolLabel = ($issues | Where-Object {
    $issueLabels = $_.labels.nodes.name
    -not ($toolLabels | Where-Object { $issueLabels -contains $_ })
}).Count

# Count issues missing type labels
$missingTypeLabel = ($issues | Where-Object {
    $issueLabels = $_.labels.nodes.name
    -not ($typeLabels | Where-Object { $issueLabels -contains $_ })
}).Count

# Build final cache structure
$cache = @{
    fetchedAt = $now
    repository = "LordOfMyatar/Radoub"
    maxAgeHours = 1
    issues = $issues
    pullRequests = $prs
    summary = @{
        totalOpenIssues = $data.data.repository.issues.totalCount
        totalOpenPRs = $data.data.repository.pullRequests.totalCount
        staleIssues = $staleCount
        missingToolLabel = $missingToolLabel
        missingTypeLabel = $missingTypeLabel
        byLabel = $labelCounts
    }
}

# Write cache file
$cache | ConvertTo-Json -Depth 20 | Set-Content -Path $CacheFile -Encoding UTF8

Write-Host "Cache updated: $CacheFile"
Write-Host "  Issues: $($cache.summary.totalOpenIssues) open ($staleCount stale)"
Write-Host "  PRs: $($cache.summary.totalOpenPRs) open"
Write-Host "  Missing tool label: $missingToolLabel"
Write-Host "  Missing type label: $missingTypeLabel"