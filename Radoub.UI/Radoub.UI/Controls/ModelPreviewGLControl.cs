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
using Radoub.UI.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Silk.NET.OpenGL;

namespace Radoub.UI.Controls;

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
public partial class ModelPreviewGLControl : OpenGlControlBase
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

    // OpenGL ES vs desktop GL — detected in OnOpenGlInit, reused for the particle
    // shader version preamble so particles match the mesh shader's profile (#2395).
    private bool _isOpenGLES;

    // Geometric center the mesh path subtracts from every vertex so the model sits at
    // origin (UpdateMeshBuffersCore Pass 2). Particle sim runs in RAW (un-centered) model
    // space, so the render path subtracts this same center to align particles with the
    // mesh (#2395). Stays Vector3.Zero until a mesh is built.
    private Vector3 _modelCenter;
    private readonly Dictionary<string, uint> _textureCache = new();
    // Alpha profile (Opaque/Binary/Graded) per cached texture, classified once at upload from
    // the RGBA bytes. Drives the per-mesh transparency mode (#2540). Keyed like _textureCache.
    private readonly Dictionary<string, AlphaProfile> _textureAlphaProfile = new();
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
        // Raw transparency inputs from the mesh node (#2540). The MaterialMode is resolved in the
        // draw loop (not here): mesh buffers are built before textures upload, so the texture's
        // AlphaProfile — which ClassifyMesh needs — isn't known at population time.
        public float MeshAlpha;
        public int TransparencyHint;
        // Geometric centroid in the same centered space as the GPU vertex buffer (#2540).
        // Used to depth-sort Transparent meshes back-to-front.
        public Vector3 Centroid;
    }

    // Discard cutoff for Cutout meshes (#2540). The real engine alpha-tests at GL_GREATER 0.1
    // (xoreos modelnode.cpp:440, behavioral ref only — GPLv3, not copied), keeping the soft fur
    // tips of a mane/fur card instead of carving them away. 0.5 (rollnw's hard-silhouette default)
    // discarded the wispy upper-mane fragments and left black triangular voids behind them; 0.1
    // keeps the wisps, matching the in-game dire-tiger mane.
    private const float CutoutAlphaThreshold = 0.1f;

    // Alpha-test floor applied to Transparent meshes during the blend pass (#2540). The engine
    // keeps GL_ALPHA_TEST on at GL_GREATER 0.1 while blending (xoreos modelnode.cpp:440/770,
    // behavioral ref only — GPLv3, not copied), so near-zero-alpha fur/mane texels are discarded
    // before they write colour or sort as ghost layers. This removes the zod-rat emitter
    // show-through and the dire-tiger mane gaps while still blending the soft fur edges.
    private const float TransparentAlphaFloor = 0.1f;

    private MdlModel? _model;
    private TextureService? _textureService;
    private bool _preferBifTextures;

    // PLT color indices for texture rendering
    private PltColorIndices _colorIndices = new();
    private bool _needsTextureUpdate;
    private bool _needsMeshUpdate;
    private bool _logOncePerModel;
    // One-shot per model: log resolved transparency modes for non-opaque meshes (#2540).
    private bool _logTransparencyOncePerModel;

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
    private float _animSpeed = 1.0f;
    private bool _animPlaying;
    private DateTime _animLastTick = DateTime.UtcNow;
    private Dictionary<string, ModelViewController.NodePose>? _cachedPose;
    private Avalonia.Threading.DispatcherTimer? _animTimer;

    // Particle simulation lifecycle (#2395). One ParticleSystem per emitter node in the
    // model. Driven by the same 30fps clock as animation (OnAnimTick), but runs even when
    // no skeletal animation is selected/playing — emitters animate at idle. NO rendering
    // here yet; this task spawns/updates particles in memory and proves it via logs.
    private readonly List<(MdlEmitterNode node, Radoub.UI.Particles.CompiledEmitter emitter, Radoub.UI.Particles.ParticleSystem system)> _particleSystems = new();
    private DateTime _particleLastTick = DateTime.UtcNow;
    private DateTime _lastParticleLogTime = DateTime.MinValue;

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
    public record ModelMeshInfo(int TotalMeshes, int SkinMeshCount, int HiddenMeshCount);

    /// <summary>
    /// Raised on the UI thread after model mesh analysis completes.
    /// </summary>
    public event EventHandler<ModelMeshInfo>? MeshInfoChanged;

    /// <summary>
    /// Raised on the UI thread after textures load. The bool is true when a non-base
    /// model (HAK/Module) had at least one texture fall back to a base-game (BIF) stub,
    /// so the preview skin may not match the intended asset (#1758).
    /// </summary>
    public event EventHandler<bool>? TextureSourceChanged;

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
            _logTransparencyOncePerModel = true;
            _viewController.CenterCamera();
            RebuildParticleSystems(value);
            if (value == null)
            {
                SetPreviewState(PreviewState.None);
                MeshInfoChanged?.Invoke(this, new ModelMeshInfo(0, 0, 0));
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
            _isOpenGLES = isOpenGLES;  // reused by the particle shader preamble (#2395)
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
                _textureAlphaProfile.Clear();
                _pltTextureNames.Clear();

                // Clean up buffers and program
                _gl.DeleteBuffer(_vbo);
                _gl.DeleteBuffer(_ebo);
                _gl.DeleteVertexArray(_vao);
                _shaderManager?.Cleanup();
                _shaderManager = null;

                // Particle GL resources (#2395)
                CleanupParticleGl();

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
        _gl.DepthFunc(DepthFunction.Less);  // Opaque geometry: strict Less avoids coplanar z-fighting.
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
        _shaderManager!.SetUniformVec3("lightColor", new Vector3(0.25f, 0.25f, 0.25f));
        _shaderManager!.SetUniformVec3("ambientColor", new Vector3(0.70f, 0.70f, 0.70f));
        _shaderManager!.SetUniformInt("debugMode", _debugMode);

        // Bind VAO and draw
        _gl.BindVertexArray(_vao);

        // Resolve a draw call's texture (direct or remapped), whether it has one, and its
        // MaterialMode (#2540). Shared by the routing pass and the actual draw.
        (string? texture, bool hasTexture, MaterialMode mode) ResolveDrawCall(in MeshDrawCall drawCall)
        {
            var resolvedTexture = drawCall.TextureName;
            if (!string.IsNullOrEmpty(resolvedTexture) && !_textureCache.ContainsKey(resolvedTexture)
                && _textureRemapping.TryGetValue(resolvedTexture, out var remapped))
            {
                resolvedTexture = remapped;
            }

            bool hasTexture = !string.IsNullOrEmpty(resolvedTexture) &&
                             _textureCache.TryGetValue(resolvedTexture, out var texId) && texId != 0;

            var profile = hasTexture && _textureAlphaProfile.TryGetValue(resolvedTexture!, out var p)
                ? p : AlphaProfile.Opaque;
            var mode = MeshTransparency.ClassifyMesh(drawCall.MeshAlpha, drawCall.TransparencyHint, profile);
            return (resolvedTexture, hasTexture, mode);
        }

        // Draws one mesh with its texture and transparency uniforms.
        void DrawMesh(in MeshDrawCall drawCall)
        {
            var (resolvedTexture, hasTexture, mode) = ResolveDrawCall(drawCall);

            if (_logTransparencyOncePerModel && mode != MaterialMode.Opaque)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  [Transparency] '{resolvedTexture}' -> {mode} (alpha={drawCall.MeshAlpha:F2}, " +
                    $"hint={drawCall.TransparencyHint})");
            }

            _shaderManager!.SetUniformInt("meshMode", (int)mode);
            _shaderManager!.SetUniformFloat("alphaThreshold", CutoutAlphaThreshold);
            _shaderManager!.SetUniformFloat("transparentAlphaFloor", TransparentAlphaFloor);
            _shaderManager!.SetUniformFloat("meshAlpha", drawCall.MeshAlpha);

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

            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)drawCall.IndexCount,
                    DrawElementsType.UnsignedInt, (void*)drawCall.IndexOffset);
            }
        }

        // Pass 1a: opaque meshes only, depth write ON, depth func Less (set above), no blend.
        // Strict Less is required for normal opaque/part-composited geometry (e.g. armored
        // humanoids) — LEQUAL there causes coplanar-face z-fighting. Cutout and Transparent meshes
        // are deferred so they never run under the opaque depth-func.
        var cutoutCalls = new List<MeshDrawCall>();
        var transparentCalls = new List<MeshDrawCall>();
        foreach (var drawCall in _meshDrawCalls)
        {
            if (drawCall.IndexCount == 0) continue;
            var mode = ResolveDrawCall(drawCall).mode;
            if (mode == MaterialMode.Transparent) { transparentCalls.Add(drawCall); continue; }
            if (mode == MaterialMode.Cutout) { cutoutCalls.Add(drawCall); continue; }
            DrawMesh(drawCall);
        }

        // Pass 1b: cutout meshes (#2540), depth write ON + blend + depth func LEQUAL. LEQUAL is
        // scoped to ONLY these fur/mane cards: it lets equal-depth fragments of overlapping cards
        // both draw so the layered mane fills without dark gaps, while the kept soft tips composite
        // by alpha (no black opaque texels). Depth write on keeps it order-independent (no shards).
        // Restored to Less after so it cannot leak into the transparent pass or the next frame.
        if (cutoutCalls.Count > 0)
        {
            _gl.DepthFunc(DepthFunction.Lequal);
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            foreach (var drawCall in cutoutCalls)
                DrawMesh(drawCall);
            _gl.Disable(EnableCap.Blend);
            _gl.DepthFunc(DepthFunction.Less);
        }

        // Pass 2: transparent meshes, sorted back-to-front, alpha-blended, depth writes off so
        // overlapping translucent layers don't cull each other. Depth TEST stays on so opaque
        // geometry still occludes them. State is restored after (matches the particle path).
        if (transparentCalls.Count > 0)
        {
            var mv = m * view; // model-rotation then view: centroid -> view space
            var keys = new (int hint, float depth)[transparentCalls.Count];
            for (int i = 0; i < transparentCalls.Count; i++)
            {
                // OpenGL view space looks down -Z, so a more-negative Z is farther. Negate so a
                // larger 'depth' means farther, matching SortBackToFront's descending order.
                float viewZ = Vector3.Transform(transparentCalls[i].Centroid, mv).Z;
                keys[i] = (transparentCalls[i].TransparencyHint, -viewZ);
            }
            var order = MeshTransparency.SortBackToFront(keys);

            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _gl.DepthMask(false);
            foreach (var idx in order)
                DrawMesh(transparentCalls[idx]);
            _gl.DepthMask(true);
            _gl.Disable(EnableCap.Blend);
        }
        _logTransparencyOncePerModel = false;

        // Check for errors after drawing
        var drawErr = _gl.GetError();
        if (drawErr != GLEnum.NoError)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"GL error after draw: {drawErr}");
        }

        _gl.BindVertexArray(0);

        // Particle billboards draw AFTER the opaque mesh, sharing the same model rotation
        // so they spin with the model. Uses its own program/VAO/VBO and restores all GL
        // state it touches. Wrapped so a particle GL failure can never break the mesh
        // render (CLAUDE.md: never throw into the render loop) (#2395).
        if (_particleSystems.Count > 0)
        {
            try
            {
                RenderParticles(modelMatrix, view, projection);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"[Particle] render failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }
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
        _modelCenter = Vector3.Zero;  // reset; set below if a valid geometric center is computed (#2395)

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
        // #2498: the 30-vertex shared-bitmap heuristic (#1676/#2057) was removed — it hid real
        // geometry that reuses the body texture (hands, necks, hair, dragon spikes, tongues).
        // Visibility now matches the Aurora engine: honor the MDL Render flag + drop empty meshes.
        var allMeshes = _model.GetMeshNodes().ToList();

        UnifiedLogger.LogApplication(LogLevel.INFO, $"  Model '{_model.Name}': {allMeshes.Count} mesh nodes: {string.Join(", ", allMeshes.Select(m => m.Name))}");
        foreach (var mesh in allMeshes)
        {
            meshIndex++;

            // Honor the MDL Render flag — meshes with Render=false are invisible
            // (e.g., animation-only geometry, hidden internal meshes, stale body parts).
            if (!MeshVisibility.ShouldRender(mesh.Render, mesh.Vertices.Length, mesh.Faces.Length))
            {
                skippedMeshes++;
                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"  Skipping mesh {meshIndex} '{mesh.Name}': Render={mesh.Render}, verts={mesh.Vertices.Length}, faces={mesh.Faces.Length}");
                continue;
            }

            bool isSkinMesh = mesh is Radoub.Formats.Mdl.MdlSkinNode;

            // All meshes: apply full hierarchy world transform.
            var pose = GetCurrentPose();
            var worldTransform = ModelViewController.GetWorldTransform(mesh, pose);
            bool hasWorldTransform = worldTransform != Matrix4x4.Identity;

            // Skin meshes (#2399 / R1): deform each vertex by its bone weights instead of applying
            // the single node transform. Without this the skin body renders frozen at bind pose
            // while bone-driven parts animate (robe head detaches, snakes stay flat). At bind pose
            // every skin matrix collapses to the mesh bind-world transform, so static creatures are
            // unaffected. Falls back to the rigid path if bone data is missing.
            Matrix4x4[]? skinMatrices = null;
            if (mesh is Radoub.Formats.Mdl.MdlSkinNode skinNode
                && skinNode.BoneNodeNames.Length > 0
                && skinNode.BoneWeights.Length == mesh.Vertices.Length
                && _model?.GeometryRoot != null)
            {
                skinMatrices = SkinMatrixBuilder.Build(skinNode, _model.GeometryRoot, pose);
            }

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

            UnifiedLogger.LogApplication(LogLevel.TRACE,
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
                TextureName = rawBitmap,
                MeshAlpha = mesh.Alpha,
                TransparencyHint = mesh.TransparencyHint
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
                    var localNormal = cornerNormals[ci];

                    Vector3 worldPos, worldNormal;
                    if (skinMatrices != null)
                    {
                        // Bone-weighted deformation: position blended through the skin matrices,
                        // normal rotated by the same blend (matched to vs[c] for stored normals).
                        var bw = ((Radoub.Formats.Mdl.MdlSkinNode)mesh).BoneWeights[vs[c]];
                        var vw = new SkinDeformer.VertexWeights(
                            bw.Bone0, bw.Weight0, bw.Bone1, bw.Weight1,
                            bw.Bone2, bw.Weight2, bw.Bone3, bw.Weight3);
                        bool hasInfluence = SkinDeformer.HasInfluence(vw, skinMatrices.Length);
                        if (hasInfluence)
                        {
                            worldPos = SkinDeformer.BlendVertex(localVertex, vw, skinMatrices);
                            worldNormal = Vector3.Normalize(SkinDeformer.BlendNormal(localNormal, vw, skinMatrices));
                        }
                        else
                        {
                            // No valid bone influence — place at the rigid bind-world position (the
                            // mesh's own transform) instead of letting it sit at the local origin.
                            worldPos = hasWorldTransform
                                ? ModelViewController.TransformPosition(localVertex, worldTransform)
                                : localVertex;
                            worldNormal = hasWorldTransform
                                ? ModelViewController.TransformNormal(localNormal, worldTransform)
                                : localNormal;
                        }
                    }
                    else
                    {
                        worldPos = hasWorldTransform
                            ? ModelViewController.TransformPosition(localVertex, worldTransform)
                            : localVertex;
                        worldNormal = hasWorldTransform
                            ? ModelViewController.TransformNormal(localNormal, worldTransform)
                            : localNormal;
                    }
                    cornerPositions[ci] = worldPos;
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
            // Also accumulate this mesh's world-space centroid for the transparency depth
            // sort (#2540). It is re-centered alongside the vertices in pass 2.
            var emitted = new bool[weld.OutputVertexCount];
            var centroidSum = Vector3.Zero;
            int centroidCount = 0;
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

                centroidSum += cornerPositions[ci];
                centroidCount++;
            }
            if (centroidCount > 0)
                drawCall.Centroid = centroidSum / centroidCount;

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
                UnifiedLogger.LogApplication(LogLevel.TRACE,
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

            // Store for the particle render path: particles sim in raw model space and
            // must subtract this same center to align with the centered mesh (#2395).
            _modelCenter = center;

            // Subtract center from all vertex positions (every 8 floats, first 3 are XYZ)
            for (int i = 0; i < vertices.Count; i += 8)
            {
                vertices[i]     -= center.X;
                vertices[i + 1] -= center.Y;
                vertices[i + 2] -= center.Z;
            }

            // Re-center each mesh centroid by the same offset so the transparency depth sort
            // (#2540) works in the centered space the GPU draws in. Struct in a List → rewrite.
            for (int i = 0; i < _meshDrawCalls.Count; i++)
            {
                var dc = _meshDrawCalls[i];
                dc.Centroid -= center;
                _meshDrawCalls[i] = dc;
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
        var meshInfo = new ModelMeshInfo(totalMeshCount, skinMeshCount, hiddenMeshCount);
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
