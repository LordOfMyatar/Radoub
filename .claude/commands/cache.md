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
- 1-hour freshness window reduces API calls during active sessions
- Skills read from cache for instant access

**Used by:** `/backlog`, `/sprint-planning`, `/grooming`

## Workflow

### Show Status (default)

```bash
pwsh -File .claude/scripts/Get-CacheData.ps1 -View status
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
  "maxAgeHours": 1,
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

## Reading Cache Data

The full cache is ~220KB (too large for context). Use `Get-CacheData.ps1` to extract views:

```bash
# Summary only (~1KB) - just stats
pwsh -File .claude/scripts/Get-CacheData.ps1 -View summary

# List view (~25KB) - issues without bodies
pwsh -File .claude/scripts/Get-CacheData.ps1 -View list

# List filtered by tool
pwsh -File .claude/scripts/Get-CacheData.ps1 -View list -Tool parley

# Single issue with body
pwsh -File .claude/scripts/Get-CacheData.ps1 -View issue -Number 123
```

## Integration with Skills

Skills should:
1. Check cache freshness at start:
   ```bash
   pwsh -File .claude/scripts/Refresh-GitHubCache.ps1
   ```
2. Use `Get-CacheData.ps1 -View list` for batch operations
3. Use `Get-CacheData.ps1 -View issue -Number N` when body is needed

The refresh script exits immediately if cache is fresh (< 1 hour).

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