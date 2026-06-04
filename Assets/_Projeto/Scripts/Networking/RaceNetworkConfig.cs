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

#if PARTYRACERS_ONLINE
        [SerializeField] private NetworkPrefabsList networkPrefabs;

        public NetworkPrefabsList NetworkPrefabs => networkPrefabs;
#endif

        public GameObject PlayerKartPrefab => playerKartPrefab;
    }
}
