using System;
using System.ComponentModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
            Title = $"{baseTitle} - Radoub Toolset (Alpha)";
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
            new AlertDialog("ERF Creation Failed", ex.Message).Show(this);
        }
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
                // ERF only. Adding to a .mod is the module workflow (unpack -> edit -> Save Module),
                // and .hak has its own "Add to HAK" flow (#2267); neither belongs here.
                new Avalonia.Platform.Storage.FilePickerFileType("ERF Archives")
                {
                    Patterns = new[] { "*.erf" }
                }
            }
        });
        if (targets.Count == 0) return;
        var erfPath = targets[0].Path.LocalPath;

        // Defense-in-depth: the picker only offers .erf, but guard against the open module's own
        // archive anyway — adding its working files back to itself is a confusing no-op (#2268).
        if (ModulePathHelper.IsCurrentModuleArchive(erfPath, RadoubSettings.Instance.CurrentModulePath))
        {
            new AlertDialog("Add to ERF",
                "That is the currently open module. To pack its working files into the module, " +
                "use Save Module.\n\nAdd to ERF builds a separate ERF archive.").Show(this);
            return;
        }

        // 2. Pick files to add. Default the picker to the current module's working folder so
        //    palette assets (loose blueprints, compiled scripts) are right there to select.
        var startFolder = await TryGetModuleFolderAsync(storage);
        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Add to ERF — choose files",
            AllowMultiple = true,
            SuggestedStartLocation = startFolder,
            FileTypeFilter = new[]
            {
                // Module user-generated content: blueprints, scripts, and dialog — the assets
                // a user authors and would package into an ERF.
                new Avalonia.Platform.Storage.FilePickerFileType("Module content (blueprints, scripts)")
                {
                    Patterns = new[] { "*.utc", "*.uti", "*.utp", "*.utd", "*.utt", "*.uts",
                                       "*.ute", "*.utw", "*.utm", "*.nss", "*.ncs", "*.dlg" }
                },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
            }
        });
        if (files.Count == 0) return;

        var paths = new System.Collections.Generic.List<string>();
        foreach (var f in files)
            paths.Add(f.Path.LocalPath);

        try
        {
            var result = new ErfAssetService().AddFiles(erfPath, paths, overwriteExisting: false);
            var archiveName = System.IO.Path.GetFileName(erfPath);

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
            new AlertDialog("Add to ERF", message).Show(this);
        }
        catch (Exception ex)
        {
            new AlertDialog("Add to ERF Failed", ex.Message).Show(this);
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

    #endregion
}
