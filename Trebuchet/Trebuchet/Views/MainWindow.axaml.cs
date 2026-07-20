using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using RadoubLauncher.Services;
using RadoubLauncher.ViewModels;

namespace RadoubLauncher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;
    private PaletteCacheWarmupService? _paletteCacheWarmup;
    private CancellationTokenSource? _appShutdownCts;
    private TabControl? _workspaceTabs;
    private ModuleEditorViewModel? _moduleEditorVm;

    // Save-on-exit guard (#2453): once the user has chosen Save/Discard in the
    // unsaved-changes prompt, the re-issued Close() must not prompt again.
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainWindowViewModel();
        _viewModel.SetParentWindow(this);
        DataContext = _viewModel;

        // Initialize the embedded module editor panel
        var moduleEditorPanel = this.FindControl<Controls.ModuleEditorPanel>("ModuleEditorPanel");
        if (moduleEditorPanel != null)
        {
            _moduleEditorVm = new ModuleEditorViewModel();
            moduleEditorPanel.Initialize(_moduleEditorVm, this);
            _viewModel.SetModuleEditorViewModel(_moduleEditorVm);

            // Wire palette cache pre-warming (#1633)
            _appShutdownCts = new CancellationTokenSource();
            var cacheService = new SharedPaletteCacheService();
            var hakScanner = new HakPaletteScannerService();
            _paletteCacheWarmup = new PaletteCacheWarmupService(cacheService, hakScanner);

            _moduleEditorVm.GameDataServiceInitialized += OnGameDataServiceInitialized;
            _moduleEditorVm.ModuleLoaded += OnModuleLoaded;
        }

        // Initialize the embedded faction editor panel
        var factionEditorPanel = this.FindControl<Controls.FactionEditorPanel>("FactionEditorPanel");
        if (factionEditorPanel != null)
        {
            var factionEditorVm = new FactionEditorViewModel();
            factionEditorPanel.Initialize(factionEditorVm, this);
            _viewModel.SetFactionEditorViewModel(factionEditorVm);
        }

        // Initialize the launch & test panel
        var launchTestPanel = this.FindControl<Controls.LaunchTestPanel>("LaunchTestPanel");
        launchTestPanel?.Initialize(_viewModel);

        // Initialize the Marlinspike search & replace panel
        var marlinspikePanel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
        if (marlinspikePanel != null)
        {
            var marlinspikeVm = new MarlinspikePanelViewModel();
            marlinspikePanel.Initialize(marlinspikeVm, _viewModel, this);
        }

        // Initialize the ITP palette editor panel (#2477)
        var palettePanel = this.FindControl<Controls.PaletteEditorPanel>("PaletteEditorPanel");
        palettePanel?.Initialize(this);

        // Restore window position from settings
        RestoreWindowState();

        // Keyboard navigation (Ctrl+1/2/3 tab switching)
        KeyDown += OnKeyDown;

        // Tab change updates title and status bar context
        _workspaceTabs = this.FindControl<TabControl>("WorkspaceTabs");
        if (_workspaceTabs != null)
        {
            _workspaceTabs.SelectionChanged += OnWorkspaceTabChanged;
        }

        // Save window state on close
        Closing += OnWindowClosing;

        // Update module name color when module changes (#1003)
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, System.EventArgs e)
    {
        Opened -= OnWindowOpened;

        if (_viewModel != null)
            UpdateModuleNameColor(_viewModel);

        UpdateWindowTitle();

        // Auto-load module if one is already selected
        var moduleEditorPanel = this.FindControl<Controls.ModuleEditorPanel>("ModuleEditorPanel");
        if (moduleEditorPanel != null && _viewModel?.HasModule == true)
        {
            await moduleEditorPanel.LoadModuleAsync();
        }

        // Auto-load factions if a module is already selected
        var factionEditorPanel = this.FindControl<Controls.FactionEditorPanel>("FactionEditorPanel");
        if (factionEditorPanel != null && _viewModel?.HasModule == true)
        {
            await factionEditorPanel.LoadFacFileAsync();
        }

        // --settings flag: auto-open settings window
        if (Program.OpenSettingsOnStartup)
        {
            _viewModel?.OpenSettingsCommand.Execute(null);
        }

        // Startup housekeeping, off the first-paint path (#2647)
        Radoub.UI.Services.StartupCleanupCoordinator.RunDeferredCleanup(
            SettingsService.Instance.LogRetentionSessions,
            Radoub.Formats.Settings.RadoubSettings.Instance.BackupRetentionDays);
    }

    private void RestoreWindowState()
    {
        Radoub.UI.Services.WindowPositionHelper.Restore(this, SettingsService.Instance, validateBounds: true);
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        // Save-on-exit guard (#2453): prompt before discarding unsaved editor edits.
        // Avalonia's Closing handler can't await, so cancel the close now, run the
        // dialog, then re-issue Close() with a confirmed guard.
        if (!_closeConfirmed && _viewModel?.HasAnyUnsavedEditorChanges == true)
        {
            e.Cancel = true;
            await HandleUnsavedChangesOnCloseAsync();
            return;
        }

        SaveWindowState();

        // Unsubscribe window-level event handlers (#2034 round 3)
        KeyDown -= OnKeyDown;
        Closing -= OnWindowClosing;
        if (_workspaceTabs != null)
            _workspaceTabs.SelectionChanged -= OnWorkspaceTabChanged;
        if (_viewModel != null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_moduleEditorVm != null)
        {
            _moduleEditorVm.GameDataServiceInitialized -= OnGameDataServiceInitialized;
            _moduleEditorVm.ModuleLoaded -= OnModuleLoaded;
        }

        // Cancel palette cache warm-up operations (#1633)
        _paletteCacheWarmup?.CancelAll();
        _appShutdownCts?.Cancel();
        _paletteCacheWarmup?.Dispose();
        _appShutdownCts?.Dispose();

        (_viewModel)?.Cleanup();
    }

    /// <summary>
    /// Run the Save / Discard / Cancel prompt and act on the choice (#2453).
    /// On Save → persist dirty editors then close; on Discard → close; on Cancel
    /// → leave the window open. The actual close is re-issued via <see cref="Window.Close()"/>
    /// with <see cref="_closeConfirmed"/> set so the guard does not re-prompt.
    /// </summary>
    private async System.Threading.Tasks.Task HandleUnsavedChangesOnCloseAsync()
    {
        if (_viewModel == null) return;

        var message = string.IsNullOrEmpty(_viewModel.BuildWarningText)
            ? "You have unsaved changes. Save before closing?"
            : _viewModel.BuildWarningText + ". Save before closing?";

        var dialog = new UnsavedChangesDialog(message);
        await dialog.ShowDialog(this);

        var action = CloseGuard.Resolve(dialog.Result);
        switch (action)
        {
            case CloseAction.Abort:
                return; // Cancel — keep the window open.

            case CloseAction.SaveThenProceed:
                var saved = await _viewModel.SaveDirtyEditorsAsync();
                if (!saved)
                    return; // A save failed/left the editor dirty — abort the close.
                break;

            case CloseAction.Proceed:
                break; // Discard — fall through and close.
        }

        _closeConfirmed = true;
        Close();
    }

    private void OnGameDataServiceInitialized(object? sender, System.EventArgs e)
    {
        if (_moduleEditorVm?.GameDataService?.IsConfigured == true && _appShutdownCts != null)
        {
            _ = _paletteCacheWarmup!.WarmBifCacheAsync(
                _moduleEditorVm.GameDataService, _appShutdownCts.Token);
        }
    }

    private void OnModuleLoaded(object? sender, System.EventArgs e)
    {
        if (_moduleEditorVm?.GameDataService?.IsConfigured == true &&
            _moduleEditorVm.ModuleDirectory != null &&
            _appShutdownCts != null)
        {
            var hakSearchPaths = RadoubSettings.Instance.GetAllHakSearchPaths();
            _ = _paletteCacheWarmup!.WarmModuleHakCacheAsync(
                _moduleEditorVm.GameDataService,
                _moduleEditorVm.ModuleDirectory,
                hakSearchPaths,
                _appShutdownCts.Token);
        }
    }

    private void SaveWindowState()
    {
        Radoub.UI.Services.WindowPositionHelper.Save(this, SettingsService.Instance);
    }

    #region Module Name Color (#1003)

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.CurrentModuleName) && sender is MainWindowViewModel vm)
        {
            UpdateModuleNameColor(vm);
            UpdateWindowTitle();

            // Clear Marlinspike results when module changes
            var marlinspikePanel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
            marlinspikePanel?.OnModuleChanged();

            // Reload the palette editor against the new module (#2477)
            var palettePanel = this.FindControl<Controls.PaletteEditorPanel>("PaletteEditorPanel");
            palettePanel?.OnModuleChanged();
        }

        if (e.PropertyName == nameof(MainWindowViewModel.HasModule))
            UpdateWindowTitle();
    }

    private void UpdateModuleNameColor(MainWindowViewModel vm)
    {
        var moduleNameText = this.FindControl<TextBlock>("ModuleNameText");
        if (moduleNameText == null) return;

        var hasModule = !string.IsNullOrEmpty(Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath);
        moduleNameText.Foreground = hasModule
            ? BrushManager.GetInfoBrush(this)
            : BrushManager.GetWarningBrush(this);
    }

    #endregion

    #region Window Title & Status Context

    private void OnWorkspaceTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not TabControl tabControl) return;
        UpdateWindowTitle(tabControl);
    }

    private void UpdateWindowTitle(TabControl? tabControl = null)
    {
        tabControl ??= this.FindControl<TabControl>("WorkspaceTabs");

        var moduleName = _viewModel?.CurrentModuleName ?? "(No module)";
        var baseTitle = "Trebuchet";

        if (tabControl?.SelectedItem is TabItem selectedTab && _viewModel?.HasModule == true)
        {
            var tabName = selectedTab.Header?.ToString() ?? "";
            Title = $"{baseTitle} - {moduleName} [{tabName}]";
        }
        else if (_viewModel?.HasModule == true)
        {
            Title = $"{baseTitle} - {moduleName}";
        }
        else
        {
            // #1572: idle title is the bare tool name, matching every other tool.
            // The module name and tab remain when a module is open — Trebuchet is a
            // hub, so the tab is the equivalent of a filename here.
            Title = baseTitle;
        }
    }

    #endregion

    #region Keyboard Navigation

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var tabControl = this.FindControl<TabControl>("WorkspaceTabs");
        if (tabControl == null || !tabControl.IsVisible) return;

        // Ctrl+Shift+F: Switch to Marlinspike tab and focus search box
        if (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.F)
        {
            tabControl.SelectedIndex = 3; // Marlinspike
            var panel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
            panel?.FocusSearchBox();
            e.Handled = true;
            return;
        }

        // Ctrl+1/2/3/4 switches workspace tabs
        if (e.KeyModifiers == KeyModifiers.Control)
        {
            int? tabIndex = e.Key switch
            {
                Key.D1 => 0, // Module
                Key.D2 => 1, // Factions
                Key.D3 => 2, // Build & Test
                Key.D4 => 3, // Marlinspike
                Key.D5 => 4, // Palette
                _ => null
            };

            if (tabIndex.HasValue && tabIndex.Value < tabControl.ItemCount)
            {
                tabControl.SelectedIndex = tabIndex.Value;
                e.Handled = true;
            }
        }
    }

    #endregion

    #region Title Bar Handlers

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    #endregion

    #region ERF Import

    private void OnImportErfClick(object? sender, RoutedEventArgs e)
    {
        var modulePath = RadoubLauncher.Services.ModulePathHelper.GetWorkingDirectory(
            Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(modulePath)) return;

        var window = new ErfImportWindow();
        window.Initialize(modulePath);
        window.ImportSucceeded += (_, _) =>
        {
            // #2072 — module working directory contents just changed; invalidate
            // Marlinspike's cached search/item-resolution services so the next
            // search picks up the imported files.
            var panel = this.FindControl<Controls.MarlinspikePanel>("MarlinspikePanel");
            panel?.InvalidateSearchIndex();
        };
        window.Show(this);
    }

    #endregion

    #region New ERF

    private async void OnNewErfClick(object? sender, RoutedEventArgs e)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var file = await storage.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "New ERF Archive",
            DefaultExtension = "erf",
            SuggestedFileName = "newarchive.erf",
            FileTypeChoices = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("ERF Archives") { Patterns = new[] { "*.erf" } }
            }
        });

        if (file is null) return;

        var path = file.Path.LocalPath;

        // Validate the chosen filename against Aurora constraints before writing,
        // so the user gets a clear message instead of an unusable in-game file (#2268).
        var stem = System.IO.Path.GetFileNameWithoutExtension(path);
        var validation = AuroraFilenameValidator.Validate(stem);
        if (!validation.IsValid)
        {
            new AlertDialog("Invalid ERF Name", validation.GetErrorMessage()).Show(this);
            return;
        }

        try
        {
            new ErfCreationService().CreateErf(path, overwrite: true);
            new AlertDialog("ERF Created", $"Created empty ERF archive:\n{System.IO.Path.GetFileName(path)}").Show(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"New ERF failed: {ex.Message}");
            new AlertDialog("ERF Creation Failed", ex.Message).Show(this);
        }
    }

    #endregion

    #region New HAK

    private void OnNewHakClick(object? sender, RoutedEventArgs e)
    {
        // The IFO checkbox only applies when a module is open (Trebuchet auto-unpacks on open,
        // so "module open" already implies "unpacked").
        var moduleName = _moduleEditorVm?.IsModuleLoaded == true ? _moduleEditorVm.ModuleName : null;
        var dialog = new NewHakDialog(moduleName);
        dialog.Confirmed += OnNewHakConfirmed;
        dialog.Show(this);
    }

    private async void OnNewHakConfirmed(object? sender, EventArgs e)
    {
        if (sender is not NewHakDialog dialog) return;
        // One-shot: the dialog raises Confirmed at most once, but unsubscribe so the handler
        // doesn't outlive the dialog instance.
        dialog.Confirmed -= OnNewHakConfirmed;

        var path = System.IO.Path.Combine(dialog.OutputFolder, dialog.HakName + ".hak");

        // Overwrite is the one destructive branch — a modal confirm is the sanctioned exception
        // to the non-modal rule.
        if (System.IO.File.Exists(path))
        {
            var confirm = new ConfirmDialog(
                "Overwrite HAK?",
                $"{System.IO.Path.GetFileName(path)} already exists in that folder.\n\nOverwrite it?");
            await confirm.ShowDialog(this);
            if (!confirm.Confirmed) return;
        }

        try
        {
            new ErfCreationService().CreateHak(path,
                string.IsNullOrEmpty(dialog.Description) ? null : dialog.Description,
                overwrite: true);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"New HAK failed: {ex.Message}");
            new AlertDialog("HAK Creation Failed", ex.Message).Show(this);
            return;
        }

        // Optionally register the new HAK in the open module's IFO HAK list. Staged into the
        // module editor; persisted on the next Save Module, matching the existing HAK-list flow (#2267).
        if (dialog.AddToModuleIfo)
            StageHakInModuleIfo(dialog.HakName);

        // Chain straight into populating the fresh HAK (#2267).
        await AddFilesToArchiveAsync(path, ArchiveContentKind.HakMedia);
    }

    /// <summary>
    /// Append a HAK (by bare name, no extension) to the open module editor's HAK list so the
    /// next Save Module writes it to the module IFO. Deduplicates case-insensitively (#2267).
    /// </summary>
    private void StageHakInModuleIfo(string hakName)
    {
        if (_moduleEditorVm?.IsModuleLoaded != true) return;

        if (_moduleEditorVm.AddHakByName(hakName))
        {
            new AlertDialog("Added to Module",
                $"'{hakName}' was added to the module's HAK list.\n\nUse Save Module to write it to the .ifo.").Show(this);
        }
    }

    #endregion

    #region Add to HAK

    private async void OnAddToHakClick(object? sender, RoutedEventArgs e)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var startFolder = await TryGetHakFolderAsync(storage);
        var targets = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Add to HAK Archive — choose target",
            AllowMultiple = false,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("HAK Archives")
                {
                    Patterns = new[] { "*.hak" }
                }
            }
        });
        if (targets.Count == 0) return;

        await AddFilesToArchiveAsync(targets[0].Path.LocalPath, ArchiveContentKind.HakMedia);
    }

    #endregion

    #region Add to ERF

    private async void OnAddToErfClick(object? sender, RoutedEventArgs e)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        // 1. Pick the target ERF (must already exist).
        var targets = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Add to ERF Archive — choose target",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                // ERF-family archives you build/populate directly (#2267). The open module's own
                // archive is guarded below — packing working files back into it is the Save Module
                // workflow, not this one.
                new Avalonia.Platform.Storage.FilePickerFileType("ERF-family Archives")
                {
                    Patterns = new[] { "*.erf", "*.hak", "*.mod" }
                }
            }
        });
        if (targets.Count == 0) return;
        var erfPath = targets[0].Path.LocalPath;

        // Defense-in-depth: the picker now offers .erf/.hak/.mod (#2267), so guard against the
        // open module's own .mod — adding its working files back to itself is a confusing no-op,
        // and packing another module's .mod this way would corrupt it. Use Save Module instead (#2268).
        if (ModulePathHelper.IsCurrentModuleArchive(erfPath, RadoubSettings.Instance.CurrentModulePath))
        {
            new AlertDialog("Add to ERF",
                "That is the currently open module. To pack its working files into the module, " +
                "use Save Module.\n\nAdd to ERF builds a separate ERF archive.").Show(this);
            return;
        }

        await AddFilesToArchiveAsync(erfPath, ArchiveContentKind.ModuleContent);
    }

    /// <summary>
    /// Picker-default profile for <see cref="AddFilesToArchiveAsync"/>. ERF archives hold
    /// module user-generated content (blueprints, scripts) authored inside the module; HAK
    /// archives hold media/models the module references but that live outside it (#2267).
    /// </summary>
    private enum ArchiveContentKind
    {
        ModuleContent,
        HakMedia,
    }

    /// <summary>
    /// Pick files and add them to an existing ERF-family archive (.erf/.hak/.mod), reporting the
    /// result. Shared by "Add to ERF", "Add to HAK", and the post-create step of "New HAK" (#2267).
    /// The picker's filter and default folder follow <paramref name="contentKind"/>.
    /// </summary>
    private async System.Threading.Tasks.Task AddFilesToArchiveAsync(string archivePath, ArchiveContentKind contentKind)
    {
        var storage = GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;

        var archiveName = System.IO.Path.GetFileName(archivePath);

        // Default location + type filter depend on what the archive is for. ERF: module working
        // folder + blueprint/script filter. HAK: the game hak folder + media/model filter, since
        // HAK content (models, textures, sounds, 2DAs) lives outside the module (#2267).
        Avalonia.Platform.Storage.IStorageFolder? startFolder;
        Avalonia.Platform.Storage.FilePickerFileType[] fileTypes;
        var archiveLabel = contentKind == ArchiveContentKind.HakMedia ? "HAK" : "ERF";
        if (contentKind == ArchiveContentKind.HakMedia)
        {
            startFolder = await TryGetHakFolderAsync(storage);
            fileTypes = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("HAK media & models")
                {
                    Patterns = new[] { "*.mdl", "*.dds", "*.tga", "*.plt", "*.txi", "*.mtr",
                                       "*.wav", "*.bmu", "*.mp3", "*.ogg", "*.2da", "*.tlk",
                                       "*.ssf", "*.gui", "*.set", "*.itp" }
                },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            };
        }
        else
        {
            startFolder = await TryGetModuleFolderAsync(storage);
            fileTypes = new[]
            {
                // Module user-generated content: blueprints, scripts, and dialog — the assets
                // a user authors and would package into an ERF.
                new Avalonia.Platform.Storage.FilePickerFileType("Module content (blueprints, scripts)")
                {
                    Patterns = new[] { "*.utc", "*.uti", "*.utp", "*.utd", "*.utt", "*.uts",
                                       "*.ute", "*.utw", "*.utm", "*.nss", "*.ncs", "*.dlg" }
                },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            };
        }

        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = $"Add to {archiveName} — choose files",
            AllowMultiple = true,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = fileTypes
        });
        if (files.Count == 0) return;

        var paths = new System.Collections.Generic.List<string>();
        foreach (var f in files)
            paths.Add(f.Path.LocalPath);

        try
        {
            var result = new ErfAssetService().AddFiles(archivePath, paths, overwriteExisting: false);

            string message;
            if (result.AddedCount == 0 && result.SkippedCount > 0 && result.Errors.Count == 0)
            {
                // Everything the user picked was already in the archive — say so plainly rather
                // than the confusing "Added 0, Skipped N" (#2268).
                message = result.SkippedCount == 1
                    ? $"Nothing added — that resource is already in {archiveName}."
                    : $"Nothing added — all {result.SkippedCount} resources are already in {archiveName}.";
            }
            else
            {
                message =
                    $"Added {result.AddedCount} resource(s) to {archiveName}.\n" +
                    $"Skipped {result.SkippedCount} (already present).";
            }

            if (result.Errors.Count > 0)
            {
                message += $"\n\nRejected {result.Errors.Count}:";
                foreach (var (name, reason) in result.Errors)
                    message += $"\n  • {name}: {reason}";
            }
            new AlertDialog($"Add to {archiveLabel}", message).Show(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Add to {archiveLabel} failed: {ex.Message}");
            new AlertDialog($"Add to {archiveLabel} Failed", ex.Message).Show(this);
        }
    }

    private static async System.Threading.Tasks.Task<Avalonia.Platform.Storage.IStorageFolder?>
        TryGetModuleFolderAsync(Avalonia.Platform.Storage.IStorageProvider storage)
    {
        // Trebuchet unpacks a module when you open it, so the working directory (where loose
        // UGC files live) is the opened module's directory.
        var folder = ModulePathHelper.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
            return null;

        return await storage.TryGetFolderFromPathAsync(new Uri(folder));
    }

    /// <summary>
    /// Default folder for HAK content pickers: the first existing configured HAK search folder
    /// (the game hak/ folder first). HAK media/models live outside the module (#2267).
    /// </summary>
    private static async System.Threading.Tasks.Task<Avalonia.Platform.Storage.IStorageFolder?>
        TryGetHakFolderAsync(Avalonia.Platform.Storage.IStorageProvider storage)
    {
        foreach (var folder in RadoubSettings.Instance.GetAllHakSearchPaths())
        {
            if (!string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder))
                return await storage.TryGetFolderFromPathAsync(new Uri(folder));
        }
        return null;
    }

    #endregion
}
