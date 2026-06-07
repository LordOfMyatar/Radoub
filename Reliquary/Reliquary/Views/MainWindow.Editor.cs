using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Utp;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using Radoub.UI.Services.Search;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;
using PlaceableEditor.Commands;
using PlaceableEditor.Services;
using PlaceableEditor.ViewModels;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Editor integration for Reliquary's MainWindow (#2295): loads a UTP into a
/// <see cref="PlaceableViewModel"/>, wires the two content panels' DataContext, and routes every
/// mutation through a shared <see cref="UndoRedoManager"/> (Ctrl+Z/Ctrl+Y). The VariablesPanel is
/// undo-agnostic (#2293) — its Add/Delete events become <see cref="AddVariableCommand"/> /
/// <see cref="RemoveVariableCommand"/> here.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();
    private PlaceableViewModel? _placeable;
    private bool _editorWired;

    /// <summary>Connect the undo manager + panel events once (called from construction).</summary>
    private void WireEditor()
    {
        if (_editorWired) return;
        _editorWired = true;

        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        // Command-based edits (appearance, scripts, variables, inventory) flow through the undo
        // manager; binding-based edits (identity/text fields) flow through the VM. Mark dirty on
        // both so the title bar + save prompt reflect any change. MarkDirty no-ops while loading.
        _undo.StateChanged += (_, _) => MarkDirty();
        RefreshUndoMenu();

        var behavior = this.FindControl<BehaviorPanel>("BehaviorPanel");
        if (behavior != null)
        {
            behavior.Variables.AddRequested += OnVariableAddRequested;
            behavior.Variables.DeleteRequested += OnVariableDeleteRequested;
            behavior.ScriptBrowseRequested += OnScriptBrowseRequested;
            behavior.ScriptEditRequested += OnScriptEditRequested;
            behavior.SaveScriptSetRequested += OnSaveScriptSetRequested;
            behavior.LoadScriptSetRequested += OnLoadScriptSetRequested;
            behavior.EditConversationRequested += OnEditConversationRequested;
            behavior.ConversationBrowseRequested += OnConversationBrowseRequested;
            behavior.FactionChanged += OnFactionSelected;
        }
    }

    /// <summary>File → New (#2367): prompt to discard, then bind a blank placeable as an unsaved document.</summary>
    private async void OnNewClick(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        try
        {
            _isLoading = true; // suppress dirty marking while panels rebind to the new VM
            _currentFilePath = null;          // unsaved — first Save routes through Save As
            _documentState.IsReadOnly = false;
            BindPlaceable(PlaceableViewModel.NewPlaceable());
            UpdateTitle(); // BindPlaceable's ClearDirty is a no-op when already clean, so refresh the title
            UpdateStatus("New placeable — fill in name/tag, then Save.");
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Load a UTP file into the editor, wrap it in a VM, and bind the panels.</summary>
    private void LoadPlaceable(string filePath)
    {
        try
        {
            _isLoading = true; // suppress dirty marking while panels rebind to the new VM
            var utp = UtpReader.Read(filePath);
            _currentFilePath = filePath;
            _documentState.IsReadOnly = false;
            BindPlaceable(new PlaceableViewModel(utp));
            UpdateTitle(); // BindPlaceable's ClearDirty is a no-op when already clean, so refresh the title explicitly
            AddRecentFile(filePath);
            UpdateStatus($"Loaded {Path.GetFileName(filePath)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: failed to load {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
            UpdateStatus($"Could not load {Path.GetFileName(filePath)}: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>
    /// Load a UTP from raw bytes (HAK/BIF archive entry) as a read-only preview — no file path, so
    /// Save is a no-op until the user does Save As (future). Mirrors Relique's archive preview.
    /// </summary>
    private void LoadPlaceableFromBytes(byte[] bytes, string name)
    {
        try
        {
            _isLoading = true;
            var utp = UtpReader.Read(bytes);
            _currentFilePath = null;          // no backing file — read-only resource
            _documentState.IsReadOnly = true;
            BindPlaceable(new PlaceableViewModel(utp));
            UpdateTitle(); // surface the [Read-Only] marker (ClearDirty is a no-op when already clean)
            UpdateStatus($"Base-game placeable (read-only): {name}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Reliquary: failed to load archive {name}: {ex.Message}");
            UpdateStatus($"Could not load {name}: {ex.Message}");
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Bind a freshly-loaded placeable VM to all panels + reset undo/dirty. Caller sets _isLoading.</summary>
    private void BindPlaceable(PlaceableViewModel placeable)
    {
        _placeable = placeable;

        var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
        var behavior = this.FindControl<BehaviorPanel>("BehaviorPanel");
        var text = this.FindControl<TextPanel>("TextPanel");
        if (identity != null) identity.DataContext = _placeable;
        if (text != null) text.DataContext = _placeable;
        if (behavior != null)
        {
            behavior.DataContext = _placeable;
            behavior.Variables.Variables = new System.Collections.ObjectModel.ObservableCollection<VariableViewModel>(
                _placeable.VarTable.Select(VariableViewModel.FromVariable));
        }

        PopulateAppearanceAndPreview(); // appearance combo + 3D model (when game data configured)
        PopulateFactionCombo();         // faction combo from the module's repute.fac (#2354)
        PopulatePaletteCategoryCombo(); // category combo from placeablepal.itp (#2416)
        RefreshInventory();             // backpack + palette (visible only when Has Inventory)

        TrackPlaceableEdits(_placeable); // any field/variable/inventory edit marks the document dirty
        _undo.Clear(); // fresh history per file
        RefreshUndoMenu();
        _documentState.ClearDirty();
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e) => await SavePlaceableAsync();

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e) => await SaveAsPlaceableAsync();

    /// <summary>
    /// Save to the current file. Read-only (base-game/HAK preview) or never-saved documents route
    /// to Save As so the user picks a module destination — this is how a base-game placeable is
    /// copied into the module for editing. Returns true on a successful write.
    /// </summary>
    private async Task<bool> SavePlaceableAsync()
    {
        if (_placeable is null)
        {
            UpdateStatus("Nothing to save — open a placeable first.");
            return false;
        }

        // No backing file (base-game/HAK preview) → Save As to choose a destination.
        if (string.IsNullOrEmpty(_currentFilePath) || _documentState.IsReadOnly)
            return await SaveAsPlaceableAsync();

        return WritePlaceable(_currentFilePath);
    }

    /// <summary>Prompt for a destination and save there (copies a read-only preview into the module).</summary>
    private async Task<bool> SaveAsPlaceableAsync()
    {
        if (_placeable is null)
        {
            UpdateStatus("Nothing to save — open a placeable first.");
            return false;
        }

        var suggested = _placeable.TemplateResRef;
        if (string.IsNullOrEmpty(suggested))
            suggested = Path.GetFileNameWithoutExtension(_currentFilePath ?? "placeable");

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Placeable As",
            DefaultExtension = "utp",
            SuggestedFileName = suggested + ".utp",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Placeable Blueprint") { Patterns = new[] { "*.utp" } },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return false;

        if (!WritePlaceable(path)) return false;

        // The saved copy is now the editable document.
        _currentFilePath = path;
        _documentState.IsReadOnly = false;
        UpdateTitle();
        return true;
    }

    /// <summary>Write the model to disk + clear dirty + notify the browser. Returns false on failure.</summary>
    private bool WritePlaceable(string path)
    {
        if (_placeable is null) return false;
        try
        {
            var utp = _placeable.WriteToUtp();
            // Backstop: never write a damageable placeable with HP 0 (Aurora divide-by-zero, #2417).
            if (PlaceableEditor.Services.PlaceableDefaults.EnsureGameSafe(utp))
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    "Reliquary: backfilled HP on save (damageable placeable had HP 0) (#2417)");
            }
            UtpWriter.Write(utp, path);
            _documentState.ClearDirty();
            AddRecentFile(path);

            // Refresh the browser row's Tag/Name in place, or add+select the row if the saved file
            // is new to the list (Save As of a New placeable). One shared helper handles both (#2413).
            var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
            _ = Radoub.UI.Controls.BrowserSaveNotifier.NotifyOrAddAsync(browser, path);

            UpdateStatus($"Saved {Path.GetFileName(path)}");
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Reliquary: saved {UnifiedLogger.SanitizePath(path)}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: save failed for {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
            UpdateStatus($"Save failed: {ex.Message}");
            return false;
        }
    }

    // --- Recent Files (MRU) ---
    // Persistence is inherited from BaseToolSettingsService (RecentFiles / AddRecentFile);
    // Trebuchet reads ~/Radoub/Reliquary/ReliquarySettings.json via ToolRecentFilesService (#2368).

    /// <summary>Record a file in the MRU and refresh the Open Recent submenu.</summary>
    private void AddRecentFile(string filePath)
    {
        PlaceableEditor.Services.SettingsService.Instance.AddRecentFile(filePath);
        PopulateRecentFiles();
    }

    /// <summary>Rebuild the Open Recent submenu from the persisted MRU list.</summary>
    private void PopulateRecentFiles()
    {
        var menu = this.FindControl<MenuItem>("RecentFilesMenu");
        if (menu == null) return;

        menu.Items.Clear();
        var recent = PlaceableEditor.Services.SettingsService.Instance.RecentFiles;

        if (recent.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = "(none)", IsEnabled = false });
            return;
        }

        foreach (var path in recent)
        {
            var item = new MenuItem { Header = Path.GetFileName(path), Tag = path };
            ToolTip.SetTip(item, UnifiedLogger.SanitizePath(path));
            item.Click += async (_, _) =>
            {
                if (!await ConfirmDiscardAsync()) return;
                LoadPlaceable((string)item.Tag!);
            };
            menu.Items.Add(item);
        }
    }

    // --- Undo / Redo ---

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _undo.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _undo.Redo();

    private void RefreshUndoMenu()
    {
        var undoItem = this.FindControl<MenuItem>("UndoMenuItem");
        var redoItem = this.FindControl<MenuItem>("RedoMenuItem");
        if (undoItem != null)
        {
            undoItem.IsEnabled = _undo.CanUndo;
            undoItem.Header = _undo.CanUndo ? $"_Undo {_undo.UndoDescription}" : "_Undo";
        }
        if (redoItem != null)
        {
            redoItem.IsEnabled = _undo.CanRedo;
            redoItem.Header = _undo.CanRedo ? $"_Redo {_undo.RedoDescription}" : "_Redo";
        }
    }

    // --- Variable mutations (undo-agnostic panel → undoable commands) ---

    private void OnVariableAddRequested(object? sender, VariableAddRequestedEventArgs e)
    {
        if (_placeable is null || sender is not VariablesPanel panel) return;

        var existing = panel.Variables.Select(v => v.Name);
        var vm = new VariableViewModel
        {
            Name = VariableViewModel.NextDefaultName(existing),
            Type = Radoub.Formats.Gff.VariableType.Int,
            ValueText = "0"
        };

        _undo.Execute(new AddVariableCommand(_placeable.VarTable, panel.Variables, vm));
        panel.SelectedVariable = vm;
        panel.FocusSelectedName();
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        if (_placeable is null || sender is not VariablesPanel panel) return;

        // Remove each selected variable as its own undoable step (newest index first keeps indices valid).
        foreach (var vm in e.Variables)
            _undo.Execute(new RemoveVariableCommand(_placeable.VarTable, panel.Variables, vm));
    }

    // --- Scripts ---

    private async void OnScriptBrowseRequested(object? sender, ScriptSlotViewModel slot)
    {
        var context = new PlaceableEditor.Services.ReliquaryScriptBrowserContext(_currentFilePath);
        var browser = new Radoub.UI.Views.ScriptBrowserWindow(context);
        var result = await browser.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(result)) return;

        _undo.Execute(new SetFieldCommand<string>(
            () => slot.ResRef, v => slot.ResRef = v, result, $"set {slot.EventName} script"));
    }

    private void OnScriptEditRequested(object? sender, ScriptSlotViewModel slot)
    {
        if (string.IsNullOrWhiteSpace(slot.ResRef))
        {
            UpdateStatus($"No script assigned to {slot.EventName} — assign one first.");
            return;
        }

        var fileDir = string.IsNullOrEmpty(_currentFilePath) ? null : Path.GetDirectoryName(_currentFilePath);
        var moduleDir = GetModuleWorkingDirectory();
        if (!ExternalEditorService.OpenScript(slot.ResRef, fileDir, moduleDir))
            UpdateStatus($"Could not open {slot.ResRef}.nss — not found near the placeable or module.");
    }

    // --- Script sets (save/load a reusable preset, #2369) ---

    private async void OnSaveScriptSetRequested(object? sender, EventArgs e)
    {
        if (_placeable is null)
        {
            UpdateStatus("Open or create a placeable before saving a script set.");
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Script Set",
            DefaultExtension = "txt",
            SuggestedFileName = "placeable-scripts.txt",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Script Set") { Patterns = new[] { "*.txt" } }
            }
        });

        var path = file?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            File.WriteAllBytes(path, ScriptSetService.Serialize(_placeable.Scripts));
            UpdateStatus($"Saved script set to {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: script-set save failed for {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
            UpdateStatus($"Could not save script set: {ex.Message}");
        }
    }

    private async void OnLoadScriptSetRequested(object? sender, EventArgs e)
    {
        if (_placeable is null)
        {
            UpdateStatus("Open or create a placeable before loading a script set.");
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Load Script Set",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Script Set") { Patterns = new[] { "*.txt" } }
            }
        });

        var path = files.Count > 0 ? files[0].Path.LocalPath : null;
        if (string.IsNullOrEmpty(path)) return;

        IReadOnlyDictionary<string, string> set;
        try
        {
            set = ScriptSetService.Parse(File.ReadAllBytes(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: script-set load failed for {UnifiedLogger.SanitizePath(path)}: {ex.Message}");
            UpdateStatus($"Could not load script set: {ex.Message}");
            return;
        }

        // Snapshot the current 13 slots so the whole apply is a single undo step.
        var before = _placeable.Scripts.ToDictionary(s => s.EventName, s => s.ResRef);
        var slots = _placeable.Scripts;

        _undo.Execute(new RelayUndoableCommand(
            () => { ScriptSetService.Apply(slots, set); },
            () => { ScriptSetService.Apply(slots, before); },
            "load script set"));

        UpdateStatus($"Loaded script set from {Path.GetFileName(path)}");
    }

    // --- Conversation (cross-tool dispatch to Parley, Sprint 7 #2297) ---

    /// <summary>
    /// Resolve the placeable's Conversation ResRef to a .dlg near the file/module and open it in Parley
    /// via the shared <see cref="ToolDispatchService"/>. Reports status when blank, unresolved, or the
    /// launch fails (e.g. Parley not deployed alongside Reliquary).
    /// </summary>
    private void OnEditConversationRequested(object? sender, EventArgs e)
    {
        var resRef = _placeable?.Conversation;
        if (string.IsNullOrWhiteSpace(resRef))
        {
            UpdateStatus("No conversation set — enter a DLG resref first.");
            return;
        }

        var fileDir = string.IsNullOrEmpty(_currentFilePath) ? null : Path.GetDirectoryName(_currentFilePath);
        var moduleDir = GetModuleWorkingDirectory();
        var dlgPath = ExternalEditorService.ResolveResourcePath(resRef, ".dlg", fileDir, moduleDir);
        if (dlgPath is null)
        {
            UpdateStatus($"Could not open {resRef}.dlg — not found near the placeable or module.");
            return;
        }

        if (!new ToolDispatchService().LaunchTool(ResourceTypes.Dlg, dlgPath))
            UpdateStatus($"Could not launch Parley for {resRef}.dlg — Parley may not be installed alongside Reliquary.");
        else
            UpdateStatus($"Opening {resRef}.dlg in Parley…");
    }

    /// <summary>
    /// Open the shared DialogBrowserWindow and set the Conversation ResRef from the selection (#2373).
    /// Routes the change through undo (SetFieldCommand), mirroring the script-slot browse.
    /// </summary>
    private async void OnConversationBrowseRequested(object? sender, EventArgs e)
    {
        if (_placeable is null) return;

        var context = new PlaceableEditor.Services.ReliquaryScriptBrowserContext(_currentFilePath, _gameData);
        var browser = new Radoub.UI.Views.DialogBrowserWindow(context);
        var result = await browser.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(result)) return;

        _undo.Execute(new SetFieldCommand<string>(
            () => _placeable.Conversation, v => _placeable.Conversation = v, result, "set conversation"));
    }
}
