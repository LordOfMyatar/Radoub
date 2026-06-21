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

        _previewRendererAdapter = new RendererAdapter(ItemPreviewGL, ItemPreviewPlaceholder, ItemPreviewControls, ItemPreviewInputSurface, ItemPreviewGenderPanel);
        ItemPreviewGL.SetTextureService(_previewTextureService);

        WirePreviewInput();

        // Restore the persisted preview gender so armor/clothing composes on the last-used
        // mannequin body across sessions (#2407).
        int persistedGender = SettingsService.Instance.PreviewGender;
        string mannequinPrefix = MannequinPrefix.ForGender(persistedGender);

        _previewController = new ItemPreviewController(
            _previewResolver,
            _previewComposer,
            _previewRendererAdapter,
            baseItemSvc,
            mannequinPrefix);

        // Reflect the persisted gender in the toggle without firing the change handler.
        _suppressGenderHandler = true;
        if (persistedGender == 1)
            ItemPreviewFemaleRadio.IsChecked = true;
        else
            ItemPreviewMaleRadio.IsChecked = true;
        _suppressGenderHandler = false;

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

        UnwirePreviewInput();

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

    // --- Gender toggle (#2407) ---

    private bool _suppressGenderHandler;

    private void OnItemPreviewGenderChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_suppressGenderHandler || _previewController == null) return;

        int gender = ItemPreviewFemaleRadio.IsChecked == true ? 1 : 0;
        SettingsService.Instance.PreviewGender = gender;
        _previewController.SetArmorMannequinPrefix(MannequinPrefix.ForGender(gender));
    }

    // --- Rotate / zoom button handlers (#2409, match Quartermaster) ---

    private void OnItemPreviewRotateLeftClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.Rotate(-0.3f);

    private void OnItemPreviewRotateRightClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.Rotate(0.3f);

    private void OnItemPreviewZoomInClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.Zoom *= 1.2f;

    private void OnItemPreviewZoomOutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => ItemPreviewGL.Zoom /= 1.2f;

    // --- Pointer / wheel / key input forwarding (#2409, match Quartermaster #2124) ---
    // Wired on the transparent input-surface Border overlaying the GL control.

    private PreviewDragMode _previewDragMode = PreviewDragMode.None;
    private Avalonia.Point _previewLastPointer;

    private void WirePreviewInput()
    {
        ItemPreviewInputSurface.PointerPressed += OnPreviewPointerPressed;
        ItemPreviewInputSurface.PointerMoved += OnPreviewPointerMoved;
        ItemPreviewInputSurface.PointerReleased += OnPreviewPointerReleased;
        ItemPreviewInputSurface.PointerWheelChanged += OnPreviewWheel;
        ItemPreviewInputSurface.KeyDown += OnPreviewKeyDown;
    }

    private void UnwirePreviewInput()
    {
        ItemPreviewInputSurface.PointerPressed -= OnPreviewPointerPressed;
        ItemPreviewInputSurface.PointerMoved -= OnPreviewPointerMoved;
        ItemPreviewInputSurface.PointerReleased -= OnPreviewPointerReleased;
        ItemPreviewInputSurface.PointerWheelChanged -= OnPreviewWheel;
        ItemPreviewInputSurface.KeyDown -= OnPreviewKeyDown;
    }

    private void OnPreviewPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(ItemPreviewInputSurface).Properties;
        bool shift = (e.KeyModifiers & Avalonia.Input.KeyModifiers.Shift) != 0;

        _previewDragMode = PreviewDragModeDecider.Decide(
            props.IsLeftButtonPressed, props.IsMiddleButtonPressed, shift);
        if (_previewDragMode == PreviewDragMode.None) return;

        _previewLastPointer = e.GetPosition(ItemPreviewInputSurface);
        ItemPreviewInputSurface.Focus();
        e.Pointer.Capture(ItemPreviewInputSurface);
        e.Handled = true;
    }

    private void OnPreviewPointerMoved(object? sender, Avalonia.Input.PointerEventArgs e)
    {
        if (_previewDragMode == PreviewDragMode.None) return;

        var pos = e.GetPosition(ItemPreviewInputSurface);
        double dx = pos.X - _previewLastPointer.X;
        double dy = pos.Y - _previewLastPointer.Y;
        _previewLastPointer = pos;

        if (_previewDragMode == PreviewDragMode.Rotate)
            ItemPreviewGL.RotateByPixels(dx, dy);
        else if (_previewDragMode == PreviewDragMode.Pan)
            ItemPreviewGL.PanByPixels(dx, dy);
    }

    private void OnPreviewPointerReleased(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
    {
        if (_previewDragMode != PreviewDragMode.None)
        {
            _previewDragMode = PreviewDragMode.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnPreviewWheel(object? sender, Avalonia.Input.PointerWheelEventArgs e)
    {
        var pos = e.GetPosition(ItemPreviewGL); // unproject uses GL viewport coords
        ItemPreviewGL.ZoomAtCursorPixels(pos, e.Delta.Y);
        e.Handled = true;
    }

    private void OnPreviewKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        const float rotStep = 0.1f;
        switch (e.Key)
        {
            case Avalonia.Input.Key.Left:
            case Avalonia.Input.Key.A:
                ItemPreviewGL.Rotate(-rotStep, 0);
                break;
            case Avalonia.Input.Key.Right:
            case Avalonia.Input.Key.D:
                ItemPreviewGL.Rotate(rotStep, 0);
                break;
            case Avalonia.Input.Key.Up:
            case Avalonia.Input.Key.W:
                ItemPreviewGL.Rotate(0, -rotStep);
                break;
            case Avalonia.Input.Key.Down:
            case Avalonia.Input.Key.S:
                ItemPreviewGL.Rotate(0, rotStep);
                break;
            case Avalonia.Input.Key.Home:
                ItemPreviewGL.ResetView();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

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
        private readonly Control _inputSurface;
        private readonly Control _genderPanel;

        public RendererAdapter(ModelPreviewGLControl gl, Control placeholder, Control viewControls, Control inputSurface, Control genderPanel)
        {
            _gl = gl;
            _placeholder = placeholder;
            _viewControls = viewControls;
            _inputSurface = inputSurface;
            _genderPanel = genderPanel;
        }

        public void SetModel(MdlModel model, bool gendered)
        {
            _gl.Model = model;
            _gl.IsVisible = true;
            _inputSurface.IsVisible = true;
            _placeholder.IsVisible = false;
            _viewControls.IsVisible = true;
            _genderPanel.IsVisible = gendered;
        }

        public void Clear()
        {
            _gl.Model = null;
            _gl.IsVisible = false;
            _inputSurface.IsVisible = false;
            _placeholder.IsVisible = true;
            _viewControls.IsVisible = false;
            _genderPanel.IsVisible = false;
        }

        public void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)
        {
            _gl.SetArmorColors(metal1, metal2, cloth1, cloth2, leather1, leather2);
        }
    }
}
