# NEW_TOOL_BOOTSTRAP.md — Radoub New-Tool Bootstrap Guide

> Loaded on demand when bootstrapping a new Radoub tool. Not auto-loaded every session.

This guide holds the full checklist and detailed patterns for adding a new tool to the Radoub repository. It was extracted from the always-loaded root `CLAUDE.md` so the per-session context stays lean. Read it when starting a new tool; the root `CLAUDE.md` retains a one-paragraph pointer.

## Table of Contents

- [New Tool Bootstrap Checklist](#new-tool-bootstrap-checklist)
  - [Reference Implementation](#reference-implementation)
  - [Pre-Coding Checklist](#pre-coding-checklist)
  - [Required Components (Every Tool)](#required-components-every-tool)
  - [Shared Library References](#shared-library-references)
  - [Backups Before Destructive File Operations](#backups-before-destructive-file-operations)
  - [Spell-Check Integration (Radoub.Dictionary)](#spell-check-integration-radoubdictionary)
  - [Implementation Checklist](#implementation-checklist)
  - [Trebuchet Integration](#trebuchet-integration)
  - [Cross-Tool Dispatch (ToolDispatchService)](#cross-tool-dispatch-tooldispatchservice)
  - [Testing Requirements](#testing-requirements)
  - [UI Uniformity Checklist (Epic #959)](#ui-uniformity-checklist-epic-959)
  - [File Browser Adoption (FileBrowserPanelBase)](#file-browser-adoption-filebrowserpanelbase)
  - [Post-Implementation Audit](#post-implementation-audit)
  - [Versioning (NBGV)](#versioning-nbgv)
  - [Common Mistakes to Avoid](#common-mistakes-to-avoid)
- [UI/UX Detailed Patterns](#uiux-detailed-patterns)
  - [Button Labeling Standards](#button-labeling-standards)
  - [Progress Indicator Standards](#progress-indicator-standards)
  - [Deferred Loading Patterns](#deferred-loading-patterns)
- [Aurora File Format Implementation](#aurora-file-format-implementation)

---

## New Tool Bootstrap Checklist

**CRITICAL**: Before writing any code for a new tool, complete this checklist. This prevents pattern drift and rework.

[↑ TOC](#table-of-contents)

### Reference Implementation

**Quartermaster is the canonical reference** for new Radoub tools. Study its structure before starting. For the single-resource blueprint pattern specifically (one file = one editable resource), **Relique, Fence, and Quartermaster are the most feature-complete** — study all three for the full surface (new-resource flow, recent files, properties, preview, settings) before deciding what your tool needs.

### Pre-Coding Checklist

Before writing code, verify you understand these patterns by reading the reference files:

| Component | Reference File | Purpose |
|-----------|---------------|---------|
| Program.cs startup | `Quartermaster/Quartermaster/Program.cs` | Logging init, command line, Avalonia setup |
| CommandLineService | `Quartermaster/Quartermaster/Services/CommandLineService.cs` | `--file`, `--safemode`, `--help` pattern |
| SettingsService | `Quartermaster/Quartermaster/Services/SettingsService.cs` | JSON settings, theme, font persistence |
| MainWindow structure | `Quartermaster/Quartermaster/Views/MainWindow.axaml` | Menu bar, status bar, panel layout |
| Panel controls | `Quartermaster/Quartermaster/Controls/` | BasePanelControl inheritance |
| Theme support | `Radoub.UI/Radoub.UI/Services/ThemeManager.cs` | Dark/light theme, custom themes |
| Model preview | `Radoub.UI/Radoub.UI/Controls/ModelPreviewGLControl.cs` | Silk.NET OpenGL MDL preview — reuse, do not fork |

### Required Components (Every Tool)

```
ToolName/
├── ToolName/
│   ├── Program.cs                    # Copy from Quartermaster, update namespace
│   ├── App.axaml + App.axaml.cs      # Avalonia app setup
│   ├── Services/
│   │   ├── CommandLineService.cs     # --file, --safemode, --help
│   │   └── SettingsService.cs        # Tool-specific settings + theme
│   ├── Views/
│   │   ├── MainWindow.axaml          # Standard menu structure (incl. File → New)
│   │   └── Dialogs/                  # About, Settings, New-resource flow
│   ├── ViewModels/
│   │   └── MainWindowViewModel.cs    # MVVM pattern
│   └── Controls/                     # Custom controls
├── ToolName.Tests/                   # Unit tests from day 1
├── CLAUDE.md                         # Tool-specific guidance
├── CHANGELOG.md                      # Initialized with first version
└── README.md                         # User-facing documentation
```

### Shared Library References

Every tool should reference these shared libraries:

| Library | Purpose | Required? |
|---------|---------|-----------|
| `Radoub.Formats` | GFF, 2DA, TLK, KEY/BIF parsing | Yes |
| `Radoub.UI` | ThemeManager, ScriptBrowser, AboutWindow, shared controls | Yes |
| `Radoub.Dictionary` | Spell-checking for text fields | If tool has text editing |

**AboutWindow**: Use the universal `AboutWindow` from `Radoub.UI` - don't create tool-specific About dialogs.

**Appearance lookups**: If the tool resolves an appearance ID to a model/display name, use the shared service for the format instead of a tool-local 2DA reader:

| Service | Resource | Resolves |
|---------|----------|----------|
| `Radoub.Formats.Services.PlaceableAppearanceService` *(Sprint 1)* | placeables.2da | appearance ID → model name + display name (TLK with LABEL fallback) |

### Backups Before Destructive File Operations

**MANDATORY**: Any operation that deletes, overwrites, or renames a user file must back it up first. Backups are not optional — every destructive path in the toolset snapshots before touching disk, and a tool that skips this loses user data on a misclick.

Use the shared `Radoub.UI.Services.Search.BackupService`. Do not roll your own.

| Operation | Pattern |
|-----------|---------|
| Delete a file | `await backupService.BackupFilesAsync(new[]{ path }, moduleName)` → then `File.Delete(path)` |
| Overwrite a file | Back up the existing file before writing, or write via temp + atomic replace and snapshot the prior version |
| Rename a file | Back up source (and any reference files you rewrite) before the move — see `ResRefRenameOrchestrator` |
| Repack an archive (.mod/.erf) | `await backupService.BackupArchiveAsync(modPath, moduleName)` before replacing — see `ModulePackService` |

Rules:
- Backups go to the shared root `~/Radoub/Backups/{moduleName}/{timestamp}/` — `BackupService` handles this; never hardcode a backup path.
- `moduleName` = `Path.GetFileNameWithoutExtension(RadoubSettings.Instance.CurrentModulePath)` (fall back to `"unknown"`).
- The confirm dialog for a destructive action should tell the user a backup is saved — don't say "cannot be undone" when it can.
- Compiled/derived output that regenerates from source (e.g. `.ncs` from `.nss`) is exempt — backing it up is wasteful.

Reference: Relique item delete (#2347), `ModulePackService` (#2246), `ResRefRenameOrchestrator`.

### Spell-Check Integration (Radoub.Dictionary)

For tools with game-facing text (dialog, journal entries, descriptions), use `Radoub.Dictionary`:

**User Dictionary Location**: `~/Radoub/Dictionaries/`
- `custom.dic` - JSON format (programmatic)
- `custom_dictionary.txt` - Text format (user-editable, one word per line)

**Usage Pattern** (see Parley/Manifest for examples):

```csharp
// In App.axaml.cs or startup
var dictionaryManager = new DictionaryManager();
var spellChecker = new SpellChecker(dictionaryManager);
await spellChecker.LoadBundledDictionaryAsync("en_US", loadNwnDictionary: true);

// Load user's custom words
var userDict = UserDictionaryService.Instance;
foreach (var word in userDict.Words)
{
    dictionaryManager.AddWord(word);
}

// Check spelling
if (!spellChecker.IsCorrect("wrod"))
{
    var suggestions = spellChecker.GetSuggestions("wrod");
}

// Add to custom dictionary
userDict.AddWord("Waterdeep");
```

**Included Dictionaries**:
- Hunspell `en_US` (bundled)
- NWN/D&D terms (bundled)
- User custom words (shared across all tools)

**NWN-Specific Features**:
- Token variables (`<FirstName>`, `<CUSTOM1016>`) are ignored during spell-check
- D&D terminology pre-loaded (races, classes, spell names)

### Implementation Checklist

**Day 1 Requirements** (before writing significant code):

- [ ] **Read Quartermaster/Program.cs** - Understand startup sequence
- [ ] **Copy CommandLineService pattern** - Same flags, same behavior
- [ ] **Copy SettingsService pattern** - JSON storage in `~/Radoub/ToolName/`
- [ ] **Use ThemeManager from Radoub.UI** - Don't reinvent theming
- [ ] **Use IGameDataService** for any 2DA/TLK data - Never hardcode game data
- [ ] **Inherit BasePanelControl** for panel controls - Consistent styling
- [ ] **Add to Radoub.sln** - Root solution builds all tools
- [ ] **Initialize CHANGELOG.md** with branch/PR format
- [ ] **Create CLAUDE.md** with tool-specific patterns
- [ ] **Add dictionary support** if tool has text editing fields
- [ ] **Variables editing**: use the shared `Radoub.UI.Controls.VariablesPanel` *(Sprint 3)* — do NOT fork `VariableViewModel`. The shared panel covers all GFF VarTable types (int/float/string/object/location) with validation; host supplies the `UndoRedoManager`.
- [ ] **`AssemblyName` matches the tool name** in the `.csproj` (e.g. `<AssemblyName>Relique</AssemblyName>` for the Relique tool). `RootNamespace` can differ if a legacy internal name is preferred, but the built binary must ship as `ToolName.exe` / `ToolName` — Trebuchet's sibling discovery (`ToolLauncherService.RefreshPathsFromSiblingDirectory`) walks `ToolInfo.Name`, and the release workflow / cross-tool launchers all assume `Radoub/ToolName.exe`. Mismatch causes silent launch failures + a forced rename later with a settings-path migration (#2080).
- [ ] **Override `IndexMetadataAsync` + `ReadSourceMetadataAsync`** on the file browser panel if the format has Name and/or Tag fields — see [File Browser Adoption](#file-browser-adoption-filebrowserpanelbase)
- [ ] **Wire Undo/Redo via shared `UndoRedoManager`** — register Ctrl+Z / Ctrl+Y and route every user-initiated mutation through `IUndoableCommand` so the standard Undo/Redo menu items work from day one (#2231). Do **not** ship disabled Undo/Redo menu stubs — either wire them correctly or omit them.
- [ ] **Ship a New-resource flow** (`File → New`, Ctrl+N) — the tool must let the user create a blank resource from scratch, not only edit existing files. A `SaveFilePicker`/`Save As`-into-module path that *copies* a base-game blueprint is **not** a substitute: it can't make a resource that isn't already in the game data. Match the lightest pattern that fits the format — Relique's New Item Wizard (base-item-type branching), Fence's simpler new-store, or a plain blank-document `File → New`. A tool that can only edit pre-existing files is incomplete. (Reliquary shipped without this — #2367 — because Save-As-copy masked the gap.)
- [ ] **Wire Recent Files / MRU on BOTH ends** — see [Trebuchet Integration](#trebuchet-integration). Easy to get for free via `BaseToolSettingsService` + a `ToolRecentFilesService` case; easy to forget entirely (#2247, #2368).

### Trebuchet Integration

New tools must integrate with Trebuchet (the Radoub launcher):

- [ ] **Add to ToolLauncherService** - Register tool for discovery and launch. **Do not** set `AssemblyName` on the `ToolInfo` — leave it null so sibling discovery falls back to `Name`. Setting `AssemblyName` means the built `.csproj` `<AssemblyName>` diverges from the tool name, which the bootstrap checklist forbids (see Day 1 Requirements).
- [ ] **Support `--file` argument** - Enable launching with a file from Trebuchet's MRU dropdown
- [ ] **Wire Recent Files (both ends)** - The tool must (a) **persist its own MRU**: write the recent-files list to its `~/Radoub/ToolName/ToolNameSettings.json` on every open/save, in the same shape the other tools use, AND (b) **register the tool in `ToolRecentFilesService.GetSettingsPath`** (Trebuchet) so the launcher's per-tool Recent Files dropdown can find and read that list. Missing either end ships a permanently empty dropdown — the exact Relique regression in #2247 (the `GetSettingsPath` case was absent, so the card's Recent Files never populated). Verify by opening a file, relaunching Trebuchet, and confirming it appears under the tool's card.
- [ ] **Use Radoub.UI file browsers** - Use `ModuleBrowserWindow`, `ScriptBrowserWindow`, `DialogBrowserWindow`, or create resource-specific browser instead of OS file pickers for module resources
- [ ] **Register in `ToolDispatchService`** — map your `ResourceTypes.*` to `new DispatchableToolInfo { ToolName = "X", AssemblyName = "X" }`. Both fields must match the same identifier so search-bar dispatch and `ItemEditorLauncher`-style cross-tool launchers find the binary. See [Cross-Tool Dispatch](#cross-tool-dispatch-tooldispatchservice).

### Cross-Tool Dispatch (ToolDispatchService)

When another tool (or the Trebuchet search bar) should be able to open a resource your tool owns, register the mapping in `ToolDispatchService` (`Radoub.UI/Radoub.UI/Services/Search/ToolDispatchService.cs`):

```csharp
// In ToolDispatchService — map the resource type your tool edits to the tool binary.
{ ResourceTypes.Utp, new DispatchableToolInfo { ToolName = "Reliquary", AssemblyName = "Reliquary" } }
```

- `ResourceTypes.X` — the resource type constant for the format your tool edits (UTI, UTM, UTC, UTP, …).
- `ToolName` **and** `AssemblyName` must be the **same identifier** — search-bar dispatch and the `ItemEditorLauncher`-style cross-tool launchers both resolve the binary by this name, and it must equal the `<AssemblyName>` in the tool's `.csproj` (i.e. `Radoub/ToolName.exe`).

The `[Edit → OtherTool]` launch pattern (one tool opening a resource in another) flows through this same registration: the launcher asks `ToolDispatchService` for the `DispatchableToolInfo` of the target resource type, then starts `Radoub/{AssemblyName}.exe --file <path>`.

### Testing Requirements

- [ ] **Create unit test project** - `ToolName.Tests/` with xUnit
- [ ] **Add smoke test** - Basic UI integration test using FlaUI
- [ ] **Add to run-tests.ps1** - Include tool in `-Tool` ValidateSet parameter
- [ ] **Test file launch** - Verify `--file` argument opens the file correctly

### UI Uniformity Checklist (Epic #959)

**Required for all tools** - ensures consistency, performance, and reliability:

| Category | Requirement | Reference |
|----------|-------------|-----------|
| **AboutWindow** | Use `AboutWindow.Create(AboutWindowConfig)` from Radoub.UI | Trebuchet |
| **Version Display** | Use shared `VersionHelper` (not hardcoded strings) | Parley/Utils/VersionHelper.cs |
| **Modal Dialogs** | Use `Show()` not `ShowDialog()` for info windows | CLAUDE.md UI/UX Guidelines |
| **Theme Brushes** | Use `BrushManager` for Success/Warning/Error colors | Radoub.UI/Services/BrushManager.cs |
| **Deferred Loading** | Heavy I/O in `Loaded`/`Opened` events, not constructor | Fence/Views/MainWindow.axaml.cs |
| **Progress Feedback** | Show status for operations >2 seconds | Fence palette loading pattern |
| **Caching** | Cache game data if applicable (feats, spells, items) | Fence/Services/PaletteCacheService.cs |
| **TLK Support** | Use shared ITlkService for localized strings | Radoub.UI/Services/ITlkService.cs |
| **Spell-Check** | Use Radoub.Dictionary for game-facing text | Parley/Manifest patterns |
| **Token Picker** | Right-click "All Tokens..." must open `Radoub.UI.Views.TokenSelectorWindow` (4 tabs: Standard / Highlight / Custom Tokens / Custom Colors). Route through `TokenInsertionHelper.OpenTokenWindow` — do NOT instantiate the older `TokenInsertionWindow` directly; it lacks the Custom Tokens tab (#2075). | Manifest.PropertyPanel, Relique post-#2075 |
| **Variables Grid** | Use shared `Radoub.UI.Controls.VariablesPanel` *(Sprint 3)* — do NOT fork `VariableViewModel`. | (shared control) |
| **Undo/Redo** | Wire shared `UndoRedoManager` + Ctrl+Z / Ctrl+Y; route mutating actions through `IUndoableCommand`. No disabled "Not yet implemented" menu stubs (#2231). | (TBD — pending epic #2231 reference impl) |

**Resource Browsers** (use shared implementations from Radoub.UI):

| Browser | Interface | Use When |
|---------|-----------|----------|
| ScriptBrowserWindow | IScriptBrowserContext | Tool needs script selection |
| SoundBrowserWindow | ISoundBrowserContext | Tool needs sound selection |
| PortraitBrowserWindow *(Sprint 1)* | IPortraitBrowserContext | Tool needs portrait selection |
| ModelPreviewGLControl | (control, not a browser) | Tool needs MDL preview — reuse `Radoub.UI/Controls/ModelPreviewGLControl.cs` |

**Anti-Patterns** (do NOT do these):

| Anti-Pattern | Correct Approach |
|--------------|------------------|
| Custom AboutWindow | Use `AboutWindow.Create()` |
| Hardcoded version "0.1.0-alpha" | Use `VersionHelper.GetVersion()` |
| `await dialog.ShowDialog(this)` for info | `dialog.Show(this)` |
| Duplicate brush methods in each file | Use `BrushManager.GetSuccessBrush()` |
| Load game data in constructor | Defer to `Loaded` event |
| Rebuild game data every launch | Cache to `~/Radoub/{Tool}/` |
| Custom TLK loading | Use shared ITlkService |
| Custom portrait browser | Use shared PortraitBrowserWindow |
| Forking `VariableViewModel` per tool | Use shared `Radoub.UI.Controls.VariablesPanel` |
| Tool-local placeables.2da reader | Use `Radoub.Formats.Services.PlaceableAppearanceService` |
| Browser shows ResRef-only sort/search | Override `IndexMetadataAsync` + `ReadSourceMetadataAsync` on the panel (see [File Browser Adoption](#file-browser-adoption-filebrowserpanelbase)) |
| Save flow doesn't refresh browser row | Call `BrowserSaveNotifier.NotifyAsync(panel, filePath)` after a successful save |
| Synchronous GFF parse inside `LoadFilesFromModuleAsync` | Defer Name/Tag extraction to the background `IndexMetadataAsync` pass |

### File Browser Adoption (FileBrowserPanelBase)

If the new tool edits a resource type with a localized Name and/or script Tag (UTI, UTM, UTC, UTP, etc.), the file browser panel must expose Name/Tag sort and search alongside ResRef. The shared `FileBrowserPanelBase` does the DataGrid, column headers, search box, sort comparators, background prefetch, and cancellation; the tool wires three virtual hooks plus one host-side save-notify call. Epic [#2186](https://github.com/LordOfMyatar/Radoub/issues/2186) (PRs [#2204](https://github.com/LordOfMyatar/Radoub/pull/2204) Sprint 1 infra, [#2208](https://github.com/LordOfMyatar/Radoub/pull/2208) Relique adoption, [#2209](https://github.com/LordOfMyatar/Radoub/pull/2209) Fence adoption).

**Lifecycle (base-class — do not duplicate)**: `FileBrowserPanelBase` owns the indexing `CancellationTokenSource` and disposes it via an `OnDetachedFromVisualTree` override that calls `CancelIndexing()` (#2262). Subclasses must **not** add their own detach handler for the indexing CTS, must not re-cancel in `Unloaded`, and must not hold a reference to the CTS. If a subclass needs to cancel its own background work, it must own a separate CTS and dispose it from its own detach handler — never reuse `_indexingCts`.

**Panel-side virtual hooks** (in the `*BrowserPanel : FileBrowserPanelBase` subclass):

| Hook | Required when | Purpose |
|------|---------------|---------|
| `ReadSourceMetadataAsync(byte[]) → (tag, name)` | Always (Name/Tag formats) | Pure parse of one resource's bytes → Tag + DisplayLabel. Same hook used by the copy-to-module flow — reuse the existing implementation. |
| `IndexMetadataAsync(entries, ct)` | Always (Name/Tag formats) | Background pass: for each entry where `MetadataLoaded == false`, read bytes → call `ReadSourceMetadataAsync` → set `Tag` + `DisplayLabel`. Yield (`await Task.Yield()`) every ~50 entries. Honor the cancellation token between batches. Always set `MetadataLoaded = true` (even on parse failure) so the loop doesn't retry forever. |
| `RefreshEntryMetadataAsync(entry)` | When tool supports save | Single-entry re-read called by the host after a save. No full reindex. |
| `SupportedSortModes` (property) | Only to suppress a mode | Default exposes all three (`ResRef`, `Name`, `Tag`). Override only when the format lacks Name or Tag (Parley's `DialogBrowserPanel` returns `[ResRef]` because DLG has no Name/Tag fields). |

**Worked example** (abbreviated from [ItemBrowserPanel.cs](../Radoub.UI/Radoub.UI/Controls/ItemBrowserPanel.cs)):

```csharp
public class ItemBrowserPanel : FileBrowserPanelBase, IBrowserRowRefresher
{
    public ISharedPaletteCacheService? PaletteCache { get; set; }

    protected override Task<(string tag, string name)> ReadSourceMetadataAsync(byte[] bytes)
    {
        try
        {
            var uti = Radoub.Formats.Uti.UtiReader.Read(bytes);
            return Task.FromResult(
                (uti.Tag ?? string.Empty,
                 uti.LocalizedName.GetDefault() ?? string.Empty));
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"ItemBrowserPanel.ReadSourceMetadataAsync: {ex.Message}");
            return Task.FromResult((string.Empty, string.Empty));
        }
    }

    protected override async Task IndexMetadataAsync(
        IReadOnlyList<FileBrowserEntry> entries,
        CancellationToken cancellationToken)
    {
        var paletteLookup = BuildPaletteLookup();
        int processed = 0;

        foreach (var entry in entries)
        {
            if (cancellationToken.IsCancellationRequested) return;
            if (entry.MetadataLoaded) { processed++; continue; }

            try
            {
                if (TryFillFromCache(entry, paletteLookup))
                {
                    entry.MetadataLoaded = true;
                }
                else
                {
                    var bytes = await ReadEntryBytesAsync(entry, cancellationToken);
                    if (bytes != null)
                    {
                        var (tag, name) = await ReadSourceMetadataAsync(bytes);
                        entry.Tag = tag;
                        entry.DisplayLabel = name;
                    }
                    entry.MetadataLoaded = true;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"ItemBrowserPanel.IndexMetadataAsync({entry.Name}): {ex.Message}");
                entry.MetadataLoaded = true; // don't retry forever
            }

            processed++;
            if (processed % 50 == 0) await Task.Yield();
        }
    }

    public override async Task RefreshEntryMetadataAsync(FileBrowserEntry entry)
    {
        var bytes = await ReadEntryBytesAsync(entry, CancellationToken.None);
        if (bytes == null) return;
        var (tag, name) = await ReadSourceMetadataAsync(bytes);
        entry.Tag = tag;
        entry.DisplayLabel = name;
        entry.MetadataLoaded = true;
    }

    // IBrowserRowRefresher — host calls this via BrowserSaveNotifier
    public Task RefreshRowAsync(string filePath)
    {
        var entry = FindEntryByFilePath(filePath);
        return entry == null ? Task.CompletedTask : RefreshEntryFromDiskAsync(entry);
    }
}
```

**Host-side wiring** (in the tool's `MainWindow`):

```csharp
// Lifecycle: wire the (optional) shared palette cache on the panel.
ItemBrowserPanel.PaletteCache = new SharedPaletteCacheService(/* see cache rules below */);

// FileOps: notify the browser after every successful save so the row's
// Tag/Name reflect the new values without a full reindex.
_ = Radoub.UI.Controls.BrowserSaveNotifier.NotifyAsync(ItemBrowserPanel, _currentFilePath);
```

Live references: [MainWindow.Lifecycle.cs:168](../Relique/Relique/Views/MainWindow.Lifecycle.cs#L168) (cache wiring), [MainWindow.FileOps.cs:135](../Relique/Relique/Views/MainWindow.FileOps.cs#L135) (post-save notify), [Fence MainWindow.axaml.cs:79](../Fence/Fence/Views/MainWindow.axaml.cs#L79) (per-resource cache instantiation).

**Per-resource cache directory pattern**:

`SharedPaletteCacheService` is keyed by source (BIF / Override / per-HAK) inside a single root directory. Different resource types (UTI / UTM / UTC / UTP) must use **separate root directories** so aggregated lookups don't return cross-type hits.

| Tool | Resource | Cache directory |
|------|----------|-----------------|
| Relique | UTI | `~/Radoub/Cache/ItemPalette/` (default constructor) |
| Fence | UTM | `~/Radoub/Cache/StorePalette/` (explicit path) |
| Quartermaster | UTC | `~/Radoub/Cache/CreaturePalette/` (planned, [#2201](https://github.com/LordOfMyatar/Radoub/issues/2201)) |
| Future placeable editor | UTP | `~/Radoub/Cache/PlaceablePalette/` |

Naming convention: `~/Radoub/Cache/{Resource}Palette/`. For non-UTI tools, pass the path explicitly:

```csharp
private readonly ISharedPaletteCacheService _palette =
    new SharedPaletteCacheService(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Radoub", "Cache", "StorePalette"));
```

A `PaletteCache` is optional — wire it only when HAK/BIF extraction is expensive enough that the cache pays for itself. Module-folder files are read directly on every indexing pass (cheap enough). When wired, populate the cache during the same archive scan that lists entries (see [StoreBrowserPanel.cs:568](../Radoub.UI/Radoub.UI/Controls/StoreBrowserPanel.cs#L568) for Fence's populate-during-scan flow).

**Host-side save plumbing**: `BrowserSaveNotifier.NotifyAsync` is null-safe on both the refresher and the path, so the call is safe to drop in early returns or on failed saves. The static helper exists so the post-save wire-up is unit-testable without a `MainWindow` — see [BrowserSaveNotifierTests.cs](../Radoub.UI/Radoub.UI.Tests/BrowserSaveNotifierTests.cs).

**Host-side browser collapse (REQUIRED)**: `FileBrowserPanelBase` only flips its own ◀/▶ button and raises `CollapsedChanged` — it does **not** hide itself. The host owns the actual collapse: subscribe to `CollapsedChanged` and zero the browser column's width (or the panel's `Width`) plus hide the splitter. Forgetting this is a silent bug — the toggle button (and any F4 shortcut) flips the arrow but nothing disappears. QM/Relique resize `OuterContentGrid.ColumnDefinitions[0]`; the minimal form is `browser.Width = collapsed ? 0 : <default>; splitter.IsVisible = !collapsed;`.

**Read-only archive preview + copy-to-module (Save As)**: a browser that lists HAK/BIF entries (base-game/HAK rows) must let the user open them. Archive entries have **no `FilePath`**, so the host's file-select handler must branch: detect `IsFromBif`/`IsFromHak`, extract bytes (expose a public `ExtractArchiveBytes(entry)` on the panel that delegates to the private archive extractor), load them read-only (`DocumentState.IsReadOnly = true`, `CurrentFilePath = null`), and bind the editor. A plain `if (string.IsNullOrEmpty(entry.FilePath)) return;` silently swallows every base-game selection. To let the user **copy a base-game resource into the module to edit it**, route Save (and Save on a read-only/never-saved document) through **Save As**: a `SaveFilePicker` writes a new file into the module, then clears read-only and adopts the new path. This is the standard "edit a base-game blueprint" flow (Relique `MainWindow.FileOps.SaveAsAsync`, QM `OnSaveAsClick`).

**Test seams**:

- `BrowserSortLogic.FilterAndSort` ([BrowserSortLogic.cs](../Radoub.UI/Radoub.UI/Controls/BrowserSortLogic.cs)) is a pure-static helper covering the comparator + filter behavior; mirror its test style in `BrowserSortLogicTests.cs` if extending sort semantics.
- `TryFillFromCache(entry, lookup)` on each panel is an `internal static` seam — write cache-hit/miss tests against it (see `ItemBrowserPanelIndexingTests.cs`, `StoreBrowserPanelIndexingTests.cs`).
- `BuildPaletteItemFromUtm` (`StoreBrowserPanel.cs:335`) shows the pure-logic pattern for converting raw bytes → `SharedPaletteCacheItem` so the populator path is testable independent of disk I/O.

[↑ TOC](#table-of-contents)

### Post-Implementation Audit

After a new tool's initial implementation is complete (before first release), run a UI uniformity audit against the checklist table above:

- [ ] **Run UI uniformity audit** — verify all criteria in the UI Uniformity Checklist table pass
- [ ] **Verify New-resource flow** — `File → New` creates a blank resource (NOT just Save-As-copy of an existing file). A tool that can only edit pre-existing files fails this check (#2367).
- [ ] **Verify Recent Files end-to-end** — open a file, relaunch Trebuchet, confirm it appears under the tool's card. Empty dropdown = the #2247 regression (missing MRU persistence and/or `ToolRecentFilesService` registration).
- [ ] **Verify no dead controls** — every menu item and button performs an action or is removed. An event-raised-but-unhandled control (handler-less `Click`, no `+=` subscription) is a no-op stub and violates #2231 (#2369).
- [ ] **Run `run-tests.ps1 -Tool [ToolName]`** — confirm clean pass (privacy, tech-debt, unit tests)
- [ ] **Verify Trebuchet integration** — tool registered in ToolLauncherService, `--file` argument works
- [ ] **Add FlaUI smoke test** — basic launch/close test in `Radoub.IntegrationTests/[ToolName]/` (requires CI infrastructure from #1905)

> These three audit rows exist because Reliquary passed a 12/12 UI-uniformity audit while missing the New-resource flow, Recent Files, and with two dead buttons — the checklist didn't catch what the prose required. Audit against behavior, not just the uniformity table.

### Versioning (NBGV)

**Version Source**: Nerdbank.GitVersioning (NBGV) via per-tool `version.json` files.

**How it works**:
- Each tool has a `version.json` in its directory (e.g., `Parley/version.json`)
- NBGV computes the patch version from git commit height since `version.json` last changed its major.minor
- No version properties in `.csproj` files — NBGV injects them at build time via MSBuild
- `VersionHelper.GetVersion()` reads the NBGV-injected `InformationalVersion` at runtime

**Version file structure**:
```json
{
  "inherit": true,
  "version": "0.2-alpha",
  "pathFilters": ["."]
}
```

- `inherit: true` — inherits `publicReleaseRefSpec`, `cloudBuild`, etc. from root `version.json`
- `pathFilters: ["."]` — only commits touching this tool's directory increment the patch version
- Root `version.json` has shared settings (public release ref spec, cloud build config)

**New tools**: Create a `version.json` in the tool directory with `"version": "0.1-alpha"`.

**Version bumps**: To bump a tool's minor version, edit its `version.json` and change the `"version"` field (e.g., `"0.2-alpha"` → `"0.3-alpha"`). The patch resets to 0 automatically.

**Checking computed versions**:
```bash
dotnet nbgv get-version --project [ToolDir]
```

**Semantic Versioning Rules**:
| Version | When to Bump |
|---------|--------------|
| **Major** (1.0.0) | Breaking changes, major rewrites, stable release |
| **Minor** (0.2.0) | New features, significant enhancements — edit `version.json` |
| **Patch** (0.1.1) | Automatic — increments with each commit to the tool's directory |
| **Prerelease** (-alpha, -beta) | Set in `version.json` `"version"` field suffix |

**Alpha vs Beta vs Stable**:
- `alpha`: Active development, features incomplete, may have bugs
- `beta`: Feature complete, testing phase, API may change
- (no suffix): Stable release, production ready

### Common Mistakes to Avoid

| Mistake | Correct Pattern |
|---------|-----------------|
| Hardcoding game data (races, classes, etc.) | Use IGameDataService + 2DA files |
| Custom theme implementation | Use Radoub.UI ThemeManager |
| Modal dialogs for messages | Use non-modal or toast notifications |
| Settings in app folder | Store in `~/Radoub/ToolName/settings.json` |
| Missing SafeMode support | Always implement `--safemode` flag |
| Skipping unit tests | Create ToolName.Tests from day 1 |
| Hardcoding version in .csproj | Use NBGV `version.json` — no version properties in .csproj |
| `<AssemblyName>` differs from tool name (built exe is `Foo.exe` for tool "Bar") | Set `<AssemblyName>ToolName</AssemblyName>` to match the folder/tool name. Sibling discovery, release workflow, cross-tool launchers, and `ToolDispatchService` all assume `Radoub/ToolName.exe` — divergence requires a settings-path migration to fix (#2080). |
| Destructive file op with no backup (delete / overwrite / rename) | Back up via `Radoub.UI.Services.Search.BackupService` first. A bare `File.Delete` / `File.WriteAllBytes` over a user file is unrecoverable (#2347). See **Backups Before Destructive File Operations**. |

[↑ TOC](#table-of-contents)

---

## UI/UX Detailed Patterns

These are the detailed UI/UX reference tables for tool implementation. The session-relevant dialog/window rules (non-modal `Show()` over `ShowDialog()`) stay in the root `CLAUDE.md`.

[↑ TOC](#table-of-contents)

### Button Labeling Standards

- **Browse buttons**: Use "Browse..." or "..." (ellipsis indicates dialog will open)
- **Action buttons**: Use verb describing action (e.g., "Save", "Export", "Add")
- **Position**: Browse buttons should be immediately adjacent to their associated field, not right-aligned away from it
- **Consistency**: All tools must follow the same button labeling patterns

See [#868](https://github.com/LordOfMyatar/Radoub/issues/868) for standardization audit.

### Progress Indicator Standards

Operations should provide appropriate feedback based on duration:

| Duration | Feedback Type | Implementation |
|----------|---------------|----------------|
| <2 seconds | Status bar text | Update `StatusText` property |
| 2-5 seconds | Indeterminate progress | `StatusBarControl.ShowProgress = true` |
| >5 seconds | Progress with status updates | Update status text periodically |

**Pattern Examples**:
```csharp
// Brief operation - status text only
StatusText = "Saving...";
await SaveFileAsync();
StatusText = "Ready";

// Medium operation - indeterminate progress
ShowProgress = true;
StatusText = "Loading palette...";
await LoadPaletteAsync();
ShowProgress = false;
StatusText = "Ready";

// Long operation - progress with updates
ShowProgress = true;
for (int i = 0; i < items.Count; i++)
{
    StatusText = $"Processing {i + 1} of {items.Count}...";
    await ProcessItemAsync(items[i]);
}
ShowProgress = false;
StatusText = $"Processed {items.Count} items";
```

**Guidelines**:
- Always reset status bar to "Ready" when operation completes
- Use `Radoub.UI.Controls.StatusBarControl` for consistent styling
- For cancellable operations, include cancel button or Escape key handling
- Prefer `IsIndeterminate=true` when total progress is unknown

### Deferred Loading Patterns

(Epic #959, Sprint 4)

**Lifecycle Event Responsibilities**:

| Event | Purpose | Example Operations |
|-------|---------|-------------------|
| Constructor | Light synchronous setup | `InitializeComponent()`, create services, register events |
| Loaded | UI restoration | Restore panel sizes, debug settings, combo box selections |
| Opened | Heavy async work | Initialize caches, load palettes, handle startup file |

**Recommended Startup Pattern**:
```csharp
public MainWindow()
{
    InitializeComponent();
    DataContext = _viewModel;

    // Fast services only (no I/O)
    _gameDataService = new GameDataService();
    _displayService = new CreatureDisplayService(_gameDataService);

    InitializeUI();  // Fast UI setup
    RestoreWindowPosition();

    Loaded += OnWindowLoaded;
    Opened += OnWindowOpened;
}

private void OnWindowLoaded(object? sender, RoutedEventArgs e)
{
    RestoreDebugSettings();
    RestorePanelSizes();
}

private async void OnWindowOpened(object? sender, EventArgs e)
{
    Opened -= OnWindowOpened;  // Unsubscribe immediately

    UpdateStatus("Loading game data...");
    await InitializeCachesAsync();  // Sequential async
    _ = StartPaletteLoadAsync();     // Fire-and-forget
    await HandleStartupFileAsync();
    UpdateStatus("Ready");
}
```

**Fire-and-Forget Pattern**:
```csharp
// Use underscore to suppress compiler warning
_ = LoadBaseItemTypesAsync();
_ = ScanCreaturesAsync(token);
```

**Cancellation Token Pattern**:
```csharp
private CancellationTokenSource? _loadCts;

private void StartLoad()
{
    _loadCts?.Cancel();  // Cancel previous operation
    _loadCts = new CancellationTokenSource();
    _ = LoadAsync(_loadCts.Token);
}

private async Task LoadAsync(CancellationToken token)
{
    try
    {
        await DoWorkAsync(token);
    }
    catch (OperationCanceledException)
    {
        UnifiedLogger.LogApplication(LogLevel.DEBUG, "Load cancelled");
    }
}
```

**Anti-Patterns to Avoid**:

| Anti-Pattern | Problem | Fix |
|--------------|---------|-----|
| `await` in constructor | Blocks window show | Move to `Opened` event |
| No cancellation token | Orphaned tasks | Use `CancellationTokenSource` |
| Status not reset | "Loading..." stuck | Always reset to "Ready" |
| No error handling | Silent failures | Catch and log in async methods |

[↑ TOC](#table-of-contents)

---

## Aurora File Format Implementation

**Reference Strategy** for implementing Aurora Engine file parsers (GFF, ERF, KEY, BIF, TLK, 2DA, SSF):

**PRIMARY Reference**: [neverwinter.nim](https://github.com/niv/neverwinter.nim) (MIT License)

**SECONDARY Reference**: BioWare Aurora Specifications (Wiki or `Documentation/BioWare_Original_PDFs/`)
- Markdown conversions available in the Wiki: https://github.com/LordOfMyatar/Radoub/wiki
- Original PDFs in `Documentation/BioWare_Original_PDFs/`
- Specs are 20 years old and may not reflect modern edge cases
- Good for "why" questions, not "how to handle X" questions

**Implementation Approach**:
1. Write parsers in C# (native to Radoub toolset)
2. Follow neverwinter.nim's edge case handling and validation patterns

[↑ TOC](#table-of-contents)
