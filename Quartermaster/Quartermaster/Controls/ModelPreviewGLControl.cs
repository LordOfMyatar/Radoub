// GPU-accelerated 3D Model Preview Control for Quartermaster
// Uses Silk.NET OpenGL with Avalonia's OpenGlControlBase
// Provides proper depth buffer, perspective-correct texture mapping, and per-pixel lighting

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.Input;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Silk.NET.OpenGL;

namespace Quartermaster.Controls;

/// <summary>
/// Preview rendering state for the 3D model preview.
/// </summary>
public enum PreviewState
{
    /// <summary>No model loaded (null model or load failure).</summary>
    None,
    /// <summary>All meshes rendered successfully, no emitter nodes.</summary>
    Complete,
    /// <summary>Meshes rendered but model also has emitter nodes (particles not shown).</summary>
    Incomplete,
    /// <summary>Model loaded but has no renderable geometry (emitter-only).</summary>
    NotAvailable
}

/// <summary>
/// GPU-accelerated control for rendering 3D model previews using OpenGL.
/// Provides proper depth buffer, perspective-correct texture mapping, and lighting.
/// </summary>
public class ModelPreviewGLControl : OpenGlControlBase
{
    static ModelPreviewGLControl()
    {
        // Allow keyboard focus (arrow keys, WASD, Home, F8) — #2124.
        FocusableProperty.OverrideDefaultValue<ModelPreviewGLControl>(true);
    }

    private GL? _gl;
    private OpenGLShaderManager? _shaderManager;
    private readonly ModelViewController _viewController = new();
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private int _indexCount;
    private readonly Dictionary<string, uint> _textureCache = new();
    // Names of textures in _textureCache that came from PLT sources and depend on
    // the current color indices. Color-index changes invalidate only these — not
    // flat TGA/DDS textures whose pixels are color-independent (#2058).
    private readonly HashSet<string> _pltTextureNames = new();
    // Maps unresolvable bitmap names to valid fallback texture names
    private readonly Dictionary<string, string> _textureRemapping = new();

    // Per-mesh draw info for textured rendering
    private readonly List<MeshDrawCall> _meshDrawCalls = new();
    private struct MeshDrawCall
    {
        public int IndexOffset;
        public int IndexCount;
        public string? TextureName;
    }

    private MdlModel? _model;
    private TextureService? _textureService;
    private bool _preferBifTextures;

    // PLT color indices for texture rendering
    private PltColorIndices _colorIndices = new();
    private bool _needsTextureUpdate;
    private bool _needsMeshUpdate;
    private bool _logOncePerModel;

    // Shader debug visualisation (#2026 investigation):
    //   0 = normal rendering
    //   1 = world-space normal as RGB
    //   2 = lighting dot-product as grayscale
    private int _debugMode;

    // Cached render matrices / viewport for pointer unprojection (#2124).
    private Matrix4x4 _lastProjection = Matrix4x4.Identity;
    private Matrix4x4 _lastView = Matrix4x4.Identity;
    private int _lastViewportWidth;
    private int _lastViewportHeight;
    private float _lastCameraDistance = 1f;

    // Animation playback state (#2124).
    private MdlAnimation? _activeAnimation;
    private float _animTime;
    private bool _animPlaying;
    private DateTime _animLastTick = DateTime.UtcNow;
    private Dictionary<string, ModelViewController.NodePose>? _cachedPose;
    private Avalonia.Threading.DispatcherTimer? _animTimer;

    // Pointer drag state (#2124).
    private enum DragMode { None, Rotate, Pan }
    private DragMode _dragMode = DragMode.None;
    private Point _lastPointerPos;

    private PreviewState _previewState = PreviewState.None;

    /// <summary>
    /// Current preview rendering state.
    /// </summary>
    public PreviewState PreviewState => _previewState;

    /// <summary>
    /// Raised on the UI thread when the preview state changes.
    /// </summary>
    public event EventHandler<PreviewState>? PreviewStateChanged;

    /// <summary>
    /// Mesh composition info for the currently loaded model.
    /// </summary>
    public record ModelMeshInfo(int TotalMeshes, int SkinMeshCount, int HiddenMeshCount, int SkippedTrimeshCount);

    /// <summary>
    /// Raised on the UI thread after model mesh analysis completes.
    /// </summary>
    public event EventHandler<ModelMeshInfo>? MeshInfoChanged;

