// OpenGL shader lifecycle: compile, link, uniform setting, cleanup.
// Extracted from ModelPreviewGLControl to improve separation of concerns.

using System;
using System.Numerics;
using Radoub.Formats.Logging;
using Silk.NET.OpenGL;

namespace Quartermaster.Controls;

/// <summary>
/// Manages OpenGL shader compilation, linking, uniform setting, and cleanup.
/// Owns the shader program handle and provides typed uniform setters.
/// </summary>
public class OpenGLShaderManager
{
    private readonly GL _gl;
    private readonly bool _isOpenGLES;
    private uint _shaderProgram;
    private bool _loggedUniforms;

    // Shader source split into version preamble + shared body.
    // Windows (ANGLE) provides OpenGL ES → needs "#version 300 es" + precision qualifier.
    // Linux (GLX) provides desktop OpenGL → needs "#version 330 core", no precision qualifier.
    private const string VersionEs = "#version 300 es\nprecision highp float;\n";
    private const string VersionDesktop = "#version 330 core\n";

    private const string VertexShaderBody = @"
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

    private const string FragmentShaderBody = @"
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

// Debug visualisation modes (#2026 investigation):
//   0 = normal rendering
//   1 = world-space normal as RGB (xyz -> rgb remapped from [-1,1] to [0,1])
//   2 = pure lighting dot product as grayscale (no texture, no ambient)
uniform int debugMode;

void main()
{
    vec3 norm = normalize(Normal);

    if (debugMode == 1) {
        // Visualise the post-transform normal as colour. If the normal is
        // correctly rotating with the model, the colour on a given surface
        // point should change as the user rotates the model. If colours
        // stay locked to the face, normals are not being transformed.
        FragColor = vec4(norm * 0.5 + 0.5, 1.0);
        return;
    }

    if (debugMode == 2) {
        // Pure lighting term with no texture/ambient. Uses signed dot
        // (not abs) so front-facing is bright, back-facing is dark --
        // reveals whether normals are transformed with the model.
        float d = max(dot(norm, lightDir), 0.0);
        FragColor = vec4(vec3(d), 1.0);
        return;
    }

    // Two-sided lighting: use abs() so thin surfaces (bat wings, dragon
    // membranes) are lit from both sides. The Aurora Engine renders these
    // single-layer polygons visible from both directions (#1867).
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

    public uint ShaderProgram => _shaderProgram;

    public OpenGLShaderManager(GL gl, bool isOpenGLES)
    {
        _gl = gl;
        _isOpenGLES = isOpenGLES;
    }

    /// <summary>
    /// Compile and link the shader program. Returns true on success.
    /// </summary>
    public bool CreateProgram()
    {
        var preamble = _isOpenGLES ? VersionEs : VersionDesktop;
        var vertexShader = CompileShader(ShaderType.VertexShader, preamble + VertexShaderBody);
        var fragmentShader = CompileShader(ShaderType.FragmentShader, preamble + FragmentShaderBody);

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

        return status != 0;
    }

    public uint CompileShader(ShaderType type, string source)
    {
        try
        {
            var shader = _gl.CreateShader(type);
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

    public void SetUniformMatrix4(string name, Matrix4x4 matrix)
    {
        var location = _gl.GetUniformLocation(_shaderProgram, name);
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
            ReadOnlySpan<float> values = stackalloc float[16]
            {
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            };
            _gl.UniformMatrix4(location, 1, false, values);
        }
    }

    public void SetUniformVec3(string name, Vector3 value)
    {
        var location = _gl.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform3(location, value.X, value.Y, value.Z);
        }
    }

    public void SetUniformBool(string name, bool value)
    {
        var location = _gl.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value ? 1 : 0);
        }
    }

    public void SetUniformInt(string name, int value)
    {
        var location = _gl.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value);
        }
    }

    public void SetUniformFloat(string name, float value)
    {
        var location = _gl.GetUniformLocation(_shaderProgram, name);
        if (location >= 0)
        {
            _gl.Uniform1(location, value);
        }
    }

    /// <summary>
    /// Delete the shader program. Call during GL deinit.
    /// </summary>
    public void Cleanup()
    {
        if (_shaderProgram != 0)
        {
            _gl.DeleteProgram(_shaderProgram);
            _shaderProgram = 0;
        }
    }
}
