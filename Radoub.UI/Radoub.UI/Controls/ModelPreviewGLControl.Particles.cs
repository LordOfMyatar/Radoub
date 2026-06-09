// Camera-facing particle billboard rendering for the shared model preview (#2395, Task 4.2).
// Owns a dedicated shader program + dynamic VBO, separate from the mesh shader
// (OpenGLShaderManager holds the mesh program only). Particles draw after the opaque mesh,
// sharing the mesh model rotation so they spin with the model, and align with the centered
// mesh by subtracting _modelCenter from each particle's raw-model-space position.

using System;
using System.Numerics;
using Radoub.Formats.Logging;
using Radoub.UI.Particles;
using Silk.NET.OpenGL;

namespace Radoub.UI.Controls;

public partial class ModelPreviewGLControl
{
    // Dedicated particle GL objects. Lazily created on first particle draw so we don't
    // allocate them for models without emitters. Cleaned up in CleanupParticleGl().
    private uint _particleProgram;
    private uint _particleVao;
    private uint _particleVbo;
    private bool _particleGlReady;

    // Vertex layout (floats): center.xyz(3) + corner.xy(2) + uv.xy(2) + color.rgba(4) + size(1) = 12
    private const int ParticleFloatsPerVertex = 12;
    private const int ParticleVertsPerQuad = 6; // two triangles, no index buffer (MVP)

    // Scratch CPU buffer reused across frames to avoid per-frame allocation churn.
    private float[] _particleScratch = Array.Empty<float>();

    // Corner offsets for the two triangles of a quad, in (corner, uv) pairs.
    // Corner is the half-extent offset in camera right/up space; uv spans 0..1.
    private static readonly (float cx, float cy, float u, float v)[] QuadCorners =
    {
        (-0.5f, -0.5f, 0f, 1f),
        ( 0.5f, -0.5f, 1f, 1f),
        ( 0.5f,  0.5f, 1f, 0f),
        (-0.5f, -0.5f, 0f, 1f),
        ( 0.5f,  0.5f, 1f, 0f),
        (-0.5f,  0.5f, 0f, 0f),
    };

    private const string ParticleVersionEs = "#version 300 es\nprecision highp float;\n";
    private const string ParticleVersionDesktop = "#version 330 core\n";

    private const string ParticleVertexBody = @"
layout (location = 0) in vec3 aCenterWorld;
layout (location = 1) in vec2 aCorner;
layout (location = 2) in vec2 aUV;
layout (location = 3) in vec4 aColor;
layout (location = 4) in float aSize;

out vec2 vUV;
out vec4 vColor;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProj;
uniform vec3 uCamRight;
uniform vec3 uCamUp;

void main()
{
    vec3 worldCenter = vec3(uModel * vec4(aCenterWorld, 1.0));
    vec3 pos = worldCenter + (uCamRight * aCorner.x + uCamUp * aCorner.y) * aSize;
    gl_Position = uProj * uView * vec4(pos, 1.0);
    vUV = aUV;
    vColor = aColor;
}
";

    private const string ParticleFragmentBody = @"
in vec2 vUV;
in vec4 vColor;

out vec4 outColor;

uniform sampler2D uTex;
uniform int uHasTex;
uniform int uCutout;

void main()
{
    vec4 c = vColor;
    if (uHasTex != 0) {
        c = texture(uTex, vUV) * vColor;
    }
    if (uCutout != 0 && c.a < 0.5) {
        discard;
    }
    outColor = c;
}
";

    /// <summary>
    /// Lazily compile + link the particle shader program and create its VAO/VBO. Returns
    /// true if the program is ready to use. Mirrors <see cref="OpenGLShaderManager"/>'s
    /// compile/link/error pattern. Profile (#version) matches the mesh shader via
    /// <see cref="_isOpenGLES"/> (#2395).
    /// </summary>
    private bool EnsureParticleGl()
    {
        if (_particleGlReady) return true;
        if (_gl == null) return false;

        var preamble = _isOpenGLES ? ParticleVersionEs : ParticleVersionDesktop;
        var vs = CompileParticleShader(ShaderType.VertexShader, preamble + ParticleVertexBody);
        var fs = CompileParticleShader(ShaderType.FragmentShader, preamble + ParticleFragmentBody);
        if (vs == 0 || fs == 0)
        {
            if (vs != 0) _gl.DeleteShader(vs);
            if (fs != 0) _gl.DeleteShader(fs);
            return false;
        }

        _particleProgram = _gl.CreateProgram();
        _gl.AttachShader(_particleProgram, vs);
        _gl.AttachShader(_particleProgram, fs);
        _gl.LinkProgram(_particleProgram);
        _gl.GetProgram(_particleProgram, ProgramPropertyARB.LinkStatus, out var status);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
        if (status == 0)
        {
            var log = _gl.GetProgramInfoLog(_particleProgram);
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"[Particle] shader link error: {log}");
            _gl.DeleteProgram(_particleProgram);
            _particleProgram = 0;
            return false;
        }

        _particleVao = _gl.GenVertexArray();
        _particleVbo = _gl.GenBuffer();

