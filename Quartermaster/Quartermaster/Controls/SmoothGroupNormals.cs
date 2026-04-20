// Smoothing-group-aware per-vertex normal computation for NWN MDL meshes.
//
// Why this exists (#2026): NWN binary MDL files store per-vertex normals,
// but for many meshes (notably part-based heads like pfh0_head001) these
// stored normals are unreliable or asymmetric — an artifact of the 2002-era
// BioWare compiler's mirrored-half workflow. The Aurora Engine's own
// renderer ignores those stored normals for heads and computes per-vertex
// normals at load time by averaging per-face plane normals, scoped by the
// face's smoothgroup bitmask (each face's SurfaceId field).
//
// Algorithm (matches NWN ASCII-to-binary compiler semantics):
//   For each vertex v:
//     For each face f that uses v:
//       Accumulate plane_normal[f] into a bucket keyed by f.SurfaceId
//     Pick the bucket: all faces sharing at least one smoothgroup bit with
//     any other face at v blend; faces with no shared bit stay separate.
//
// We implement a simple approximation: for each face using vertex v, sum
// the plane normals of OTHER faces at v that share at least one SurfaceId
// bit with f (bitwise AND != 0). This gives each face's 3 vertex positions
// a normal that's smoothed across its smoothing group.
//
// Returns one normal per face-corner (3 per face). The caller supplies
// that back to the vertex buffer keyed by face-corner, or welds by
// (position, normal, UV) triples to dedup matching corners.

using System.Collections.Generic;
using System.Numerics;
using Radoub.Formats.Mdl;

namespace Quartermaster.Controls;

public static class SmoothGroupNormals
{
    /// <summary>
    /// Compute per-vertex normals using NWN smoothgroup semantics. Returns a
    /// parallel array where result[i] is the normal for vertex i.
    ///
    /// If two faces at the same vertex have no shared smoothgroup bit, the
    /// vertex inherits whichever face's smoothing group is more common (last
    /// one wins for a simple per-vertex buffer). For faithful hard-edge
    /// support, callers should duplicate vertices per face-corner and use
    /// <see cref="ComputePerCorner"/> instead.
    /// </summary>
    public static Vector3[] ComputePerVertex(Vector3[] vertices, MdlFace[] faces)
    {
        var result = new Vector3[vertices.Length];
        var perFaceNormal = BuildFaceNormals(vertices, faces);
        var facesAtVertex = BuildVertexToFaces(vertices.Length, faces);

        for (int v = 0; v < vertices.Length; v++)
        {
            var incident = facesAtVertex[v];
            if (incident == null || incident.Count == 0)
            {
                result[v] = Vector3.UnitZ;
                continue;
            }

            // Combine smoothgroup bitmasks from all incident faces. Any face
            // that shares at least one bit with this combined mask contributes.
            int combinedMask = 0;
            foreach (var fi in incident) combinedMask |= faces[fi].SurfaceId;

            var sum = Vector3.Zero;
            foreach (var fi in incident)
            {
                if ((faces[fi].SurfaceId & combinedMask) != 0)
                    sum += perFaceNormal[fi];
            }

            result[v] = sum.LengthSquared() > 1e-9f
                ? Vector3.Normalize(sum)
                : (incident.Count > 0 ? perFaceNormal[incident[0]] : Vector3.UnitZ);
        }

        return result;
    }

    /// <summary>
    /// Compute normals per face-corner (3 per face, in face order).
    /// For each face corner (f, c), the normal is the average of plane
    /// normals of faces at the same vertex that share at least one
    /// smoothgroup bit with f.SurfaceId.
    ///
    /// This preserves hard edges between faces whose smoothgroup masks
    /// don't overlap — e.g. smoothgroup 1 and smoothgroup 2 with no shared
    /// bits produce distinct normals at a shared-position seam.
    /// </summary>
    public static Vector3[] ComputePerCorner(Vector3[] vertices, MdlFace[] faces)
    {
        var result = new Vector3[faces.Length * 3];
        var perFaceNormal = BuildFaceNormals(vertices, faces);
        var facesAtVertex = BuildVertexToFaces(vertices.Length, faces);

        for (int f = 0; f < faces.Length; f++)
        {
            var face = faces[f];
            int sg = face.SurfaceId;
            int[] vs = { face.VertexIndex0, face.VertexIndex1, face.VertexIndex2 };
            for (int c = 0; c < 3; c++)
            {
                int v = vs[c];
                var incident = facesAtVertex[v];
                if (incident == null || incident.Count == 0)
                {
                    result[f * 3 + c] = perFaceNormal[f];
                    continue;
                }

                var sum = Vector3.Zero;
                int contribCount = 0;
                foreach (var fi in incident)
                {
                    if ((faces[fi].SurfaceId & sg) != 0)
                    {
                        sum += perFaceNormal[fi];
                        contribCount++;
                    }
                }

                result[f * 3 + c] = sum.LengthSquared() > 1e-9f
                    ? Vector3.Normalize(sum)
                    : perFaceNormal[f];
            }
        }

        return result;
    }

    private static Vector3[] BuildFaceNormals(Vector3[] vertices, MdlFace[] faces)
    {
        var fn = new Vector3[faces.Length];
        for (int i = 0; i < faces.Length; i++)
        {
            var face = faces[i];
            if (face.VertexIndex0 >= vertices.Length ||
                face.VertexIndex1 >= vertices.Length ||
                face.VertexIndex2 >= vertices.Length)
            {
                fn[i] = Vector3.UnitZ;
                continue;
            }

            // Prefer the MDL's stored plane normal when it's a unit vector —
            // it matches the authoring tool's intent exactly. Fall back to
            // cross product for faces missing a valid plane normal.
            var stored = face.PlaneNormal;
            var storedLen = stored.Length();
            if (storedLen > 0.95f && storedLen < 1.05f)
            {
                fn[i] = stored / storedLen;
                continue;
            }

            var a = vertices[face.VertexIndex0];
            var b = vertices[face.VertexIndex1];
            var c = vertices[face.VertexIndex2];
            var cross = Vector3.Cross(b - a, c - a);
            fn[i] = cross.LengthSquared() > 1e-12f
                ? Vector3.Normalize(cross)
                : Vector3.UnitZ;
        }
        return fn;
    }

    private static List<int>?[] BuildVertexToFaces(int vertexCount, MdlFace[] faces)
    {
        var map = new List<int>?[vertexCount];
        for (int i = 0; i < faces.Length; i++)
        {
            var face = faces[i];
            AddFace(map, face.VertexIndex0, i);
            AddFace(map, face.VertexIndex1, i);
            AddFace(map, face.VertexIndex2, i);
        }
        return map;
    }

    private static void AddFace(List<int>?[] map, int v, int faceIndex)
    {
        if (v < 0 || v >= map.Length) return;
        var list = map[v];
        if (list == null)
        {
            list = new List<int>(4);
            map[v] = list;
        }
        list.Add(faceIndex);
    }
}
