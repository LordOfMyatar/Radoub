using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Radoub.UI.Controls;

/// <summary>
/// Shared 3D model preview with rotate/zoom/pan/reset camera controls (#2430). Wraps
/// <see cref="ModelPreviewGLControl"/> with a transparent input surface (so pointer/wheel/key
/// events hit-test over the GL control) and a camera button bar. The camera math itself lives in
/// the GL control / ModelViewController; this control only forwards user input to it.
///
/// Hosts load a model and configure textures through <see cref="Preview"/>. Extracted from
/// Quartermaster's AppearancePanel so Reliquary (and later QM/Relique) share one implementation.
///
/// Interaction (mirrors QM): left-drag rotates, middle-drag or Shift+left-drag pans, wheel zooms
/// at the cursor, arrow keys / WASD rotate, Home resets. Models default to facing the camera
/// (ModelViewController initializes RotationY = π); fit-to-view happens automatically on load when
/// the GL control computes the model bounds.
/// </summary>
public partial class ModelPreviewPanel : UserControl
{
    private const float RotateStep = 0.1f;     // keyboard rotate step (radians), matches QM
    private const float ButtonRotate = 0.3f;   // rotate-button step (radians), matches QM
    private const float ZoomFactor = 1.2f;     // zoom-button multiplier, matches QM

    private PreviewDragMode _dragMode = PreviewDragMode.None;
    private Point _lastPointer;
    private bool _suppressStateEvent;

    // Radoub.UI controls do not get generated x:Name backing fields, so the named children are
    // resolved via FindControl after the XAML loads and cached here (matches the repo pattern).
    private readonly ModelPreviewGLControl _gl;
    private readonly Border _inputSurface;
    private readonly Border _stateSelectorRow;
    private readonly ComboBox _stateCombo;

    /// <summary>
    /// Raised when the user picks a state from the optional state selector (#2431), carrying the
    /// selected state's byte value. Only fires for user selections, never while the host populates.
    /// </summary>
    public event EventHandler<byte>? StateSelected;

    public ModelPreviewPanel()
    {
        InitializeComponent();

        _gl = this.FindControl<ModelPreviewGLControl>("ModelPreviewGL")!;
        _inputSurface = this.FindControl<Border>("InputSurface")!;
        _stateSelectorRow = this.FindControl<Border>("StateSelectorRow")!;
        _stateCombo = this.FindControl<ComboBox>("StateCombo")!;

        _inputSurface.PointerPressed += OnPointerPressed;
        _inputSurface.PointerMoved += OnPointerMoved;
        _inputSurface.PointerReleased += OnPointerReleased;
        _inputSurface.PointerWheelChanged += OnPointerWheel;
        _inputSurface.KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>The hosted GL control. Hosts load models and set the texture service through this.</summary>
    public ModelPreviewGLControl Preview => _gl;

    // ----- Optional state selector (#2431) -----

    /// <summary>One selectable preview state: a byte value (matching the model/engine state) + a label.</summary>
    public readonly record struct PreviewState(byte Value, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>
    /// Show the state selector populated with the given states, preselecting <paramref name="selected"/>.
    /// Passing one or zero states hides the row (nothing to choose). Selecting a state raises
    /// <see cref="StateSelected"/>; the host owns posing the model. Populating never raises the event.
    /// </summary>
    public void ShowStateSelector(IReadOnlyList<PreviewState> states, byte selected)
    {
        if (states == null || states.Count <= 1)
        {
            HideStateSelector();
            return;
        }

        _suppressStateEvent = true;
        try
        {
            _stateCombo.ItemsSource = states;
            var match = states.FirstOrDefault(s => s.Value == selected);
            _stateCombo.SelectedItem = states.Contains(match) ? match : states[0];
            _stateSelectorRow.IsVisible = true;
        }
        finally
        {
            _suppressStateEvent = false;
        }
    }

    /// <summary>Hide and clear the state selector (e.g. when no model is loaded).</summary>
    public void HideStateSelector()
    {
        _suppressStateEvent = true;
        try
        {
            _stateSelectorRow.IsVisible = false;
            _stateCombo.ItemsSource = null;
        }
        finally
        {
            _suppressStateEvent = false;
        }
    }

    private void OnStateSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressStateEvent) return;
        if (_stateCombo.SelectedItem is PreviewState state)
            StateSelected?.Invoke(this, state.Value);
    }

    // ----- Pointer / wheel / key forwarding (extracted from QM AppearancePanel #2124/#2430) -----

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(_inputSurface).Properties;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        _dragMode = PreviewDragModeDecider.Decide(
            isLeft: props.IsLeftButtonPressed,
            isMiddle: props.IsMiddleButtonPressed,
            shift: shift);

        if (_dragMode == PreviewDragMode.None) return;

        _lastPointer = e.GetPosition(_inputSurface);
        _inputSurface.Focus();
        e.Pointer.Capture(_inputSurface);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragMode == PreviewDragMode.None) return;

        var pos = e.GetPosition(_inputSurface);
        double dx = pos.X - _lastPointer.X;
        double dy = pos.Y - _lastPointer.Y;
        _lastPointer = pos;

        if (_dragMode == PreviewDragMode.Rotate)
            _gl.RotateByPixels(dx, dy);
        else if (_dragMode == PreviewDragMode.Pan)
            _gl.PanByPixels(dx, dy);
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragMode == PreviewDragMode.None) return;
        _dragMode = PreviewDragMode.None;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        var pos = e.GetPosition(_gl); // unproject uses GL viewport coords
        _gl.ZoomAtCursorPixels(pos, e.Delta.Y);
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.A:
                _gl.Rotate(-RotateStep, 0);
                break;
            case Key.Right:
            case Key.D:
                _gl.Rotate(RotateStep, 0);
                break;
            case Key.Up:
            case Key.W:
                _gl.Rotate(0, -RotateStep);
                break;
            case Key.Down:
            case Key.S:
                _gl.Rotate(0, RotateStep);
                break;
            case Key.Home:
                _gl.ResetView();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // ----- Camera button handlers -----

    private void OnRotateLeftClicked(object? sender, RoutedEventArgs e) => _gl.Rotate(-ButtonRotate);
    private void OnRotateRightClicked(object? sender, RoutedEventArgs e) => _gl.Rotate(ButtonRotate);
    private void OnResetViewClicked(object? sender, RoutedEventArgs e) => _gl.ResetView();
    private void OnZoomInClicked(object? sender, RoutedEventArgs e) => _gl.Zoom *= ZoomFactor;
    private void OnZoomOutClicked(object? sender, RoutedEventArgs e) => _gl.Zoom /= ZoomFactor;
}
