using UnityEngine;

namespace PartyRacers.Networking
{
    // Marcador de papel do kart (local / remoto / bot) e dono lógico. É um MonoBehaviour puro
    // (NÃO um NetworkBehaviour) para não acoplar o prefab à rede ainda — o gameplay local continua
    // idêntico. Quando a camada online for habilitada (define PARTYRACERS_ONLINE), um componente de
    // sincronização separado (ver MULTIPLAYER_SETUP.md) lê estes papéis para configurar autoridade.
    [DisallowMultipleComponent]
    public class KartNetworkIdentity : MonoBehaviour
    {
        [SerializeField] private PlayerKind kind = PlayerKind.Local;
        [SerializeField] private string playerId;
        [SerializeField] private string displayName = "Player";

        public PlayerKind Kind => kind;
        public string PlayerId => playerId;
        public string DisplayName => displayName;

        public bool IsLocalControlled => kind == PlayerKind.Local;
        public bool IsBot => kind == PlayerKind.Bot;
        public bool IsRemote => kind == PlayerKind.Remote;

        public void Configure(RacePlayerInfo info)
        {
            if (info == null)
                return;

            kind = info.Kind;
            playerId = info.Id;
            displayName = info.DisplayName;
        }

        public void SetKind(PlayerKind newKind) => kind = newKind;

        // Permite que sistemas locais (ex.: bots offline) definam o nome exibido sem um RacePlayerInfo.
        public void SetDisplayName(string newName)
        {
            if (!string.IsNullOrEmpty(newName))
                displayName = newName;
        }

        public void SetPlayerId(string id) => playerId = id;
    }
}
