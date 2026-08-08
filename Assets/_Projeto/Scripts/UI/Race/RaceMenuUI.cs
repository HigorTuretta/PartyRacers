using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PartyRacers.UI.Frontend;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder da tela 12 (menu da partida). NÃO é pausa: a partida é online e a corrida nunca para.
    /// Este script jamais toca em Time.timeScale e não existe botão REINICIAR — handoff §5.
    /// O carro segue em piloto automático enquanto a gaveta está aberta.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceMenuUI : MonoBehaviour
    {
        [Header("Partes já montadas na cena")]
        [SerializeField] private GameObject gaveta;
        [Tooltip("Véu escuro que cobre a tela com a gaveta aberta. Fica ligado junto com ela — " +
                 "deixá-lo sempre visível escurecia a corrida inteira.")]
        [SerializeField] private GameObject veu;
        [SerializeField] private GameObject popupSair;

        [Header("HUD da corrida")]
        [Tooltip("CanvasGroup da HUD de corrida: esmaece enquanto a gaveta está aberta (handoff §5).")]
        [SerializeField] private CanvasGroup hudDaCorrida;
        [Range(0f, 1f)]
        [SerializeField] private float alfaComMenuAberto = 0.35f;

        [Header("Botões")]
        [SerializeField] private Button btnVoltar;
        [SerializeField] private Button btnConfiguracoes;
        [SerializeField] private Button btnCopiarCodigo;
        [SerializeField] private Button btnSair;
        [SerializeField] private Button btnSairAgora;
        [SerializeField] private Button btnFicar;

        [Header("Sala e sessão")]
        [Tooltip("Só um cache: o código de verdade vem do Relay ou da sala privada.")]
        [SerializeField] private string codigoDaSala = string.Empty;
        [Tooltip("Linha curta de retorno das ações do menu (código copiado, saindo...).")]
        [SerializeField] private TextMeshProUGUI aviso;
        [Tooltip("Chapinha do aviso. Some junto com o texto — moldura vazia parece defeito.")]
        [SerializeField] private GameObject avisoFundo;
        [Tooltip("Chapinha AO VIVO. Só acende quando a sessão é online de verdade.")]
        [SerializeField] private GameObject chipAoVivo;
        [Tooltip("Latência. Escondida fora do online — 28 ms desenhado é número inventado.")]
        [SerializeField] private GameObject blocoDePing;

        [Header("Ajuste rápido")]
        [Tooltip("Volume geral. Hoje TODO som do jogo é efeito, então este slider governa tudo.")]
        [SerializeField] private Slider volume;
        [SerializeField] private TextMeshProUGUI valorDoVolume;
        [Tooltip("Linhas de MÚSICA e VIBRAÇÃO. O jogo ainda não tem trilha nem rumble; um " +
                 "controle que não muda nada é mockup, então elas ficam desligadas até existirem.")]
        [SerializeField] private GameObject linhaDeMusica;
        [SerializeField] private GameObject linhaDeVibracao;

        [Tooltip("CONFIGURAÇÕES ainda não tem tela na corrida. Fica marcado em vez de fingir.")]
        [SerializeField] private GameObject marcaEmBreve;

        [Header("Resumo da partida")]
        [Tooltip("Catálogo de pistas — só para traduzir o nome da cena no nome de exibição.")]
        [SerializeField] private System.Collections.Generic.List<PartyRacers.UI.Settings.TrackDefinition> pistas
            = new System.Collections.Generic.List<PartyRacers.UI.Settings.TrackDefinition>();
        [SerializeField] private TextMeshProUGUI valorPista;
        [SerializeField] private TextMeshProUGUI valorVoltas;
        [SerializeField] private TextMeshProUGUI valorCorredores;
        [Tooltip("A mesma fonte de dados da HUD. NÃO apague por parecer duplicata: as telas " +
                 "compartilham UMA instância.")]
        [SerializeField] private HUD.RaceHUDDataProvider dados;

        /// <summary>Mesma chave da tela de Configurações — os dois controlam o mesmo número.</summary>
        private const string ChaveDeVolume = "cfg.efeitos";

        private float avisoAte;

        [Header("Eventos")]
        public UnityEngine.Events.UnityEvent aoAbrirConfiguracoes;
        public UnityEngine.Events.UnityEvent aoSairDaPartida;

        [Header("Destino ao sair")]
        [Tooltip("Cena do frontend carregada por SAIR DA PARTIDA. Precisa estar no Build Settings.")]
        [SerializeField] private string cenaDoFrontend = "Frontend";
        [SerializeField] private LoadingScreenUI telaDeCarregamento;

        public bool Aberto { get; private set; }

        private void Awake()
        {
            if (btnVoltar != null) btnVoltar.onClick.AddListener(Fechar);
            if (btnConfiguracoes != null) btnConfiguracoes.onClick.AddListener(() => aoAbrirConfiguracoes?.Invoke());
            if (btnCopiarCodigo != null) btnCopiarCodigo.onClick.AddListener(CopiarCodigo);
            if (btnSair != null) btnSair.onClick.AddListener(() => MostrarConfirmacao(true));
            if (btnFicar != null) btnFicar.onClick.AddListener(() => MostrarConfirmacao(false));
            if (btnSairAgora != null) btnSairAgora.onClick.AddListener(ConfirmarSaida);

            // CONFIGURAÇÕES não tem tela dentro da corrida ainda. Em vez de um botão que não
            // responde, ele fica visivelmente indisponível — botão morto é pior que botão marcado.
            if (btnConfiguracoes != null)
                btnConfiguracoes.interactable = false;

            Ligar(marcaEmBreve, true);
            Ligar(linhaDeMusica, false);
            Ligar(linhaDeVibracao, false);

            PrepararVolume();
            Fechar();
        }

        /// <summary>
        /// O slider governa `AudioListener.volume`.
        ///
        /// Não há AudioMixer nem trilha no projeto: todo som sai de `AudioSource` de gameplay. Um
        /// slider "MÚSICA" separado não teria o que mexer, então existe UM controle e ele é o de
        /// verdade. A chave é a mesma da tela de Configurações — dois lugares que dizem "volume"
        /// têm de dizer o mesmo número.
        /// </summary>
        private void PrepararVolume()
        {
            float salvo = PlayerPrefs.GetFloat(ChaveDeVolume, 70f);
            AplicarVolume(salvo);

            if (volume == null)
                return;

            volume.minValue = 0f;
            volume.maxValue = 100f;
            volume.wholeNumbers = true;
            volume.SetValueWithoutNotify(salvo);
            volume.onValueChanged.AddListener(v =>
            {
                AplicarVolume(v);
                PlayerPrefs.SetFloat(ChaveDeVolume, v);
                PlayerPrefs.Save();
            });
        }

        private void AplicarVolume(float valor)
        {
            AudioListener.volume = Mathf.Clamp01(valor / 100f);

            if (valorDoVolume != null)
                valorDoVolume.text = Mathf.RoundToInt(valor).ToString();
        }

        /// <summary>
        /// O volume salvo vale desde o primeiro frame, não só depois de alguém abrir o menu.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AplicarVolumeSalvo()
            => AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(ChaveDeVolume, 70f) / 100f);

        private void Update()
        {
            if (avisoAte > 0f && Time.unscaledTime >= avisoAte)
                Avisar(string.Empty);

            // ESC fecha e volta na hora; abre o menu quando fechado.
            // Input System (o projeto está em "Input System Package" — o Input legado lança exceção).
            Keyboard teclado = Keyboard.current;
            if (teclado != null && teclado.escapeKey.wasPressedThisFrame)
            {
                if (popupSair != null && popupSair.activeSelf) MostrarConfirmacao(false);
                else if (Aberto) Fechar();
                else Abrir();
            }
        }

        public void Abrir()
        {
            Aberto = true;
            Ligar(veu, true);
            Ligar(gaveta, true);
            EsmaecerHUD(true);
            MostrarConfirmacao(false);
            Avisar(string.Empty);
            AtualizarSessao();
            AtualizarResumo();
            // sem Time.timeScale: a corrida continua rodando atrás
        }

        /// <summary>
        /// Pista, volta e tamanho da grade — lidos da corrida que está rodando atrás.
        ///
        /// O rodapé da gaveta ficou vazio quando MÚSICA e VIBRAÇÃO saíram. Preencher com o estado
        /// real responde justamente o que se pergunta ao abrir o menu no meio de uma corrida.
        /// </summary>
        private void AtualizarResumo()
        {
            if (dados == null)
                dados = FindAnyObjectByType<HUD.RaceHUDDataProvider>();

            if (valorPista != null)
                valorPista.text = NomeDaPista();

            if (valorVoltas != null)
                valorVoltas.text = dados == null
                    ? "—"
                    : $"{Mathf.Clamp(dados.CurrentLap, 1, Mathf.Max(1, dados.TotalLaps))}/{dados.TotalLaps}";

            if (valorCorredores == null)
                return;

            // Refresh antes de contar: a classificação é atualizada a 20 Hz e o menu pode abrir
            // entre dois passos, quando a lista ainda está vazia no primeiro frame da corrida.
            if (dados != null)
                dados.Refresh();

            int grade = dados != null ? dados.Standings.Count : 0;
            valorCorredores.text = grade > 0 ? grade.ToString() : "—";
        }

        /// <summary>Nome de exibição da pista; sem catálogo, o nome da cena já diz alguma coisa.</summary>
        private string NomeDaPista()
        {
            string cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            foreach (PartyRacers.UI.Settings.TrackDefinition p in pistas)
                if (p != null && string.Equals(p.cena, cena, System.StringComparison.OrdinalIgnoreCase))
                    return string.IsNullOrWhiteSpace(p.nome) ? cena.ToUpperInvariant() : p.nome;

            return cena.ToUpperInvariant();
        }

        /// <summary>
        /// Estado real da sessão: AO VIVO e a latência só existem quando há sessão online.
        ///
        /// O documento desenha os dois sempre acesos, com "28 ms" fixo. Offline isso é mentira
        /// dupla — não está ao vivo e não há latência nenhuma para medir.
        /// </summary>
        private void AtualizarSessao()
        {
            PartyRacers.Networking.NetworkBootstrap rede = PartyRacers.Networking.NetworkBootstrap.Instance;
            bool online = rede != null && rede.IsOnline;

            Ligar(chipAoVivo, online);
            Ligar(blocoDePing, online);

            if (btnCopiarCodigo != null)
                btnCopiarCodigo.interactable = !string.IsNullOrEmpty(CodigoAtual());
        }

        /// <summary>
        /// Código da sala: o do Relay quando a sessão é online, senão o da sala privada.
        ///
        /// O campo serializado era preenchido por ninguém, então COPIAR CÓDIGO copiava vazio.
        /// </summary>
        private string CodigoAtual()
        {
            PartyRacers.Networking.NetworkBootstrap rede = PartyRacers.Networking.NetworkBootstrap.Instance;
            if (rede != null && rede.IsOnline && rede.HasJoinCode)
                return rede.CurrentJoinCode;

            if (!string.IsNullOrEmpty(codigoDaSala))
                return codigoDaSala;

            return PlayerPrefs.GetString("sala.codigo", string.Empty);
        }

        private void Avisar(string texto)
        {
            bool tem = !string.IsNullOrEmpty(texto);

            if (aviso != null)
                aviso.text = texto;

            // O SetActive é que dispara a entrada animada da chapinha (UIAppear roda no OnEnable).
            Ligar(avisoFundo, tem);
            avisoAte = tem ? Time.unscaledTime + 3.5f : 0f;
        }

        public void Fechar()
        {
            Aberto = false;
            Ligar(veu, false);
            Ligar(gaveta, false);
            EsmaecerHUD(false);
            MostrarConfirmacao(false);
        }

        /// <summary>A HUD continua visível durante o menu, só mais apagada — nada é duplicado.</summary>
        private void EsmaecerHUD(bool menuAberto)
        {
            if (hudDaCorrida == null)
                return;

            hudDaCorrida.alpha = menuAberto ? alfaComMenuAberto : 1f;
        }

        public void DefinirCodigoDaSala(string codigo) => codigoDaSala = codigo;

        private void MostrarConfirmacao(bool mostrar) => Ligar(popupSair, mostrar);

        private void CopiarCodigo()
        {
            string codigo = CodigoAtual();

            if (string.IsNullOrEmpty(codigo))
            {
                Avisar("ESTA PARTIDA NÃO TEM CÓDIGO");
                return;
            }

            GUIUtility.systemCopyBuffer = codigo;
            Avisar($"CÓDIGO {codigo} COPIADO");
        }

        private void ConfirmarSaida()
        {
            MostrarConfirmacao(false);
            Avisar("SAINDO DA PARTIDA...");
            Fechar();
            aoSairDaPartida?.Invoke();

            // Não existe ScreenRouter numa cena de pista: quem sai da partida tem de carregar o
            // frontend aqui mesmo. Antes só o evento era disparado, e como ninguém o escutava o
            // botão SAIR não fazia absolutamente nada.
            if (string.IsNullOrEmpty(cenaDoFrontend))
            {
                Debug.LogWarning("[RaceMenuUI] 'cenaDoFrontend' vazio — SAIR não tem para onde ir.");
                return;
            }

            // Encerra a sessão antes de trocar de cena: com o gerenciamento de cenas do Netcode
            // ligado, um cliente que chama LoadScene sozinho é simplesmente ignorado e o botão
            // SAIR não fazia nada para quem não era o dono da sala.
            PartyRacers.Networking.NetworkBootstrap rede = PartyRacers.Networking.NetworkBootstrap.Instance;
            if (rede != null && rede.IsOnline)
                rede.LeaveGame();

            LoadingScreenUI loading = LoadingScreenUI.Resolver(telaDeCarregamento);
            if (loading != null)
                loading.CarregarCena(cenaDoFrontend, "VOLTANDO AO LOBBY");
            else
            {
                Debug.LogWarning("[RaceMenuUI] Screen_Loading nao encontrada; usando troca de cena direta.");
                UnityEngine.SceneManagement.SceneManager.LoadScene(cenaDoFrontend);
            }
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
