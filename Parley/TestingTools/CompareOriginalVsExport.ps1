# Compare original files vs our exports to find differences

$basePath = "~\Documents\Neverwinter Nights\modules\LNS_DLG"

$comparisons = @(
    @{ Original = "chef.dlg"; Export = "chef01.dlg" },
    @{ Original = "lista.dlg"; Export = "lista01.dlg" },
    @{ Original = "myra_james.dlg"; Export = "myra01.dlg" },
    @{ Original = "generic_hench.dlg"; Export = "hench01.dlg" }
)

foreach ($comp in $comparisons) {
    $origPath = Join-Path $basePath $comp.Original
    $exportPath = Join-Path $basePath $comp.Export

    if (!(Test-Path $origPath)) {
        Write-Host "❌ Original not found: $($comp.Original)" -ForegroundColor Red
        continue
    }

    if (!(Test-Path $exportPath)) {
        Write-Host "❌ Export not found: $($comp.Export)" -ForegroundColor Red
        continue
    }

    $origSize = (Get-Item $origPath).Length
    $exportSize = (Get-Item $exportPath).Length
    $diff = $exportSize - $origSize
    $pct = [math]::Round(($diff / $origSize) * 100, 1)

    Write-Host "`n=== $($comp.Original) vs $($comp.Export) ===" -ForegroundColor Cyan
    Write-Host "Original: $origSize bytes"
    Write-Host "Export:   $exportSize bytes"

    if ($diff -gt 0) {
        Write-Host "BLOAT:    +$diff bytes (+$pct%)" -ForegroundColor Yellow
    } elseif ($diff -lt 0) {
        Write-Host "SMALLER:  $diff bytes ($pct%)" -ForegroundColor Green
    } else {
        Write-Host "SAME SIZE" -ForegroundColor Green
    }
}
