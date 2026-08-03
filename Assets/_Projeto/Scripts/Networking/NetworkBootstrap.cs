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
        public enum JoinFailureReason { None, InvalidCode, LobbyFull, ServicesUnavailable, Unknown }

        [SerializeField] private bool autoInitialize = true;

        [Header("Retorno")]
        [Tooltip("Cena de menu para onde o jogador volta quando a sessão online termina.")]
        [SerializeField] private string frontendSceneName = "Frontend";
        [Tooltip("Cenas que já SÃO menu — estando nelas, a queda de sessão não recarrega nada.")]
        [SerializeField] private string[] menuSceneNames = { "Frontend", "Boot", "Garage" };

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
        public bool IsBusy => operationInFlight;
        public JoinFailureReason LastJoinFailure { get; private set; } = JoinFailureReason.None;
        public bool ServicesReady
        {
            get
            {
#if PARTYRACERS_ONLINE
                return servicesReady;
#else
                return false;
#endif
            }
        }
        public bool IsRaceSceneReadyForCountdown
        {
            get
            {
#if PARTYRACERS_ONLINE
                return Mode != SessionMode.Host || !networkRaceSceneLoadInProgress;
#else
                return true;
#endif
            }
        }

        public event Action<string> StatusChanged;

        /// <summary>
        /// A sessão online acabou sem ter sido pedido por este jogador (o dono fechou a sala, a
        /// conexão caiu). Quem estiver numa pista precisa voltar ao menu.
        /// </summary>
        public event Action SessionEnded;

        private bool operationInFlight;
        private bool leaveRequestedLocally;

#if PARTYRACERS_ONLINE
        private const string RelayJoinCodeKey = "relayJoinCode";
        private const string DisplayNameKey = "displayName";
        private const string ReadyKey = "ready";
        private const string CarIndexKey = "carIndex";
        private const string ColorIndexKey = "colorIndex";
        private const string ElementDataKey = "elementData";

        private bool servicesReady;
        private Task servicesInitializationTask;
        private bool lobbyPollInFlight;
        private bool lobbyHeartbeatInFlight;
        private Lobby currentLobby;
        private Coroutine lobbyPollRoutine;
        private Coroutine lobbyHeartbeatRoutine;
        private NetworkManager networkManager;
        private bool networkManagerCallbacksSubscribed;
        private bool networkSceneEventsSubscribed;
        private bool networkRaceSceneLoadInProgress;
        private string networkRaceSceneLoadName = string.Empty;
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
            UnsubscribeNetworkManagerCallbacks();
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
            if (operationInFlight)
            {
                SetStatus("Já existe uma conexão em andamento.");
                return;
            }

            _ = HostGameAsync();
#else
            Mode = SessionMode.Host;
            SetStatus("Partida local iniciada (host simulado).");
#endif
        }

        public void JoinGame(string joinCode)
        {
#if PARTYRACERS_ONLINE
            if (operationInFlight)
            {
                SetStatus("Já existe uma conexão em andamento.");
                return;
            }

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
                if (operationInFlight)
                    return;

                operationInFlight = true;
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
                StartNetworkRaceScene(sceneName, requireEveryoneReady: true);
                return;
            }
