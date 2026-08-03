using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PartyRacers.Networking;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if PARTYRACERS_ONLINE
using Unity.Netcode;
#endif

// Gerencia a largada da corrida. Generalizado de 1 para até 16 karts: a contagem regressiva
// trava/destrava TODOS os karts participantes. Continua funcionando em single-player local
// (basta o playerKart) e já está pronto para múltiplos jogadores/bots.
public class RaceManager : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("Kart principal (jogador local). Opcional — karts também são descobertos na cena.")]
    [SerializeField] private KartController playerKart;
    [Tooltip("Prefab local usado quando a cena de corrida não possui um PlayerKart já posicionado.")]
    [SerializeField] private GameObject playerKartPrefab;

    [Header("Descoberta de karts")]
    [Tooltip("Instancia o kart local automaticamente se nenhum KartController existir na cena.")]
    [SerializeField] private bool spawnPlayerKartIfMissing = true;
    [Tooltip("Inclui automaticamente todos os KartControllers da cena (bots/remotos/16 jogadores).")]
    [SerializeField] private bool autoCollectKarts = true;

    [Header("Largada")]
    [SerializeField] private RaceSpawnManager spawnManager;
    [Tooltip("Posiciona os karts nos pontos do RaceSpawnManager (se houver) ao iniciar.")]
    [SerializeField] private bool placeOnSpawnPoints = false;

    [Header("Configuração")]
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float goMessageDuration = 0.75f;
    [Tooltip("Garante que esta cena dirija a contagem local. Em offline fica sempre ativo.")]
    [SerializeField] private bool driveOwnCountdown = true;

#if PARTYRACERS_ONLINE
    [Header("Online")]
    [SerializeField] private bool waitForOnlinePlayersBeforeCountdown = true;
    [SerializeField] private float onlinePlayerWaitPollInterval = 0.1f;
    [SerializeField] private string onlineWaitingText = "AGUARDANDO";
    [Tooltip("Tempo máximo esperando os jogadores entrarem na pista (rede travada) antes de largar.")]
    [SerializeField, Min(2f)] private float onlineWaitTimeout = 30f;
    [Tooltip("Prefab do árbitro de rede. Se vazio, é lido do RaceNetworkConfig em Resources.")]
    [SerializeField] private GameObject raceDirectorPrefab;
