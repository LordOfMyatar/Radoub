# HexDumpParams

Hex dump comparison tool for DLG parameter lists (ActionParams and ConditionParams).

## Purpose

Compares binary format of parameter lists between original Aurora DLG files and Parley exported files. Helps diagnose issues with ListIndices section data.

## Usage

```bash
dotnet run --project TestingTools/HexDumpParams/HexDumpParams.csproj -- <original.dlg> <exported.dlg>
```

## Example

```bash
dotnet run --project TestingTools/HexDumpParams/HexDumpParams.csproj -- \
  "~\Documents\Neverwinter Nights\modules\LNS_DLG\chef.dlg" \
  "~\Documents\Neverwinter Nights\modules\LNS_DLG\chef01.dlg"
```

## Output

Shows side-by-side hex dumps of:
- **ActionParams** lists (first 3 nodes with non-empty lists)
- **ConditionParams** lists (first 3 pointers with non-empty lists)

For each parameter list:
- Offset in ListIndices section
- 32 bytes of hex data
- Interpreted data (count + struct indices)
- Difference highlighting

## Binary Format

Parameter lists in ListIndices section:
```
[4 bytes] count (DWORD)
[4 bytes] struct_index_0 (DWORD)
[4 bytes] struct_index_1 (DWORD)
...
```

Empty lists: `00 00 00 00` (count=0, no indices)

## Known Issues Found (2025-10-24)

Using chef.dlg vs chef01.dlg comparison:

1. **ActionParams count mismatch**:
   - Struct[23]: Original count=0, Exported count=3 ❌
   - Struct[37]: Original count=3, Exported count=0 ❌
   - Struct[42]: Original count=3, Exported count=2 ❌

2. **ConditionParams struct indices different**:
   - Format is correct (count + indices)
   - Values differ (expected due to struct reordering)

3. **ListIndices size difference**:
   - Original: 604 bytes
   - Exported: 380 bytes (224 bytes missing)

## Conclusion

Binary format is correct. Issue is in mapping parameter data to ListIndices section during export.
