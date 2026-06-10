using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Resolve o traçado que o bot segue. Prioridade da fonte:
    /// 1. BotRacingLine autorado na cena (linha principal + BotRouteBranch para atalhos).
    /// 2. RaceCheckpoints ativos ordenados por CheckpointIndex (fallback).
    ///
    /// Suporta rotas alternativas: cada BotRouteBranch filho do BotRacingLine vira um caminho
    /// opcional com ponto de entrada/saída projetados na linha principal. A decisão de usar o
    /// atalho é por bot (seed + habilidade + chance configurada no branch) a cada passagem.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotPathFollower : MonoBehaviour
    {
        [Tooltip("When falling back to checkpoints, ignore checkpoint indices outside the lap count.")]
        [SerializeField] private bool useOnlyLapCheckpoints = true;

        [Tooltip("Distance in meters between sampled points when smoothing the source line.")]
        [SerializeField] private float sampleSpacing = 4f;

        [Header("Atalhos / rotas alternativas")]
        [Tooltip("Distância (m) antes da entrada do atalho em que o bot decide se vai usá-lo.")]
        [SerializeField] private float branchDecisionDistance = 28f;
        [Tooltip("Distância (m) da entrada para efetivamente trocar para a rota alternativa.")]
        [SerializeField] private float branchSwitchDistance = 9f;
        [Tooltip("Distância (m) do fim do atalho em que o bot volta para a linha principal.")]
        [SerializeField] private float branchExitDistance = 5f;
        [Tooltip("Se o bot se afastar mais que isso do atalho, abandona e volta à linha principal.")]
        [SerializeField] private float branchAbandonDistance = 14f;

        private PathData mainPath;
        private readonly List<BranchRuntime> branches = new List<BranchRuntime>();

        // Estado de navegação atual (linha principal ou um branch).
        private PathData currentPath;
        private int currentBranchIndex = -1;
        private int nearestSegment;
        private Vector3 nearestPoint;
        private float nearestDistanceOnPath;
        private bool ready;

        // Decisão pendente de atalho (uma por passagem pela janela de decisão).
        private int decidedBranchIndex = -1;
        private bool decidedTake;
        private int decisionRollCounter;

        private int botSeed;
        private float botSkill01 = 0.5f;

        public bool IsReady => ready && mainPath != null && mainPath.IsValid;
        public int WaypointCount => mainPath != null ? mainPath.Points.Count : 0;
        public float TotalLength => mainPath != null ? mainPath.TotalLength : 0f;
        public Vector3 CurrentNearestPoint => IsReady ? nearestPoint : transform.position;
        public bool IsOnBranch => currentBranchIndex >= 0;
        public int BranchCount => branches.Count;

        public struct PathFrame
        {
            public PathFrame(
                bool isValid,
                Vector3 aimPoint,
                Vector3 nearestPoint,
                Vector3 tangent,
                float distanceToPath,
                float signedLateralError,
                float upcomingTurn01)
            {
                IsValid = isValid;
                AimPoint = aimPoint;
                NearestPoint = nearestPoint;
                Tangent = tangent;
                DistanceToPath = distanceToPath;
                SignedLateralError = signedLateralError;
                UpcomingTurn01 = upcomingTurn01;
            }

            public bool IsValid { get; }
            public Vector3 AimPoint { get; }
            public Vector3 NearestPoint { get; }
            public Vector3 Tangent { get; }
            public float DistanceToPath { get; }
            public float SignedLateralError { get; }
            public float UpcomingTurn01 { get; }
        }

        /// <summary>Identidade do bot usada nas decisões de atalho (determinística por corrida).</summary>
        public void SetBotIdentity(int seed, float skill01)
        {
            botSeed = seed;
            botSkill01 = Mathf.Clamp01(skill01);
        }

        /// <summary>Abandona qualquer atalho e volta a navegar pela linha principal (ex.: após respawn).</summary>
        public void ResetToMainPath()
        {
            currentBranchIndex = -1;
            currentPath = mainPath;
            nearestSegment = 0;
            decidedBranchIndex = -1;
        }

        public void Build(int lapCheckpointCount)
        {
            ready = false;
            branches.Clear();
            currentBranchIndex = -1;
            decidedBranchIndex = -1;
            nearestSegment = 0;

            BotRacingLine line;
            List<Vector3> source = ResolveSourcePoints(lapCheckpointCount, out bool looped, out line);
            if (source == null || source.Count < 2)
                return;

            mainPath = new PathData();
            mainPath.BuildFrom(source, looped, sampleSpacing);
            currentPath = mainPath;

            if (!mainPath.IsValid)
                return;

            if (line != null)
                BuildBranches(line);

            ready = true;
            nearestPoint = mainPath.Points[0];
            nearestDistanceOnPath = 0f;
        }

        private void BuildBranches(BotRacingLine line)
        {
            foreach (BotRouteBranch branchSource in line.GetBranches())
            {
                List<Vector3> pts = new List<Vector3>(branchSource.GetWorldPoints());
                if (pts.Count < 2)
                    continue;

                var path = new PathData();
                path.BuildFrom(pts, false, sampleSpacing);
                if (!path.IsValid)
                    continue;

                // Entrada/saída: projeção do primeiro/último ponto do branch na linha principal.
                PathData.NearestResult entry = mainPath.FindNearest(pts[0], 0);
                PathData.NearestResult exit = mainPath.FindNearest(pts[pts.Count - 1], 0);

                branches.Add(new BranchRuntime
                {
                    Source = branchSource,
                    Path = path,
                    EntryDistanceOnMain = entry.DistanceOnPath,
                    ExitDistanceOnMain = exit.DistanceOnPath
                });
            }
        }

        private List<Vector3> ResolveSourcePoints(int lapCheckpointCount, out bool loop, out BotRacingLine line)
        {
            loop = true;

            line = FindAnyObjectByType<BotRacingLine>(FindObjectsInactive.Exclude);
            if (line != null && line.HasEnoughPoints())
            {
                loop = line.Loop;
                return new List<Vector3>(line.GetWorldPoints());
            }

            line = null;

            RaceCheckpoint[] checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
            if (checkpoints == null || checkpoints.Length == 0)
                return null;

            var list = new List<RaceCheckpoint>(checkpoints);
            list.Sort((a, b) => a.CheckpointIndex.CompareTo(b.CheckpointIndex));

            int detectedLapCount = DetectCheckpointCount(list);
            int effectiveLapCount = Mathf.Max(lapCheckpointCount, detectedLapCount);

            var result = new List<Vector3>();
            for (int i = 0; i < list.Count; i++)
            {
                RaceCheckpoint cp = list[i];
                if (cp == null)
                    continue;

                if (useOnlyLapCheckpoints && effectiveLapCount > 0 && cp.CheckpointIndex >= effectiveLapCount)
                    continue;

                result.Add(cp.transform.position);
            }

            return result;
        }

        private static int DetectCheckpointCount(List<RaceCheckpoint> checkpoints)
        {
            int maxIndex = -1;
            for (int i = 0; i < checkpoints.Count; i++)
            {
                RaceCheckpoint checkpoint = checkpoints[i];
                if (checkpoint != null)
                    maxIndex = Mathf.Max(maxIndex, checkpoint.CheckpointIndex);
            }

            return maxIndex + 1;
        }

        public Vector3 GetAimPoint(Vector3 pos, float lookAhead)
        {
            PathFrame frame = GetPathFrame(pos, lookAhead);
            return frame.IsValid ? frame.AimPoint : pos + transform.forward * 5f;
        }

        public PathFrame GetPathFrame(Vector3 pos, float lookAhead)
        {
            if (!IsReady)
                return new PathFrame(false, pos + transform.forward * 5f, pos, transform.forward, 0f, 0f, 0f);

            UpdateNearest(pos);
            UpdateBranchState(pos);

            float safeLookAhead = Mathf.Max(1f, lookAhead);
            Vector3 aim = GetPointAhead(safeLookAhead);
            Vector3 nearAhead = GetPointAhead(Mathf.Max(3f, safeLookAhead * 0.45f));
            Vector3 farAhead = GetPointAhead(Mathf.Max(6f, safeLookAhead * 1.65f));

            Vector3 tangent = Planar(nearAhead - nearestPoint);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = Planar(aim - nearestPoint);
            if (tangent.sqrMagnitude < 0.001f)
                tangent = transform.forward;
            tangent.Normalize();

            Vector3 farDirection = Planar(farAhead - nearAhead);
            float turnAngle = farDirection.sqrMagnitude > 0.001f ? Vector3.Angle(tangent, farDirection.normalized) : 0f;
            float upcomingTurn01 = Mathf.InverseLerp(8f, 65f, turnAngle);

            Vector3 offset = Planar(pos - nearestPoint);
            float distanceToPath = offset.magnitude;
            float signedError = Vector3.Cross(tangent, offset).y;

            return new PathFrame(true, aim, nearestPoint, tangent, distanceToPath, signedError, upcomingTurn01);
        }

        public float PlanarDistanceToPath(Vector3 pos)
        {
            if (!IsReady)
                return 0f;

            UpdateNearest(pos);
            return Mathf.Sqrt(SqrPlanar(nearestPoint, pos));
        }

        // ------------------------------------------------------------------ navegação
        private void UpdateNearest(Vector3 pos)
        {
            PathData.NearestResult result = currentPath.FindNearest(pos, nearestSegment);
            nearestSegment = result.Segment;
            nearestPoint = result.Point;
            nearestDistanceOnPath = result.DistanceOnPath;
        }

        // Ponto à frente no caminho atual. Em um atalho, ao passar do fim ele continua
        // automaticamente na linha principal (a mira do bot "emenda" as duas rotas).
        private Vector3 GetPointAhead(float aheadDistance)
        {
            float target = nearestDistanceOnPath + aheadDistance;

            if (currentBranchIndex >= 0)
            {
                BranchRuntime branch = branches[currentBranchIndex];
                if (target <= branch.Path.TotalLength)
                    return branch.Path.PointAtDistance(target);

                float overflow = target - branch.Path.TotalLength;
                return mainPath.PointAtDistance(branch.ExitDistanceOnMain + overflow);
            }

            return mainPath.PointAtDistance(target);
        }

        private void UpdateBranchState(Vector3 pos)
        {
            if (currentBranchIndex >= 0)
            {
                BranchRuntime branch = branches[currentBranchIndex];
                bool nearEnd = nearestDistanceOnPath >= branch.Path.TotalLength - branchExitDistance;
                bool tooFar = Mathf.Sqrt(SqrPlanar(nearestPoint, pos)) > branchAbandonDistance;

                if (nearEnd || tooFar)
                {
                    ResetToMainPath();
                    UpdateNearest(pos);
                }

                return;
            }

            if (branches.Count == 0)
                return;

            // Procura o branch mais próximo à frente na linha principal.
            int upcoming = -1;
            float upcomingDistance = float.MaxValue;
            for (int i = 0; i < branches.Count; i++)
            {
                float ahead = DistanceAheadOnMain(branches[i].EntryDistanceOnMain);
                if (ahead <= branchDecisionDistance && ahead < upcomingDistance)
                {
                    upcomingDistance = ahead;
                    upcoming = i;
                }
            }

            if (upcoming < 0)
            {
                // Entrada decidida ficou para trás sem ser usada → libera para a próxima volta.
                if (decidedBranchIndex >= 0 &&
                    DistanceAheadOnMain(branches[decidedBranchIndex].EntryDistanceOnMain) > mainPath.TotalLength * 0.5f)
                {
                    decidedBranchIndex = -1;
                }

                return;
            }

            if (decidedBranchIndex != upcoming)
            {
                decidedBranchIndex = upcoming;
                decidedTake = RollBranchDecision(upcoming);
            }

            if (decidedTake && upcomingDistance <= branchSwitchDistance)
            {
                currentBranchIndex = upcoming;
                currentPath = branches[upcoming].Path;
                nearestSegment = 0;
                decidedBranchIndex = -1;
                UpdateNearest(pos);
            }
        }

        private float DistanceAheadOnMain(float targetDistance)
        {
            if (mainPath.Looped)
                return Mathf.Repeat(targetDistance - nearestDistanceOnPath, mainPath.TotalLength);

            return targetDistance - nearestDistanceOnPath;
        }

        // Decisão determinística por bot/branch/passagem: bots diferentes escolhem rotas
        // diferentes, e o mesmo bot pode variar a cada volta (aleatoriedade controlada).
        private bool RollBranchDecision(int branchIndex)
        {
            BranchRuntime branch = branches[branchIndex];

            if (botSkill01 < branch.Source.MinSkill01)
                return false;

            decisionRollCounter++;
            var rng = new System.Random(botSeed * 397 + branchIndex * 31 + decisionRollCounter * 7919);
            return rng.NextDouble() < branch.Source.TakeChance;
        }

        // ------------------------------------------------------------------ tipos internos
        private class BranchRuntime
        {
            public BotRouteBranch Source;
            public PathData Path;
            public float EntryDistanceOnMain;
            public float ExitDistanceOnMain;
        }

        /// <summary>Caminho amostrado (suavizado por Catmull-Rom) com cache de distâncias.</summary>
        private class PathData
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
            }

            public void BuildFrom(List<Vector3> source, bool loop, float spacing)
            {
                Points.Clear();
                distanceAt.Clear();
                TotalLength = 0f;
                Looped = loop;

                if (source == null || source.Count < 2)
                    return;

                if (source.Count < 3)
                    Points.AddRange(source);
                else
                    BuildSmoothed(source, loop, spacing);

                RemoveDuplicates();
                BuildDistanceCache();
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
                        Points.Add(CatmullRom(p0, p1, p2, p3, t));
                    }
                }

                if (!loop)
                    Points.Add(pts[count - 1]);
            }

            private void RemoveDuplicates()
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

            public NearestResult FindNearest(Vector3 pos, int hintSegment)
            {
                var result = new NearestResult { Segment = hintSegment, Point = Points.Count > 0 ? Points[0] : pos };

                int segments = SegmentCount;
                if (segments <= 0)
                    return result;

                float best = float.MaxValue;

                for (int s = 0; s < segments; s++)
                {
                    Vector3 a = Points[s];
                    Vector3 b = Points[NextIndex(s)];
                    float t = ClosestTPlanar(a, b, pos);
                    Vector3 projected = Vector3.Lerp(a, b, t);
                    float sqr = SqrPlanar(projected, pos);

                    if (sqr < best)
                    {
                        best = sqr;
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
        }

        // ------------------------------------------------------------------ helpers
        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (
                2f * p1 +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static float ClosestTPlanar(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = Planar(b - a);
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 0.0001f)
                return 0f;

            Vector3 ap = Planar(p - a);
            return Mathf.Clamp01(Vector3.Dot(ap, ab) / lenSq);
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(SqrPlanar(a, b));
        }

        private static float SqrPlanar(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
