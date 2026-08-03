using UnityEngine;

#if PARTYRACERS_ONLINE
using Unity.Netcode;
#endif

namespace PartyRacers.Networking
{
    [CreateAssetMenu(menuName = "PartyRacers/Networking/Race Network Config")]
    public class RaceNetworkConfig : ScriptableObject
    {
        [Tooltip("Kart usado nas corridas ONLINE (tem NetworkObject).")]
        [SerializeField] private GameObject playerKartPrefab;

        [Tooltip("Kart usado nas corridas LOCAIS. Fica aqui, e não só na cena, porque em build não " +
                 "existe AssetDatabase: sem esta referência o jogo compilado abria a pista sem carro.")]
        [SerializeField] private GameObject localKartPrefab;

        [Tooltip("Árbitro da corrida online (ItemBox e chegada autoritativos). O servidor cria uma " +
                 "instância por corrida; precisa estar na lista de network prefabs.")]
        [SerializeField] private GameObject raceDirectorPrefab;

#if PARTYRACERS_ONLINE
        [SerializeField] private NetworkPrefabsList networkPrefabs;

        public NetworkPrefabsList NetworkPrefabs => networkPrefabs;
#endif

        public GameObject PlayerKartPrefab => playerKartPrefab;
        public GameObject LocalKartPrefab => localKartPrefab != null ? localKartPrefab : playerKartPrefab;
        public GameObject RaceDirectorPrefab => raceDirectorPrefab;
    }
}
