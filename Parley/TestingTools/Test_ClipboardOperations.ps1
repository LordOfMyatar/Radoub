# Test_ClipboardOperations.ps1
# Tests for DialogClipboardService copy/paste/cut operations

param(
    [string]$TestFile = "$env:USERPROFILE\Documents\Neverwinter Nights\modules\LNS_DLG\test_clipboard.dlg"
)

Write-Host "=== Clipboard Operations Test Suite ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Basic Copy Operation
Write-Host "[TEST 1] Basic Copy Operation" -ForegroundColor Yellow
Write-Host "Expected: Copy a simple node without crash" -ForegroundColor Gray
Write-Host "Action: Open dialog, select node, Ctrl+C" -ForegroundColor Gray
Write-Host "Verify: No crash, clipboard has content" -ForegroundColor Gray
Write-Host ""

# Test 2: Deep Tree Copy
Write-Host "[TEST 2] Deep Tree Copy (Depth 11+)" -ForegroundColor Yellow
Write-Host "Expected: Copy deeply nested node without crash" -ForegroundColor Gray
Write-Host "File: __hicks.dlg" -ForegroundColor Gray
Write-Host "Action: Navigate to 'upsy daisy' node (near bottom), Ctrl+C" -ForegroundColor Gray
Write-Host "Verify: No crash, all node properties preserved" -ForegroundColor Gray
Write-Host ""

# Test 3: Cut Operation with Focus
Write-Host "[TEST 3] Cut Operation Focus Preservation" -ForegroundColor Yellow
Write-Host "Expected: Focus moves to sibling after cut" -ForegroundColor Gray
Write-Host "Action: Select middle node with siblings, Ctrl+X" -ForegroundColor Gray
Write-Host "Verify: Focus on previous sibling (or next if first, or parent if only child)" -ForegroundColor Gray
Write-Host ""

# Test 4: Paste Type Validation
Write-Host "[TEST 4] Invalid Paste Type Blocking" -ForegroundColor Yellow
Write-Host "Expected: PC→PC paste blocked with error message" -ForegroundColor Gray
Write-Host "Action: Copy PC Reply node, select PC Reply parent, Ctrl+V" -ForegroundColor Gray
Write-Host "Verify: Error 'Cannot paste PC under PC - conversation must alternate NPC/PC'" -ForegroundColor Gray
Write-Host ""

# Test 5: Copy with Circular References
Write-Host "[TEST 5] Copy Node with Circular References" -ForegroundColor Yellow
Write-Host "Expected: Circular references handled without infinite loop" -ForegroundColor Gray
Write-Host "Action: Copy node that links back to ancestor" -ForegroundColor Gray
Write-Host "Verify: Copy completes, cloneMap prevents duplicate cloning" -ForegroundColor Gray
Write-Host ""

# Test 6: Copy Preserves All Properties
Write-Host "[TEST 6] Property Preservation in Copy" -ForegroundColor Yellow
Write-Host "Expected: All DialogNode properties cloned correctly" -ForegroundColor Gray
Write-Host "Properties to check:" -ForegroundColor Gray
Write-Host "  - Text (LocString dictionary)" -ForegroundColor DarkGray
Write-Host "  - Speaker, Comment, Sound, ScriptAction" -ForegroundColor DarkGray
Write-Host "  - Animation, AnimationLoop, Delay" -ForegroundColor DarkGray
Write-Host "  - Quest, QuestEntry" -ForegroundColor DarkGray
Write-Host "  - ActionParams (Dictionary)" -ForegroundColor DarkGray
Write-Host "  - Pointers (recursively)" -ForegroundColor DarkGray
Write-Host "Action: Copy complex node with all properties set, paste, compare" -ForegroundColor Gray
Write-Host ""

# Test 7: Cut vs Copy Behavior
Write-Host "[TEST 7] Cut vs Copy Storage Differences" -ForegroundColor Yellow
Write-Host "Expected: Cut stores reference, Copy deep clones" -ForegroundColor Gray
Write-Host "Action: Cut node, modify original → paste should reflect changes" -ForegroundColor Gray
Write-Host "        Copy node, modify original → paste should NOT reflect changes" -ForegroundColor Gray
Write-Host ""

