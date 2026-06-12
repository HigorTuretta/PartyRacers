using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Caminho amostrado (suavizado por Catmull-Rom CENTRÍPETO) com cache de distâncias.
    /// Usado pelo BotPathFollower em runtime e pelos gizmos/validações do BotRacingLine no Editor
    /// (a curva desenhada na Scene View é EXATAMENTE a que os bots seguem).
    ///
    /// Por que centrípeto: o Catmull-Rom uniforme cria laços/overshoots enormes quando os pontos
    /// têm espaçamentos muito diferentes (ex.: um segmento de 20 m seguido de um de 200 m). O
    /// centrípeto (alpha 0.5) nunca forma laço dentro de um segmento — a curva fica sempre
    /// contida entre os pontos de controle, o que é essencial para rotas autoradas à mão.
    /// </summary>
    public sealed class BotPath
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        private readonly List<float> distanceAt = new List<float>();

        public float TotalLength { get; private set; }
        public bool Looped { get; private set; }
        public bool IsValid => Points.Count >= 2 && TotalLength > 0.1f;

        public struct NearestResult
        {
            public int Segment;
            public Vector3 Point;
            public float DistanceOnPath;
            public float SqrPlanarDistance;
        }

        public void BuildFrom(List<Vector3> source, bool loop, float spacing)
        {
            Points.Clear();
            distanceAt.Clear();
            TotalLength = 0f;
            Looped = loop;

            if (source == null)
                return;

            // Pontos de controle duplicados/colados quebram a parametrização da spline.
            List<Vector3> clean = RemoveNearDuplicates(source, loop);
            if (clean.Count < 2)
                return;

            if (clean.Count < 3)
                Points.AddRange(clean);
            else
                BuildSmoothed(clean, loop, spacing);

            RemoveDuplicateSamples();
            BuildDistanceCache();
        }

        private static List<Vector3> RemoveNearDuplicates(List<Vector3> source, bool loop)
        {
            var clean = new List<Vector3>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (clean.Count > 0 && SqrPlanar(clean[clean.Count - 1], source[i]) < 0.25f)
                    continue;

                clean.Add(source[i]);
            }

            if (loop && clean.Count > 2 && SqrPlanar(clean[0], clean[clean.Count - 1]) < 0.25f)
                clean.RemoveAt(clean.Count - 1);

            return clean;
        }

        private void BuildSmoothed(List<Vector3> pts, bool loop, float spacing)
        {
            int count = pts.Count;
            int segments = loop ? count : count - 1;

            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = loop ? pts[(s - 1 + count) % count] : pts[Mathf.Max(0, s - 1)];
                Vector3 p1 = pts[s % count];
                Vector3 p2 = pts[(s + 1) % count];
                Vector3 p3 = loop ? pts[(s + 2) % count] : pts[Mathf.Min(count - 1, s + 2)];

                float segLen = PlanarDistance(p1, p2);
                int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / Mathf.Max(0.5f, spacing)));

                for (int i = 0; i < steps; i++)
                {
                    float t = i / (float)steps;
                    Points.Add(CatmullRomCentripetal(p0, p1, p2, p3, t));
                }
            }

            if (!loop)
                Points.Add(pts[count - 1]);
        }

        private void RemoveDuplicateSamples()
        {
            for (int i = Points.Count - 1; i > 0; i--)
            {
                if (SqrPlanar(Points[i], Points[i - 1]) < 0.01f)
                    Points.RemoveAt(i);
            }

            if (Looped && Points.Count > 2 && SqrPlanar(Points[0], Points[Points.Count - 1]) < 0.01f)
                Points.RemoveAt(Points.Count - 1);
        }

        private void BuildDistanceCache()
        {
            if (Points.Count == 0)
                return;

            distanceAt.Add(0f);
            TotalLength = 0f;

            for (int i = 1; i < Points.Count; i++)
            {
                TotalLength += PlanarDistance(Points[i - 1], Points[i]);
                distanceAt.Add(TotalLength);
            }

            if (Looped && Points.Count > 2)
                TotalLength += PlanarDistance(Points[Points.Count - 1], Points[0]);
        }

        public int SegmentCount => Points.Count < 2 ? 0 : (Looped ? Points.Count : Points.Count - 1);

        public int NextIndex(int segment)
        {
            int next = segment + 1;
            if (next >= Points.Count)
                return Looped ? 0 : Points.Count - 1;

            return next;
        }

        public float SegmentLength(int segment)
        {
            return PlanarDistance(Points[segment], Points[NextIndex(segment)]);
        }

        /// <summary>
        /// Busca global (todos os segmentos). Use apenas na inicialização, respawn ou quando o
        /// kart se perdeu — durante a navegação normal use <see cref="FindNearestLocal"/>.
        /// </summary>
        public NearestResult FindNearestGlobal(Vector3 pos)
        {
            return FindNearestInWindow(pos, 0, SegmentCount);
        }

        /// <summary>
        /// Busca LOCAL com continuidade: examina apenas uma janela de segmentos em volta do
        /// último segmento conhecido. Isso impede o "teleporte" do progresso quando dois trechos
        /// da pista passam perto um do outro (ex.: ida e volta de uma reta, ou o fechamento do
        /// loop cruzando a área da largada) — o bot mantém o trecho em que realmente está.
        /// </summary>
        public NearestResult FindNearestLocal(Vector3 pos, int hintSegment, float windowMeters)
        {
            int segments = SegmentCount;
            if (segments <= 0)
                return FindNearestGlobal(pos);

            // Converte a janela em nº de segmentos (amostras têm espaçamento ~uniforme).
            float avgSegment = TotalLength / Mathf.Max(1, segments);
            int halfWindow = Mathf.Clamp(Mathf.CeilToInt(windowMeters / Mathf.Max(0.5f, avgSegment)), 4, segments);

            int start = hintSegment - halfWindow;
            int count = halfWindow * 2 + 1;

            if (!Looped)
            {
                start = Mathf.Clamp(start, 0, segments - 1);
                count = Mathf.Min(count, segments - start);
            }
            else if (count >= segments)
            {
                start = 0;
                count = segments;
            }

            return FindNearestInWindow(pos, start, count);
        }

        private NearestResult FindNearestInWindow(Vector3 pos, int start, int count)
        {
            var result = new NearestResult
            {
                Segment = 0,
                Point = Points.Count > 0 ? Points[0] : pos,
                SqrPlanarDistance = float.MaxValue
            };

            int segments = SegmentCount;
            if (segments <= 0)
                return result;

            for (int i = 0; i < count; i++)
            {
                int s = start + i;
                if (Looped)
                    s = ((s % segments) + segments) % segments;
                else if (s < 0 || s >= segments)
                    continue;

                Vector3 a = Points[s];
                Vector3 b = Points[NextIndex(s)];
                float t = ClosestTPlanar(a, b, pos);
                Vector3 projected = Vector3.Lerp(a, b, t);
                float sqr = SqrPlanar(projected, pos);

                if (sqr < result.SqrPlanarDistance)
                {
                    result.SqrPlanarDistance = sqr;
                    result.Segment = s;
                    result.Point = projected;
                    result.DistanceOnPath = distanceAt[s] + SegmentLength(s) * t;
                }
            }

            if (Looped && TotalLength > 0.01f)
                result.DistanceOnPath = Mathf.Repeat(result.DistanceOnPath, TotalLength);

            return result;
        }

        public Vector3 PointAtDistance(float distance)
        {
            if (!IsValid)
                return Points.Count > 0 ? Points[0] : Vector3.zero;

            float target = Looped
                ? Mathf.Repeat(distance, TotalLength)
                : Mathf.Clamp(distance, 0f, TotalLength);

            int segments = SegmentCount;
            for (int s = 0; s < segments; s++)
            {
                float start = distanceAt[s];
                float length = SegmentLength(s);
                float end = start + length;

                if (target <= end || s == segments - 1)
                {
                    float t = length > 0.001f ? Mathf.Clamp01((target - start) / length) : 0f;
                    return Vector3.Lerp(Points[s], Points[NextIndex(s)], t);
                }
            }

            return Points[Points.Count - 1];
        }

        // ------------------------------------------------------------------ spline
        // Catmull-Rom centrípeto (Barry-Goldman). Sem overshoot/laço mesmo com espaçamento
        // de pontos muito desigual — seguro para rotas autoradas à mão.
        private static Vector3 CatmullRomCentripetal(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            const float alpha = 0.5f;

            float t0 = 0f;
            float t1 = t0 + KnotInterval(p0, p1, alpha);
            float t2 = t1 + KnotInterval(p1, p2, alpha);
            float t3 = t2 + KnotInterval(p2, p3, alpha);

            float u = Mathf.Lerp(t1, t2, t);

            Vector3 a1 = LerpSafe(p0, p1, t0, t1, u);
            Vector3 a2 = LerpSafe(p1, p2, t1, t2, u);
            Vector3 a3 = LerpSafe(p2, p3, t2, t3, u);
            Vector3 b1 = LerpSafe(a1, a2, t0, t2, u);
            Vector3 b2 = LerpSafe(a2, a3, t1, t3, u);
            return LerpSafe(b1, b2, t1, t2, u);
        }

        private static float KnotInterval(Vector3 a, Vector3 b, float alpha)
        {
            float dist = Vector3.Distance(a, b);
            return Mathf.Max(0.0001f, Mathf.Pow(dist, alpha));
        }

        private static Vector3 LerpSafe(Vector3 a, Vector3 b, float ta, float tb, float u)
        {
            float span = tb - ta;
            if (span < 0.0001f)
                return a;

            return Vector3.LerpUnclamped(a, b, (u - ta) / span);
        }

        // ------------------------------------------------------------------ helpers
        private static float ClosestTPlanar(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            ab.y = 0f;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return 0f;

            Vector3 ap = p - a;
            ap.y = 0f;
            return Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
        }

        public static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(SqrPlanar(a, b));
        }

        public static float SqrPlanar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
