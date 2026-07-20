# Removes allow rules that can never match from .claude/settings.local.json.
#
# Four categories of unreachable rule:
#   1. Doubled backslashes  - upstream anthropics/claude-code#57013; the saved rule
#                             has \\ where the real command has \, so it never matches.
#   2. PowerShell(...)      - the PowerShell tool's rules never match on Windows
#                             (#57013/#60289/#42318). Every such rule is dead.
#   3. cd-prefix rules      - .claude/hooks/check-cd-prefix.sh blocks the command
#                             before permissions are consulted.
#   4. Rules containing ..  - the Bash(*..*) / PowerShell(*..*) deny guard wins.
#
# Writes a timestamped .bak next to the settings file. Run with -WhatIf to preview.

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$SettingsPath = 'd:\LOM\workspace\Radoub\.claude\settings.local.json'
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $SettingsPath)) { throw "Settings file not found: $SettingsPath" }

$raw  = Get-Content $SettingsPath -Raw
$json = $raw | ConvertFrom-Json
$before = @($json.permissions.allow)

$bs2 = [string][char]92 + [char]92

function Test-Unreachable([string]$rule) {
    if ($rule -match [regex]::Escape($bs2)) { return 'doubled-backslash' }
    if ($rule -like 'PowerShell(*')         { return 'powershell-tool' }
    # Any cd-into-the-repo prefix, quoted or not, either slash style, either case.
    if ($rule -match '(?i)cd\s+"?[dD]:[\\/]LOM') { return 'cd-prefix-hook' }
    if ($rule -like '*..*')                 { return 'deny-shadowed' }
    return $null
}

$removed = @()
$kept    = @()
foreach ($r in $before) {
    $why = Test-Unreachable $r
    if ($why) { $removed += [pscustomobject]@{ Reason = $why; Rule = $r } }
    else      { $kept += $r }
}

Write-Output "Settings : $SettingsPath"
Write-Output "Before   : $($before.Count) allow rules"
Write-Output "Removing : $($removed.Count)"
$removed | Group-Object Reason | Sort-Object Name | ForEach-Object {
    "  {0,-20} {1}" -f $_.Name, $_.Count
}
Write-Output "After    : $($kept.Count)"

if ($removed.Count -eq 0) { Write-Output "Nothing to do."; return }

if ($PSCmdlet.ShouldProcess($SettingsPath, "Remove $($removed.Count) unreachable allow rules")) {
    $stamp  = Get-Date -Format 'yyyyMMdd_HHmmss'
    $backup = "$SettingsPath.$stamp.bak"
    Copy-Item $SettingsPath $backup
    Write-Output "Backup   : $backup"

    $json.permissions.allow = $kept
    $json | ConvertTo-Json -Depth 100 | Set-Content $SettingsPath -Encoding UTF8

    # Re-read to prove the result still parses.
    $check = Get-Content $SettingsPath -Raw | ConvertFrom-Json
    Write-Output "Verified : $($check.permissions.allow.Count) allow rules, JSON parses"
}
