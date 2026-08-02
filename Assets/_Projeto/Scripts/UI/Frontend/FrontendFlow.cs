using System;
using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Orquestra as telas do frontend, a pré-visualização do kart e a sessão Lobby + Relay.
    /// As telas continuam responsáveis somente por apresentar dados e disparar eventos.
    /// </summary>
    [DisallowMultipleComponent]
    public class FrontendFlow : MonoBehaviour
    {
        [Header("Telas montadas na cena")]
        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private LobbyScreenUI lobby;
        [SerializeField] private GarageScreenUI garagem;
        [SerializeField] private JoinCodeUI joinCode;
        [SerializeField] private StoreScreenUI loja;

        [Header("Carro de pré-visualização (opcional)")]
        [Tooltip("Deixe vazio para rodar o frontend sem o carro 3D — a seleção continua salvando.")]
        [SerializeField] private KartVisualCustomizer carro;
        [Tooltip("Palco inteiro: some nas telas que não mostram carro (Loja, Passe, Config).")]
        [SerializeField] private GameObject palco;
        [SerializeField] private Camera cameraDoPalco;

        [Header("Enquadramento do carro")]
        [SerializeField] private float inclinacao = 8f;
        [SerializeField] private float giro = 18f;
        [Tooltip("Distância: quanto maior, menor o carro na tela.")]
        [SerializeField] private float afastamentoNoLobby = 1.95f;
        [SerializeField] private float afastamentoNaGaragem = 1.45f;
        [Tooltip("Empurra o carro para a direita da tela. 0 = centralizado.")]
        [Range(0f, 2f)] [SerializeField] private float viesNoLobby = 1.45f;
        [Range(0f, 2f)] [SerializeField] private float viesNaGaragem;
        [Tooltip("Move o carro para cima no lobby para não disputar espaço com o card de pista.")]
        [Range(0f, 1f)] [SerializeField] private float viesVerticalNoLobby = 0.28f;

        [Header("Partida")]
        [Tooltip("Pista padrão quando não há seletor de mapa na cena. Precisa estar no Build Settings.")]
        [SerializeField] private string cenaDaCorrida = "MiniGolfeRun";
        [Tooltip("Seletor de mapa do lobby. Quando presente, é ele quem decide a pista.")]
        [SerializeField] private TrackSelectUI seletorDePista;
        [Tooltip("Tela de carregamento do frontend. Sem ela a troca local usa SceneManager.")]
        [SerializeField] private LoadingScreenUI telaDeCarregamento;
        [SerializeField] private int vagasDaSala = 16;

        [Header("Carteira (PlayerPrefs)")]
        [SerializeField] private int moedasIniciais = 12480;
        [SerializeField] private int fichasIniciais = 340;

        private const string ChaveNome = "jogador.nome";
        private const string ChaveMoedas = "carteira.moedas";
        private const string ChaveFichas = "carteira.fichas";

        private float proximoTick;
        private string telaDoPalco;
        private RacePlayerRegistry registry;
        private NetworkBootstrap bootstrap;

        private void Awake()
        {
            GarantirSistemasDeRede();

            if (garagem != null)
            {
                // A garagem não larga corrida: ela só edita e confirma a estilização. Quem inicia a
                // partida é o lobby (que sabe se a sessão é online e quem é o dono da sala).
                garagem.aoSalvarEVoltar.AddListener(SalvarEVoltarAoLobby);
                garagem.aoSalvarEstilo.AddListener(SalvarEstilo);
                garagem.aoTrocarCarro.AddListener(TrocarCarro);
            }

            if (lobby != null)
            {
                lobby.aoAcionarConvite.AddListener(CriarOuCopiarConvite);
                lobby.aoIniciarPartida.AddListener(AcionarPrincipalDoLobby);
                lobby.aoSairDaSala.AddListener(SairDaSala);
            }

            if (joinCode != null)
                joinCode.aoConfirmar.AddListener(EntrarPorCodigo);

            if (registry != null)
                registry.Changed += AtualizarLobby;
            if (bootstrap != null)
                bootstrap.StatusChanged += AoMudarStatusDaRede;
        }

        private void Start()
        {
            // O carro do palco tem loadSelectionOnStart DESLIGADO (o frontend controla o preview).
            // Por isso ele precisa receber a seleção salva explicitamente: com EnsureBuilt sozinho
            // ele nascia no carro padrão, e a primeira troca na garagem gravava esse padrão por
            // cima da escolha real — era isso que "resetava" a customização ao voltar da corrida.
            if (carro != null)
                carro.ApplySavedSelection();

            AtualizarLobby();

            if (loja != null)
            {
                loja.DefinirCarteira(PlayerPrefs.GetInt(ChaveMoedas, moedasIniciais),
                                     PlayerPrefs.GetInt(ChaveFichas, fichasIniciais));
                AtualizarRotacao();
            }
        }

        private void OnDestroy()
        {
            if (garagem != null)
            {
                garagem.aoSalvarEVoltar.RemoveListener(SalvarEVoltarAoLobby);
                garagem.aoSalvarEstilo.RemoveListener(SalvarEstilo);
                garagem.aoTrocarCarro.RemoveListener(TrocarCarro);
            }

            if (lobby != null)
            {
                lobby.aoAcionarConvite.RemoveListener(CriarOuCopiarConvite);
                lobby.aoIniciarPartida.RemoveListener(AcionarPrincipalDoLobby);
                lobby.aoSairDaSala.RemoveListener(SairDaSala);
            }

            if (joinCode != null)
                joinCode.aoConfirmar.RemoveListener(EntrarPorCodigo);

            if (registry != null)
                registry.Changed -= AtualizarLobby;
            if (bootstrap != null)
                bootstrap.StatusChanged -= AoMudarStatusDaRede;
        }

        private void Update()
        {
            AcompanharTela();

            if (loja == null || Time.unscaledTime < proximoTick)
                return;

            proximoTick = Time.unscaledTime + 1f;
            AtualizarRotacao();
        }

        private void GarantirSistemasDeRede()
        {
            registry = RacePlayerRegistry.Instance;
            bootstrap = NetworkBootstrap.Instance;

            GameObject systems = null;
            if (registry == null || bootstrap == null)
            {
                systems = new GameObject("NetworkSystems");
                if (registry == null)
                    registry = systems.AddComponent<RacePlayerRegistry>();
                if (bootstrap == null)
                    bootstrap = systems.AddComponent<NetworkBootstrap>();
            }

            if (registry == null)
                return;

            registry.EnsureLocalPlayer();
            string nome = PlayerPrefs.GetString(ChaveNome, "JOGADOR").Trim();
            registry.SetLocalPlayerIdentity(null, string.IsNullOrEmpty(nome) ? "JOGADOR" : nome, true);
        }

        // ---------- palco do carro ----------
        private void AcompanharTela()
        {
            if (roteador == null || roteador.TelaAtual == telaDoPalco)
                return;

            telaDoPalco = roteador.TelaAtual;
            bool mostra = telaDoPalco == "Lobby" || telaDoPalco == "Garagem";

            if (palco != null)
                palco.SetActive(mostra);

            if (mostra)
                Enquadrar();
        }

        public void Enquadrar()
        {
            if (cameraDoPalco == null || carro == null || carro.CurrentRig == null)
                return;

            Renderer[] renderers = carro.CurrentRig.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds caixa = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                caixa.Encapsulate(renderer.bounds);

            bool noLobby = telaDoPalco == "Lobby";
            float afastamento = noLobby ? afastamentoNoLobby : afastamentoNaGaragem;
            float viesHorizontal = noLobby ? viesNoLobby : viesNaGaragem;

            float escalaDoPalco = palco != null ? Mathf.Abs(palco.transform.localScale.x) : 1f;
            if (escalaDoPalco < 0.01f)
                escalaDoPalco = 1f;

            float raio = caixa.extents.magnitude / escalaDoPalco;
            float fov = cameraDoPalco.fieldOfView * Mathf.Deg2Rad;
            float distancia = raio / Mathf.Tan(fov * 0.5f) * afastamento;

            Quaternion rotacao = Quaternion.Euler(inclinacao, giro, 0f);
            cameraDoPalco.transform.position = caixa.center - rotacao * Vector3.forward * distancia;
            cameraDoPalco.transform.rotation = rotacao;

            Vector3 alvo = caixa.center - cameraDoPalco.transform.right * (raio * viesHorizontal);
            if (noLobby)
                alvo -= cameraDoPalco.transform.up * (raio * viesVerticalNoLobby);
            cameraDoPalco.transform.LookAt(alvo);
        }

        // ---------- partida ----------
        public void Correr()
        {
            string cena = ObterCenaSelecionada();
            if (!ValidarCena(cena))
                return;

            if (carro != null)
                KartGarageSelection.Save();
            registry?.SetLocalPlayerVisual(KartGarageSelection.Capture());

            if (bootstrap != null && bootstrap.IsOnline)
            {
                if (bootstrap.Mode != NetworkBootstrap.SessionMode.Host)
                {
                    lobby?.DefinirAviso("Aguardando o dono da sala iniciar a corrida.");
                    return;
                }

                if (registry == null || !registry.AllReady())
                {
                    lobby?.DefinirAviso("Todos os jogadores precisam ficar prontos antes da largada.");
                    return;
                }

                bootstrap.StartRaceScene(cena);
                return;
            }

            string nomeDaPista = seletorDePista != null && seletorDePista.Atual != null
                ? seletorDePista.Atual.nome
                : cena;

            LoadingScreenUI loading = LoadingScreenUI.Resolver(telaDeCarregamento);
            if (loading != null)
                loading.CarregarCena(cena, "CARREGANDO " + nomeDaPista.ToUpperInvariant());
            else
            {
                Debug.LogWarning("[FrontendFlow] Screen_Loading não encontrada; usando troca de cena direta.");
                SceneManager.LoadScene(cena);
            }
        }

        private string ObterCenaSelecionada()
        {
            return seletorDePista != null && seletorDePista.Atual != null
                ? seletorDePista.Atual.cena
                : cenaDaCorrida;
        }

        private static bool ValidarCena(string cena)
        {
            if (string.IsNullOrWhiteSpace(cena))
            {
                Debug.LogWarning("[FrontendFlow] Nenhuma pista foi selecionada.");
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(cena))
            {
                Debug.LogError($"[FrontendFlow] A cena '{cena}' não está no Build Settings.");
                return false;
            }

            return true;
        }

        // ---------- garagem ----------
        private void TrocarCarro(int indice) => Enquadrar();

        /// <summary>
        /// Confirma a estilização atual: grava em PlayerPrefs, atualiza o registro do jogador e —
        /// quando a sessão é online — publica o visual no lobby para os outros verem o carro certo.
        /// </summary>
        public void SalvarEstilo()
        {
            KartGarageSelection.Save();

            KartVisualSelection selecao = KartGarageSelection.Capture();
            registry?.SetLocalPlayerVisual(selecao);

            if (bootstrap != null && bootstrap.IsOnline)
                bootstrap.PublishLocalVisual();

            garagem?.ConfirmarSalvamento();
        }

        /// <summary>Salva e devolve o jogador ao lobby — a garagem nunca larga corrida.</summary>
        public void SalvarEVoltarAoLobby()
        {
            SalvarEstilo();

            if (roteador != null)
                roteador.Ir("Lobby");

            AtualizarLobby();
        }

        // ---------- sala ----------
        private void CriarOuCopiarConvite()
        {
            if (bootstrap == null || bootstrap.IsBusy)
                return;

            if (bootstrap.IsOnline && bootstrap.HasJoinCode)
            {
                GUIUtility.systemCopyBuffer = bootstrap.CurrentJoinCode;
                lobby?.DefinirAviso($"Código {bootstrap.CurrentJoinCode} copiado — envie para seus amigos.");
                return;
            }

            bootstrap.HostGame();
            AtualizarLobby();
        }

        private void AcionarPrincipalDoLobby()
        {
            if (bootstrap == null || bootstrap.IsBusy)
                return;

            if (!bootstrap.IsOnline)
            {
                Correr();
                return;
            }

            RacePlayerInfo local = registry?.LocalPlayer;
            if (bootstrap.Mode == NetworkBootstrap.SessionMode.Host && registry != null && registry.AllReady())
            {
                Correr();
                return;
            }

            if (local != null)
                bootstrap.SetLocalReady(!local.IsReady);
        }

        private void SairDaSala()
        {
            if (bootstrap != null && bootstrap.IsOnline)
                bootstrap.LeaveGame();

            if (roteador != null)
                roteador.Ir("Garagem");

            AtualizarLobby();
        }

        private void EntrarPorCodigo(string codigo)
        {
            codigo = (codigo ?? string.Empty).Trim().ToUpperInvariant();
            if (codigo.Length != 6)
            {
                joinCode?.MostrarCodigoInvalido();
                return;
            }

            if (bootstrap == null)
            {
                joinCode?.MostrarFalhaConexao("Serviço online indisponível.");
                return;
            }

            joinCode?.DefinirConectando(true);
            bootstrap.JoinGame(codigo);
        }

        private void AoMudarStatusDaRede(string status)
        {
            bool joinVisivel = joinCode != null && joinCode.gameObject.activeInHierarchy;

            if (bootstrap != null && bootstrap.IsOnline)
            {
                joinCode?.DefinirConectando(false);
                if (joinVisivel && roteador != null)
                    roteador.Ir("Lobby");
            }
            else if (bootstrap != null && !bootstrap.IsBusy && joinVisivel &&
                     bootstrap.LastJoinFailure != NetworkBootstrap.JoinFailureReason.None)
            {
                switch (bootstrap.LastJoinFailure)
                {
                    case NetworkBootstrap.JoinFailureReason.LobbyFull:
                        joinCode.MostrarSalaCheia();
                        break;
                    case NetworkBootstrap.JoinFailureReason.InvalidCode:
                        joinCode.MostrarCodigoInvalido();
                        break;
                    case NetworkBootstrap.JoinFailureReason.ServicesUnavailable:
                        joinCode.MostrarFalhaConexao("Serviço online indisponível. Tente novamente.");
                        break;
                    default:
                        joinCode.MostrarFalhaConexao("Não foi possível entrar. Confira o código.");
                        break;
                }
            }

            AtualizarLobby();
        }

        private void AtualizarLobby()
        {
            if (lobby == null)
                return;

            bool online = bootstrap != null && bootstrap.IsOnline;
            var participantes = new List<LobbyScreenUI.Participante>();
            if (registry != null)
            {
                foreach (RacePlayerInfo player in registry.Players)
                {
                    if (player == null)
                        continue;

                    participantes.Add(new LobbyScreenUI.Participante
                    {
                        nome = string.IsNullOrWhiteSpace(player.DisplayName) ? "JOGADOR" : player.DisplayName,
                        pronto = player.IsReady,
                        estado = LobbyScreenUI.EstadoVaga.Ocupada,
                        ehLocal = player.IsLocal,
                        ehDono = online && player.IsHost,
                        ehBot = player.IsBot,
                    });
                }
            }

            lobby.Mostrar(participantes, Mathf.Min(vagasDaSala, RaceConstants.MaxPlayers));

            bool ocupado = bootstrap != null && bootstrap.IsBusy;
            bool ehHost = online && bootstrap.Mode == NetworkBootstrap.SessionMode.Host;
            bool localPronto = registry?.LocalPlayer != null && registry.LocalPlayer.IsReady;
            bool todosProntos = online && registry != null && registry.AllReady();
            string codigo = online ? bootstrap.CurrentJoinCode : string.Empty;

            lobby.MostrarEstadoSessao(
                codigo,
                online,
                ocupado,
                ehHost,
                localPronto,
                todosProntos,
                ObterMensagemDoLobby(online, ocupado, ehHost, localPronto, todosProntos));
        }

        private string ObterMensagemDoLobby(bool online, bool ocupado, bool ehHost, bool localPronto, bool todosProntos)
        {
            if (ocupado && bootstrap != null)
                return bootstrap.Status;

            if (!online)
            {
                if (bootstrap != null && bootstrap.Status.StartsWith("Falha", StringComparison.OrdinalIgnoreCase))
                    return "Online indisponível agora — o modo local continua disponível.";
                return "Crie uma sala online ou entre com o código de um amigo.";
            }

            if (!localPronto)
                return ehHost
                    ? "Sala online criada — compartilhe o código e fique pronto."
                    : "Você entrou na sala — fique pronto para confirmar sua vaga.";

            if (todosProntos)
                return ehHost
                    ? "Todos prontos — você pode iniciar a corrida."
                    : "Todos prontos — aguardando o dono iniciar a corrida.";

            return ehHost
                ? "Você está pronto — aguardando os outros jogadores."
                : "Pronto — aguardando os outros jogadores e o dono da sala.";
        }

        // ---------- loja ----------
        private void AtualizarRotacao()
        {
            TimeSpan falta = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            loja.DefinirTempoDeRotacao($"{falta.Hours:00}:{falta.Minutes:00}:{falta.Seconds:00}");
        }
    }
}
