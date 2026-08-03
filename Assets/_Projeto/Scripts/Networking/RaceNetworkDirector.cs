using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace PartyRacers.Networking
{
    /// <summary>
    /// Árbitro da corrida online. O servidor cria uma única instância assim que a cena de pista
    /// carrega e o Netcode a replica para todos os clientes.
    ///
    /// Existe porque duas regras eram decididas por cada máquina isoladamente — e por isso
    /// divergiam entre os jogadores:
    ///
    ///  • <b>ItemBox</b>: quem consome a caixa, quando ela some e quando volta. Agora o servidor
    ///    decide e transmite; as caixas não precisam de um NetworkObject cada uma porque recebem
    ///    um índice estável, derivado de uma ordenação idêntica em todas as máquinas.
    ///
    ///  • <b>Fim de corrida</b>: quem cruzou a linha, em que ordem e com que tempo. O servidor vê
    ///    todos os karts (os remotos chegam replicados pelo NetworkTransform), então é ele quem
    ///    fecha a classificação e avisa cada cliente — que assim abre a tela de resultado mesmo
    ///    não sendo o dono da sala.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceNetworkDirector : NetworkBehaviour
    {
        public static RaceNetworkDirector Instance { get; private set; }

        [Tooltip("Com que frequência o servidor varre os karts em busca de quem terminou.")]
        [SerializeField, Min(0.02f)] private float finishPollInterval = 0.1f;

        // Largada anunciada pelo servidor. É NetworkVariable (e não só um RPC) para que um cliente
        // que termine de carregar a cena depois do anúncio ainda receba o estado ao sincronizar.
        private readonly NetworkVariable<bool> countdownAnnounced = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly List<ItemBox> orderedBoxes = new List<ItemBox>();
        private readonly HashSet<ulong> announcedFinishes = new HashSet<ulong>();
        private readonly Dictionary<ulong, NetworkObject> spawnedKarts = new Dictionary<ulong, NetworkObject>();
        private RaceManager raceManager;
        private float nextFinishPoll;
        private int nextRank = 1;

        /// <summary>O servidor já liberou a largada para todo mundo.</summary>
        public bool CountdownAnnounced => IsSpawned && countdownAnnounced.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            EnsureBoxIndex();

            if (!IsServer)
                return;

            SpawnKartsForConnectedClients();
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;

            if (Instance == this)
                Instance = null;
        }

        // ------------------------------------------------------------------ Karts dos jogadores

        /// <summary>
        /// Cria o kart de cada cliente conectado, aqui na pista. O Netcode não faz mais isso
        /// sozinho na conexão (ver NetworkBootstrap): assim nenhum carro aparece no menu e a grade
        /// é destruída junto com a cena quando a sala volta ao lobby.
        /// </summary>
        private void SpawnKartsForConnectedClients()
        {
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                SpawnKartFor(clientId);
        }

        private void OnClientConnected(ulong clientId) => SpawnKartFor(clientId);

        private void SpawnKartFor(ulong clientId)
        {
            if (!IsServer || spawnedKarts.ContainsKey(clientId))
                return;

            NetworkBootstrap bootstrap = NetworkBootstrap.Instance;
            GameObject prefab = bootstrap != null ? bootstrap.ResolveOnlineKartPrefab() : null;
            NetworkObject prefabNetObj = prefab != null ? prefab.GetComponent<NetworkObject>() : null;

            if (prefabNetObj == null)
            {
                Debug.LogError("[RaceNetworkDirector] Sem prefab de kart online — o jogador " + clientId +
                               " ficaria sem carro.");
                return;
            }

            Pose pose = ResolveSpawnPose((int)clientId);
            NetworkObject kart = NetworkManager.SpawnManager.InstantiateAndSpawn(
                prefabNetObj,
                clientId,
                destroyWithScene: true,
                isPlayerObject: true,
                forceOverride: false,
                position: pose.position,
                rotation: pose.rotation);

            if (kart != null)
                spawnedKarts[clientId] = kart;
        }

        private static Pose ResolveSpawnPose(int index)
        {
            RaceSpawnManager spawns = RaceSpawnManager.Instance;
            return spawns != null ? spawns.GetSpawnPose(index) : new Pose(Vector3.zero, Quaternion.identity);
        }

        /// <summary>Quantos clientes já têm o kart criado (usado pela espera da largada).</summary>
        public int SpawnedPlayerKartCount => spawnedKarts.Count;

        // ------------------------------------------------------------------ Largada

        /// <summary>Servidor: libera a contagem para todos. Idempotente.</summary>
        public void AnnounceCountdown()
        {
            if (IsServer && IsSpawned && !countdownAnnounced.Value)
                countdownAnnounced.Value = true;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
                return;

            if (Time.time < nextFinishPoll)
                return;

            nextFinishPoll = Time.time + finishPollInterval;
            PollFinishes();
        }

        // ------------------------------------------------------------------ ItemBox

        /// <summary>
        /// Ordena as caixas da cena por uma chave idêntica em todas as máquinas (posição + caminho
        /// na hierarquia) e distribui os índices. A cena é a mesma para todos, então a ordem também.
        /// </summary>
        private void EnsureBoxIndex()
        {
            if (orderedBoxes.Count == ItemBox.All.Count && orderedBoxes.Count > 0)
                return;

            orderedBoxes.Clear();
            foreach (ItemBox box in ItemBox.All)
            {
                if (box != null)
                    orderedBoxes.Add(box);
            }

            orderedBoxes.Sort(static (a, b) => string.CompareOrdinal(a.SortKey, b.SortKey));

            for (int i = 0; i < orderedBoxes.Count; i++)
                orderedBoxes[i].AssignNetworkIndex(i);
        }

        /// <summary>Chamado pela caixa no servidor logo após entregar o poder.</summary>
        public static void NotifyBoxConsumed(ItemBox box)
        {
            if (box == null || Instance == null || !Instance.IsSpawned || !Instance.IsServer)
                return;

            Instance.EnsureBoxIndex();

            if (box.NetworkIndex < 0)
                return;

            Instance.BoxConsumedClientRpc(box.NetworkIndex);
        }

        [ClientRpc]
        private void BoxConsumedClientRpc(int boxIndex)
        {
            // O host já consumiu a caixa localmente ao entregar o poder.
            if (IsServer)
                return;

            EnsureBoxIndex();

            if (boxIndex < 0 || boxIndex >= orderedBoxes.Count)
                return;

            ItemBox box = orderedBoxes[boxIndex];
            if (box != null)
                box.ConsumeFromNetwork();
        }

        // ------------------------------------------------------------------ Fim de corrida

        private void PollFinishes()
        {
            if (raceManager == null)
                raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);

            if (raceManager == null || raceManager.Karts == null)
                return;

            IReadOnlyList<KartController> karts = raceManager.Karts;
            for (int i = 0; i < karts.Count; i++)
            {
                KartController kart = karts[i];
                if (kart == null)
                    continue;

                KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
                if (tracker == null || !tracker.RaceFinished)
                    continue;

                NetworkObject netObj = kart.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsSpawned)
                    continue;

                if (!announcedFinishes.Add(netObj.NetworkObjectId))
                    continue;

                int rank = nextRank++;
                tracker.ApplyNetworkFinish(rank, tracker.TotalRaceTime);
                KartFinishedClientRpc(netObj.NetworkObjectId, rank, tracker.TotalRaceTime);
            }
        }

        [ClientRpc]
        private void KartFinishedClientRpc(ulong kartNetworkObjectId, int rank, float totalTime)
        {
            if (IsServer)
                return;

            NetworkObject netObj = RaceAuthority.FindSpawned(kartNetworkObjectId);
            if (netObj == null)
                return;

            KartRaceTracker tracker = netObj.GetComponent<KartRaceTracker>();
            if (tracker != null)
                tracker.ApplyNetworkFinish(rank, totalTime);
        }
    }
}