#endif

            if (!string.IsNullOrWhiteSpace(sceneName))
                SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Recarrega a pista para todos os jogadores da sala (botão JOGAR NOVAMENTE). Diferente de
        /// <see cref="StartRaceScene"/>, não exige que todos marquem "pronto" de novo: quem acabou
        /// de correr junto já está na sala. Só o dono da sala pode disparar.
        /// </summary>
        public void RestartRaceScene(string sceneName)
        {
#if PARTYRACERS_ONLINE
            if (IsOnline)
            {
                StartNetworkRaceScene(sceneName, requireEveryoneReady: false);
                return;
            }
#endif

            if (!string.IsNullOrWhiteSpace(sceneName))
                SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Leva TODA a sala de volta ao frontend mantendo a sessão viva — é o "voltar ao lobby"
        /// depois de uma corrida, não um "sair". Antes este botão chamava LeaveGame e derrubava a
        /// sala inteira: o host voltava para um lobby vazio e os convidados eram expulsos.
        ///
        /// Só o dono da sala pode disparar; os convidados são levados pelo Netcode.
        /// Retorna false quando não havia sessão online (o chamador faz a troca de cena local).
        /// </summary>
        public bool ReturnEveryoneToLobby()
        {
#if PARTYRACERS_ONLINE
            if (!IsOnline)
                return false;

            if (Mode != SessionMode.Host)
            {
                SetStatus("Aguardando o dono da sala voltar para o lobby.");
                return true;
            }

            if (networkManager == null || !networkManager.IsListening || networkManager.SceneManager == null)
                return false;

            string destino = string.IsNullOrWhiteSpace(frontendSceneName) ? "Frontend" : frontendSceneName;
            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(destino, LoadSceneMode.Single);

            SetStatus(status == SceneEventProgressStatus.Started
                ? "Voltando ao lobby com a sala..."
                : $"Falha ao voltar ao lobby: {status}");

            return status == SceneEventProgressStatus.Started;
#else
            return false;
#endif
        }

        /// <summary>Prefab de kart online resolvido (usado pelo árbitro para criar os karts).</summary>
        public GameObject ResolveOnlineKartPrefab()
        {
#if PARTYRACERS_ONLINE
            return ResolvePlayerPrefab(ResolveNetworkConfig());
#else
            return null;
#endif
        }

        /// <summary>
        /// Republica a customização do jogador no lobby. Chamado quando ele confirma a estilização
        /// na garagem, para que os outros vejam o carro certo antes da largada.
        /// </summary>
        public void PublishLocalVisual()
        {
#if PARTYRACERS_ONLINE
            if (!IsOnline || operationInFlight)
                return;

            operationInFlight = true;
            _ = SetLocalReadyAsync(RacePlayerRegistry.Instance?.LocalPlayer?.IsReady ?? false);
#endif
        }

        public void LeaveGame()
        {
#if PARTYRACERS_ONLINE
            if (operationInFlight)
                return;

            // Marca que a saída partiu daqui: o callback de desconexão do Netcode não deve tratar
            // isto como queda de sessão e recarregar o frontend por cima de quem já está saindo.
            leaveRequestedLocally = true;
            operationInFlight = true;
            _ = LeaveGameRequestedAsync();
#else
            Mode = SessionMode.Offline;
            SetStatus("Local (offline)");
#endif
        }

        /// <summary>True quando a cena ativa já é um menu (não faz sentido "voltar" para lugar nenhum).</summary>
        public bool IsInMenuScene()
        {
            string atual = SceneManager.GetActiveScene().name;
            if (menuSceneNames == null)
                return false;

            foreach (string nome in menuSceneNames)
            {
                if (string.Equals(nome, atual, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public string FrontendSceneName => frontendSceneName;

        private void SetStatus(string status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }

#if PARTYRACERS_ONLINE
        private Task InitializeServicesAsync()
        {
            if (servicesReady)
                return Task.CompletedTask;

            if (servicesInitializationTask == null || servicesInitializationTask.IsCompleted)
                servicesInitializationTask = InitializeServicesCoreAsync();

            return servicesInitializationTask;
        }

        private async Task InitializeServicesCoreAsync()
        {
            try
            {
                SetStatus("Preparando os serviços online...");

                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                servicesReady = true;
                RacePlayerRegistry.Instance?.SetLocalPlayerIdentity(AuthenticationService.Instance.PlayerId, GetLocalDisplayName(), Mode == SessionMode.Host);
                SetStatus("Online pronto — crie uma sala ou entre com um código.");
            }
            catch (Exception e)
            {
                servicesReady = false;
                SetStatus($"Falha ao preparar o modo online: {e.Message}");
                Debug.LogException(e);
            }
        }

        private async Task HostGameAsync()
        {
            if (Mode == SessionMode.Host && currentLobby != null)
            {
                SetStatus($"Sala online ativa. Código: {CurrentJoinCode}");
                return;
            }

            operationInFlight = true;
            LastJoinFailure = JoinFailureReason.None;

            try
            {
                if (Mode != SessionMode.Offline)
                    await LeaveGameAsync();

                await EnsureServicesReadyAsync();
                EnsureNetworkManagerConfigured();

                SetStatus("Criando conexão segura pelo Relay...");
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(RaceConstants.MaxPlayers - 1);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                ConfigureRelay(allocation);

                SetStatus("Criando a sala online...");
                currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                    lobbyName,
                    RaceConstants.MaxPlayers,
                    new CreateLobbyOptions
                    {
                        IsPrivate = true,
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
                SetStatus($"Sala online criada. Código: {CurrentJoinCode}");
            }
            catch (Exception e)
            {
                await CleanupFailedHostAsync();
                SetStatus($"Falha ao criar a sala online: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                operationInFlight = false;
                StatusChanged?.Invoke(Status);
            }
        }

        private async Task JoinGameAsync(string joinCode)
        {
            joinCode = (joinCode ?? string.Empty).Trim().ToUpperInvariant();
            if (joinCode.Length != 6)
            {
                LastJoinFailure = JoinFailureReason.InvalidCode;
                SetStatus("Informe um código de sala válido.");
                return;
            }

            operationInFlight = true;
            LastJoinFailure = JoinFailureReason.None;

            try
            {
                if (Mode != SessionMode.Offline)
                    await LeaveGameAsync();

                await EnsureServicesReadyAsync();
                EnsureNetworkManagerConfigured();

                SetStatus($"Procurando a sala {joinCode}...");
                currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(
                    joinCode,
                    new JoinLobbyByCodeOptions
                    {
                        Player = BuildLobbyPlayer(isHost: false),
                    });

                if (!TryGetLobbyData(currentLobby, RelayJoinCodeKey, out string relayJoinCode))
                    throw new InvalidOperationException("A sala foi encontrada, mas a conexão Relay está ausente.");

                SetStatus("Conectando ao dono da sala pelo Relay...");
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                ConfigureRelay(joinAllocation);

                if (!networkManager.StartClient())
                    throw new InvalidOperationException("NetworkManager.StartClient retornou false.");

                Mode = SessionMode.Client;
                CurrentJoinCode = currentLobby.LobbyCode;
                SyncRegistryFromLobby(currentLobby);
                StartLobbyLoops(hostOwnsLobby: false);
                SetStatus($"Você entrou na sala {CurrentJoinCode}.");
            }
            catch (LobbyServiceException e)
            {
                LastJoinFailure = MapJoinFailure(e);
                await CleanupFailedJoinAsync();
                SetStatus(GetJoinFailureMessage(LastJoinFailure));
                Debug.LogWarning($"Falha esperada ao entrar no lobby ({e.Reason}): {e.Message}");
            }
            catch (Exception e)
            {
                LastJoinFailure = JoinFailureReason.Unknown;
                await CleanupFailedJoinAsync();
                SetStatus("Falha ao entrar na sala. Confira sua conexão e tente novamente.");
                Debug.LogException(e);
            }
            finally
            {
                operationInFlight = false;
                StatusChanged?.Invoke(Status);
            }
        }

        private static JoinFailureReason MapJoinFailure(LobbyServiceException exception)
        {
            switch (exception.Reason)
            {
                case LobbyExceptionReason.InvalidJoinCode:
                case LobbyExceptionReason.LobbyNotFound:
                case LobbyExceptionReason.EntityNotFound:
                    return JoinFailureReason.InvalidCode;

                case LobbyExceptionReason.LobbyFull:
                case LobbyExceptionReason.LobbyLocked:
                    return JoinFailureReason.LobbyFull;

                case LobbyExceptionReason.NetworkError:
                case LobbyExceptionReason.ServiceUnavailable:
                case LobbyExceptionReason.RateLimited:
                case LobbyExceptionReason.RequestTimeOut:
                case LobbyExceptionReason.GatewayTimeout:
                    return JoinFailureReason.ServicesUnavailable;

                default:
                    return JoinFailureReason.Unknown;
            }
        }

        private static string GetJoinFailureMessage(JoinFailureReason failure)
        {
            switch (failure)
            {
                case JoinFailureReason.InvalidCode:
                    return "Código inválido ou sala encerrada.";
                case JoinFailureReason.LobbyFull:
                    return "A sala está cheia ou não aceita novas entradas.";
                case JoinFailureReason.ServicesUnavailable:
                    return "Serviço online indisponível. Tente novamente em instantes.";
                default:
                    return "Não foi possível entrar na sala. Confira o código.";
            }
        }

        private async Task SetLocalReadyAsync(bool ready)
        {
            try
            {
                if (currentLobby == null || !servicesReady)
                    return;

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
                RacePlayerRegistry.Instance?.SetReady(AuthenticationService.Instance.PlayerId, !ready);
                SetStatus($"Falha ao atualizar seu estado de pronto: {e.Message}");
                Debug.LogException(e);
            }
            finally
            {
                operationInFlight = false;
                StatusChanged?.Invoke(Status);
            }
        }

        private void StartNetworkRaceScene(string sceneName, bool requireEveryoneReady)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return;

            if (Mode != SessionMode.Host)
            {
                SetStatus("Aguardando o dono da sala iniciar a corrida.");
                return;
            }

            if (networkManager == null || !networkManager.IsListening || networkManager.SceneManager == null)
            {
                SetStatus("NetworkManager ainda nao esta pronto para carregar a corrida.");
                return;
            }

            if (requireEveryoneReady &&
                (RacePlayerRegistry.Instance == null || !RacePlayerRegistry.Instance.AllReady()))
            {
                SetStatus("Todos os jogadores precisam estar prontos para iniciar.");
                return;
            }

            BeginNetworkRaceSceneLoad(sceneName);
            SceneEventProgressStatus status = networkManager.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                ResetNetworkRaceSceneLoadState();

            SetStatus(status == SceneEventProgressStatus.Started
                ? $"Carregando {sceneName} para todos os jogadores..."
                : $"Falha ao carregar cena online: {status}");
        }

        private async Task LeaveGameRequestedAsync()
        {
            try
            {
                await LeaveGameAsync();
            }
            finally
            {
                operationInFlight = false;
                StatusChanged?.Invoke(Status);
            }
        }

        private async Task LeaveGameAsync()
        {
            StopLobbyLoops();

            Lobby lobbyToLeave = currentLobby;
            bool wasHost = Mode == SessionMode.Host;
            currentLobby = null;
            CurrentJoinCode = string.Empty;
            Mode = SessionMode.Offline;
            ResetNetworkRaceSceneLoadState();
            UnsubscribeNetworkSceneEvents();

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
            LastJoinFailure = JoinFailureReason.None;
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

        // ------------------------------------------------------------------ Queda de sessão
        private void SubscribeNetworkManagerCallbacks()
        {
            if (networkManager == null || networkManagerCallbacksSubscribed)
                return;

            networkManager.OnClientStopped += OnNetworkClientStopped;
            networkManager.OnServerStopped += OnNetworkServerStopped;
            networkManagerCallbacksSubscribed = true;
        }

        private void UnsubscribeNetworkManagerCallbacks()
        {
            if (!networkManagerCallbacksSubscribed)
                return;

            if (networkManager != null)
            {
                networkManager.OnClientStopped -= OnNetworkClientStopped;
                networkManager.OnServerStopped -= OnNetworkServerStopped;
            }

            networkManagerCallbacksSubscribed = false;
        }

        private void OnNetworkClientStopped(bool wasHost)
        {
            if (wasHost)
                return;

            HandleSessionDropped();
        }

        private void OnNetworkServerStopped(bool wasHost) => HandleSessionDropped();

        /// <summary>
        /// O transporte caiu sem que este jogador tenha pedido para sair — tipicamente o dono da
        /// sala encerrou. Antes disso o cliente ficava preso na pista, com a corrida congelada e
        /// nenhum botão funcionando, porque o NGO já não deixava trocar de cena.
        /// </summary>
        private void HandleSessionDropped()
        {
            if (leaveRequestedLocally)
            {
                leaveRequestedLocally = false;
                return;
            }

            if (Mode == SessionMode.Offline)
                return;

            StopLobbyLoops();
            currentLobby = null;
            CurrentJoinCode = string.Empty;
            Mode = SessionMode.Offline;
            ResetNetworkRaceSceneLoadState();
            UnsubscribeNetworkSceneEvents();
            RacePlayerRegistry.Instance?.ResetToLocalPlayer();

            SetStatus("A sala foi encerrada pelo dono.");
            SessionEnded?.Invoke();

            if (!IsInMenuScene() && !string.IsNullOrWhiteSpace(frontendSceneName))
                SceneManager.LoadScene(frontendSceneName);
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

            SubscribeNetworkManagerCallbacks();

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

            // PlayerPrefab fica VAZIO de propósito: com ele preenchido o Netcode cria o kart no
            // instante em que o cliente conecta — ou seja, ainda no lobby. O carro nascia no meio do
            // menu, caía no vazio e depois era arrastado para a pista pela troca de cena.
            // Quem cria os karts agora é o RaceNetworkDirector, já dentro da cena de corrida e com
            // destroyWithScene ligado, para que voltar ao lobby limpe a grade.
            config.PlayerPrefab = null;

            NetworkPrefabsList prefabsList = resolvedConfig != null ? resolvedConfig.NetworkPrefabs : null;
            if (prefabsList != null && !config.Prefabs.NetworkPrefabsLists.Contains(prefabsList))
                config.Prefabs.NetworkPrefabsLists.Add(prefabsList);

            bool alreadyInConfiguredList = prefabsList != null && prefabsList.Contains(playerPrefab);
            if (!alreadyInConfiguredList && !config.Prefabs.Contains(playerPrefab))
                config.Prefabs.Add(new NetworkPrefab { Prefab = playerPrefab });
        }

        private void BeginNetworkRaceSceneLoad(string sceneName)
        {
            networkRaceSceneLoadInProgress = true;
            networkRaceSceneLoadName = sceneName;
            SubscribeNetworkSceneEvents();
        }

        private void ResetNetworkRaceSceneLoadState()
        {
            networkRaceSceneLoadInProgress = false;
            networkRaceSceneLoadName = string.Empty;
        }

        private void SubscribeNetworkSceneEvents()
        {
            if (networkSceneEventsSubscribed || networkManager == null || networkManager.SceneManager == null)
                return;

            networkManager.SceneManager.OnLoadEventCompleted += OnNetworkSceneLoadEventCompleted;
            networkSceneEventsSubscribed = true;
        }

        private void UnsubscribeNetworkSceneEvents()
        {
            if (!networkSceneEventsSubscribed)
                return;

            if (networkManager != null && networkManager.SceneManager != null)
                networkManager.SceneManager.OnLoadEventCompleted -= OnNetworkSceneLoadEventCompleted;

            networkSceneEventsSubscribed = false;
        }

        private void OnNetworkSceneLoadEventCompleted(
            string sceneName,
            LoadSceneMode loadSceneMode,
            List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            if (!networkRaceSceneLoadInProgress)
                return;

            if (!string.Equals(sceneName, networkRaceSceneLoadName, StringComparison.Ordinal))
                return;

            if (clientsTimedOut != null && clientsTimedOut.Count > 0)
                Debug.LogWarning($"Cena online {sceneName} completou com {clientsTimedOut.Count} cliente(s) em timeout.");

            ResetNetworkRaceSceneLoadState();
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
            ResetNetworkRaceSceneLoadState();
            UnsubscribeNetworkSceneEvents();

            if (networkManager != null && networkManager.IsListening)
                networkManager.Shutdown();

            RacePlayerRegistry.Instance?.ResetToLocalPlayer();
        }
#endif
    }
}
