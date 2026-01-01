# CLAUDE.md - Parley

Tool-specific guidance for Parley, the dialog editor for Neverwinter Nights.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Tool Overview

**Parley** is an Aurora-compatible dialog editor for NWN DLG files.

**Current Version**: See `CHANGELOG.md` for latest version
**Project Board**: [Parley GitHub Project](https://github.com/users/LordOfMyatar/projects/2)

---

## Project Structure

```
Parley/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── Parley/
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs)
│   │   ├── SettingsWindow.axaml(.cs)
│   │   └── Controllers/ (settings section controllers)
│   ├── ViewModels/MainViewModel.cs
│   ├── Models/ (Dialog, DialogNode, DialogPtr)
│   ├── Services/ (DialogFileService, SoundService, ScriptService, etc.)
│   ├── Handlers/ (UI event handlers)
│   ├── Themes/ (JSON theme files)
│   └── Plugins/ (plugin manifests)
├── Parley.Tests/ (unit tests)
└── TestingTools/ (debug/analysis tools)
```

---

## Architecture Rules

### MainViewModel is Closed for New Logic

MainViewModel.cs has been refactored from 3,500+ to ~1,500 lines. **Do not add new logic here.**

**Pattern**:
```csharp
// ❌ BAD - Adding logic to MainViewModel
public void DoComplexThing() { /* 100 lines */ }

// ✅ GOOD - Extract to service
private readonly ComplexThingService _complexService = new();
public void DoComplexThing() => _complexService.Execute(CurrentDialog);
```

**Where to add new code**:
- Business logic → `Parley/Services/`
- UI event handling → `Parley/Handlers/`
- Data models → `Parley/Models/`

### Orphan Node Handling (Critical)

When nodes become unreachable from START points, Parley moves them to a scrap container instead of deleting them.

**Key Rules**:
- `IsLink=false`: Regular flow - traversed for orphan detection
- `IsLink=true`: Back-reference - NEVER traversed for orphan detection
- Only root orphans added to container (prevents duplicates)

See wiki: [Parley-Developer-Delete-Behavior](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Delete-Behavior)

---

## Aurora Compatibility

### Binary Format (CRITICAL)

DLG parsing is handled by `Radoub.Formats.Dlg.DlgReader/DlgWriter`. Before committing changes:
1. Build and run round-trip test
2. Check logs for WARN/ERROR
3. Test with lista.dlg (simple) and myra.dlg (complex)

**DO NOT commit if logs show struct/buffer violations.**

### Filename Constraints

Aurora Engine enforces strict limits:
- **Max 16 characters** (excluding `.dlg`)
- Lowercase, alphanumeric + underscore only
- Violations cause silent in-game rejection

### Speaker Tags

- Empty speaker (`""`) works with any NPC
- Tagged speaker validated against area creatures
- Invalid tags = entire conversation silently discarded

---

## Quick Commands

```bash
# Run Parley
dotnet run --project Parley/Parley/Parley.csproj

# Build
dotnet build Parley/Parley.sln

# Run tests
dotnet test Parley/Parley.Tests
```

---

## Testing

### Unit Tests

```bash
dotnet test Parley/Parley.Tests
```

### Integration Tests (FlaUI)

```bash
dotnet test Radoub.IntegrationTests --filter "FullyQualifiedName~Parley"
```

### Critical Tests for Orphan Handling

- `OrphanNodeTests.cs:OrphanContainer_ShouldNotDuplicateNestedOrphans`
- `OrphanNodeTests.cs:DeletingLinkParent_ShouldOrphanLinkedNodes`
- `OrphanContainerIntegrationTests.cs:DeletingParentEntry_CreatesOrphanContainer_AndPersistsToFile`

---

## Commit Conventions

Use `[Parley]` prefix:

```bash
[Parley] fix: Resolve parser buffer overflow (#123)
[Parley] feat: Add script parameter preview (#456)
```

Changes go in `Parley/CHANGELOG.md` (not Radoub CHANGELOG).

---

## PR Standards

- **15 sentences max** - longer requires consultation
- **What/Why/Tests** format
- Link issues: `closes #X`, `relates to #Y`

---

## Logging

- Logs are privacy-scrubbed (paths show `~`)
- Location: `~/Parley/Logs/[session]/`
- Review logs yourself - don't ask user to log-dive

---

## Related Documentation

- [Parley-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Architecture)
- [Parley-Developer-Testing](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Testing)
- [Parley-Developer-Delete-Behavior](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Delete-Behavior)
- [Parley-Developer-CopyPaste](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-CopyPaste)
- [Parley-Developer-Scrap-System](https://github.com/LordOfMyatar/Radoub/wiki/Parley-Developer-Scrap-System)

---
