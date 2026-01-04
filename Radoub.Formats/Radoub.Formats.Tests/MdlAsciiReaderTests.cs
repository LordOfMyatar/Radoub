using System.Numerics;
using Radoub.Formats.Mdl;
using Xunit;
using Xunit.Abstractions;

namespace Radoub.Formats.Tests;

public class MdlAsciiReaderTests
{
    private readonly ITestOutputHelper _output;

    public MdlAsciiReaderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Parse_CompleteModel_ShowsOutput()
    {
        var mdlContent = @"
newmodel test_character
setsupermodel test_character NULL
classification character
setanimationscale 1.0
beginmodelgeom test_character
node dummy test_character
  parent NULL
  position 0 0 0
  node trimesh body_mesh
    parent test_character
    position 0 0 1
    bitmap armor_tex
    ambient 0.2 0.2 0.2
    diffuse 0.8 0.8 0.8
    verts 4
      -1 -1 0
      1 -1 0
      1 1 0
      -1 1 0
    tverts 4
      0 0
      1 0
      1 1
      0 1
    faces 2
      0 1 2 1 0 1 2 0
      0 2 3 1 0 2 3 0
  endnode
  node light torch
    parent test_character
    position 0 0 2
    color 1.0 0.8 0.5
    radius 5.0
  endnode
endnode
endmodelgeom test_character
newanim idle test_character
  length 2.0
  transtime 0.25
  animroot test_character
  event 0.5 sound_breath
doneanim idle test_character
donemodel test_character
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        _output.WriteLine("=== MDL Parser Output ===\n");
        _output.WriteLine($"Model Name: {model.Name}");
        _output.WriteLine($"Classification: {model.Classification}");
        _output.WriteLine($"Animation Scale: {model.AnimationScale}");
        _output.WriteLine($"Super Model: {model.SuperModel}");
        _output.WriteLine($"Bounding Min: {model.BoundingMin}");
        _output.WriteLine($"Bounding Max: {model.BoundingMax}");
        _output.WriteLine($"Radius: {model.Radius:F2}");

        _output.WriteLine($"\n--- Nodes ({model.EnumerateAllNodes().Count()}) ---");
        foreach (var node in model.EnumerateAllNodes())
        {
            var indent = "";
            var p = node.Parent;
            while (p != null) { indent += "  "; p = p.Parent; }

            _output.WriteLine($"{indent}[{node.NodeType}] {node.Name}");
            _output.WriteLine($"{indent}  Position: {node.Position}");

            if (node is MdlTrimeshNode mesh)
            {
                _output.WriteLine($"{indent}  Bitmap: {mesh.Bitmap}");
                _output.WriteLine($"{indent}  Vertices: {mesh.Vertices.Length}");
                _output.WriteLine($"{indent}  Faces: {mesh.Faces.Length}");
                _output.WriteLine($"{indent}  Diffuse: {mesh.Diffuse}");
            }
            else if (node is MdlLightNode light)
            {
                _output.WriteLine($"{indent}  Color: {light.Color}");
                _output.WriteLine($"{indent}  Radius: {light.Radius}");
            }
        }

        _output.WriteLine($"\n--- Animations ({model.Animations.Count}) ---");
        foreach (var anim in model.Animations)
        {
            _output.WriteLine($"Animation: {anim.Name}");
            _output.WriteLine($"  Length: {anim.Length}s");
            _output.WriteLine($"  Transition: {anim.TransitionTime}s");
            _output.WriteLine($"  Events: {anim.Events.Count}");
            foreach (var evt in anim.Events)
                _output.WriteLine($"    @{evt.Time}s: {evt.EventName}");
        }

        _output.WriteLine($"\n--- Mesh Geometry ---");
        foreach (var mesh in model.GetMeshNodes())
        {
            _output.WriteLine($"Mesh '{mesh.Name}':");
            _output.WriteLine($"  Vertices ({mesh.Vertices.Length}):");
            for (int i = 0; i < mesh.Vertices.Length; i++)
                _output.WriteLine($"    [{i}] {mesh.Vertices[i]}");
            _output.WriteLine($"  Faces ({mesh.Faces.Length}):");
            for (int i = 0; i < mesh.Faces.Length; i++)
            {
                var f = mesh.Faces[i];
                _output.WriteLine($"    [{i}] vertices: ({f.VertexIndex0}, {f.VertexIndex1}, {f.VertexIndex2})");
            }
        }

        // Actual assertions
        Assert.Equal("test_character", model.Name);
        Assert.Equal(3, model.EnumerateAllNodes().Count());
        Assert.Single(model.Animations);
    }

