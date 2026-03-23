using Radoub.Formats.Common;
using Radoub.UI.Services.Search;
using Xunit;

namespace Radoub.UI.Tests.Services.Search;

public class ToolDispatchServiceTests
{
    private readonly ToolDispatchService _service = new();

    [Theory]
    [InlineData(ResourceTypes.Dlg, "Parley")]
    [InlineData(ResourceTypes.Utc, "Quartermaster")]
    [InlineData(ResourceTypes.Bic, "Quartermaster")]
    [InlineData(ResourceTypes.Uti, "Relique")]
    [InlineData(ResourceTypes.Utm, "Fence")]
    [InlineData(ResourceTypes.Jrl, "Manifest")]
    public void GetToolForFileType_ReturnsMappedTool(ushort resourceType, string expectedTool)
    {
        var info = _service.GetToolForFileType(resourceType);

        Assert.NotNull(info);
        Assert.Equal(expectedTool, info.ToolName);
    }

    [Fact]
    public void GetToolForFileType_UnmappedType_ReturnsNull()
    {
        var info = _service.GetToolForFileType(ResourceTypes.Are);

        Assert.Null(info);
    }

    [Theory]
    [InlineData(ResourceTypes.Dlg)]
    [InlineData(ResourceTypes.Utc)]
    [InlineData(ResourceTypes.Uti)]
    [InlineData(ResourceTypes.Utm)]
    [InlineData(ResourceTypes.Jrl)]
    public void CanDispatch_MappedTypes_ReturnsTrue(ushort resourceType)
    {
        Assert.True(_service.CanDispatch(resourceType));
    }

    [Fact]
    public void CanDispatch_UnmappedType_ReturnsFalse()
    {
        Assert.False(_service.CanDispatch(ResourceTypes.Are));
        Assert.False(_service.CanDispatch(ResourceTypes.Git));
    }

    [Fact]
    public void GetToolForFileType_IncludesAssemblyName()
    {
        var info = _service.GetToolForFileType(ResourceTypes.Dlg);

        Assert.NotNull(info);
        Assert.NotEmpty(info.AssemblyName);
    }

    [Fact]
    public void GetToolForFileType_ReliqueHasDifferentAssemblyName()
    {
        var info = _service.GetToolForFileType(ResourceTypes.Uti);

        Assert.NotNull(info);
        Assert.Equal("Relique", info.ToolName);
        Assert.Equal("ItemEditor", info.AssemblyName);
    }
}
