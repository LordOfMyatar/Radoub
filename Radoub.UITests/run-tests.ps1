$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputFile = "Radoub.UITests\TestOutput\test_$timestamp.output"

Write-Host "Testing began: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"

dotnet test Radoub.UITests --logger "console;verbosity=detailed" 2>&1 | Out-File -FilePath $outputFile

Write-Host "Testing completed: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host "Test output saved to: $outputFile"
Write-Host "`nFailed tests:"
Get-Content $outputFile | Select-String -Pattern "\[FAIL\]" | ForEach-Object { $_.Line }
Write-Host "`nLast 7 lines of output:"
Get-Content $outputFile -Tail 7
