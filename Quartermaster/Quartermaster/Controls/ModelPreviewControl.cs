// 3D Model Preview Control for Quartermaster
// Uses SkiaSharp for software rendering with simple 3D projection
// This is a foundation - can be upgraded to OpenGL later

using System;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Radoub.Formats.Mdl;
using SkiaSharp;

namespace Quartermaster.Controls;

/// <summary>
/// Control for rendering 3D model previews using SkiaSharp.
/// Provides wireframe and simple solid rendering with rotation.
/// </summary>
public class ModelPreviewControl : Control
{
    private MdlModel? _model;
    private float _rotationY;
    private float _rotationX;
    private float _zoom = 1.0f;
    private Vector3 _cameraPosition = new(0, 0, 5);

    /// <summary>
    /// The model to render.
    /// </summary>
    public MdlModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            CenterCamera();
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Rotation around Y axis (horizontal rotation) in radians.
    /// </summary>
    public float RotationY
    {
        get => _rotationY;
        set
        {
            _rotationY = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Rotation around X axis (vertical rotation) in radians.
    /// </summary>
    public float RotationX
    {
        get => _rotationX;
        set
        {
            _rotationX = value;
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Zoom level (1.0 = default).
    /// </summary>
    public float Zoom
    {
        get => _zoom;
        set
        {
            _zoom = Math.Clamp(value, 0.1f, 10f);
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Rotate the model incrementally.
    /// </summary>
    public void Rotate(float deltaY, float deltaX = 0)
    {
        _rotationY += deltaY;
        _rotationX += deltaX;
        InvalidateVisual();
    }

    /// <summary>
    /// Reset the view to default.
    /// </summary>
    public void ResetView()
    {
        _rotationY = 0;
        _rotationX = 0;
        _zoom = 1.0f;
        CenterCamera();
        InvalidateVisual();
    }

    private void CenterCamera()
    {
        if (_model == null)
        {
            _cameraPosition = new Vector3(0, 0, 5);
            return;
        }

        // Center camera on model bounds
        var center = (_model.BoundingMin + _model.BoundingMax) * 0.5f;
        var distance = _model.Radius * 2.5f;
        if (distance < 1) distance = 5;
        _cameraPosition = new Vector3(center.X, center.Y, center.Z + distance);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // Draw background
        context.DrawRectangle(
            new SolidColorBrush(Color.FromRgb(26, 26, 46)),
            null,
            new Rect(0, 0, bounds.Width, bounds.Height));

        if (_model == null)
        {
            DrawPlaceholder(context, bounds);
            return;
        }

        // Use custom SkiaSharp drawing operation for 3D rendering
        var customOp = new ModelRenderOperation(
            new Rect(0, 0, bounds.Width, bounds.Height),
            _model,
            _rotationY,
            _rotationX,
            _zoom,
            _cameraPosition);

        context.Custom(customOp);
    }

    private void DrawPlaceholder(DrawingContext context, Rect bounds)
    {
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;

        // Draw placeholder text
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            "No Model",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            16,
            new SolidColorBrush(Color.FromRgb(106, 106, 138)));

        context.DrawText(formattedText,
            new Point(centerX - formattedText.Width / 2, centerY - formattedText.Height / 2));
    }

    /// <summary>
    /// Custom SkiaSharp draw operation for 3D model rendering.
    /// </summary>
    private class ModelRenderOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly MdlModel _model;
        private readonly float _rotationY;
        private readonly float _rotationX;
        private readonly float _zoom;
        private readonly Vector3 _cameraPosition;

        public ModelRenderOperation(Rect bounds, MdlModel model,
            float rotationY, float rotationX, float zoom, Vector3 cameraPosition)
        {
            _bounds = bounds;
            _model = model;
            _rotationY = rotationY;
            _rotationX = rotationX;
            _zoom = zoom;
            _cameraPosition = cameraPosition;
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            return other is ModelRenderOperation op &&
                   _bounds == op._bounds &&
                   _rotationY == op._rotationY &&
                   _rotationX == op._rotationX &&
                   _zoom == op._zoom;
        }

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null) return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            if (canvas == null) return;

            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)_bounds.Width, (float)_bounds.Height));

            // Set up transformation matrices
            var rotationMatrix = Matrix4x4.CreateRotationY(_rotationY) *
                                Matrix4x4.CreateRotationX(_rotationX);

            var viewMatrix = Matrix4x4.CreateLookAt(
                _cameraPosition,
                new Vector3(_cameraPosition.X, _cameraPosition.Y, 0),
                Vector3.UnitY);

            var projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4, // 45 degrees FOV
                (float)(_bounds.Width / _bounds.Height),
                0.1f,
                1000f);

            var screenCenterX = (float)_bounds.Width / 2;
            var screenCenterY = (float)_bounds.Height / 2;
            var scale = (float)Math.Min(_bounds.Width, _bounds.Height) / 2 * _zoom;

            // Draw wireframe for each mesh
            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                Color = new SKColor(100, 180, 255), // Light blue wireframe
                IsAntialias = true
            };

            foreach (var mesh in _model.GetMeshNodes())
            {
                DrawMeshWireframe(canvas, mesh, rotationMatrix, viewMatrix, projectionMatrix,
                    screenCenterX, screenCenterY, scale, paint);
            }

            canvas.Restore();
        }

        private void DrawMeshWireframe(SKCanvas canvas, MdlTrimeshNode mesh,
            Matrix4x4 rotation, Matrix4x4 view, Matrix4x4 projection,
            float centerX, float centerY, float scale, SKPaint paint)
        {
            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
                return;

            // Model center for rotation
            var modelCenter = (_model.BoundingMin + _model.BoundingMax) * 0.5f;

            // Project vertices to screen space
            var screenPoints = new SKPoint[mesh.Vertices.Length];
            var depths = new float[mesh.Vertices.Length];

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                // Center on model, rotate, then project
                var v = mesh.Vertices[i] - modelCenter;
                var rotated = Vector3.Transform(v, rotation);

                // Simple perspective projection
                var z = rotated.Z + 5; // Move away from camera
                if (z < 0.1f) z = 0.1f;

                var screenX = centerX + (rotated.X / z) * scale;
                var screenY = centerY - (rotated.Y / z) * scale; // Flip Y

                screenPoints[i] = new SKPoint(screenX, screenY);
                depths[i] = z;
            }

            // Draw triangle edges
            foreach (var face in mesh.Faces)
            {
                if (face.VertexIndex0 >= mesh.Vertices.Length ||
                    face.VertexIndex1 >= mesh.Vertices.Length ||
                    face.VertexIndex2 >= mesh.Vertices.Length)
                    continue;

                var p0 = screenPoints[face.VertexIndex0];
                var p1 = screenPoints[face.VertexIndex1];
                var p2 = screenPoints[face.VertexIndex2];

                // Simple backface culling using screen-space winding
                var cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross < 0) continue; // Backface

                // Draw edges
                canvas.DrawLine(p0, p1, paint);
                canvas.DrawLine(p1, p2, paint);
                canvas.DrawLine(p2, p0, paint);
            }
        }
    }
}
