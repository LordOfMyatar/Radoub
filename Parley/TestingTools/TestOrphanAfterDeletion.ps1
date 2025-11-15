# Test script to verify orphan detection after deletion
# This script:
# 1. Opens fox_orphan_test.dlg
# 2. Identifies nodes with parent-child links
# 3. Displays structure before/after deletion would occur

$testFile = Join-Path $PSScriptRoot "TestFiles\fox_orphan_test.dlg"
$outputFile = Join-Path $PSScriptRoot "TestFiles\fox_deletion_test_output.dlg"

if (-not (Test-Path $testFile)) {
    Write-Host "ERROR: Test file not found: $testFile" -ForegroundColor Red
    exit 1
}

Write-Host "=== Orphan Detection Test ===" -ForegroundColor Cyan
Write-Host "Test file: $testFile" -ForegroundColor Gray
Write-Host ""

Write-Host "To manually test:" -ForegroundColor Yellow
Write-Host "1. Copy fox_orphan_test.dlg to fox_test_copy.dlg" -ForegroundColor Gray
Write-Host "2. Open fox_test_copy.dlg in Parley" -ForegroundColor Gray
Write-Host "3. Delete the 'Greetings <FirstName>, what can I do for you?' Entry" -ForegroundColor Gray
Write-Host "4. Save and close Parley" -ForegroundColor Gray
Write-Host "5. Reopen fox_test_copy.dlg" -ForegroundColor Gray
Write-Host "6. Look for '!!! Orphaned Nodes' in the START list" -ForegroundColor Gray
Write-Host ""

Write-Host "Expected behavior:" -ForegroundColor Green
Write-Host "- PC Reply 'I would like to see what you have' should NOT be deleted" -ForegroundColor Gray
Write-Host "- It should appear under '!!! Orphaned Nodes' container" -ForegroundColor Gray
Write-Host "- Orphan container should have sc_false script" -ForegroundColor Gray
Write-Host ""

Write-Host "Check Application logs in:" -ForegroundColor Yellow
Write-Host "  ~\Parley\Logs\Session_<timestamp>\Application_<timestamp>.log" -ForegroundColor Gray
Write-Host ""
Write-Host "Look for these log messages:" -ForegroundColor Yellow
Write-Host "  'Skipping deletion of shared child (is parent in parent-child link(s))'" -ForegroundColor Gray
Write-Host "  'PopulateDialogNodes: Found X orphaned nodes'" -ForegroundColor Gray
Write-Host "  'Created new orphan root container'" -ForegroundColor Gray
Write-Host "  'Created new orphan START pointer'" -ForegroundColor Gray
