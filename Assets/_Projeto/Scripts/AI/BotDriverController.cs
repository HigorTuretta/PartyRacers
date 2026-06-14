using PartyRacers.Networking;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PartyRacers.AI
{
    /// <summary>
    /// IA de corrida ARCADE baseada em máquina de estados. NÃO é um seguidor de waypoints: lê a
    /// pista à frente (curvatura, rampas, obstáculos via sensores), calcula uma VELOCIDADE-ALVO por
    /// trecho e busca o melhor traçado na maior velocidade possível, como um piloto faria.
    ///
    /// Alimenta o KartController por input simulado (IKartInputSource) — mesma física dos players.
    ///
    /// Estados (BotState):
    ///  - Launching        largada: full gás, sem freio/handbrake.
    ///  - Racing           reta/curva suave: persegue a linha de corrida na velocidade-alvo.
    ///  - ApproachingCorner curva forte à frente: freia o necessário e mira o ápice.
    ///  - ApproachingRamp  rampa/salto à frente: alinha ao traçado e GARANTE embalo (acelera).
    ///  - Jumping          no ar: corta gás (não capota) e só alinha o nariz com o traçado.
    ///  - AvoidingObstacle sensor detectou parede/obstáculo/borda: desvia para o lado livre SEM
    ///                     ficar lento; só freia se não houver escapatória.
    ///  - Reversing        preso: ré curta apontando para o lado livre.
    ///  - RejoiningRoute   fora do centro do traçado (mas na pista): volta ao traçado.
    ///  - Recovering       pós-pancada/pouso: realinha por um instante.
    ///  - Respawning       queda/capotamento/preso sem solução: respawn (último recurso).
    ///  - Knockback        arremessado por obstáculo do cenário: vira projétil (input neutro).
    ///
    /// Recuperação diferencia: erro de percurso (RejoiningRoute), queda real (Respawning),
    /// rota alternativa válida (Racing em branch), obstáculo (AvoidingObstacle), parede/preso
    /// (Reversing→Respawning). Respawn é FALLBACK; o "breadcrumb" devolve o bot ao último ponto
    /// bom (inclusive dentro de uma branch), nunca ao checkpoint anterior só por sair da main.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BotPathFollower))]
    public class BotDriverController : MonoBehaviour, IKartInputSource
    {
        public enum BotState
        {
            Disabled,
            Launching,
            Racing,
            ApproachingCorner,
            PreparingDrift,
            Drifting,
            ExitingDrift,
            ApproachingRamp,
            Jumping,
            Landing,
            AvoidingObstacle,
            AvoidingTraffic,
            Reversing,
            RejoiningRoute,
            Recovering,
            Respawning,
            Knockback
        }

        // ------------------------------------------------------------------ Velocidade (modelo)
        [Header("Velocidade — modelo de ritmo")]
        [Tooltip("Usa a velocidade máxima do PRÓPRIO kart como teto de reta (mudanças no prefab valem para os bots).")]
        [SerializeField] private bool matchKartMaxSpeed = true;
        [SerializeField, Range(0.6f, 1f)] private float kartMaxSpeedFraction = 0.96f;
        [SerializeField] private float maxStraightSpeedKmh = 150f;
        [Tooltip("Piso de velocidade em curva fechada — ALTO de propósito: bots devem seguir competitivos, não medrosos.")]
        [SerializeField] private float minCornerSpeedKmh = 82f;
        [Tooltip("Piso ABSOLUTO de velocidade-alvo em qualquer trecho (evita bot rastejando).")]
        [SerializeField] private float hardMinSpeedKmh = 55f;
        [Tooltip("Velocidade-alvo ao voltar para o traçado (RejoiningRoute).")]
        [SerializeField] private float rejoinSpeedKmh = 74f;

        [Header("Leitura da pista (look-ahead / curvatura)")]
        [SerializeField] private float baseLookAheadMeters = 16f;
        [Tooltip("Quanto o look-ahead cresce com a velocidade (m por fração da vel. máx).")]
        [SerializeField] private float lookAheadSpeedScale = 22f;
        [Tooltip("Ângulo (graus) de curvatura à frente que JÁ conta como curva severa (satura o freio de curva).")]
        [SerializeField] private float cornerFullAngle = 70f;
        [Tooltip("Ângulo (graus) a partir do qual a IA entra no estado ApproachingCorner.")]
        [SerializeField] private float cornerEnterAngle = 24f;

        [Header("Linha de corrida (variação por bot)")]
        [SerializeField] private float maxRacingLineOffset = 3.6f;

        [Header("Faixas dirigíveis (anti-scrum / ocupação)")]
        [Tooltip("Liga o sistema de faixas: cada bot recebe uma faixa livre por trecho (anti-bolo) " +
                 "via BotTrafficController, em vez de todos perseguirem a mesma linha de corrida.")]
        [SerializeField] private bool useLaneSystem = true;
        [Tooltip("Velocidade (m/s) com que o alvo lateral desliza para a faixa escolhida (troca suave).")]
        [SerializeField] private float laneShiftSpeed = 6f;

        [Header("Drift Driver")]
        [SerializeField] private bool useDrift = true;
        [Tooltip("Curva começa a ser preparada para drift acima deste ângulo lido no look-ahead.")]
        [SerializeField] private float driftPrepareAngle = 24f;
        [Tooltip("Ângulo mínimo para iniciar drift sem uma zona explícita de CurvaForte.")]
        [SerializeField] private float driftEnterAngle = 32f;
        [SerializeField] private float driftExitAngle = 12f;
        [SerializeField] private float driftMinSpeedKmh = 48f;
        [SerializeField] private float driftTargetSpeedKmh = 102f;
        [SerializeField] private float driftHardCornerTargetSpeedKmh = 88f;
        [SerializeField, Range(0.25f, 1f)] private float driftSteerMinimum = 0.48f;
        [SerializeField] private float driftEntryHandbrakeHold = 0.12f;
        [SerializeField] private float driftCooldownSeconds = 0.45f;
        [SerializeField] private float driftObstacleBlockDistance = 5f;
        [SerializeField] private float driftNarrowSpeedFloorKmh = 78f;

        [Header("Largada")]
        [SerializeField] private float launchSeconds = 1.4f;

        [Header("Rampas / saltos")]
        [Tooltip("Velocidade mínima padrão para encarar uma rampa/salto quando a zona não define uma.")]
        [SerializeField] private float defaultRampMinSpeedKmh = 95f;
        [Tooltip("Tempo sem chão (s) antes de assumir o estado Jumping (corta gás, alinha o nariz).")]
        [SerializeField] private float airborneGraceSeconds = 0.28f;
        [Tooltip("Velocidade mínima (km/h) para considerar 'voo de verdade'. Sem chão porém LENTO = " +
                 "wedgeado numa quina (suspensão fora do chão), NÃO um salto — não corta o gás.")]
        [SerializeField] private float minAirborneSpeedKmh = 16f;
        [Tooltip("Ganho do alinhamento de nariz (yaw) no ar.")]
        [SerializeField, Range(0f, 2f)] private float airSteerGain = 0.85f;
        [Tooltip("Após pousar de um voo > este tempo, entra em Recovering por um instante e refaz a busca de rota se caiu longe.")]
        [SerializeField] private float landRecoverAirTime = 0.7f;
        [SerializeField] private float recoveringSeconds = 0.8f;

        // ------------------------------------------------------------------ Sensores
        [Header("Sensores (parede / obstáculo / borda)")]
        [SerializeField] private LayerMask sensorMask = ~0;
        [SerializeField] private float sensorOriginHeight = 0.7f;
        [SerializeField] private float sensorRadius = 0.5f;
        [Tooltip("Alcance base dos sensores frontais; cresce com a velocidade.")]
        [SerializeField] private float sensorRange = 9f;
        [SerializeField] private float sensorRangeSpeedScale = 8f;
        [Tooltip("Ângulos (graus) do leque de sensores frontais. ±0 é o central.")]
        [SerializeField] private float[] sensorFanAngles = { 0f, 14f, -14f, 30f, -30f, 48f, -48f };
        [Tooltip("Acima desta normal.y a superfície é CHÃO/rampa (não conta como parede).")]
        [SerializeField, Range(0f, 1f)] private float wallMaxNormalY = 0.6f;
        [Tooltip("Força do esterço de desvio (quanto a IA vira para o lado livre).")]
        [SerializeField, Range(0f, 1.5f)] private float avoidSteerStrength = 1f;
        [Tooltip("Distância (m) abaixo da qual a IA freia SE não houver lado livre (escapatória).")]
        [SerializeField] private float avoidBrakeDistance = 3.5f;
        [SerializeField] private float obstacleSlalomOffset = 2.8f;
        [SerializeField] private float obstacleSlalomFrequency = 0.085f;

        [Header("Sensor de borda (queda à frente)")]
        [Tooltip("Sonda o chão à frente nesta distância (m) para detectar buraco/borda.")]
        [SerializeField] private float edgeProbeAhead = 4.5f;
        [Tooltip("Queda (m) a partir da qual a borda à frente conta como perigo.")]
        [SerializeField] private float edgeDropDepth = 4f;

        [Header("Outros karts (desvio suave, nunca tratados como parede)")]
        [SerializeField] private float kartProbeDistance = 12f;
        [SerializeField, Range(0f, 1f)] private float kartAvoidanceSteer = 0.75f;
        [SerializeField, Range(0f, 1f)] private float kartThrottleRelief = 0.2f;
        [SerializeField] private float kartBrakeMinSpeedKmh = 55f;
        [Tooltip("ANTI-SCRUM: distância (m) de um kart à frente abaixo da qual o bot entra em modo fila " +
                 "(não empurra, alivia o gás e contorna). Evita o bolo imóvel a full gás nas curvas estreitas.")]
        [SerializeField] private float kartQueueDistance = 5f;
        [Tooltip("Só entra em fila se o kart à frente estiver mais lento que isto (km/h).")]
        [SerializeField] private float kartQueueSpeedKmh = 28f;

        // ------------------------------------------------------------------ Anti-ré / anti-burnout
        [Header("Segurança de input (anti-ré / anti-burnout)")]
        [Tooltip("Abaixo desta velocidade o bot nunca freia (freio parado = ré no KartController).")]
        [SerializeField] private float minBrakeSpeedKmh = 12f;

        // ------------------------------------------------------------------ Progresso / preso
        [Header("Progresso real / preso")]
        [Tooltip("Janela (s) da medição de progresso em METROS DE ROTA.")]
        [SerializeField] private float progressWindowSeconds = 4f;
        [Tooltip("Avanço mínimo (m de rota) na janela; menos que isso (tentando andar) = preso.")]
        [SerializeField] private float progressMinMeters = 4f;
        [Tooltip("Parede a menos de X m + devagar + acelerando por 'blockedSeconds' = bloqueado.")]
        [SerializeField] private float blockedDistance = 3.2f;
        [SerializeField] private float blockedSpeedKmh = 14f;
        [SerializeField] private float blockedSeconds = 0.9f;
        [SerializeField] private float reverseDurationBase = 0.85f;
        [SerializeField] private int maxReverseAttemptsBeforeRespawn = 4;
        [SerializeField] private float recoveryActionCooldown = 1.1f;
        [Tooltip("UN-JAM RÁPIDO: parado (v < hardJamSpeedKmh) PEDINDO gás por este tempo (s) → ré " +
                 "imediata. Bem mais rápido que a janela de progresso (4s); desfaz bolos de karts " +
                 "wedgeados a full gás antes de virarem trava longa.")]
        [SerializeField] private float hardJamSeconds = 1.1f;
        [SerializeField] private float hardJamSpeedKmh = 8f;
        [Tooltip("Largada/respawn: período de graça em que detectores de preso ficam suspensos.")]
        [SerializeField] private float recoveryGraceSeconds = 3.5f;

        // ------------------------------------------------------------------ Fora da rota / queda / respawn
        [Header("Fora da rota / queda / respawn (fallback)")]
        [Tooltip("Distância planar do traçado atual além da qual o bot entra em RejoiningRoute.")]
        [SerializeField] private float rejoinDistance = 7f;
        [Tooltip("Distância planar além da qual conta como FORA DA PISTA (respawn após offTrackSeconds).")]
        [SerializeField] private float offTrackDistance = 16f;
        [SerializeField] private float offTrackSeconds = 2.5f;
        [Tooltip("Caiu este tanto (m) ABAIXO do traçado rastreado, lento → respawn (errou rampa e parou embaixo).")]
        [SerializeField] private float belowRouteHeight = 4.5f;
        [SerializeField] private float belowRouteSeconds = 2f;
        [SerializeField] private float belowRouteMaxSpeedKmh = 25f;
        [Tooltip("Em zona de rampa/salto, tolerância vertical menor: se ficou lento abaixo da rota, está preso na quina/subida.")]
        [SerializeField] private float rampBelowRouteHeight = 2.4f;
        [SerializeField] private float rampBelowRouteSeconds = 0.85f;
        [SerializeField, Range(-1f, 1f)] private float flippedUpThreshold = 0.3f;
        [SerializeField] private float flippedSeconds = 1.6f;
        [Tooltip("Watchdog final: muito lento por este tempo (qualquer motivo) → respawn.")]
        [SerializeField] private float hardStuckSeconds = 8f;
        [Tooltip("Sem passar checkpoint por este tempo → respawn. Deve ser MAIOR que o setor mais lento.")]
        [SerializeField] private float checkpointStallSeconds = 0f;
        [Tooltip("Afastado do trecho rastreado por este tempo → refaz a busca global (adota o trecho onde caiu).")]
        [SerializeField] private float wrongSectionResearchSeconds = 4f;

        // ------------------------------------------------------------------ Branches / atalhos
        [Header("Branches / atalhos")]
        [Tooltip("Grace (s) ao SAIR de uma branch: não dispara off-track enquanto reencaixa na main.")]
        [SerializeField] private float branchExitGraceSeconds = 2.5f;
        [Tooltip("Ao travar dentro de um atalho, abandona-o e segue pela main, sem reentrar por este tempo.")]
        [SerializeField] private float branchAbandonSuppressSeconds = 14f;
        [SerializeField] private bool allowBranchAbandonRecovery = true;

        // ------------------------------------------------------------------ Breadcrumb
        [Header("Breadcrumb (respawn que NÃO volta ao checkpoint)")]
        [SerializeField] private bool useBreadcrumb = true;
        [SerializeField] private float breadcrumbInterval = 0.5f;
        [SerializeField] private float breadcrumbMaxAge = 45f;
        [SerializeField] private float breadcrumbMinSpeedKmh = 18f;
        [SerializeField] private float breadcrumbRespawnCooldown = 2.5f;

        // ------------------------------------------------------------------ Debug
        [Header("Debug")]
        [SerializeField] private bool drawDebug = true;
        [SerializeField] private bool alwaysDrawDebug = false;

        // ================================================================== refs / estado
        private KartController kart;
        private BotPathFollower path;
        private KartRaceTracker tracker;
        private KartRespawn respawn;
        private BotDifficultyProfile profile;
        private Collider[] ownColliders;
        private int seed;

        private BotState state = BotState.Disabled;
        private float stateEnterTime;

        private float racingLineOffset;
        private float wanderPhase;
        private bool wasControlEnabled;
        private float controlEnabledTime = float.NegativeInfinity;
        private KartInputState lastInput;
        private bool handbrakeHeldLastRead;

        // drift
        private float driftInputHoldUntil = float.NegativeInfinity;
        private float driftCooldownUntil = float.NegativeInfinity;
        private float driftExitStateUntil = float.NegativeInfinity;
        private int requestedDriftDirection;
        private string driftReason = "-";
        private string driftBlockReason = "-";
        private bool wasKartDrifting;
        private float activeDriftEntrySpeedKmh;
        private float activeDriftStartTime;

        // tráfego
        private bool trafficAhead;
        private bool trafficSlowAhead;
        private float trafficDistance = float.MaxValue;
        private float trafficSpeedKmh;
        private int trafficAvoidSide;

        // faixas (lane occupancy / anti-scrum)
        private BotTrafficController traffic;
        private float laneTargetOffset;
        private float laneSmoothedOffset;
        private float currentCenterOffset;
        private bool laneYield;
        private float laneGapAhead = float.MaxValue;
        private float laneLeaderSpeedKmh;
        private int laneChangeCount;

        // telemetria de validação
        private float telemetrySeconds;
        private float speedKmhSeconds;
        private float maxObservedSpeedKmh;
        private float below70Seconds;
        private int recoveryCount;
        private int respawnCount;
        private int driftCount;
        private float driftSeconds;
        private float driftEntrySpeedSum;
        private float driftExitSpeedSum;
        private int driftExitCount;
        private float lastDriftEntrySpeedKmh;
        private float lastDriftExitSpeedKmh;

        // tempo
        private float airborneTimer;
        private float reverseTimer;
        private float reverseSteer;

        // progresso (metros de rota)
        private int progressPathId = -2;
        private float progressAnchor;
        private float progressTimer;
        private float lastRouteDelta;

        // detectores
        private float blockedTimer;
        private float hardJamTimer;
        private float offTrackTimer;
        private float belowRouteTimer;
        private float flippedTimer;
        private float hardStuckTimer;
        private float wrongSectionTimer;
        private float lastCheckpointKey = -1f;
        private float lastCheckpointTime;
        private float lastRecoveryActionTime = float.NegativeInfinity;
        private int reverseEscalation;

        // branch
        private int lastPathId = -2;
        private float branchExitGraceUntil = float.NegativeInfinity;

        // breadcrumb
        private bool hasBreadcrumb;
        private Vector3 breadcrumbPos;
        private Quaternion breadcrumbRot;
        private float breadcrumbTime = float.NegativeInfinity;
        private float lastBreadcrumbRespawnTime = float.NegativeInfinity;
        private float nextBreadcrumbTime;

        // perception cache (debug)
        private BotPathFollower.PathFrame frame;
        private Vector3 debugAim;
        private Sensing sensing;
        private float targetSpeedKmh;
        private string slowReason = "-";
        private string lastRecoveryReason = "-";
        private string lastRespawnReason = "-";

        // ================================================================== API de debug
        public BotState State => state;
        public float CurrentSpeedKmh => kart != null ? kart.SpeedKmh : 0f;
        public float TargetSpeedKmh => targetSpeedKmh;
        public string SlowReason => slowReason;
        public int AvoidSide => sensing.freeSide;
        public bool ObstacleDetected => sensing.centerBlocked || sensing.edgeAhead;
        public float FrontSensorDist => sensing.frontDist;
        public float LeftSensorClear => sensing.leftClear;
        public float RightSensorClear => sensing.rightClear;
        public float SecondsWithoutProgress => progressTimer;
        public float SecondsSinceCheckpoint => Time.time - lastCheckpointTime;
        public string RouteKind => path != null && path.IsOnBranch ? "Atalho/Branch" : "Principal";
        public string LastRecoveryReason => lastRecoveryReason;
        public string LastRespawnReason => lastRespawnReason;
        public KartInputState LastInput => lastInput;
        public bool IsInReverseRecovery => state == BotState.Reversing;
        public BotTrackZone ActiveZone => frame.ActiveZone != null ? frame.ActiveZone : frame.UpcomingZone;
        public string DriftReason => driftReason;
        public string DriftBlockReason => driftBlockReason;
        public bool TrafficAhead => trafficAhead;
        public bool TrafficSlowAhead => trafficSlowAhead;
        public float TrafficDistance => trafficDistance;
        public float TrafficSpeedKmh => trafficSpeedKmh;
        public int TrafficAvoidSide => trafficAvoidSide;
        public float AverageSpeedKmh => telemetrySeconds > 0.01f ? speedKmhSeconds / telemetrySeconds : 0f;
        public float TelemetrySeconds => telemetrySeconds;
        public float MaxObservedSpeedKmh => maxObservedSpeedKmh;
        public float Below70Seconds => below70Seconds;
        public int RecoveryCount => recoveryCount;
        public int RespawnCount => respawnCount;
        public int DriftCount => driftCount;
        public float DriftSeconds => driftSeconds;
        public float MeanDriftEntrySpeedKmh => driftCount > 0 ? driftEntrySpeedSum / driftCount : 0f;
        public float MeanDriftExitSpeedKmh => driftExitCount > 0 ? driftExitSpeedSum / driftExitCount : 0f;
        public float LastDriftEntrySpeedKmh => lastDriftEntrySpeedKmh;
        public float LastDriftExitSpeedKmh => lastDriftExitSpeedKmh;
        public int ArtificialCheckpointAdvances => 0;
        public float CurrentLaneOffset => laneTargetOffset;
        public int LaneChangeCount => laneChangeCount;
        public bool IsYieldingLane => laneYield;

        // ================================================================== init
        public void Initialize(KartController kartController, BotDifficultyProfile botProfile, int botSeed, bool allowLocalPlayer = false)
        {
            kart = kartController;
            profile = botProfile ?? new BotDifficultyProfile();
            seed = botSeed;

            if (matchKartMaxSpeed && kart != null && kart.MaxForwardSpeedKmh > 1f)
                maxStraightSpeedKmh = kart.MaxForwardSpeedKmh * kartMaxSpeedFraction;

            if (checkpointStallSeconds > 0f && checkpointStallSeconds < 180f)
                checkpointStallSeconds = 0f;
            maxReverseAttemptsBeforeRespawn = Mathf.Max(4, maxReverseAttemptsBeforeRespawn);
            hardStuckSeconds = Mathf.Max(8f, hardStuckSeconds);

            wanderPhase = (seed % 1000) * 0.137f;
            uint h = (uint)seed * 2654435761u;
            float o01 = (h % 1000u) / 999f;
            racingLineOffset = (o01 * 2f - 1f) * maxRacingLineOffset;

            path = GetComponent<BotPathFollower>();
            tracker = kart != null ? kart.GetComponent<KartRaceTracker>() : null;
            respawn = kart != null ? kart.GetComponent<KartRespawn>() : null;
            ownColliders = kart != null ? kart.GetComponentsInChildren<Collider>(true) : null;
            KartLocalRig rig = kart != null ? kart.GetComponent<KartLocalRig>() : null;
            if (!allowLocalPlayer && rig != null && rig.IsLocalPlayer)
            {
                Debug.LogError($"[BotDriverController] Tentativa de controlar o kart LOCAL '{kart.name}' — ignorada.");
                enabled = false;
                return;
            }

            if (tracker != null)
                tracker.ConfigureCheckpointCount(KartRaceTracker.DetectSceneCheckpointCount());

            if (path != null)
            {
                path.Build(tracker != null ? tracker.TotalCheckpoints : KartRaceTracker.DetectSceneCheckpointCount());
                path.SetBotIdentity(seed, Mathf.InverseLerp(0.75f, 1.05f, profile.throttleScale));
            }

            if (useLaneSystem)
            {
                traffic = BotTrafficController.Instance;
                traffic?.Register(this);
                // Linha natural inicial = a faixa de seed (espalha o pelotão já na largada).
                laneTargetOffset = racingLineOffset;
                laneSmoothedOffset = racingLineOffset;
            }

            ResetTimers();
            lastInput = KartInputState.Neutral;
            kart?.SetInputSource(this);
            wasControlEnabled = kart != null && kart.CanControl;
            controlEnabledTime = wasControlEnabled ? Time.time : float.NegativeInfinity;
            SetState(BotState.Launching);
        }

        private void ResetTimers()
        {
            airborneTimer = 0f;
            reverseTimer = 0f;
            blockedTimer = 0f;
            hardJamTimer = 0f;
            offTrackTimer = 0f;
            belowRouteTimer = 0f;
            flippedTimer = 0f;
            hardStuckTimer = 0f;
            wrongSectionTimer = 0f;
            progressPathId = -2;
            progressTimer = 0f;
            lastRouteDelta = 0f;
            reverseEscalation = 0;
            lastCheckpointKey = -1f;
            lastCheckpointTime = Time.time;
            lastPathId = -2;
            branchExitGraceUntil = float.NegativeInfinity;
            handbrakeHeldLastRead = false;
            driftInputHoldUntil = float.NegativeInfinity;
            driftCooldownUntil = float.NegativeInfinity;
            driftExitStateUntil = float.NegativeInfinity;
            requestedDriftDirection = 0;
            driftReason = "-";
            driftBlockReason = "-";
            trafficAhead = false;
            trafficSlowAhead = false;
            trafficDistance = float.MaxValue;
            trafficSpeedKmh = 0f;
            trafficAvoidSide = 0;
            wasKartDrifting = kart != null && kart.IsDrifting;
        }

        private void SetState(BotState s)
        {
            if (state == s)
                return;
            state = s;
            stateEnterTime = Time.time;
        }

        // ================================================================== ciclo principal (IKartInputSource)
        public KartInputState Read()
        {
            if (kart == null || path == null || !path.IsReady || !enabled)
            {
                SetState(BotState.Disabled);
                handbrakeHeldLastRead = false;
                return Store(KartInputState.Neutral);
            }

            if (!kart.CanControl)
            {
                wasControlEnabled = false;
                handbrakeHeldLastRead = false;
                SetState(BotState.Launching);
                return Store(KartInputState.Neutral);
            }

            if (!wasControlEnabled)
            {
                wasControlEnabled = true;
                controlEnabledTime = Time.time;
                ResetTimers();
                SetState(BotState.Launching);
            }

            if (kart.IsInKnockback)
            {
                SetState(BotState.Knockback);
                handbrakeHeldLastRead = false;
                // Voar/cambalhotar é esperado; zera detectores de preso para não respawnar no voo.
                airborneTimer = 0f;
                flippedTimer = 0f;
                hardStuckTimer = 0f;
                blockedTimer = 0f;
                return Store(KartInputState.Neutral);
            }

            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            Perceive(dt);
            UpdateBreadcrumb();
            DecideState();
            KartInputState input = ComputeInput(dt);
            RunBackgroundDetectors(dt);
            UpdateTelemetry(dt);

            // Reporta pose/estado para o gerente de faixas (anti-scrum). currentCenterOffset é
            // atualizado em DoDrive; em estados sem condução usa o último valor (folga aceitável).
            if (useLaneSystem && traffic != null)
                traffic.Report(this, kart.transform.position, kart.transform.forward, currentCenterOffset, kart.SpeedKmh, path.CurrentPathId);

            return Store(input);
        }

        // ------------------------------------------------------------------ percepção
        private void Perceive(float dt)
        {
            Vector3 pos = kart.transform.position;
            float speed01 = Mathf.Clamp01(kart.SpeedKmh / Mathf.Max(1f, maxStraightSpeedKmh));
            float lookAhead = baseLookAheadMeters + speed01 * lookAheadSpeedScale + profile.lookAheadDistance * 0.4f;

            frame = path.GetPathFrame(pos, lookAhead);

            // ar
            if (kart.IsGrounded)
            {
                if (airborneTimer > landRecoverAirTime && frame.IsValid &&
                    path.PlanarDistanceToPath(pos) > rejoinDistance)
                    path.ForceGlobalResearch();
                airborneTimer = 0f;
            }
            else
            {
                airborneTimer += dt;
            }

            sensing = Scan(lookAhead);
        }

        // ------------------------------------------------------------------ transições de estado
        private void DecideState()
        {
            // Acabou de sair de um knockback (arremesso): realinha por um instante.
            if (state == BotState.Knockback)
            {
                SetState(BotState.Recovering);
                return;
            }

            // Estados temporizados têm prioridade.
            if (reverseTimer > 0f)
            {
                SetState(BotState.Reversing);
                return;
            }

            bool launching = Time.time - controlEnabledTime < launchSeconds;
            if (launching)
            {
                SetState(BotState.Launching);
                return;
            }

            // Só é JUMPING (voo de verdade) se está sem chão E com velocidade real. Um kart "sem
            // chão" porém PARADO não está saltando — está wedgeado numa parede/quina com a
            // suspensão fora do chão. Tratar isso como Jumping cortava o gás e o bot ficava preso
            // (v=0 "no ar") até a rede de 60s. Aqui, airborne+lento cai no fluxo normal → o un-jam age.
            if (airborneTimer > airborneGraceSeconds && kart.SpeedKmh > minAirborneSpeedKmh)
            {
                SetState(BotState.Jumping);
                return;
            }

            if (state == BotState.Recovering && Time.time - stateEnterTime < recoveringSeconds)
                return;

            float distToPath = frame.IsValid ? frame.DistanceToPath : 0f;
            BotTrackZone zone = frame.ActiveZone;
            BotTrackZone upcomingZone = frame.UpcomingZone;
            bool activeObstacleZone = zone != null && zone.Type == BotTrackZoneType.Obstaculo;
            bool activeRampOrJump = IsRampOrJumpZone(zone);
            bool upcomingRampOrJump = IsRampOrJumpZone(upcomingZone);
            bool commitRampLine = activeRampOrJump
                || (upcomingRampOrJump && (!activeObstacleZone || frame.DistanceToUpcomingZone < 5.5f));

            // Fora do traçado (mas na pista) → reencaixar. (Off-track real vira respawn nos detectores.)
            if (distToPath > rejoinDistance && distToPath < offTrackDistance)
            {
                SetState(BotState.RejoiningRoute);
                return;
            }

            // Obstáculo/parede/borda detectado pelos sensores → desviar.
            if (!commitRampLine && (sensing.centerBlocked || sensing.edgeAhead))
            {
                SetState(BotState.AvoidingObstacle);
                return;
            }

            // Rampa/salto à frente → preparar (alinhar + embalo).
            if (activeRampOrJump || upcomingRampOrJump)
            {
                SetState(BotState.ApproachingRamp);
                return;
            }

            if (kart.IsDrifting)
            {
                SetState(BotState.Drifting);
                return;
            }

            if (Time.time < driftExitStateUntil)
            {
                SetState(BotState.ExitingDrift);
                return;
            }

            if (ShouldPrepareDrift(zone))
            {
                SetState(BotState.PreparingDrift);
                return;
            }

            if (trafficAhead && trafficDistance < kartProbeDistance)
            {
                SetState(BotState.AvoidingTraffic);
                return;
            }

            // Curva forte à frente.
            if (sensing.cornerAngle >= cornerEnterAngle)
            {
                SetState(BotState.ApproachingCorner);
                return;
            }

            SetState(BotState.Racing);
        }

        // ------------------------------------------------------------------ produção de input por estado
        private KartInputState ComputeInput(float dt)
        {
            switch (state)
            {
                case BotState.Respawning: return DoRespawnInput();
                case BotState.Reversing: return DoReverse();
                case BotState.Launching: return DoLaunch();
                case BotState.Jumping: return DoJump();
                default: return DoDrive(dt); // Racing/ApproachingCorner/ApproachingRamp/AvoidingObstacle/RejoiningRoute/Recovering
            }
        }

        private KartInputState DoLaunch()
        {
            // Full gás para frente; uma pitada de correção de direção para a largada não sair torta.
            float steer = 0f;
            if (frame.IsValid)
            {
                Vector3 toAim = Planar(frame.AimPoint - kart.transform.position);
                debugAim = frame.AimPoint;
                if (toAim.sqrMagnitude > 0.01f)
                    steer = Mathf.Clamp(kart.transform.InverseTransformDirection(toAim.normalized).x * profile.steerSharpness, -0.6f, 0.6f);
            }
            targetSpeedKmh = maxStraightSpeedKmh;
            slowReason = "largada";
            handbrakeHeldLastRead = false;
            return new KartInputState { Throttle = 1f, Brake = 0f, Steer = steer };
        }

        private KartInputState DoJump()
        {
            // No ar: SEM gás (gás = pitch = capota no pouso). Só alinha o nariz com a tangente.
            handbrakeHeldLastRead = false;
            float airSteer = 0f;
            if (frame.IsValid)
            {
                float headErr = Vector3.SignedAngle(Planar(kart.transform.forward), frame.Tangent, Vector3.up);
                airSteer = Mathf.Clamp(headErr / 45f, -1f, 1f) * airSteerGain;
                debugAim = frame.AimPoint;
            }
            targetSpeedKmh = 0f;
            slowReason = "no ar";
            return new KartInputState { Throttle = 0f, Brake = 0f, Steer = airSteer };
        }

        private KartInputState DoReverse()
        {
            handbrakeHeldLastRead = false;
            targetSpeedKmh = 0f;
            slowReason = "ré (recuperação)";
            return new KartInputState { Throttle = 0f, Brake = 1f, Steer = reverseSteer };
        }

        private KartInputState DoRespawnInput()
        {
            // O respawn em si já foi executado em DoRespawnNow(); aqui devolve neutro 1 frame.
            handbrakeHeldLastRead = false;
            return KartInputState.Neutral;
        }

        // Núcleo de condução: traçado + velocidade-alvo + desvio. Cobre Racing, curvas, rampas,
        // desvio de obstáculo, reencaixe e recuperação — todos compartilham a mesma base, com
        // modificadores por estado (mira, embalo, alinhamento, supressão de wander).
        private KartInputState DoDrive(float dt)
        {
            Vector3 pos = kart.transform.position;
            float speedKmh = kart.SpeedKmh;
            BotTrackZone zone = frame.ActiveZone;
            float zoneBlend = 1f;
            BotTrackZone approachZone = frame.UpcomingZone;
            float approachBlend = 0f;
            if (approachZone != null && approachZone.ApproachDistance > 0.5f)
                approachBlend = 1f - Mathf.Clamp01(frame.DistanceToUpcomingZone / approachZone.ApproachDistance);

            bool activeObstacleZone = zone != null && zone.Type == BotTrackZoneType.Obstaculo;
            bool activeRampOrJump = IsRampOrJumpZone(zone);
            bool upcomingRampOrJump = IsRampOrJumpZone(approachZone);
            bool commitRampLine = activeRampOrJump
                || (upcomingRampOrJump && (!activeObstacleZone || frame.DistanceToUpcomingZone < 5.5f));
            bool rampState = state == BotState.ApproachingRamp && commitRampLine;
            BotTrackZone rampAimZone = activeRampOrJump ? zone : (commitRampLine && upcomingRampOrJump ? approachZone : null);
            BotTrackZone rampSpeedZone = activeRampOrJump ? zone : (upcomingRampOrJump ? approachZone : null);

            // ---------- mira / traçado ----------
            float distToPath = frame.DistanceToPath;
            float offPath01 = Mathf.InverseLerp(rejoinDistance, offTrackDistance, distToPath);

            Vector3 aim = frame.AimPoint;
            Vector3 routeRight = Vector3.Cross(Vector3.up, frame.Tangent).normalized;

            // Linha de corrida própria: desloca a mira lateralmente; encolhe em curva, na zona com
            // alinhamento e em reencaixe. Em rampa/salto o alinhamento é máximo (entra reto).
            float align = 0f;
            bool applyApproachAlignment = approachZone != null
                && (!activeObstacleZone || commitRampLine || approachZone.Type == BotTrackZoneType.Obstaculo);
            if (zone != null && zone.AlignToRoute) align = Mathf.Max(align, zoneBlend * zone.AlignmentStrength);
            if (applyApproachAlignment && approachZone.AlignToRoute) align = Mathf.Max(align, approachBlend * approachZone.AlignmentStrength);
            if (rampState) align = Mathf.Max(align, 0.9f);

            float zoneOffsetCap = maxRacingLineOffset;
            if (zone != null)
                zoneOffsetCap = Mathf.Min(zoneOffsetCap, Mathf.Max(0.05f, zone.MaxLateralOffset));
            if (applyApproachAlignment && approachBlend > 0f)
                zoneOffsetCap = Mathf.Lerp(zoneOffsetCap, Mathf.Min(zoneOffsetCap, Mathf.Max(0.05f, approachZone.MaxLateralOffset)), approachBlend);

            float allowedOffset = Mathf.Lerp(maxRacingLineOffset, zoneOffsetCap, align);

            // Offset lateral do bot em relação ao CENTRO da rota (coordenada de pista) — base do
            // raciocínio de faixas do BotTrafficController.
            currentCenterOffset = Vector3.Dot(Planar(pos - frame.NearestPoint), routeRight);

            // FAIXAS: cada bot recebe a faixa livre mais próxima da sua linha natural (anti-bolo).
            // Em trecho largo cabem várias faixas (pelotão se abre); em gargalo cabe 1 e o controle
            // sinaliza ceder (fila). A faixa NUNCA empurra o bot para uma mureta próxima (clamp).
            float naturalBias = Mathf.Clamp(racingLineOffset, -allowedOffset, allowedOffset);
            float laneOffset = naturalBias;
            laneYield = false; laneGapAhead = float.MaxValue; laneLeaderSpeedKmh = 0f;

            // FAIXAS SÓ EM TRECHOS CRÍTICOS (campo de pegs, curva forte, área estreita, zona de
            // risco). Em trecho largo os bots mantêm o offset estático por seed + a racing line
            // (comportamento comprovadamente rápido). Validação mostrou que aplicar faixas em TODA
            // a pista piora (troca de faixa demais em curva → mais contato com mureta).
            bool criticalCorridor = IsCorridorZone(zone)
                || (approachZone != null && approachBlend > 0.3f && IsCorridorZone(approachZone));
            bool laneActive = useLaneSystem && traffic != null && !rampState && criticalCorridor;
            if (laneActive)
            {
                float laneProbe = sensorRange + Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxStraightSpeedKmh)) * sensorRangeSpeedScale;
                Vector3 laneProbeOrigin = pos + Vector3.up * sensorOriginHeight;
                laneOffset = traffic.QueryLane(this, pos, laneProbeOrigin, kart.transform.forward, routeRight,
                    currentCenterOffset, allowedOffset, naturalBias, laneTargetOffset,
                    laneProbe, sensorMask,
                    out laneYield, out laneGapAhead, out laneLeaderSpeedKmh);

                if (laneOffset > currentCenterOffset && sensing.rightClear < 4f) laneOffset = currentCenterOffset;
                if (laneOffset < currentCenterOffset && sensing.leftClear < 4f) laneOffset = currentCenterOffset;
            }

            laneOffset = Mathf.Clamp(laneOffset, -allowedOffset, allowedOffset);
            if (Mathf.Sign(laneOffset) != Mathf.Sign(laneTargetOffset)
                && Mathf.Abs(laneOffset) > 0.5f && Mathf.Abs(laneTargetOffset) > 0.5f)
                laneChangeCount++;
            laneTargetOffset = laneOffset;

            // Desliza o alvo lateral suavemente até a faixa (troca de faixa sem esterço nervoso).
            laneSmoothedOffset = Mathf.MoveTowards(laneSmoothedOffset, laneOffset, dt * laneShiftSpeed);
            float effOffset = laneSmoothedOffset;
            if (Mathf.Abs(effOffset) > 0.01f)
            {
                float scale = (1f - sensing.cornerSeverity * 0.30f) * (1f - offPath01);
                aim += routeRight * (effOffset * scale);
            }

            // Slalom sinusoidal cego só quando o sistema de faixas NÃO está ativo: com faixas, a
            // escolha de faixa dirigível já sonda e contorna os pegs/obstáculos (sem dois bots no
            // mesmo buraco), o que é melhor que o weave fixo por seed.
            bool obstacleZone = (zone != null && zone.Type == BotTrackZoneType.Obstaculo)
                || (approachZone != null && approachBlend > 0.35f && approachZone.Type == BotTrackZoneType.Obstaculo);
            if (obstacleZone && !rampState && !laneActive)
            {
                float obstacleCap = zone != null && zone.Type == BotTrackZoneType.Obstaculo
                    ? Mathf.Max(0.5f, zone.MaxLateralOffset)
                    : Mathf.Max(0.5f, approachZone.MaxLateralOffset);
                float wave = Mathf.Sin((path.CurrentDistanceOnPath + seed * 9.17f) * obstacleSlalomFrequency);
                float side = sensing.centerBlocked && sensing.freeSide != 0
                    ? sensing.freeSide
                    : (Mathf.Abs(wave) > 0.05f ? Mathf.Sign(wave) : (seed % 2 == 0 ? 1f : -1f));
                float amount = sensing.centerBlocked
                    ? obstacleSlalomOffset
                    : obstacleSlalomOffset * (0.35f + Mathf.Abs(wave) * 0.65f);
                aim += routeRight * (side * Mathf.Min(obstacleCap, amount));
            }

            if (state == BotState.RejoiningRoute || offPath01 > 0f)
            {
                Vector3 rejoinAim = frame.NearestPoint + frame.Tangent * Mathf.Max(5f, 0.5f * (baseLookAheadMeters));
                aim = Vector3.Lerp(aim, rejoinAim, Mathf.Max(offPath01 * 0.5f, state == BotState.RejoiningRoute ? 0.6f : 0f));
            }
            debugAim = aim;

            Vector3 toAim = Planar(aim - pos);
            if (toAim.sqrMagnitude < 0.01f)
            {
                targetSpeedKmh = minCornerSpeedKmh;
                return ApplyArcadeClamps(new KartInputState { Throttle = profile.throttleScale, Steer = 0f }, speedKmh);
            }

            Vector3 localAim = kart.transform.InverseTransformDirection(toAim.normalized);
            Vector3 localNearest = kart.transform.InverseTransformPoint(frame.NearestPoint);

            float wander = (Mathf.PerlinNoise(wanderPhase + Time.time * profile.wanderFrequency, seed * 0.31f) - 0.5f) * 2f;
            wander *= (1f - align);
            float returnSteer = Mathf.Clamp(localNearest.x * 0.08f, -0.6f, 0.6f) * Mathf.Max(offPath01, state == BotState.RejoiningRoute ? 0.6f : 0f);
            float steer = localAim.x * profile.steerSharpness + returnSteer + wander * profile.steerWander;

            // Alinhamento de rampa/zona: alinhar o nariz à tangente e puxar ao centro (entra reto).
            if (align > 0.01f)
            {
                float headErr = Vector3.SignedAngle(Planar(kart.transform.forward), frame.Tangent, Vector3.up);
                float headSteer = Mathf.Clamp(headErr / 38f, -1f, 1f);
                float center = Mathf.Clamp(localNearest.x * 0.12f, -0.6f, 0.6f);
                steer = Mathf.Lerp(steer, Mathf.Clamp(headSteer * 0.6f + center, -1f, 1f), align * 0.85f);
            }

            // ---------- desvio (sensores) ----------
            bool suppressKartAvoid = (zone != null && zone.SuppressKartAvoidance)
                || (approachZone != null && approachBlend > 0.55f && approachZone.SuppressKartAvoidance);
            // Quando a ZONA pede suppress explicitamente (ex.: campo de pegs), o desvio lateral de
            // kart-a-kart joga o bot em cima do poste. Nesses trechos a precisão vem da DISTRIBUIÇÃO
            // de faixas (lane system espalha os bots por faixas livres) + FILA por velocidade (yield),
            // não de esterço lateral. Por isso respeitamos o suppress MESMO em obstáculo e em baixa
            // velocidade. Fora desses casos, mantém o desvio (não suprime em obstáculo/baixa vel.).
            bool zoneWantsSuppress = suppressKartAvoid;
            if (activeObstacleZone && !rampState && !zoneWantsSuppress)
                suppressKartAvoid = false;
            if (speedKmh < 55f && !zoneWantsSuppress)
                suppressKartAvoid = false;
            float avoidSteer = sensing.avoidSteer * avoidSteerStrength;
            steer += avoidSteer;

            float kartBrake = 0f, kartThrottleScale = 1f;
            if (suppressKartAvoid)
            {
                trafficAhead = false;
                trafficSlowAhead = false;
                trafficDistance = float.MaxValue;
                trafficSpeedKmh = 0f;
                trafficAvoidSide = 0;
            }
            float kartSteer = suppressKartAvoid ? 0f : ComputeKartAvoidance(steer, speedKmh, out kartBrake, out kartThrottleScale);
            steer = Mathf.Clamp(steer + kartSteer, -1f, 1f);

            DriftDecision drift = EvaluateDriftIntent(zone, approachZone, approachBlend, rampState, steer, speedKmh);
            if (drift.ShouldUseDrift)
                steer = ShapeSteerForDrift(steer, drift.Direction);

            // ---------- velocidade-alvo ----------
            targetSpeedKmh = ComputeTargetSpeed(zone, approachZone, approachBlend, rampSpeedZone, drift, out slowReason);

            // ---------- pedais ----------
            float throttle, brake;
            ThrottleBrakeForTarget(speedKmh, targetSpeedKmh, out throttle, out brake);

            // Rampa: garante embalo (não freia abaixo do mínimo da rampa).
            if (rampSpeedZone != null)
            {
                float rampMin = rampSpeedZone.MinSpeedKmh > 1f ? rampSpeedZone.MinSpeedKmh : defaultRampMinSpeedKmh;
                if (speedKmh < rampMin) { throttle = 1f; brake = 0f; }
            }

            // Desvio: só freia se NÃO houver lado livre (escapatória). Senão mantém ritmo e contorna.
            bool ignoreRampLipBrake = rampAimZone != null && !sensing.edgeAhead && sensing.frontDist > 0.9f;
            if (sensing.mustBrake && !ignoreRampLipBrake)
            {
                if (!sensing.edgeAhead && speedKmh < 38f)
                {
                    throttle = 1f;
                    brake = 0f;
                    if (sensing.freeSide != 0)
                        steer = Mathf.Clamp(steer + sensing.freeSide * 0.55f, -1f, 1f);
                    slowReason = "desvio agressivo em baixa";
                }
                else
                {
                    brake = Mathf.Max(brake, sensing.brakeAmount);
                    throttle = Mathf.Min(throttle, 1f - sensing.brakeAmount * 0.8f);
                    slowReason = sensing.edgeAhead ? "borda à frente" : "parede sem escape";
                }
            }
            else if (ignoreRampLipBrake)
            {
                throttle = 1f;
                brake = 0f;
                slowReason = rampAimZone.Type == BotTrackZoneType.Salto ? "salto: ignora quina transponivel" : "rampa: ignora quina transponivel";
            }
            else if ((sensing.centerBlocked || Mathf.Abs(avoidSteer) > 0.05f) && state == BotState.AvoidingObstacle)
            {
                // Há espaço lateral: desvia em velocidade (alívio leve, não freada).
                throttle = Mathf.Min(throttle, 0.95f);
            }

            // Alívio por kart à frente.
            throttle *= kartThrottleScale;
            brake = Mathf.Max(brake, kartBrake);
            if (trafficAhead && slowReason == "reta")
                slowReason = trafficSlowAhead ? "tráfego à frente" : "ultrapassagem";

            // FILA POR FAIXA: há um mais lento logo à frente na minha faixa e não há faixa melhor
            // → mantém distância aliviando o gás (sem travar nem empurrar). Resolve o bolo formando
            // fila funcional EM MOVIMENTO. O alívio nunca zera o gás (anti-deadlock).
            if (useLaneSystem && laneYield && laneGapAhead < 7f && speedKmh > 28f && !rampState)
            {
                float relief = Mathf.Lerp(0.92f, 0.55f, Mathf.InverseLerp(7f, 2.5f, laneGapAhead));
                throttle *= relief;
                slowReason = "fila (faixa ocupada)";
            }

            if (drift.ShouldUseDrift)
            {
                // Drift é usado para preservar velocidade. Evita que o planner mande freio junto com
                // handbrake, que viraria burnout em baixa e destruiria a entrada de curva.
                throttle = Mathf.Max(throttle, 0.88f);
                brake = 0f;
            }

            // Erro humano ocasional.
            if (profile.mistakeChance > 0f && Random.value < profile.mistakeChance * dt)
                throttle *= 0.5f;

            var input = new KartInputState { Throttle = throttle, Brake = brake, Steer = steer };
            ApplyDriftInput(ref input, drift, speedKmh);
            return ApplyArcadeClamps(input, speedKmh);
        }

        // ------------------------------------------------------------------ modelo de velocidade
        private float ComputeTargetSpeed(BotTrackZone zone, BotTrackZone approachZone, float approachBlend, BotTrackZone rampZone, DriftDecision drift, out string reason)
        {
            reason = "reta segura";

            // Velocidade de curva pela CURVATURA real à frente (rápido em curva suave, baixo só em
            // curva fechada). minCornerSpeedKmh é ALTO de propósito (bots competitivos).
            float cautionScale = Mathf.Lerp(0.72f, 1.08f, profile.corneringCaution);
            float speedSeverity = Mathf.Clamp01(sensing.cornerSeverity * cautionScale);
            float curveFloor = drift.Beneficial || kart.IsDrifting
                ? Mathf.Lerp(driftTargetSpeedKmh, driftHardCornerTargetSpeedKmh, speedSeverity)
                : minCornerSpeedKmh;
            float curvePower = drift.Beneficial || kart.IsDrifting ? 1.45f : 1.2f;
            float cornerV = Mathf.Lerp(maxStraightSpeedKmh, curveFloor, Mathf.Pow(speedSeverity, curvePower));
            float target = cornerV;
            if (sensing.cornerSeverity > 0.25f)
                reason = drift.Beneficial || kart.IsDrifting ? "curva com drift" : "curva sem drift";

            // Teto explícito do designer (curva forte / área estreita / zona de risco).
            BotTrackZone capZone = zone != null ? zone : (approachBlend > 0.4f ? approachZone : null);
            if (capZone != null && capZone.MaxSpeedKmh > 1f &&
                (capZone.Type == BotTrackZoneType.CurvaForte || capZone.Type == BotTrackZoneType.AreaEstreita || capZone.Type == BotTrackZoneType.ZonaDeRisco))
            {
                float cap = capZone.MaxSpeedKmh;
                if (capZone.Type == BotTrackZoneType.CurvaForte && (drift.Beneficial || kart.IsDrifting))
                    cap = Mathf.Max(cap, driftHardCornerTargetSpeedKmh);
                else if (capZone.Type == BotTrackZoneType.AreaEstreita)
                    cap = Mathf.Max(cap, driftNarrowSpeedFloorKmh);

                if (cap < target) { target = cap; reason = "zona " + capZone.Type; }
            }

            // Velocidade recomendada (alvo suave).
            if (zone != null && zone.RecommendedSpeedKmh > 1f)
            {
                target = Mathf.Lerp(target, zone.RecommendedSpeedKmh, 0.6f);
                reason = "recomendada";
            }

            // Rampa/salto: GARANTE embalo (acelera para o mínimo). Tem prioridade sobre teto de curva.
            if (rampZone != null)
            {
                float rampMin = rampZone.MinSpeedKmh > 1f ? rampZone.MinSpeedKmh : defaultRampMinSpeedKmh;
                if (rampMin > target) { target = rampMin; reason = rampZone.Type == BotTrackZoneType.Salto ? "velocidade mínima de salto" : "aproximação de rampa"; }
            }
            else if (zone != null && zone.MinSpeedKmh > 1f && zone.MinSpeedKmh > target)
            {
                target = zone.MinSpeedKmh; reason = "min zona";
            }

            // Postura do trecho + perfil do bot.
            if (zone != null) target *= zone.AggressivenessSpeedScale;
            target *= profile.throttleScale;

            // Reencaixe usa um alvo moderado.
            if (state == BotState.RejoiningRoute) { target = Mathf.Min(target, rejoinSpeedKmh); reason = "reencaixe"; }

            return Mathf.Clamp(target, hardMinSpeedKmh, maxStraightSpeedKmh);
        }

        private void ThrottleBrakeForTarget(float speed, float target, out float throttle, out float brake)
        {
            float err = target - speed;
            if (err > 2f) { throttle = 1f; brake = 0f; }
            else if (err > -10f) { throttle = 0.62f; brake = 0f; }
            else if (err > -22f) { throttle = 0.2f; brake = 0f; }
            else { brake = Mathf.Clamp01((-err - 22f) / 36f + 0.08f); throttle = 0f; }
        }

        private KartInputState ApplyArcadeClamps(KartInputState s, float speedKmh)
        {
            // Nunca freia em baixa velocidade (freio parado = ré no KartController).
            if (speedKmh < minBrakeSpeedKmh) s.Brake = 0f;
            // Acelerador + freio em baixa velocidade = burnout: o acelerador vence.
            if (s.Throttle > 0.1f && s.Brake > 0.1f && speedKmh < 25f) s.Brake = 0f;
            if (s.Throttle > 0.1f && s.Handbrake && speedKmh < driftMinSpeedKmh * 0.75f)
            {
                s.Handbrake = false;
                s.HandbrakePressed = false;
            }

            s.Throttle = Mathf.Clamp01(s.Throttle);
            s.Brake = Mathf.Clamp01(s.Brake);
            s.Steer = Mathf.Clamp(s.Steer, -1f, 1f);
            handbrakeHeldLastRead = s.Handbrake;
            return s;
        }

        // ================================================================== DRIFT DRIVER
        private struct DriftDecision
        {
            public bool Beneficial;
            public bool ShouldUseDrift;
            public bool ShouldStart;
            public bool ShouldHoldInput;
            public int Direction;
            public string Reason;
            public string BlockReason;
        }

        private bool ShouldPrepareDrift(BotTrackZone zone)
        {
            if (!useDrift || kart == null || kart.IsDrifting)
                return false;

            if (Time.time < driftCooldownUntil)
                return false;

            bool zoneRecommends = zone != null && zone.AllowDrift;
            if (!zoneRecommends && sensing.cornerAngle < driftPrepareAngle)
                return false;

            if (sensing.edgeAhead || sensing.mustBrake)
                return false;

            return kart.SpeedKmh >= driftMinSpeedKmh * 0.85f;
        }

        private DriftDecision EvaluateDriftIntent(
            BotTrackZone zone,
            BotTrackZone approachZone,
            float approachBlend,
            bool rampState,
            float steer,
            float speedKmh)
        {
            var decision = new DriftDecision
            {
                Direction = requestedDriftDirection,
                Reason = "-",
                BlockReason = "-"
            };

            bool activeZoneAllows = zone != null && zone.AllowDrift;
            bool approachZoneAllows = approachZone != null && approachZone.AllowDrift && approachBlend > 0.35f;
            bool zoneForbids = ZoneForbidsDrift(zone) || (approachZone != null && approachBlend > 0.65f && ZoneForbidsDrift(approachZone));
            bool curveIsWorthDrift = sensing.cornerAngle >= driftEnterAngle || activeZoneAllows || approachZoneAllows;
            bool preparing = sensing.cornerAngle >= driftPrepareAngle || activeZoneAllows || approachZoneAllows;

            int direction = ResolveDriftDirection(steer);
            decision.Direction = direction;
            decision.Beneficial = useDrift && preparing && direction != 0 && !rampState && !zoneForbids;

            if (!useDrift)
            {
                decision.BlockReason = "drift desligado";
            }
            else if (rampState)
            {
                decision.BlockReason = "rampa/salto";
            }
            else if (zoneForbids)
            {
                decision.BlockReason = "zona estreita/risco";
            }
            else if (!kart.IsGrounded)
            {
                decision.BlockReason = "sem chão";
            }
            else if (speedKmh < driftMinSpeedKmh)
            {
                decision.BlockReason = "velocidade baixa";
            }
            else if (sensing.edgeAhead)
            {
                decision.BlockReason = "borda à frente";
            }
            else if (sensing.mustBrake || (sensing.centerBlocked && sensing.frontDist < driftObstacleBlockDistance))
            {
                decision.BlockReason = "obstáculo frontal";
            }
            else if (!curveIsWorthDrift)
            {
                decision.BlockReason = "curva leve";
            }
            else if (Time.time < driftCooldownUntil && !kart.IsDrifting)
            {
                decision.BlockReason = "cooldown drift";
            }

            bool blocked = decision.BlockReason != "-";
            if (kart.IsDrifting)
            {
                bool keepCurve = sensing.cornerAngle > driftExitAngle || activeZoneAllows || approachZoneAllows;
                bool sameDirection = direction == 0 || direction == requestedDriftDirection;
                decision.ShouldUseDrift = !blocked && keepCurve && sameDirection;
                decision.ShouldHoldInput = false;
                decision.Reason = decision.ShouldUseDrift ? "mantendo drift" : "saindo do drift";
                driftReason = decision.Reason;
                driftBlockReason = decision.BlockReason;
                return decision;
            }

            decision.ShouldStart = !blocked && curveIsWorthDrift;
            decision.ShouldUseDrift = decision.ShouldStart || Time.time < driftInputHoldUntil;
            decision.ShouldHoldInput = decision.ShouldUseDrift && Time.time < driftInputHoldUntil;
            decision.Reason = decision.ShouldStart
                ? (activeZoneAllows || approachZoneAllows ? "drift recomendado pela zona" : "curva com drift")
                : "-";

            if (decision.ShouldStart)
            {
                requestedDriftDirection = direction;
                decision.Direction = direction;
            }

            driftReason = decision.ShouldUseDrift ? decision.Reason : "-";
            driftBlockReason = decision.BlockReason;
            return decision;
        }

        private static bool ZoneForbidsDrift(BotTrackZone zone)
        {
            if (zone == null || zone.AllowDrift)
                return false;

            return zone.Type == BotTrackZoneType.Rampa
                || zone.Type == BotTrackZoneType.Salto
                || zone.Type == BotTrackZoneType.AreaEstreita
                || zone.Type == BotTrackZoneType.ZonaDeRisco;
        }

        private int ResolveDriftDirection(float steer)
        {
            if (Mathf.Abs(steer) > 0.08f)
                return steer >= 0f ? 1 : -1;

            if (frame.IsValid)
            {
                float routeTurn = Vector3.SignedAngle(Planar(kart.transform.forward), frame.Tangent, Vector3.up);
                if (Mathf.Abs(routeTurn) > 3f)
                    return routeTurn >= 0f ? 1 : -1;
            }

            return requestedDriftDirection;
        }

        private float ShapeSteerForDrift(float steer, int direction)
        {
            if (direction == 0)
                return steer;

            float min = driftSteerMinimum;
            float magnitude = Mathf.Max(Mathf.Abs(steer), min);
            if (kart.IsDrifting)
            {
                // Durante o drift, deixa o alvo de rota corrigir, mas nunca cai abaixo do steer
                // mínimo que mantém o KartController em estado de drift.
                magnitude = Mathf.Max(magnitude, driftSteerMinimum * 0.72f);
            }

            return Mathf.Clamp(direction * magnitude, -1f, 1f);
        }

        private void ApplyDriftInput(ref KartInputState input, DriftDecision decision, float speedKmh)
        {
            bool wantsInput = false;
            if (decision.ShouldStart)
            {
                driftInputHoldUntil = Time.time + driftEntryHandbrakeHold;
                wantsInput = true;
            }
            else if (decision.ShouldHoldInput)
            {
                wantsInput = true;
            }

            if (speedKmh < driftMinSpeedKmh * 0.75f || sensing.edgeAhead || sensing.mustBrake)
                wantsInput = false;

            input.Handbrake = wantsInput;
            input.HandbrakePressed = wantsInput && !handbrakeHeldLastRead;

            if (decision.ShouldUseDrift)
            {
                if (kart.IsDrifting)
                    SetState(BotState.Drifting);
                else
                    SetState(BotState.PreparingDrift);
            }
            else if (kart.IsDrifting)
            {
                SetState(BotState.ExitingDrift);
            }
        }

        // ================================================================== SENSORES
        private struct Sensing
        {
            public float frontDist;       // parede/obstáculo central mais próximo (range se livre)
            public float leftClear;
            public float rightClear;
            public int freeSide;          // +1 direita, -1 esquerda, 0 indefinido
            public bool centerBlocked;
            public bool edgeAhead;
            public bool mustBrake;        // bloqueado sem escapatória
            public float brakeAmount;
            public float avoidSteer;      // esterço sugerido para o lado livre
            public float cornerAngle;     // ângulo de curva à frente (graus)
            public float cornerSeverity;  // 0..1
        }

        private Sensing Scan(float lookAhead)
        {
            var s = new Sensing
            {
                frontDist = float.MaxValue,
                leftClear = float.MaxValue,
                rightClear = float.MaxValue,
                freeSide = 0
            };

            // Curvatura à frente (amostra o traçado atual).
            ComputeCurvature(lookAhead, out s.cornerAngle, out s.cornerSeverity);

            if (kart == null) return s;

            Vector3 fwd = Planar(kart.transform.forward);
            if (fwd.sqrMagnitude < 0.001f) return s;
            fwd.Normalize();
            Vector3 right = Planar(kart.transform.right).normalized;
            Vector3 origin = kart.transform.position + Vector3.up * sensorOriginHeight;

            float range = sensorRange + Mathf.Clamp01(kart.SpeedKmh / Mathf.Max(1f, maxStraightSpeedKmh)) * sensorRangeSpeedScale;
            bool inObstacleZone = frame.ActiveZone != null && frame.ActiveZone.Type == BotTrackZoneType.Obstaculo;
            bool rampContext = IsRampContext();
            float radius = inObstacleZone ? sensorRadius * 1.4f : (rampContext ? sensorRadius * 0.82f : sensorRadius);

            // CONE CENTRAL (|ang| <= centerCone) decide BLOQUEIO/FREIO — o que está realmente à
            // frente. As "asas" (rays largos) só informam clearance lateral e o esterço de desvio.
            // Sem essa separação, a mureta/borda da pista (face vertical do próprio asfalto) era
            // lida como "parede sem escape" numa reta larga e o bot freava à toa, empilhando.
            const float centerCone = 20f;
            float centerSeverity = 0f, centerDist = range;
            float leftNear = range, rightNear = range;       // parede mais próxima por lado (qualquer asa)
            float leftCloseSev = 0f, rightCloseSev = 0f;      // severidade só de asas PRÓXIMAS
            float wingCloseDist = range * 0.55f;

            if (sensorFanAngles != null)
            {
                for (int i = 0; i < sensorFanAngles.Length; i++)
                {
                    float ang = sensorFanAngles[i];
                    Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                    float dist = range;
                    bool hitWall = false;

                    if (Physics.SphereCast(origin, radius, dir, out RaycastHit hit, range, sensorMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null && !IsOwnCollider(hit.collider) &&
                            hit.collider.GetComponentInParent<KartController>() == null && // karts tratados à parte
                            hit.normal.y <= wallMaxNormalY &&                              // chão/rampa não é parede
                            !IsDrivableStep(hit.point, dir))                               // degrau/rampa/meio-fio: passa por cima
                        {
                            dist = hit.distance;
                            hitWall = true;
                        }
                    }

                    float sev = hitWall ? 1f - Mathf.Clamp01(dist / range) : 0f;

                    if (Mathf.Abs(ang) <= centerCone)
                    {
                        if (sev > centerSeverity) centerSeverity = sev;
                        if (hitWall) centerDist = Mathf.Min(centerDist, dist);
                    }

                    if (ang > 0.5f)
                    {
                        rightNear = Mathf.Min(rightNear, dist);
                        if (hitWall && dist < wingCloseDist) rightCloseSev = Mathf.Max(rightCloseSev, sev);
                    }
                    else if (ang < -0.5f)
                    {
                        leftNear = Mathf.Min(leftNear, dist);
                        if (hitWall && dist < wingCloseDist) leftCloseSev = Mathf.Max(leftCloseSev, sev);
                    }
                }
            }

            s.frontDist = centerDist;
            s.leftClear = leftNear;
            s.rightClear = rightNear;
            s.freeSide = rightNear >= leftNear ? 1 : -1;
            // Sensor de borda: o chão some à frente? (buraco/queda)
            Vector3 edgeProbe = kart.transform.position + fwd * edgeProbeAhead + Vector3.up * 1f;
            if (!Physics.Raycast(edgeProbe, Vector3.down, out RaycastHit gh, edgeDropDepth + 2f, sensorMask, QueryTriggerInteraction.Ignore))
                s.edgeAhead = true;
            else if (gh.point.y < kart.transform.position.y - edgeDropDepth)
                s.edgeAhead = true;

            // BLOQUEADO = algo à frente no cone central (não as muretas laterais distantes).
            if (rampContext && !s.edgeAhead && centerDist > 1.2f)
                centerSeverity *= 0.35f;

            s.centerBlocked = centerSeverity > (rampContext ? 0.42f : 0.18f) || s.edgeAhead;

            // Esterço de desvio:
            //  - borda à frente → vira para o lado com chão;
            //  - obstáculo central → contorna para o lado livre;
            //  - parede lateral PRÓXIMA (encostando na mureta) → corrige de leve para longe dela.
            if (s.edgeAhead)
            {
                s.avoidSteer = s.freeSide * 0.8f;
            }
            else if (s.centerBlocked)
            {
                s.avoidSteer = s.freeSide * Mathf.Max(centerSeverity, 0.5f);
            }
            else if (leftCloseSev > 0.25f || rightCloseSev > 0.25f)
            {
                // afasta do lado mais próximo (corrige rumo à mureta sem frear).
                float bias = Mathf.Max(leftCloseSev, rightCloseSev) * 0.6f;
                s.avoidSteer = (leftCloseSev > rightCloseSev ? 1f : -1f) * bias;
            }

            // Freia SÓ se: obstáculo de frente perto SEM escapatória lateral, ou borda imediata.
            float escape = Mathf.Max(leftNear, rightNear);
            bool noEscape = escape < range * 0.4f;
            if ((centerSeverity > (rampContext ? 0.78f : 0.5f) && centerDist < avoidBrakeDistance + kart.SpeedKmh * 0.06f && noEscape) ||
                (s.edgeAhead && centerDist < avoidBrakeDistance))
            {
                s.mustBrake = true;
                s.brakeAmount = Mathf.Clamp01(centerSeverity);
            }

            return s;
        }

        [Header("Classificação parede × degrau")]
        [Tooltip("Altura (m) até onde uma 'parede' detectada é, na verdade, um DEGRAU/meio-fio/rampa " +
                 "que o kart sobe — e portanto NÃO trava o bot. Acima disso é parede de verdade. " +
                 "Resolve as costuras entre peças de pista (faces verticais baixas) que faziam o bot " +
                 "frear/empilhar numa reta larga.")]
        [SerializeField] private float climbableStepHeight = 0.6f;
        [SerializeField] private float rampClimbableStepHeight = 4.5f;

        // Uma face vertical detectada pelo sensor é PAREDE ou um degrau/meio-fio/rampa que o kart
        // sobe? Sonda o chão logo ADIANTE do ponto de impacto: se há chão em ~nível do kart, é
        // transponível (degrau/rampa) e não deve travar; se não há chão (ou está muito acima), é parede.
        private bool IsDrivableStep(Vector3 hitPoint, Vector3 dir)
        {
            bool rampContext = IsRampContext();
            Vector3 probe = hitPoint + dir.normalized * 0.9f + Vector3.up * 1.3f;
            float probeDistance = rampContext ? 7f : 2.6f;
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit g, probeDistance, sensorMask, QueryTriggerInteraction.Ignore))
            {
                float allowedStep = rampContext
                    ? Mathf.Max(climbableStepHeight, rampClimbableStepHeight)
                    : climbableStepHeight;
                return g.point.y <= kart.transform.position.y + allowedStep;
            }
            return false; // sem chão adiante = parede/borda de verdade
        }

        private static bool IsRampOrJumpZone(BotTrackZone zone)
        {
            return zone != null && (zone.Type == BotTrackZoneType.Rampa || zone.Type == BotTrackZoneType.Salto);
        }

        // Trecho CRÍTICO onde o sistema de faixas dirigíveis substitui o offset estático: campo de
        // obstáculos/pegs, curva forte, área estreita e zona de risco. (Em trechos largos os bots
        // seguem a racing line normal com offset por seed.)
        private static bool IsCorridorZone(BotTrackZone zone)
        {
            return zone != null && (
                zone.Type == BotTrackZoneType.Obstaculo ||
                zone.Type == BotTrackZoneType.AreaEstreita ||
                zone.Type == BotTrackZoneType.CurvaForte ||
                zone.Type == BotTrackZoneType.ZonaDeRisco);
        }

        private bool IsRampContext()
        {
            if (IsRampOrJumpZone(frame.ActiveZone))
                return true;

            if (frame.ActiveZone != null && frame.ActiveZone.Type == BotTrackZoneType.Obstaculo)
                return false;

            BotTrackZone upcoming = frame.UpcomingZone;
            if (!IsRampOrJumpZone(upcoming))
                return false;

            float commitDistance = Mathf.Min(10f, Mathf.Max(3f, upcoming.ApproachDistance * 0.55f));
            return frame.DistanceToUpcomingZone <= commitDistance;
        }

        private void ComputeCurvature(float lookAhead, out float angle, out float severity)
        {
            angle = 0f; severity = 0f;
            if (!frame.IsValid) return;

            float la = Mathf.Max(6f, lookAhead);
            Vector3 n = frame.NearestPoint;
            Vector3 a = path.PointAhead(la * 0.5f);
            Vector3 b = path.PointAhead(la * 1.15f);
            Vector3 c = path.PointAhead(la * 2.1f);

            Vector3 d1 = Planar(a - n);
            Vector3 d2 = Planar(b - a);
            Vector3 d3 = Planar(c - b);

            float a1 = (d1.sqrMagnitude > 0.01f && d2.sqrMagnitude > 0.01f) ? Vector3.Angle(d1, d2) : 0f;
            float a2 = (d2.sqrMagnitude > 0.01f && d3.sqrMagnitude > 0.01f) ? Vector3.Angle(d2, d3) : 0f;
            angle = Mathf.Max(a1, a2);
            // Também considera o ângulo entre a frente do kart e a mira (curva imediata).
            angle = Mathf.Max(angle, frame.UpcomingTurn01 * cornerFullAngle);
            severity = Mathf.Clamp01(Mathf.InverseLerp(8f, cornerFullAngle, angle));
        }

        private float ComputeKartAvoidance(float preferredSteer, float speedKmh, out float brake, out float throttleScale)
        {
            brake = 0f; throttleScale = 1f;
            trafficAhead = false;
            trafficSlowAhead = false;
            trafficDistance = float.MaxValue;
            trafficSpeedKmh = 0f;
            trafficAvoidSide = 0;

            if (kart == null || kartProbeDistance <= 0f) return 0f;

            Vector3 fwd = Planar(kart.transform.forward);
            if (fwd.sqrMagnitude < 0.001f) return 0f;
            fwd.Normalize();
            Vector3 origin = kart.transform.position + Vector3.up * sensorOriginHeight;

            if (!Physics.SphereCast(origin, sensorRadius, fwd, out RaycastHit hit, kartProbeDistance, sensorMask, QueryTriggerInteraction.Ignore))
                return 0f;
            if (hit.collider == null || IsOwnCollider(hit.collider)) return 0f;
            KartController other = hit.collider.GetComponentInParent<KartController>();
            if (other == null || other == kart) return 0f;

            trafficAhead = true;
            trafficDistance = hit.distance;
            trafficSpeedKmh = other.SpeedKmh;

            float severity = 1f - Mathf.Clamp01(hit.distance / kartProbeDistance);
            Vector3 localOther = kart.transform.InverseTransformPoint(other.transform.position);
            float steerAway = sensing.freeSide != 0 ? sensing.freeSide : (localOther.x >= 0f ? -1f : 1f);
            if (Mathf.Abs(preferredSteer) > 0.35f) steerAway = Mathf.Sign(preferredSteer);
            trafficAvoidSide = steerAway >= 0f ? 1 : -1;

            throttleScale = 1f - kartThrottleRelief * severity;

            // ANTI-SCRUM (fila): se o kart à frente está perto E devagar, NÃO empurra — alivia forte
            // o acelerador e tenta contornar, mas não zera gás se existe lado livre. Zerar gás fazia
            // o pelotão morrer em gargalos; agora o bot mantém embalo de ultrapassagem.
            float otherSpeed = other.SpeedKmh;
            bool closeAndSlow = hit.distance < kartQueueDistance && otherSpeed < kartQueueSpeedKmh;
            trafficSlowAhead = closeAndSlow;
            if (closeAndSlow)
            {
                float queue = Mathf.Clamp01((kartQueueDistance - hit.distance) / Mathf.Max(0.5f, kartQueueDistance));
                bool hasEscape = Mathf.Max(sensing.leftClear, sensing.rightClear) > kartQueueDistance * 1.35f;
                float minThrottle = hasEscape ? 0.68f : 0.28f;
                throttleScale = Mathf.Min(throttleScale, Mathf.Lerp(0.82f, minThrottle, queue));
                // reforça o esterço para sair de trás dele (busca o lado livre).
                steerAway *= hasEscape ? 2.25f : 1.55f;
                if (!hasEscape && speedKmh > kartBrakeMinSpeedKmh)
                    brake = Mathf.Max(brake, queue * 0.25f);
            }
            else if (speedKmh > kartBrakeMinSpeedKmh && severity > 0.7f)
            {
                brake = (severity - 0.7f) * 0.22f;
            }

            return Mathf.Clamp(steerAway * severity * kartAvoidanceSteer, -1f, 1f);
        }

        // ================================================================== detectores (preso/queda/respawn)
        private void RunBackgroundDetectors(float dt)
        {
            bool grace = Time.time - controlEnabledTime < recoveryGraceSeconds;
            // "airborne" que PAUSA os detectores só vale para voo REAL (rápido). Sem chão e lento =
            // wedgeado: deixa os detectores (un-jam) agirem para soltar o bot.
            bool airborne = airborneTimer > airborneGraceSeconds && kart.SpeedKmh > minAirborneSpeedKmh;

            UpdatePathChangeGrace();

            // Capotado → respawn (não há recuperação por input).
            if (!airborne && kart.transform.up.y < flippedUpThreshold)
            {
                flippedTimer += dt;
                if (flippedTimer >= flippedSeconds) { Respawn("capotado"); return; }
            }
            else flippedTimer = 0f;

            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                if (reverseTimer <= 0f) { progressPathId = -2; progressTimer = 0f; blockedTimer = 0f; }
                return;
            }

            if (grace || airborne)
            {
                blockedTimer = 0f; hardStuckTimer = 0f; progressPathId = -2; progressTimer = 0f;
                if (!grace) UpdateCheckpointStall(true);
                return;
            }

            UpdateHardJam(dt);
            UpdateRouteProgress(dt);
            UpdateBlocked(dt);
            UpdateOffTrackAndBelow(dt);
            UpdateHardStuck(dt);
            UpdateCheckpointStall(false);
        }

        // UN-JAM RÁPIDO: o bot está PEDINDO gás mas praticamente parado (bolo de karts wedgeados,
        // encostado num obstáculo). Não espera os 4s da janela de progresso — dá ré em ~1.3s para
        // desfazer a trava. Karts não são "parede" para os sensores, então sem isto um bolo de
        // karts a full gás só seria resolvido tarde (watchdog) — era a causa do "v=0 throttle=1".
        private void UpdateHardJam(float dt)
        {
            bool trying = (lastInput.Throttle > 0.35f && lastInput.Brake < 0.5f) || trafficSlowAhead;
            if (trying && kart.SpeedKmh < hardJamSpeedKmh && !InAvoidRespawnZone())
            {
                hardJamTimer += dt;
                if (hardJamTimer >= hardJamSeconds) { Escalate("travado pedindo gás (jam)"); hardJamTimer = 0f; }
            }
            else hardJamTimer = Mathf.Max(0f, hardJamTimer - dt * 2f);
        }

        private void UpdatePathChangeGrace()
        {
            int id = path.CurrentPathId;
            if (id != lastPathId)
            {
                if (lastPathId >= 0 && id < 0) branchExitGraceUntil = Time.time + branchExitGraceSeconds;
                lastPathId = id;
                progressPathId = -2; // re-ancora progresso ao trocar de caminho
            }
        }

        // Progresso REAL em metros de rota: andar sem avançar (raspar parede, orbitar obstáculo,
        // empurra-empurra) dispara recuperação mesmo "se mexendo".
        private void UpdateRouteProgress(float dt)
        {
            int id = path.CurrentPathId;
            float distance = path.CurrentDistanceOnPath;
            if (id != progressPathId || progressPathId == -2)
            {
                progressPathId = id; progressAnchor = distance; progressTimer = 0f; return;
            }
            progressTimer += dt;
            if (progressTimer < progressWindowSeconds) return;

            float delta = path.SignedRouteDelta(progressAnchor, distance);
            lastRouteDelta = delta;
            bool trying = lastInput.Throttle > 0.25f || lastInput.Brake > 0.25f;
            if (delta < progressMinMeters && trying && !InAvoidRespawnZone())
                Escalate("sem progresso na rota");
            progressAnchor = distance; progressTimer = 0f;
        }

        // Bloqueio frontal: parede colada + acelerando + devagar = preso de cara. Karts NÃO contam
        // (são tratados pelo desvio dedicado), então isto só dispara em parede/obstáculo real.
        private void UpdateBlocked(float dt)
        {
            bool blocked = sensing.centerBlocked && !sensing.edgeAhead
                && sensing.frontDist < blockedDistance
                && kart.SpeedKmh < blockedSpeedKmh
                && lastInput.Throttle > 0.25f;

            if (blocked && !InAvoidRespawnZone())
            {
                blockedTimer += dt;
                if (blockedTimer >= blockedSeconds) { Escalate("bloqueado na parede"); blockedTimer = 0f; }
            }
            else blockedTimer = Mathf.Max(0f, blockedTimer - dt * 2f);
        }

        private void UpdateOffTrackAndBelow(float dt)
        {
            float dist = path.PlanarDistanceToPath(kart.transform.position);
            bool grace = Time.time < branchExitGraceUntil;

            if (!grace && dist > offTrackDistance && !InAvoidRespawnZone())
            {
                offTrackTimer += dt;
                if (offTrackTimer >= offTrackSeconds) { Respawn("fora da pista"); offTrackTimer = 0f; return; }
            }
            else offTrackTimer = Mathf.Max(0f, offTrackTimer - dt);

            // Caiu DEBAIXO de um trecho elevado (errou rampa, parou embaixo): rota muito acima + lento.
            float below = path.CurrentNearestPoint.y - kart.transform.position.y;
            bool rampContext = IsRampContext();
            float belowThreshold = rampContext ? Mathf.Min(belowRouteHeight, rampBelowRouteHeight) : belowRouteHeight;
            float belowSeconds = rampContext ? Mathf.Min(belowRouteSeconds, rampBelowRouteSeconds) : belowRouteSeconds;
            if (!grace && kart.IsGrounded && kart.SpeedKmh < belowRouteMaxSpeedKmh && below > belowThreshold && dist < offTrackDistance)
            {
                belowRouteTimer += dt;
                if (belowRouteTimer >= belowSeconds) { Respawn(rampContext ? "preso abaixo da rampa" : "abaixo do traçado"); belowRouteTimer = 0f; return; }
            }
            else belowRouteTimer = Mathf.Max(0f, belowRouteTimer - dt);

            // Longe do trecho rastreado por um tempo → adota o trecho onde está (dá a volta).
            if (dist > rejoinDistance)
            {
                wrongSectionTimer += dt;
                if (wrongSectionTimer >= wrongSectionResearchSeconds) { path.ForceGlobalResearch(); wrongSectionTimer = 0f; }
            }
            else wrongSectionTimer = 0f;
        }

        // Acumulador com vazamento: andar rápido drena; só dispara se ficar lento por muito tempo
        // por QUALQUER motivo (rede de segurança independente de input).
        private void UpdateHardStuck(float dt)
        {
            if (InAvoidRespawnZone()) { hardStuckTimer = Mathf.Max(0f, hardStuckTimer - dt); return; }
            if (kart.SpeedKmh < 12f) hardStuckTimer += dt;
            else hardStuckTimer = Mathf.Max(0f, hardStuckTimer - dt * 1.5f);
            if (hardStuckTimer >= hardStuckSeconds) { Respawn("parado tempo demais"); hardStuckTimer = 0f; }
        }

        private void UpdateCheckpointStall(bool paused)
        {
            if (tracker == null || tracker.RaceFinished || checkpointStallSeconds <= 0f) return;
            float key = tracker.CurrentLap * 1000f + tracker.NextCheckpointIndex;
            if (!Mathf.Approximately(key, lastCheckpointKey)) { lastCheckpointKey = key; lastCheckpointTime = Time.time; return; }
            if (paused) { lastCheckpointTime = Mathf.Max(lastCheckpointTime, Time.time - checkpointStallSeconds * 0.5f); return; }
            if (Time.time - lastCheckpointTime >= checkpointStallSeconds)
            {
                Respawn("sem checkpoint há muito tempo");
                lastCheckpointTime = Time.time;
            }
        }

        private bool InAvoidRespawnZone()
        {
            return frame.ActiveZone != null && frame.ActiveZone.AvoidAutoRespawn;
        }

        // ------------------------------------------------------------------ recuperação
        // Escada: 1º — se travado DENTRO de atalho, larga a branch (segue a main, que cobre os CPs);
        //         2º/3º — ré curta apontando para o lado livre; depois respawn (breadcrumb).
        private void Escalate(string reason)
        {
            if (Time.time - lastRecoveryActionTime < recoveryActionCooldown) return;
            lastRecoveryActionTime = Time.time;
            lastRecoveryReason = reason;
            recoveryCount++;

            if (allowBranchAbandonRecovery && path.ForceAbandonBranch(branchAbandonSuppressSeconds))
            {
                progressPathId = -2; progressTimer = 0f; blockedTimer = 0f;
                lastRecoveryReason = reason + " → abandonou atalho";
                return;
            }

            reverseEscalation++;
            if (reverseEscalation >= Mathf.Max(2, maxReverseAttemptsBeforeRespawn)) { Respawn(reason + " (ré não resolveu)"); reverseEscalation = 0; return; }

            BeginReverse(reverseDurationBase * reverseEscalation);
        }

        private void BeginReverse(float duration)
        {
            reverseTimer = duration;
            SetState(BotState.Reversing);
            // Ré apontando o nariz para o lado LIVRE (medido pelos sensores). Em ré, esterçar para
            // o lado oposto aponta o nariz para lá.
            if (sensing.freeSide != 0) reverseSteer = -sensing.freeSide;
            else
            {
                Vector3 aim = path.GetAimPoint(kart.transform.position, profile.lookAheadDistance);
                Vector3 local = kart.transform.InverseTransformDirection(aim - kart.transform.position);
                reverseSteer = local.x >= 0f ? -1f : 1f;
            }
        }

        private void Respawn(string reason)
        {
            lastRespawnReason = reason;
            respawnCount++;
            SetState(BotState.Respawning);
            reverseTimer = 0f; blockedTimer = 0f; belowRouteTimer = 0f; offTrackTimer = 0f;
            progressPathId = -2; progressTimer = 0f; reverseEscalation = 0;
            lastRecoveryActionTime = Time.time;

            if (respawn != null)
            {
                bool checkpointOnly = reason.Contains("fora da pista")
                    || reason.Contains("capotado")
                    || reason.Contains("abaixo")
                    || reason.Contains("rampa");
                bool freshCrumb = useBreadcrumb && hasBreadcrumb
                    && !checkpointOnly
                    && Time.time - breadcrumbTime < breadcrumbMaxAge
                    && Time.time - lastBreadcrumbRespawnTime > breadcrumbRespawnCooldown;

                if (freshCrumb)
                {
                    respawn.RespawnToPose(breadcrumbPos, breadcrumbRot);
                    lastBreadcrumbRespawnTime = Time.time;
                    hasBreadcrumb = false;
                    lastRespawnReason = reason + " → breadcrumb";
                }
                else
                {
                    respawn.Respawn();
                    path.ResetToMainPath();
                    lastRespawnReason = reason + " → checkpoint";
                }
            }

            controlEnabledTime = Time.time; // graça pós-respawn
            // NÃO reseta lastCheckpointTime: a rede de checkpoint (forward-respawn) precisa medir o
            // tempo desde o último checkpoint REAL, imune a respawns. Sem isto, um bot que respawna
            // repetidamente num trecho impassável (ex.: canal final) zerava o timer a cada respawn e
            // nunca disparava o avanço garantido — ficava em loop eterno antes da linha de chegada.
        }

        // Grava a última pose comprovadamente boa (chão, em pé, rápido, em cima da rota — inclusive
        // dentro de branch). É o ponto de respawn preferido: nunca joga o bot ao checkpoint anterior.
        private void UpdateBreadcrumb()
        {
            if (!useBreadcrumb || Time.time < nextBreadcrumbTime) return;
            nextBreadcrumbTime = Time.time + breadcrumbInterval;

            if (reverseTimer > 0f || kart.IsInKnockback || !kart.IsGrounded) return;
            if (kart.transform.up.y < 0.85f || kart.SpeedKmh < breadcrumbMinSpeedKmh) return;
            if (airborneTimer > 0f) return;
            if (!frame.IsValid || frame.DistanceToPath > rejoinDistance) return;

            Vector3 vel = kart.Rigidbody != null ? kart.Rigidbody.linearVelocity : kart.transform.forward;
            vel.y = 0f;
            Vector3 heading = vel.sqrMagnitude > 1f ? vel.normalized : Planar(kart.transform.forward).normalized;

            hasBreadcrumb = true;
            breadcrumbPos = kart.transform.position + Vector3.up * 0.4f;
            breadcrumbRot = Quaternion.LookRotation(heading, Vector3.up);
            breadcrumbTime = Time.time;
        }

        private void UpdateTelemetry(float dt)
        {
            if (kart == null || !kart.CanControl)
                return;

            float speed = kart.SpeedKmh;
            telemetrySeconds += dt;
            speedKmhSeconds += speed * dt;
            maxObservedSpeedKmh = Mathf.Max(maxObservedSpeedKmh, speed);
            if (speed < 70f)
                below70Seconds += dt;

            bool drifting = kart.IsDrifting;
            if (drifting)
                driftSeconds += dt;

            if (drifting && !wasKartDrifting)
            {
                activeDriftEntrySpeedKmh = speed;
                activeDriftStartTime = Time.time;
                lastDriftEntrySpeedKmh = speed;
                driftEntrySpeedSum += speed;
                driftCount++;
            }
            else if (!drifting && wasKartDrifting)
            {
                lastDriftExitSpeedKmh = speed;
                driftExitSpeedSum += speed;
                driftExitCount++;
                driftExitStateUntil = Time.time + 0.5f;
                driftCooldownUntil = Time.time + driftCooldownSeconds;

                if (Time.time - activeDriftStartTime > 0.1f)
                    driftReason = $"drift saiu {activeDriftEntrySpeedKmh:F0}->{speed:F0} km/h";
            }

            wasKartDrifting = drifting;
        }

        // ------------------------------------------------------------------ helpers
        private KartInputState Store(KartInputState s) { lastInput = s; return s; }

        private bool IsOwnCollider(Collider c)
        {
            if (ownColliders == null || c == null) return false;
            for (int i = 0; i < ownColliders.Length; i++) if (ownColliders[i] == c) return true;
            return false;
        }

        private static Vector3 Planar(Vector3 v) { v.y = 0f; return v; }

        private void OnDisable()
        {
            if (traffic != null)
                traffic.Unregister(this);
        }

        // ================================================================== DEBUG (Editor)
        private void OnDrawGizmos()
        {
            if (alwaysDrawDebug) DrawDebug();
        }

        private void OnDrawGizmosSelected()
        {
            if (drawDebug && !alwaysDrawDebug) DrawDebug();
        }

        private void DrawDebug()
        {
            if (!Application.isPlaying || kart == null || !frame.IsValid) return;
            Vector3 pos = kart.transform.position;

            // mira (amarelo) e ponto mais próximo da rota (ciano).
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(pos + Vector3.up * 0.5f, debugAim + Vector3.up * 0.5f);
            Gizmos.DrawWireSphere(debugAim + Vector3.up * 0.5f, 0.5f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(pos, frame.NearestPoint);

            // leque de sensores.
            Vector3 fwd = Planar(kart.transform.forward).normalized;
            Vector3 origin = pos + Vector3.up * sensorOriginHeight;
            float range = sensorRange + Mathf.Clamp01(kart.SpeedKmh / Mathf.Max(1f, maxStraightSpeedKmh)) * sensorRangeSpeedScale;
            if (sensorFanAngles != null)
            {
                foreach (float ang in sensorFanAngles)
                {
                    Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * fwd;
                    bool hit = Physics.SphereCast(origin, sensorRadius, dir, out RaycastHit h, range, sensorMask, QueryTriggerInteraction.Ignore)
                               && h.collider != null && !IsOwnCollider(h.collider)
                               && h.collider.GetComponentInParent<KartController>() == null && h.normal.y <= wallMaxNormalY;
                    Gizmos.color = hit ? Color.red : new Color(0f, 1f, 0f, 0.5f);
                    float d = hit ? h.distance : range;
                    Gizmos.DrawLine(origin, origin + dir * d);
                }
            }
            // sensor de borda.
            Gizmos.color = sensing.edgeAhead ? Color.magenta : new Color(0.5f, 0.5f, 1f, 0.5f);
            Vector3 edge = pos + fwd * edgeProbeAhead + Vector3.up;
            Gizmos.DrawLine(edge, edge + Vector3.down * (edgeDropDepth + 2f));

#if UNITY_EDITOR
            string label =
                $"{name}\n" +
                $"Estado: {state}\n" +
                $"Rota: {RouteKind}  seg {path.CurrentSegmentIndex}  dist {path.CurrentDistanceOnPath:F0}m  fora {frame.DistanceToPath:F1}m\n" +
                $"LookAhead alvo {debugAim.x:F1},{debugAim.y:F1},{debugAim.z:F1}\n" +
                $"Vel {kart.SpeedKmh:F0} / alvo {targetSpeedKmh:F0} km/h  ({slowReason})  media {AverageSpeedKmh:F0} max {maxObservedSpeedKmh:F0} <70 {below70Seconds:F1}s\n" +
                $"Input T{lastInput.Throttle:F2} B{lastInput.Brake:F2} S{lastInput.Steer:F2} HB{(lastInput.Handbrake ? "1" : "0")}/{(lastInput.HandbrakePressed ? "P" : "-")}\n" +
                $"Drift {(kart.IsDrifting ? "SIM" : "nao")} dir {kart.DriftDirection} blend {kart.DriftBlend:F2} cmd:{driftReason} bloqueio:{driftBlockReason} qtd {driftCount} {lastDriftEntrySpeedKmh:F0}->{lastDriftExitSpeedKmh:F0}\n" +
                $"Sensor: frente {(sensing.frontDist >= range ? -1f : sensing.frontDist):F1}m  L {sensing.leftClear:F1}  R {sensing.rightClear:F1}  ladoLivre {(sensing.freeSide>0?"DIR":sensing.freeSide<0?"ESQ":"-")}\n" +
                $"Trafego {(trafficAhead ? "SIM" : "nao")} dist {trafficDistance:F1} v {trafficSpeedKmh:F0} lado {(trafficAvoidSide>0?"DIR":trafficAvoidSide<0?"ESQ":"-")}\n" +
                $"Curva {sensing.cornerAngle:F0}° (sev {sensing.cornerSeverity:F2})  obstaculo:{(ObstacleDetected ? "SIM" : "nao")}\n" +
                $"Zona {(frame.ActiveZone != null ? frame.ActiveZone.Type.ToString() : "-")} prox {(frame.UpcomingZone != null ? frame.UpcomingZone.Type.ToString() : "-")} {frame.DistanceToUpcomingZone:F0}m  ar {airborneTimer:F1}s\n" +
                $"semProgresso {progressTimer:F1}s (Δ{lastRouteDelta:F1}m)  semCP {SecondsSinceCheckpoint:F0}s  rec {recoveryCount} resp {respawnCount}\n" +
                $"recuperacao: {lastRecoveryReason}\n" +
                $"ultimo respawn: {lastRespawnReason}";
            Handles.color = Color.white;
            Handles.Label(pos + Vector3.up * 3.2f, label);
#endif
        }
    }
}
