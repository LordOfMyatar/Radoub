# StructTypeAnalysis.ps1 - Analyze struct Type vs FieldCount correlation
param([string]$FilePath)

$bytes = [System.IO.File]::ReadAllBytes($FilePath)
$structOffset = [BitConverter]::ToUInt32($bytes, 8)
$structCount = [BitConverter]::ToUInt32($bytes, 12)

Write-Host "Analyzing $structCount structs from $FilePath"
Write-Host ""
Write-Host "Struct#  Type  FieldCnt  DataOffset"
Write-Host "======================================="

for ($i = 0; $i -lt [Math]::Min($structCount, 82); $i++) {
    $pos = $structOffset + ($i * 12)
    $type = [BitConverter]::ToUInt32($bytes, $pos)
    $dataOffset = [BitConverter]::ToUInt32($bytes, $pos + 4)
    $fieldCnt = [BitConverter]::ToUInt32($bytes, $pos + 8)

    if ($type -eq 4294967295) {
        $typeStr = "ROOT"
    } else {
        $typeStr = $type.ToString()
    }

    Write-Host ("{0,6}  {1,6}  {2,8}  {3,10}" -f $i, $typeStr, $fieldCnt, $dataOffset)
}

# Group by Type
Write-Host ""
Write-Host "Grouping by Type:"
Write-Host "================="
$typeGroups = @{}
for ($i = 0; $i -lt $structCount; $i++) {
    $pos = $structOffset + ($i * 12)
    $type = [BitConverter]::ToUInt32($bytes, $pos)
    $fieldCnt = [BitConverter]::ToUInt32($bytes, $pos + 8)

    if (-not $typeGroups.ContainsKey($type)) {
        $typeGroups[$type] = @()
    }
    $typeGroups[$type] += @{ Index = $i; FieldCount = $fieldCnt }
}

foreach ($type in ($typeGroups.Keys | Sort-Object)) {
    $structs = $typeGroups[$type]
    $fieldCounts = $structs | ForEach-Object { $_.FieldCount } | Sort-Object -Unique

    if ($type -eq 4294967295) {
        $typeStr = "ROOT"
    } else {
        $typeStr = "Type $type"
    }

    Write-Host "$typeStr : $($structs.Count) structs, FieldCounts: $($fieldCounts -join ', ')"
}
