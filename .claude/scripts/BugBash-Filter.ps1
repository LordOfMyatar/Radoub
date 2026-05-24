<#
.SYNOPSIS
    Filter cached issues for bug-bash sprint planning.

.DESCRIPTION
    Reads .claude/cache/github-data.json and emits bug-tagged issues with
    age/inactivity, optionally excluding one or more labels (e.g. model-rendering).
    Also supports a title-keyword exclusion to drop issues that lack a label but
    clearly belong to the excluded area.

.PARAMETER ExcludeLabels
    Labels to exclude (comma-separated or array). Default: model-rendering.

.PARAMETER ExcludeTitleRegex
    Regex of title keywords to exclude. Default catches rendering-by-symptom
    issues that lack the model-rendering label.

.PARAMETER Tool
    Optional tool label filter (parley, quartermaster, etc.).

.PARAMETER CacheFile
    Path to the cache JSON. Defaults to repo cache location.

.PARAMETER AsJson
    Emit JSON instead of a formatted table.

.EXAMPLE
    # Default bug-bash filter
    .\BugBash-Filter.ps1

.EXAMPLE
    # Strict label-only exclusion
    .\BugBash-Filter.ps1 -ExcludeTitleRegex ''

.EXAMPLE
    # Exclude both rendering and accessibility, Trebuchet only
    .\BugBash-Filter.ps1 -ExcludeLabels model-rendering,accessibility -Tool Trebuchet

.EXAMPLE
    # Machine-readable
    .\BugBash-Filter.ps1 -AsJson
#>

param(
    [string[]]$ExcludeLabels = @('model-rendering'),
    [string]$ExcludeTitleRegex = 'render|3D preview|animation playback|floating body|floating fangs|preview has floating|broken/floating',
    [string]$Tool,
    [string]$CacheFile,
    [switch]$AsJson
)

if (-not $CacheFile) {
    $CacheFile = Join-Path $PSScriptRoot "..\cache\github-data.json"
}

if (-not (Test-Path $CacheFile)) {
    Write-Error "Cache not found: $CacheFile. Run Refresh-GitHubCache.ps1"
    exit 1
}

$d = Get-Content $CacheFile | ConvertFrom-Json

$bugs = $d.issues | Where-Object {
    $names = $_.labels.nodes.name
    if ($names -notcontains 'bug') { return $false }
    foreach ($excl in $ExcludeLabels) {
        if ($names -contains $excl) { return $false }
    }
    if ($Tool -and ($names -notcontains $Tool)) { return $false }
    if ($ExcludeTitleRegex -and $_.title -match $ExcludeTitleRegex) { return $false }
    return $true
}

$rows = $bugs | ForEach-Object {
    $names = $_.labels.nodes.name
    [PSCustomObject]@{
        number       = $_.number
        title        = $_.title
        ageDays      = [int]((Get-Date) - [datetime]$_.createdAt).TotalDays
        inactiveDays = [int]((Get-Date) - [datetime]$_.updatedAt).TotalDays
        labels       = ($names -join ',')
        assigned     = ($_.assignees.nodes.login -join ',')
        comments     = $_.comments.totalCount
    }
} | Sort-Object ageDays -Descending

if ($AsJson) {
    $rows | ConvertTo-Json -Depth 4
} else {
    "Bug-bash candidates: $($rows.Count)  (excluded labels: $($ExcludeLabels -join ', '); tool: $(if($Tool){$Tool}else{'all'}))"
    ""
    $rows | Format-Table -AutoSize -Wrap
}
