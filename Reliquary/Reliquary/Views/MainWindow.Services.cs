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

    private void OnPortraitBrowseRequested(object? sender, EventArgs e)
    {
        // The shared PortraitBrowserWindow needs an IPortraitBrowserContext (portraits.2da +
        // bitmap loading) that currently lives only in Quartermaster. Sharing that context is
        // tracked as follow-up; until then the button surfaces a status hint rather than a
        // half-built portrait service. PortraitId still round-trips via the model.
        UpdateStatus("Portrait browser pending shared IPortraitBrowserContext extract (follow-up).");
    }
}
