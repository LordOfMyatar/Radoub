# CLAUDE.md - Manifest

Tool-specific guidance for Manifest, the journal editor for Neverwinter Nights.

**Read the repository-level [CLAUDE.md](../CLAUDE.md) first** for shared conventions.

---

## Tool Overview

**Manifest** is an Aurora-compatible journal editor for NWN JRL files.

**Current Version**: See `CHANGELOG.md` for latest version
**Project Board**: [Radoub GitHub Project](https://github.com/users/LordOfMyatar/projects/3)

---

## Project Structure

```
Manifest/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── Manifest/
│   ├── Views/MainWindow.axaml(.cs)
│   ├── Models/ (ViewModels for categories/entries)
│   ├── Services/ (SettingsService, ThemeManager)
│   └── Themes/
└── Manifest.Tests/ (unit tests)
```

---

## JRL Format

### Data Model

- `JrlFile` contains list of `JournalCategory`
- Each `JournalCategory` contains list of `JournalEntry`
- Categories: Name, Tag, Priority, XP, Comment
- Entries: ID (uint), Text (localized), End flag

### Using Radoub.Formats.Jrl

```csharp
// Read
var jrl = JrlReader.Read("path/to/journal.jrl");

// Write
JrlWriter.Write(jrl, "path/to/output.jrl");
```

---

## Quick Commands

```bash
# Run Manifest
dotnet run --project Manifest/Manifest/Manifest.csproj

# Build
dotnet build Manifest/Manifest/Manifest.csproj

# Run tests
dotnet test Manifest/Manifest.Tests
```

---

## Testing

### Unit Tests

```bash
dotnet test Manifest/Manifest.Tests
```

### Integration Tests (FlaUI)

```bash
dotnet test Radoub.IntegrationTests --filter "FullyQualifiedName~Manifest"
```

### Round-Trip Testing

- Open JRL → Save JRL → Compare binary output
- Verify all categories/entries preserved

### Cross-Tool CLI Tests

**Location**: `Manifest.Tests/CommandLineServiceTests.cs`

Manifest has unique `--quest` and `--entry` flags for cross-tool navigation from Parley:
- `--quest <tag>` - Navigate to a specific quest after opening
- `--entry <id>` - Select a specific entry (requires --quest)

Example cross-tool invocation: `Manifest.exe --file module.jrl -q my_quest -e 100`

---

## Commit Conventions

Use `[Manifest]` prefix:

```bash
[Manifest] fix: Correct entry ID handling (#123)
[Manifest] feat: Add category reordering (#456)
```

Changes go in `Manifest/CHANGELOG.md` (not Radoub CHANGELOG).

---

## Logging

- Logs are privacy-scrubbed (paths show `~`)
- Location: `~/Manifest/Logs/[session]/`
- Uses shared UnifiedLogger from Radoub.Formats

---

## Related Documentation

- [Manifest-Developer-Architecture](https://github.com/LordOfMyatar/Radoub/wiki/Manifest-Developer-Architecture)
- [Radoub-Formats-JRL](https://github.com/LordOfMyatar/Radoub/wiki/Radoub-Formats-JRL)

---
