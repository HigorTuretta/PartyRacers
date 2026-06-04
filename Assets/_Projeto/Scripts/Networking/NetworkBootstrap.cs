using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if PARTYRACERS_ONLINE
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

#if UNITY_EDITOR
using UnityEditor;
#endif
#endif

namespace PartyRacers.Networking
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        public enum SessionMode { Offline, Host, Client }

        [SerializeField] private bool autoInitialize = true;

#if PARTYRACERS_ONLINE
        [Header("Online")]
        [SerializeField] private RaceNetworkConfig networkConfig;
        [SerializeField] private string resourcesConfigName = "RaceNetworkConfig";
        [SerializeField] private string lobbyName = "PartyRacers";
        [SerializeField] private string relayConnectionType = "udp";
        [SerializeField] private float lobbyHeartbeatInterval = 15f;
        [SerializeField] private float lobbyPollInterval = 2f;
#endif

        public SessionMode Mode { get; private set; } = SessionMode.Offline;
        public string Status { get; private set; } = "Local (offline)";
        public string CurrentJoinCode { get; private set; } = string.Empty;
        public bool IsOnline => Mode != SessionMode.Offline;
        public bool HasJoinCode => !string.IsNullOrWhiteSpace(CurrentJoinCode);

        public event Action<string> StatusChanged;

#if PARTYRACERS_ONLINE
        private const string RelayJoinCodeKey = "relayJoinCode";
        private const string DisplayNameKey = "displayName";
        private const string ReadyKey = "ready";
        private const string CarIndexKey = "carIndex";
        private const string ColorIndexKey = "colorIndex";
        private const string ElementDataKey = "elementData";

        private bool servicesReady;
        private bool lobbyPollInFlight;
        private bool lobbyHeartbeatInFlight;
        private Lobby currentLobby;
        private Coroutine lobbyPollRoutine;
        private Coroutine lobbyHeartbeatRoutine;
        private NetworkManager networkManager;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (autoInitialize)
                Initialize();
        }

        private void OnDestroy()
        {
#if PARTYRACERS_ONLINE
            StopLobbyLoops();
#endif

            if (Instance == this)
                Instance = null;
        }

        public void Initialize()
        {
#if PARTYRACERS_ONLINE
            _ = InitializeServicesAsync();
#else
            SetStatus("Local (offline) - habilite PARTYRACERS_ONLINE e configure os Unity Services para jogar online.");
#endif
        }

        public void HostGame()
        {
#if PARTYRACERS_ONLINE
            _ = HostGameAsync();
#else
            Mode = SessionMode.Host;
            SetStatus("Partida local iniciada (host simulado).");
#endif
        }

        public void JoinGame(string joinCode)
        {
#if PARTYRACERS_ONLINE
            _ = JoinGameAsync(joinCode);
#else
            SetStatus("Entrar online requer PARTYRACERS_ONLINE.");
#endif
        }

        public void SetLocalReady(bool ready)
        {
#if PARTYRACERS_ONLINE
            if (IsOnline)
            {
                _ = SetLocalReadyAsync(ready);
                return;
            }
#endif

            RacePlayerRegistry.Instance?.SetReady(RacePlayerRegistry.Instance.LocalPlayer?.Id, ready);
        }

        public void StartRaceScene(string sceneName)
        {
#if PARTYRACERS_ONLINE
            if (IsOnline)
            {
                StartNetworkRaceScene(sceneName);
                return;
            }
#endif

            if (!string.IsNullOrWhiteSpace(sceneName))
                SceneManager.LoadScene(sceneName);
        }

        public void LeaveGame()
        {
#if PARTYRACERS_ONLINE
            _ = LeaveGameAsync();
#else
            Mode = SessionMode.Offline;
            SetStatus("Local (offline)");
#endif
        }

        private void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

