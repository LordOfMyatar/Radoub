// GPU-accelerated 3D Model Preview Control for Quartermaster
// Uses Silk.NET OpenGL with Avalonia's OpenGlControlBase
// Provides proper depth buffer, perspective-correct texture mapping, and per-pixel lighting

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Silk.NET.OpenGL;

namespace Quartermaster.Controls;

/// <summary>
/// GPU-accelerated control for rendering 3D model previews using OpenGL.
/// Provides proper depth buffer, perspective-correct texture mapping, and lighting.
/// </summary>
public class ModelPreviewGLControl : OpenGlControlBase
{
    private GL? _gl;
    private uint _shaderProgram;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private int _indexCount;
    private readonly Dictionary<string, uint> _textureCache = new();
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
    private float _rotationY = MathF.PI; // Default 180° so model faces camera
    private float _rotationX;
    private float _zoom = 1.0f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _modelRadius = 1.0f;
    private bool _hasVertexBounds;

    // PLT color indices for texture rendering
    private PltColorIndices _colorIndices = new();
    private bool _needsTextureUpdate;
    private bool _needsMeshUpdate;
    private bool _logOncePerModel;

    // Shader source code - GLSL ES 300 for ANGLE compatibility on Windows
    // Avalonia uses ANGLE which provides OpenGL ES, not desktop OpenGL
    private const string VertexShaderSource = @"#version 300 es
precision highp float;

layout (location = 0) in vec3 aPosition;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTexCoord;

out vec3 FragPos;
out vec3 Normal;
out vec2 TexCoord;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;

void main()
{
    FragPos = vec3(model * vec4(aPosition, 1.0));
    Normal = mat3(model) * aNormal;
    TexCoord = aTexCoord;
    gl_Position = projection * view * model * vec4(aPosition, 1.0);
}
";

