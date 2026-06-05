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
#endif

    [Header("Estado")]
    [SerializeField] private bool raceStarted;

    private readonly List<KartController> karts = new List<KartController>();

    public bool RaceStarted => raceStarted;
    public IReadOnlyList<KartController> Karts => karts;

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

        if (placeOnSpawnPoints && ActiveSpawnManager != null)
            PlaceKartOnSpawn(kart, karts.Count - 1);

        // Karts que entram após a largada já largam liberados.
        kart.SetControlEnabled(raceStarted);
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

    private GameObject ResolvePlayerKartPrefab()
    {
        if (playerKartPrefab != null)
            return playerKartPrefab;

#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab");
#else
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
        NetworkObject networkObject = kart.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
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

    private void SetAllControl(bool enabled)
    {
        foreach (KartController kart in karts)
        {
            if (kart != null)
                kart.SetControlEnabled(enabled);
        }
    }

    private IEnumerator StartRaceRoutine()
    {
        CountdownUI.EnsureSceneInstance();

        raceStarted = false;
        SetAllControl(false);

#if PARTYRACERS_ONLINE
        yield return WaitForOnlinePlayersBeforeCountdown();
        SetAllControl(false);
#endif

        // Etapas da contagem — disparadas como evento; o CountdownUI (na cena) renderiza para todos.
        yield return RunCountdownStep(CountdownPhase.Three);
        yield return RunCountdownStep(CountdownPhase.Two);
        yield return RunCountdownStep(CountdownPhase.One);

        BroadcastPhase(CountdownPhase.Go);

        raceStarted = true;
        SetAllControl(true);

        yield return new WaitForSeconds(goMessageDuration);

        BroadcastHidden();
    }

    private IEnumerator RunCountdownStep(CountdownPhase phase)
    {
        BroadcastPhase(phase);
        yield return new WaitForSeconds(countdownStepDuration);
    }

#if PARTYRACERS_ONLINE
    private IEnumerator WaitForOnlinePlayersBeforeCountdown()
    {
        if (!ShouldWaitForOnlinePlayers())
            yield break;

        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.05f, onlinePlayerWaitPollInterval));

        while (!AllExpectedOnlineKartsSpawned())
        {
            CollectKarts();
            SetAllControl(false);
            UpdateOnlineWaitingText();
            yield return wait;
        }

        CollectKarts();
        SetAllControl(false);
    }

    private bool ShouldWaitForOnlinePlayers()
    {
        if (!waitForOnlinePlayersBeforeCountdown)
            return false;

        if (NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.IsOnline)
            return true;

        NetworkManager networkManager = NetworkManager.Singleton;
        return networkManager != null && networkManager.IsListening;
    }

    private bool AllExpectedOnlineKartsSpawned()
    {
        if (NetworkBootstrap.Instance != null && !NetworkBootstrap.Instance.IsRaceSceneReadyForCountdown)
            return false;

        int expectedPlayers = ResolveExpectedOnlinePlayerCount();
        int spawnedKarts = CountSpawnedOnlineKarts();
        return spawnedKarts >= expectedPlayers;
    }

    private int ResolveExpectedOnlinePlayerCount()
    {
        int expectedPlayers = 1;

        RacePlayerRegistry registry = RacePlayerRegistry.Instance;
        if (registry != null && registry.Count > 0)
            expectedPlayers = registry.Count;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsServer)
            expectedPlayers = Mathf.Max(expectedPlayers, networkManager.ConnectedClientsIds.Count);

        return Mathf.Clamp(expectedPlayers, 1, RaceConstants.MaxPlayers);
    }

    private int CountSpawnedOnlineKarts()
    {
        int count = 0;
        KartNetworkSync[] networkKarts = FindObjectsByType<KartNetworkSync>(FindObjectsInactive.Exclude);

        foreach (KartNetworkSync networkKart in networkKarts)
        {
            if (networkKart != null && networkKart.IsSpawned)
                count++;
        }

        return count;
    }

    private void UpdateOnlineWaitingText()
    {
        int expectedPlayers = ResolveExpectedOnlinePlayerCount();
        int spawnedKarts = CountSpawnedOnlineKarts();
        string message = $"{onlineWaitingText} {Mathf.Min(spawnedKarts, expectedPlayers)}/{expectedPlayers}";

        RaiseCountdownMessage(message);
    }
#endif
}
