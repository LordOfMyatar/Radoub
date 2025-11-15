# TestScrapOperations.ps1
# Comprehensive test for cut/copy/delete/paste operations and scrap tab functionality
# Tests Epic #112 implementation

param(
    [string]$ParleyPath = "D:\LOM\workspace\Radoub\Parley\Parley\bin\Debug\net9.0\Parley.exe",
    [string]$TestFilePath = "D:\LOM\workspace\Radoub\Parley\TestingTools\TestFiles\test1_link.dlg"
)

Write-Host "=== Scrap Operations Test Suite ===" -ForegroundColor Cyan
Write-Host "Testing cut/copy/delete/paste operations and scrap tab" -ForegroundColor Cyan
Write-Host ""

# Verify files exist
if (-not (Test-Path $ParleyPath)) {
    Write-Host "ERROR: Parley.exe not found at: $ParleyPath" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $TestFilePath)) {
    Write-Host "ERROR: Test file not found at: $TestFilePath" -ForegroundColor Red
    exit 1
}

# Create backup
$backupPath = "$TestFilePath.backup"
Copy-Item $TestFilePath $backupPath -Force
Write-Host "Created backup: $backupPath" -ForegroundColor Green

# Clean scrap file before testing
$scrapPath = Join-Path $env:USERPROFILE "Parley\scrap.json"
if (Test-Path $scrapPath) {
    Write-Host "Clearing existing scrap file..." -ForegroundColor Yellow
    Remove-Item $scrapPath -Force
}

Write-Host ""
Write-Host "=== Manual Test Instructions ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "This test requires manual interaction with Parley UI." -ForegroundColor White
Write-Host "Follow the steps below and verify each operation:" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 1: Delete Operations ---" -ForegroundColor Cyan
Write-Host "1. Open test1_link.dlg in Parley" -ForegroundColor White
Write-Host "2. Select a leaf node (node with no children)" -ForegroundColor White
Write-Host "3. Press Delete or use Edit > Delete Node" -ForegroundColor White
Write-Host "4. Check Scrap tab - should show 'Scrap (1)'" -ForegroundColor White
Write-Host "5. Verify node appears in scrap with correct type and text" -ForegroundColor White
Write-Host "6. Delete another node (with children this time)" -ForegroundColor White
Write-Host "7. Check Scrap tab - count should increase" -ForegroundColor White
Write-Host "8. Verify hierarchy info shows parent relationships" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 2: Cut Operations (Move) ---" -ForegroundColor Cyan
Write-Host "1. Select a node with children" -ForegroundColor White
Write-Host "2. Use Edit > Cut Node (Ctrl+X)" -ForegroundColor White
Write-Host "3. IMPORTANT: Scrap tab should NOT increase (cut is a move, not delete)" -ForegroundColor White
Write-Host "4. Select a different parent node" -ForegroundColor White
Write-Host "5. Use Edit > Paste as Duplicate (Ctrl+V)" -ForegroundColor White
Write-Host "6. Verify node moved with all children intact" -ForegroundColor White
Write-Host "7. Save file and verify in Aurora Toolset - no orphans should exist" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 3: Copy Operations (Duplicate) ---" -ForegroundColor Cyan
Write-Host "1. Select a node" -ForegroundColor White
Write-Host "2. Use Edit > Copy Node (Ctrl+C)" -ForegroundColor White
Write-Host "3. Scrap tab should NOT change (copy doesn't delete)" -ForegroundColor White
Write-Host "4. Select a parent node" -ForegroundColor White
Write-Host "5. Use Edit > Paste as Duplicate" -ForegroundColor White
Write-Host "6. Verify original node still exists AND duplicate was created" -ForegroundColor White
Write-Host "7. Delete the original node" -ForegroundColor White
Write-Host "8. Scrap tab should now increase by 1 (only the deleted original)" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 4: Complex Sequence 1 (Cut > Delete > Copy > Paste) ---" -ForegroundColor Cyan
Write-Host "1. Cut node A (scrap should NOT increase)" -ForegroundColor White
Write-Host "2. Delete node B (scrap should increase by 1)" -ForegroundColor White
Write-Host "3. Copy node C (scrap should NOT change)" -ForegroundColor White
Write-Host "4. Paste node A somewhere (cut node moved)" -ForegroundColor White
Write-Host "5. Paste node C somewhere (creates duplicate)" -ForegroundColor White
Write-Host "6. Verify scrap only contains deleted node B" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 5: Complex Sequence 2 (Delete > Cut > Paste > Delete) ---" -ForegroundColor Cyan
Write-Host "1. Delete node X (scrap increases)" -ForegroundColor White
Write-Host "2. Cut node Y (scrap should NOT increase)" -ForegroundColor White
Write-Host "3. Paste node Y elsewhere" -ForegroundColor White
Write-Host "4. Delete node Z (scrap increases)" -ForegroundColor White
Write-Host "5. Verify scrap contains X and Z but NOT Y" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 6: Restore from Scrap ---" -ForegroundColor Cyan
Write-Host "1. Select a node from the scrap list" -ForegroundColor White
Write-Host "2. Select a target location in the tree (or ROOT)" -ForegroundColor White
Write-Host "3. Click 'Restore' button" -ForegroundColor White
Write-Host "4. Verify node appears in tree at selected location" -ForegroundColor White
Write-Host "5. Verify node disappears from scrap list" -ForegroundColor White
Write-Host "6. Verify scrap count decreases" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 7: Clear Scrap ---" -ForegroundColor Cyan
Write-Host "1. Click 'Clear All' button in Scrap tab" -ForegroundColor White
Write-Host "2. Verify all entries disappear from scrap" -ForegroundColor White
Write-Host "3. Verify tab shows 'Scrap' (no count)" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 8: Scrap Persistence ---" -ForegroundColor Cyan
Write-Host "1. Delete a few nodes (scrap should increase)" -ForegroundColor White
Write-Host "2. Close Parley (File > Exit)" -ForegroundColor White
Write-Host "3. Reopen Parley" -ForegroundColor White
Write-Host "4. Open the same file" -ForegroundColor White
Write-Host "5. Verify scrap tab shows correct count" -ForegroundColor White
Write-Host "6. Verify scrap entries are still present" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 9: Multi-File Scrap Isolation ---" -ForegroundColor Cyan
Write-Host "1. Open test1_link.dlg" -ForegroundColor White
Write-Host "2. Delete a node (note scrap count)" -ForegroundColor White
Write-Host "3. Close file (scrap should show 'Scrap' with no count)" -ForegroundColor White
Write-Host "4. Open a different .dlg file" -ForegroundColor White
Write-Host "5. Verify scrap shows 'Scrap' (no entries from previous file)" -ForegroundColor White
Write-Host "6. Delete a node in this file" -ForegroundColor White
Write-Host "7. Close file and reopen test1_link.dlg" -ForegroundColor White
Write-Host "8. Verify scrap shows entries from test1_link.dlg only" -ForegroundColor White
Write-Host ""

