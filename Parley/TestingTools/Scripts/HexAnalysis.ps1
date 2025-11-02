# HexAnalysis.ps1 - Comprehensive binary analysis of DLG files
param(
    [string]$OriginalFile,
    [string]$ExportedFile
)

function Read-GffHeader {
    param([byte[]]$bytes)

    $header = @{}
    $header.FileType = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 4)
    $header.FileVersion = [System.Text.Encoding]::ASCII.GetString($bytes, 4, 4)
    $header.StructOffset = [BitConverter]::ToUInt32($bytes, 8)
    $header.StructCount = [BitConverter]::ToUInt32($bytes, 12)
    $header.FieldOffset = [BitConverter]::ToUInt32($bytes, 16)
    $header.FieldCount = [BitConverter]::ToUInt32($bytes, 20)
    $header.LabelOffset = [BitConverter]::ToUInt32($bytes, 24)
    $header.LabelCount = [BitConverter]::ToUInt32($bytes, 28)
    $header.FieldDataOffset = [BitConverter]::ToUInt32($bytes, 32)
    $header.FieldDataCount = [BitConverter]::ToUInt32($bytes, 36)
    $header.FieldIndicesOffset = [BitConverter]::ToUInt32($bytes, 40)
    $header.FieldIndicesCount = [BitConverter]::ToUInt32($bytes, 44)
    $header.ListIndicesOffset = [BitConverter]::ToUInt32($bytes, 48)
    $header.ListIndicesCount = [BitConverter]::ToUInt32($bytes, 52)

    return $header
}

function Read-Struct {
    param([byte[]]$bytes, [int]$offset)

    $struct = @{}
    $struct.Type = [BitConverter]::ToUInt32($bytes, $offset)
    $struct.DataOrDataOffset = [BitConverter]::ToUInt32($bytes, $offset + 4)
    $struct.FieldCount = [BitConverter]::ToUInt32($bytes, $offset + 8)

    return $struct
}

function Read-Field {
    param([byte[]]$bytes, [int]$offset)

    $field = @{}
    $field.Type = [BitConverter]::ToUInt32($bytes, $offset)
    $field.LabelIndex = [BitConverter]::ToUInt32($bytes, $offset + 4)
    $field.DataOrDataOffset = [BitConverter]::ToUInt32($bytes, $offset + 8)

    return $field
}