#endif

    [Header("Bots")]
    [Tooltip("Tempo máximo esperando os bots entrarem no grid antes de largar assim mesmo.")]
    [SerializeField, Min(0.5f)] private float botFillTimeout = 5f;

    [Header("Estado")]
    [SerializeField] private bool raceStarted;

    private readonly List<KartController> karts = new List<KartController>();

    public bool RaceStarted => raceStarted;
    public IReadOnlyList<KartController> Karts => karts;

    /// <summary>
    /// True quando todos os jogadores esperados já estão na pista. É o sinal para o RaceBotManager
    /// completar as vagas: antes disso a contagem de players reais ainda está incompleta e os bots
    /// eram criados a mais (o grid online ficava com mais de 16 competidores).
    /// </summary>
    public bool GridReady { get; private set; }

    // ---------------------------------------------------------------------
    // Eventos de contagem regressiva (desacoplados do display).
    // Qualquer widget de UI (CountdownUI) assina e renderiza. Isso evita a dependência frágil
    // de SetCountdownText, que em multiplayer apontava para o texto de um HUD remoto desligado.
    // Cada cliente roda seu próprio RaceManager na cena de corrida, então a contagem propaga a todos.
    // ---------------------------------------------------------------------
    public enum CountdownPhase { Idle, Three, Two, One, Go }

    /// <summary>Etapa atual da contagem (3/2/1/VAI). Disparado em todos os clientes.</summary>
    public static event System.Action<CountdownPhase> CountdownPhaseChanged;

    /// <summary>Mensagem livre antes da contagem (ex.: "AGUARDANDO 2/4" no online).</summary>
    public static event System.Action<string> CountdownMessageChanged;

    /// <summary>Contagem encerrada — esconder o display.</summary>
    public static event System.Action CountdownHidden;

    private void BroadcastPhase(CountdownPhase phase) => CountdownPhaseChanged?.Invoke(phase);
    private void RaiseCountdownMessage(string message) => CountdownMessageChanged?.Invoke(message);
    private void BroadcastHidden() => CountdownHidden?.Invoke();

    public void RegisterKart(KartController kart)
    {
        if (kart == null || karts.Contains(kart))
            return;

        karts.Add(kart);
        ConfigureKartRaceTracker(kart);

        if (placeOnSpawnPoints && ActiveSpawnManager != null)
            PlaceKartOnSpawn(kart, karts.Count - 1);

        // Karts que entram após a largada já largam liberados.
        // O grid bloqueia a física, mas mantém a leitura local para o burnout de pré-largada.
        kart.SetControlEnabled(true);
        kart.SetStartGridLocked(!raceStarted);
    }

    public void UnregisterKart(KartController kart)
    {
        karts.Remove(kart);
    }

    private void Start()
    {
        CollectKarts();

        if (ShouldDriveCountdown())
            StartCoroutine(StartRaceRoutine());
    }

    private bool ShouldDriveCountdown()
    {
#if PARTYRACERS_ONLINE
        if (NetworkBootstrap.Instance == null || !NetworkBootstrap.Instance.IsOnline)
            return true;

        return driveOwnCountdown;
#else
        _ = driveOwnCountdown;
        return true;
#endif
    }

    private void CollectKarts()
    {
        karts.Clear();

        if (playerKart != null && !ShouldIgnoreKartForOnline(playerKart))
            karts.Add(playerKart);

        if (autoCollectKarts)
        {
            KartController[] found = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
            foreach (KartController kart in found)
            {
                if (kart != null && !karts.Contains(kart) && !ShouldIgnoreKartForOnline(kart))
                    karts.Add(kart);
            }
        }

        if (karts.Count == 0)
        {
            KartController spawnedKart = TrySpawnPlayerKart();
            if (spawnedKart != null)
                karts.Add(spawnedKart);
        }

        ConfigureRaceTrackers();

        if (placeOnSpawnPoints && ActiveSpawnManager != null)
            PlaceKartsOnSpawns();
    }

    private KartController TrySpawnPlayerKart()
    {
        if (!spawnPlayerKartIfMissing || !ShouldSpawnLocalPlayerKart())
            return null;

        GameObject prefab = ResolvePlayerKartPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("RaceManager nao encontrou um prefab de PlayerKart para instanciar.");
            return null;
        }

        Pose pose = ResolveInitialSpawnPose(0);
        GameObject instance = Instantiate(prefab, pose.position, pose.rotation);
        instance.name = prefab.name;

        KartController kart = instance.GetComponent<KartController>();
        if (kart == null)
        {
            Debug.LogWarning($"{prefab.name} foi instanciado, mas nao possui KartController.");
            return null;
        }

        playerKart = kart;
        return kart;
    }

    private bool ShouldSpawnLocalPlayerKart()
    {
#if PARTYRACERS_ONLINE
        if (NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.IsOnline)
            return false;
#endif

        return true;
    }

    /// <summary>
    /// Resolve o prefab do kart local. A ordem importa: campo do Inspector → RaceNetworkConfig em
    /// Resources → AssetDatabase (só no Editor).
    ///
    /// O caminho por AssetDatabase NÃO existe em build. Como as cenas de pista estão com
    /// 'playerKartPrefab' vazio, no jogo compilado nenhum kart era instanciado — e sem kart não há
    /// rig local, ou seja, a câmera nunca ia para trás do carro. Só funcionava dentro do Editor.
    /// </summary>
    private GameObject ResolvePlayerKartPrefab()
    {
        if (playerKartPrefab != null)
            return playerKartPrefab;

        RaceNetworkConfig config = Resources.Load<RaceNetworkConfig>("RaceNetworkConfig");
        if (config != null && config.LocalKartPrefab != null)
        {
            playerKartPrefab = config.LocalKartPrefab;
            return playerKartPrefab;
        }

#if UNITY_EDITOR
        playerKartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab");
        if (playerKartPrefab != null)
        {
            Debug.LogWarning("[RaceManager] 'playerKartPrefab' vazio: resolvido pelo AssetDatabase, que " +
                             "NÃO existe em build. Preencha o campo na cena ou o RaceNetworkConfig.", this);
        }

        return playerKartPrefab;
#else
        Debug.LogError("[RaceManager] Sem prefab de kart local: a corrida vai abrir sem carro e sem câmera. " +
                       "Preencha 'playerKartPrefab' na cena ou 'localKartPrefab' no RaceNetworkConfig.", this);
        return null;
#endif
    }

    private Pose ResolveInitialSpawnPose(int spawnIndex)
    {
        RaceSpawnManager resolvedSpawnManager = ActiveSpawnManager;
        if (resolvedSpawnManager != null)
            return resolvedSpawnManager.GetSpawnPose(spawnIndex);

        return new Pose(transform.position, transform.rotation);
    }

    private bool ShouldIgnoreKartForOnline(KartController kart)
    {
#if PARTYRACERS_ONLINE
        if (kart == null || NetworkBootstrap.Instance == null || !NetworkBootstrap.Instance.IsOnline)
            return false;

        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            return false;

        // Kart de rede que ainda não terminou de spawnar: apenas IGNORA nesta passagem. Ele se
        // registra sozinho no OnNetworkSpawn.
        //
        // Antes ele era DESATIVADO aqui — e como CollectKarts só varre objetos ativos, nunca mais
        // era encontrado nem reativado. O kart do jogador podia sumir de vez, e com ele a
        // CinemachineCamera que vive dentro do LocalPlayerRig: a câmera ficava parada no lugar em
        // vez de seguir o carro.
        if (networkObject != null)
            return true;

        // Sem NetworkObject nenhum numa corrida online é sobra de cena (kart posicionado à mão).
        // Esse sim pode ser escondido.
        kart.gameObject.SetActive(false);
        return true;
#else
        return false;
#endif
    }

    private void PlaceKartsOnSpawns()
    {
        for (int i = 0; i < karts.Count; i++)
            PlaceKartOnSpawn(karts[i], i);
    }

    private void PlaceKartOnSpawn(KartController kart, int fallbackIndex)
    {
        if (kart == null || !ShouldPlaceKartLocally(kart))
            return;

        RaceSpawnManager resolvedSpawnManager = ActiveSpawnManager;
        if (resolvedSpawnManager == null)
            return;

        Pose pose = resolvedSpawnManager.GetSpawnPose(ResolveSpawnIndex(kart, fallbackIndex));

        Rigidbody body = kart.Rigidbody;
        if (body != null)
        {
            body.position = pose.position;
            body.rotation = pose.rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        kart.transform.SetPositionAndRotation(pose.position, pose.rotation);
    }

    private int ResolveSpawnIndex(KartController kart, int fallbackIndex)
    {
#if PARTYRACERS_ONLINE
        // Só jogadores ganham a vaga pelo id do dono (assim cada cliente se coloca na mesma casa
        // do grid em todas as máquinas). Bots são TODOS do servidor: usar o id do dono neles
        // empilharia os quinze na primeira vaga.
        KartNetworkSync sync = kart.GetComponent<KartNetworkSync>();
        bool isBot = sync != null && sync.IsBot;

        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        if (!isBot && networkObject != null && networkObject.IsSpawned)
            return Mathf.Clamp((int)networkObject.OwnerClientId, 0, RaceConstants.MaxPlayers - 1);
#endif

        return fallbackIndex;
    }

    private bool ShouldPlaceKartLocally(KartController kart)
    {
#if PARTYRACERS_ONLINE
        if (NetworkBootstrap.Instance == null || !NetworkBootstrap.Instance.IsOnline)
            return true;

        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        return networkObject == null || !networkObject.IsSpawned || networkObject.IsOwner;
#else
        return true;
#endif
    }

    private RaceSpawnManager ActiveSpawnManager
    {
        get
        {
            if (spawnManager != null)
                return spawnManager;

            return RaceSpawnManager.Instance;
        }
    }

    private void ConfigureRaceTrackers()
    {
        for (int i = 0; i < karts.Count; i++)
            ConfigureKartRaceTracker(karts[i]);
    }

    private static void ConfigureKartRaceTracker(KartController kart)
    {
        if (kart == null)
            return;

        KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
        if (tracker != null)
            tracker.ConfigureCheckpointCount(KartRaceTracker.DetectSceneCheckpointCount());
    }

    private void SetAllControl(bool enabled)
    {
        foreach (KartController kart in karts)
        {
            if (kart != null)
                kart.SetControlEnabled(enabled);
        }
    }

    private void SetAllStartGridLocked(bool locked)
    {
        foreach (KartController kart in karts)
        {
            if (kart != null)
                kart.SetStartGridLocked(locked);
        }
    }

    // Arma o cronômetro de volta de todos os karts no "VAI!". A contagem em si só inicia
    // quando cada kart cruza a linha de largada/chegada (ver KartRaceTracker).
    private void StartLapTimers()
    {
        foreach (KartController kart in karts)
        {
            if (kart == null)
                continue;

            KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
            if (tracker != null)
                tracker.NotifyRaceStarted();
        }
    }

    private IEnumerator StartRaceRoutine()
    {
        raceStarted = false;
        SetAllControl(true);
        SetAllStartGridLocked(true);

#if PARTYRACERS_ONLINE
        if (IsOnlineRace())
        {
            // O servidor cria o árbitro; o cliente espera a réplica chegar.
            EnsureNetworkDirector();
            yield return WaitForNetworkDirector();

            if (RaceAuthority.IsServer)
            {
                // Só o servidor sabe quem já carregou a cena e quem já tem kart. Antes cada máquina
                // decidia sozinha quando largar, olhando uma contagem de lobby que ainda estava
                // defasada — e a corrida começava com gente fora da pista.
                yield return WaitForEveryPlayerOnTrack();
                GridReady = true;
                yield return WaitForBotsToFill();
                RaceNetworkDirector.Instance?.AnnounceCountdown();
            }
            else
            {
                yield return WaitForCountdownAnnouncement();
                GridReady = true;
            }
        }
        else
        {
            GridReady = true;
            yield return WaitForBotsToFill();
        }
#else
        GridReady = true;
        yield return WaitForBotsToFill();
#endif

        // Bots e jogadores que entraram durante a espera já se registraram sozinhos (RegisterKart);
        // aqui só garantimos que ninguém ficou solto antes do "VAI".
        SetAllControl(true);
        SetAllStartGridLocked(true);

        // Etapas da contagem — disparadas como evento; o CountdownUI (na cena) renderiza para todos.
        yield return RunCountdownStep(CountdownPhase.Three);
        yield return RunCountdownStep(CountdownPhase.Two);
        yield return RunCountdownStep(CountdownPhase.One);

        BroadcastPhase(CountdownPhase.Go);

        raceStarted = true;
        SetAllStartGridLocked(false);
        SetAllControl(true);
        StartLapTimers();

        yield return new WaitForSeconds(goMessageDuration);

        BroadcastHidden();
    }

    private IEnumerator RunCountdownStep(CountdownPhase phase)
    {
        BroadcastPhase(phase);
        yield return new WaitForSeconds(countdownStepDuration);
    }

    /// <summary>Segura a contagem até os bots entrarem, para todo mundo largar com o grid cheio.</summary>
    private IEnumerator WaitForBotsToFill()
    {
        PartyRacers.AI.RaceBotManager botManager = FindAnyObjectByType<PartyRacers.AI.RaceBotManager>(FindObjectsInactive.Exclude);
        if (botManager == null || !botManager.WillFillBots)
            yield break;

        float limite = Time.time + Mathf.Max(0.5f, botFillTimeout);
        while (!botManager.Filled && Time.time < limite)
            yield return null;
    }

#if PARTYRACERS_ONLINE
    /// <summary>
    /// Cria o árbitro da corrida (ItemBox e fim de corrida autoritativos). Só o servidor cria; o
    /// Netcode replica a instância para todos os clientes.
    /// </summary>
    private void EnsureNetworkDirector()
    {
        if (!RaceAuthority.IsServer || RaceNetworkDirector.Instance != null)
            return;

        GameObject prefab = ResolveDirectorPrefab();
        if (prefab == null)
        {
            Debug.LogWarning("[RaceManager] Prefab do RaceNetworkDirector nao encontrado — a corrida " +
                             "online vai rodar sem arbitro (itens e chegada podem divergir).");
            return;
        }

        GameObject instance = Instantiate(prefab);
        NetworkObject netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogWarning("[RaceManager] Prefab do RaceNetworkDirector sem NetworkObject.");
            Destroy(instance);
            return;
        }

        netObj.Spawn(destroyWithScene: true);
    }

    private GameObject ResolveDirectorPrefab()
    {
        if (raceDirectorPrefab != null)
            return raceDirectorPrefab;

        RaceNetworkConfig config = Resources.Load<RaceNetworkConfig>("RaceNetworkConfig");
        raceDirectorPrefab = config != null ? config.RaceDirectorPrefab : null;
        return raceDirectorPrefab;
    }

    private bool IsOnlineRace()
    {
        return NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.IsOnline && RaceAuthority.IsNetworked;
    }

    /// <summary>O cliente não pode largar antes de o árbitro existir aqui — é ele que traz o sinal.</summary>
    private IEnumerator WaitForNetworkDirector()
    {
        float limite = Time.time + Mathf.Max(1f, onlineWaitTimeout);
        while (RaceNetworkDirector.Instance == null && Time.time < limite)
        {
            RaiseCountdownMessage(onlineWaitingText);
            yield return null;
        }
    }

    /// <summary>
    /// Servidor: segura a largada até (a) a cena ter terminado de carregar em TODOS os clientes e
    /// (b) cada um deles já ter o seu kart criado na pista.
    /// </summary>
    private IEnumerator WaitForEveryPlayerOnTrack()
    {
        if (!waitForOnlinePlayersBeforeCountdown)
            yield break;

        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, onlinePlayerWaitPollInterval));
        float limite = Time.time + Mathf.Max(1f, onlineWaitTimeout);

        while (Time.time < limite)
        {
            int esperados = Mathf.Clamp(NetworkManager.Singleton.ConnectedClientsIds.Count, 1, RaceConstants.MaxPlayers);
            int presentes = ContarKartsDeJogadoresProntos();

            bool cenaPronta = NetworkBootstrap.Instance == null
                              || NetworkBootstrap.Instance.IsRaceSceneReadyForCountdown;

            RaiseCountdownMessage($"{onlineWaitingText} {Mathf.Min(presentes, esperados)}/{esperados}");

            if (cenaPronta && presentes >= esperados)
                yield break;

            SetAllControl(true);
            SetAllStartGridLocked(true);
            yield return wait;
        }

        Debug.LogWarning("[RaceManager] Tempo esgotado esperando os jogadores entrarem na pista — largando assim mesmo.");
    }

    /// <summary>
    /// Conta os karts de JOGADORES já ativos na pista. Bots ficam de fora de propósito: eles também
    /// têm KartNetworkSync, e contá-los faria a espera passar com a pista ainda vazia de gente.
    /// </summary>
    private int ContarKartsDeJogadoresProntos()
    {
        int count = 0;
        KartNetworkSync[] networkKarts = FindObjectsByType<KartNetworkSync>(FindObjectsInactive.Exclude);

        foreach (KartNetworkSync networkKart in networkKarts)
        {
            if (networkKart != null && networkKart.IsSpawned && !networkKart.IsBot)
                count++;
        }

        return count;
    }

    /// <summary>Cliente: espera o servidor liberar a largada. Não decide nada sozinho.</summary>
    private IEnumerator WaitForCountdownAnnouncement()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, onlinePlayerWaitPollInterval));
        float limite = Time.time + Mathf.Max(1f, onlineWaitTimeout);

        while (Time.time < limite)
        {
            RaceNetworkDirector director = RaceNetworkDirector.Instance;
            if (director != null && director.CountdownAnnounced)
                yield break;

            RaiseCountdownMessage(onlineWaitingText);
            SetAllControl(true);
            SetAllStartGridLocked(true);
            yield return wait;
        }

        Debug.LogWarning("[RaceManager] Tempo esgotado esperando a largada do servidor — largando assim mesmo.");
    }
#endif
}
