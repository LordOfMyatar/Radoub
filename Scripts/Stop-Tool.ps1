# Stop a running Radoub tool launched for log capture (mutual-testing loop).
# Stable wrapper so the invocation string never varies — it matches the committed
# PS7 "Scripts/*" permission rule, instead of brittle per-variant Stop-Process rules.
#
#   & "C:\Program Files\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass `
#       -File "Scripts/Stop-Tool.ps1" -Tool Quartermaster

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Quartermaster', 'Relique', 'Reliquary', 'Fence', 'Parley', 'Manifest', 'Trebuchet')]
    [string] $Tool
)

$procs = Get-Process -Name $Tool -ErrorAction SilentlyContinue
if (-not $procs) {
    Write-Host "$Tool not running."
    return
}
$procs | Stop-Process -Force
Start-Sleep -Milliseconds 800
if (Get-Process -Name $Tool -ErrorAction SilentlyContinue) {
    Write-Host "$Tool still running after Stop-Process."
} else {
    Write-Host "$Tool stopped ($($procs.Count) process(es))."
}
