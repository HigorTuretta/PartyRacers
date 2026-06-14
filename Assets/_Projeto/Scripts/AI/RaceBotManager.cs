using System.Collections;
using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;

#if PARTYRACERS_ONLINE
using Unity.Netcode;
#endif

namespace PartyRacers.AI
{
    /// <summary>
    /// Cria, configura e registra bots para completar a corrida até um total de competidores
    /// (padrão 16), contando os players reais já presentes (local e online). Os bots reutilizam
    /// o MESMO prefab de kart dos players, alimentados por IA (BotDriverController) e registrados
    /// como participantes reais no RaceManager (ranking, voltas e largada compartilhados).
    ///
    /// Autoridade: offline cria localmente; online apenas o servidor/host cria (clientes não).
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceBotManager : MonoBehaviour
    {
        [Header("Total de competidores")]
        [Tooltip("Total alvo de competidores na corrida (players reais + bots).")]
        [Range(1, 16)] [SerializeField] private int maxCompetitors = RaceConstants.MaxPlayers;

        [Tooltip("Preenche os bots automaticamente no início da corrida.")]
        [SerializeField] private bool fillOnStart = true;

        [Header("Prefab e referências")]
        [Tooltip("Prefab de kart usado pelos bots (mesmo veículo dos players). Ex.: PlayerKart_Local.")]
        [SerializeField] private GameObject botKartPrefab;
        [Tooltip("Opcional. Se vazio, busca o RaceManager na cena.")]
        [SerializeField] private RaceManager raceManager;

        [Header("Identidade dos bots")]
        [SerializeField]
        private string[] botNames =
        {
            "TURBO", "NITRO", "BLAZE", "ZÉ", "RAIO", "FÚRIA", "PIPOCA", "TROVÃO",
            "FOGUETE", "BÓLIDO", "CHISPA", "VENTO", "DÍNAMO", "FAÍSCA", "CICLONE", "MÍSSIL"
        };

        [Header("Dificuldade (sorteada por bot)")]
        [SerializeField]
        private List<BotDifficultyProfile> difficultyProfiles = new List<BotDifficultyProfile>
        {
            new BotDifficultyProfile { label = "Competitivo", throttleScale = 0.96f, corneringCaution = 0.34f, steerSharpness = 1.75f, lookAheadDistance = 17f, steerWander = 0.055f, mistakeChance = 0.025f },
            new BotDifficultyProfile { label = "Agressivo",   throttleScale = 1.02f, corneringCaution = 0.26f, steerSharpness = 1.95f, lookAheadDistance = 19f, steerWander = 0.045f, mistakeChance = 0.018f },
            new BotDifficultyProfile { label = "Elite",       throttleScale = 1.06f, corneringCaution = 0.18f, steerSharpness = 2.12f, lookAheadDistance = 21f, steerWander = 0.035f, mistakeChance = 0.010f }
        };

        [Header("Competitividade")]
        [Tooltip("Garante que perfis antigos serializados na cena não deixem bots lentos demais.")]
        [SerializeField] private bool enforceCompetitiveProfiles = true;
        [SerializeField, Range(0.8f, 1.1f)] private float minCompetitiveThrottleScale = 0.96f;
        [SerializeField, Range(0f, 1f)] private float maxCompetitiveCorneringCaution = 0.38f;
        [SerializeField, Range(0.5f, 3f)] private float minCompetitiveSteerSharpness = 1.7f;
        [SerializeField, Range(4f, 30f)] private float minCompetitiveLookAhead = 16f;
        [SerializeField, Range(0f, 0.35f)] private float maxCompetitiveSteerWander = 0.07f;
        [SerializeField, Range(0f, 1f)] private float maxCompetitiveMistakeChance = 0.035f;

        [Header("Visual dos bots")]
        [Tooltip("Sorteia o modelo do carro de cada bot entre as variantes do KartVisualCustomizer (ex.: 15 rigs).")]
        [SerializeField] private bool randomizeBotCarModels = true;
        [Tooltip("Evita que bots usem o mesmo modelo escolhido pelo player na garagem.")]
        [SerializeField] private bool avoidPlayerCarModel = true;
        [Tooltip("Sorteia também as peças (para-choques, rodas, spoiler, piloto...).")]
        [SerializeField] private bool randomizeBotCarElements = true;
        [Tooltip("Restringe os índices de modelos usados pelos bots (vazio = todos).")]
        [SerializeField] private List<int> allowedBotCarIndices = new List<int>();

        [Header("Performance")]
        [Tooltip("Desliga fumaça de drift/burnout e marcas de pneu dos BOTS (grande ganho com muitos " +
                 "karts na tela). O player mantém todos os efeitos.")]
        [SerializeField] private bool disableBotSmokeEffects = true;

        [Header("Determinismo")]
        [Tooltip("Semente base da corrida (cor/perfil/nome derivam dela + índice do bot).")]
        [SerializeField] private int raceSeed = 1000;

        [Header("Telemetria de validação")]
        [SerializeField] private bool logBotTelemetry = true;
        [SerializeField] private float telemetryLogInterval = 30f;
        [SerializeField] private float firstLapSnapshotAfterSeconds = 180f;

        private readonly List<KartController> spawnedBots = new List<KartController>();
        private bool filled;
        private bool raceWasStarted;
        private bool firstLapSnapshotLogged;
        private float raceStartTime = -1f;
        private float nextTelemetryLogTime;

        public int MaxCompetitors => maxCompetitors;
        public IReadOnlyList<KartController> SpawnedBots => spawnedBots;

        private void Start()
        {
            if (fillOnStart)
                StartCoroutine(FillRoutine());
        }

        private void Update()
        {
            if (!logBotTelemetry || raceManager == null)
                return;

            bool raceStarted = raceManager.RaceStarted;
            if (raceStarted && !raceWasStarted)
            {
                raceStartTime = Time.time;
                nextTelemetryLogTime = Time.time + telemetryLogInterval;
                firstLapSnapshotLogged = false;
            }

            raceWasStarted = raceStarted;
            if (!raceStarted || spawnedBots.Count == 0)
                return;

            if (Time.time >= nextTelemetryLogTime)
            {
                LogTelemetrySummary(false);
                nextTelemetryLogTime = Time.time + telemetryLogInterval;
            }

            if (!firstLapSnapshotLogged && ShouldLogFirstLapSnapshot())
            {
                LogTelemetrySummary(true);
                firstLapSnapshotLogged = true;
            }
        }

        private IEnumerator FillRoutine()
        {
            // Espera um frame para o RaceManager coletar os karts reais (RaceManager.Start).
            yield return null;

            FillBots();
        }

        /// <summary>Cria os bots necessários para atingir 'maxCompetitors'. Idempotente.</summary>
        public void FillBots()
        {
            if (filled)
                return;

            if (!ShouldSpawnHere())
                return;

            if (raceManager == null)
                raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);

            if (raceManager == null)
            {
                Debug.LogWarning("[RaceBotManager] RaceManager não encontrado — bots não criados.");
                return;
            }

            if (botKartPrefab == null)
            {
                Debug.LogWarning("[RaceBotManager] 'botKartPrefab' não atribuído — bots não criados.");
                return;
            }

            int realCount = CountRealKarts();
            int botsNeeded = Mathf.Clamp(maxCompetitors - realCount, 0, RaceConstants.MaxPlayers);

            for (int i = 0; i < botsNeeded; i++)
                SpawnBot(realCount + i, i);

            filled = true;
            nextTelemetryLogTime = Time.time + telemetryLogInterval;
            if (logBotTelemetry)
                BotFailureAnalyzer.EnsureInstance();
            Debug.Log($"[RaceBotManager] Players reais={realCount}, bots criados={botsNeeded}, total={realCount + botsNeeded}.");
        }

