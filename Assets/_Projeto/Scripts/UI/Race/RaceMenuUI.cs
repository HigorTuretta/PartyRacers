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

        [Header("Sala")]
        [Tooltip("Código da sala copiado para a área de transferência.")]
        [SerializeField] private string codigoDaSala = string.Empty;

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

            Fechar();
        }

        private void Update()
        {
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
            // sem Time.timeScale: a corrida continua rodando atrás
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
            if (!string.IsNullOrEmpty(codigoDaSala))
                GUIUtility.systemCopyBuffer = codigoDaSala;
        }

        private void ConfirmarSaida()
        {
            MostrarConfirmacao(false);
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
