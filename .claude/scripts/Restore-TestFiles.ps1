# Restore Test Files Script
# Copies game resource files from Lord Myatar module to LNS_DLG test module
# Supports: .dlg, .utc, .utm, .uti
#
# Usage: .\.claude\scripts\Restore-TestFiles.ps1 [-Type <extension>]
#
# Examples:
#   .\.claude\scripts\Restore-TestFiles.ps1              # All supported types
#   .\.claude\scripts\Restore-TestFiles.ps1 -Type dlg    # DLG files only
#   .\.claude\scripts\Restore-TestFiles.ps1 -Type uti    # UTI files only

param(
    [ValidateSet("dlg", "utc", "utm", "uti", "all")]
    [string]$Type = "all"
)

$SourceBase = "$env:USERPROFILE\Documents\Neverwinter Nights\modules\Lord Of Myatar\"
$DestinationPath = "$env:USERPROFILE\Documents\Neverwinter Nights\modules\LNS_DLG\"

$extensions = if ($Type -eq "all") { @("dlg", "utc", "utm", "uti") } else { @($Type) }

Write-Host "Copying files from Lord Myatar to LNS_DLG..." -ForegroundColor Green
Write-Host "  Source: $SourceBase" -ForegroundColor Gray
Write-Host "  Destination: $DestinationPath" -ForegroundColor Gray
Write-Host ""

$totalCopied = 0

foreach ($ext in $extensions) {
    try {
        $files = Copy-Item -Path "$SourceBase*.$ext" -Destination $DestinationPath -Force -PassThru -ErrorAction Stop
        $count = @($files).Count
        $totalCopied += $count
        Write-Host "  .$ext`: $count files" -ForegroundColor Cyan
    }
    catch {
        if ($_.Exception.Message -match "Cannot find path") {
            Write-Host "  .$ext`: 0 files (none found)" -ForegroundColor Gray
        } else {
            Write-Host "  .$ext`: ERROR - $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}

Write-Host ""
Write-Host "Copied $totalCopied files total." -ForegroundColor Green
