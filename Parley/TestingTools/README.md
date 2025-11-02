# Testing Tools

Organized test suite for Parley DLG compatibility testing.

## Directory Structure

```
TestingTools/
├── Core/                    # Essential testing utilities
│   ├── DumpChildLinks/     # Script export/import diagnostics
│   ├── HexComparison/      # Binary hex dump analysis
│   ├── RoundTripTest/      # Round-trip validation
│   └── SharedTestUtils/    # Shared test utilities
├── Scripts/                # Useful PowerShell scripts
│   └── HexAnalysis.ps1    # Binary analysis script
├── TestFiles/              # Test DLG files
└── TestFiles.json          # Test file configuration
```

## Core Tools

### RoundTripTest
Tests: Parse → Export → Re-import → Verify Structure

```bash
cd TestingTools/Core/RoundTripTest
dotnet run [file-key|path|suite:name]
```

**Validates:**
- Conversation structure preserved
- StartingList points to correct entries
- Pointer indices maintain correct references
- Entry/Reply counts match
- No corruption during round-trip

### HexComparison
Binary hex dump analysis for debugging export issues.

```bash
cd TestingTools/Core/HexComparison
dotnet run original.dlg exported.dlg
```

### DumpChildLinks
Diagnostic tool for testing script export/import and pointer relationships.

```bash
cd TestingTools/Core/DumpChildLinks
dotnet run
```

## Test Files Configuration

Edit `TestingTools/TestFiles.json` to add/modify test files:

```json
{
  "testFilesDirectory": "C:~\\Documents\\Neverwinter Nights\\modules\\LNS_DLG",
  "testFiles": {
    "chef": {
      "filename": "chef.dlg",
      "description": "Simple conversation - Chef from South Park",
      "complexity": "low",
      "features": ["basic_entry_reply", "simple_branching"]
    }
  },
  "testSuites": {
    "quick": ["chef", "hicks_hudson"],
    "medium": ["chef", "hicks_hudson", "lista"]
  }
}
```

## Available Test Files

- **chef** - Simple low-complexity conversation
- **hicks_hudson** - Colonial Marines with conditional pointers
- **lista** - Medium complexity with multiple starts
- **convolutedconvo** - High complexity with complex branching
- **blank** - Minimal/empty conversation (edge case)
- **generic_hench** - BioWare henchman conversation
- **x2_associate** - HotU expansion associate (very complex)

## Available Test Suites

- **quick** - Fast smoke test (chef, hicks_hudson)
- **medium** - Medium coverage (quick + lista)
- **full** - Comprehensive testing (all files)
- **edge_cases** - Edge case testing (blank)
- **bioware** - BioWare original files (generic_hench, x2_associate)

## Quick Start

```bash
# Test single file by key
cd TestingTools/Core/RoundTripTest
dotnet run chef

# Test with full path
dotnet run "C:\path\to\custom.dlg"

# Run test suite
dotnet run suite:quick
dotnet run suite:medium
dotnet run suite:full

# Show available files and suites
dotnet run
```

## Adding New Test Files

1. Add file to `TestFiles.json`:
```json
"myfile": {
  "filename": "myfile.dlg",
  "description": "Description of conversation",
  "complexity": "low|medium|high",
  "features": ["feature1", "feature2"]
}
```

2. Optionally add to suite:
```json
"testSuites": {
  "mysuite": ["chef", "myfile"]
}
```

3. Run test:
```bash
dotnet run myfile
# or
dotnet run suite:mysuite
```

## Shared Test Utilities

`Core/SharedTestUtils/TestFileHelper.cs` provides:
- `GetTestFilePath(key)` - Get full path from key
- `GetTestSuite(name)` - Get all files in suite
- `GetAllTestFiles()` - Get all configured test files
- `PrintAvailableFiles()` - Display available files and suites

Other test projects can reference SharedTestUtils to use the same configuration.

## Scripts

### HexAnalysis.ps1
PowerShell script for detailed binary analysis of DLG files. Useful for debugging export issues and comparing binary layouts.

```powershell
.\Scripts\HexAnalysis.ps1 -FilePath "path\to\file.dlg"
```

## Cleanup History

**October 4, 2025** - Major cleanup performed:
- Removed 90+ obsolete test project folders
- Removed 30+ duplicate/old PowerShell scripts
- Removed old analysis files and loose test files
- Organized remaining tools into Core/, Scripts/, TestFiles/
- Updated this README to reflect new structure
