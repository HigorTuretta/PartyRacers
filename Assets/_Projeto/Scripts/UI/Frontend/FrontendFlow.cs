using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Amarra as telas do frontend umas nas outras e no gameplay. É o único ponto que sabe
    /// "o que acontece quando o jogador clica CORRER" — as telas continuam burras, só
    /// disparam eventos. Não monta nada: tudo aqui é assinar evento e escrever em campo
    /// que já existe na cena.
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
        [SerializeField] private float afastamentoNoLobby = 1.7f;
        [SerializeField] private float afastamentoNaGaragem = 1.45f;
        [Tooltip("Empurra o carro para a direita da tela. 0 = centralizado.")]
        [Range(0f, 2f)] [SerializeField] private float viesNoLobby = 1.35f;
        [Range(0f, 2f)] [SerializeField] private float viesNaGaragem = 0f;

        [Header("Partida")]
        [Tooltip("Pista padrão quando não há seletor de mapa na cena. Precisa estar no Build Settings.")]
        [SerializeField] private string cenaDaCorrida = "MiniGolfeRun";
        [Tooltip("Seletor de mapa do lobby. Quando presente, é ele quem decide a pista.")]
        [SerializeField] private TrackSelectUI seletorDePista;
        [Tooltip("Tela de carregamento do frontend. Sem ela a troca de cena trava a imagem.")]
        [SerializeField] private LoadingScreenUI telaDeCarregamento;
        [SerializeField] private int vagasDaSala = 16;

        [Header("Carteira (PlayerPrefs)")]
        [SerializeField] private int moedasIniciais = 12480;
        [SerializeField] private int fichasIniciais = 340;

        const string ChaveNome = "jogador.nome";
        const string ChaveCodigo = "sala.codigo";
        const string ChaveMoedas = "carteira.moedas";
        const string ChaveFichas = "carteira.fichas";
        const string Alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // sem I/O/0/1: confundem no convite

        private float proximoTick;
        private string telaDoPalco;

        private void Awake()
        {
            if (garagem != null)
            {
                garagem.aoCorrer.AddListener(Correr);
                garagem.aoJogarLocalmente.AddListener(Correr);
                garagem.aoTrocarCarro.AddListener(TrocarCarro);
            }

            if (lobby != null)
            {
                lobby.aoIniciarPartida.AddListener(Correr);
                lobby.aoSairDaSala.AddListener(SairDaSala);
            }

            if (joinCode != null)
                joinCode.aoConfirmar.AddListener(EntrarPorCodigo);
        }

        private void Start()
        {
            if (carro != null)
                carro.EnsureBuilt();

            AplicarCodigo(PlayerPrefs.GetString(ChaveCodigo, GerarCodigo()));
            AtualizarLobby();

            if (loja != null)
            {
                loja.DefinirCarteira(PlayerPrefs.GetInt(ChaveMoedas, moedasIniciais),
                                     PlayerPrefs.GetInt(ChaveFichas, fichasIniciais));
                AtualizarRotacao();
            }
        }

        private void Update()
        {
            AcompanharTela();

            // o timer da loja é o único dado que muda sozinho no frontend
            if (loja == null || Time.unscaledTime < proximoTick)
                return;

            proximoTick = Time.unscaledTime + 1f;
            AtualizarRotacao();
        }

        // ---------- palco do carro ----------
        /// <summary>
        /// O carro só aparece no Lobby (à direita, só visualização) e na Garagem (centralizado).
        /// Nas outras telas o palco é desligado — elas têm fundo opaco e nada apareceria mesmo.
        /// </summary>
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

        /// <summary>
        /// Aponta a câmera para o carro atual. É 3D, não layout de UI: a regra do handoff sobre
        /// não posicionar por código vale para os elementos de interface.
        /// </summary>
        public void Enquadrar()
        {
            if (cameraDoPalco == null || carro == null || carro.CurrentRig == null)
                return;

            var renderers = carro.CurrentRig.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds caixa = renderers[0].bounds;
            foreach (Renderer r in renderers)
                caixa.Encapsulate(r.bounds);

            bool noLobby = telaDoPalco == "Lobby";
            float afastamento = noLobby ? afastamentoNoLobby : afastamentoNaGaragem;
            float vies = noLobby ? viesNoLobby : viesNaGaragem;

            // o palco encolhe o carro durante a animação de troca; enquadrar pela caixa medida
            // nesse instante deixaria a câmera perto demais e o carro estouraria a tela ao
            // voltar ao tamanho normal. Descontar a escala dá sempre o enquadramento final.
            float escalaDoPalco = palco != null ? Mathf.Abs(palco.transform.localScale.x) : 1f;
            if (escalaDoPalco < 0.01f) escalaDoPalco = 1f;

            float raio = caixa.extents.magnitude / escalaDoPalco;
            float fov = cameraDoPalco.fieldOfView * Mathf.Deg2Rad;
            float distancia = raio / Mathf.Tan(fov * 0.5f) * afastamento;

            Quaternion rot = Quaternion.Euler(inclinacao, giro, 0f);
            cameraDoPalco.transform.position = caixa.center - rot * Vector3.forward * distancia;
            cameraDoPalco.transform.rotation = rot;

            // mirar à esquerda do carro joga o carro para a direita da tela
            cameraDoPalco.transform.LookAt(caixa.center - cameraDoPalco.transform.right * (raio * vies));
        }

        // ---------- partida ----------
        public void Correr()
        {
            // a pista escolhida manda; 'cenaDaCorrida' é só o padrão de quando não há seletor
            string cena = seletorDePista != null && seletorDePista.Atual != null
                ? seletorDePista.Atual.cena
                : cenaDaCorrida;

            if (string.IsNullOrEmpty(cena))
            {
                Debug.LogWarning("[FrontendFlow] nenhuma pista selecionada e 'cenaDaCorrida' vazio.");
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(cena) == false)
            {
                Debug.LogError($"[FrontendFlow] a cena '{cena}' não está no Build Settings — a corrida não carrega.");
                return;
            }

            if (carro != null)
                KartGarageSelection.Save();   // a pista lê a seleção salva ao montar o kart

            // carregar de forma síncrona congelava a imagem até a pista abrir; a tela de
            // carregamento faz em segundo plano e mantém o movimento e as dicas na tela
            string nomeDaPista = seletorDePista != null && seletorDePista.Atual != null
                ? seletorDePista.Atual.nome
                : cena;

            LoadingScreenUI loading = LoadingScreenUI.Resolver(telaDeCarregamento);
            if (loading != null)
                loading.CarregarCena(cena, "CARREGANDO " + nomeDaPista.ToUpperInvariant());
            else
            {
                Debug.LogWarning("[FrontendFlow] Screen_Loading nao encontrada; usando troca de cena direta.");
                SceneManager.LoadScene(cena);
            }
        }

        // ---------- garagem ----------
        /// <summary>
        /// Quem aplica a troca é a GarageScreenUI, que fala direto com o rig do carro. Aqui só
        /// resta reenquadrar: o carro novo tem outra caixa. Chega já na escala final, porque a
        /// tela só avisa depois da animação do palco terminar.
        /// </summary>
        private void TrocarCarro(int indice) => Enquadrar();

        // ---------- sala ----------
        private void AplicarCodigo(string codigo)
        {
            PlayerPrefs.SetString(ChaveCodigo, codigo);
            PlayerPrefs.Save();
            if (lobby != null)
                lobby.DefinirCodigo(codigo);
        }

        /// <summary>
        /// Enquanto não existe sessão de rede, a sala tem só o jogador local. Quando o NGO
        /// entrar, é esta lista que passa a vir do RacePlayerRegistry.
        /// </summary>
        private void AtualizarLobby()
        {
            if (lobby == null)
                return;

            var participantes = new List<LobbyScreenUI.Participante>
            {
                new LobbyScreenUI.Participante
                {
                    // a própria tela acrescenta o "(dono · você)" — o nome aqui é só o nome
                    nome = PlayerPrefs.GetString(ChaveNome, "JOGADOR"),
                    pronto = true,
                    estado = LobbyScreenUI.EstadoVaga.Ocupada,
                    ehLocal = true,
                    ehDono = true,
                },
            };

            lobby.Mostrar(participantes, vagasDaSala);
        }

        private void SairDaSala()
        {
            AplicarCodigo(GerarCodigo());
            AtualizarLobby();
            if (roteador != null)
                roteador.Ir("Garagem");
        }

        private void EntrarPorCodigo(string codigo)
        {
            if (string.IsNullOrEmpty(codigo) || codigo.Length < 6)
            {
                joinCode?.MostrarCodigoInvalido();
                return;
            }

            AplicarCodigo(codigo);
            AtualizarLobby();
            if (roteador != null)
                roteador.Ir("Lobby");
        }

        private static string GerarCodigo()
        {
            var chars = new char[6];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = Alfabeto[UnityEngine.Random.Range(0, Alfabeto.Length)];
            return new string(chars);
        }

        // ---------- loja ----------
        /// <summary>A rotação diária vira à meia-noite UTC — o mesmo instante para todo mundo.</summary>
        private void AtualizarRotacao()
        {
            TimeSpan falta = DateTime.UtcNow.Date.AddDays(1) - DateTime.UtcNow;
            loja.DefinirTempoDeRotacao($"{falta.Hours:00}:{falta.Minutes:00}:{falta.Seconds:00}");
        }
    }
}
