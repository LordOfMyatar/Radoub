# TestingTools Cleanup Plan

## Current Structure Analysis
- **90+ test project folders** - Most are obsolete one-off debugging tools
- **30+ PowerShell scripts** - Various analysis scripts, many duplicates
- **10+ loose .cs files** - Old test files
- **Analysis text files** - Old debugging outputs (chef_analysis.txt, etc.)
- **Mixed organization** - Tools scattered with no clear structure

## Proposed Clean Structure

```
TestingTools/
├── Core/                    # Essential testing utilities
│   ├── HexComparison/      # Hex dump analysis (KEEP)
│   ├── RoundTripTest/      # Round-trip validation (KEEP)
│   └── DumpChildLinks/     # Script export testing (KEEP - new)
├── TestFiles/              # Test DLG files (KEEP)
│   └── TestFiles.json      # Test file registry
├── SharedUtils/            # Shared test utilities (if exists)
├── Scripts/                # Useful PowerShell scripts
│   └── HexAnalysis.ps1     # Move here from root
└── _Archive/               # Old tools for reference
```

## Tools to KEEP (Move to Core/)
1. **HexComparison/** - Binary comparison utility
2. **RoundTripTest/** - Export/import validation
3. **DumpChildLinks/** - Script export diagnostics (just created)
4. **TestFiles/** - Test data files
5. **SharedTestUtils/** - If has useful utilities

## DELETE Immediately
All obsolete test project folders (unless marked KEEP):
- AnalyzeListProject, AsheraExportTest, AuroraAlgorithmImplementation
- AuroraCompatibilityTest, AuroraExportTest, AuroraFixTest
- AuroraListAnalyzer, AuroraOrderAnalysis, AuroraStartDebug
- AuroraTest, AuroraTestDir, BinaryAnalysis, BinaryDebug
- BinaryLayoutGaps, BinaryTreeComparison, BoundaryProject
- BufferDebugTest, CExoLocStringAnalysis, CompareChefStructure
- CompareExportOrder, CompareStructures, ComplexFileTest
- ComprehensiveRoundTripTest, ConversationAnalyzer, ConversationOrder
- ConversationOrderTest, DebugLista032, DebugPointerResolution
- DebugRootFields, DebugScripts (folder in TestingTools), DebugTest
- DiagnoseContentMismatch, DiagnoseHudson, DiagnosticTest
- EntryOrderTest, ExactStructAnalysis, ExportTest
- FieldDataAnalysis, FieldIndicesProject, GffListFieldTest
- GffValidation, HexDumpAnalysis, IndexTest
- LinkDebug, ListaExportTest, MinimalTest
- MinimalWpfTest, PointerDebug, QuickExport
- QuickParseTest, QuickTestApp, ReverseEngineerTest
- StructAnalysis, StructIndexAnalysis, StructOrderDebug
- TestAuroraExport, TestCleanTreeStructure, TestConsole
- TestCopyTreeStructure, TestExactRoundTripApp, TestExport
- TestExportOffsets, TestExportProject, TestFileValidatorApp
- TestFontScaling, TestGuiImprovements, TestLista032Tree
- TestListaExportApp, TestListBoundary, TestParser16
- TestRefactoredExport, TestRoundTripApp, TestScripts
- TreeAnalysis, TreeStructureDebug, TreeStructureTest
- TreeTest, TreeViewGenerator, VerifyExportTest

## DELETE - Loose Files in TestingTools/
- All *.ps1 scripts (will save useful ones to Scripts/ folder)
- All *.cs files (old test files)
- All *_analysis.txt files
- All *.dlg test files (keep only in TestFiles/)
- All *.csproj files in root
- bin/ and obj/ folders

## Scripts to PRESERVE (move to Scripts/)
- HexAnalysis.ps1 (useful for binary debugging)
- Any others you want to keep (ask user)

## Root Cleanup
Move to TestingTools/Scripts/ or delete:
- CompareOriginalVsExport.cs
- TestEntryFieldFix.cs
- TestExport.cs
- TestListaExport.cs
- TestParseExported.cs
- TestParseRenamed.cs
- TestStartStructOrder.cs
- TestStructCount.cs
- convert-gifs-to-svg.ps1
- Fix-Broken-Lines.ps1
- Fix-Broken-Lines-Parameterized.ps1

## Execution Order
1. Create new folder structure
2. Move KEEP items to Core/
3. Move useful scripts to Scripts/
4. Delete all obsolete folders
5. Delete loose files
6. Update README.md
7. Clean up root
