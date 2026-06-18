using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// #2496: the binary MDL trimesh header stores the NWN:EE material name in the
/// 4th texture slot (texture3). rollnw confirms this authoritatively
/// (MdlBinaryParser.cpp: <c>mesh->materialname = to_string(data.texture3)</c>,
/// header comment "This is material name in NWN:EE"). The reader already reads that
/// 64-byte slot — this verifies it is captured into <see cref="MdlTrimeshNode.MaterialName"/>.
/// </summary>
public class TrimeshMaterialNameTests
{
    [Fact]
    public void Parse_BinaryTrimesh_CapturesMaterialNameFromTexture3()
    {
        var mdl = TrimeshMdlFixture.BuildSingleTrimesh(
            bitmap: "c_zod_boar",
            materialName: "c_zod_boar_mat");

        var model = new MdlBinaryReader().Parse(mdl);
        var mesh = Assert.IsType<MdlTrimeshNode>(model.GeometryRoot);

        Assert.Equal("c_zod_boar", mesh.Bitmap);
        Assert.Equal("c_zod_boar_mat", mesh.MaterialName);
    }

    [Fact]
    public void Parse_BinaryTrimesh_EmptyMaterialName_StaysEmpty()
    {
        var mdl = TrimeshMdlFixture.BuildSingleTrimesh(
            bitmap: "sometex",
            materialName: "");

        var model = new MdlBinaryReader().Parse(mdl);
        var mesh = Assert.IsType<MdlTrimeshNode>(model.GeometryRoot);

        Assert.Equal(string.Empty, mesh.MaterialName);
    }
}
