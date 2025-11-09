# Copy DLG Files Script
# Copies .dlg files from Lord Myatar module to LNS_DLG module
# Preserves timestamps and overwrites existing files

$SourcePath = "~\Documents\Neverwinter Nights\modules\Lord Of Myatar\*.dlg"
$DestinationPath = "~\Documents\Neverwinter Nights\modules\LNS_DLG\"

Write-Host "Copying .dlg files from Lord Myatar to LNS_DLG..." -ForegroundColor Green

try {
    $CopiedFiles = Copy-Item -Path $SourcePath -Destination $DestinationPath -Force -PassThru -ErrorAction Stop

    Write-Host "`nSuccessfully copied $($CopiedFiles.Count) files:" -ForegroundColor Green
    foreach ($File in $CopiedFiles) {
        Write-Host "  • $($File.Name) - Modified: $($File.LastWriteTime)" -ForegroundColor Cyan
    }
}
catch {
    Write-Host "Error copying files: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nCopy operation completed." -ForegroundColor Green