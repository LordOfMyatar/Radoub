# Copy DLG and UTC Files Script
# Copies .dlg and .utc files from Lord Myatar module to LNS_DLG module
# Preserves timestamps and overwrites existing files

$SourceBase = "~\Documents\Neverwinter Nights\modules\Lord Of Myatar\"
$DestinationPath = "~\Documents\Neverwinter Nights\modules\LNS_DLG\"

Write-Host "Copying .dlg and .utc files from Lord Myatar to LNS_DLG..." -ForegroundColor Green

try {
    $DlgFiles = Copy-Item -Path "$SourceBase*.dlg" -Destination $DestinationPath -Force -PassThru -ErrorAction Stop
    $UtcFiles = Copy-Item -Path "$SourceBase*.utc" -Destination $DestinationPath -Force -PassThru -ErrorAction Stop

    $AllFiles = @($DlgFiles) + @($UtcFiles)

    Write-Host "`nSuccessfully copied $($AllFiles.Count) files:" -ForegroundColor Green
    Write-Host "  DLG: $($DlgFiles.Count) files" -ForegroundColor Cyan
    Write-Host "  UTC: $($UtcFiles.Count) files" -ForegroundColor Cyan
    foreach ($File in $AllFiles) {
        Write-Host "  • $($File.Name) - Modified: $($File.LastWriteTime)" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "Error copying files: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nCopy operation completed." -ForegroundColor Green