        private int CountRealKarts()
        {
            if (raceManager != null && raceManager.Karts != null)
                return raceManager.Karts.Count;

            return 0;
        }

        private bool ShouldSpawnHere()
        {
#if PARTYRACERS_ONLINE
            bool online = NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.IsOnline;
            if (online)
            {
                NetworkManager nm = NetworkManager.Singleton;
                // Apenas servidor/host cria bots; clientes nunca (evita duplicação).
                return nm != null && nm.IsServer;
            }
#endif
            return true;
        }

        private void SpawnBot(int spawnIndex, int botIndex)
        {
            int seed = raceSeed + botIndex * 7919;

            Pose pose = ResolveSpawnPose(spawnIndex);

            // Instancia sob um holder INATIVO para configurar antes do Awake (evita ligar câmera do bot).
            GameObject holder = new GameObject("BotSpawnHolder");
            holder.SetActive(false);
            GameObject go = Instantiate(botKartPrefab, pose.position, pose.rotation, holder.transform);
            go.name = $"Bot_{botIndex + 1}_{ResolveName(botIndex)}";

            // Marca como NÃO-local antes de ativar → rig local (câmera/HUD) fica desligado.
            KartLocalRig rig = go.GetComponent<KartLocalRig>();
            if (rig != null)
                rig.IsLocalPlayer = false;

            // Ativa (Awake roda agora, já com o rig desligado).
            go.transform.SetParent(null, true);
            Destroy(holder);

            KartController kart = go.GetComponent<KartController>();
            if (kart == null)
            {
                Debug.LogWarning("[RaceBotManager] Prefab de bot sem KartController — ignorando.");
                Destroy(go);
                return;
            }

            DisablePlayerOnlyComponents(go);

            // Identidade (nome/kind) — reutiliza KartNetworkIdentity (o que a HUD já lê).
            ConfigureIdentity(go, botIndex);

            // IA: path follower + driver + customização visual.
            BotPathFollower follower = go.GetComponent<BotPathFollower>();
            if (follower == null)
                follower = go.AddComponent<BotPathFollower>();

            BotDriverController driver = go.GetComponent<BotDriverController>();
            if (driver == null)
                driver = go.AddComponent<BotDriverController>();

            BotDifficultyProfile profile = PickProfile(botIndex).Varied(seed);
            ApplyCompetitiveProfileFloor(profile);
            driver.Initialize(kart, profile, seed);

            BotPowerController powerController = go.GetComponent<BotPowerController>();
            if (powerController == null)
                powerController = go.AddComponent<BotPowerController>();
            powerController.Initialize(kart, seed);

            BotKartCustomizer customizer = go.GetComponent<BotKartCustomizer>();
            if (customizer == null)
                customizer = go.AddComponent<BotKartCustomizer>();
            customizer.Configure(randomizeBotCarModels, avoidPlayerCarModel, randomizeBotCarElements, allowedBotCarIndices);
            // deckIndex = botIndex distribui os modelos sem repetição (baralho embaralhado por raceSeed).
            customizer.Apply(seed, botIndex, raceSeed);

#if PARTYRACERS_ONLINE
            TrySpawnNetworked(go);
#endif

            // Registra como competidor real (ranking/voltas/largada compartilhados).
            raceManager.RegisterKart(kart);
            spawnedBots.Add(kart);
        }