function Analyze-File {
    param([string]$FilePath, [string]$Label)

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " $Label" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host ""

    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    $header = Read-GffHeader $bytes

    Write-Host "FILE HEADER:" -ForegroundColor Yellow
    Write-Host "  FileType: $($header.FileType)"
    Write-Host "  FileVersion: $($header.FileVersion)"
    Write-Host "  StructOffset: $($header.StructOffset), Count: $($header.StructCount)"
    Write-Host "  FieldOffset: $($header.FieldOffset), Count: $($header.FieldCount)"
    Write-Host "  LabelOffset: $($header.LabelOffset), Count: $($header.LabelCount)"
    Write-Host "  FieldDataOffset: $($header.FieldDataOffset), Count: $($header.FieldDataCount)"
    Write-Host "  FieldIndicesOffset: $($header.FieldIndicesOffset), Count: $($header.FieldIndicesCount)"
    Write-Host "  ListIndicesOffset: $($header.ListIndicesOffset), Count: $($header.ListIndicesCount)"
    Write-Host ""

    # Calculate ratios
    if ($header.FieldCount -gt 0) {
        $fieldIndicesRatio = $header.FieldIndicesCount / $header.FieldCount
        Write-Host "  Field Indices Ratio: $($fieldIndicesRatio.ToString('F2')):1" -ForegroundColor Green
    }
    Write-Host ""

    # Read root struct
    Write-Host "ROOT STRUCT:" -ForegroundColor Yellow
    $rootStruct = Read-Struct $bytes $header.StructOffset
    Write-Host "  Type: $($rootStruct.Type)"
    Write-Host "  DataOrDataOffset: $($rootStruct.DataOrDataOffset)"
    Write-Host "  FieldCount: $($rootStruct.FieldCount)"
    Write-Host ""

    # Read all structs and categorize by type
    Write-Host "STRUCT ANALYSIS:" -ForegroundColor Yellow
    $structsByType = @{}
    for ($i = 0; $i -lt $header.StructCount; $i++) {
        $structPos = $header.StructOffset + ($i * 12)
        $struct = Read-Struct $bytes $structPos

        if (-not $structsByType.ContainsKey($struct.Type)) {
            $structsByType[$struct.Type] = @()
        }
        $structsByType[$struct.Type] += $i
    }

    foreach ($type in ($structsByType.Keys | Sort-Object)) {
        $count = $structsByType[$type].Count
        Write-Host "  Type $type : $count structs (indices: $($structsByType[$type] -join ', '))"
    }
    Write-Host ""

    # Read StartingList from root
    Write-Host "STARTINGLIST ANALYSIS:" -ForegroundColor Yellow

    # Root struct's field 8 should be StartingList
    $rootFieldIndicesStart = $header.FieldIndicesOffset + $rootStruct.DataOrDataOffset

    # Read all 9 root field indices
    Write-Host "  Root struct field indices:"
    for ($i = 0; $i -lt 9; $i++) {
        $fieldIdx = [BitConverter]::ToUInt32($bytes, $rootFieldIndicesStart + ($i * 4))
        Write-Host "    [$i] -> Field $fieldIdx"
    }

    # Field 8 is StartingList
    $startingListFieldIdx = [BitConverter]::ToUInt32($bytes, $rootFieldIndicesStart + (8 * 4))
    $startingListFieldPos = $header.FieldOffset + ($startingListFieldIdx * 12)
    $startingListField = Read-Field $bytes $startingListFieldPos

    Write-Host ""
    Write-Host "  StartingList Field (index $startingListFieldIdx):" -ForegroundColor Green
    Write-Host "    Type: $($startingListField.Type) (15=List)"
    Write-Host "    LabelIndex: $($startingListField.LabelIndex)"
    Write-Host "    DataOrDataOffset: $($startingListField.DataOrDataOffset)"

    # Read StartingList data
    $startingListDataPos = $header.ListIndicesOffset + $startingListField.DataOrDataOffset
    $startingListCount = [BitConverter]::ToUInt32($bytes, $startingListDataPos)

    Write-Host ""
    Write-Host "  StartingList Data (at offset $startingListDataPos):" -ForegroundColor Green
    Write-Host "    Count: $startingListCount" -ForegroundColor Red
    Write-Host ""

    for ($i = 0; $i -lt $startingListCount; $i++) {
        $structIdx = [BitConverter]::ToUInt32($bytes, $startingListDataPos + 4 + ($i * 4))
        $structPos = $header.StructOffset + ($structIdx * 12)
        $struct = Read-Struct $bytes $structPos

        Write-Host "    [$i] -> Struct $structIdx (Type=$($struct.Type), DataOffset=$($struct.DataOrDataOffset), FieldCount=$($struct.FieldCount))"

        # Try to read fields of this struct
        $fieldIndicesPos = $header.FieldIndicesOffset + $struct.DataOrDataOffset
        Write-Host "         Fields:"
        for ($f = 0; $f -lt $struct.FieldCount; $f++) {
            $fieldIdx = [BitConverter]::ToUInt32($bytes, $fieldIndicesPos + ($f * 4))
            $fieldPos = $header.FieldOffset + ($fieldIdx * 12)
            $field = Read-Field $bytes $fieldPos

            # Try to get field label
            $labelPos = $header.LabelOffset + ($field.LabelIndex * 16)
            $labelBytes = $bytes[$labelPos..($labelPos + 15)]
            $labelStr = [System.Text.Encoding]::ASCII.GetString($labelBytes).TrimEnd([char]0)

            Write-Host "           [$f] Field $fieldIdx : '$labelStr' (Type=$($field.Type), Data=$($field.DataOrDataOffset))"

            # If it's a DWORD/UINT type, show the value
            if ($field.Type -eq 4 -or $field.Type -eq 5) {
                Write-Host "               Value: $($field.DataOrDataOffset)" -ForegroundColor Cyan
            }
        }
    }

    return @{
        Header = $header
        StructsByType = $structsByType
        StartingListCount = $startingListCount
    }
}

# Main execution
Write-Host ""
Write-Host "============================================================" -ForegroundColor Magenta
Write-Host "          AURORA DLG HEX ANALYSIS COMPARISON               " -ForegroundColor Magenta
Write-Host "============================================================" -ForegroundColor Magenta

$original = Analyze-File $OriginalFile "ORIGINAL FILE: $(Split-Path $OriginalFile -Leaf)"
$exported = Analyze-File $ExportedFile "EXPORTED FILE: $(Split-Path $ExportedFile -Leaf)"

# Comparison
Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " COMPARISON" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Header Differences:" -ForegroundColor Yellow
Write-Host "  StructCount: $($original.Header.StructCount) -> $($exported.Header.StructCount)"
Write-Host "  FieldCount: $($original.Header.FieldCount) -> $($exported.Header.FieldCount)"
Write-Host "  LabelCount: $($original.Header.LabelCount) -> $($exported.Header.LabelCount)"
Write-Host "  FieldIndicesCount: $($original.Header.FieldIndicesCount) -> $($exported.Header.FieldIndicesCount)"
Write-Host "  ListIndicesCount: $($original.Header.ListIndicesCount) -> $($exported.Header.ListIndicesCount)"
Write-Host ""

Write-Host "Struct Type Distribution:" -ForegroundColor Yellow
$allTypes = ($original.StructsByType.Keys + $exported.StructsByType.Keys) | Sort-Object -Unique
foreach ($type in $allTypes) {
    $origCount = if ($original.StructsByType.ContainsKey($type)) { $original.StructsByType[$type].Count } else { 0 }
    $expCount = if ($exported.StructsByType.ContainsKey($type)) { $exported.StructsByType[$type].Count } else { 0 }

    $diff = ""
    if ($origCount -ne $expCount) {
        $diff = " [MISMATCH]"
    }
    Write-Host "  Type $type : $origCount -> $expCount$diff"
}
Write-Host ""

Write-Host "StartingList Count: $($original.StartingListCount) -> $($exported.StartingListCount)" -ForegroundColor Yellow
if ($original.StartingListCount -ne $exported.StartingListCount) {
    Write-Host "  WARNING: MISMATCH!" -ForegroundColor Red
}
