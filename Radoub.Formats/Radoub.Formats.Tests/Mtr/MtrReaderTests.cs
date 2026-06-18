using Radoub.Formats.Mtr;
using Xunit;

namespace Radoub.Formats.Tests.Mtr;

public class MtrReaderTests
{
    // Representative of a real NWN:EE creature .mtr (Celestial Zodiac pack, c_zod_boar.mtr).
    private const string SampleMtr = @"customshadervs vslit_nm
customshaderfs fslit_nm
renderhint NormalTangents

// Textures
texture0 c_zod_boar
texture1 null
texture2 null
texture3 null
texture4 null
texture5 c_zod_boar

parameter float Specularity 0.65
parameter float Roughness 0.35
";

    [Fact]
    public void Parse_ReadsTexture0AsDiffuse()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.Equal("c_zod_boar", mtr.DiffuseTexture);
    }

    [Fact]
    public void Parse_NullTextureSlot_IsNull()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.Null(mtr.Textures[1]);
        Assert.Null(mtr.Textures[2]);
    }

    [Fact]
    public void Parse_ReadsHigherTextureSlot()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.Equal("c_zod_boar", mtr.Textures[5]);
    }

    [Fact]
    public void Parse_ReadsRenderHint()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.Equal("NormalTangents", mtr.RenderHint);
    }

    [Fact]
    public void Parse_ReadsShaderNames()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.Equal("vslit_nm", mtr.CustomShaderVs);
        Assert.Equal("fslit_nm", mtr.CustomShaderFs);
    }

    [Fact]
    public void Parse_ReadsFloatParameters()
    {
        var mtr = MtrReader.Parse(SampleMtr);
        Assert.True(mtr.Parameters.ContainsKey("Specularity"));
        Assert.Equal(0.65f, mtr.Parameters["Specularity"][0], 3);
        Assert.Equal(0.35f, mtr.Parameters["Roughness"][0], 3);
    }

    [Fact]
    public void Parse_IgnoresCommentsAndBlankLines()
    {
        var mtr = MtrReader.Parse("// just a comment\n\n   \ntexture0 mytex\n");
        Assert.Equal("mytex", mtr.DiffuseTexture);
    }

    [Fact]
    public void Parse_NoTexture0_DiffuseIsNull()
    {
        var mtr = MtrReader.Parse("renderhint NormalTangents\n");
        Assert.Null(mtr.DiffuseTexture);
    }

    [Fact]
    public void Parse_DivergentTexture0_DiffersFromMaterialName()
    {
        // The white-model case: texture0 is NOT the material/bitmap base name.
        var mtr = MtrReader.Parse("texture0 skin_diffuse_actual\n");
        Assert.Equal("skin_diffuse_actual", mtr.DiffuseTexture);
    }

    [Fact]
    public void Read_FromBytes_Utf8Bom_IsStripped()
    {
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = System.Text.Encoding.UTF8.GetBytes("texture0 bomtex\n");
        var buffer = new byte[bom.Length + body.Length];
        bom.CopyTo(buffer, 0);
        body.CopyTo(buffer, bom.Length);

        var mtr = MtrReader.Read(buffer);
        Assert.Equal("bomtex", mtr.DiffuseTexture);
    }
}
