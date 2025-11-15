# Known Issues

This document tracks known edge cases, limitations, and minor issues in Parley that are documented but not prioritized for immediate resolution.

## Table of Contents
- [Scrap Tab](#scrap-tab)

---

## Scrap Tab

### Scrap Entries Persist Across "Save As" Operations

**Issue**: When testing with a DLG file, if you:
1. Generate scrap entries by deleting nodes
2. Use "Save As" to overwrite the same test file (to restart testing)
3. Reopen the file

The old scrap entries from the previous session will still appear in the Scrap tab.

**Why**: Scrap entries are stored per-file path in `~/Parley/scrap.json`. When you "Save As" over the same filename, the scrap entries for that path remain. The scrap system doesn't distinguish between "old version" and "new version" of a file with the same path.

**Workaround**: Users can click "Clear All" in the Scrap tab before restarting tests, or manually delete specific entries.

**Impact**:
- Low impact on end users - typical workflow is editing existing dialogs, not repeatedly overwriting test files
- Primary impact is on testing/development workflows
- Scrap auto-cleanup (30 days) will eventually remove old entries

**Resolution**: Not prioritized. This is an edge case primarily affecting testing scenarios, not typical user workflows.

---

**Document Version**: 1.0
**Last Updated**: 2025-11-15
**Related Epic**: #112 (Scrap Tab Implementation)