# Test 8: Paste After Cut (Same Dialog)
Write-Host "[TEST 8] Paste After Cut (Move Operation)" -ForegroundColor Yellow
Write-Host "Expected: Original node moved to new location" -ForegroundColor Gray
Write-Host "Action: Cut node, paste under different parent" -ForegroundColor Gray
Write-Host "Verify: Node removed from old location, appears in new location" -ForegroundColor Gray
Write-Host ""

# Test 9: Paste After Copy (Duplicate Operation)
Write-Host "[TEST 9] Paste After Copy (Duplicate Operation)" -ForegroundColor Yellow
Write-Host "Expected: New clone created, original unchanged" -ForegroundColor Gray
Write-Host "Action: Copy node, paste under different parent" -ForegroundColor Gray
Write-Host "Verify: Original node unchanged, new node created" -ForegroundColor Gray
Write-Host ""

# Test 10: Clipboard Clear After Cut+Paste
Write-Host "[TEST 10] Clipboard Clear After Cut+Paste" -ForegroundColor Yellow
Write-Host "Expected: Clipboard cleared after successful cut+paste" -ForegroundColor Gray
Write-Host "Action: Cut node, paste, try to paste again" -ForegroundColor Gray
Write-Host "Verify: Second paste does nothing (clipboard empty)" -ForegroundColor Gray
Write-Host ""

# Test 11: MAX_DEPTH Protection
Write-Host "[TEST 11] Maximum Depth Protection" -ForegroundColor Yellow
Write-Host "Expected: Clone stops at depth 100 with warning" -ForegroundColor Gray
Write-Host "Action: Create artificially deep tree (100+ levels), copy root" -ForegroundColor Gray
Write-Host "Verify: Warning logged, no crash, partial clone created" -ForegroundColor Gray
Write-Host ""

# Test 12: Cross-Dialog Paste
Write-Host "[TEST 12] Cross-Dialog Paste Behavior" -ForegroundColor Yellow
Write-Host "Expected: Copy works, Cut creates copy (not move)" -ForegroundColor Gray
Write-Host "Action: Copy node from Dialog A, switch to Dialog B, paste" -ForegroundColor Gray
Write-Host "Verify: New node created in Dialog B, Dialog A unchanged" -ForegroundColor Gray
Write-Host ""

Write-Host ""
Write-Host "=== Manual Test Instructions ===" -ForegroundColor Cyan
Write-Host "1. Run Parley" -ForegroundColor White
Write-Host "2. Open test dialog files:" -ForegroundColor White
Write-Host "   - __hicks.dlg (deep tree testing)" -ForegroundColor DarkGray
Write-Host "   - billy_wanderers.dlg (complex properties)" -ForegroundColor DarkGray
Write-Host "3. Perform each test above" -ForegroundColor White
Write-Host "4. Check logs in ~/Parley/Logs/ for errors/warnings" -ForegroundColor White
Write-Host "5. Verify scrap.json doesn't capture copy operations (only deletes)" -ForegroundColor White
Write-Host ""
Write-Host "=== Expected Log Patterns ===" -ForegroundColor Cyan
Write-Host "SUCCESS:" -ForegroundColor Green
Write-Host "  - 'Copied node to clipboard: Type=Entry'" -ForegroundColor DarkGray
Write-Host "  - 'Cut node to clipboard: Type=Reply'" -ForegroundColor DarkGray
Write-Host "  - 'Pasted node as duplicate: Type=Entry, WasCut=false'" -ForegroundColor DarkGray
Write-Host "  - 'Clipboard cleared'" -ForegroundColor DarkGray
Write-Host ""
Write-Host "WARNINGS (Expected):" -ForegroundColor Yellow
Write-Host "  - 'Maximum clone depth (100) reached' (if testing depth limit)" -ForegroundColor DarkGray
Write-Host "  - 'Cannot paste PC under PC' (if testing type validation)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "ERRORS (Unexpected):" -ForegroundColor Red
Write-Host "  - Any stack overflow errors" -ForegroundColor DarkGray
Write-Host "  - Any JSON serialization errors" -ForegroundColor DarkGray
Write-Host "  - Any null reference errors during clone" -ForegroundColor DarkGray
Write-Host ""
