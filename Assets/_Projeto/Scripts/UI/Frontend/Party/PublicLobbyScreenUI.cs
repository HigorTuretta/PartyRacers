using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Binder do LOBBY PÚBLICO (Screen_Lobby): modo, grupo, amigos e o botão de buscar partida.
    ///
    /// Tudo o que esta tela desenha vem de <see cref="PartyState"/>. Ela não guarda estado próprio —
    /// se guardasse, a tela e o grupo poderiam discordar sobre quem está pronto, que é exatamente
    /// o tipo de divergência que faz um jogador clicar em BUSCAR e nada acontecer.
    ///
    /// Nenhum objeto é criado, movido ou pintado aqui. As vagas do grupo (4) já estão na cena com
    /// os três filhos de estado; só a lista de amigos usa Instantiate, porque é a única cujo
    /// tamanho é desconhecido.
    /// </summary>
    [DisallowMultipleComponent]
    public class PublicLobbyScreenUI : MonoBehaviour
    {
        /// <summary>Uma das 4 vagas do grupo, já montada na cena com seus estados.</summary>
        [System.Serializable]
        public class SlotDeGrupo
        {
            public GameObject raiz;

            [Header("Estados (filhos mutuamente exclusivos)")]
            public GameObject estadoJogador;
            public GameObject estadoVazio;
            public GameObject estadoBloqueado;

            [Header("Peças de dentro de State_Player")]
            public TextMeshProUGUI nome;
            public TextMeshProUGUI meta;
            public GameObject seloDeLider;
            public GameObject estadoPronto;
            public GameObject estadoAguardando;
            [Tooltip("Contorno que marca a linha do próprio jogador.")]
            public GameObject destaqueLocal;
            [Tooltip("Quadrado de identidade. A cor vem do nome do membro.")]
            public Graphic avatar;
        }

        /// <summary>Um card de modo (SOLO/DUO/SQUAD), já montado com seus dois estados.</summary>
        [System.Serializable]
        public class CardDeModo
        {
            public PartyMode modo;
            public Button botao;
            public GameObject estadoAtivo;
            public GameObject estadoOcioso;
        }

        [Header("Grupo")]
        [SerializeField] private List<SlotDeGrupo> vagas = new List<SlotDeGrupo>();
        [SerializeField] private TextMeshProUGUI contadorDoGrupo;

        [Header("Modo")]
        [SerializeField] private List<CardDeModo> cardsDeModo = new List<CardDeModo>();

        [Header("Amigos")]
        [SerializeField] private Transform conteudoDaListaDeAmigos;
        [SerializeField] private FriendRowUI prefabDeAmigo;
        [SerializeField] private Button abaNoJogo;
        [SerializeField] private Button abaSteam;
        [SerializeField] private GameObject abaNoJogoAtiva;
        [SerializeField] private GameObject abaNoJogoOciosa;
        [SerializeField] private GameObject abaSteamAtiva;
        [SerializeField] private GameObject abaSteamOciosa;
        [SerializeField] private GameObject avisoDeListaVazia;

        [Header("Barra de ação")]
        [SerializeField] private Button btnPronto;
        [SerializeField] private GameObject btnProntoEstadoPronto;
        [SerializeField] private GameObject btnProntoEstadoAguardando;
        [SerializeField] private Button btnBuscarPartida;
        [Tooltip("Aparência habilitada (verde) do botão de buscar.")]
        [SerializeField] private GameObject buscarHabilitado;
        [Tooltip("Aparência desabilitada (tracejada). Mostra o MOTIVO do bloqueio.")]
        [SerializeField] private GameObject buscarDesabilitado;
        [SerializeField] private TextMeshProUGUI motivoDoBloqueio;
        [SerializeField] private TextMeshProUGUI resumoDoGrupo;

        [Header("Palco 3D")]
        [Tooltip("Chapinha sob o kart do jogador. Mostra o nome de quem está no palco.")]
        [SerializeField] private TextMeshProUGUI etiquetaDoKart;

        [Header("Fluxo")]
        [SerializeField] private PartyController controlador;
        [SerializeField] private MatchmakingModalUI modalDeBusca;

        private readonly List<FriendRowUI> linhasDeAmigo = new List<FriendRowUI>();
        private FriendSource abaAtual = FriendSource.InGame;

        private PartyState Grupo => controlador != null ? controlador.Party : null;

        // ---------------------------------------------------------------- Ciclo

        private void Awake()
        {
            foreach (CardDeModo card in cardsDeModo)
            {
                if (card?.botao == null)
                    continue;

                PartyMode modo = card.modo;
                card.botao.onClick.AddListener(() => EscolherModo(modo));
            }

            if (btnPronto != null)
                btnPronto.onClick.AddListener(AlternarPronto);

            if (btnBuscarPartida != null)
                btnBuscarPartida.onClick.AddListener(BuscarPartida);

            if (abaNoJogo != null)
                abaNoJogo.onClick.AddListener(() => TrocarAba(FriendSource.InGame));

            if (abaSteam != null)
                abaSteam.onClick.AddListener(() => TrocarAba(FriendSource.Steam));
        }

        private void OnEnable()
        {
            if (Grupo != null)
                Grupo.Changed += Redesenhar;

            Redesenhar();
            RedesenharAbas();
            RedesenharAmigos();
        }

        // O grupo é montado no Awake do PartyController, e a ordem entre Awake de objetos
        // diferentes não é garantida: no OnEnable a lista de amigos ainda podia estar vazia, e a
        // coluna da direita nascia em branco. Redesenhar no Start acontece com todo mundo pronto.
        private void Start()
        {
            Redesenhar();
            RedesenharAbas();
            RedesenharAmigos();
        }

        private void OnDisable()
        {
            if (Grupo != null)
                Grupo.Changed -= Redesenhar;
        }

        // ---------------------------------------------------------------- Ações

        private void EscolherModo(PartyMode modo)
        {
            if (controlador != null)
                controlador.EscolherModo(modo);
        }

        private void AlternarPronto()
        {
            Grupo?.ToggleLocalReady();
        }

        private void BuscarPartida()
        {
            // O botão só age com o grupo inteiro pronto. O clique bloqueado não é silencioso:
            // o estado desabilitado já está na tela dizendo o motivo.
            if (Grupo == null || !Grupo.CanSearch)
                return;

            controlador?.IniciarBusca();

            if (modalDeBusca != null)
                modalDeBusca.Abrir();
        }

        private void TrocarAba(FriendSource fonte)
        {
            abaAtual = fonte;
            RedesenharAbas();
            RedesenharAmigos();
        }

        // ---------------------------------------------------------------- Desenho

        private void Redesenhar()
        {
            PartyState grupo = Grupo;
            if (grupo == null)
                return;

            RedesenharModos(grupo);
            RedesenharVagas(grupo);
            RedesenharBarraDeAcao(grupo);
        }

        private void RedesenharModos(PartyState grupo)
        {
            foreach (CardDeModo card in cardsDeModo)
            {
                if (card == null)
                    continue;

                bool ativo = card.modo == grupo.Mode;
                Ligar(card.estadoAtivo, ativo);
                Ligar(card.estadoOcioso, !ativo);
            }
        }

        private void RedesenharVagas(PartyState grupo)
        {
            IReadOnlyList<PartyMember> membros = grupo.Members;

            for (int i = 0; i < vagas.Count; i++)
            {
                SlotDeGrupo vaga = vagas[i];
                if (vaga == null || vaga.raiz == null)
                    continue;

                // Vaga além da capacidade do modo fica BLOQUEADA em vez de sumir: sumir faria o
                // painel mudar de altura a cada troca de modo, e o jogador perde a referência de
                // onde as coisas estão.
                bool dentroDaCapacidade = i < grupo.Capacity;
                bool ocupada = i < membros.Count;

                Ligar(vaga.estadoJogador, ocupada);
                Ligar(vaga.estadoVazio, !ocupada && dentroDaCapacidade);
                Ligar(vaga.estadoBloqueado, !dentroDaCapacidade);

                if (!ocupada)
                    continue;

                PartyMember membro = membros[i];

                if (vaga.nome != null)
                    vaga.nome.text = membro.IsLocal
                        ? $"{membro.DisplayName} (VOCÊ)"
                        : membro.DisplayName;

                if (vaga.meta != null)
                    vaga.meta.text = $"nível {membro.Level} · {membro.PingMs}ms";

                if (vaga.avatar != null)
                    vaga.avatar.color = PlayerTint.De(membro.DisplayName);

                Ligar(vaga.seloDeLider, membro.IsLeader);
                Ligar(vaga.estadoPronto, membro.State == MemberState.Ready);
                Ligar(vaga.estadoAguardando, membro.State != MemberState.Ready);
                Ligar(vaga.destaqueLocal, membro.IsLocal);
            }

            if (contadorDoGrupo != null)
                contadorDoGrupo.text = $"{grupo.FilledSlots}/{grupo.Capacity}";

            // A chapinha sob o kart dizia "SEU KART" fixo — era o rótulo do mockup. Com o nome,
            // ela passa a ser a legenda do carro que está no palco.
            if (etiquetaDoKart != null)
            {
                PartyMember eu = grupo.Local;
                etiquetaDoKart.text = eu != null ? eu.DisplayName.ToUpperInvariant() : "SEU KART";
            }
        }

        private void RedesenharBarraDeAcao(PartyState grupo)
        {
            PartyMember local = grupo.Local;
            bool localPronto = local != null && local.State == MemberState.Ready;

            Ligar(btnProntoEstadoPronto, localPronto);
            Ligar(btnProntoEstadoAguardando, !localPronto);

            bool podeBuscar = grupo.CanSearch;
            Ligar(buscarHabilitado, podeBuscar);
            Ligar(buscarDesabilitado, !podeBuscar);

            if (btnBuscarPartida != null)
                btnBuscarPartida.interactable = podeBuscar;

            // Botão desabilitado SEMPRE explica o motivo. Um botão cinza mudo ensina o jogador a
            // clicar em vão e depois a desconfiar da tela inteira.
            if (motivoDoBloqueio != null)
                motivoDoBloqueio.text = grupo.SearchBlockReason;

            if (resumoDoGrupo != null)
            {
                int esperando = grupo.WaitingCount;
                resumoDoGrupo.text = esperando == 0
                    ? "GRUPO PRONTO"
                    : $"{esperando} AGUARDANDO CONFIRMAÇÃO";
            }
        }

        private void RedesenharAbas()
        {
            bool noJogo = abaAtual == FriendSource.InGame;
            Ligar(abaNoJogoAtiva, noJogo);
            Ligar(abaNoJogoOciosa, !noJogo);
            Ligar(abaSteamAtiva, !noJogo);
            Ligar(abaSteamOciosa, noJogo);
        }

        private void RedesenharAmigos()
        {
            if (conteudoDaListaDeAmigos == null || prefabDeAmigo == null || controlador == null)
                return;

            IReadOnlyList<FriendEntry> amigos = controlador.AmigosDe(abaAtual);

            // Desanexa antes de destruir: o Layout Group conta os filhos antigos por um frame e a
            // lista aparece com o dobro de linhas antes de assentar.
            for (int i = 0; i < linhasDeAmigo.Count; i++)
            {
                if (linhasDeAmigo[i] == null)
                    continue;

                linhasDeAmigo[i].transform.SetParent(null);
                Destroy(linhasDeAmigo[i].gameObject);
            }

            linhasDeAmigo.Clear();

            for (int i = 0; i < amigos.Count; i++)
            {
                FriendRowUI linha = Instantiate(prefabDeAmigo, conteudoDaListaDeAmigos);
                linha.Bind(amigos[i], controlador);
                linhasDeAmigo.Add(linha);
            }

            Ligar(avisoDeListaVazia, amigos.Count == 0);
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