Write-Host "--- Test 10: Aurora Toolset Round-Trip ---" -ForegroundColor Cyan
Write-Host "1. Perform several cut/paste operations" -ForegroundColor White
Write-Host "2. Save file in Parley" -ForegroundColor White
Write-Host "3. Open file in Aurora Toolset" -ForegroundColor White
Write-Host "4. Verify NO orphaned nodes exist" -ForegroundColor White
Write-Host "5. Verify all nodes are reachable from START nodes" -ForegroundColor White
Write-Host "6. Make a change in Aurora and save" -ForegroundColor White
Write-Host "7. Reopen in Parley" -ForegroundColor White
Write-Host "8. Verify file loads correctly" -ForegroundColor White
Write-Host ""

Write-Host "=== Expected Results ===" -ForegroundColor Yellow
Write-Host "✓ Delete operations add nodes to scrap" -ForegroundColor Green
Write-Host "✓ Cut operations do NOT add to scrap (move operation)" -ForegroundColor Green
Write-Host "✓ Copy operations do NOT affect scrap" -ForegroundColor Green
Write-Host "✓ Paste after Cut reuses original node (move)" -ForegroundColor Green
Write-Host "✓ Paste after Copy creates duplicate" -ForegroundColor Green
Write-Host "✓ Scrap tab shows 'Scrap' when empty, 'Scrap (N)' when has items" -ForegroundColor Green
Write-Host "✓ Scrap tab header scales with font size" -ForegroundColor Green
Write-Host "✓ Scrap entries persist across sessions" -ForegroundColor Green
Write-Host "✓ Scrap entries are file-specific" -ForegroundColor Green
Write-Host "✓ Restore functionality works correctly" -ForegroundColor Green
Write-Host "✓ Files saved by Parley have NO orphan nodes" -ForegroundColor Green
Write-Host "✓ Aurora Toolset can open Parley-saved files without issues" -ForegroundColor Green
Write-Host ""

Write-Host "=== Starting Parley ===" -ForegroundColor Cyan
Write-Host "Opening Parley with test file..." -ForegroundColor White
Write-Host ""
Write-Host "Command: $ParleyPath `"$TestFilePath`"" -ForegroundColor Gray
Write-Host ""

# Launch Parley
Start-Process -FilePath $ParleyPath -ArgumentList "`"$TestFilePath`""

Write-Host ""
Write-Host "=== After Testing ===" -ForegroundColor Yellow
Write-Host "1. Check scrap.json location: $scrapPath" -ForegroundColor White
Write-Host "2. Review logs in: $env:USERPROFILE\Parley\Logs" -ForegroundColor White
Write-Host "3. To restore original file: Copy-Item `"$backupPath`" `"$TestFilePath`" -Force" -ForegroundColor White
Write-Host ""
Write-Host "Test script ready. Follow the manual test steps above." -ForegroundColor Cyan