    [Fact]
    public void Parse_MinimalModel_ReturnsModel()
    {
        var mdlContent = @"
newmodel testmodel
setsupermodel testmodel NULL
classification character
setanimationscale 1.0
beginmodelgeom testmodel
node dummy testmodel
  parent NULL
endnode
endmodelgeom testmodel
donemodel testmodel
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.NotNull(model);
        Assert.Equal("testmodel", model.Name);
        Assert.Equal(MdlClassification.Character, model.Classification);
        Assert.Equal(1.0f, model.AnimationScale);
        Assert.NotNull(model.GeometryRoot);
        Assert.Equal("testmodel", model.GeometryRoot.Name);
    }

    [Fact]
    public void Parse_TrimeshWithVertices_ParsesGeometry()
    {
        var mdlContent = @"
newmodel cube
beginmodelgeom cube
node trimesh cube_mesh
  parent NULL
  position 0 0 0
  orientation 0 0 1 0
  bitmap texture01
  ambient 0.2 0.2 0.2
  diffuse 1 1 1
  specular 0 0 0
  shininess 1
  verts 4
    0.0 0.0 0.0
    1.0 0.0 0.0
    1.0 1.0 0.0
    0.0 1.0 0.0
  tverts 4
    0.0 0.0
    1.0 0.0
    1.0 1.0
    0.0 1.0
  faces 2
    0 1 2 1 0 1 2 0
    0 2 3 1 0 2 3 0
endnode
endmodelgeom cube
donemodel cube
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.NotNull(model);
        Assert.Equal("cube", model.Name);

        var meshNodes = model.GetMeshNodes().ToList();
        Assert.Single(meshNodes);

        var mesh = meshNodes[0];
        Assert.Equal("cube_mesh", mesh.Name);
        Assert.Equal("texture01", mesh.Bitmap);
        Assert.Equal(4, mesh.Vertices.Length);
        Assert.Equal(2, mesh.Faces.Length);

        // Check vertex positions
        Assert.Equal(new Vector3(0, 0, 0), mesh.Vertices[0]);
        Assert.Equal(new Vector3(1, 0, 0), mesh.Vertices[1]);
        Assert.Equal(new Vector3(1, 1, 0), mesh.Vertices[2]);
        Assert.Equal(new Vector3(0, 1, 0), mesh.Vertices[3]);

        // Check face indices
        Assert.Equal(0, mesh.Faces[0].VertexIndex0);
        Assert.Equal(1, mesh.Faces[0].VertexIndex1);
        Assert.Equal(2, mesh.Faces[0].VertexIndex2);
    }

    [Fact]
    public void Parse_NodeHierarchy_BuildsParentChildRelationships()
    {
        var mdlContent = @"
newmodel skeleton
beginmodelgeom skeleton
node dummy root
  parent NULL
  position 0 0 0
  node dummy torso
    parent root
    position 0 0 1
    node dummy head
      parent torso
      position 0 0 0.5
    endnode
    node dummy larm
      parent torso
      position -0.5 0 0
    endnode
    node dummy rarm
      parent torso
      position 0.5 0 0
    endnode
  endnode
endnode
endmodelgeom skeleton
donemodel skeleton
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.NotNull(model);
        var root = model.GeometryRoot;
        Assert.NotNull(root);
        Assert.Equal("root", root.Name);
        Assert.Null(root.Parent);

        // Root should have torso as child
        Assert.Single(root.Children);
        var torso = root.Children[0];
        Assert.Equal("torso", torso.Name);
        Assert.Equal(root, torso.Parent);

        // Torso should have head, larm, rarm as children
        Assert.Equal(3, torso.Children.Count);
    }

    [Fact]
    public void Parse_LightNode_ParsesLightProperties()
    {
        var mdlContent = @"
newmodel lighttest
beginmodelgeom lighttest
node light torch_light
  parent NULL
  position 0 0 2
  color 1.0 0.8 0.6
  radius 10.0
  multiplier 2.0
  isdynamic 1
  shadow 1
  priority 3
endnode
endmodelgeom lighttest
donemodel lighttest
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        var lightNode = model.FindNode("torch_light") as MdlLightNode;
        Assert.NotNull(lightNode);
        Assert.Equal(new Vector3(1.0f, 0.8f, 0.6f), lightNode.Color);
        Assert.Equal(10.0f, lightNode.Radius);
        Assert.Equal(2.0f, lightNode.Multiplier);
        Assert.True(lightNode.IsDynamic);
        Assert.True(lightNode.Shadow);
        Assert.Equal(3, lightNode.Priority);
    }

