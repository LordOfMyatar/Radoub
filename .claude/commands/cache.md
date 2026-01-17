# GitHub Data Cache Management

Manage the local cache of GitHub issues and PRs used by planning skills.

## Usage

```
/cache [command]
```

**Commands:**
- No args: Show cache status
- `refresh`: Force refresh the cache
- `clear`: Delete the cache file

## Cache Overview

The cache stores GitHub data fetched via GraphQL in `.claude/cache/github-data.json`.

**Benefits:**
- Single API call fetches all data (vs. multiple `gh issue list` calls)
- 4-hour freshness window reduces API calls during active sessions
- Skills read from cache for instant access

**Used by:** `/backlog`, `/sprint-planning`, `/grooming`

## Workflow

### Show Status (default)

```bash
# Check if cache exists and its age
pwsh -Command "
    \$cache = '.claude/cache/github-data.json'
    if (Test-Path \$cache) {
        \$data = Get-Content \$cache | ConvertFrom-Json
        \$age = (Get-Date) - (Get-Item \$cache).LastWriteTime
        \$ageStr = if (\$age.TotalHours -ge 1) { '{0:N1} hours' -f \$age.TotalHours } else { '{0:N0} minutes' -f \$age.TotalMinutes }
        \$fresh = if (\$age.TotalHours -lt 4) { 'Yes' } else { 'No (stale)' }
        Write-Host 'Cache Status'
        Write-Host '------------'
        Write-Host \"Age: \$ageStr\"
        Write-Host \"Fresh: \$fresh\"
        Write-Host \"Issues: \$(\$data.summary.totalOpenIssues) open (\$(\$data.summary.staleIssues) stale)\"
        Write-Host \"PRs: \$(\$data.summary.totalOpenPRs) open\"
        Write-Host \"Missing tool label: \$(\$data.summary.missingToolLabel)\"
        Write-Host \"Missing type label: \$(\$data.summary.missingTypeLabel)\"
    } else {
        Write-Host 'No cache exists. Run /cache refresh to create.'
    }
"
```

### Refresh

Force refresh the cache regardless of age:

```bash
pwsh -File .claude/scripts/Refresh-GitHubCache.ps1 -Force
```

### Clear

Delete the cache file:

```bash
rm -f .claude/cache/github-data.json
echo "Cache cleared."
```

## Cache Structure

```json
{
  "fetchedAt": "2026-01-16T12:00:00Z",
  "repository": "LordOfMyatar/Radoub",
  "maxAgeHours": 4,
  "issues": [...],
  "pullRequests": [...],
  "summary": {
    "totalOpenIssues": 116,
    "totalOpenPRs": 2,
    "staleIssues": 9,
    "missingToolLabel": 2,
    "missingTypeLabel": 13,
    "byLabel": { "parley": 45, "quartermaster": 20, ... }
  }
}
```

## Integration with Skills

Skills should check cache freshness at start:

```bash
# Auto-refresh if stale or missing
pwsh -File .claude/scripts/Refresh-GitHubCache.ps1
```

The script exits immediately if cache is fresh (< 4 hours), so it's safe to call every time.

## Output Format

### Status Display

```markdown
## Cache Status

| Field | Value |
|-------|-------|
| Age | 45 minutes |
| Fresh | Yes |
| Issues | 116 open (9 stale) |
| PRs | 2 open |
| Missing tool label | 2 |
| Missing type label | 13 |

**Top Labels:**
- quartermaster: 58
- enhancement: 74
- priority-low: 46
```

### After Refresh

```markdown
## Cache Refreshed

Fetched at: 2026-01-16 12:00:00

| Metric | Count |
|--------|-------|
| Open Issues | 116 |
| Open PRs | 2 |
| Stale (15+ days) | 9 |
| Missing tool label | 2 |
| Missing type label | 13 |
```