#if PARTYRACERS_ONLINE
        private async Task InitializeServicesAsync()
        {
            try
            {
                SetStatus("Inicializando Unity Services...");

                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                servicesReady = true;
                RacePlayerRegistry.Instance?.SetLocalPlayerIdentity(AuthenticationService.Instance.PlayerId, GetLocalDisplayName(), Mode == SessionMode.Host);
                SetStatus($"Conectado aos Unity Services (id: {AuthenticationService.Instance.PlayerId}).");
            }
            catch (Exception e)
            {
                servicesReady = false;
                SetStatus($"Falha ao inicializar Unity Services: {e.Message}");
                Debug.LogException(e);
            }
        }

        private async Task HostGameAsync()
        {
            if (Mode == SessionMode.Host && currentLobby != null)
            {
                SetStatus($"Host online. Codigo: {CurrentJoinCode}");
                return;
            }

            if (Mode != SessionMode.Offline)
                await LeaveGameAsync();

            try
            {
                await EnsureServicesReadyAsync();
                EnsureNetworkManagerConfigured();

                SetStatus("Criando alocacao Relay...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(RaceConstants.MaxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                ConfigureRelay(allocation);

                SetStatus("Criando Lobby...");
                currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    lobbyName,
                    RaceConstants.MaxPlayers,
                    new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Player = BuildLobbyPlayer(isHost: true),
                        Data = new Dictionary<string, DataObject>
                        {
                            [RelayJoinCodeKey] = new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode),
                        }
                    });

                CurrentJoinCode = currentLobby.LobbyCode;

                if (!networkManager.StartHost())
                    throw new InvalidOperationException("NetworkManager.StartHost retornou false.");

                Mode = SessionMode.Host;
                SyncRegistryFromLobby(currentLobby);
                StartLobbyLoops(hostOwnsLobby: true);
                SetStatus($"Host online. Codigo: {CurrentJoinCode}");
            }
            catch (Exception e)
            {
                await CleanupFailedHostAsync();
                SetStatus($"Falha ao criar partida online: {e.Message}");
                Debug.LogException(e);
            }
        }

        private async Task JoinGameAsync(string joinCode)
        {
            joinCode = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(joinCode))
            {
                SetStatus("Informe um codigo de lobby para entrar.");
                return;
            }

            if (Mode != SessionMode.Offline)
                await LeaveGameAsync();

            try
            {
                await EnsureServicesReadyAsync();
                EnsureNetworkManagerConfigured();

                SetStatus($"Entrando no lobby {joinCode}...");
                currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                    joinCode,
                    new JoinLobbyByCodeOptions
                    {
                        Player = BuildLobbyPlayer(isHost: false),
                    });

                if (!TryGetLobbyData(currentLobby, RelayJoinCodeKey, out string relayJoinCode))
                    throw new InvalidOperationException("Lobby encontrado, mas sem codigo Relay.");

                SetStatus("Conectando ao Relay...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                ConfigureRelay(joinAllocation);

                if (!networkManager.StartClient())
                    throw new InvalidOperationException("NetworkManager.StartClient retornou false.");

                Mode = SessionMode.Client;
                CurrentJoinCode = currentLobby.LobbyCode;
                SyncRegistryFromLobby(currentLobby);
                StartLobbyLoops(hostOwnsLobby: false);
                SetStatus($"Cliente online no lobby {CurrentJoinCode}. Aguardando host iniciar.");
            }
            catch (Exception e)
            {
                await CleanupFailedJoinAsync();
                SetStatus($"Falha ao entrar no lobby: {e.Message}");
                Debug.LogException(e);
            }
        }

        private async Task SetLocalReadyAsync(bool ready)
        {
            if (currentLobby == null || !servicesReady)
                return;

            try
            {
                RacePlayerRegistry.Instance?.SetReady(AuthenticationService.Instance.PlayerId, ready);
                currentLobby = await LobbyService.Instance.UpdatePlayerAsync(
                    currentLobby.Id,
                    AuthenticationService.Instance.PlayerId,
                    new UpdatePlayerOptions
                    {
                        Data = BuildPlayerData(ready),
                    });

                SyncRegistryFromLobby(currentLobby);
            }
            catch (Exception e)
            {
                SetStatus($"Falha ao atualizar pronto: {e.Message}");
                Debug.LogException(e);
            }
        }

        private void StartNetworkRaceScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (Mode != SessionMode.Host)
            {
                SetStatus("Aguardando o host iniciar a corrida.");
                return;
            }

            if (networkManager == null || !networkManager.IsListening || networkManager.SceneManager == null)
            {
                SetStatus("NetworkManager ainda nao esta pronto para carregar a corrida.");
                return;
            }

            if (RacePlayerRegistry.Instance == null || !RacePlayerRegistry.Instance.AllReady())
            {
                SetStatus("Todos os jogadores precisam estar prontos para iniciar.");
                return;
            }

            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            SetStatus(status == SceneEventProgressStatus.Started
                ? $"Carregando {sceneName} para todos os jogadores..."
                : $"Falha ao carregar cena online: {status}");
        }

        private async Task LeaveGameAsync()
        {
            StopLobbyLoops();

            Lobby lobbyToLeave = currentLobby;
            bool wasHost = Mode == SessionMode.Host;
            currentLobby = null;
            CurrentJoinCode = string.Empty;
            Mode = SessionMode.Offline;

            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            if (lobbyToLeave != null && servicesReady)
            {
                try
                {
                    if (wasHost)
                        await LobbyService.Instance.DeleteLobbyAsync(lobbyToLeave.Id);
                    else if (AuthenticationService.Instance.IsSignedIn)
                        await LobbyService.Instance.RemovePlayerAsync(lobbyToLeave.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Falha ao sair/remover lobby: {e.Message}");
                }
            }

            RacePlayerRegistry.Instance?.ResetToLocalPlayer();
            SetStatus("Local (offline)");
            await Task.Yield();
        }

        private async Task EnsureServicesReadyAsync()
        {
            if (!servicesReady)
                await InitializeServicesAsync();

            if (!servicesReady)
                throw new InvalidOperationException("Unity Services nao inicializados.");
        }

        private void EnsureNetworkManagerConfigured()
        {
            networkManager = NetworkManager.Singleton;

            if (networkManager == null)
            {
                GameObject go = new GameObject("NetworkManager");
                DontDestroyOnLoad(go);
                networkManager = go.AddComponent<NetworkManager>();
                go.AddComponent<UnityTransport>();
            }

            if (networkManager.NetworkConfig == null)
                networkManager.NetworkConfig = new NetworkConfig();

            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
                transport = networkManager.gameObject.AddComponent<UnityTransport>();

            NetworkConfig config = networkManager.NetworkConfig;
            config.NetworkTransport = transport;
            config.NetworkTopology = NetworkTopologyTypes.ClientServer;
            config.EnableSceneManagement = true;
            config.ConnectionApproval = false;
            config.ForceSamePrefabs = false;
            config.TickRate = 30;

            RaceNetworkConfig resolvedConfig = ResolveNetworkConfig();
            GameObject playerPrefab = ResolvePlayerPrefab(resolvedConfig);
            if (playerPrefab == null)
                throw new InvalidOperationException("Prefab de kart online nao encontrado.");

            if (playerPrefab.GetComponent<NetworkObject>() == null)
                throw new InvalidOperationException($"{playerPrefab.name} precisa de NetworkObject.");

            config.PlayerPrefab = playerPrefab;

            NetworkPrefabsList prefabsList = resolvedConfig != null ? resolvedConfig.NetworkPrefabs : null;
            if (prefabsList != null && !config.Prefabs.NetworkPrefabsLists.Contains(prefabsList))
                config.Prefabs.NetworkPrefabsLists.Add(prefabsList);

            if (prefabsList == null || !prefabsList.Contains(playerPrefab))
                config.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
        }

        private RaceNetworkConfig ResolveNetworkConfig()
        {
            if (networkConfig != null)
                return networkConfig;

            if (!string.IsNullOrWhiteSpace(resourcesConfigName))
                networkConfig = Resources.Load<RaceNetworkConfig>(resourcesConfigName);

#if UNITY_EDITOR
            if (networkConfig == null)
                networkConfig = AssetDatabase.LoadAssetAtPath<RaceNetworkConfig>("Assets/_Projeto/Resources/RaceNetworkConfig.asset");
#endif

            return networkConfig;
        }

        private GameObject ResolvePlayerPrefab(RaceNetworkConfig resolvedConfig)
        {
            if (resolvedConfig != null && resolvedConfig.PlayerKartPrefab != null)
                return resolvedConfig.PlayerKartPrefab;

#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/Cars/PlayerKart_Network.prefab");
#else
            return null;
#endif
        }

        private void ConfigureRelay(Allocation allocation)
        {
            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.ToRelayServerData(relayConnectionType));
        }

        private void ConfigureRelay(JoinAllocation allocation)
        {
            UnityTransport transport = networkManager.GetComponent<UnityTransport>();
            transport.SetRelayServerData(allocation.ToRelayServerData(relayConnectionType));
        }

        private Player BuildLobbyPlayer(bool isHost)
        {
            return new Player(
                id: AuthenticationService.Instance.PlayerId,
                data: BuildPlayerData(null, isHost));
        }

        private Dictionary<string, PlayerDataObject> BuildPlayerData(bool? readyOverride, bool? hostOverride = null)
        {
            RefreshLocalPlayerVisual();
            RacePlayerInfo local = RacePlayerRegistry.Instance != null ? RacePlayerRegistry.Instance.LocalPlayer : null;
            bool ready = readyOverride ?? local?.IsReady ?? false;

            return new Dictionary<string, PlayerDataObject>
            {
                [DisplayNameKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, GetLocalDisplayName()),
                [ReadyKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, ready ? "1" : "0"),
                [CarIndexKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, (local?.CarIndex ?? 0).ToString()),
                [ColorIndexKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, (local?.ColorIndex ?? 0).ToString()),
                [ElementDataKey] = new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, local?.ElementData ?? string.Empty),
            };
        }

        private void RefreshLocalPlayerVisual()
        {
            RacePlayerRegistry.Instance?.SetLocalPlayerVisual(KartGarageSelection.Capture());
        }

        private string GetLocalDisplayName()
        {
            string name = RacePlayerRegistry.Instance != null && RacePlayerRegistry.Instance.LocalPlayer != null
                ? RacePlayerRegistry.Instance.LocalPlayer.DisplayName
                : string.Empty;

            if (!IsPlaceholderName(name))
                return name;

            string playerId = AuthenticationService.Instance != null && AuthenticationService.Instance.IsSignedIn
                ? AuthenticationService.Instance.PlayerId
                : string.Empty;

            if (string.IsNullOrWhiteSpace(playerId))
                return "Player";

            int suffixLength = Mathf.Min(4, playerId.Length);
            string suffix = playerId.Substring(playerId.Length - suffixLength, suffixLength).ToUpperInvariant();
            return "Player " + suffix;
        }

        private static bool IsPlaceholderName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;

            if (string.Equals(name, "Player", StringComparison.OrdinalIgnoreCase))
                return true;

            return name.Length <= 5 && name.StartsWith("Voc", StringComparison.OrdinalIgnoreCase);
        }

        private void SyncRegistryFromLobby(Lobby lobby)
        {
            if (lobby == null || RacePlayerRegistry.Instance == null)
                return;

            string localPlayerId = AuthenticationService.Instance.PlayerId;
            List<RacePlayerInfo> snapshot = new List<RacePlayerInfo>();

            if (lobby.Players != null)
            {
                foreach (Player player in lobby.Players)
                {
                    if (player == null || string.IsNullOrWhiteSpace(player.Id))
                        continue;

                    RacePlayerInfo info = new RacePlayerInfo(
                        player.Id,
                        GetPlayerData(player, DisplayNameKey, "Player"),
                        player.Id == localPlayerId ? PlayerKind.Local : PlayerKind.Remote)
                    {
                        IsHost = player.Id == lobby.HostId,
                        IsReady = GetPlayerData(player, ReadyKey, "0") == "1",
                        CarIndex = ParseInt(GetPlayerData(player, CarIndexKey, "0")),
                        ColorIndex = ParseInt(GetPlayerData(player, ColorIndexKey, "0")),
                        ElementData = GetPlayerData(player, ElementDataKey, string.Empty)
                    };

                    snapshot.Add(info);
                }
            }

            RacePlayerRegistry.Instance.ApplyNetworkSnapshot(snapshot, localPlayerId);
        }

        private static string GetPlayerData(Player player, string key, string fallback)
        {
            if (player.Data != null &&
                player.Data.TryGetValue(key, out PlayerDataObject data) &&
                !string.IsNullOrWhiteSpace(data.Value))
            {
                return data.Value;
            }

            return fallback;
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value, out int parsed) ? parsed : 0;
        }

        private static bool TryGetLobbyData(Lobby lobby, string key, out string value)
        {
            value = null;
            if (lobby?.Data == null || !lobby.Data.TryGetValue(key, out DataObject data))
                return false;

            value = data.Value;
            return !string.IsNullOrWhiteSpace(value);
        }

        private void StartLobbyLoops(bool hostOwnsLobby)
        {
            StopLobbyLoops();

            if (hostOwnsLobby)
                lobbyHeartbeatRoutine = StartCoroutine(LobbyHeartbeatRoutine());

            lobbyPollRoutine = StartCoroutine(LobbyPollRoutine());
        }

        private void StopLobbyLoops()
        {
            if (lobbyHeartbeatRoutine != null)
            {
                StopCoroutine(lobbyHeartbeatRoutine);
                lobbyHeartbeatRoutine = null;
            }

            if (lobbyPollRoutine != null)
            {
                StopCoroutine(lobbyPollRoutine);
                lobbyPollRoutine = null;
            }
        }

        private IEnumerator LobbyHeartbeatRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(5f, lobbyHeartbeatInterval));

            while (currentLobby != null)
            {
                if (!lobbyHeartbeatInFlight)
                    _ = SendLobbyHeartbeatAsync();

                yield return wait;
            }
        }

        private IEnumerator LobbyPollRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(1f, lobbyPollInterval));

            while (currentLobby != null)
            {
                if (!lobbyPollInFlight)
                    _ = PollLobbyAsync();

                yield return wait;
            }
        }

        private async Task SendLobbyHeartbeatAsync()
        {
            if (currentLobby == null)
                return;

            lobbyHeartbeatInFlight = true;

            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(currentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Falha no heartbeat do lobby: {e.Message}");
            }
            finally
            {
                lobbyHeartbeatInFlight = false;
            }
        }

        private async Task PollLobbyAsync()
        {
            if (currentLobby == null)
                return;

            lobbyPollInFlight = true;

            try
            {
                currentLobby = await LobbyService.Instance.GetLobbyAsync(currentLobby.Id);
                SyncRegistryFromLobby(currentLobby);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Falha ao atualizar lobby: {e.Message}");
            }
            finally
            {
                lobbyPollInFlight = false;
            }
        }

        private async Task CleanupFailedHostAsync()
        {
            if (currentLobby != null)
            {
                try
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Falha ao remover lobby apos erro: {e.Message}");
                }
            }

            CleanupNetworkAfterFailure();
        }

        private async Task CleanupFailedJoinAsync()
        {
            if (currentLobby != null && AuthenticationService.Instance.IsSignedIn)
            {
                try
                {
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Falha ao sair do lobby apos erro: {e.Message}");
                }
            }

            CleanupNetworkAfterFailure();
        }

        private void CleanupNetworkAfterFailure()
        {
            StopLobbyLoops();
            currentLobby = null;
            CurrentJoinCode = string.Empty;
            Mode = SessionMode.Offline;

            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            RacePlayerRegistry.Instance?.ResetToLocalPlayer();
        }
#endif
    }
}
