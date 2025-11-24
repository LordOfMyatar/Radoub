# Delete specific duplicate methods by line range
$filePath = "Parley\Parley\Views\MainWindow.axaml.cs"
$lines = Get-Content $filePath

# Convert to ArrayList for easy removal
$linesList = [System.Collections.ArrayList]::new($lines)

# Delete in reverse order so line numbers don't shift
# FindLastAddedNode + FindLastAddedNodeRecursive: lines 2770-2811 (indices 2769-2810, 42 lines)
for ($i = 2810; $i -ge 2769; $i--) {
    $linesList.RemoveAt($i)
}

# UpdateActionParamsFromUI: lines 2076-2159 (indices 2075-2158, 84 lines)
for ($i = 2158; $i -ge 2075; $i--) {
    $linesList.RemoveAt($i)
}

# UpdateConditionParamsFromUI: lines 1980-2074 (indices 1979-2073, 95 lines)
for ($i = 2073; $i -ge 1979; $i--) {
    $linesList.RemoveAt($i)
}

# Write back
$linesList | Set-Content $filePath

Write-Host "Deleted 221 lines of duplicate methods"
Write-Host "- UpdateConditionParamsFromUI (95 lines)"
Write-Host "- UpdateActionParamsFromUI (84 lines)"
Write-Host "- FindLastAddedNode + FindLastAddedNodeRecursive (42 lines)"
