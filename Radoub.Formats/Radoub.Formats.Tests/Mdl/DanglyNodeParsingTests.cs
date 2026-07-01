using System.IO;
using System.Linq;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Danglymesh extension parsing (#2619). The binary reader's dangly fields
/// (displacement/tightness/period + the per-vertex constraints array) live at
/// meshHeaderStart+0x200 (nodeOffset+0x270) per nwnexplorer's CNwnMdlDanglyMeshNode. The old code
/// read from the wrong stream cursor and produced denormal-float garbage with zero constraints —
/// which is why CEP creatures' dangly manes/hair carried meaningless tuning values.
/// </summary>
public class DanglyNodeParsingTests
{
    [Fact]
    public void Binary_DanglyExtension_ReadsDisplacementTightnessPeriod()
    {
        var bytes = TrimeshMdlFixture.BuildSingleDangly(
            displacement: 0.05f, tightness: 3.0f, period: 8.0f,
            constraints: new[] { 0f, 128f, 240f, 255f });

        var model = new MdlBinaryReader().Parse(bytes);
        var dangly = Assert.IsType<MdlDanglyNode>(model.GeometryRoot);

        Assert.Equal(0.05f, dangly.Displacement, 4);
        Assert.Equal(3.0f, dangly.Tightness, 4);
        Assert.Equal(8.0f, dangly.Period, 4);
    }

    [Fact]
    public void Binary_DanglyExtension_ReadsConstraintsArray()
    {
        var expected = new[] { 0f, 128f, 240f, 255f };
        var bytes = TrimeshMdlFixture.BuildSingleDangly(0.05f, 3.0f, 8.0f, expected);

        var model = new MdlBinaryReader().Parse(bytes);
        var dangly = (MdlDanglyNode)model.GeometryRoot!;

        Assert.Equal(expected, dangly.Constraints);
    }

    [Fact]
    public void Binary_DanglyValues_AreNotDenormalGarbage()
    {
        // The pre-fix bug yielded denormal floats (~1e-43). Pin that they're plausible tuning values.
        var bytes = TrimeshMdlFixture.BuildSingleDangly(0.025f, 4.0f, 6.0f, new[] { 10f, 20f });
        var dangly = (MdlDanglyNode)new MdlBinaryReader().Parse(bytes).GeometryRoot!;

        Assert.True(dangly.Displacement is >= 0f and < 1000f);
        Assert.True(dangly.Tightness is >= 0f and < 1000f);
        Assert.True(dangly.Period is >= 0f and < 1000f);
    }

    // ---- ASCII reader sanity (already handled dangly props; guard against regression) ----

    private static readonly string AsciiAllipPath = Path.Combine(
        Path.GetDirectoryName(typeof(DanglyNodeParsingTests).Assembly.Location)!,
        "TestData", "Mdl", "c_allip_d.mdl");

    [Fact]
    public void Ascii_Allip_DanglyConstraintsMatchVertexCounts()
    {
        Assert.True(File.Exists(AsciiAllipPath), $"Fixture missing: {AsciiAllipPath}");
        var model = new MdlReader().Parse(File.ReadAllBytes(AsciiAllipPath));

        var danglies = model.GetMeshNodes().OfType<MdlDanglyNode>().ToList();
        Assert.NotEmpty(danglies);
        Assert.Contains(danglies, d => d.Constraints.Length > 0);
        foreach (var d in danglies.Where(d => d.Constraints.Length > 0))
            Assert.True(d.Constraints.Length <= d.Vertices.Length,
                $"'{d.Name}' has {d.Constraints.Length} constraints but {d.Vertices.Length} vertices");
    }
}
