using PartyRacers.Networking;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Feeds simulated inputs into KartController. The bot still uses the same physics as players;
    /// this script only decides throttle/brake/steer from the racing line.
    ///
    /// Regras importantes de input (corrigem ré/burnout indevidos):
    /// - Outros karts NUNCA são tratados como parede: o probe de parede os ignora e um desvio
    ///   suave dedicado cuida deles (sem freadas que viravam marcha à ré na largada).
    /// - Freio é zerado em baixa velocidade (freio + parado = ré no KartController).
    /// - Acelerador + freio/handbrake em baixa velocidade é proibido (entrava em burnout,
    ///   travando o bot no lugar com fumaça nos pneus).
    /// - Na largada o bot acelera full por alguns segundos, sem freio/handbrake.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BotPathFollower))]
    public class BotDriverController : MonoBehaviour, IKartInputSource
    {
        [Header("Track following")]
        [SerializeField] private float maxStraightSpeedKmh = 145f;
        [SerializeField] private float minCornerSpeedKmh = 42f;
        [SerializeField] private float offPathTargetSpeedKmh = 34f;
        [SerializeField] private float pathRecoveryDistance = 6f;
        [SerializeField] private float hardRecoveryDistance = 15f;
        [SerializeField] private float returnToPathSteerScale = 0.08f;

        [Header("Largada")]
        [Tooltip("Tempo após a liberação da corrida em que o bot acelera full, sem freio/handbrake.")]
        [SerializeField] private float launchFullThrottleSeconds = 1.4f;

        [Header("Wall avoidance")]
        [SerializeField] private LayerMask wallProbeMask = ~0;
        [SerializeField] private float wallProbeHeight = 0.8f;
        [SerializeField] private float wallProbeRadius = 0.55f;
        [SerializeField] private float wallProbeDistance = 10f;
        [SerializeField] private float wallAvoidanceStrength = 0.85f;
        [SerializeField] private float wallBrakeStrength = 0.7f;
        [SerializeField] private float wallMaxNormalY = 0.55f;

        [Header("Desvio de outros karts")]
        [Tooltip("Distância do probe para detectar karts à frente (desvio suave, sem freada brusca).")]
        [SerializeField] private float kartProbeDistance = 8f;
        [SerializeField, Range(0f, 1f)] private float kartAvoidanceSteer = 0.45f;
        [Tooltip("Quanto o bot alivia o acelerador com um kart colado à frente (0 = nada).")]
        [SerializeField, Range(0f, 1f)] private float kartThrottleRelief = 0.35f;
        [Tooltip("Velocidade mínima (km/h) para permitir freio por causa de kart à frente.")]
        [SerializeField] private float kartBrakeMinSpeedKmh = 50f;

        [Header("Handbrake turns")]
        [SerializeField] private bool allowHandbrakeTurns = true;
        [SerializeField] private float handbrakeTurnAngle = 30f;
        [SerializeField] private float handbrakeMinSpeedKmh = 35f;
        [SerializeField] private float driftUpcomingTurn01 = 0.24f;
        [SerializeField] private float driftMinimumSteer = 0.35f;
        [SerializeField] private float driftPulseDuration = 0.38f;
        [SerializeField] private float driftCooldown = 0.55f;
        [SerializeField] private float driftBrakeRelief = 0.18f;
        [SerializeField] private float driftThrottleFloor = 0.78f;

        [Header("Anti-ré / anti-burnout")]
        [Tooltip("Abaixo desta velocidade (km/h) o bot nunca pisa no freio (freio parado = ré).")]
        [SerializeField] private float minBrakeSpeedKmh = 12f;

        [Header("Stuck / recovery")]
        [SerializeField] private float stuckSpeedKmh = 6f;
        [Tooltip("Tempo parado tentando acelerar antes de iniciar a recuperação.")]
        [SerializeField] private float stuckSeconds = 1.6f;
        [SerializeField] private float reverseDuration = 0.7f;
        [SerializeField] private float launchRecoveryGraceSeconds = 4f;
        [SerializeField] private float stuckThrottleThreshold = 0.25f;
        [SerializeField] private float offTrackDistance = 14f;
        [SerializeField] private float offTrackSeconds = 2.5f;
        [Tooltip("Se o bot andar menos que isso (m) em 'noProgressSeconds', é tratado como preso.")]
        [SerializeField] private float noProgressMinDistance = 3f;
        [SerializeField] private float noProgressSeconds = 5f;
        [Tooltip("Se transform.up.y ficar abaixo disso (capotado/de lado) por 'flippedSeconds', respawna.")]
        [SerializeField, Range(-1f, 1f)] private float flippedUpThreshold = 0.35f;
        [SerializeField] private float flippedSeconds = 1.5f;

        private KartController kart;
        private KartRaceTracker tracker;
        private BotPathFollower path;
        private KartRespawn respawn;
        private BotDifficultyProfile profile;
        private Collider[] ownColliders;
        private int seed;

        private float stuckTimer;
        private float reverseTimer;
        private int stuckEscalation;
        private float offTrackTimer;
        private float flippedTimer;
        private float reverseSteer;
        private float wanderPhase;
        private bool handbrakeHeldLastRead;
        private bool wasControlEnabled;
        private float controlEnabledTime = float.NegativeInfinity;
        private float driftPulseTimer;
        private float driftCooldownTimer;
        private KartInputState lastInput;

        // Janela de progresso (anti "preso acelerando contra algo").
        private Vector3 progressAnchor;
        private float progressTimer;

        public bool IsInReverseRecovery => reverseTimer > 0f;
        public KartInputState LastInput => lastInput;

        public void Initialize(KartController kartController, BotDifficultyProfile botProfile, int botSeed)
        {
            kart = kartController;
            profile = botProfile ?? new BotDifficultyProfile();
            seed = botSeed;
            wanderPhase = (seed % 1000) * 0.137f;
            ResetRecoveryState();
            handbrakeHeldLastRead = false;
            lastInput = KartInputState.Neutral;

            path = GetComponent<BotPathFollower>();
            tracker = kart != null ? kart.GetComponent<KartRaceTracker>() : null;
            respawn = kart != null ? kart.GetComponent<KartRespawn>() : null;
            ownColliders = kart != null ? kart.GetComponentsInChildren<Collider>(true) : null;

            // Segurança: este controlador é exclusivo de bots. Nunca assume o kart do player local.
            KartLocalRig rig = kart != null ? kart.GetComponent<KartLocalRig>() : null;
            if (rig != null && rig.IsLocalPlayer)
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

            kart?.SetInputSource(this);
            wasControlEnabled = kart != null && kart.CanControl;
            controlEnabledTime = wasControlEnabled ? Time.time : float.NegativeInfinity;
        }

        public KartInputState Read()
        {
            if (kart == null || path == null || !path.IsReady || !enabled)
            {
                handbrakeHeldLastRead = false;
                return StoreInput(KartInputState.Neutral);
            }

            // Detecta a liberação da largada aqui (Read roda no Update do KartController,
            // que pode executar antes do nosso Update neste frame).
            if (!kart.CanControl)
            {
                wasControlEnabled = false;
                handbrakeHeldLastRead = false;
                return StoreInput(KartInputState.Neutral);
            }

            if (!wasControlEnabled)
                OnControlEnabled();

            float dt = Time.deltaTime;
            driftPulseTimer = Mathf.Max(0f, driftPulseTimer - dt);
            driftCooldownTimer = Mathf.Max(0f, driftCooldownTimer - dt);

            // Recuperação por ré: freio em baixa velocidade = ré no KartController.
            if (reverseTimer > 0f)
            {
                handbrakeHeldLastRead = false;
                return StoreInput(new KartInputState
                {
                    Throttle = 0f,
                    Brake = 1f,
                    Steer = reverseSteer,
                    Handbrake = false,
                    HandbrakePressed = false
                });
            }

            Vector3 pos = kart.transform.position;
            float speedKmh = kart.SpeedKmh;
            float speed01 = Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxStraightSpeedKmh));
            float baseLookAhead = profile.lookAheadDistance + speed01 * profile.lookAheadDistance * 0.65f;

            BotPathFollower.PathFrame frame = path.GetPathFrame(pos, baseLookAhead);
            if (!frame.IsValid)
                return StoreInput(KartInputState.Neutral);

            float offPath01 = Mathf.InverseLerp(pathRecoveryDistance, hardRecoveryDistance, frame.DistanceToPath);
            float lookAhead = Mathf.Lerp(baseLookAhead, Mathf.Max(4f, baseLookAhead * 0.45f), offPath01);
            frame = path.GetPathFrame(pos, lookAhead);

            Vector3 aim = frame.AimPoint;
            if (offPath01 > 0f)
            {
                Vector3 recoveryAim = frame.NearestPoint + frame.Tangent * Mathf.Max(5f, lookAhead * 0.55f);
                aim = Vector3.Lerp(aim, recoveryAim, offPath01 * 0.45f);
            }

            Vector3 toAim = aim - pos;
            toAim.y = 0f;

            if (toAim.sqrMagnitude < 0.01f)
                return StoreInput(new KartInputState { Throttle = profile.throttleScale, Steer = 0f });

            Vector3 localAim = kart.transform.InverseTransformDirection(toAim.normalized);
            Vector3 localNearest = kart.transform.InverseTransformPoint(frame.NearestPoint);

            float wander = (Mathf.PerlinNoise(wanderPhase + Time.time * profile.wanderFrequency, seed * 0.31f) - 0.5f) * 2f;
            float returnSteer = Mathf.Clamp(localNearest.x * returnToPathSteerScale, -0.6f, 0.6f) * offPath01;
            float steer = localAim.x * profile.steerSharpness + returnSteer + wander * profile.steerWander * (1f - offPath01);

            // Paredes (karts são tratados separadamente, NUNCA como parede).
            float wallBrake;
            float wallSteer = ComputeWallAvoidance(steer, out wallBrake);

            // Desvio suave de karts à frente.
            float kartBrake;
            float kartThrottleScale;
            float kartSteer = ComputeKartAvoidance(steer, speedKmh, out kartBrake, out kartThrottleScale);

            steer = Mathf.Clamp(steer + wallSteer + kartSteer, -1f, 1f);

            float angle = Vector3.Angle(kart.transform.forward, toAim);
            float aimCorner01 = Mathf.InverseLerp(18f, 78f, angle);
            float corner01 = Mathf.Clamp01(Mathf.Max(aimCorner01, frame.UpcomingTurn01));
            float caution01 = Mathf.Clamp01(corner01 * Mathf.Lerp(0.75f, 1.2f, profile.corneringCaution));

            float targetSpeed = Mathf.Lerp(maxStraightSpeedKmh * profile.throttleScale, minCornerSpeedKmh, caution01);
            targetSpeed = Mathf.Lerp(targetSpeed, offPathTargetSpeedKmh, offPath01);

            float throttle = profile.throttleScale * Mathf.Lerp(1f, Mathf.Max(0.15f, 1f - profile.corneringCaution), corner01);
            throttle *= Mathf.Lerp(1f, 0.45f, offPath01);
            throttle *= kartThrottleScale;

            float brake = Mathf.Max(wallBrake, kartBrake);
            if (speedKmh > targetSpeed)
            {
                float speedBrake = Mathf.InverseLerp(targetSpeed + 5f, targetSpeed + 36f, speedKmh);
                brake = Mathf.Max(brake, speedBrake * 0.75f);
                throttle *= 1f - Mathf.Clamp01(speedBrake * 1.2f);
            }

            if (localAim.z < -0.15f && speedKmh > 22f)
            {
                brake = Mathf.Max(brake, 0.65f);
                throttle = 0f;
            }
            else if (angle > 70f && speedKmh > 45f)
            {
                brake = Mathf.Max(brake, 0.45f);
            }

            if (profile.mistakeChance > 0f && Random.value < profile.mistakeChance * Time.deltaTime)
                throttle *= 0.4f;

            bool wantsDrift = allowHandbrakeTurns
                && frame.DistanceToPath < pathRecoveryDistance * 1.15f
                && offPath01 < 0.55f
                && localAim.z > -0.05f
                && speedKmh >= handbrakeMinSpeedKmh
                && Mathf.Abs(steer) >= driftMinimumSteer
                && wallBrake < 0.55f
                && (angle >= handbrakeTurnAngle || frame.UpcomingTurn01 >= driftUpcomingTurn01);

            if (wantsDrift && driftPulseTimer <= 0f && driftCooldownTimer <= 0f)
            {
                driftPulseTimer = driftPulseDuration;
                driftCooldownTimer = driftCooldown;
            }

            bool handbrake = wantsDrift && driftPulseTimer > 0f;
            if (handbrake)
            {
                brake = Mathf.Min(brake, driftBrakeRelief);
                throttle = Mathf.Max(throttle, Mathf.Max(0.65f, profile.throttleScale * driftThrottleFloor));
            }

            // ---------------- Clamps finais de segurança (largada/ré/burnout) ----------------
            bool launching = Time.time - controlEnabledTime < launchFullThrottleSeconds;
            if (launching)
            {
                // Largada: full gás para frente, nada de freio/handbrake/input residual.
                throttle = Mathf.Max(throttle, profile.throttleScale);
                brake = 0f;
                handbrake = false;
            }

            // Nunca freia em baixa velocidade (freio parado = marcha à ré no KartController).
            if (speedKmh < minBrakeSpeedKmh)
                brake = 0f;

            // Nunca segura handbrake abaixo da velocidade de drift (evita travar/queimar pneu).
            if (handbrake && speedKmh < handbrakeMinSpeedKmh)
                handbrake = false;

            // Acelerador + freio simultâneos em baixa velocidade = burnout (kart preso soltando
            // fumaça). Bots nunca fazem isso: em baixa velocidade o acelerador vence.
            if (throttle > 0.1f && brake > 0.1f && speedKmh < 25f)
                brake = 0f;

            bool handbrakePressed = handbrake && !handbrakeHeldLastRead;
            handbrakeHeldLastRead = handbrake;

            return StoreInput(new KartInputState
            {
                Throttle = Mathf.Clamp01(throttle),
                Brake = Mathf.Clamp01(brake),
                Steer = steer,
                Handbrake = handbrake,
                HandbrakePressed = handbrakePressed
            });
        }

        private void OnControlEnabled()
        {
            wasControlEnabled = true;
            controlEnabledTime = Time.time;
            ResetRecoveryState();
        }

        private void ResetRecoveryState()
        {
            stuckTimer = 0f;
            reverseTimer = 0f;
            stuckEscalation = 0;
            offTrackTimer = 0f;
            flippedTimer = 0f;
            driftPulseTimer = 0f;
            driftCooldownTimer = 0f;
            progressAnchor = transform.position;
            progressTimer = 0f;
        }

        // ------------------------------------------------------------------ recuperação
        private void Update()
        {
            if (kart == null || !kart.CanControl || !enabled)
            {
                wasControlEnabled = kart != null && kart.CanControl;
                return;
            }

            if (!wasControlEnabled)
                OnControlEnabled();

            float dt = Time.deltaTime;

            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                if (reverseTimer <= 0f)
                {
                    // Saiu da ré: zera janela de progresso para dar chance ao novo arranque.
                    progressAnchor = transform.position;
                    progressTimer = 0f;
                }
                return;
            }

            bool inLaunchGrace = Time.time - controlEnabledTime < launchRecoveryGraceSeconds;

            UpdateFlippedDetection(dt);
            UpdateStuckDetection(dt, inLaunchGrace);
            UpdateNoProgressDetection(dt, inLaunchGrace);
            UpdateOffTrackDetection(dt);
        }

        // Capotado / muito inclinado: não há recuperação por input — respawn direto.
        private void UpdateFlippedDetection(float dt)
        {
            if (transform.up.y < flippedUpThreshold)
            {
                flippedTimer += dt;
                if (flippedTimer >= flippedSeconds)
                {
                    RespawnRecovery();
                    flippedTimer = 0f;
                }
            }
            else
            {
                flippedTimer = 0f;
            }
        }

        // Parado tentando acelerar: recuperação gradual (ré curta → ré maior → respawn).
        private void UpdateStuckDetection(float dt, bool inLaunchGrace)
        {
            bool tryingForward = lastInput.Throttle > stuckThrottleThreshold && lastInput.Brake < 0.75f;

            if (!inLaunchGrace && tryingForward && kart.SpeedKmh < stuckSpeedKmh)
            {
                stuckTimer += dt;
                if (stuckTimer >= stuckSeconds)
                {
                    EscalateRecovery();
                    stuckTimer = 0f;
                }
            }
            else
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - dt * 2f);
                if (kart.SpeedKmh > stuckSpeedKmh * 2f)
                    stuckEscalation = 0;
            }
        }

        // Sem progresso real (ex.: acelerando raspando numa parede/kart sem sair do lugar).
        private void UpdateNoProgressDetection(float dt, bool inLaunchGrace)
        {
            if (inLaunchGrace)
            {
                progressAnchor = transform.position;
                progressTimer = 0f;
                return;
            }

            progressTimer += dt;
            if (progressTimer < noProgressSeconds)
                return;

            Vector3 delta = transform.position - progressAnchor;
            delta.y = 0f;

            if (delta.magnitude < noProgressMinDistance && lastInput.Throttle > stuckThrottleThreshold)
                EscalateRecovery();

            progressAnchor = transform.position;
            progressTimer = 0f;
        }

        private void UpdateOffTrackDetection(float dt)
        {
            if (!path.IsReady)
                return;

            float dist = path.PlanarDistanceToPath(kart.transform.position);
            if (dist > offTrackDistance)
            {
                offTrackTimer += dt;
                if (offTrackTimer >= offTrackSeconds)
                {
                    RespawnRecovery();
                    offTrackTimer = 0f;
                }
            }
            else
            {
                offTrackTimer = Mathf.Max(0f, offTrackTimer - dt);
            }
        }

        // Escada de recuperação: 1ª/2ª tentativa = ré (cada vez mais longa) realinhando para o
        // traçado; 3ª = respawn no último checkpoint. Evita teleporte frequente.
        private void EscalateRecovery()
        {
            stuckEscalation++;

            if (stuckEscalation >= 3)
            {
                RespawnRecovery();
                stuckEscalation = 0;
                return;
            }

            BeginReverseRecovery(reverseDuration * stuckEscalation);
        }

        private void BeginReverseRecovery(float duration)
        {
            reverseTimer = duration;
            Vector3 aim = path.GetAimPoint(kart.transform.position, profile.lookAheadDistance);
            Vector3 toAim = aim - kart.transform.position;
            Vector3 local = kart.transform.InverseTransformDirection(toAim);
            // Em ré, esterçar para o lado OPOSTO do alvo aponta o nariz para o alvo.
            reverseSteer = local.x >= 0f ? -1f : 1f;
        }

        private void RespawnRecovery()
        {
            reverseTimer = 0f;
            stuckTimer = 0f;
            progressTimer = 0f;

            if (respawn != null)
                respawn.Respawn();

            path?.ResetToMainPath();
            progressAnchor = transform.position;

            // Dá um período de graça pós-respawn (mesmo tratamento da largada).
            controlEnabledTime = Time.time;
        }

        private KartInputState StoreInput(KartInputState input)
        {
            lastInput = input;
            return input;
        }

        // ------------------------------------------------------------------ sensores
        private float ComputeWallAvoidance(float preferredSteer, out float brake)
        {
            brake = 0f;
            if (kart == null || wallProbeDistance <= 0f || wallProbeRadius <= 0f)
                return 0f;

            Vector3 forward = Planar(kart.transform.forward);
            if (forward.sqrMagnitude < 0.001f)
                return 0f;
            forward.Normalize();

            Vector3 right = Planar(kart.transform.right).normalized;
            Vector3 origin = kart.transform.position + Vector3.up * wallProbeHeight;

            float bestSeverity = 0f;
            float bestSteer = 0f;

            ProbeWall(origin, forward, preferredSteer, ref bestSeverity, ref bestSteer, ref brake);
            ProbeWall(origin, (forward + right * 0.42f).normalized, preferredSteer, ref bestSeverity, ref bestSteer, ref brake);
            ProbeWall(origin, (forward - right * 0.42f).normalized, preferredSteer, ref bestSeverity, ref bestSteer, ref brake);

            return bestSteer * bestSeverity * wallAvoidanceStrength;
        }

        private void ProbeWall(
            Vector3 origin,
            Vector3 direction,
            float preferredSteer,
            ref float bestSeverity,
            ref float bestSteer,
            ref float brake)
        {
            if (!Physics.SphereCast(
                    origin,
                    wallProbeRadius,
                    direction,
                    out RaycastHit hit,
                    wallProbeDistance,
                    wallProbeMask,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider == null || IsOwnCollider(hit.collider) || hit.normal.y > wallMaxNormalY)
                return;

            // Outro kart NÃO é parede (era a causa dos bots darem ré na largada: o probe via o
            // kart da frente no grid, freava parado e o freio virava marcha à ré).
            if (hit.collider.GetComponentInParent<KartController>() != null)
                return;

            float severity = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, wallProbeDistance));
            Vector3 localNormal = kart.transform.InverseTransformDirection(hit.normal);
            float steerAway = Mathf.Clamp(localNormal.x, -1f, 1f);

            if (Mathf.Abs(steerAway) < 0.15f)
                steerAway = Mathf.Abs(preferredSteer) > 0.1f ? Mathf.Sign(preferredSteer) : 1f;

            if (severity > bestSeverity)
            {
                bestSeverity = severity;
                bestSteer = steerAway;
            }

            brake = Mathf.Max(brake, severity * wallBrakeStrength);
        }

        // Desvio dedicado para karts: esterço suave para o lado livre + alívio de acelerador.
        // Freio só em velocidade alta (nunca causa ré/burnout em baixa velocidade).
        private float ComputeKartAvoidance(float preferredSteer, float speedKmh, out float brake, out float throttleScale)
        {
            brake = 0f;
            throttleScale = 1f;

            if (kart == null || kartProbeDistance <= 0f)
                return 0f;

            Vector3 forward = Planar(kart.transform.forward);
            if (forward.sqrMagnitude < 0.001f)
                return 0f;
            forward.Normalize();

            Vector3 origin = kart.transform.position + Vector3.up * wallProbeHeight;

            if (!Physics.SphereCast(
                    origin,
                    wallProbeRadius,
                    forward,
                    out RaycastHit hit,
                    kartProbeDistance,
                    wallProbeMask,
                    QueryTriggerInteraction.Ignore))
            {
                return 0f;
            }

            if (hit.collider == null || IsOwnCollider(hit.collider))
                return 0f;

            KartController otherKart = hit.collider.GetComponentInParent<KartController>();
            if (otherKart == null || otherKart == kart)
                return 0f;

            float severity = 1f - Mathf.Clamp01(hit.distance / Mathf.Max(0.01f, kartProbeDistance));

            // Desvia para o lado em que o outro kart NÃO está.
            Vector3 localOther = kart.transform.InverseTransformPoint(otherKart.transform.position);
            float steerAway = localOther.x >= 0f ? -1f : 1f;
            if (Mathf.Abs(preferredSteer) > 0.35f)
                steerAway = Mathf.Sign(preferredSteer); // já estava virando: mantém a intenção

            throttleScale = 1f - kartThrottleRelief * severity;

            if (speedKmh > kartBrakeMinSpeedKmh && severity > 0.6f)
                brake = (severity - 0.6f) * 0.5f;

            return steerAway * severity * kartAvoidanceSteer;
        }

        private bool IsOwnCollider(Collider candidate)
        {
            if (ownColliders == null || candidate == null)
                return false;

            for (int i = 0; i < ownColliders.Length; i++)
            {
                if (ownColliders[i] == candidate)
                    return true;
            }

            return false;
        }

        private static Vector3 Planar(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
