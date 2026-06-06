using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using PlaceableEditor.Services;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Game-data service initialization for the editor (#2295). Stands up the shared
/// <see cref="GameDataService"/> on the window's first Opened, then the placeable appearance
/// service + raw MDL loader the 3D preview needs. All optional: when no game install is
/// configured the editor still loads/edits/saves UTPs; only the appearance combo and 3D
/// preview degrade gracefully.
/// </summary>
public partial class MainWindow
{
    private GameDataService? _gameData;
    private IPlaceableAppearanceService? _appearances;
    private PlaceableModelLoader? _modelLoader;
    private TextureService? _textureService;
    private ItemIconService? _itemIconService;
    private bool _servicesInitialized;

    private void WireServices()
    {
        Opened += OnWindowOpened;
    }

    private async void OnWindowOpened(object? sender, EventArgs e)
    {
        if (_servicesInitialized) return;
        _servicesInitialized = true;

        await Task.Run(() =>
        {
            _gameData = new GameDataService();
            if (_gameData.IsConfigured)
            {
                var moduleDir = GetModuleWorkingDirectory();
                if (!string.IsNullOrEmpty(moduleDir))
                    _gameData.ConfigureModuleHaks(moduleDir);

                _appearances = new PlaceableAppearanceService(_gameData);
                _modelLoader = new PlaceableModelLoader(_gameData, _appearances);
                _textureService = new TextureService(_gameData);
                _itemIconService = new ItemIconService(_gameData);
                _itemFactory = new Radoub.UI.ViewModels.ItemViewModelFactory(_gameData);
                UnifiedLogger.LogApplication(LogLevel.INFO, "Reliquary: GameDataService configured.");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    "Reliquary: GameDataService not configured — appearance/preview disabled.");
            }
        });

        // Push game data into the browser so it can resolve UTPs from BIF/HAK.
        var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
        if (browser != null) browser.GameDataService = _gameData;

        // Hand the texture service to the 3D preview control.
        var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
        if (identity != null && _textureService != null)
        {
            identity.Preview.SetTextureService(_textureService);
            identity.AppearanceChanged += OnAppearanceChanged;
            identity.PortraitBrowseRequested += OnPortraitBrowseRequested;
        }

        // Open the startup file from the command line (--file), now that services + the model
        // preview are ready. Relique pattern (#2295). FilePath is already module-resolved by
        // CommandLineService.Parse → ResolveModuleName.
        var startupFile = CommandLineService.Options.FilePath;
        if (!string.IsNullOrEmpty(startupFile) && System.IO.File.Exists(startupFile))
            LoadPlaceable(startupFile);
    }

    private static string? GetModuleWorkingDirectory()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (string.IsNullOrEmpty(modulePath)) return null;

        // Settings store paths with a leading ~ for the user profile; expand before any
        // filesystem check (Directory.Exists does not understand ~ on Windows).
        modulePath = Radoub.Formats.Common.PathHelper.ExpandPath(modulePath);

        if (System.IO.Directory.Exists(modulePath)) return modulePath;

        if (System.IO.File.Exists(modulePath) &&
            modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(modulePath);
            var dir = System.IO.Path.GetDirectoryName(modulePath);
            if (!string.IsNullOrEmpty(dir))
            {
                foreach (var candidate in new[]
                {
                    System.IO.Path.Combine(dir, name),
                    System.IO.Path.Combine(dir, "temp0"),
                    System.IO.Path.Combine(dir, "temp1")
                })
                {
                    if (System.IO.Directory.Exists(candidate)) return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>Populate the appearance combo + initial 3D model for the loaded placeable.</summary>
    private void PopulateAppearanceAndPreview()
    {
        if (_placeable is null) return;
        var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
        if (identity is null) return;

        if (_appearances != null)
            identity.PopulateAppearances(_appearances, _placeable.Appearance);

        UpdateModelPreview(_placeable.Appearance);
        RefreshPortraitPreview();
    }

    private void UpdateModelPreview(uint appearanceId)
    {
        var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
        if (identity is null) return;
        identity.Preview.Model = _modelLoader?.Load(appearanceId);
    }

    private void OnAppearanceChanged(object? sender, uint appearanceId)
    {
        if (_placeable is null) return;
        // Route the appearance change through undo, then refresh the preview.
        _undo.Execute(new Radoub.UI.Undo.SetFieldCommand<uint>(
            () => _placeable.Appearance, v => _placeable.Appearance = v, appearanceId, "change appearance"));
        UpdateModelPreview(appearanceId);
    }

    private async void OnPortraitBrowseRequested(object? sender, EventArgs e)
    {
        if (_placeable is null) return;
        if (_gameData is null || !_gameData.IsConfigured || _itemIconService is null)
        {
            UpdateStatus("Portrait browser needs a configured game install (set paths in Trebuchet).");
            return;
        }

        var context = new PlaceableEditor.Services.ReliquaryPortraitBrowserContext(_gameData, _itemIconService);
        var browser = Radoub.UI.Views.PortraitBrowserWindow.Create(context);
        var result = await browser.ShowDialog<ushort?>(this);
        if (result is not { } portraitId) return; // cancelled

        // Route the change through undo, then refresh the preview image.
        _undo.Execute(new Radoub.UI.Undo.SetFieldCommand<ushort>(
            () => _placeable.PortraitId, v => _placeable.PortraitId = v, portraitId, "change portrait"));

        RefreshPortraitPreview();
    }

    /// <summary>Resolve the placeable's PortraitId to a bitmap and push it into the identity panel.</summary>
    private void RefreshPortraitPreview()
    {
        if (_placeable is null || _gameData is null || _itemIconService is null) return;
        var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
        if (identity is null) return;

        var resRef = _gameData.Get2DAValue("portraits", _placeable.PortraitId, "BaseResRef");
        if (string.IsNullOrWhiteSpace(resRef) || resRef.Trim().Trim('*').Length == 0)
        {
            identity.SetPortrait(null);
            return;
        }

        // GetPortraitBitmap takes the base ResRef and resolves the size suffix internally
        // (same path the browser thumbnails use).
        var context = new PlaceableEditor.Services.ReliquaryPortraitBrowserContext(_gameData, _itemIconService);
        identity.SetPortrait(context.GetPortraitBitmap(resRef.Trim()));
    }
}
