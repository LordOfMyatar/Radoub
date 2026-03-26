using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests.Mdl;

/// <summary>
/// Fixture-free tests for MdlModel helper methods.
/// </summary>
public class MdlModelTests
{
    [Fact]
    public void HasEmitterNodes_ReturnsFalse_WhenNoNodes()
    {
        var model = new MdlModel();
        Assert.False(model.HasEmitterNodes());
    }

    [Fact]
    public void HasEmitterNodes_ReturnsFalse_WhenOnlyMeshNodes()
    {
        var model = new MdlModel
        {
            GeometryRoot = new MdlNode { Name = "root" }
        };
        model.GeometryRoot.Children.Add(new MdlTrimeshNode { Name = "mesh1" });
        model.GeometryRoot.Children.Add(new MdlTrimeshNode { Name = "mesh2" });

        Assert.False(model.HasEmitterNodes());
    }

    [Fact]
    public void HasEmitterNodes_ReturnsTrue_WhenEmitterPresent()
    {
        var model = new MdlModel
        {
            GeometryRoot = new MdlNode { Name = "root" }
        };
        model.GeometryRoot.Children.Add(new MdlEmitterNode { Name = "emitter1" });

        Assert.True(model.HasEmitterNodes());
    }

    [Fact]
    public void HasEmitterNodes_ReturnsTrue_WhenEmitterNestedDeep()
    {
        var model = new MdlModel
        {
            GeometryRoot = new MdlNode { Name = "root" }
        };
        var child = new MdlNode { Name = "dummy" };
        child.Children.Add(new MdlEmitterNode { Name = "deep_emitter" });
        model.GeometryRoot.Children.Add(child);

        Assert.True(model.HasEmitterNodes());
    }

    [Fact]
    public void HasEmitterNodes_ReturnsTrue_WithMixedNodes()
    {
        var model = new MdlModel
        {
            GeometryRoot = new MdlNode { Name = "root" }
        };
        model.GeometryRoot.Children.Add(new MdlTrimeshNode { Name = "mesh1", Render = false });
        model.GeometryRoot.Children.Add(new MdlEmitterNode { Name = "fx_glow" });
        model.GeometryRoot.Children.Add(new MdlLightNode { Name = "light1" });

        Assert.True(model.HasEmitterNodes());
    }
}
