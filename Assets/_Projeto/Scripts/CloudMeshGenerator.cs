using System.Collections.Generic;
using UnityEngine;

public static class CloudMeshGenerator
{
    public static Mesh Generate(float bumpStrength = 0.22f, float noiseScale = 2.5f, float seed = 0f)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        BuildIcosahedron(verts, tris);
        Subdivide(verts, tris);

        for (int i = 0; i < verts.Count; i++)
            verts[i] = verts[i].normalized;

        for (int i = 0; i < verts.Count; i++)
        {
            float n = SampleNoise(verts[i], noiseScale, seed);
            verts[i] = verts[i] * (1f + Mathf.Pow(n, 1.6f) * bumpStrength);
        }

        return BuildFlatMesh(verts, tris);
    }

    private static void BuildIcosahedron(List<Vector3> verts, List<int> tris)
    {
        float t = (1f + Mathf.Sqrt(5f)) * 0.5f;

        verts.AddRange(new[] {
            new Vector3(-1,  t,  0), new Vector3( 1,  t,  0),
            new Vector3(-1, -t,  0), new Vector3( 1, -t,  0),
            new Vector3( 0, -1,  t), new Vector3( 0,  1,  t),
            new Vector3( 0, -1, -t), new Vector3( 0,  1, -t),
            new Vector3( t,  0, -1), new Vector3( t,  0,  1),
            new Vector3(-t,  0, -1), new Vector3(-t,  0,  1),
        });

        tris.AddRange(new[] {
            0,11,5,  0,5,1,  0,1,7,  0,7,10,  0,10,11,
            1,5,9,  5,11,4,  11,10,2,  10,7,6,  7,1,8,
            3,9,4,  3,4,2,  3,2,6,  3,6,8,  3,8,9,
            4,9,5,  2,4,11,  6,2,10,  8,6,7,  9,8,1,
        });
    }

    private static void Subdivide(List<Vector3> verts, List<int> tris)
    {
        var midCache = new Dictionary<long, int>();
        var newTris = new List<int>(tris.Count * 4);

        for (int i = 0; i < tris.Count; i += 3)
        {
            int a = tris[i], b = tris[i + 1], c = tris[i + 2];
            int ab = GetMidpoint(a, b, verts, midCache);
            int bc = GetMidpoint(b, c, verts, midCache);
            int ca = GetMidpoint(c, a, verts, midCache);

            newTris.AddRange(new[] { a, ab, ca,  b, bc, ab,  c, ca, bc,  ab, bc, ca });
        }

        tris.Clear();
        tris.AddRange(newTris);
    }

    private static int GetMidpoint(int a, int b, List<Vector3> verts, Dictionary<long, int> cache)
    {
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        long key = ((long)lo << 32) | (uint)hi;

        if (cache.TryGetValue(key, out int idx))
            return idx;

        idx = verts.Count;
        verts.Add((verts[a] + verts[b]) * 0.5f);
        cache[key] = idx;
        return idx;
    }

    private static float SampleNoise(Vector3 v, float scale, float seed)
    {
        float n1 = Mathf.PerlinNoise(v.x * scale + seed,          v.y * scale + seed * 0.71f);
        float n2 = Mathf.PerlinNoise(v.y * scale + seed * 1.33f,  v.z * scale + seed * 0.53f);
        float n3 = Mathf.PerlinNoise(v.z * scale + seed * 0.89f,  v.x * scale + seed * 1.17f);
        return (n1 + n2 * 0.6f + n3 * 0.4f) / 2f;
    }

    private static Mesh BuildFlatMesh(List<Vector3> verts, List<int> tris)
    {
        int count = tris.Count;
        var flatVerts   = new Vector3[count];
        var flatNormals = new Vector3[count];
        var flatTris    = new int[count];

        for (int i = 0; i < count; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;

            flatVerts[i]     = a; flatVerts[i + 1]     = b; flatVerts[i + 2]     = c;
            flatNormals[i]   = n; flatNormals[i + 1]   = n; flatNormals[i + 2]   = n;
            flatTris[i]      = i; flatTris[i + 1]      = i + 1; flatTris[i + 2]  = i + 2;
        }

        var mesh = new Mesh();
        mesh.vertices = flatVerts;
        mesh.triangles = flatTris;
        mesh.normals = flatNormals;
        mesh.RecalculateBounds();
        return mesh;
    }
}
