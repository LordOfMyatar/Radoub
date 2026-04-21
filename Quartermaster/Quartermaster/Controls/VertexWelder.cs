// Vertex welding for post-weld deduplication of face-corner vertex streams.
//
// Context (#2026): the NWN MDL renderer generates per-face-corner vertex
// attributes (position, smoothgroup-scoped normal, UV) to preserve hard
// edges between disjoint smoothgroups. That expands the vertex buffer
// 3x over the face count. Welding collapses face-corners that share an
// identical (position, normal, UV) triple back into shared vertices so
// the GPU interpolates normals across same-smoothgroup edges.
//
// Welding on three attributes keeps real boundaries intact: disjoint
// smoothgroups produce different normals and stay separate, UV seams
// produce different UVs and stay separate.

using System.Collections.Generic;
using System.Numerics;

namespace Quartermaster.Controls;

public static class VertexWelder
{
    public readonly struct Result
    {
        public readonly int[] IndexRemap;      // source corner index -> output index (or -1 to drop)
        public readonly int OutputVertexCount; // unique (pos, normal, uv) triples
        public readonly int WeldedCount;       // corners collapsed into earlier ones

        public Result(int[] indexRemap, int outputVertexCount, int weldedCount)
        {
            IndexRemap = indexRemap;
            OutputVertexCount = outputVertexCount;
            WeldedCount = weldedCount;
        }
    }

    /// <summary>
    /// Build an index remap that welds corners sharing the same
    /// (position, normal, UV) triple. Positions/normals are quantised to
    /// 1e-5/1e-4 to tolerate floating-point noise from world-transform math.
    /// </summary>
    /// <param name="positions">Per-corner positions (world-space or local).</param>
    /// <param name="normals">Per-corner normals in the same frame as positions.</param>
    /// <param name="uvs">Per-corner UV coordinates; zero vectors if mesh has no UVs.</param>
    /// <param name="dropMask">Optional: true at index i means drop that corner (e.g. NaN or degenerate).</param>
    public static Result Build(Vector3[] positions, Vector3[] normals, Vector2[] uvs, bool[]? dropMask = null)
    {
        int n = positions.Length;
        var remap = new int[n];
        var keyToIndex = new Dictionary<long, int>(n);
        int welded = 0;
        int output = 0;

        for (int i = 0; i < n; i++)
        {
            if (dropMask != null && dropMask[i])
            {
                remap[i] = -1;
                continue;
            }

            var key = WeldKey(positions[i], normals[i], uvs[i]);
            if (keyToIndex.TryGetValue(key, out var existing))
            {
                remap[i] = existing;
                welded++;
            }
            else
            {
                keyToIndex[key] = output;
                remap[i] = output;
                output++;
            }
        }

        return new Result(remap, output, welded);
    }

    private static long WeldKey(Vector3 p, Vector3 n, Vector2 uv)
    {
        unchecked
        {
            long h = 17;
            h = h * 31 + (long)(p.X * 1e5f);
            h = h * 31 + (long)(p.Y * 1e5f);
            h = h * 31 + (long)(p.Z * 1e5f);
            h = h * 31 + (long)(n.X * 1e4f);
            h = h * 31 + (long)(n.Y * 1e4f);
            h = h * 31 + (long)(n.Z * 1e4f);
            h = h * 31 + (long)(uv.X * 1e5f);
            h = h * 31 + (long)(uv.Y * 1e5f);
            return h;
        }
    }
}
