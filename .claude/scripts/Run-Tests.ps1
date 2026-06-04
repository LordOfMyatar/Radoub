#Requires -Version 7
<#
.SYNOPSIS
  Run targeted dotnet tests and write a clean summary file Claude can Read.

.DESCRIPTION
  Single approvable entry point for test runs so permissions match one prefix
  instead of infinite `dotnet test ... | grep ...` variants. Writes full output
  to a file and prints a short summary (pass/fail counts + failing test names).
  Claude reads the output file with the Read tool — no grep/sleep/Monitor chains.

.PARAMETER Project
  Path to the test project or .sln, relative to repo root or absolute.

.PARAMETER Filter
  Optional --filter expression (e.g. "FullyQualifiedName~MyTests").

.PARAMETER NoBuild
  Pass --no-build to dotnet test.

.PARAMETER OutFile
  Where to write full test output. Defaults to a temp file whose path is printed.

.EXAMPLE
  pwsh .claude/scripts/Run-Tests.ps1 -Project Radoub.UI/Radoub.UI.Tests -Filter "FullyQualifiedName~FilenameRenameScopeTests"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [string]$Filter = "",

    [switch]$NoBuild,

    [string]$OutFile = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutFile)) {
    $OutFile = Join-Path ([System.IO.Path]::GetTempPath()) ("radoub-test-{0}.log" -f ([guid]::NewGuid().ToString("N")))
}

$dotnetArgs = @("test", $Project, "--nologo")
if ($NoBuild) { $dotnetArgs += "--no-build" }
if (-not [string]::IsNullOrWhiteSpace($Filter)) { $dotnetArgs += @("--filter", $Filter) }

Write-Host "Running: dotnet $($dotnetArgs -join ' ')"
Write-Host "Output : $OutFile"
Write-Host "----"

# Capture everything to the file; never pipe through head/tail (CLAUDE.md rule).
& dotnet @dotnetArgs *>&1 | Tee-Object -FilePath $OutFile | Out-Null
$exit = $LASTEXITCODE

# Print a compact summary from the log.
$summary = Select-String -Path $OutFile -Pattern '^(Passed!|Failed!|\s+(Passed|Failed)\s)' -AllMatches |
    ForEach-Object { $_.Line.Trim() }

Write-Host "===== SUMMARY ====="
if ($summary) { $summary | ForEach-Object { Write-Host $_ } }
else { Write-Host "(no summary lines found — read $OutFile for details)" }
Write-Host "===== EXIT $exit ====="
Write-Host "Full output: $OutFile"

exit $exit
