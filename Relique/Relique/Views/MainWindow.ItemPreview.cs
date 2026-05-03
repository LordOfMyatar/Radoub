using Avalonia.Controls;
using Avalonia.Threading;
using ItemEditor.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.UI.Controls;
using Radoub.UI.Services;
using System;
using ViewPreset = Radoub.UI.Services.ViewPreset;

namespace ItemEditor.Views;

/// <summary>
/// 3D item preview wiring (#1908 PR3b). Owns the per-window <see cref="TextureService"/>
/// + <see cref="MdlPartComposer"/> + <see cref="ItemPreviewController"/> + the renderer
/// adapter, and pumps the controller's debounce timer.
/// </summary>
public partial class MainWindow
{
    private const int PreviewDebounceMs = 100;

    private TextureService? _previewTextureService;
    private MdlPartComposer? _previewComposer;
    private ItemModelResolver? _previewResolver;
    private ItemPreviewController? _previewController;
    private RendererAdapter? _previewRendererAdapter;
    private DispatcherTimer? _previewDebounceTimer;

    /// <summary>
    /// Construct the preview pipeline once game data has finished loading. Called from
    /// <see cref="InitializeGameDataAsync"/>'s UI-thread continuation.
    /// </summary>
    private void InitializeItemPreview()
    {
        if (_gameDataService == null) return;
        if (_previewController != null) return; // already initialized

        var baseItemSvc = new Radoub.Formats.Services.BaseItemTypeService(_gameDataService);

        _previewTextureService = new TextureService(_gameDataService);

        // Reuse texture cache when the renderer asks for body-part MDLs through the composer
        _previewComposer = new MdlPartComposer(
            _gameDataService,
            (resRef, _) => LoadMdlForPreview(resRef));

        _previewResolver = new ItemModelResolver(baseItemSvc, _gameDataService);

        _previewRendererAdapter = new RendererAdapter(ItemPreviewGL, ItemPreviewPlaceholder, ItemPreviewControls);
        ItemPreviewGL.SetTextureService(_previewTextureService);

        _previewController = new ItemPreviewController(
            _previewResolver,
            _previewComposer,
            _previewRendererAdapter);

        _previewDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PreviewDebounceMs) };
        _previewDebounceTimer.Tick += (_, _) =>
        {
            if (_previewController?.HasPendingUpdate == true)
            {
                _previewController.FlushDebounce();
            }
        };
        _previewDebounceTimer.Start();

        UnifiedLogger.LogApplication(LogLevel.DEBUG, "ItemPreview pipeline initialized");
    }

    private MdlReader? _previewMdlReader;

    private MdlModel? LoadMdlForPreview(string resRef)
    {
        if (_gameDataService == null || string.IsNullOrEmpty(resRef)) return null;
        try
        {
            var data = _gameDataService.FindResource(resRef.ToLowerInvariant(), Radoub.Formats.Common.ResourceTypes.Mdl);
            if (data == null || data.Length == 0) return null;
            _previewMdlReader ??= new MdlReader();
            return _previewMdlReader.Parse(data);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"LoadMdlForPreview('{resRef}') failed: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Bind the preview to the currently-loaded item ViewModel, or unbind when the item
    /// is closed. Called from <see cref="PopulateEditor"/>.
    /// </summary>
    private void BindItemPreview(ItemEditor.ViewModels.ItemViewModel? vm)
    {
        _previewController?.BindViewModel(vm);
    }

    /// <summary>
    /// Tear down the preview pipeline on window close. Stops the debounce timer,
    /// unbinds the controller, and disposes the texture service so its caches release.
    /// </summary>
    private void DisposeItemPreview()
    {
        _previewDebounceTimer?.Stop();
        _previewDebounceTimer = null;

        _previewController?.Unbind();
        _previewController = null;

        _previewTextureService?.ClearCache();
        _previewTextureService = null;

        _previewComposer = null;
        _previewResolver = null;
        _previewRendererAdapter = null;
        _previewMdlReader = null;
    }

    // --- View preset button handlers ---

    private void OnItemPreviewFrontClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.SetViewPreset(ViewPreset.Front);

    private void OnItemPreviewBackClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.SetViewPreset(ViewPreset.Back);

    private void OnItemPreviewLeftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.SetViewPreset(ViewPreset.Side);

    private void OnItemPreviewRightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.SetViewPreset(ViewPreset.SideRight);

    private void OnItemPreviewResetClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.ResetView();

    /// <summary>
    /// Adapter that drives the live <see cref="ModelPreviewGLControl"/> from
    /// <see cref="IItemPreviewRenderer"/> calls. Toggles the placeholder and view-controls
    /// visibility as the model loads/clears.
    /// </summary>
    private sealed class RendererAdapter : IItemPreviewRenderer
    {
        private readonly ModelPreviewGLControl _gl;
        private readonly Control _placeholder;
        private readonly Control _viewControls;

        public RendererAdapter(ModelPreviewGLControl gl, Control placeholder, Control viewControls)
        {
            _gl = gl;
            _placeholder = placeholder;
            _viewControls = viewControls;
        }

        public void SetModel(MdlModel model)
        {
            _gl.Model = model;
            _gl.IsVisible = true;
            _placeholder.IsVisible = false;
            _viewControls.IsVisible = true;
        }

        public void Clear()
        {
            _gl.Model = null;
            _gl.IsVisible = false;
            _placeholder.IsVisible = true;
            _viewControls.IsVisible = false;
        }

        public void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)
        {
            _gl.SetArmorColors(metal1, metal2, cloth1, cloth2, leather1, leather2);
        }
    }
}
