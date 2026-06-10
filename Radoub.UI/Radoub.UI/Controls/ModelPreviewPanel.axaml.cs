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

    /// <summary>
    /// Raised when the user picks a state from the optional state selector (#2431), carrying the
    /// selected state's byte value. Only fires for user selections, never while the host populates.
    /// </summary>
    public event EventHandler<byte>? StateSelected;

    public ModelPreviewPanel()
    {
        InitializeComponent();

        InputSurface.PointerPressed += OnPointerPressed;
        InputSurface.PointerMoved += OnPointerMoved;
        InputSurface.PointerReleased += OnPointerReleased;
        InputSurface.PointerWheelChanged += OnPointerWheel;
        InputSurface.KeyDown += OnKeyDown;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>The hosted GL control. Hosts load models and set the texture service through this.</summary>
    public ModelPreviewGLControl Preview => ModelPreviewGL;

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
            StateCombo.ItemsSource = states;
            var match = states.FirstOrDefault(s => s.Value == selected);
            StateCombo.SelectedItem = states.Contains(match) ? match : states[0];
            StateSelectorRow.IsVisible = true;
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
            StateSelectorRow.IsVisible = false;
            StateCombo.ItemsSource = null;
        }
        finally
        {
            _suppressStateEvent = false;
        }
    }

    private void OnStateSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressStateEvent) return;
        if (StateCombo.SelectedItem is PreviewState state)
            StateSelected?.Invoke(this, state.Value);
    }

    // ----- Pointer / wheel / key forwarding (extracted from QM AppearancePanel #2124/#2430) -----

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(InputSurface).Properties;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        _dragMode = PreviewDragModeDecider.Decide(
            isLeft: props.IsLeftButtonPressed,
            isMiddle: props.IsMiddleButtonPressed,
            shift: shift);

        if (_dragMode == PreviewDragMode.None) return;

        _lastPointer = e.GetPosition(InputSurface);
        InputSurface.Focus();
        e.Pointer.Capture(InputSurface);
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragMode == PreviewDragMode.None) return;

        var pos = e.GetPosition(InputSurface);
        double dx = pos.X - _lastPointer.X;
        double dy = pos.Y - _lastPointer.Y;
        _lastPointer = pos;

        if (_dragMode == PreviewDragMode.Rotate)
            ModelPreviewGL.RotateByPixels(dx, dy);
        else if (_dragMode == PreviewDragMode.Pan)
            ModelPreviewGL.PanByPixels(dx, dy);
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
        var pos = e.GetPosition(ModelPreviewGL); // unproject uses GL viewport coords
        ModelPreviewGL.ZoomAtCursorPixels(pos, e.Delta.Y);
        e.Handled = true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
            case Key.A:
                ModelPreviewGL.Rotate(-RotateStep, 0);
                break;
            case Key.Right:
            case Key.D:
                ModelPreviewGL.Rotate(RotateStep, 0);
                break;
            case Key.Up:
            case Key.W:
                ModelPreviewGL.Rotate(0, -RotateStep);
                break;
            case Key.Down:
            case Key.S:
                ModelPreviewGL.Rotate(0, RotateStep);
                break;
            case Key.Home:
                ModelPreviewGL.ResetView();
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // ----- Camera button handlers -----

    private void OnRotateLeftClicked(object? sender, RoutedEventArgs e) => ModelPreviewGL.Rotate(-ButtonRotate);
    private void OnRotateRightClicked(object? sender, RoutedEventArgs e) => ModelPreviewGL.Rotate(ButtonRotate);
    private void OnResetViewClicked(object? sender, RoutedEventArgs e) => ModelPreviewGL.ResetView();
    private void OnZoomInClicked(object? sender, RoutedEventArgs e) => ModelPreviewGL.Zoom *= ZoomFactor;
    private void OnZoomOutClicked(object? sender, RoutedEventArgs e) => ModelPreviewGL.Zoom /= ZoomFactor;
}
