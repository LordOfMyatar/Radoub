// Vertex welding for NWN binary MDL meshes.
//
// The NWN binary MDL compiler often emits one vertex per face-corner with
// identical (position, normal, UV) across triangles that share an edge.
// Loading the faces verbatim yields flat-shaded rendering because the GPU
// can't interpolate normals across triangles that don't share vertex indices
// (#2026). This helper collapses duplicate (position, normal, UV) triples
// into a single index so adjacent triangles share vertices and normals
// interpolate across edges.
//
// Vertices that disagree on any attribute (e.g. real smoothing-group splits
// with different normals at the same position) stay separate.

using System.Collections.Generic;
using System.Numerics;

namespace Quartermaster.Controls;

public static class VertexWelder
{
    public readonly struct Result
    {
        public readonly int[] IndexRemap;      // source vertex index → output index (or -1 to drop)
        public readonly int OutputVertexCount; // number of unique (position, normal, UV) triples kept
        public readonly int WeldedCount;       // how many source vertices collapsed into earlier ones

        public Result(int[] indexRemap, int outputVertexCount, int weldedCount)
        {
            IndexRemap = indexRemap;
            OutputVertexCount = outputVertexCount;
            WeldedCount = weldedCount;
        }
    }

    /// <summary>
    /// Build an index remap that welds vertices sharing the same
    /// (position, normal, UV) triple. Positions/normals quantised to 1e-5/1e-4
    /// to tolerate floating-point noise.
    /// </summary>
    /// <param name="positions">Vertex positions (world-space or local).</param>
    /// <param name="normals">Vertex normals (same coordinate frame as positions).</param>
    /// <param name="uvs">UV coordinates (one per vertex); pass zero vectors if the mesh has no UVs.</param>
    /// <param name="dropMask">Optional: true at index i means drop that vertex (e.g. NaN).</param>
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