        private void DisablePlayerOnlyComponents(GameObject go)
        {
            // Componentes que só fazem sentido para o player local (câmera/efeitos de tela).
            DisableComponent<KartCameraDynamics>(go);
            DisableComponent<KartTurboScreenEffect>(go);

            // KartRespawn fica ATIVO nos bots: a tecla R já é ignorada por karts não-locais e o
            // componente agora cuida do respawn automático fora da pista (Terreno) + ghost.

            // Fumaça/marcas de pneu dos bots: emissores caros (instanciam muitos GameObjects/meshes).
            // Desligados por padrão — o player mantém os efeitos. Reduz drasticamente o custo com 16 karts.
            if (disableBotSmokeEffects)
            {
                DisableComponent<KartDriftPuffTrail>(go);
                DisableComponent<KartTireMarks>(go);
                DisableComponent<KartDriftParticles>(go);
            }

            // Garante que não sobre nenhum AudioListener ativo no rig do bot.
            foreach (AudioListener listener in go.GetComponentsInChildren<AudioListener>(true))
                listener.enabled = false;
        }

        private static void DisableComponent<T>(GameObject go) where T : MonoBehaviour
        {
            T comp = go.GetComponent<T>();
            if (comp != null)
                comp.enabled = false;
        }

        private void ConfigureIdentity(GameObject go, int botIndex)
        {
            KartNetworkIdentity identity = go.GetComponent<KartNetworkIdentity>();
            if (identity == null)
                identity = go.AddComponent<KartNetworkIdentity>();

            identity.SetKind(PlayerKind.Bot);
            identity.SetPlayerId($"bot_{botIndex}");
            identity.SetDisplayName(ResolveName(botIndex));
        }