    /// <summary>
    /// The model to render.
    /// </summary>
    public MdlModel? Model
    {
        get => _model;
        set
        {
            _model = value;
            _needsMeshUpdate = true;
            _needsTextureUpdate = true;  // New model needs textures loaded
            _logOncePerModel = true;
            _viewController.CenterCamera();
            if (value == null)
            {
                SetPreviewState(PreviewState.None);
                MeshInfoChanged?.Invoke(this, new ModelMeshInfo(0, 0, 0, 0));
            }
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// Rotation around Y axis (horizontal rotation) in radians.
    /// </summary>
    public float RotationY
    {
        get => _viewController.RotationY;
        set
        {
            _viewController.RotationY = value;
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// Rotation around X axis (vertical rotation) in radians.
    /// </summary>
    public float RotationX
    {
        get => _viewController.RotationX;
        set
        {
            _viewController.RotationX = value;
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// Shader debug mode (0 = normal, 1 = normal-as-RGB, 2 = lighting-as-grayscale).
    /// Used to diagnose shading issues — when a model rotates, a correct
    /// world-space normal will paint different colours on the same face
    /// region. Locked colours indicate the normal isn't being transformed.
    /// </summary>
    public int DebugMode
    {
        get => _debugMode;
        set
        {
            if (_debugMode != value)
            {
                _debugMode = value;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"ModelPreview: debugMode = {value}");
                RequestNextFrameRendering();
            }
        }
    }

    /// <summary>
    /// Zoom level (1.0 = default).
    /// </summary>
    public float Zoom
    {
        get => _viewController.Zoom;
        set
        {
            _viewController.Zoom = value;
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// When true, textures are loaded preferring BIF over HAK (Override → BIF, skip HAK).
    /// Set this for base game creatures to avoid CEP texture incompatibilities (#1867).
    /// </summary>
    public bool PreferBifTextures
    {
        get => _preferBifTextures;
        set
        {
            if (_preferBifTextures != value)
            {
                _preferBifTextures = value;
                _needsTextureUpdate = true;
                ClearTextureCache();
                RequestNextFrameRendering();
            }
        }
    }

    /// <summary>
    /// Set the texture service for loading textures.
    /// </summary>
    public void SetTextureService(TextureService textureService)
    {
        _textureService = textureService;
        _needsTextureUpdate = true;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Set character colors for PLT texture rendering.
    /// </summary>
    public void SetCharacterColors(int skinColor, int hairColor, int tattoo1Color, int tattoo2Color)
    {
        SetColorIndices(new PltColorIndices
        {
            Skin = skinColor,
            Hair = hairColor,
            Tattoo1 = tattoo1Color,
            Tattoo2 = tattoo2Color,
            Metal1 = _colorIndices.Metal1,
            Metal2 = _colorIndices.Metal2,
            Cloth1 = _colorIndices.Cloth1,
            Cloth2 = _colorIndices.Cloth2,
            Leather1 = _colorIndices.Leather1,
            Leather2 = _colorIndices.Leather2
        });
    }

    /// <summary>
    /// Set all PLT color indices (body + armor colors).
    /// </summary>
    public void SetColorIndices(PltColorIndices colorIndices)
    {
        if (!ColorsEqual(_colorIndices, colorIndices))
        {
            _colorIndices = colorIndices;
            // Only PLT textures depend on color indices. Flat TGA/DDS textures
            // are color-independent and can survive the change (#2058).
            InvalidatePltTextures();
            _needsTextureUpdate = true;
            RequestNextFrameRendering();
        }
    }

    private void InvalidatePltTextures()
    {
        if (_pltTextureNames.Count == 0) return;

        foreach (var name in _pltTextureNames)
        {
            if (_textureCache.TryGetValue(name, out var texId))
            {
                if (_gl != null && texId != 0)
                    _gl.DeleteTexture(texId);
                _textureCache.Remove(name);
            }
        }
        _pltTextureNames.Clear();
    }

    /// <summary>
    /// Set armor colors for PLT texture rendering.
    /// </summary>
    public void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)
    {
        SetColorIndices(new PltColorIndices
        {
            Skin = _colorIndices.Skin,
            Hair = _colorIndices.Hair,
            Tattoo1 = _colorIndices.Tattoo1,
            Tattoo2 = _colorIndices.Tattoo2,
            Metal1 = metal1,
            Metal2 = metal2,
            Cloth1 = cloth1,
            Cloth2 = cloth2,
            Leather1 = leather1,
            Leather2 = leather2
        });
    }

    /// <summary>
    /// Clears the GPU texture cache so textures reload with current color indices.
    /// Also called on module switch to clear stale HAK textures (#1867).
    /// </summary>
    public void ClearTextureCache()
    {
        if (_gl != null)
        {
            foreach (var texId in _textureCache.Values)
                _gl.DeleteTexture(texId);
        }
        _textureCache.Clear();
        _pltTextureNames.Clear();
    }

    private static bool ColorsEqual(PltColorIndices a, PltColorIndices b)
    {
        return a.Skin == b.Skin && a.Hair == b.Hair &&
               a.Tattoo1 == b.Tattoo1 && a.Tattoo2 == b.Tattoo2 &&
               a.Metal1 == b.Metal1 && a.Metal2 == b.Metal2 &&
               a.Cloth1 == b.Cloth1 && a.Cloth2 == b.Cloth2 &&
               a.Leather1 == b.Leather1 && a.Leather2 == b.Leather2;
    }

    /// <summary>
    /// Rotate the model incrementally.
    /// </summary>
    public void Rotate(float deltaY, float deltaX = 0)
    {
        _viewController.Rotate(deltaY, deltaX);
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Reset the view to default (facing front).
    /// </summary>
    public void ResetView()
    {
        _viewController.ResetView();
        RequestNextFrameRendering();
    }

    // ----- Pointer / keyboard input (#2124) -----
    //
    // OpenGlControlBase has no Background brush, so pointer events don't
    // hit-test on its transparent pixels. The host panel overlays a
    // transparent Border on top of this control and forwards input here
    // via the public Handle* / Pan / ZoomAtCursor / RotateBy methods
    // below. The OnPointerPressed etc. overrides remain as a fallback
    // if the control is ever used without the overlay.

    /// <summary>
    /// Host panels call this to request a pan in screen-pixel units.
    /// Converts to world units using the last-rendered viewport + camera distance.
    /// </summary>
    public void PanByPixels(double dxPixels, double dyPixels)
    {
        if (_lastViewportHeight <= 0) return;
        float fovRadians = MathF.PI / 6f;
        float halfFovTan = MathF.Tan(fovRadians * 0.5f);
        float worldPerPixel = 2f * _lastCameraDistance * halfFovTan / _lastViewportHeight;

        // See OnPointerMoved for axis convention; camera right = -X, up = +Z.
        _viewController.Pan(new Vector3(-(float)dxPixels * worldPerPixel, 0, (float)dyPixels * worldPerPixel));
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Host panels call this to zoom toward a world point under the cursor.
    /// </summary>
    public void ZoomAtCursorPixels(Point screenPos, double wheelDeltaY)
    {
        float factor = (float)Math.Pow(1.1, wheelDeltaY);
        var pivot = UnprojectToTargetPlane(screenPos);
        _viewController.ZoomAtPoint(factor, pivot);
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Host panels call this to rotate by screen-pixel drag deltas.
    /// </summary>
    public void RotateByPixels(double dxPixels, double dyPixels)
    {
        _viewController.Rotate((float)(dxPixels * 0.01), (float)(dyPixels * 0.01));
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Cycle debug visualisation mode (0..4).
    /// </summary>
    public void CycleDebugMode()
    {
        DebugMode = (_debugMode + 1) % 5;
    }

    /// <summary>
    /// Snap to a preset view (front/side/top/back). #2124.
    /// </summary>
    public void SetViewPreset(ViewPreset preset)
    {
        _viewController.SetViewPreset(preset);
        RequestNextFrameRendering();
    }

    // ----- Animation playback (#2124) -----

    /// <summary>
    /// Currently active animation, or null if none selected.
    /// </summary>
    public MdlAnimation? ActiveAnimation => _activeAnimation;

    /// <summary>
    /// Current playhead time in seconds (0..Animation.Length).
    /// </summary>
    public float AnimationTime
    {
        get => _animTime;
        set
        {
            _animTime = value;
            _cachedPose = null;
            _needsMeshUpdate = true;
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// True when animation is auto-advancing. Toggle with Play/Pause.
    /// </summary>
    public bool IsAnimationPlaying => _animPlaying;

    /// <summary>
    /// Select an animation by reference (null to clear). Resets playhead to 0.
    /// </summary>
    public void SetActiveAnimation(MdlAnimation? animation)
    {
        _activeAnimation = animation;
        _animTime = 0f;
        _cachedPose = null;
        _needsMeshUpdate = true;
        RequestNextFrameRendering();
    }

    public void PlayAnimation()
    {
        if (_activeAnimation == null) return;
        _animPlaying = true;
        _animLastTick = DateTime.UtcNow;
        EnsureAnimTimer();
    }

    public void PauseAnimation()
    {
        _animPlaying = false;
    }

    private void EnsureAnimTimer()
    {
        if (_animTimer != null) return;
        _animTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33), // ~30 fps
        };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (!_animPlaying || _activeAnimation == null) return;

        var now = DateTime.UtcNow;
        var dt = (float)(now - _animLastTick).TotalSeconds;
        _animLastTick = now;

        float length = _activeAnimation.Length;
        if (length <= 0)
        {
            _animTime = 0f;
        }
        else
        {
            _animTime += dt;
            if (_animTime >= length)
                _animTime -= length * MathF.Floor(_animTime / length); // loop
        }

        _cachedPose = null;
        _needsMeshUpdate = true;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Build a node-name → sampled-pose dictionary from the active animation
    /// at the current <see cref="AnimationTime"/>. Cached until playhead moves
    /// or the animation changes.
    /// </summary>
    private Dictionary<string, ModelViewController.NodePose>? GetCurrentPose()
    {
        if (_activeAnimation?.GeometryRoot == null) return null;
        if (_cachedPose != null) return _cachedPose;

        var pose = new Dictionary<string, ModelViewController.NodePose>(StringComparer.OrdinalIgnoreCase);
        BuildPoseRecursive(_activeAnimation.GeometryRoot, _animTime, pose);
        _cachedPose = pose;
        return pose;
    }

    private static void BuildPoseRecursive(MdlNode animNode, float t,
        Dictionary<string, ModelViewController.NodePose> pose)
    {
        bool hasPos = animNode.PositionTimes.Length > 1;
        bool hasOri = animNode.OrientationTimes.Length > 1;
        bool hasScl = animNode.ScaleTimes.Length > 1;

        if (hasPos || hasOri || hasScl)
        {
            var p = new ModelViewController.NodePose(
                hasPos, hasPos ? MdlAnimationEvaluator.EvaluatePosition(animNode, t) : animNode.Position,
                hasOri, hasOri ? MdlAnimationEvaluator.EvaluateOrientation(animNode, t) : animNode.Orientation,
                hasScl, hasScl ? MdlAnimationEvaluator.EvaluateScale(animNode, t) : animNode.Scale);
            if (!string.IsNullOrEmpty(animNode.Name))
                pose[animNode.Name] = p;
        }

        foreach (var child in animNode.Children)
            BuildPoseRecursive(child, t, pose);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var props = e.GetCurrentPoint(this).Properties;
        bool shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && shift))
        {
            _dragMode = DragMode.Pan;
        }
        else if (props.IsLeftButtonPressed)
        {
            _dragMode = DragMode.Rotate;
        }
        else
        {
            return;
        }

        _lastPointerPos = e.GetPosition(this);
        Focus();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragMode == DragMode.None) return;

        var pos = e.GetPosition(this);
        double dx = pos.X - _lastPointerPos.X;
        double dy = pos.Y - _lastPointerPos.Y;
        _lastPointerPos = pos;

        if (_dragMode == DragMode.Rotate)
        {
            // 0.01 rad/px matches the feel of the discrete rotate buttons
            // (0.3 rad per click). Free X-axis orbit — no clamping.
            _viewController.Rotate((float)(dx * 0.01), (float)(dy * 0.01));
            RequestNextFrameRendering();
        }
        else if (_dragMode == DragMode.Pan)
        {
            // Convert screen-pixel delta to world units at the target's depth.
            // At the camera-to-target distance, one NDC unit covers
            // cameraDistance * halfFovTan world units vertically.
            if (_lastViewportHeight <= 0) return;
            float fovRadians = MathF.PI / 6f;
            float halfFovTan = MathF.Tan(fovRadians * 0.5f);
            float worldPerPixel = 2f * _lastCameraDistance * halfFovTan / _lastViewportHeight;

            var worldDelta = ScreenDeltaToWorld(-(float)dx * worldPerPixel, (float)dy * worldPerPixel);
            _viewController.Pan(worldDelta);
            RequestNextFrameRendering();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragMode != DragMode.None)
        {
            _dragMode = DragMode.None;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);

        // Each wheel notch is ~1.0 in e.Delta.Y. 1.1× per notch gives
        // a natural zoom feel without being twitchy.
        float factor = (float)Math.Pow(1.1, e.Delta.Y);

        var pos = e.GetPosition(this);
        var pivot = UnprojectToTargetPlane(pos);
        _viewController.ZoomAtPoint(factor, pivot);

        RequestNextFrameRendering();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        const float rotStep = 0.1f;

        switch (e.Key)
        {
            case Key.Left:
            case Key.A:
                _viewController.Rotate(-rotStep, 0);
                break;
            case Key.Right:
            case Key.D:
                _viewController.Rotate(rotStep, 0);
                break;
            case Key.Up:
            case Key.W:
                _viewController.Rotate(0, -rotStep);
                break;
            case Key.Down:
            case Key.S:
                _viewController.Rotate(0, rotStep);
                break;
            case Key.Home:
                _viewController.ResetView();
                break;
            case Key.F8:
                DebugMode = (_debugMode + 1) % 5;
                return; // DebugMode setter already triggers re-render
            default:
                return;
        }

        RequestNextFrameRendering();
        e.Handled = true;
    }

    /// <summary>
    /// Convert a screen-space (pixel) delta into a world-space delta in the
    /// plane facing the camera, using the current rotation state. Pan drags
    /// move the model along these in-plane axes rather than world XY so the
    /// motion feels right at any view angle.
    /// </summary>
    private Vector3 ScreenDeltaToWorld(float rightAmount, float upAmount)
    {
        // The model rotation is applied to vertices, so the visible
        // "right" / "up" directions in world space are the unrotated
        // camera axes. Camera looks along +Y (LookAt from -Y eye to
        // origin with Z-up), so "right" = -X and "up" = +Z when no
        // rotation has been applied. We want pan to move the model
        // under the mouse regardless of the model rotation — so we
        // translate the camera target along world-aligned axes only.
        // This matches common DCC tool behaviour.
        return new Vector3(rightAmount, 0, upAmount);
    }

    /// <summary>
    /// Approximate the world-space point under the cursor at the depth of
    /// the camera target, so scroll-wheel zoom can pivot there.
    /// </summary>
    private Vector3 UnprojectToTargetPlane(Point screenPos)
    {
        if (_lastViewportWidth <= 0 || _lastViewportHeight <= 0)
            return _viewController.CameraTarget;

        // NDC coordinates (-1..1), Y inverted because screen Y is top-down.
        float ndcX = (float)(screenPos.X / _lastViewportWidth * 2.0 - 1.0);
        float ndcY = (float)(1.0 - screenPos.Y / _lastViewportHeight * 2.0);

        // At the target's depth, one NDC unit vertically = cameraDistance * halfFovTan world units.
        float fovRadians = MathF.PI / 6f;
        float halfFovTan = MathF.Tan(fovRadians * 0.5f);
        float aspect = (float)_lastViewportWidth / _lastViewportHeight;
        float worldY = ndcY * _lastCameraDistance * halfFovTan;
        float worldX = ndcX * _lastCameraDistance * halfFovTan * aspect;

        // The camera's right/up axes at the target depth. See
        // ScreenDeltaToWorld — right = -X, up = +Z under our LookAt.
        var target = _viewController.CameraTarget;
        return target + new Vector3(-worldX, 0, worldY);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        try
        {
            // Create Silk.NET GL context from Avalonia's proc address loader
            _gl = GL.GetApi(gl.GetProcAddress);

            // Detect whether this is an OpenGL ES context (ANGLE on Windows)
            // or desktop OpenGL (GLX on Linux) — shaders need different version preambles.
            var versionString = _gl.GetStringS(StringName.Version) ?? "";
            var isOpenGLES = versionString.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
            var renderer = _gl.GetStringS(StringName.Renderer) ?? "unknown";
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"GL context: {versionString} | Renderer: {renderer} | ES={isOpenGLES}");

            // Compile and link shaders via manager
            _shaderManager = new OpenGLShaderManager(_gl, isOpenGLES);
            _shaderManager.CreateProgram();

            // Create VAO/VBO/EBO
            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            UnifiedLogger.LogApplication(LogLevel.INFO, $"OpenGL initialized: VAO={_vao}, VBO={_vbo}, EBO={_ebo}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"OpenGL init failed: {ex.Message}");
        }
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        try
        {
            if (_gl != null)
            {
                // Clean up textures
                foreach (var texId in _textureCache.Values)
                {
                    _gl.DeleteTexture(texId);
                }
                _textureCache.Clear();
                _pltTextureNames.Clear();

                // Clean up buffers and program
                _gl.DeleteBuffer(_vbo);
                _gl.DeleteBuffer(_ebo);
                _gl.DeleteVertexArray(_vao);
                _shaderManager?.Cleanup();
                _shaderManager = null;
                _gl = null;
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"OpenGL cleanup error (non-fatal): {ex.GetType().Name}: {ex.Message}");
            _gl = null;
        }

        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null)
        {
            return;
        }

        var bounds = Bounds;
        var width = (int)bounds.Width;
        var height = (int)bounds.Height;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        // Clear with visible background color (blue-ish gray)
        // Note: Avalonia already binds the correct framebuffer before calling OnOpenGlRender
        _gl.ClearColor(0.2f, 0.2f, 0.3f, 1.0f);
        _gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Less);  // Ensure proper depth comparison
        _gl.Viewport(0, 0, (uint)width, (uint)height);

        // Check for any GL errors after basic setup
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"GL error after clear/viewport: {err}");
        }

        if (_model == null)
        {
            return;
        }

        // Update mesh data if needed - this also updates _cameraTarget and _modelRadius
        if (_needsMeshUpdate)
        {
            UpdateMeshBuffers();
            _needsMeshUpdate = false;
            // Note: We continue rendering with the updated values
        }

        // Update textures if needed
        if (_needsTextureUpdate)
        {
            try
            {
                UpdateTextures();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"UpdateTextures failed for model '{_model?.Name}': {ex.GetType().Name}: {ex.Message}");
            }
            _needsTextureUpdate = false;
        }

        if (_indexCount == 0)
        {
            return;
        }

        // Disable culling for debugging - NWN models may have inconsistent winding
        _gl.Disable(EnableCap.CullFace);

        // Set up matrices
        var aspect = (float)width / height;

        // Perspective projection: camera at fixed distance, model scaled to fit.
        // FOV of 30° gives a natural look without extreme foreshortening.
        float fovRadians = MathF.PI / 6f; // 30°
        float halfFovTan = MathF.Tan(fovRadians * 0.5f);

        // Camera distance so the model fills ~90% of view height at zoom=1
        float radius = Math.Max(_viewController.ModelRadius, 0.001f);
        float cameraDistance = (radius / halfFovTan) / _viewController.Zoom;

        // Near/far clamped by distance-to-target so pan can move the eye
        // away from origin without vertices falling outside the frustum.
        float farScale = Math.Max(_viewController.CameraTarget.Length() / cameraDistance + 100f, 100f);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fovRadians, aspect, cameraDistance * 0.01f, cameraDistance * farScale);

        // View matrix: camera sits at (target + eyeOffset) looking at target.
        // eyeOffset is along -Y in world space (pre-LookAt); LookAt handles Z-up.
        var target = _viewController.CameraTarget;
        var eye = target + new Vector3(0, -cameraDistance, 0);
        var view = Matrix4x4.CreateLookAt(eye, target, Vector3.UnitZ);

        // Cache matrices + viewport so pointer handlers can unproject cursor positions.
        _lastProjection = projection;
        _lastView = view;
        _lastViewportWidth = width;
        _lastViewportHeight = height;
        _lastCameraDistance = cameraDistance;

        // Model matrix: rotate the model (vertices are pre-centered at origin).
        // No scale needed — perspective + camera distance handles framing.
        // The view matrix (LookAt with Z-up) already handles coordinate conversion,
        // so no extra Z-up to Y-up tilt is needed here.
        var m = Matrix4x4.CreateRotationZ(_viewController.RotationY);
        m = Matrix4x4.CreateRotationX(_viewController.RotationX) * m;

        var modelMatrix = m;

        // Use shader
        _gl.UseProgram(_shaderManager!.ShaderProgram);

        // Set uniforms
        _shaderManager!.SetUniformMatrix4("model", modelMatrix);
        _shaderManager!.SetUniformMatrix4("view", view);
        _shaderManager!.SetUniformMatrix4("projection", projection);

        // Debug logging - only log once per model
        if (_logOncePerModel)
        {
            _logOncePerModel = false;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Render: camDist={cameraDistance:F3}, radius={_viewController.ModelRadius:F3}");
        }

        // Lighting - match NWN toolset brightness with higher ambient fill
        // #2026: NWN textures already carry painted-in shading. A strong
        // directional light fights that painted detail and exposes
        // low-poly facets (the Aurora toolset renders with minimal
        // directional contribution). Keep ambient high so the texture
        // dominates, and add a gentle directional term for subtle
        // surface cues.
        var lightDir = Vector3.Normalize(new Vector3(0.3f, -0.5f, 0.8f));
        _shaderManager!.SetUniformVec3("lightDir", lightDir);
        _shaderManager!.SetUniformVec3("lightColor", new Vector3(0.15f, 0.15f, 0.15f));
        _shaderManager!.SetUniformVec3("ambientColor", new Vector3(0.95f, 0.95f, 0.95f));
        _shaderManager!.SetUniformInt("debugMode", _debugMode);

        // Bind VAO and draw
        _gl.BindVertexArray(_vao);

        // Draw each mesh with its texture
        foreach (var drawCall in _meshDrawCalls)
        {
            if (drawCall.IndexCount == 0) continue;

            // Check if we have a texture for this mesh (direct or remapped)
            var resolvedTexture = drawCall.TextureName;
            if (!string.IsNullOrEmpty(resolvedTexture) && !_textureCache.ContainsKey(resolvedTexture)
                && _textureRemapping.TryGetValue(resolvedTexture, out var remapped))
            {
                resolvedTexture = remapped;
            }

            bool hasTexture = !string.IsNullOrEmpty(resolvedTexture) &&
                             _textureCache.TryGetValue(resolvedTexture, out var texId) && texId != 0;

            if (hasTexture)
            {
                _shaderManager!.SetUniformBool("hasTexture", true);
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, _textureCache[resolvedTexture!]);
                _shaderManager!.SetUniformInt("diffuseTexture", 0);
            }
            else
            {
                _shaderManager!.SetUniformBool("hasTexture", false);
                _shaderManager!.SetUniformVec3("flatColor", new Vector3(0.6f, 0.6f, 0.6f)); // Neutral gray for untextured meshes
            }

            // Use unsafe pointer for DrawElements offset
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)drawCall.IndexCount,
                    DrawElementsType.UnsignedInt, (void*)drawCall.IndexOffset);
            }
        }