        // Configure attribute pointers once; the VBO is re-uploaded each frame but the
        // layout never changes. Stride = 12 floats.
        _gl.BindVertexArray(_particleVao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _particleVbo);
        int stride = ParticleFloatsPerVertex * sizeof(float);
        unsafe
        {
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, (uint)stride, (void*)0);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, (uint)stride, (void*)(5 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, (uint)stride, (void*)(7 * sizeof(float)));
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribPointer(4, 1, VertexAttribPointerType.Float, false, (uint)stride, (void*)(11 * sizeof(float)));
            _gl.EnableVertexAttribArray(4);
        }
        _gl.BindVertexArray(0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);

        _particleGlReady = true;
        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"[Particle] render program ready (prog={_particleProgram}, vao={_particleVao}, vbo={_particleVbo})");
        return true;
    }

    private uint CompileParticleShader(ShaderType type, string source)
    {
        if (_gl == null) return 0;
        try
        {
            var shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);
            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
            if (status == 0)
            {
                var log = _gl.GetShaderInfoLog(shader);
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"[Particle] shader compile error ({type}): {log}");
                _gl.DeleteShader(shader);
                return 0;
            }
            return shader;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR,
                $"[Particle] CompileShader failed ({type}): {ex.GetType().Name}: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Render all live particle systems as camera-facing billboards (#2395).
    /// Shares the mesh <paramref name="modelMatrix"/> (rotation only) so particles spin with
    /// the model. Camera right/up come from the view matrix basis. State touched (program, VAO,
    /// blend, depth mask, active texture) is restored before returning so the next frame's mesh
    /// pass is unaffected.
    /// </summary>
    private void RenderParticles(Matrix4x4 modelMatrix, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_gl == null) return;

        // Skip entirely if nothing is alive — avoids creating GL objects for a static frame.
        int totalLive = 0;
        foreach (var (_, _, system) in _particleSystems)
            totalLive += system.LiveCount;
        if (totalLive == 0) return;

        if (!EnsureParticleGl()) return;

        // Camera basis from the view matrix. System.Numerics view is row-major and
        // CreateLookAt stores the camera right/up/forward in the matrix columns of the
        // rotation block (M11..M33). Right = (M11,M21,M31), Up = (M12,M22,M32). These are
        // the world-space axes that face the screen; billboard corners expand along them.
        var camRight = new Vector3(view.M11, view.M21, view.M31);
        var camUp = new Vector3(view.M12, view.M22, view.M32);

        _gl.UseProgram(_particleProgram);
        SetParticleMatrix("uModel", modelMatrix);
        SetParticleMatrix("uView", view);
        SetParticleMatrix("uProj", projection);
        SetParticleVec3("uCamRight", camRight);
        SetParticleVec3("uCamUp", camUp);

        _gl.BindVertexArray(_particleVao);
        _gl.ActiveTexture(TextureUnit.Texture0);

        // Particles blend over the opaque mesh. Keep depth TEST on (so mesh occludes
        // particles behind it) but control depth WRITES per blend mode below.
        _gl.Enable(EnableCap.Blend);

        // TODO(#2395): back-to-front sort for Alpha blend (additive is order-independent).
        foreach (var (_, emitter, system) in _particleSystems)
        {
            int count = system.LiveCount;
            if (count == 0) continue;

            // Per-blend state.
            bool cutout = emitter.Blend == ParticleBlendMode.Cutout;
            switch (emitter.Blend)
            {
                case ParticleBlendMode.Additive:
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.One);
                    _gl.DepthMask(false);
                    break;
                case ParticleBlendMode.Cutout:
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    _gl.DepthMask(true); // cutout is effectively opaque; writes depth
                    break;
                case ParticleBlendMode.Alpha:
                default:
                    _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                    _gl.DepthMask(false);
                    break;
            }

            // Resolve texture via the same cache + remapping the mesh path uses.
            uint texId = ResolveParticleTexture(emitter.Material.Texture);
            bool hasTex = texId != 0;
            SetParticleInt("uHasTex", hasTex ? 1 : 0);
            SetParticleInt("uCutout", cutout ? 1 : 0);
            if (hasTex)
            {
                _gl.BindTexture(TextureTarget.Texture2D, texId);
                SetParticleInt("uTex", 0);
            }

            BuildParticleVertices(emitter, system, count);

            int vertCount = count * ParticleVertsPerQuad;
            int floatCount = vertCount * ParticleFloatsPerVertex;
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _particleVbo);
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(floatCount * sizeof(float)),
                new ReadOnlySpan<float>(_particleScratch, 0, floatCount), BufferUsageARB.DynamicDraw);

            _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)vertCount);
        }

        // Restore state for the mesh pass: re-enable depth writes, drop blend (mesh is opaque),
        // unbind everything we touched.
        _gl.DepthMask(true);
        _gl.Disable(EnableCap.Blend);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);

        var err = _gl.GetError();
        if (err != GLEnum.NoError)
            UnifiedLogger.LogApplication(LogLevel.WARN, $"[Particle] GL error after particle draw: {err}");
    }

    /// <summary>
    /// Fill <see cref="_particleScratch"/> with 6 vertices per live particle. Each vertex is
    /// (center - _modelCenter), a quad corner, a UV (full quad or sprite-sheet subrect),
    /// the particle color, and its size. Center is in raw model space; the shader applies
    /// uModel so particles rotate with the mesh (#2395).
    /// </summary>
    private void BuildParticleVertices(CompiledEmitter emitter, ParticleSystem system, int count)
    {
        int needed = count * ParticleVertsPerQuad * ParticleFloatsPerVertex;
        if (_particleScratch.Length < needed)
            _particleScratch = new float[needed];

        var sheet = emitter.Material.Sheet;
        int cols = Math.Max(sheet.Columns, (ushort)1);
        int rows = Math.Max(sheet.Rows, (ushort)1);
        bool useSheet = cols * rows > 1;

        var particles = system.Particles;
        int o = 0;
        for (int p = 0; p < count && p < particles.Count; p++)
        {
            var part = particles[p];
            Vector3 center = part.Position - _modelCenter;

            // Sprite-sheet UV subrect for the particle's current frame, or full 0..1.
            float uMin = 0f, vMin = 0f, uMax = 1f, vMax = 1f;
            if (useSheet)
            {
                int frame = part.Frame;
                int cell = frame % (cols * rows);
                if (cell < 0) cell += cols * rows;
                int col = cell % cols;
                int row = cell / cols;
                uMin = col / (float)cols;
                uMax = (col + 1) / (float)cols;
                // Row 0 at the top of the sheet (v=1 top, v=0 bottom in our corner UVs).
                vMax = 1f - row / (float)rows;
                vMin = 1f - (row + 1) / (float)rows;
            }

            float size = part.SizeX; // MVP: square billboard sized by SizeX
            Vector4 color = part.Color;

            for (int c = 0; c < ParticleVertsPerQuad; c++)
            {
                var corner = QuadCorners[c];
                // Map the corner's template UV (0..1) into the sheet subrect.
                float u = uMin + corner.u * (uMax - uMin);
                float v = vMin + corner.v * (vMax - vMin);

                _particleScratch[o++] = center.X;
                _particleScratch[o++] = center.Y;
                _particleScratch[o++] = center.Z;
                _particleScratch[o++] = corner.cx;
                _particleScratch[o++] = corner.cy;
                _particleScratch[o++] = u;
                _particleScratch[o++] = v;
                _particleScratch[o++] = color.X;
                _particleScratch[o++] = color.Y;
                _particleScratch[o++] = color.Z;
                _particleScratch[o++] = color.W;
                _particleScratch[o++] = size;
            }
        }
    }

    /// <summary>
    /// Resolve a particle material texture name to a GL texture id using the same cache
    /// and remapping the mesh draw loop uses. Returns 0 if not loaded (caller renders flat
    /// color). Particle textures aren't pre-loaded by UpdateTextures, so this commonly
    /// returns 0 in the MVP — particles still render as flat colored billboards (#2395).
    /// </summary>
    private uint ResolveParticleTexture(string? textureName)
    {
        if (string.IsNullOrEmpty(textureName)) return 0;
        var name = textureName.ToLowerInvariant();
        if (!_textureCache.ContainsKey(name) && _textureRemapping.TryGetValue(name, out var remapped))
            name = remapped;
        if (_textureCache.TryGetValue(name, out var texId) && texId != 0)
            return texId;
        return 0;
    }

    private void SetParticleMatrix(string name, Matrix4x4 m)
    {
        if (_gl == null) return;
        int loc = _gl.GetUniformLocation(_particleProgram, name);
        if (loc < 0) return;
        // Same convention as OpenGLShaderManager: upload System.Numerics row-major data
        // with transpose=false (GLSL reads it column-major, which transposes for us).
        ReadOnlySpan<float> values = stackalloc float[16]
        {
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44
        };
        _gl.UniformMatrix4(loc, 1, false, values);
    }

    private void SetParticleVec3(string name, Vector3 v)
    {
        if (_gl == null) return;
        int loc = _gl.GetUniformLocation(_particleProgram, name);
        if (loc >= 0) _gl.Uniform3(loc, v.X, v.Y, v.Z);
    }

    private void SetParticleInt(string name, int value)
    {
        if (_gl == null) return;
        int loc = _gl.GetUniformLocation(_particleProgram, name);
        if (loc >= 0) _gl.Uniform1(loc, value);
    }

    /// <summary>Delete particle GL objects. Called from OnOpenGlDeinit (#2395).</summary>
    private void CleanupParticleGl()
    {
        if (_gl == null) return;
        try
        {
            if (_particleVbo != 0) { _gl.DeleteBuffer(_particleVbo); _particleVbo = 0; }
            if (_particleVao != 0) { _gl.DeleteVertexArray(_particleVao); _particleVao = 0; }
            if (_particleProgram != 0) { _gl.DeleteProgram(_particleProgram); _particleProgram = 0; }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"[Particle] GL cleanup error (non-fatal): {ex.GetType().Name}: {ex.Message}");
        }
        _particleGlReady = false;
    }
}
