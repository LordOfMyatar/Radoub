using Radoub.Formats.Common;
using Xunit;

namespace Radoub.Formats.Tests;

public class ResourceTypesTests
{
    [Theory]
    [InlineData(ResourceTypes.Dlg, ".dlg")]
    [InlineData(ResourceTypes.Nss, ".nss")]
    [InlineData(ResourceTypes.Ncs, ".ncs")]
    [InlineData(ResourceTypes.Utc, ".utc")]
    [InlineData(ResourceTypes.Uti, ".uti")]
    [InlineData(ResourceTypes.TwoDA, ".2da")]
    [InlineData(ResourceTypes.Are, ".are")]
    [InlineData(ResourceTypes.Git, ".git")]
    [InlineData(ResourceTypes.Ifo, ".ifo")]
    [InlineData(ResourceTypes.Mtr, ".mtr")]
    public void GetExtension_ReturnsCorrectExtension(ushort resourceType, string expectedExtension)
    {
        var result = ResourceTypes.GetExtension(resourceType);
        Assert.Equal(expectedExtension, result);
    }

    [Theory]
    [InlineData(".dlg", ResourceTypes.Dlg)]
    [InlineData("dlg", ResourceTypes.Dlg)]
    [InlineData(".nss", ResourceTypes.Nss)]
    [InlineData(".NCS", ResourceTypes.Ncs)]
    [InlineData(".2da", ResourceTypes.TwoDA)]
    [InlineData(".mtr", ResourceTypes.Mtr)]
    [InlineData("MTR", ResourceTypes.Mtr)]
    public void FromExtension_ReturnsCorrectType(string extension, ushort expectedType)
    {
        var result = ResourceTypes.FromExtension(extension);
        Assert.Equal(expectedType, result);
    }

    [Theory]
    [InlineData(".unknown")]
    [InlineData(".xyz")]
    [InlineData("")]
    public void FromExtension_ReturnsInvalidForUnknown(string extension)
    {
        var result = ResourceTypes.FromExtension(extension);
        Assert.Equal(ResourceTypes.Invalid, result);
    }

    [Fact]
    public void CommonResourceTypes_HaveCorrectValues()
    {
        // Verify key resource type values match BioWare spec
        Assert.Equal((ushort)2009, ResourceTypes.Nss);
        Assert.Equal((ushort)2010, ResourceTypes.Ncs);
        Assert.Equal((ushort)2029, ResourceTypes.Dlg);
        Assert.Equal((ushort)2017, ResourceTypes.TwoDA);
        Assert.Equal((ushort)2027, ResourceTypes.Utc);
        Assert.Equal((ushort)2025, ResourceTypes.Uti);
        Assert.Equal((ushort)3007, ResourceTypes.Mtr);  // NWN:EE material file (restype.nim)
    }
}