        private string ResolveName(int botIndex)
        {
            if (botNames == null || botNames.Length == 0)
                return $"BOT {botIndex + 1}";

            string baseName = botNames[botIndex % botNames.Length];
            int lap = botIndex / botNames.Length;
            return lap == 0 ? baseName : $"{baseName} {lap + 1}";
        }

        private BotDifficultyProfile PickProfile(int botIndex)
        {
            if (difficultyProfiles == null || difficultyProfiles.Count == 0)
                return new BotDifficultyProfile();

            return difficultyProfiles[botIndex % difficultyProfiles.Count];
        }

        private void ApplyCompetitiveProfileFloor(BotDifficultyProfile profile)
        {
            if (!enforceCompetitiveProfiles || profile == null)
                return;

            profile.throttleScale = Mathf.Max(profile.throttleScale, minCompetitiveThrottleScale);
            profile.corneringCaution = Mathf.Min(profile.corneringCaution, maxCompetitiveCorneringCaution);
            profile.steerSharpness = Mathf.Max(profile.steerSharpness, minCompetitiveSteerSharpness);
            profile.lookAheadDistance = Mathf.Max(profile.lookAheadDistance, minCompetitiveLookAhead);
            profile.steerWander = Mathf.Min(profile.steerWander, maxCompetitiveSteerWander);
            profile.mistakeChance = Mathf.Min(profile.mistakeChance, maxCompetitiveMistakeChance);
        }

        private bool ShouldLogFirstLapSnapshot()
        {
            if (raceStartTime < 0f || spawnedBots.Count == 0)
                return false;

            int completed = 0;
            for (int i = 0; i < spawnedBots.Count; i++)
            {
                KartRaceTracker tracker = spawnedBots[i] != null ? spawnedBots[i].GetComponent<KartRaceTracker>() : null;
                if (tracker != null && tracker.LapTimes.Count > 0)
                    completed++;
            }

            int majority = Mathf.FloorToInt(spawnedBots.Count * 0.5f) + 1;
            return completed >= majority || Time.time - raceStartTime >= firstLapSnapshotAfterSeconds;
        }

