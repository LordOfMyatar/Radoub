# Quick Regression Testing Example

## Before Making Parser Changes

### 1. Create Test Baseline

Open Parley and save reference files:

```
2entry2reply.dlg → save as → 2entry2rep_baseline.dlg
lista.dlg → save as → lista_baseline.dlg
chef.dlg → save as → chef_baseline.dlg
```

All tests should PASS (tree expands, no errors in Aurora).

### 2. Make Your Parser Changes

Edit `Parley/Parsers/DialogParser.cs`

### 3. Build

```bash
dotnet build Parley/Parley.Avalonia.csproj
```

### 4. Test Each File

Open Parley and save each reference file again:

```
2entry2reply.dlg → save as → 2entry2rep_test.dlg
lista.dlg → save as → lista_test.dlg
chef.dlg → save as → chef_test.dlg
```

### 5. Run Regression Checks

```powershell
# Simple files
cd TestingTools/TestFiles
powershell ..\..\TestingTools\Scripts\HexAnalysis.ps1 -OriginalFile "2entry2reply.dlg" -ExportedFile "2entry2rep_test.dlg"

# Check output for:
# StructCount: X -> X (same)
# FieldCount: Y -> Y (same)
# StartingList Count: Z -> Z (same)
```

### 6. Verify in Aurora

Open each test file in Aurora Editor:
- ✅ Tree expands (not flat)
- ✅ All nodes visible
- ✅ No errors or warnings

### 7. Decision

**If ALL tests pass**: ✅ Safe to commit
```bash
git add Parley/Parsers/DialogParser.cs
git commit -m "fix: Parser improvement - all regression tests pass"
```

**If ANY test fails**: ❌ DO NOT COMMIT
- Investigate the regression
- Fix the issue
- Repeat testing
- Only commit when all tests pass

## Test File Locations

**Reference Files** (originals from Aurora):
- `TestingTools/TestFiles/*.dlg`

**Test Output** (Parley exports):
- `TestingTools/TestOutput/*_test.dlg`
- OR save to module directory with consistent naming

## Common Issues

### File paths with spaces

Use quotes:
```powershell
powershell -File "path\to\script.ps1" -OriginalFile "C:\path with spaces\file.dlg"
```

### HexAnalysis.ps1 not found

Run from TestingTools/Scripts directory:
```powershell
cd TestingTools\Scripts
.\HexAnalysis.ps1 -OriginalFile "..\TestFiles\lista.dlg" -ExportedFile "..\TestOutput\lista_test.dlg"
```

### Different results each time

Ensure consistent build:
```bash
dotnet clean
dotnet build
# Then re-test
```

## Success Example

```
========================================
 COMPARISON
========================================

Header Differences:
  StructCount: 14 -> 14  ✅
  FieldCount: 100 -> 100  ✅
  LabelCount: 26 -> 26  ✅

Struct Type Distribution:
  Type 0 : 8 -> 8  ✅
  Type 1 : 3 -> 3  ✅

StartingList Count: 1 -> 1  ✅
```

**Result**: All counts match → Safe to commit

## Failure Example

```
========================================
 COMPARISON
========================================

Header Differences:
  StructCount: 14 -> 14  ✅
  FieldCount: 100 -> 117  ❌ MISMATCH!

StartingList Count: 1 -> 4  ❌ WRONG!
```

**Result**: Regressions detected → DO NOT COMMIT
- Field count increased (creating extra fields)
- StartingList corrupted (pointing to wrong structs)
- FIX REQUIRED

---

**Remember**: 5 minutes of testing prevents hours of debugging!
