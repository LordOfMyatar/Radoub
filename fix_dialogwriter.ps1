$file = "D:\LOM\workspace\Radoub\Parley\Parley\Parsers\DialogWriter.cs"
$content = Get-Content $file -Raw -Encoding UTF8

$before = ([regex]::Matches($content, 'LogParser\(LogLevel\.INFO')).Count
Write-Host "DialogWriter.cs before: $before INFO logs"

# DialogWriter has NO user-facing messages - all are field/binary operations → TRACE
$content = $content -replace 'LogParser\(LogLevel\.INFO', 'LogParser(LogLevel.TRACE'

$after = ([regex]::Matches($content, 'LogParser\(LogLevel\.INFO')).Count
Write-Host "DialogWriter.cs after: $after INFO logs"
Write-Host "Replaced: $($before - $after) logs"

Set-Content $file -Value $content -Encoding UTF8 -NoNewline
Write-Host "DialogWriter.cs complete!"
