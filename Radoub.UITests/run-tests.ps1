$timestamp = Get-Date -Format "yyyyMMddHHmmss"
$outputFile = "Radoub.UITests\TestOutput\test_$timestamp.output"

dotnet test Radoub.UITests --logger "console;verbosity=detailed" 2>&1 | Out-File -FilePath $outputFile

Write-Host "Test output saved to: $outputFile"
