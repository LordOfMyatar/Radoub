$files = @(
    "D:\LOM\workspace\Radoub\Parley\Parley\Parsers\DialogBuilder.cs",
    "D:\LOM\workspace\Radoub\Parley\Parley\Parsers\GffBinaryReader.cs",
    "D:\LOM\workspace\Radoub\Parley\Parley\Parsers\GffIndexFixer.cs"
)

$totalBefore = 0
$totalAfter = 0

foreach ($file in $files) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw -Encoding UTF8
        $before = ([regex]::Matches($content, 'LogParser\(LogLevel\.INFO')).Count

        # All parser files contain only internal operations → TRACE
        $content = $content -replace 'LogParser\(LogLevel\.INFO', 'LogParser(LogLevel.TRACE'

        $after = ([regex]::Matches($content, 'LogParser\(LogLevel\.INFO')).Count

        $fileName = Split-Path $file -Leaf
        Write-Host "$fileName : $before → $after (replaced $($before - $after))"

        $totalBefore += $before
        $totalAfter += $after

        Set-Content $file -Value $content -Encoding UTF8 -NoNewline
    }
}

Write-Host "`nTotal: $totalBefore → $totalAfter (replaced $($totalBefore - $totalAfter) logs)"