        // Check for errors after drawing
        var drawErr = _gl.GetError();
        if (drawErr != GLEnum.NoError)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"GL error after draw: {drawErr}");
        }

        _gl.BindVertexArray(0);
    }

    private void UpdateMeshBuffers()
    {
        if (_gl == null || _model == null)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"UpdateMeshBuffers called but gl={_gl != null}, model={_model != null}");
            return;
        }

        try
        {
            UpdateMeshBuffersCore();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"UpdateMeshBuffers failed for model '{_model?.Name}': {ex.GetType().Name}: {ex.Message}");
            _meshDrawCalls.Clear();
            _indexCount = 0;
            _model = null; // Prevent repeated crash on next render frame
        }
    }

    private void UpdateMeshBuffersCore()
    {
        if (_gl == null || _model == null) return;

        _meshDrawCalls.Clear();

        // Two-pass vertex collection:
        // Pass 1: Collect world-space vertices and compute geometric center
        // Pass 2: Subtract center from all positions so mesh is centered at origin
        // This ensures rotation pivots around the geometric center, not world origin.

        var vertices = new List<float>();
        var indices = new List<uint>();
        var nanFlatIndices = new HashSet<int>(); // Flat vertex indices with NaN data (for bounds exclusion)
        uint baseVertex = 0;

        int meshIndex = 0;
        int skippedMeshes = 0;
        int skippedTrimeshes = 0;
        var allMeshes = _model.GetMeshNodes().ToList();

        // #1676/#2057: CEP models often have both skin meshes and trimeshes. Tiny trimeshes
        // (<30 verts) that share a skin's bitmap are bone visualizations causing artifacts.
        // Trimeshes with unique bitmaps are real geometry (tails, manes, fangs) — keep them.
        bool hasSkins = allMeshes.Any(m => m is Radoub.Formats.Mdl.MdlSkinNode && m.Render && m.Vertices.Length > 0);
        var skinBitmaps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (hasSkins)
        {
            foreach (var m in allMeshes)
            {
                if (m is Radoub.Formats.Mdl.MdlSkinNode && m.Render && m.Vertices.Length > 0)
                {
                    if (!string.IsNullOrEmpty(m.Bitmap) && !m.Bitmap.Equals("null", StringComparison.OrdinalIgnoreCase))
                        skinBitmaps.Add(m.Bitmap);
                }
            }
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"  Model '{_model.Name}': Has skin meshes — will filter tiny trimeshes sharing skin textures ({string.Join(", ", skinBitmaps)})");
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"  Model '{_model.Name}': {allMeshes.Count} mesh nodes: {string.Join(", ", allMeshes.Select(m => m.Name))}");
        foreach (var mesh in allMeshes)
        {
            meshIndex++;

            // Honor the MDL Render flag — meshes with Render=false are invisible
            // (e.g., animation-only geometry, hidden internal meshes, stale body parts)
            if (!mesh.Render)
            {
                skippedMeshes++;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Skipping mesh {meshIndex} '{mesh.Name}': Render=false");
                continue;
            }

            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
            {
                skippedMeshes++;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Skipping mesh {meshIndex}: verts={mesh.Vertices.Length}, faces={mesh.Faces.Length}");
                continue;
            }

            bool isSkinMesh = mesh is Radoub.Formats.Mdl.MdlSkinNode;

            // #1676/#2057: In models with skins, skip tiny trimeshes (<30 verts) that share
            // a skin bitmap (bone visualizations). Keep trimeshes with unique bitmaps — they're
            // real geometry (tails, manes, fangs, accessories).
            if (MeshSkipHeuristic.ShouldSkipTrimesh(hasSkins, isSkinMesh, mesh.Vertices.Length, mesh.Bitmap, skinBitmaps))
            {
                skippedMeshes++;
                skippedTrimeshes++;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  Skipping tiny trimesh '{mesh.Name}' ({mesh.Vertices.Length} verts, bitmap='{mesh.Bitmap}'): shares skin texture in skin model");
                continue;
            }

            // All meshes: apply full hierarchy world transform.
            // Skin mesh vertices (m_pavVerts) are in bind-pose space — same treatment as trimesh.
            // m_aQBoneRefInv/m_aTBoneRefInv are inverse bind-pose matrices for runtime animation, not static display.
            var pose = GetCurrentPose();
            var worldTransform = ModelViewController.GetWorldTransform(mesh, pose);
            bool hasWorldTransform = worldTransform != Matrix4x4.Identity;

            // Count NaN vertices - we'll skip them during rendering
            var nanVertexIndices = new HashSet<int>();
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i];
                if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                {
                    nanVertexIndices.Add(i);
                }
            }
            if (nanVertexIndices.Count > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"  Mesh {meshIndex} '{mesh.Name}': {nanVertexIndices.Count}/{mesh.Vertices.Length} NaN vertices will be skipped — possible parser issue");
            }
            var hasUVs = mesh.TextureCoords.Length > 0 && mesh.TextureCoords[0].Length == mesh.Vertices.Length;

            // Debug: check for data consistency issues
            if (mesh.TextureCoords.Length > 0 && mesh.TextureCoords[0].Length != mesh.Vertices.Length)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"  Mesh {meshIndex} '{mesh.Name}' UV count mismatch: {mesh.TextureCoords[0].Length} UVs vs {mesh.Vertices.Length} vertices");
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"  MESH '{mesh.Name}': bitmap='{mesh.Bitmap}', isSkin={isSkinMesh}, hasXform={hasWorldTransform}, " +
                $"verts={mesh.Vertices.Length}, faces={mesh.Faces.Length}");

            // Resolve texture name: use mesh bitmap, falling back to model name for
            // empty/NULL bitmaps (common in skin meshes and some simple creatures)
            var rawBitmap = mesh.Bitmap?.ToLowerInvariant();
            if (string.IsNullOrEmpty(rawBitmap) || rawBitmap == "null")
                rawBitmap = _model.Name?.ToLowerInvariant();

            // Track draw call for this mesh
            var drawCall = new MeshDrawCall
            {
                IndexOffset = indices.Count * sizeof(uint),
                TextureName = rawBitmap
            };

            // #2026: Pick normal source based on how the mesh encodes hard
            // edges. Multi-smoothgroup meshes (heads) use SurfaceId bitmasks;
            // stored per-vertex normals are unreliable BioWare-compiler
            // output. Single-smoothgroup meshes (bodies) encode hard edges
            // by duplicating vertices with different stored normals, so the
            // stored data IS the source of truth.
            var distinctSmoothgroups = new HashSet<int>();
            foreach (var face in mesh.Faces) distinctSmoothgroups.Add(face.SurfaceId);
            bool hasStoredNormals = mesh.Normals != null && mesh.Normals.Length == mesh.Vertices.Length;
            bool useStoredNormals = distinctSmoothgroups.Count <= 1 && hasStoredNormals;

            Vector3[] cornerNormals;
            if (useStoredNormals)
            {
                // One normal per face-corner, sourced from the stored per-vertex array.
                cornerNormals = new Vector3[mesh.Faces.Length * 3];
                for (int f = 0; f < mesh.Faces.Length; f++)
                {
                    var face = mesh.Faces[f];
                    cornerNormals[f * 3 + 0] = face.VertexIndex0 < mesh.Normals!.Length ? mesh.Normals[face.VertexIndex0] : Vector3.UnitZ;
                    cornerNormals[f * 3 + 1] = face.VertexIndex1 < mesh.Normals.Length ? mesh.Normals[face.VertexIndex1] : Vector3.UnitZ;
                    cornerNormals[f * 3 + 2] = face.VertexIndex2 < mesh.Normals.Length ? mesh.Normals[face.VertexIndex2] : Vector3.UnitZ;
                }
            }
            else
            {
                cornerNormals = SmoothGroupNormals.ComputePerCorner(mesh.Vertices, mesh.Faces);
            }

            // Build per-corner attribute arrays (3 corners per face, in face order).
            int cornerCount = mesh.Faces.Length * 3;
            var cornerPositions = new Vector3[cornerCount];
            var cornerNormalsWorld = new Vector3[cornerCount];
            var cornerUVs = new Vector2[cornerCount];
            var cornerDrop = new bool[cornerCount];

            for (int f = 0; f < mesh.Faces.Length; f++)
            {
                var face = mesh.Faces[f];
                int[] vs = { face.VertexIndex0, face.VertexIndex1, face.VertexIndex2 };

                bool faceInvalid =
                    vs[0] >= mesh.Vertices.Length ||
                    vs[1] >= mesh.Vertices.Length ||
                    vs[2] >= mesh.Vertices.Length ||
                    nanVertexIndices.Contains(vs[0]) ||
                    nanVertexIndices.Contains(vs[1]) ||
                    nanVertexIndices.Contains(vs[2]);

                for (int c = 0; c < 3; c++)
                {
                    int ci = f * 3 + c;
                    if (faceInvalid)
                    {
                        cornerDrop[ci] = true;
                        continue;
                    }

                    var localVertex = mesh.Vertices[vs[c]];
                    var worldPos = hasWorldTransform
                        ? ModelViewController.TransformPosition(localVertex, worldTransform)
                        : localVertex;
                    cornerPositions[ci] = worldPos;

                    var localNormal = cornerNormals[ci];
                    var worldNormal = hasWorldTransform
                        ? ModelViewController.TransformNormal(localNormal, worldTransform)
                        : localNormal;
                    cornerNormalsWorld[ci] = worldNormal;

                    if (hasUVs)
                        cornerUVs[ci] = mesh.TextureCoords[0][vs[c]];
                    else
                        cornerUVs[ci] = Vector2.Zero;
                }
            }

            // Weld corners sharing (pos, normal, UV). Corners across disjoint
            // smoothgroups have different normals so they stay separate; UV
            // seams produce different UVs and also stay separate.
            var weld = VertexWelder.Build(cornerPositions, cornerNormalsWorld, cornerUVs, cornerDrop);
            int meshIndexStart = indices.Count;

            // Emit vertex buffer: one entry per unique (pos, normal, UV).
            var emitted = new bool[weld.OutputVertexCount];
            for (int ci = 0; ci < cornerCount; ci++)
            {
                int local = weld.IndexRemap[ci];
                if (local < 0 || emitted[local]) continue;
                emitted[local] = true;

                vertices.Add(cornerPositions[ci].X);
                vertices.Add(cornerPositions[ci].Y);
                vertices.Add(cornerPositions[ci].Z);
                vertices.Add(cornerNormalsWorld[ci].X);
                vertices.Add(cornerNormalsWorld[ci].Y);
                vertices.Add(cornerNormalsWorld[ci].Z);
                vertices.Add(cornerUVs[ci].X);
                vertices.Add(cornerUVs[ci].Y);
            }

            // Emit indices: each face contributes 3 corner indices through the weld map.
            for (int f = 0; f < mesh.Faces.Length; f++)
            {
                int i0 = weld.IndexRemap[f * 3 + 0];
                int i1 = weld.IndexRemap[f * 3 + 1];
                int i2 = weld.IndexRemap[f * 3 + 2];
                if (i0 < 0 || i1 < 0 || i2 < 0) continue;            // dropped (NaN/invalid)
                if (i0 == i1 || i1 == i2 || i0 == i2) continue;      // degenerate after welding

                indices.Add(baseVertex + (uint)i0);
                indices.Add(baseVertex + (uint)i1);
                indices.Add(baseVertex + (uint)i2);
            }

            if (weld.WeldedCount > 0)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  Mesh '{mesh.Name}': welded {weld.WeldedCount}/{cornerCount} corners " +
                    $"-> {weld.OutputVertexCount} unique in GPU buffer");
            }

            drawCall.IndexCount = indices.Count - meshIndexStart;
            if (drawCall.IndexCount > 0)
                _meshDrawCalls.Add(drawCall);

            baseVertex += (uint)weld.OutputVertexCount;
        }

        _indexCount = indices.Count;

        // Pass 2: Compute geometric center from vertex data, then subtract it
        // from all vertex positions so the mesh is centered at origin.
        // This ensures rotation pivots around the geometric center.
        if (vertices.Count >= 8)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            for (int i = 0; i < vertices.Count; i += 8)
            {
                // Skip NaN-zeroed vertices — they'd incorrectly pull bounds toward origin
                if (nanFlatIndices.Contains(i / 8)) continue;

                minX = Math.Min(minX, vertices[i]);
                maxX = Math.Max(maxX, vertices[i]);
                minY = Math.Min(minY, vertices[i + 1]);
                maxY = Math.Max(maxY, vertices[i + 1]);
                minZ = Math.Min(minZ, vertices[i + 2]);
                maxZ = Math.Max(maxZ, vertices[i + 2]);
            }

            // Guard: if all vertices were NaN, bounds are still at MaxValue — skip centering
            if (minX == float.MaxValue)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "All vertices were NaN — skipping centering");
                _viewController.UpdateBounds(1.0f, false);
            }
            else
            {

            var center = new Vector3(
                (minX + maxX) * 0.5f,
                (minY + maxY) * 0.5f,
                (minZ + maxZ) * 0.5f);

            // Subtract center from all vertex positions (every 8 floats, first 3 are XYZ)
            for (int i = 0; i < vertices.Count; i += 8)
            {
                vertices[i]     -= center.X;
                vertices[i + 1] -= center.Y;
                vertices[i + 2] -= center.Z;
            }

            // Calculate radius from bounds
            float sizeX = maxX - minX;
            float sizeY = maxY - minY;
            float sizeZ = maxZ - minZ;
            var computedRadius = Math.Max(Math.Max(sizeX, sizeY), sizeZ) * 0.5f;
            _viewController.UpdateBounds(computedRadius, true);

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Vertex bounds: X=[{minX:F2},{maxX:F2}] Y=[{minY:F2},{maxY:F2}] Z=[{minZ:F2},{maxZ:F2}], " +
                $"pre-centered by ({center.X:F2}, {center.Y:F2}, {center.Z:F2}), radius={computedRadius:F2}");
            } // else (valid bounds)
        }

        // Mesh composition analysis for model completeness indicator (#1873)
        int totalMeshCount = allMeshes.Count;
        int skinMeshCount = allMeshes.Count(m => m is Radoub.Formats.Mdl.MdlSkinNode && m.Render && m.Vertices.Length > 0);
        int hiddenMeshCount = allMeshes.Count(m => !m.Render);

        UnifiedLogger.LogApplication(LogLevel.INFO, $"UpdateMeshBuffers: model={_model?.Name ?? "null"}, {_meshDrawCalls.Count} meshes ({skippedMeshes} skipped), {vertices.Count / 8} vertices, {_indexCount / 3} triangles");

        // Notify listeners of mesh composition
        var meshInfo = new ModelMeshInfo(totalMeshCount, skinMeshCount, hiddenMeshCount, skippedTrimeshes);
        if (Dispatcher.UIThread.CheckAccess())
            MeshInfoChanged?.Invoke(this, meshInfo);
        else
            Dispatcher.UIThread.Post(() => MeshInfoChanged?.Invoke(this, meshInfo));

        // Determine preview state based on rendered geometry and emitter nodes
        if (_indexCount == 0)
        {
            SetPreviewState(_model != null ? PreviewState.NotAvailable : PreviewState.None);
        }
        else if (_model?.HasEmitterNodes() == true)
        {
            SetPreviewState(PreviewState.Incomplete);
        }
        else
        {
            SetPreviewState(PreviewState.Complete);
        }

        if (_indexCount == 0) return;

        // Upload to GPU
        _gl.BindVertexArray(_vao);

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        var vertexArray = vertices.ToArray();
        _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(vertexArray.Length * sizeof(float)),
            new ReadOnlySpan<float>(vertexArray), BufferUsageARB.StaticDraw);

        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _ebo);
        var indexArray = indices.ToArray();
        _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indexArray.Length * sizeof(uint)),
            new ReadOnlySpan<uint>(indexArray), BufferUsageARB.StaticDraw);

        // Set up vertex attributes
        unsafe
        {
            // Position (location 0)
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Normal (location 1)
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            // TexCoord (location 2)
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
        }

        _gl.BindVertexArray(0);
    }

    private void UpdateTextures()
    {
        if (_gl == null || _textureService == null || _model == null) return;

        _textureRemapping.Clear();

        // Collect unique texture names from meshes
        var textureNames = new HashSet<string>();
        foreach (var mesh in _model.GetMeshNodes())
        {
            if (!string.IsNullOrEmpty(mesh.Bitmap))
                textureNames.Add(mesh.Bitmap.ToLowerInvariant());
        }

        // Delete old textures that are no longer needed
        var toRemove = new List<string>();
        foreach (var kvp in _textureCache)
        {
            if (!textureNames.Contains(kvp.Key))
            {
                _gl.DeleteTexture(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var key in toRemove)
        {
            _textureCache.Remove(key);
            _pltTextureNames.Remove(key);
        }

        // Load new textures
        UnifiedLogger.LogApplication(LogLevel.DEBUG,
            $"UpdateTextures: {textureNames.Count} textures, colors: skin={_colorIndices.Skin}, hair={_colorIndices.Hair}, " +
            $"metal1={_colorIndices.Metal1}, metal2={_colorIndices.Metal2}, cloth1={_colorIndices.Cloth1}, cloth2={_colorIndices.Cloth2}");

        var failedTextures = new List<string>();
        foreach (var texName in textureNames)
        {
            if (_textureCache.ContainsKey(texName))
                continue; // Already loaded

            try
            {
                var textureData = _preferBifTextures
                    ? _textureService.LoadTexturePreferBIFWithKind(texName, _colorIndices)
                    : _textureService.LoadTextureWithKind(texName, _colorIndices);
                if (textureData == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Texture '{texName}' returned null");
                    failedTextures.Add(texName);
                    continue;
                }

                var (width, height, pixels, isPlt) = textureData.Value;
                var texId = UploadTexture(width, height, pixels);
                if (texId != 0)
                {
                    _textureCache[texName] = texId;
                    if (isPlt) _pltTextureNames.Add(texName);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Loaded texture '{texName}' ({width}x{height}) -> texId={texId}, isPlt={isPlt}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load texture '{texName}': {ex.Message}");
                failedTextures.Add(texName);
            }
        }

        // For textures that failed to load, try model name as fallback.
        // NWN creature models often have meshes with bitmap set to node name (e.g. "torso_g")
        // instead of an actual texture. The real texture is typically the model name (e.g. "c_curst2").
        if (failedTextures.Count > 0)
        {
            var modelTexture = _model.Name?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(modelTexture) && !_textureCache.ContainsKey(modelTexture))
            {
                var fallbackData = _preferBifTextures
                    ? _textureService.LoadTexturePreferBIFWithKind(modelTexture, _colorIndices)
                    : _textureService.LoadTextureWithKind(modelTexture, _colorIndices);
                if (fallbackData != null)
                {
                    var (w, h, px, isPlt) = fallbackData.Value;
                    var fallbackId = UploadTexture(w, h, px);
                    if (fallbackId != 0)
                    {
                        _textureCache[modelTexture] = fallbackId;
                        if (isPlt) _pltTextureNames.Add(modelTexture);
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"  Loaded model fallback texture '{modelTexture}' ({w}x{h}) -> texId={fallbackId}, isPlt={isPlt}");
                    }
                }
            }

            // Map failed textures to model texture if it loaded successfully
            if (!string.IsNullOrEmpty(modelTexture) && _textureCache.ContainsKey(modelTexture))
            {
                foreach (var failed in failedTextures)
                {
                    _textureRemapping[failed] = modelTexture;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"  Remapped texture '{failed}' -> '{modelTexture}' (model fallback)");
                }
            }
        }
    }

    private uint UploadTexture(int width, int height, byte[] rgba)
    {
        if (_gl == null) return 0;

        var texId = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, texId);

        // Upload RGBA data
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8,
            (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte,
            new ReadOnlySpan<byte>(rgba));

        // Set texture parameters
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

        // Generate mipmaps for better quality at distance
        _gl.GenerateMipmap(TextureTarget.Texture2D);

        _gl.BindTexture(TextureTarget.Texture2D, 0);

        return texId;
    }

    /// <summary>
    /// Determines if a texture name represents a body/skin texture that should render behind armor.
    /// NWN body part textures follow naming conventions like:
    /// - pXX_bodyYYY (player body parts)
    /// - pXX_headYYY (player heads)
    /// - cXX_bodyYYY (creature body parts)
    /// Armor textures don't contain these patterns.
    /// </summary>
    private static bool IsBodyTexture(string? textureName)
    {
        if (string.IsNullOrEmpty(textureName))
            return false;

        var name = textureName.ToLowerInvariant();

        // Body part patterns for player/creature models
        // These render BEHIND armor to prevent z-fighting
        return name.Contains("_body") ||
               name.Contains("_head") ||
               name.Contains("_neck") ||
               name.Contains("_hand") ||
               name.Contains("_foot") ||
               name.Contains("_shin") ||
               name.Contains("_thigh") ||
               name.Contains("_bicep") ||
               name.Contains("_forearm") ||
               name.Contains("_pelvis");
    }

    /// <summary>
    /// Thread-safe state update — marshals to UI thread if needed.
    /// </summary>
    private void SetPreviewState(PreviewState newState)
    {
        if (_previewState == newState) return;
        _previewState = newState;

        if (Dispatcher.UIThread.CheckAccess())
        {
            PreviewStateChanged?.Invoke(this, newState);
        }
        else
        {
            Dispatcher.UIThread.Post(() => PreviewStateChanged?.Invoke(this, newState));
        }
    }
}
