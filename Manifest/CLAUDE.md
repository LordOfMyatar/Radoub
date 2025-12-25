# CLAUDE.md - Manifest

Project guidance for Claude Code sessions working with Manifest, the journal editor for Neverwinter Nights.

## Project Overview

**Manifest** - Aurora-compatible journal editor for Neverwinter Nights JRL files
- Part of the Radoub multi-tool repository for NWN modding
- See parent `../CLAUDE.md` for repository-wide guidance
- Uses Radoub GitHub project board (unlike Parley which has its own)

### Core Architecture
- **Manifest/** - Main application (.NET 9.0, Avalonia UI for cross-platform)
- Shared library: `Radoub.Formats.Jrl` for JRL reading/writing

### Key Components
- `Radoub.Formats/Jrl/` - JRL parser (JrlReader, JrlWriter, JrlFile)
- `Manifest/Services/` - Settings, Logging (adapted from Parley patterns)
- `Manifest/Views/` - MainWindow, SettingsWindow
- `Manifest/Models/` - ViewModels for categories and entries

## Quick Commands

### Development
```bash
# Run Manifest
dotnet run --project Manifest/Manifest.csproj

# Build
dotnet build Manifest/Manifest.csproj

# Run tests
dotnet test Manifest.Tests/Manifest.Tests.csproj
```

## Project Structure

```
Manifest/
├── Manifest/
│   ├── App.axaml(.cs)
│   ├── Program.cs
│   ├── Manifest.csproj
│   ├── Models/
│   │   └── ViewModels.cs
│   ├── Services/
│   │   ├── SettingsService.cs
│   │   └── UnifiedLogger.cs
│   ├── Views/
│   │   ├── MainWindow.axaml(.cs)
│   │   └── SettingsWindow.axaml(.cs)
│   └── Themes/ (link to shared)
├── Manifest.Tests/
├── CHANGELOG.md
├── CLAUDE.md (this file)
└── README.md
```

## Development Patterns

### Service Patterns (from Parley)
- **SettingsService**: Manages `~/Radoub/Manifest/ManifestSettings.json`
- **UnifiedLogger**: Logs to `~/Radoub/Manifest/Logs/`
- **Dirty State**: Track unsaved changes via boolean flag

### UI Patterns
- **TreeView**: Categories as parent nodes, entries as children
- **Property Panel**: Edit selected category/entry properties
- **Menu Bar**: File (Open, Save, Recent), Edit, Help

### Path Handling (Privacy & Cross-Platform)
```csharp
// ✅ CORRECT - Cross-platform and privacy-safe
string path = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Radoub", "Manifest", "ManifestSettings.json");
```

## JRL Format Notes

### Data Model
- `JrlFile` contains list of `JournalCategory`
- Each `JournalCategory` contains list of `JournalEntry`
- Categories: Name, Tag, Priority, XP, Comment
- Entries: ID (uint), Text (JrlLocString), End flag

### Using Radoub.Formats.Jrl
```csharp
// Read
var jrl = JrlReader.Read("path/to/journal.jrl");

// Write
JrlWriter.Write(jrl, "path/to/output.jrl");
```

## Commit & PR Standards

Follow Radoub conventions (see `../CLAUDE.md`):
- Prefix commits: `[Manifest] type: description`
- Link to GitHub issues
- Update `CHANGELOG.md` for user-facing changes

### CHANGELOG Rules
- **Manifest CHANGELOG** (`CHANGELOG.md`): Manifest-specific changes only
- **Radoub CHANGELOG** (`../CHANGELOG.md`): Repository-level changes, add tool announcement

## Testing Requirements

### Before Committing
- Build succeeds
- Unit tests pass
- Settings persist correctly
- Logs are privacy-safe (no hardcoded paths)

### Round-Trip Testing
- Open JRL → Save JRL → Compare binary output
- Verify all categories/entries preserved

## Sprint Plan

### Sprint 1: Application Shell (#405) - COMPLETED
- Project structure
- Settings service
- Logging service
- Basic MainWindow
- File dialogs

### Sprint 2: File I/O (Next)
- Open JRL via JrlReader
- Display in TreeView
- Save via JrlWriter
- Dirty state

### Sprint 3: Category Editing
- Property panel for categories
- Add/Delete/Reorder categories

### Sprint 4: Entry Editing
- Property panel for entries
- Add/Delete/Reorder entries
- Auto-increment ID

## Resources

- **JRL Format**: `Radoub.Formats/Jrl/`
- **Parley Patterns**: `Parley/Parley/Services/` (Settings, Logger)
- **Research**: `Documentation/Research/Epic380_ManifestCore_Research.md`
- **BioWare GFF Spec**: `Documentation/Bioware_Aurora_GFF.md`

---

## Session Management

- Read this file at session start
- Use `TodoWrite` for task tracking
- Commit regularly with clear messages
- Check `git log --oneline -5` for recent context
