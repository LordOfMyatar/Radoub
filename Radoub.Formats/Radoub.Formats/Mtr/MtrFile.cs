namespace Radoub.Formats.Mtr;

/// <summary>
/// A parsed NWN:EE MTR material file (resource type 3007). MTR binds PBR shaders
/// and texture samplers to a mesh material; the diffuse map is <c>texture0</c>.
/// Reference: NWN:EE material/shader system.
/// </summary>
public sealed class MtrFile
{
    /// <summary>Custom vertex shader name (<c>customshadervs</c>), if declared.</summary>
    public string? CustomShaderVs { get; set; }

    /// <summary>Custom fragment shader name (<c>customshaderfs</c>), if declared.</summary>
    public string? CustomShaderFs { get; set; }

    /// <summary>Render hint (e.g. <c>NormalTangents</c>), if declared.</summary>
    public string? RenderHint { get; set; }

    /// <summary>
    /// Texture sampler slots by index (<c>texture0</c>..<c>textureN</c>). A slot left
    /// as <c>null</c> in the file (literal "null") is stored as <c>null</c> here.
    /// </summary>
    public string?[] Textures { get; } = new string?[16];

    /// <summary>Shader parameters by name (<c>parameter &lt;type&gt; &lt;name&gt; &lt;v..&gt;</c>).</summary>
    public Dictionary<string, float[]> Parameters { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The diffuse map for texture resolution: <c>texture0</c>.</summary>
    public string? DiffuseTexture => Textures[0];
}
