using UnityEngine;

#if PARTYRACERS_ONLINE
using Unity.Netcode;
#endif

namespace PartyRacers.Networking
{
    [CreateAssetMenu(menuName = "PartyRacers/Networking/Race Network Config")]
    public class RaceNetworkConfig : ScriptableObject
    {
        [SerializeField] private GameObject playerKartPrefab;

        [Tooltip("Árbitro da corrida online (ItemBox e chegada autoritativos). O servidor cria uma " +
                 "instância por corrida; precisa estar na lista de network prefabs.")]
        [SerializeField] private GameObject raceDirectorPrefab;

#if PARTYRACERS_ONLINE
        [SerializeField] private NetworkPrefabsList networkPrefabs;

        public NetworkPrefabsList NetworkPrefabs => networkPrefabs;
#endif

        public GameObject PlayerKartPrefab => playerKartPrefab;
        public GameObject RaceDirectorPrefab => raceDirectorPrefab;
    }
}
