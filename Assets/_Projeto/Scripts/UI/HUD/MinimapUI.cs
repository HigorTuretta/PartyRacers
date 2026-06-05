using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MinimapUI : Graphic
{
    [SerializeField] private float trackThickness = 13f;
    [SerializeField] private float trackBorderThickness = 20f;
    [SerializeField] private float dotRadius = 6f;
    [SerializeField] private Color trackColor = new Color(0.90f, 0.94f, 0.98f, 1f);
    [SerializeField] private Color trackBorderColor = new Color(0.02f, 0.04f, 0.06f, 1f);
    [SerializeField] private Color localDotColor = new Color(1f, 1f, 1f, 1f);
    [SerializeField] private Color[] opponentDotColors =
    {
        new Color(1f, 0.82f, 0.10f, 1f),
        new Color(0.12f, 0.70f, 1f, 1f),
        new Color(1f, 0.38f, 0.16f, 1f),
        new Color(0.56f, 1f, 0.20f, 1f),
        new Color(0.72f, 0.28f, 1f, 1f)
    };

    private readonly List<Vector2> trackPoints = new List<Vector2>();
    private readonly List<Vector2> dotPoints = new List<Vector2>();
    private readonly List<Color> dotColors = new List<Color>();
    private readonly List<RaceCheckpoint> sortedCheckpoints = new List<RaceCheckpoint>();

    private static readonly Vector2[] FallbackTrack =
    {
        new Vector2(0.20f, 0.72f),
        new Vector2(0.38f, 0.88f),
        new Vector2(0.67f, 0.75f),
        new Vector2(0.82f, 0.52f),
        new Vector2(0.66f, 0.28f),
        new Vector2(0.43f, 0.20f),
        new Vector2(0.23f, 0.35f),
        new Vector2(0.16f, 0.56f)
    };

    public void SetRaceData(IReadOnlyList<KartController> karts, KartController localKart, RaceCheckpoint[] checkpoints)
    {
        BuildTrack(checkpoints);
        BuildDots(karts, localKart, checkpoints);
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        IReadOnlyList<Vector2> path = trackPoints.Count >= 3 ? trackPoints : FallbackTrack;
        for (int i = 0; i < path.Count; i++)
        {
            Vector2 a = ToRect(path[i]);
            Vector2 b = ToRect(path[(i + 1) % path.Count]);
            AddSegment(vh, a, b, trackBorderThickness, trackBorderColor);
        }

        for (int i = 0; i < path.Count; i++)
        {
            Vector2 a = ToRect(path[i]);
            Vector2 b = ToRect(path[(i + 1) % path.Count]);
            AddSegment(vh, a, b, trackThickness, trackColor);
        }

        for (int i = 0; i < dotPoints.Count; i++)
            AddDisc(vh, ToRect(dotPoints[i]), dotRadius + 2f, Color.black);

        for (int i = 0; i < dotPoints.Count; i++)
            AddDisc(vh, ToRect(dotPoints[i]), dotRadius, dotColors[i]);
    }

    private void BuildTrack(RaceCheckpoint[] checkpoints)
    {
        trackPoints.Clear();
        if (checkpoints == null || checkpoints.Length < 3)
            return;

        sortedCheckpoints.Clear();
        for (int i = 0; i < checkpoints.Length; i++)
        {
            if (checkpoints[i] != null)
                sortedCheckpoints.Add(checkpoints[i]);
        }

        if (sortedCheckpoints.Count < 3)
            return;

        sortedCheckpoints.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));
        Bounds bounds = BuildBounds(sortedCheckpoints);

        for (int i = 0; i < sortedCheckpoints.Count; i++)
        {
            Vector3 pos = sortedCheckpoints[i].transform.position;
            trackPoints.Add(WorldToNormalized(pos, bounds));
        }
    }

    private void BuildDots(IReadOnlyList<KartController> karts, KartController localKart, RaceCheckpoint[] checkpoints)
    {
        dotPoints.Clear();
        dotColors.Clear();

        if (karts == null || karts.Count == 0)
            return;

        Bounds bounds = checkpoints != null && checkpoints.Length >= 2
            ? BuildBounds(checkpoints)
            : BuildBounds(karts);

        for (int i = 0; i < karts.Count; i++)
        {
            KartController kart = karts[i];
            if (kart == null)
                continue;

            dotPoints.Add(WorldToNormalized(kart.transform.position, bounds));
            dotColors.Add(kart == localKart ? localDotColor : GetOpponentColor(i));
        }
    }

    private static Bounds BuildBounds(IReadOnlyList<RaceCheckpoint> checkpoints)
    {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);
        bool hasBounds = false;

        for (int i = 0; i < checkpoints.Count; i++)
        {
            if (checkpoints[i] == null)
                continue;

            if (!hasBounds)
            {
                bounds = new Bounds(checkpoints[i].transform.position, Vector3.one);
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(checkpoints[i].transform.position);
        }

        bounds.Expand(12f);
        return bounds;
    }

    private static Bounds BuildBounds(IReadOnlyList<KartController> karts)
    {
        Vector3 center = karts[0] != null ? karts[0].transform.position : Vector3.zero;
        Bounds bounds = new Bounds(center, Vector3.one);
        for (int i = 0; i < karts.Count; i++)
        {
            if (karts[i] != null)
                bounds.Encapsulate(karts[i].transform.position);
        }
        bounds.Expand(20f);
        return bounds;
    }

    private static Vector2 WorldToNormalized(Vector3 world, Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 size = bounds.size;
        float x = Mathf.InverseLerp(min.x, min.x + Mathf.Max(1f, size.x), world.x);
        float y = Mathf.InverseLerp(min.z, min.z + Mathf.Max(1f, size.z), world.z);
        return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
    }

    private Vector2 ToRect(Vector2 normalized)
    {
        Rect rect = rectTransform.rect;
        float padding = Mathf.Max(trackBorderThickness, dotRadius + 4f);
        return new Vector2(
            Mathf.Lerp(rect.xMin + padding, rect.xMax - padding, normalized.x),
            Mathf.Lerp(rect.yMin + padding, rect.yMax - padding, normalized.y));
    }

    private Color GetOpponentColor(int index)
    {
        if (opponentDotColors == null || opponentDotColors.Length == 0)
            return Color.yellow;

        return opponentDotColors[Mathf.Abs(index) % opponentDotColors.Length];
    }

    private static void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 direction = b - a;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
        int index = vh.currentVertCount;
        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);
        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }

    private static void AddDisc(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        const int steps = 16;
        int start = vh.currentVertCount;
        vh.AddVert(center, color, Vector2.zero);

        for (int i = 0; i <= steps; i++)
        {
            float angle = Mathf.PI * 2f * i / steps;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            vh.AddVert(point, color, Vector2.zero);
        }

        for (int i = 1; i <= steps; i++)
            vh.AddTriangle(start, start + i, start + i + 1);
    }
}
