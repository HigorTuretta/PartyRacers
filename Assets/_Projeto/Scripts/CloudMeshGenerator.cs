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

    // Nuvem low-poly "de verdade": vários lóbulos (icosferas) agrupados num único mesh achatado.
    // Lê como uma nuvenzinha volumétrica em vez de uma bola só. Mantém a contagem de triângulos baixa.
    public static Mesh GenerateCluster(int lobes = 4, float bumpStrength = 0.22f, float noiseScale = 2.5f, float seed = 0f)
    {
        lobes = Mathf.Clamp(lobes, 2, 7);

        var allVerts = new List<Vector3>();
        var allTris = new List<int>();
        var rng = new System.Random(Mathf.RoundToInt(seed * 1000f) ^ (lobes * 9176));

        for (int l = 0; l < lobes; l++)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Icosaedro cru (20 faces, sem subdivisão) => facetas grandes e chapadas:
            // é isso que dá a leitura "low-poly volumétrica" em vez de uma esfera lisa.
            BuildIcosahedron(verts, tris);

            float lobeSeed = seed + l * 13.7f;

            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 dir = verts[i].normalized;
                float n = SampleNoise(dir, noiseScale, lobeSeed);
                verts[i] = dir * (1f + Mathf.Pow(n, 1.6f) * bumpStrength);
            }

            Vector3 center;
            float scale;

            if (l == 0)
            {
                center = Vector3.zero;
                scale = 1f;
            }
            else
            {
                // Lóbulos espalhados num anel horizontal e SEMPRE para cima (height >= 0):
                // a parte de baixo fica plana, como uma nuvenzinha cartunesca pousada.
                float angle = (l - 1) / Mathf.Max(1f, lobes - 1f) * Mathf.PI * 2f + (float)rng.NextDouble() * 0.6f;
                float dist = 0.62f + 0.30f * (float)rng.NextDouble();
                float height = 0.05f + 0.30f * (float)rng.NextDouble();
                center = new Vector3(Mathf.Cos(angle) * dist, height, Mathf.Sin(angle) * dist);
                scale = 0.55f + 0.30f * (float)rng.NextDouble();
            }

            int baseIndex = allVerts.Count;

            for (int i = 0; i < verts.Count; i++)
                allVerts.Add(center + verts[i] * scale);

            for (int i = 0; i < tris.Count; i++)
                allTris.Add(tris[i] + baseIndex);
        }

        FlattenBottom(allVerts, -0.30f);
        NormalizeToUnit(allVerts);

        return BuildFlatMesh(allVerts, allTris);
    }

    // Achata a base do conjunto: vértices abaixo do nível são levados ao plano,
    // criando o "chão" plano de uma nuvem em vez de uma bola fechada.
    private static void FlattenBottom(List<Vector3> verts, float level)
    {
        for (int i = 0; i < verts.Count; i++)
        {
            if (verts[i].y < level)
                verts[i] = new Vector3(verts[i].x, level, verts[i].z);
        }
    }

    // Recentra e normaliza o conjunto para caber aproximadamente num raio unitário,
    // mantendo a escala dos puffs (startScale/endScale) consistente com a versão de bola única.
    private static void NormalizeToUnit(List<Vector3> verts)
    {
        if (verts.Count == 0)
            return;

        Vector3 min = verts[0];
        Vector3 max = verts[0];

        for (int i = 1; i < verts.Count; i++)
        {
            min = Vector3.Min(min, verts[i]);
            max = Vector3.Max(max, verts[i]);
        }

        Vector3 center = (min + max) * 0.5f;
        float radius = Mathf.Max(0.0001f, ((max - min) * 0.5f).magnitude);

        for (int i = 0; i < verts.Count; i++)
            verts[i] = (verts[i] - center) / radius;
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
        var flatColors  = new Color[count];
        var flatTris    = new int[count];

        // Faixa vertical do conjunto, para assar um gradiente de sombreamento.
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < verts.Count; i++)
        {
            if (verts[i].y < minY) minY = verts[i].y;
            if (verts[i].y > maxY) maxY = verts[i].y;
        }
        float rangeY = Mathf.Max(0.0001f, maxY - minY);

        // Gradiente "ambient occlusion" estilizado: base sombreada, topo claro.
        // O shader de partículas multiplica o albedo pela cor de vértice, então isto
        // dá a leitura volumétrica/low-poly mesmo com iluminação plana.
        Color bottomShade = new Color(0.55f, 0.60f, 0.70f, 1f);
        Color topShade    = new Color(1f, 1f, 1f, 1f);

        for (int i = 0; i < count; i += 3)
        {
            Vector3 a = verts[tris[i]];
            Vector3 b = verts[tris[i + 1]];
            Vector3 c = verts[tris[i + 2]];
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;

            // Faces viradas para baixo recebem um sombreamento extra (subsolo da nuvem).
            float faceDown = Mathf.Clamp01(-n.y);

            flatVerts[i]     = a; flatVerts[i + 1]     = b; flatVerts[i + 2]     = c;
            flatNormals[i]   = n; flatNormals[i + 1]   = n; flatNormals[i + 2]   = n;

            flatColors[i]     = ShadeVertex(a, minY, rangeY, faceDown, bottomShade, topShade);
            flatColors[i + 1] = ShadeVertex(b, minY, rangeY, faceDown, bottomShade, topShade);
            flatColors[i + 2] = ShadeVertex(c, minY, rangeY, faceDown, bottomShade, topShade);

            flatTris[i]      = i; flatTris[i + 1]      = i + 1; flatTris[i + 2]  = i + 2;
        }

        var mesh = new Mesh();
        mesh.vertices = flatVerts;
        mesh.triangles = flatTris;
        mesh.normals = flatNormals;
        mesh.colors = flatColors;
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Color ShadeVertex(Vector3 v, float minY, float rangeY, float faceDown, Color bottom, Color top)
    {
        float h = Mathf.Clamp01((v.y - minY) / rangeY);
        h = Mathf.SmoothStep(0f, 1f, h);
        Color c = Color.Lerp(bottom, top, h);
        return Color.Lerp(c, bottom, faceDown * 0.6f);
    }
}
