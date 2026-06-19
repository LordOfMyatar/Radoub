namespace Radoub.UI.Controls;

/// <summary>
/// Mesh visibility gate for the model preview (#2498). Matches the Aurora engine
/// (nwnexplorer <c>MdlRtNode.cpp</c>, borealis) — a mesh draws iff its MDL Render flag is set
/// and it has geometry. No vertex-count or shared-bitmap heuristic: the old
/// <c>MeshSkipHeuristic</c> (#1676/#2057) hid real geometry that reuses the body texture
/// (hands, necks, hair, dragon spikes, tongues), so it was removed.
/// </summary>
public static class MeshVisibility
{
    /// <summary>
    /// Whether a mesh should be rendered. True iff Render is set and the mesh has geometry.
    /// </summary>
    public static bool ShouldRender(bool render, int vertexCount, int faceCount)
        => render && vertexCount > 0 && faceCount > 0;
}
