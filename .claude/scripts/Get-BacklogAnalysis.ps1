param(
    [switch]$TechDebt,
    [switch]$Stale,
    [switch]$MissingLabels,
    [int]$StaleDays = 15
)

$cache = Get-Content '.\.claude\cache\github-data.json' -Raw | ConvertFrom-Json
$now = Get-Date

function Get-IssueDays($issue) {
    [math]::Floor(($now - [DateTime]::Parse($issue.updatedAt)).TotalDays)
}

function Get-LabelString($issue) {
    # Handle both string labels and GraphQL nested structure
    if ($issue.labels -is [string]) {
        return $issue.labels
    }
    if ($issue.labels.nodes) {
        return ($issue.labels.nodes | ForEach-Object { $_.name }) -join ', '
    }
    return ''
}

if ($TechDebt) {
    Write-Host "=== TECH DEBT & REFACTOR ISSUES ===" -ForegroundColor Cyan
    $cache.issues | ForEach-Object {
        $labels = Get-LabelString $_
        if ($labels -match 'tech-debt|refactor') {
            $days = Get-IssueDays $_
            Write-Host "#$($_.number) ($days d) [$labels] $($_.title)"
        }
    }
}

if ($Stale) {
    Write-Host "`n=== STALE ISSUES ($StaleDays+ days) ===" -ForegroundColor Yellow
    $cache.issues | ForEach-Object {
        $days = Get-IssueDays $_
        if ($days -ge $StaleDays) {
            $labels = Get-LabelString $_
            [PSCustomObject]@{ Number=$_.number; Days=$days; Labels=$labels; Title=$_.title }
        }
    } | Sort-Object Days -Descending | Select-Object -First 30 | ForEach-Object {
        Write-Host "#$($_.Number) ($($_.Days)d) [$($_.Labels)] $($_.Title)"
    }
}

if ($MissingLabels) {
    Write-Host "`n=== MISSING TOOL LABEL ===" -ForegroundColor Red
    $cache.issues | ForEach-Object {
        $labels = Get-LabelString $_
        if ($labels -notmatch 'parley|manifest|quartermaster|fence|trebuchet|radoub') {
            Write-Host "#$($_.number) [$labels] $($_.title)"
        }
    }

    Write-Host "`n=== MISSING TYPE LABEL ===" -ForegroundColor Red
    $cache.issues | ForEach-Object {
        $labels = Get-LabelString $_
        if ($labels -notmatch 'enhancement|bug|tech-debt|refactor|documentation|epic|sprint') {
            Write-Host "#$($_.number) [$labels] $($_.title)"
        }
    }
}

if (-not $TechDebt -and -not $Stale -and -not $MissingLabels) {
    # Default: show all
    & $PSCommandPath -TechDebt
    & $PSCommandPath -Stale
    & $PSCommandPath -MissingLabels
}
