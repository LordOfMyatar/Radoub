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
    private OpenGLShaderManager? _shaderManager;
    private readonly ModelViewController _viewController = new();
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

    // PLT color indices for texture rendering
    private PltColorIndices _colorIndices = new();
    private bool _needsTextureUpdate;
    private bool _needsMeshUpdate;
    private bool _logOncePerModel;

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

            // Compile and link shaders via manager
            _shaderManager = new OpenGLShaderManager(_gl);
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

        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            fovRadians, aspect, cameraDistance * 0.01f, cameraDistance * 100f);

        // View matrix: camera looking at origin from +Y (after Z-up tilt)
        var eye = new Vector3(0, -cameraDistance, 0);
        var view = Matrix4x4.CreateLookAt(eye, Vector3.Zero, Vector3.UnitZ);

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
        var lightDir = Vector3.Normalize(new Vector3(0.3f, -0.5f, 0.8f));
        _shaderManager!.SetUniformVec3("lightDir", lightDir);
        _shaderManager!.SetUniformVec3("lightColor", new Vector3(0.7f, 0.7f, 0.7f));
        _shaderManager!.SetUniformVec3("ambientColor", new Vector3(0.45f, 0.45f, 0.45f));

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
            var worldTransform = ModelViewController.GetWorldTransform(mesh);
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
                Vector3 v = hasWorldTransform ? ModelViewController.TransformPosition(localVertex, worldTransform) : localVertex;

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
                        normal = ModelViewController.TransformNormal(normal, worldTransform);
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
