# CLAUDE.md - Trebuchet

Project guidance for Claude Code sessions working with the Trebuchet (Radoub Launcher) tool.

---

## Tool Overview

**Trebuchet** is the central hub for the Radoub toolset. It provides:

- **Tool Launcher** - Discover and launch Parley, Manifest, Quartermaster, Fence
- **Global Settings Manager** - Game paths, TLK configuration, theme/font preferences (Sprint 2)
- **Module Info Editor** - Edit module.ifo properties (Sprint 3)

---

## Architecture

```
Trebuchet/
├── CHANGELOG.md
├── CLAUDE.md (this file)
├── Trebuchet/
│   ├── Trebuchet.csproj
│   ├── App.axaml(.cs)
│   ├── Program.cs
│   ├── Services/
│   │   ├── CommandLineService.cs   # CLI args parsing
│   │   ├── SettingsService.cs      # Tool-specific settings
│   │   └── ToolLauncherService.cs  # Discover and launch tools
│   ├── ViewModels/
│   │   └── MainWindowViewModel.cs
│   ├── Views/
│   │   └── MainWindow.axaml(.cs)
│   ├── Themes/
│   │   ├── light.json
│   │   └── dark.json
│   └── Assets/
└── Trebuchet.Tests/  (TODO)
```

### Key Design Decisions

- **Folder**: `Trebuchet/` (user-facing name)
- **Namespace**: `RadoubLauncher` (internal, like Parley uses `ConversationEditor`)
- **Assembly**: `Trebuchet`

### Dependencies

- `Radoub.Formats` - Shared settings, file format parsers
- `Radoub.UI` - Theme manager, shared UI services

---

## Development Workflow

### Building

```bash
# Build Trebuchet only
dotnet build Trebuchet/Trebuchet/Trebuchet.csproj

# Build all tools (includes Trebuchet)
dotnet build Radoub.sln

# Run
dotnet run --project Trebuchet/Trebuchet/Trebuchet.csproj
```

### Command Line Arguments

```bash
Trebuchet --help           # Show help
Trebuchet --safemode       # Reset theme/fonts to defaults
Trebuchet --module "path"  # Open with specific module
```

---

## Services

### ToolLauncherService

Discovers and launches Radoub tools:

1. Check `RadoubSettings.Instance.[ToolName]Path` (set when tools run)
2. Check same directory as Trebuchet
3. Check common installation paths

```csharp
var launcher = ToolLauncherService.Instance;
var tools = launcher.Tools;  // All known tools with availability status
launcher.LaunchTool("Parley");  // Launch by name
launcher.LaunchTool(tool, "--file myfile.dlg");  // Launch with args
```

### SettingsService

Tool-specific settings stored at `~/Radoub/Trebuchet/TrebuchetSettings.json`:

- Window position/size
- Recent modules list
- Theme and font preferences

### RadoubSettings (Shared)

Global settings at `~/Radoub/RadoubSettings.json`:

- Game installation paths
- TLK language/gender
- Tool paths (auto-registered)
- Current module path

---

## Theme System

Uses `Radoub.UI.ThemeManager` for consistent theming across tools:

- Theme files in `Themes/` folder (light, dark, vscode-dark, fluent-light, accessibility themes)
- Theme ID format: `org.radoub.theme.{name}` (universal IDs for shared themes)
- On startup, Trebuchet copies bundled themes to `~/Radoub/Themes/` so other tools can access them
- Accessibility: Font size scaling, high contrast support, colorblind themes (deuteranopia, protanopia, tritanopia)

---

## Commit Standards

Use `[Radoub]` prefix (Trebuchet is part of Radoub infrastructure):

```
[Radoub] feat: Add tool launcher service for Trebuchet (#907)
[Radoub] fix: Handle missing tools gracefully in Trebuchet
```

---

## MVP Sprints

### Sprint 1: Tool Launcher (Current)
- [x] Project structure
- [x] MainWindow with tool cards
- [x] ToolLauncherService
- [x] Theme support
- [ ] Test builds and tool launching

### Sprint 2: Global Settings UI
- [ ] SettingsWindow
- [ ] Game path configuration
- [ ] TLK settings
- [ ] Theme/font selection

### Sprint 3: Module Info Editor
- [ ] ModuleInfoWindow
- [ ] IFO reading/display
- [ ] IFO editing and saving
- [ ] ErfWriter (if needed)

---

## Testing Requirements

Before committing:

1. Build succeeds: `dotnet build Trebuchet/Trebuchet/Trebuchet.csproj`
2. App launches without errors
3. Tool cards display correctly
4. Tools launch when clicked (if installed)

---

## Resources

- [Research Notes](../NonPublic/Trebuchet/Research_907_Launcher_Architecture.md)
- [RadoubSettings.cs](../Radoub.Formats/Radoub.Formats/Settings/RadoubSettings.cs)
- [ThemeManager.cs](../Radoub.UI/Radoub.UI/Services/ThemeManager.cs)