    [Fact]
    public void Parse_Animation_ParsesAnimationData()
    {
        var mdlContent = @"
newmodel animated
beginmodelgeom animated
node dummy animated
  parent NULL
endnode
endmodelgeom animated
newanim walk animated
  length 1.5
  transtime 0.25
  animroot animated
  event 0.0 footstep_left
  event 0.5 footstep_right
  event 1.0 footstep_left
doneanim walk animated
donemodel animated
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.Single(model.Animations);
        var anim = model.Animations[0];

        Assert.Equal("walk", anim.Name);
        Assert.Equal(1.5f, anim.Length);
        Assert.Equal(0.25f, anim.TransitionTime);
        Assert.Equal("animated", anim.AnimRoot);
        Assert.Equal(3, anim.Events.Count);
        Assert.Equal("footstep_left", anim.Events[0].EventName);
        Assert.Equal(0.0f, anim.Events[0].Time);
    }

    [Fact]
    public void Parse_DanglyMesh_ParsesDanglyProperties()
    {
        var mdlContent = @"
newmodel cape
beginmodelgeom cape
node danglymesh cape_cloth
  parent NULL
  displacement 1.5
  tightness 0.8
  period 2.0
  verts 3
    0.0 0.0 0.0
    1.0 0.0 0.0
    0.5 1.0 0.0
  faces 1
    0 1 2 1 0 1 2 0
  constraints 3
    255
    255
    0
endnode
endmodelgeom cape
donemodel cape
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        var danglyNode = model.FindNode("cape_cloth") as MdlDanglyNode;
        Assert.NotNull(danglyNode);
        Assert.Equal(1.5f, danglyNode.Displacement);
        Assert.Equal(0.8f, danglyNode.Tightness);
        Assert.Equal(2.0f, danglyNode.Period);
        Assert.Equal(3, danglyNode.Constraints.Length);
        Assert.Equal(255f, danglyNode.Constraints[0]);
        Assert.Equal(0f, danglyNode.Constraints[2]);
    }

    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyModel()
    {
        var reader = new MdlAsciiReader();
        var model = reader.Parse("");

        Assert.NotNull(model);
        Assert.Empty(model.Name);
        Assert.Null(model.GeometryRoot);
    }

    [Fact]
    public void Parse_CommentsIgnored_ParsesCorrectly()
    {
        var mdlContent = @"
# This is a comment
newmodel test
# Another comment
beginmodelgeom test
node dummy test
  parent NULL
  # Comment inside node
  position 1 2 3
endnode
endmodelgeom test
donemodel test
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.NotNull(model);
        Assert.Equal("test", model.Name);
        Assert.Equal(new Vector3(1, 2, 3), model.GeometryRoot!.Position);
    }

    [Fact]
    public void GetMeshNodes_MultipleMeshes_ReturnsAllMeshes()
    {
        var mdlContent = @"
newmodel multi
beginmodelgeom multi
node dummy root
  parent NULL
  node trimesh mesh1
    parent root
    verts 3
      0 0 0
      1 0 0
      0 1 0
    faces 1
      0 1 2 1 0 1 2 0
  endnode
  node trimesh mesh2
    parent root
    verts 3
      2 0 0
      3 0 0
      2 1 0
    faces 1
      0 1 2 1 0 1 2 0
  endnode
endnode
endmodelgeom multi
donemodel multi
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        var meshes = model.GetMeshNodes().ToList();
        Assert.Equal(2, meshes.Count);
        Assert.Equal("mesh1", meshes[0].Name);
        Assert.Equal("mesh2", meshes[1].Name);
    }

    [Fact]
    public void Parse_BoundingBox_CalculatedFromVertices()
    {
        var mdlContent = @"
newmodel bounds
beginmodelgeom bounds
node trimesh box
  parent NULL
  verts 8
    -1 -1 -1
    1 -1 -1
    1 1 -1
    -1 1 -1
    -1 -1 1
    1 -1 1
    1 1 1
    -1 1 1
  faces 2
    0 1 2 1 0 1 2 0
    4 5 6 1 4 5 6 0
endnode
endmodelgeom bounds
donemodel bounds
";

        var reader = new MdlAsciiReader();
        var model = reader.Parse(mdlContent);

        Assert.Equal(new Vector3(-1, -1, -1), model.BoundingMin);
        Assert.Equal(new Vector3(1, 1, 1), model.BoundingMax);
        Assert.True(model.Radius > 0);
    }
}
