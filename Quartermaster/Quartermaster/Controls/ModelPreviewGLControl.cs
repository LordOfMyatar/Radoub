// GPU-accelerated 3D Model Preview Control for Quartermaster
// Uses Silk.NET OpenGL with Avalonia's OpenGlControlBase
// Provides proper depth buffer, perspective-correct texture mapping, and per-pixel lighting

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Silk.NET.OpenGLES;

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
    private float _rotationY = MathF.PI; // Default 180° so model faces camera
    private float _rotationX;
    private float _zoom = 1.0f;
    private Vector3 _cameraTarget = Vector3.Zero;
    private float _modelRadius = 1.0f;

    // PLT color indices for texture rendering
    private PltColorIndices _colorIndices = new();
    private bool _needsTextureUpdate;
    private bool _needsMeshUpdate;

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
    // Simplified normal matrix - works for uniform scaling
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
    vec3 norm = normalize(Normal);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * lightColor;
    vec3 ambient = ambientColor;

    vec3 baseColor;
    if (hasTexture) {
        baseColor = texture(diffuseTexture, TexCoord).rgb;
    } else {
        baseColor = flatColor;
    }

    vec3 result = (ambient + diffuse) * baseColor;
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
        CenterCamera();
        RequestNextFrameRendering();
    }

    private void CenterCamera()
    {
        if (_model == null)
        {
            _cameraTarget = Vector3.Zero;
            _modelRadius = 1.0f;
            return;
        }

        _cameraTarget = (_model.BoundingMin + _model.BoundingMax) * 0.5f;
        _modelRadius = _model.Radius > 0 ? _model.Radius : 1.0f;
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

            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            // Create VAO/VBO/EBO
            _vao = _gl.GenVertexArray();
            _vbo = _gl.GenBuffer();
            _ebo = _gl.GenBuffer();

            // Enable depth testing
            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);

            // Enable backface culling
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(TriangleFace.Back);
            _gl.FrontFace(FrontFaceDirection.Ccw);

            UnifiedLogger.LogApplication(LogLevel.INFO, "OpenGL renderer initialized successfully");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"OpenGL init failed: {ex.Message}");
        }
    }

    private uint CompileShader(ShaderType type, string source)
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

    protected override void OnOpenGlDeinit(GlInterface gl)
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

        base.OnOpenGlDeinit(gl);
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_gl == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnOpenGlRender: _gl is null");
            return;
        }

        var bounds = Bounds;
        var width = (int)bounds.Width;
        var height = (int)bounds.Height;

        if (width <= 0 || height <= 0)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnOpenGlRender: Invalid bounds {width}x{height}");
            return;
        }

        // Bind the framebuffer Avalonia gave us
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        _gl.Viewport(0, 0, (uint)width, (uint)height);

        // Clear with visible background color (blue-ish gray for debugging)
        _gl.ClearColor(0.2f, 0.2f, 0.3f, 1.0f);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_model == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnOpenGlRender: No model to render");
            return;
        }

        // Update mesh data if needed
        if (_needsMeshUpdate)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnOpenGlRender: Updating mesh buffers for model with {_model.GetMeshNodes().Count()} meshes");
            UpdateMeshBuffers();
            _needsMeshUpdate = false;
        }

        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnOpenGlRender: Rendering model, meshDrawCalls={_meshDrawCalls.Count}, indexCount={_indexCount}");

        // Update textures if needed
        if (_needsTextureUpdate)
        {
            UpdateTextures();
            _needsTextureUpdate = false;
        }

        // Set up matrices
        var aspect = (float)width / height;
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(
            MathF.PI / 4f, // 45 degree FOV
            aspect,
            0.1f,
            1000.0f);

        // Camera distance based on model size
        var cameraDistance = _modelRadius * 3.0f / _zoom;
        var cameraPos = new Vector3(0, -cameraDistance, 0);

        var view = Matrix4x4.CreateLookAt(
            cameraPos,
            Vector3.Zero,
            Vector3.UnitZ);

        // Model rotation - NWN uses Z-up
        var model = Matrix4x4.CreateTranslation(-_cameraTarget) *
                   Matrix4x4.CreateRotationZ(_rotationY) *
                   Matrix4x4.CreateRotationX(_rotationX);

        // Use shader
        _gl.UseProgram(_shaderProgram);

        // Set uniforms
        SetUniformMatrix4("model", model);
        SetUniformMatrix4("view", view);
        SetUniformMatrix4("projection", projection);

        // Lighting - from upper front right
        var lightDir = Vector3.Normalize(new Vector3(0.5f, -0.5f, 0.8f));
        SetUniformVec3("lightDir", lightDir);
        SetUniformVec3("lightColor", new Vector3(0.8f, 0.8f, 0.8f));
        SetUniformVec3("ambientColor", new Vector3(0.3f, 0.3f, 0.3f));

        // Bind VAO and draw
        _gl.BindVertexArray(_vao);

        // Draw each mesh with its texture
        foreach (var drawCall in _meshDrawCalls)
        {
            if (drawCall.IndexCount == 0) continue;

            // Check if we have a texture for this mesh
            bool hasTexture = !string.IsNullOrEmpty(drawCall.TextureName) &&
                             _textureCache.TryGetValue(drawCall.TextureName, out var texId) && texId != 0;

            if (hasTexture)
            {
                SetUniformBool("hasTexture", true);
                _gl.ActiveTexture(TextureUnit.Texture0);
                _gl.BindTexture(TextureTarget.Texture2D, _textureCache[drawCall.TextureName!]);
                SetUniformInt("diffuseTexture", 0);
            }
            else
            {
                SetUniformBool("hasTexture", false);
                SetUniformVec3("flatColor", new Vector3(0.7f, 0.65f, 0.6f)); // Neutral skin tone
            }

            nint offset = drawCall.IndexOffset;
            _gl.DrawElements(PrimitiveType.Triangles, (uint)drawCall.IndexCount, DrawElementsType.UnsignedInt, in offset);
        }

        _gl.BindVertexArray(0);
    }

    private void UpdateMeshBuffers()
    {
        if (_gl == null || _model == null)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateMeshBuffers: Skipping - gl={_gl != null}, model={_model != null}");
            return;
        }

        _meshDrawCalls.Clear();

        // Collect all mesh data
        var vertices = new List<float>();
        var indices = new List<uint>();
        uint baseVertex = 0;

        var meshNodes = _model.GetMeshNodes().ToList();
        UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateMeshBuffers: Processing {meshNodes.Count} mesh nodes");

        foreach (var mesh in meshNodes)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"UpdateMeshBuffers: Mesh has {mesh.Vertices.Length} vertices, {mesh.Faces.Length} faces");
            if (mesh.Vertices.Length == 0 || mesh.Faces.Length == 0)
                continue;

            var nodePosition = mesh.Position;
            var hasUVs = mesh.TextureCoords.Length > 0 && mesh.TextureCoords[0].Length == mesh.Vertices.Length;

            // Track draw call for this mesh
            var drawCall = new MeshDrawCall
            {
                IndexOffset = indices.Count * sizeof(uint),
                TextureName = mesh.Bitmap?.ToLowerInvariant()
            };

            // Add vertices (position, normal, texcoord)
            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var v = mesh.Vertices[i] + nodePosition;

                // Position
                vertices.Add(v.X);
                vertices.Add(v.Y);
                vertices.Add(v.Z);

                // Normal - calculate from first face that uses this vertex
                Vector3 normal = Vector3.UnitZ;
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
                vertices.Add(normal.X);
                vertices.Add(normal.Y);
                vertices.Add(normal.Z);

                // Texcoord
                if (hasUVs)
                {
                    var uv = mesh.TextureCoords[0][i];
                    vertices.Add(uv.X);
                    vertices.Add(1.0f - uv.Y); // Flip V
                }
                else
                {
                    vertices.Add(0);
                    vertices.Add(0);
                }
            }

            // Add indices
            int meshIndexStart = indices.Count;
            foreach (var face in mesh.Faces)
            {
                if (face.VertexIndex0 >= mesh.Vertices.Length ||
                    face.VertexIndex1 >= mesh.Vertices.Length ||
                    face.VertexIndex2 >= mesh.Vertices.Length)
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
        // Position (location 0)
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 0);
        _gl.EnableVertexAttribArray(0);

        // Normal (location 1)
        _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), 3 * sizeof(float));
        _gl.EnableVertexAttribArray(1);

        // TexCoord (location 2)
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), 6 * sizeof(float));
        _gl.EnableVertexAttribArray(2);

        _gl.BindVertexArray(0);
    }

    private void UpdateTextures()
    {
        if (_gl == null || _textureService == null || _model == null) return;

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
        foreach (var texName in textureNames)
        {
            if (_textureCache.ContainsKey(texName))
                continue; // Already loaded

            try
            {
                var textureData = _textureService.LoadTexture(texName, _colorIndices);
                if (textureData == null) continue;

                var (width, height, pixels) = textureData.Value;
                var texId = UploadTexture(width, height, pixels);
                if (texId != 0)
                    _textureCache[texName] = texId;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load texture '{texName}': {ex.Message}");
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

    private void SetUniformMatrix4(string name, Matrix4x4 matrix)
    {
        var location = _gl!.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            // Convert Matrix4x4 to float array (column-major order for OpenGL)
            ReadOnlySpan<float> values = stackalloc float[16]
            {
                matrix.M11, matrix.M21, matrix.M31, matrix.M41,
                matrix.M12, matrix.M22, matrix.M32, matrix.M42,
                matrix.M13, matrix.M23, matrix.M33, matrix.M43,
                matrix.M14, matrix.M24, matrix.M34, matrix.M44
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
}
