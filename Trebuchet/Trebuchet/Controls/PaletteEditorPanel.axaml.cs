using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services.Palette;
using Radoub.UI.ViewModels;
using RadoubLauncher.Services;
using RadoubLauncher.Views;
using ModulePath = RadoubLauncher.Services.ModulePathHelper;

namespace RadoubLauncher.Controls;

/// <summary>
/// Trebuchet host for the shared ITP palette editor (#2477, M3). Thin adapter: resolves the current
/// module working directory, supplies the disk-load / dirty-prompt / save-commit delegates the
/// shared <see cref="PaletteEditorControl"/> needs, and shows/hides an empty state when no module
/// is loaded. The editor logic itself lives entirely in Radoub.UI so it can be embedded elsewhere.
/// </summary>
public partial class PaletteEditorPanel : UserControl
{
    private Window? _parentWindow;
    private PaletteEditorHostViewModel? _host;

    public PaletteEditorPanel()
    {
        InitializeComponent();
    }

    /// <summary>Wire the panel to its parent window and build the host view-model.</summary>
    public void Initialize(Window parentWindow)
    {
        _parentWindow = parentWindow;
        _host = new PaletteEditorHostViewModel(
            loadContext: LoadContext,
            promptDirty: PromptDirtyAsync,
            commit: writes => PaletteSaveTransaction.Commit(writes));
        Editor.Bind(_host, parentWindow);
        RefreshVisibility();
    }

    /// <summary>Called by the host when the working module changes. Reloads (or clears) the editor.</summary>
    public async void OnModuleChanged()
    {
        if (_host is null) return;
        RefreshVisibility();
        if (HasModuleFolder())
            // Reload the currently-selected type against the new module.
            await _host.SwitchResourceTypeAsync(_host.ActiveContext?.Type ?? PaletteResourceType.Item);
    }

    // ---- delegates supplied to the shared host VM ---------------------------

    private PaletteContext LoadContext(PaletteResourceType type)
    {
        string folder = ModuleFolderOrThrow();
        var ctx = new PaletteEditorLoader().Load(folder, type);
        // After a successful save the shared item-palette cache may be stale for this type; the
        // editor owns the file while open, so invalidation happens on save (see CommitAndInvalidate).
        return ctx;
    }

    private async Task<DirtySwitchChoice> PromptDirtyAsync()
    {
        if (_parentWindow is null) return DirtySwitchChoice.Discard;
        var dialog = new UnsavedChangesDialog(
            "This palette has unsaved changes. Save before switching resource type?");
        await dialog.ShowDialog(_parentWindow);
        return dialog.Result switch
        {
            ClosePromptResult.Save => DirtySwitchChoice.Save,
            ClosePromptResult.Discard => DirtySwitchChoice.Discard,
            _ => DirtySwitchChoice.Cancel,
        };
    }

    // ---- module folder helpers ----------------------------------------------

    private static bool HasModuleFolder()
    {
        var folder = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        return !string.IsNullOrEmpty(folder) && System.IO.Directory.Exists(folder);
    }

    private static string ModuleFolderOrThrow()
    {
        var folder = ModulePath.GetWorkingDirectory(RadoubSettings.Instance.CurrentModulePath);
        if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
            throw new InvalidOperationException("No unpacked module working directory is available.");
        return folder;
    }

    private void RefreshVisibility()
    {
        bool hasModule = HasModuleFolder();
        Editor.IsVisible = hasModule;
        NoModuleText.IsVisible = !hasModule;

        if (hasModule && _host?.ActiveContext is null)
        {
            // First reveal with a module loaded: load the default (Item) palette.
            _ = LoadInitialAsync();
        }
    }

    private async Task LoadInitialAsync()
    {
        try
        {
            if (_host != null) await _host.SwitchResourceTypeAsync(PaletteResourceType.Item);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Palette editor failed to load: {ex.Message}");
        }
    }
}
