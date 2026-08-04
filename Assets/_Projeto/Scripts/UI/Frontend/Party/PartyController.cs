using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Dono do <see cref="PartyState"/> e da lista de amigos. É o ponto único onde a camada de
    /// rede vai plugar: hoje o grupo é local e os amigos são de exemplo, mas nem a tela nem o
    /// matchmaking sabem disso — os dois falam só com este controlador.
    ///
    /// Fica na cena do Frontend, ao lado do ScreenRouter, pelo mesmo motivo que o FrontendFlow:
    /// é o lugar onde as decisões de fluxo moram, e não dentro das telas.
    /// </summary>
    [DisallowMultipleComponent]
    public class PartyController : MonoBehaviour
    {
        [Header("Jogador local")]
        [SerializeField] private string nomeLocal = "HIGOR";
        [SerializeField] private int nivelLocal = 42;

        [Header("Serviços")]
        [SerializeField] private MatchmakingService matchmaking;
        [Tooltip("Quem sabe carregar a pista. O matchmaking sorteia o mapa e entrega aqui.")]
        [SerializeField] private FrontendFlow fluxo;

        [Header("Amigos de exemplo (até a lista real existir)")]
        [Tooltip("Nomes da aba NO JOGO.")]
        [SerializeField] private string[] amigosNoJogo = { "BIANCA", "RAFA", "DUDU", "LEO_99", "MARINA" };
        [Tooltip("Nomes da aba STEAM.")]
        [SerializeField] private string[] amigosSteam = { "TIAGO", "PEDRO_H", "KIKA" };

        public PartyState Party { get; private set; }
        public MatchmakingService Matchmaking => matchmaking;

        private readonly List<FriendEntry> noJogo = new List<FriendEntry>();
        private readonly List<FriendEntry> steam = new List<FriendEntry>();

        private void Awake()
        {
            Party = new PartyState();
            Party.EnsureLocal(nomeLocal, nivelLocal);

            MontarAmigosDeExemplo();

            if (matchmaking == null)
                matchmaking = GetComponent<MatchmakingService>();

            if (fluxo == null)
                fluxo = FindAnyObjectByType<FrontendFlow>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            if (matchmaking != null)
                matchmaking.MatchReady += CarregarMapa;
        }

        private void OnDisable()
        {
            if (matchmaking != null)
                matchmaking.MatchReady -= CarregarMapa;
        }

        private void CarregarMapa(string cena)
        {
            if (fluxo != null)
                fluxo.CorrerEm(cena);
        }

        // ---------------------------------------------------------------- Grupo

        public void EscolherModo(PartyMode modo) => Party?.SetMode(modo);

        public void IniciarBusca()
        {
            if (Party == null || !Party.CanSearch || matchmaking == null)
                return;

            matchmaking.Iniciar(Party);
        }

        public void CancelarBusca() => matchmaking?.Cancelar();

        // ---------------------------------------------------------------- Amigos

        public IReadOnlyList<FriendEntry> AmigosDe(FriendSource fonte)
            => fonte == FriendSource.Steam ? steam : noJogo;

        /// <summary>Convida um amigo. Ele entra como vaga ocupada em estado AGUARDANDO.</summary>
        public bool Convidar(FriendEntry amigo)
        {
            if (amigo == null || Party == null || !amigo.CanInvite)
                return false;

            bool entrou = Party.TryAdd(new PartyMember
            {
                Id = amigo.Id,
                DisplayName = amigo.DisplayName,
                Level = Random.Range(5, 60),
                PingMs = Random.Range(18, 90),
                IsLeader = false,
                IsLocal = false,
                State = MemberState.Invited,
            });

            if (!entrou)
                return false;

            // O amigo passa a aparecer como "NO GRUPO" na lista, sem botão de convite: convidar
            // duas vezes a mesma pessoa é o erro mais fácil de cometer numa lista longa.
            amigo.Presence = FriendPresence.InThisParty;
            Party.NotifyChanged();
            return true;
        }

        public void Remover(FriendEntry amigo)
        {
            if (amigo == null || Party == null)
                return;

            if (Party.Remove(amigo.Id))
                amigo.Presence = FriendPresence.Online;
        }

        // ---------------------------------------------------------------- Exemplo

        private void MontarAmigosDeExemplo()
        {
            noJogo.Clear();
            steam.Clear();

            FriendPresence[] presencas =
            {
                FriendPresence.Online, FriendPresence.InGarage, FriendPresence.InLobby,
                FriendPresence.InRace, FriendPresence.Offline,
            };

            for (int i = 0; i < amigosNoJogo.Length; i++)
            {
                noJogo.Add(new FriendEntry
                {
                    Id = "ig_" + i,
                    DisplayName = amigosNoJogo[i],
                    Source = FriendSource.InGame,
                    Presence = presencas[i % presencas.Length],
                });
            }

            for (int i = 0; i < amigosSteam.Length; i++)
            {
                steam.Add(new FriendEntry
                {
                    Id = "st_" + i,
                    DisplayName = amigosSteam[i],
                    Source = FriendSource.Steam,
                    Presence = presencas[(i + 1) % presencas.Length],
                });
            }
        }
    }
}
