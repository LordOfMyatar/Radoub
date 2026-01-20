// 3D Model Preview Control for Quartermaster
// Uses SkiaSharp for software rendering with simple 3D projection
// This is a foundation - can be upgraded to OpenGL later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Styling;
using Quartermaster.Services;
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
    private TextureService? _textureService;
    private float _rotationY = (float)Math.PI; // Default 180° so model faces camera
    private float _rotationX;
    private float _zoom = 1.0f;
    private Vector3 _cameraPosition = new(0, 0, 5);

    // Character colors for PLT rendering
    private int _skinColor;
    private int _hairColor;
    private int _tattoo1Color;
    private int _tattoo2Color;

    // Texture cache: resref -> SKBitmap
    private readonly Dictionary<string, SKBitmap?> _textureCache = new();

    // Render mode
    private bool _wireframeMode;

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
    /// Set the texture service for loading textures.
    /// </summary>
    public void SetTextureService(TextureService textureService)
    {
        _textureService = textureService;
        ClearTextureCache();
        InvalidateVisual();
    }

    /// <summary>
    /// Set character colors for PLT texture rendering.
    /// </summary>
    public void SetCharacterColors(int skinColor, int hairColor, int tattoo1Color, int tattoo2Color)
    {
        if (_skinColor != skinColor || _hairColor != hairColor ||
            _tattoo1Color != tattoo1Color || _tattoo2Color != tattoo2Color)
        {
            _skinColor = skinColor;
            _hairColor = hairColor;
            _tattoo1Color = tattoo1Color;
            _tattoo2Color = tattoo2Color;
            ClearTextureCache(); // Clear cache since colors changed
            InvalidateVisual();
        }
    }

    /// <summary>
    /// Toggle wireframe mode.
    /// </summary>
    public bool WireframeMode
    {
        get => _wireframeMode;
        set
        {
            _wireframeMode = value;
            InvalidateVisual();
        }
    }

    private void ClearTextureCache()
    {
        foreach (var bitmap in _textureCache.Values)
        {
            bitmap?.Dispose();
        }
        _textureCache.Clear();
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
    /// Reset the view to default (facing front).
    /// </summary>
    public void ResetView()
    {
        _rotationY = (float)Math.PI; // Face camera (180° rotation)
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

        // Draw background using theme-aware color
        context.DrawRectangle(
            GetBackgroundBrush(),
            null,
            new Rect(0, 0, bounds.Width, bounds.Height));

        if (_model == null)
        {
            DrawPlaceholder(context, bounds);
            return;
        }

        // Load textures for model if texture service is available
        var meshTextures = new Dictionary<string, SKBitmap?>();
        if (_textureService != null && !_wireframeMode)
        {
            foreach (var mesh in _model.GetMeshNodes())
            {
                var textureName = mesh.Bitmap?.ToLowerInvariant() ?? "";
                if (!string.IsNullOrEmpty(textureName) && !meshTextures.ContainsKey(textureName))
                {
                    if (!_textureCache.TryGetValue(textureName, out var bitmap))
                    {
                        bitmap = LoadTextureAsBitmap(textureName);
                        _textureCache[textureName] = bitmap;
                    }
                    meshTextures[textureName] = bitmap;
                }
            }
        }

        // Use custom SkiaSharp drawing operation for 3D rendering
        var customOp = new ModelRenderOperation(
            new Rect(0, 0, bounds.Width, bounds.Height),
            _model,
            _rotationY,
            _rotationX,
            _zoom,
            _cameraPosition,
            _wireframeMode,
            meshTextures);

        context.Custom(customOp);
    }

    private SKBitmap? LoadTextureAsBitmap(string textureName)
    {
        if (_textureService == null || string.IsNullOrEmpty(textureName))
            return null;

        try
        {
            // Try to load texture with character colors
            var textureData = _textureService.LoadTexture(
                textureName,
                _skinColor,
                _hairColor,
                _tattoo1Color,
                _tattoo2Color);

            if (textureData == null)
                return null;

            var (width, height, pixels) = textureData.Value;

            // Create SKBitmap from RGBA pixel data
            var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var handle = bitmap.GetPixels();

            // Copy pixel data - TextureService returns RGBA
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, handle, pixels.Length);

            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private void DrawPlaceholder(DrawingContext context, Rect bounds)
    {
        var centerX = bounds.Width / 2;
        var centerY = bounds.Height / 2;

        // Draw placeholder text using theme-aware color
        var typeface = new Typeface("Segoe UI");
        var formattedText = new FormattedText(
            "No Model",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            16,
            GetPlaceholderBrush());

        context.DrawText(formattedText,
            new Point(centerX - formattedText.Width / 2, centerY - formattedText.Height / 2));
    }

    #region Theme-Aware Colors

    // Default colors for fallback (dark theme values)
    private static readonly IBrush DefaultBackgroundBrush = new SolidColorBrush(Color.FromRgb(26, 26, 46));
    private static readonly IBrush DefaultPlaceholderBrush = new SolidColorBrush(Color.FromRgb(106, 106, 138));

    private IBrush GetBackgroundBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeSidebar", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultBackgroundBrush;
    }

    private IBrush GetPlaceholderBrush()
    {
        var app = Application.Current;
        if (app?.Resources.TryGetResource("ThemeDisabled", ThemeVariant.Default, out var brush) == true
            && brush is IBrush b)
            return b;
        return DefaultPlaceholderBrush;
    }

    #endregion

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
        private readonly bool _wireframeMode;
        private readonly Dictionary<string, SKBitmap?> _textures;

        public ModelRenderOperation(Rect bounds, MdlModel model,
            float rotationY, float rotationX, float zoom, Vector3 cameraPosition,
            bool wireframeMode, Dictionary<string, SKBitmap?> textures)
        {
            _bounds = bounds;
            _model = model;
            _rotationY = rotationY;
            _rotationX = rotationX;
            _zoom = zoom;
            _cameraPosition = cameraPosition;
            _wireframeMode = wireframeMode;
            _textures = textures;
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
            // NWN uses Z-up: rotate around Z for horizontal spin (left/right), X for tilt (up/down)
            var rotationMatrix = Matrix4x4.CreateRotationZ(_rotationY) *
                                Matrix4x4.CreateRotationX(_rotationX);

            var screenCenterX = (float)_bounds.Width / 2;
            var screenCenterY = (float)_bounds.Height / 2;

            // Calculate scale to fit model in viewport
            // Model radius determines how big it appears; we want it to fill ~80% of the smaller dimension
            var modelRadius = _model.Radius > 0 ? _model.Radius : 1f;
            var viewportSize = (float)Math.Min(_bounds.Width, _bounds.Height);
            // Scale factor: we want modelRadius to map to ~40% of viewport (leaving margin)
            // The projection divides by Z (~5), so multiply scale accordingly
            var scale = (viewportSize * 0.4f / modelRadius) * 5f * _zoom;

            // Light direction for basic shading (from upper right)
            var lightDir = Vector3.Normalize(new Vector3(0.5f, 0.8f, 0.3f));

            if (_wireframeMode)
            {
                // Wireframe rendering
                using var paint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    Color = new SKColor(100, 180, 255), // Light blue wireframe
                    IsAntialias = true
                };

                foreach (var mesh in _model.GetMeshNodes())
                {
                    DrawMeshWireframe(canvas, mesh, rotationMatrix, screenCenterX, screenCenterY, scale, paint);
                }
            }
            else
            {
                // Textured rendering with depth sorting
                var allFaces = new List<TexturedFace>();

                foreach (var mesh in _model.GetMeshNodes())
                {
                    CollectTexturedFaces(mesh, rotationMatrix, screenCenterX, screenCenterY, scale, lightDir, allFaces);
                }

                // Sort by depth (painter's algorithm - draw far faces first)
                allFaces.Sort((a, b) => b.Depth.CompareTo(a.Depth));

                // Group faces by texture for batched rendering
                var facesByTexture = new Dictionary<string, List<TexturedFace>>();
                foreach (var face in allFaces)
                {
                    var key = face.TextureName ?? "";
                    if (!facesByTexture.ContainsKey(key))
                        facesByTexture[key] = new List<TexturedFace>();
                    facesByTexture[key].Add(face);
                }

                // Render each texture group
                foreach (var (textureName, faces) in facesByTexture.OrderByDescending(kvp => kvp.Value.Max(f => f.Depth)))
                {
                    SKBitmap? texture = null;
                    if (!string.IsNullOrEmpty(textureName))
                        _textures.TryGetValue(textureName, out texture);

                    RenderTexturedFaces(canvas, faces, texture);
                }
            }

            canvas.Restore();
        }

        /// <summary>
        /// Represents a face with all data needed for textured rendering.
        /// </summary>
        private struct TexturedFace
        {
            public SKPoint[] ScreenPoints;
            public SKPoint[] UVs;
            public float Depth;
            public float LightIntensity;
            public string? TextureName;
            public SKColor DiffuseColor;
        }

        private void CollectTexturedFaces(MdlTrimeshNode mesh, Matrix4x4 rotation,
            float centerX, float centerY, float scale, Vector3 lightDir,
            List<TexturedFace> faces)
        {
            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
                return;

            // Model center for rotation
            var modelCenter = (_model.BoundingMin + _model.BoundingMax) * 0.5f;

            // Get mesh node's position offset
            var nodePosition = mesh.Position;

            // Get texture coordinates if available
            var hasUVs = mesh.TextureCoords.Length > 0 && mesh.TextureCoords[0].Length == mesh.Vertices.Length;

            // Project vertices to screen space
            var screenPoints = new SKPoint[mesh.Vertices.Length];
            var worldPositions = new Vector3[mesh.Vertices.Length];

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i] + nodePosition - modelCenter;
                var rotated = Vector3.Transform(v, rotation);
                worldPositions[i] = rotated;

                var depth = -rotated.Y + 5;
                if (depth < 0.1f) depth = 0.1f;

                var screenX = centerX + (rotated.X / depth) * scale;
                var screenY = centerY - (rotated.Z / depth) * scale;

                screenPoints[i] = new SKPoint(screenX, screenY);
            }

            // Get texture name and diffuse color
            var textureName = mesh.Bitmap?.ToLowerInvariant();
            var diffuse = mesh.Diffuse;
            var diffuseColor = new SKColor(
                (byte)(diffuse.X * 255),
                (byte)(diffuse.Y * 255),
                (byte)(diffuse.Z * 255));

            // Collect faces
            for (int faceIdx = 0; faceIdx < mesh.Faces.Length; faceIdx++)
            {
                var face = mesh.Faces[faceIdx];
                if (face.VertexIndex0 >= mesh.Vertices.Length ||
                    face.VertexIndex1 >= mesh.Vertices.Length ||
                    face.VertexIndex2 >= mesh.Vertices.Length)
                    continue;

                var p0 = screenPoints[face.VertexIndex0];
                var p1 = screenPoints[face.VertexIndex1];
                var p2 = screenPoints[face.VertexIndex2];

                // Backface culling
                var cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross > 0) continue;

                // Average depth for sorting
                var avgDepth = -(worldPositions[face.VertexIndex0].Y +
                                worldPositions[face.VertexIndex1].Y +
                                worldPositions[face.VertexIndex2].Y) / 3f;

                // Calculate face normal and lighting
                var v0 = mesh.Vertices[face.VertexIndex0];
                var v1 = mesh.Vertices[face.VertexIndex1];
                var v2 = mesh.Vertices[face.VertexIndex2];
                var edge1 = v1 - v0;
                var edge2 = v2 - v0;
                var normal = Vector3.Normalize(Vector3.Cross(edge1, edge2));
                var rotatedNormal = Vector3.TransformNormal(normal, rotation);
                var lightIntensity = Math.Max(0.3f, Vector3.Dot(rotatedNormal, lightDir));

                // Get UVs for this face
                SKPoint[] uvs;
                if (hasUVs)
                {
                    var texCoords = mesh.TextureCoords[0];
                    uvs = new[]
                    {
                        new SKPoint(texCoords[face.VertexIndex0].X, texCoords[face.VertexIndex0].Y),
                        new SKPoint(texCoords[face.VertexIndex1].X, texCoords[face.VertexIndex1].Y),
                        new SKPoint(texCoords[face.VertexIndex2].X, texCoords[face.VertexIndex2].Y)
                    };
                }
                else
                {
                    // Default UVs if none available
                    uvs = new[] { new SKPoint(0, 0), new SKPoint(1, 0), new SKPoint(0.5f, 1) };
                }

                faces.Add(new TexturedFace
                {
                    ScreenPoints = new[] { p0, p1, p2 },
                    UVs = uvs,
                    Depth = avgDepth,
                    LightIntensity = lightIntensity,
                    TextureName = textureName,
                    DiffuseColor = diffuseColor
                });
            }
        }

        private void RenderTexturedFaces(SKCanvas canvas, List<TexturedFace> faces, SKBitmap? texture)
        {
            if (faces.Count == 0) return;

            using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };

            foreach (var face in faces)
            {
                // Get lit color
                SKColor faceColor;
                if (texture != null)
                {
                    // Sample texture at face center UV and apply lighting
                    var centerU = (face.UVs[0].X + face.UVs[1].X + face.UVs[2].X) / 3f;
                    var centerV = (face.UVs[0].Y + face.UVs[1].Y + face.UVs[2].Y) / 3f;

                    // Wrap UVs and flip V
                    centerU = centerU - MathF.Floor(centerU);
                    centerV = 1.0f - (centerV - MathF.Floor(centerV));

                    var texX = (int)(centerU * texture.Width) % texture.Width;
                    var texY = (int)(centerV * texture.Height) % texture.Height;
                    if (texX < 0) texX += texture.Width;
                    if (texY < 0) texY += texture.Height;

                    var texColor = texture.GetPixel(texX, texY);
                    faceColor = ApplyLighting(texColor, face.LightIntensity);
                }
                else
                {
                    faceColor = ApplyLighting(face.DiffuseColor, face.LightIntensity);
                }

                paint.Color = faceColor;

                // Draw filled triangle
                using var path = new SKPath();
                path.MoveTo(face.ScreenPoints[0]);
                path.LineTo(face.ScreenPoints[1]);
                path.LineTo(face.ScreenPoints[2]);
                path.Close();
                canvas.DrawPath(path, paint);
            }
        }

        private static SKColor ApplyLighting(SKColor baseColor, float intensity)
        {
            return new SKColor(
                (byte)Math.Clamp(baseColor.Red * intensity, 0, 255),
                (byte)Math.Clamp(baseColor.Green * intensity, 0, 255),
                (byte)Math.Clamp(baseColor.Blue * intensity, 0, 255),
                baseColor.Alpha);
        }

        private void DrawMeshWireframe(SKCanvas canvas, MdlTrimeshNode mesh,
            Matrix4x4 rotation, float centerX, float centerY, float scale, SKPaint paint)
        {
            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
                return;

            // Model center for rotation
            var modelCenter = (_model.BoundingMin + _model.BoundingMax) * 0.5f;

            // Get mesh node's position offset (body parts have different positions)
            var nodePosition = mesh.Position;

            // Project vertices to screen space
            var screenPoints = new SKPoint[mesh.Vertices.Length];
            var depths = new float[mesh.Vertices.Length];

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                // Apply node position offset, center on model, rotate, then project
                var v = mesh.Vertices[i] + nodePosition - modelCenter;
                var rotated = Vector3.Transform(v, rotation);

                // NWN uses Z-up coordinate system:
                // X = left/right, Y = forward/back (depth), Z = up/down
                // After rotation, Y becomes depth into screen
                var depth = -rotated.Y + 5; // Negate Y for correct depth after rotation
                if (depth < 0.1f) depth = 0.1f;

                var screenX = centerX + (rotated.X / depth) * scale;
                var screenY = centerY - (rotated.Z / depth) * scale; // - because screen Y increases down, Z increases up

                screenPoints[i] = new SKPoint(screenX, screenY);
                depths[i] = depth;
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

                // Simple backface culling using screen-space winding (reversed due to Y flip)
                var cross = (p1.X - p0.X) * (p2.Y - p0.Y) - (p1.Y - p0.Y) * (p2.X - p0.X);
                if (cross > 0) continue; // Backface

                // Draw edges
                canvas.DrawLine(p0, p1, paint);
                canvas.DrawLine(p1, p2, paint);
                canvas.DrawLine(p2, p0, paint);
            }
        }
    }
}