        private void LogTelemetrySummary(bool firstLapSnapshot)
        {
            int botCount = spawnedBots.Count;
            if (botCount == 0)
                return;

            int completedFirstLap = 0;
            int stuck = 0;
            float bestLap = float.MaxValue;
            float worstLap = -1f;
            float lapSum = 0f;
            float avgSpeedSum = 0f;
            float maxSpeed = 0f;
            float below70 = 0f;
            float telemetryTime = 0f;
            int recoveries = 0;
            int respawns = 0;
            int drifts = 0;
            int artificial = 0;
            int laneChanges = 0;
            int yielding = 0;
            float driftEntrySum = 0f;
            float driftExitSum = 0f;
            int driftEntrySamples = 0;
            int driftExitSamples = 0;
            string stuckPoints = "";

            for (int i = 0; i < botCount; i++)
            {
                KartController kart = spawnedBots[i];
                if (kart == null)
                    continue;

                KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
                BotDriverController driver = kart.GetComponent<BotDriverController>();

                if (tracker != null && tracker.LapTimes.Count > 0)
                {
                    float lap = tracker.LapTimes[0];
                    completedFirstLap++;
                    bestLap = Mathf.Min(bestLap, lap);
                    worstLap = Mathf.Max(worstLap, lap);
                    lapSum += lap;
                }

                if (driver != null)
                {
                    avgSpeedSum += driver.AverageSpeedKmh;
                    maxSpeed = Mathf.Max(maxSpeed, driver.MaxObservedSpeedKmh);
                    below70 += driver.Below70Seconds;
                    telemetryTime += driver.TelemetrySeconds;
                    recoveries += driver.RecoveryCount;
                    respawns += driver.RespawnCount;
                    drifts += driver.DriftCount;
                    artificial += driver.ArtificialCheckpointAdvances;
                    laneChanges += driver.LaneChangeCount;
                    if (driver.IsYieldingLane)
                        yielding++;

                    if (driver.DriftCount > 0)
                    {
                        driftEntrySum += driver.MeanDriftEntrySpeedKmh;
                        driftEntrySamples++;
                    }

                    if (driver.MeanDriftExitSpeedKmh > 0f)
                    {
                        driftExitSum += driver.MeanDriftExitSpeedKmh;
                        driftExitSamples++;
                    }

                    bool isStuck = tracker == null || !tracker.RaceFinished;
                    isStuck = isStuck && (driver.State == BotDriverController.BotState.Reversing
                        || driver.State == BotDriverController.BotState.Respawning
                        || driver.SecondsWithoutProgress > 3f
                        || (kart.SpeedKmh < 8f && driver.LastInput.Throttle > 0.35f));

                    if (isStuck)
                    {
                        stuck++;
                        if (stuckPoints.Length < 420)
                        {
                            Vector3 p = kart.transform.position;
                            stuckPoints += $"{kart.name}@seg{kart.GetComponent<BotPathFollower>()?.CurrentSegmentIndex ?? -1}({p.x:F1},{p.y:F1},{p.z:F1}) {driver.LastRecoveryReason}; ";
                        }
                    }
                }
            }

            float avgLap = completedFirstLap > 0 ? lapSum / completedFirstLap : -1f;
            float avgSpeed = botCount > 0 ? avgSpeedSum / botCount : 0f;
            float below70Pct = telemetryTime > 0.01f ? below70 / telemetryTime * 100f : 0f;
            float meanDriftEntry = driftEntrySamples > 0 ? driftEntrySum / driftEntrySamples : 0f;
            float meanDriftExit = driftExitSamples > 0 ? driftExitSum / driftExitSamples : 0f;
            string prefix = firstLapSnapshot ? "[RaceBotManager][AI_VALIDATION][FIRST_LAP]" : "[RaceBotManager][AI_VALIDATION]";

            Debug.Log(
                $"{prefix} bots={botCount} completaramPrimeiraVolta={completedFirstLap}/{botCount} " +
                $"melhor={FormatTime(bestLap)} media={FormatTime(avgLap)} pior={FormatTime(worstLap)} " +
                $"presos={stuck} velocidadeMedia={avgSpeed:F1}km/h velocidadeMax={maxSpeed:F1}km/h " +
                $"tempoAbaixo70={below70:F1}s({below70Pct:F1}%) recuperacoes={recoveries} respawns={respawns} " +
                $"drifts={drifts} entradaDriftMedia={meanDriftEntry:F1}km/h saidaDriftMedia={meanDriftExit:F1}km/h " +
                $"trocasDeFaixa={laneChanges} cedendoFaixa={yielding} " +
                $"checkpointArtificial={artificial} pontosPresos={stuckPoints}");

            if (firstLapSnapshot)
                BotFailureAnalyzer.EnsureInstance()?.LogWorst();
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f || seconds == float.MaxValue)
                return "--:--";

            int minutes = Mathf.FloorToInt(seconds / 60f);
            float sec = seconds - minutes * 60f;
            return $"{minutes:00}:{sec:00.0}";
        }

        private Pose ResolveSpawnPose(int spawnIndex)
        {
            RaceSpawnManager spawn = RaceSpawnManager.Instance;
            if (spawn != null)
                return spawn.GetSpawnPose(spawnIndex);

            // Fallback: grade simples atrás deste objeto.
            Vector3 pos = transform.position + transform.right * ((spawnIndex % 2) * 3.2f) - transform.forward * (spawnIndex * 2.2f);
            return new Pose(pos, transform.rotation);
        }

#if PARTYRACERS_ONLINE
        private void TrySpawnNetworked(GameObject go)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer)
                return;

            NetworkObject netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
            {
                // Server-owned: o servidor mantém autoridade e dirige o bot; replica para os clientes.
                netObj.Spawn(true);
            }
        }
#endif
    }
}