    private const string FragmentShaderSource = @"#version 300 es
precision highp float;

out vec4 FragColor;

in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

uniform sampler2D diffuseTexture;
uniform vec3 lightDir;
uniform vec3 lightColor;
uniform vec3 ambientColor;
uniform bool hasTexture;
uniform vec3 flatColor;

void main()
{
    // Two-sided lighting: use abs() so thin surfaces (bat wings, dragon
    // membranes) are lit from both sides. The Aurora Engine renders these
    // single-layer polygons visible from both directions (#1867).
    vec3 norm = normalize(Normal);
    float diff = abs(dot(norm, lightDir));
    vec3 diffuse = diff * lightColor;
    vec3 ambient = ambientColor;

    vec3 baseColor;
    if (hasTexture) {
        baseColor = texture(diffuseTexture, TexCoord).rgb;
    } else {
        baseColor = flatColor;
    }

    vec3 result = (ambient + diffuse) * baseColor;

    // Gamma correction: NWN textures are sRGB; brighten midtones to match toolset look
    result = pow(result, vec3(1.0 / 1.6));

    FragColor = vec4(result, 1.0);
}
";

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
            CenterCamera();
            RequestNextFrameRendering();
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
            RequestNextFrameRendering();
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
            RequestNextFrameRendering();
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
            // Clear cached textures so they reload with new color indices
            ClearTextureCache();
            _needsTextureUpdate = true;
            RequestNextFrameRendering();
        }
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
        _rotationY += deltaY;
        _rotationX += deltaX;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Reset the view to default (facing front).
    /// </summary>
    public void ResetView()
    {
        _rotationY = MathF.PI;
        _rotationX = 0;
        _zoom = 1.0f;
        // Use vertex-computed bounds if available, otherwise safe defaults.
        // Don't call CenterCamera() — model stored bounds are unreliable
        // (they include the full skeleton hierarchy, not just rendered mesh).
        if (!_hasVertexBounds)
        {
            _cameraTarget = Vector3.Zero;
            _modelRadius = 1.0f;
        }
        RequestNextFrameRendering();
    }

    private void CenterCamera()
    {
        // Set safe defaults. Actual center and radius are computed from
        // rendered vertices in UpdateMeshBuffers(). The model's stored
        // BoundingMin/Max encompasses the full skeleton hierarchy and is
        // much larger than the visible mesh.
        _cameraTarget = Vector3.Zero;
        _modelRadius = 1.0f;
        _hasVertexBounds = false;
    }

    /// <summary>
    /// Calculate the world transform matrix for a node by walking up the parent chain.
    /// Combines position, rotation (quaternion), and scale from each ancestor.
    ///
    /// Transform order for each node: Scale first, then Rotate, then Translate (SRT)
    /// This means vertices are scaled, rotated, then positioned.
    ///
    /// For hierarchical transforms: Parent * Child
    /// So a vertex transforms as: RootTransform * ... * ParentTransform * NodeTransform * vertex
    /// </summary>
    private static Matrix4x4 GetWorldTransform(MdlNode? node)
    {
        // System.Numerics uses row-major convention where Vector3.Transform(v, M) = v * M
        // For hierarchical transforms: v_world = v_local * NodeLocal * ParentLocal * ... * RootLocal
        // So we need: worldTransform = NodeLocal * ParentLocal * ... * RootLocal
        // We walk leaf-to-root, accumulating: world = local * world
        //
        // Each local transform: vertex is scaled, rotated, then translated
        // In row-major: localTransform = S * R * T (applied left-to-right on row vector)
        var worldTransform = Matrix4x4.Identity;
        var current = node;

        while (current != null)
        {
            var scale = Matrix4x4.CreateScale(current.Scale);
            var rotation = Matrix4x4.CreateFromQuaternion(current.Orientation);
            var translation = Matrix4x4.CreateTranslation(current.Position);

            // Row-major local transform: S * R * T
            var localTransform = scale * rotation * translation;

            // Accumulate: node * parent * grandparent * ... * root
            worldTransform = worldTransform * localTransform;

            current = current.Parent;
        }

        return worldTransform;
    }

    /// <summary>
    /// Transform a position vector by a matrix.
    /// </summary>
    private static Vector3 TransformPosition(Vector3 position, Matrix4x4 matrix)
    {
        return Vector3.Transform(position, matrix);
    }

    /// <summary>
    /// Transform a normal vector by a matrix (ignores translation, handles non-uniform scale).
    /// </summary>
    private static Vector3 TransformNormal(Vector3 normal, Matrix4x4 matrix)
    {
        // For normals, we need the inverse transpose of the upper-left 3x3
        // For uniform scale and rotation only, we can simplify to just the rotation part
        // Extract rotation/scale portion and transform
        var transformed = Vector3.TransformNormal(normal, matrix);
        return Vector3.Normalize(transformed);
    }

    /// <summary>
    /// Apply bone-weighted skinning transform to a vertex position.
    /// The on-disk data contains INVERSE bind-pose transforms (world→bone space).
    /// To display in world space, we invert: v_world = Q_fwd * (v_local - T_inv)
    /// where Q_fwd = conjugate(Q_inv).
    /// </summary>
    private static Vector3 ApplySkinTransform(Vector3 vertex, int vertexIndex, Radoub.Formats.Mdl.MdlSkinNode skin)
    {
        var bw = skin.BoneWeights[vertexIndex];
        var result = Vector3.Zero;

        void Accumulate(int boneIndex, float weight)
        {
            if (weight <= 0 || boneIndex < 0) return;
            if (boneIndex >= skin.BoneQuaternions.Length || boneIndex >= skin.BoneTranslations.Length) return;

            // NWN runtime skinning formula: v_world = Q_stored * v_local + T_stored
            // Q_stored has W=-1 for identity-rotation bones, which causes Vector3.Transform
            // to NEGATE the vertex. This is intentional — the stored T compensates.
            // Use quaternion as-is (do NOT normalize W to +1).
            var q = skin.BoneQuaternions[boneIndex];
            var t = skin.BoneTranslations[boneIndex];
            var rotated = Vector3.Transform(vertex, q);
            result += weight * (rotated + t);
        }

        Accumulate(bw.Bone0, bw.Weight0);
        Accumulate(bw.Bone1, bw.Weight1);
        Accumulate(bw.Bone2, bw.Weight2);
        Accumulate(bw.Bone3, bw.Weight3);

        // Guard against NaN from invalid bone data or degenerate transforms
        if (float.IsNaN(result.X) || float.IsNaN(result.Y) || float.IsNaN(result.Z))
            return vertex; // Fall back to raw vertex

        return result;
    }

    /// <summary>
    /// Apply bone-weighted skinning rotation to a normal vector.
    /// Only applies the rotation component (no translation for normals).
    /// </summary>
    private static Vector3 ApplySkinNormalTransform(Vector3 normal, int vertexIndex, Radoub.Formats.Mdl.MdlSkinNode skin)
    {
        var bw = skin.BoneWeights[vertexIndex];
        var result = Vector3.Zero;

        void Accumulate(int boneIndex, float weight)
        {
            if (weight <= 0 || boneIndex < 0) return;
            if (boneIndex >= skin.BoneQuaternions.Length) return;

            // Use stored Q as-is — same formula as position transform (Q negates for W=-1 bones)
            var q = skin.BoneQuaternions[boneIndex];
            result += weight * Vector3.Transform(normal, q);
        }

        Accumulate(bw.Bone0, bw.Weight0);
        Accumulate(bw.Bone1, bw.Weight1);
        Accumulate(bw.Bone2, bw.Weight2);
        Accumulate(bw.Bone3, bw.Weight3);

        var len = result.Length();
        return len > 0.0001f ? result / len : Vector3.UnitZ;
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);

        try
        {
            // Create Silk.NET GL context from Avalonia's proc address loader
            _gl = GL.GetApi(gl.GetProcAddress);

            // Compile shaders
            var vertexShader = CompileShader(ShaderType.VertexShader, VertexShaderSource);
            var fragmentShader = CompileShader(ShaderType.FragmentShader, FragmentShaderSource);

            // Link program
            _shaderProgram = _gl.CreateProgram();
            _gl.AttachShader(_shaderProgram, vertexShader);
            _gl.AttachShader(_shaderProgram, fragmentShader);
            _gl.LinkProgram(_shaderProgram);

            _gl.GetProgram(_shaderProgram, ProgramPropertyARB.LinkStatus, out var status);
            if (status == 0)
            {
                var log = _gl.GetProgramInfoLog(_shaderProgram);
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Shader link error: {log}");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Shaders compiled and linked successfully");
            }

            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

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

    private uint CompileShader(ShaderType type, string source)
    {
        try
        {
            var shader = _gl!.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
            if (status == 0)
            {
                var log = _gl.GetShaderInfoLog(shader);
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Shader compile error ({type}): {log}");
            }

            return shader;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"CompileShader failed ({type}): {ex.GetType().Name}: {ex.Message}");
            return 0;
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

                // Clean up buffers and program
                _gl.DeleteBuffer(_vbo);
                _gl.DeleteBuffer(_ebo);
                _gl.DeleteVertexArray(_vao);
                _gl.DeleteProgram(_shaderProgram);
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
        float radius = Math.Max(_modelRadius, 0.001f);
        float cameraDistance = (radius / halfFovTan) / _zoom;

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fovRadians, aspect, cameraDistance * 0.01f, cameraDistance * 100f);

        // View matrix: camera looking at origin from +Y (after Z-up tilt)
        var eye = new Vector3(0, -cameraDistance, 0);
        var view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitZ);

        // Model matrix: rotate the model (vertices are pre-centered at origin).
        // No scale needed — perspective + camera distance handles framing.
        // The view matrix (LookAt with Z-up) already handles coordinate conversion,
        // so no extra Z-up to Y-up tilt is needed here.
        var m = Matrix4x4.CreateRotationZ(_rotationY);
        m = Matrix4x4.CreateRotationX(_rotationX) * m;

        var modelMatrix = m;

        // Use shader
        _gl.UseProgram(_shaderProgram);

        // Set uniforms
        SetUniformMatrix4("model", modelMatrix);
        SetUniformMatrix4("view", view);
        SetUniformMatrix4("projection", projection);

        // Debug logging - only log once per model
        if (_logOncePerModel)
        {
            _logOncePerModel = false;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Render: camDist={cameraDistance:F3}, radius={_modelRadius:F3}");
        }

        // Lighting - match NWN toolset brightness with higher ambient fill
        var lightDir = Vector3.Normalize(new Vector3(0.3f, -0.5f, 0.8f));
        SetUniformVec3("lightDir", lightDir);
        SetUniformVec3("lightColor", new Vector3(0.7f, 0.7f, 0.7f));
        SetUniformVec3("ambientColor", new Vector3(0.45f, 0.45f, 0.45f));

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
                SetUniformBool("hasTexture", true);
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, _textureCache[resolvedTexture!]);
                SetUniformInt("diffuseTexture", 0);
            }
            else
            {
                SetUniformBool("hasTexture", false);
                SetUniformVec3("flatColor", new Vector3(0.6f, 0.6f, 0.6f)); // Neutral gray for untextured meshes
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
        var allMeshes = _model.GetMeshNodes().ToList();
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

            // All meshes: apply full hierarchy world transform.
            // Skin mesh vertices (m_pavVerts) are in bind-pose space — same treatment as trimesh.
            // m_aQBoneRefInv/m_aTBoneRefInv are inverse bind-pose matrices for runtime animation, not static display.
            var worldTransform = GetWorldTransform(mesh);
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
            var hasNormals = mesh.Normals.Length == mesh.Vertices.Length;

            // Debug: check for data consistency issues
            if (mesh.TextureCoords.Length > 0 && mesh.TextureCoords[0].Length != mesh.Vertices.Length)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"  Mesh {meshIndex} '{mesh.Name}' UV count mismatch: {mesh.TextureCoords[0].Length} UVs vs {mesh.Vertices.Length} vertices");
            }
            if (mesh.Normals.Length > 0 && mesh.Normals.Length != mesh.Vertices.Length)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"  Mesh {meshIndex} '{mesh.Name}' normal count mismatch: {mesh.Normals.Length} normals vs {mesh.Vertices.Length} vertices");
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

            // Add vertices (position, normal, texcoord)
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                // For NaN vertices, output a zero vertex (won't be rendered due to face filtering)
                if (nanVertexIndices.Contains(i))
                {
                    nanFlatIndices.Add(vertices.Count / 8); // Track for bounds exclusion
                    // 8 floats per vertex: position(3), normal(3), texcoord(2)
                    for (int j = 0; j < 8; j++) vertices.Add(0);
                    continue;
                }

                // Get vertex in local mesh space
                var localVertex = mesh.Vertices[i];

                // Apply world transform to get world-space positions.
                // Skin mesh vertices (m_pavVerts) are stored in bind-pose space — apply
                // the node hierarchy transform exactly like NWNExplorer does (no bone weighting
                // needed for static bind-pose display; Q/T arrays are for runtime animation).
                Vector3 v = hasWorldTransform ? TransformPosition(localVertex, worldTransform) : localVertex;

                // Position
                vertices.Add(v.X);
                vertices.Add(v.Y);
                vertices.Add(v.Z);

                // Normal - use pre-computed normals from mesh if available, then rotate
                Vector3 normal;
                if (hasNormals)
                {
                    normal = mesh.Normals[i];
                    if (hasWorldTransform)
                        normal = TransformNormal(normal, worldTransform);
                }
                else
                {
                    // Fallback: calculate from first face that uses this vertex
                    normal = Vector3.UnitZ;
                    foreach (var face in mesh.Faces)
                    {
                        if (face.VertexIndex0 == i || face.VertexIndex1 == i || face.VertexIndex2 == i)
                        {
                            var v0 = mesh.Vertices[face.VertexIndex0];
                            var v1 = mesh.Vertices[face.VertexIndex1];
                            var v2 = mesh.Vertices[face.VertexIndex2];
                            var e1 = v1 - v0;
                            var e2 = v2 - v0;
                            normal = Vector3.Normalize(Vector3.Cross(e1, e2));
                            break;
                        }
                    }
                }
                vertices.Add(normal.X);
                vertices.Add(normal.Y);
                vertices.Add(normal.Z);

                // Texcoord
                if (hasUVs)
                {
                    var uv = mesh.TextureCoords[0][i];
                    vertices.Add(uv.X);
                    vertices.Add(uv.Y); // No V-flip — matches nwnexplorer (raw UVs)
                }
                else
                {
                    vertices.Add(0);
                    vertices.Add(0);
                }
            }

            // Add indices (skip faces that reference NaN vertices)
            int meshIndexStart = indices.Count;
            foreach (var face in mesh.Faces)
            {
                if (face.VertexIndex0 >= mesh.Vertices.Length ||
                    face.VertexIndex1 >= mesh.Vertices.Length ||
                    face.VertexIndex2 >= mesh.Vertices.Length)
                    continue;

                // Skip faces that reference NaN vertices
                if (nanVertexIndices.Contains(face.VertexIndex0) ||
                    nanVertexIndices.Contains(face.VertexIndex1) ||
                    nanVertexIndices.Contains(face.VertexIndex2))
                    continue;

                indices.Add(baseVertex + (uint)face.VertexIndex0);
                indices.Add(baseVertex + (uint)face.VertexIndex1);
                indices.Add(baseVertex + (uint)face.VertexIndex2);
            }

            drawCall.IndexCount = indices.Count - meshIndexStart;
            if (drawCall.IndexCount > 0)
                _meshDrawCalls.Add(drawCall);

            baseVertex += (uint)mesh.Vertices.Length;
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
                _modelRadius = 1.0f;
                _hasVertexBounds = false;
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
            _modelRadius = Math.Max(Math.Max(sizeX, sizeY), sizeZ) * 0.5f;
            _hasVertexBounds = true;

            // Vertices are now centered at origin — no need for screenOffset or _cameraTarget
            _cameraTarget = Vector3.Zero;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Vertex bounds: X=[{minX:F2},{maxX:F2}] Y=[{minY:F2},{maxY:F2}] Z=[{minZ:F2},{maxZ:F2}], " +
                $"pre-centered by ({center.X:F2}, {center.Y:F2}, {center.Z:F2}), radius={_modelRadius:F2}");
            } // else (valid bounds)
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"UpdateMeshBuffers: model={_model?.Name ?? "null"}, {_meshDrawCalls.Count} meshes ({skippedMeshes} skipped), {vertices.Count / 8} vertices, {_indexCount / 3} triangles");

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
            _textureCache.Remove(key);

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
                    ? _textureService.LoadTexturePreferBIF(texName, _colorIndices)
                    : _textureService.LoadTexture(texName, _colorIndices);
                if (textureData == null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Texture '{texName}' returned null");
                    failedTextures.Add(texName);
                    continue;
                }

                var (width, height, pixels) = textureData.Value;
                var texId = UploadTexture(width, height, pixels);
                if (texId != 0)
                {
                    _textureCache[texName] = texId;
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"  Loaded texture '{texName}' ({width}x{height}) -> texId={texId}");
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
                    ? _textureService.LoadTexturePreferBIF(modelTexture, _colorIndices)
                    : _textureService.LoadTexture(modelTexture, _colorIndices);
                if (fallbackData != null)
                {
                    var (w, h, px) = fallbackData.Value;
                    var fallbackId = UploadTexture(w, h, px);
                    if (fallbackId != 0)
                    {
                        _textureCache[modelTexture] = fallbackId;
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"  Loaded model fallback texture '{modelTexture}' ({w}x{h}) -> texId={fallbackId}");
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

    private bool _loggedUniforms;
    private void SetUniformMatrix4(string name, Matrix4x4 matrix)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (!_loggedUniforms && name == "projection")
        {
            _loggedUniforms = true;
            var modelLoc = _gl.GetUniformLocation(_shaderProgram, "model");
            var viewLoc = _gl.GetUniformLocation(_shaderProgram, "view");
            var projLoc = location;
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Uniform locations: model={modelLoc}, view={viewLoc}, projection={projLoc}");
        }
        if (location >= 0)
        {
            // System.Numerics uses row-vector convention: result = v * M
            // GLSL uses column-vector convention: result = M_gl * v
            // For these to agree, GLSL needs M^T (the transpose).
            // We write M^T in column-major order (which OpenGL expects).
            // M^T columns = M rows, so column 0 of M^T = row 0 of M = {M11,M12,M13,M14}.
            ReadOnlySpan<float> values = stackalloc float[16]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,  // M^T column 0 = M row 0
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,  // M^T column 1 = M row 1
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,  // M^T column 2 = M row 2
                matrix.M41, matrix.M42, matrix.M43, matrix.M44   // M^T column 3 = M row 3
            };
            _gl.UniformMatrix4(location, 1, false, values);
        }
    }

    private void SetUniformVec3(string name, Vector3 value)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }
    }

    private void SetUniformBool(string name, bool value)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value ? 1 : 0);
        }
    }

    private void SetUniformInt(string name, int value)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value);
        }
    }

    private void SetUniformFloat(string name, float value)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value);
        }
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
}
