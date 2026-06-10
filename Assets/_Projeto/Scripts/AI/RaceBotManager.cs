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
            new BotDifficultyProfile { label = "Tranquilo", throttleScale = 0.82f, corneringCaution = 0.6f, steerSharpness = 1.3f, lookAheadDistance = 12f, steerWander = 0.10f, mistakeChance = 0.06f },
            new BotDifficultyProfile { label = "Normal",    throttleScale = 0.92f, corneringCaution = 0.5f, steerSharpness = 1.5f, lookAheadDistance = 13f, steerWander = 0.07f, mistakeChance = 0.04f },
            new BotDifficultyProfile { label = "Veloz",     throttleScale = 1.0f,  corneringCaution = 0.4f, steerSharpness = 1.7f, lookAheadDistance = 15f, steerWander = 0.05f, mistakeChance = 0.02f }
        };

        [Header("Visual dos bots")]
        [Tooltip("Sorteia o modelo do carro de cada bot entre as variantes do KartVisualCustomizer (ex.: 15 rigs).")]
        [SerializeField] private bool randomizeBotCarModels = true;
        [Tooltip("Evita que bots usem o mesmo modelo escolhido pelo player na garagem.")]
        [SerializeField] private bool avoidPlayerCarModel = true;
        [Tooltip("Sorteia também as peças (para-choques, rodas, spoiler, piloto...).")]
        [SerializeField] private bool randomizeBotCarElements = true;
        [Tooltip("Restringe os índices de modelos usados pelos bots (vazio = todos).")]
        [SerializeField] private List<int> allowedBotCarIndices = new List<int>();

        [Header("Determinismo")]
        [Tooltip("Semente base da corrida (cor/perfil/nome derivam dela + índice do bot).")]
        [SerializeField] private int raceSeed = 1000;

        private readonly List<KartController> spawnedBots = new List<KartController>();
        private bool filled;

        public int MaxCompetitors => maxCompetitors;
        public IReadOnlyList<KartController> SpawnedBots => spawnedBots;

        private void Start()
        {
            if (fillOnStart)
                StartCoroutine(FillRoutine());
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

            // KartRespawn escuta a tecla R no Update (respawnaria todos). O bot chama Respawn() direto.
            DisableComponent<KartRespawn>(go);

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
