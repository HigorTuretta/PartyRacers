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
    ///
    /// Navegação com CONTINUIDADE: o ponto-mais-próximo é procurado apenas numa janela em volta
    /// do último segmento conhecido (BotPath.FindNearestLocal). Sem isso, trechos da pista que
    /// passam perto um do outro (ida/volta, fechamento do loop na largada) faziam o progresso
    /// "teleportar" e o bot orbitar um ponto. A busca global só roda na inicialização, respawn
    /// ou quando o bot se afasta demais da janela local.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotPathFollower : MonoBehaviour
    {
        [Tooltip("When falling back to checkpoints, ignore checkpoint indices outside the lap count.")]
        [SerializeField] private bool useOnlyLapCheckpoints = true;

        [Tooltip("Distance in meters between sampled points when smoothing the source line.")]
        [SerializeField] private float sampleSpacing = 4f;

        [Header("Continuidade da navegação")]
        [Tooltip("Janela (m) em volta do último segmento conhecido onde o ponto-mais-próximo é procurado.")]
        [SerializeField] private float nearestSearchWindow = 60f;
        [Tooltip("Se o ponto-mais-próximo local ficar além disso (m), o bot é considerado perdido e refaz a busca global.")]
        [SerializeField] private float lostDistance = 30f;

        [Header("Atalhos / rotas alternativas")]
        [Tooltip("Distância (m) antes da entrada do atalho em que o bot decide se vai usá-lo.")]
        [SerializeField] private float branchDecisionDistance = 28f;
        [Tooltip("Distância (m) da entrada para efetivamente trocar para a rota alternativa.")]
        [SerializeField] private float branchSwitchDistance = 9f;
        [Tooltip("Distância (m) do fim do atalho em que o bot volta para a linha principal.")]
        [SerializeField] private float branchExitDistance = 5f;
        [Tooltip("Se o bot se afastar mais que isso do atalho, abandona e volta à linha principal.")]
        [SerializeField] private float branchAbandonDistance = 14f;

        private BotPath mainPath;
        private readonly List<BranchRuntime> branches = new List<BranchRuntime>();
        private readonly List<ZoneRuntime> zones = new List<ZoneRuntime>();

        // Estado de navegação atual (linha principal ou um branch).
        private BotPath currentPath;
        private int currentBranchIndex = -1;
        private int nearestSegment;
        private bool hasNearestHint;
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
        public int ZoneCount => zones.Count;

        /// <summary>Identifica o caminho atual: -1 = linha principal, >= 0 = índice do branch.</summary>
        public int CurrentPathId => currentBranchIndex;
        public int CurrentSegmentIndex => nearestSegment;

        /// <summary>Distância percorrida (m) ao longo do caminho ATUAL até o ponto mais próximo.</summary>
        public float CurrentDistanceOnPath => nearestDistanceOnPath;

        /// <summary>
        /// Delta de progresso (m) entre duas distâncias no caminho atual, ciente do loop:
        /// positivo = avançou no sentido da corrida, negativo = recuou. Usado pela detecção
        /// de progresso REAL do bot (raspar parede sem avançar = delta ~0).
        /// </summary>
        public float SignedRouteDelta(float fromDistance, float toDistance)
        {
            BotPath p = currentPath ?? mainPath;
            if (p == null || !p.IsValid)
                return 0f;

            if (!p.Looped)
                return toDistance - fromDistance;

            float half = p.TotalLength * 0.5f;
            return Mathf.Repeat(toDistance - fromDistance + half, p.TotalLength) - half;
        }

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
                ActiveZone = null;
                UpcomingZone = null;
                DistanceToUpcomingZone = float.MaxValue;
            }

            public bool IsValid { get; }
            public Vector3 AimPoint { get; }
            public Vector3 NearestPoint { get; }
            public Vector3 Tangent { get; }
            public float DistanceToPath { get; }
            public float SignedLateralError { get; }
            public float UpcomingTurn01 { get; }

            /// <summary>Zona especial em que o bot está AGORA (null = nenhuma).</summary>
            public BotTrackZone ActiveZone { get; internal set; }
            /// <summary>Próxima zona à frente, quando dentro da distância de aproximação dela.</summary>
            public BotTrackZone UpcomingZone { get; internal set; }
            /// <summary>Metros até o início da UpcomingZone.</summary>
            public float DistanceToUpcomingZone { get; internal set; }
        }

        /// <summary>Identidade do bot usada nas decisões de atalho (determinística por corrida).</summary>
        public void SetBotIdentity(int seed, float skill01)
        {
            botSeed = seed;
            botSkill01 = Mathf.Clamp01(skill01);
        }

        /// <summary>
        /// Joga fora o hint de continuidade: na próxima consulta o ponto-mais-próximo é procurado
        /// na rota INTEIRA. Use quando o kart foi parar num lugar inesperado (ex.: rampou e caiu
        /// em outro trecho da pista) — assim ele adota o trecho onde está, segue por ele e dá a
        /// volta, em vez de insistir em voltar para o trecho antigo através de uma parede.
        /// </summary>
        public void ForceGlobalResearch()
        {
            hasNearestHint = false;
        }

        /// <summary>Abandona qualquer atalho e volta a navegar pela linha principal (ex.: após respawn).</summary>
        public void ResetToMainPath()
        {
            currentBranchIndex = -1;
            currentPath = mainPath;
            nearestSegment = 0;
            hasNearestHint = false;
            decidedBranchIndex = -1;
        }

        private float branchSuppressUntil = float.NegativeInfinity;

        /// <summary>
        /// Abandona a branch ATUAL e volta para a linha principal, suprimindo a entrada em
        /// qualquer atalho por alguns segundos. Usado quando o bot trava na boca de um atalho
        /// (ex.: scrum de tráfego num fork): em vez de insistir/respawnar, ele segue pela rota
        /// principal — que também passa pelos checkpoints. Retorna false se não estava em branch.
        /// </summary>
        public bool ForceAbandonBranch(float suppressSeconds)
        {
            if (currentBranchIndex < 0)
                return false;

            ResetToMainPath();
            branchSuppressUntil = Time.time + Mathf.Max(0f, suppressSeconds);
            UpdateNearest(transform.position);
            return true;
        }

        public void Build(int lapCheckpointCount)
        {
            ready = false;
            branches.Clear();
            zones.Clear();
            currentBranchIndex = -1;
            decidedBranchIndex = -1;
            nearestSegment = 0;
            hasNearestHint = false;

            BotRacingLine line;
            List<Vector3> source = ResolveSourcePoints(lapCheckpointCount, out bool looped, out line);
            if (source == null || source.Count < 2)
                return;

            mainPath = new BotPath();
            mainPath.BuildFrom(source, looped, sampleSpacing);
            currentPath = mainPath;

            if (!mainPath.IsValid)
                return;

            if (line != null)
            {
                BuildBranches(line);
                BuildZones(line);
            }

            ready = true;
            nearestPoint = mainPath.Points[0];
            nearestDistanceOnPath = 0f;
        }

        // Projeta cada BotTrackZone na linha principal e guarda o intervalo [início, fim] em
        // metros de rota. A consulta em GetPathFrame é uma comparação barata de distâncias.
        private void BuildZones(BotRacingLine line)
        {
            foreach (BotTrackZone zone in line.GetZones())
            {
                if (zone == null || zone.TotalLength < 0.1f)
                    continue;

                float center = mainPath.FindNearestGlobal(zone.transform.position).DistanceOnPath;
                zones.Add(new ZoneRuntime
                {
                    Source = zone,
                    StartDistance = center - zone.MetersBefore,
                    Length = zone.TotalLength
                });
            }
        }

        private void BuildBranches(BotRacingLine line)
        {
            foreach (BotRouteBranch branchSource in line.GetBranches())
            {
                List<Vector3> pts = new List<Vector3>(branchSource.GetWorldPoints());
                if (pts.Count < 2)
                    continue;

                var path = new BotPath();
                path.BuildFrom(pts, false, sampleSpacing);
                if (!path.IsValid)
                    continue;

                // Entrada/saída: projeção do primeiro/último ponto do branch na linha principal.
                BotPath.NearestResult entry = mainPath.FindNearestGlobal(pts[0]);
                BotPath.NearestResult exit = mainPath.FindNearestGlobal(pts[pts.Count - 1]);

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

        /// <summary>
        /// Ponto no caminho ATUAL (principal ou branch) a 'aheadDistance' metros à frente do
        /// ponto-mais-próximo corrente. Exige que GetPathFrame tenha sido chamado neste frame
        /// (o estado de navegação precisa estar atualizado). Usado pela IA para "ler" a pista à
        /// frente (curvatura, ponto de mira pós-rampa) como um piloto faria.
        /// </summary>
        public Vector3 PointAhead(float aheadDistance)
        {
            if (!IsReady)
                return transform.position + transform.forward * Mathf.Max(1f, aheadDistance);

            return GetPointAhead(Mathf.Max(0f, aheadDistance));
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

            var frame = new PathFrame(true, aim, nearestPoint, tangent, distanceToPath, signedError, upcomingTurn01);
            FillZoneInfo(ref frame);
            return frame;
        }

        // Zona ativa + próxima zona dentro da distância de aproximação. Zonas valem apenas na
        // linha principal (em um branch o bot segue a rota alternativa sem marcadores).
        private void FillZoneInfo(ref PathFrame frame)
        {
            if (currentBranchIndex >= 0 || zones.Count == 0)
                return;

            float total = mainPath.TotalLength;
            BotTrackZone active = null;
            float activeCenterDist = float.MaxValue;
            BotTrackZone upcoming = null;
            float upcomingDistance = float.MaxValue;

            for (int i = 0; i < zones.Count; i++)
            {
                ZoneRuntime zone = zones[i];

                // Distância (m) do bot até o INÍCIO da zona, no sentido da corrida.
                float aheadToStart = mainPath.Looped
                    ? Mathf.Repeat(zone.StartDistance - nearestDistanceOnPath, total)
                    : zone.StartDistance - nearestDistanceOnPath;

                // Dentro da zona: o início ficou para trás há menos que o comprimento dela.
                float behindStart = mainPath.Looped ? total - aheadToStart : -aheadToStart;
                if (behindStart >= 0f && behindStart <= zone.Length)
                {
                    // Zonas sobrepostas: vence a de CENTRO mais próximo (comportamento previsível).
                    float toCenter = Mathf.Abs(behindStart - zone.Length * 0.5f);
                    if (toCenter < activeCenterDist)
                    {
                        activeCenterDist = toCenter;
                        active = zone.Source;
                    }
                    continue;
                }

                if (aheadToStart >= 0f
                    && aheadToStart <= zone.Source.ApproachDistance
                    && aheadToStart < upcomingDistance)
                {
                    upcoming = zone.Source;
                    upcomingDistance = aheadToStart;
                }
            }

            frame.ActiveZone = active;
            frame.UpcomingZone = upcoming;
            frame.DistanceToUpcomingZone = upcomingDistance;
        }

        public float PlanarDistanceToPath(Vector3 pos)
        {
            if (!IsReady)
                return 0f;

            UpdateNearest(pos);
            return BotPath.PlanarDistance(nearestPoint, pos);
        }

        // ------------------------------------------------------------------ navegação
        private void UpdateNearest(Vector3 pos)
        {
            BotPath.NearestResult result;

            if (!hasNearestHint)
            {
                result = currentPath.FindNearestGlobal(pos);
                hasNearestHint = true;
            }
            else
            {
                result = currentPath.FindNearestLocal(pos, nearestSegment, nearestSearchWindow);

                // Perdeu a janela local (ex.: empurrado/teleportado para longe): busca global.
                if (result.SqrPlanarDistance > lostDistance * lostDistance)
                    result = currentPath.FindNearestGlobal(pos);
            }

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
                bool tooFar = BotPath.PlanarDistance(nearestPoint, pos) > branchAbandonDistance;

                if (nearEnd || tooFar)
                {
                    int exitSegmentHint = SegmentHintForDistance(mainPath, branch.ExitDistanceOnMain);
                    ResetToMainPath();
                    // Reaproveita a posição de saída do branch como hint na linha principal
                    // (evita que a busca global escolha outro trecho da pista que passe perto).
                    nearestSegment = exitSegmentHint;
                    hasNearestHint = !tooFar;
                    UpdateNearest(pos);
                }

                return;
            }

            if (branches.Count == 0)
                return;

            // Atalho suprimido temporariamente (acabou de abandonar um fork congestionado).
            if (Time.time < branchSuppressUntil)
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
                hasNearestHint = false; // branch é curto — busca global nele é barata e segura
                decidedBranchIndex = -1;
                UpdateNearest(pos);
            }
        }

        private static int SegmentHintForDistance(BotPath path, float distance)
        {
            int segments = path.SegmentCount;
            if (segments <= 0 || path.TotalLength < 0.01f)
                return 0;

            float normalized = Mathf.Repeat(distance, path.TotalLength) / path.TotalLength;
            return Mathf.Clamp(Mathf.FloorToInt(normalized * segments), 0, segments - 1);
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
            public BotPath Path;
            public float EntryDistanceOnMain;
            public float ExitDistanceOnMain;
        }

        private class ZoneRuntime
        {
            public BotTrackZone Source;
            public float StartDistance; // m na linha principal (pode ser negativo; o wrap é resolvido na consulta)
            public float Length;        // metersBefore + metersAfter
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
