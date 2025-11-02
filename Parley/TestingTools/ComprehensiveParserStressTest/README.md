# Comprehensive Parser Stress Test

Automated test that creates a DLG file with comprehensive property coverage to validate parser round-trip integrity.

## Purpose

Tests every dialog node property and configuration to catch binary format regressions:
- All node properties (text, speaker, comment, sound, animation, etc.)
- Links and circular conversation structures
- Multi-speaker conversations
- Conditional scripts
- Animation variety
- Quest integration fields

## Test Structure

Creates `comprehensive_test.dlg` with:

- **Entry[0]**: Minimal baseline (defaults only)
- **Entry[1]**: Full properties (speaker, sound, animation loop, delay, quest, script)
- **Entry[2]**: Link target for circular conversation test
- **Entry[3-4]**: Multi-speaker conversation (npc_guard, npc_captain)
- **Entry[5-8]**: Animation variety (Taunt, Bow, Victory1, Read)
- **Reply[0]**: PC with conditional script
- **Reply[1]**: PC with link back (circular test)
- **Reply[2]**: Conversation ending
- **Reply[3]**: PC multi-speaker response

## Parameters

Action and condition parameters are **commented out** - will be added when properly implemented.

## Usage

```bash
cd TestingTools/ComprehensiveParserStressTest
dotnet run
```

## Output

Console shows:
- Test construction progress
- Round-trip validation results (✅/❌ per property)
- File location for manual Aurora testing

File created at: `TestingTools/ComprehensiveParserStressTest/bin/Debug/net9.0/comprehensive_test.dlg`

## Validation Checks

Automated:
- Entry/Reply/Start counts match
- All Entry[1] properties survive round-trip
- Link IsLink flag preserved
- Link index correct
- Conditional script preserved
- Animation variety intact

Manual (Aurora Toolset):
1. Open `comprehensive_test.dlg` in Aurora
2. Check load time (slow = corrections happening)
3. Verify tree structure displays correctly
4. Save without changes
5. Compare before/after with HexAnalysis.ps1

## Aurora Correction Detection

If Aurora is slow to load or modifies bytes on save:

```powershell
# Before Aurora touch
Copy-Item comprehensive_test.dlg original.dlg

# Open in Aurora, save without changes, close

# Compare
.\TestingTools\Scripts\HexAnalysis.ps1 -OriginalFile original.dlg -ExportedFile comprehensive_test.dlg
```

Byte differences reveal what Aurora is correcting.

## Exit Codes

- `0`: All tests passed
- `1`: Test failures or errors

## Logs

Check unified logs in `~\Parley\Logs\` for detailed parser operations.
