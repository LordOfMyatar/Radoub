# Regression Testing Guide

## Overview

Automated regression testing to prevent Aurora compatibility breaks in the DLG parser.

## Quick Test Workflow

### Before Committing Parser Changes

1. **Build Parley**:
   ```bash
   dotnet build Parley/Parley.Avalonia.csproj
   ```

2. **Run Parley and test reference files**:
   - Open each reference file in `TestingTools/TestFiles/`
   - Save As to `TestingTools/TestOutput/`
   - Use consistent naming: `{original}_test.dlg`

3. **Run Quick Regression Check**:
   ```powershell
   .\TestingTools\Scripts\QuickRegressionCheck.ps1 -OriginalFile "TestingTools\TestFiles\2entry2reply.dlg" -ExportedFile "TestingTools\TestOutput\2entry2reply_test.dlg"
   ```

4. **Repeat for all reference files**

5. **If ALL tests pass**: Safe to commit parser changes

6. **If ANY test fails**: DO NOT COMMIT - fix regression first

## Reference Test Files

Critical test files in `TestingTools/TestFiles/`:

- **2entry2reply.dlg** - Simple conversation (2 entries, 2 replies, no parameters)
- **parameter_hell.dlg** - Parameter testing (duplicate params, multiple types)
- **lista.dlg** - Medium complexity (3 entries, 3 replies, pointer chains)
- **chef.dlg** - Real-world conversation from module

## Test Criteria

### Critical (Must Pass)
- ✅ Struct count matches
- ✅ Field count matches
- ✅ StartingList count matches
- ✅ Tree expands in Aurora (not flat)

### Important (Should Match)
- Field indices count (4:1 ratio)
- List indices count
- Label count

### Nice to Have
- Exact field indices match
- Exact byte-for-byte match

## Automated Testing (Future)

### Phase 1: Manual Testing (Current)
- User manually opens files in Parley
- User manually saves and runs regression scripts
- Quick feedback before commits

### Phase 2: Command-Line Automation
- Add command-line args to Parley: `--open`, `--save-as`, `--exit`
- Script automates: build → open → save → compare
- Run entire test suite with one command

### Phase 3: CI/CD Integration
- GitHub Actions workflow
- Runs on every PR to main/develop
- Blocks merge if regressions detected
- See `.github/workflows/regression-tests.yml` (future)

## Common Regression Patterns

### Flat Tree (Critical)
**Symptom**: Tree shows all nodes at root level in Aurora
**Cause**: StartingList offset calculation wrong
**Check**: `StartingList Field DataOrDataOffset` value
**Fix**: Verify write order matches FixListFieldOffsets calculation

### Field Count Mismatch
**Symptom**: Aurora can't read file or shows errors
**Cause**: Creating wrong number of fields
**Check**: Field count in header
**Fix**: Audit field creation in CreateAuroraCompatibleGffStructures

### Missing Parameters
**Symptom**: Parameters don't show in Aurora UI
**Cause**: Parameter structs not created or list offsets wrong
**Check**: ConditionParams/ActionParams list data
**Fix**: Verify CreatePointerParameterStructs and WriteAuroraListIndices

## Test File Naming Convention

- **Reference files**: `{name}.dlg` (original Aurora files)
- **Test exports**: `{name}_test.dlg` or `{name}##.dlg` (Parley exports)
- **Keep test files small**: Use FAT16-compatible names (8.3 format preferred)

## Adding New Test Files

1. Create minimal test case that reproduces issue
2. Save Aurora-generated reference file to `TestingTools/TestFiles/`
3. Document what it tests in this file
4. Add to regression test checklist

## Troubleshooting

### HexAnalysis.ps1 not found
```powershell
cd TestingTools/Scripts
ls HexAnalysis.ps1  # Verify exists
```

### Permission denied
```powershell
powershell -ExecutionPolicy Bypass -File .\QuickRegressionCheck.ps1 ...
```

### Different results between runs
- Ensure you're testing same Parley build
- Check git status - uncommitted changes?
- Clear TestOutput directory and re-run

## Quick Reference Commands

```powershell
# Build
dotnet build Parley/Parley.Avalonia.csproj

# Test single file
.\TestingTools\Scripts\QuickRegressionCheck.ps1 -OriginalFile "path\to\original.dlg" -ExportedFile "path\to\exported.dlg"

# Run full hex comparison (detailed)
.\TestingTools\Scripts\HexAnalysis.ps1 -OriginalFile "path\to\original.dlg" -ExportedFile "path\to\exported.dlg"
```

## Git Workflow Integration

### Before Committing
```bash
# 1. Make parser changes
# 2. Build and test
dotnet build Parley/Parley.Avalonia.csproj

# 3. Run Parley, save test files
# 4. Run regression checks
# 5. If all pass:
git add Parley/Parsers/DialogParser.cs
git commit -m "fix: Parser fix - all regression tests pass"
```

### After Regression Detected
```bash
# DO NOT COMMIT
# Fix the regression
# Re-test
# Only commit when tests pass
```

## Future Enhancements

- [ ] Automated test runner (Phase 2)
- [ ] CI/CD integration (Phase 3)
- [ ] Performance benchmarks
- [ ] Memory leak detection
- [ ] Fuzz testing with random DLG files
- [ ] Visual diff tool for hex output

---

**Last Updated**: 2025-10-19
**Status**: Phase 1 (Manual Testing) - Active
