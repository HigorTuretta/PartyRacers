using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Settings;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Binder da PARTIDA PERSONALIZADA (Screen_CustomMatch). Aqui manda a administração da sala,
    /// não o carro: o palco 3D recua e a lista de 16 ocupa a tela.
    ///
    /// Duas regras de leitura que a tela existe para cumprir:
    /// • 16 vagas em DUAS COLUNAS DE 8, sem rolagem. Rolagem numa lista de sala é o que faz o
    ///   anfitrião perder de vista quem ainda não confirmou.
    /// • Bot e humano nunca se parecem. O bot tem cor própria (violeta) e rótulo próprio; um bot
    ///   disfarçado de jogador faz o anfitrião esperar uma confirmação que nunca vem.
    ///
    /// Diferente do matchmaking público, aqui o MAPA É MANUAL.
    /// </summary>
    [DisallowMultipleComponent]
    public class CustomMatchScreenUI : MonoBehaviour
    {
        /// <summary>Uma vaga da sala, já montada na cena com seus três estados.</summary>
        [System.Serializable]
        public class Vaga
        {
            public GameObject raiz;

            [Header("Estados (filhos mutuamente exclusivos)")]
            public GameObject estadoJogador;
            public GameObject estadoBot;
            public GameObject estadoVazio;

            [Header("Peças de dentro de State_Player")]
            public TextMeshProUGUI indice;
            public TextMeshProUGUI nome;
            public GameObject seloDeAnfitriao;
            public GameObject estadoPronto;
            public GameObject estadoAguardando;
            [Tooltip("Só aparece para o anfitrião, e nunca na própria linha dele.")]
            public Button btnRemover;
        }

        /// <summary>Um ocupante da sala. Vira estado de vaga na tela.</summary>
        public class Ocupante
        {
            public string Nome;
            public bool Bot;
            public bool Anfitriao;
            public bool Local;
            public bool Pronto;
        }

        [Header("Sala")]
        [SerializeField] private List<Vaga> vagas = new List<Vaga>();
        [SerializeField] private TextMeshProUGUI contador;
        [SerializeField] private TextMeshProUGUI codigoDaSala;
        [SerializeField] private Button btnCopiarCodigo;

        [Header("Mapa (manual — diferente do público)")]
        [SerializeField] private TrackSelectUI seletorDePista;
        [SerializeField] private TextMeshProUGUI nomeDoMapa;
        [SerializeField] private TextMeshProUGUI descricaoDoMapa;
        [SerializeField] private Image previewDoMapa;

        [Header("Bots")]
        [SerializeField] private Button btnAdicionarBot;
        [SerializeField] private Button btnRemoverBot;
        [SerializeField] private TextMeshProUGUI contadorDeBots;

        [Header("Ações")]
        [SerializeField] private Button btnPronto;
        [SerializeField] private GameObject btnProntoEstadoPronto;
        [SerializeField] private GameObject btnProntoEstadoAguardando;
        [SerializeField] private Button btnIniciar;
        [SerializeField] private GameObject iniciarHabilitado;
        [SerializeField] private GameObject iniciarDesabilitado;
        [SerializeField] private TextMeshProUGUI motivoDoBloqueio;

        [Header("Fluxo")]
        [SerializeField] private FrontendFlow fluxo;
        [Tooltip("Quem comanda a sala vê os controles de remover e iniciar.")]
        [SerializeField] private bool souAnfitriao = true;

        private readonly List<Ocupante> sala = new List<Ocupante>();

        private void Awake()
        {
            if (btnPronto != null)
                btnPronto.onClick.AddListener(AlternarPronto);

            if (btnIniciar != null)
                btnIniciar.onClick.AddListener(Iniciar);

            if (btnAdicionarBot != null)
                btnAdicionarBot.onClick.AddListener(AdicionarBot);

            if (btnRemoverBot != null)
                btnRemoverBot.onClick.AddListener(RemoverUltimoBot);

            if (btnCopiarCodigo != null)
                btnCopiarCodigo.onClick.AddListener(CopiarCodigo);

            for (int i = 0; i < vagas.Count; i++)
            {
                if (vagas[i]?.btnRemover == null)
                    continue;

                int indice = i;
                vagas[i].btnRemover.onClick.AddListener(() => Remover(indice));
            }
        }

        private void OnEnable()
        {
            if (sala.Count == 0)
            {
                sala.Add(new Ocupante
                {
                    Nome = "VOCÊ",
                    Anfitriao = souAnfitriao,
                    Local = true,
                    Pronto = true,
                });
            }

            Redesenhar();
        }

        // ---------------------------------------------------------------- Ações

        private void AlternarPronto()
        {
            Ocupante local = sala.Find(o => o.Local);
            if (local == null)
                return;

            local.Pronto = !local.Pronto;
            Redesenhar();
        }

        private void AdicionarBot()
        {
            if (sala.Count >= vagas.Count)
                return;

            // Bot entra sempre PRONTO: ele não confirma nada, e mostrá-lo aguardando faria a sala
            // parecer travada por causa de alguém que nunca vai responder.
            sala.Add(new Ocupante { Nome = $"BOT {ContarBots() + 1}", Bot = true, Pronto = true });
            Redesenhar();
        }

        private void RemoverUltimoBot()
        {
            for (int i = sala.Count - 1; i >= 0; i--)
            {
                if (!sala[i].Bot)
                    continue;

                sala.RemoveAt(i);
                Redesenhar();
                return;
            }
        }

        private void Remover(int indice)
        {
            if (!souAnfitriao || indice < 0 || indice >= sala.Count)
                return;

            if (sala[indice].Local)
                return;

            sala.RemoveAt(indice);
            Redesenhar();
        }

        private void CopiarCodigo()
        {
            if (codigoDaSala != null)
                GUIUtility.systemCopyBuffer = codigoDaSala.text;
        }

        private void Iniciar()
        {
            if (!PodeIniciar())
                return;

            if (fluxo != null)
                fluxo.Correr();
        }

        // ---------------------------------------------------------------- Estado

        private int ContarBots()
        {
            int n = 0;
            for (int i = 0; i < sala.Count; i++)
            {
                if (sala[i].Bot)
                    n++;
            }

            return n;
        }

        private int ContarAguardando()
        {
            int n = 0;
            for (int i = 0; i < sala.Count; i++)
            {
                if (!sala[i].Pronto)
                    n++;
            }

            return n;
        }

        private bool PodeIniciar() => souAnfitriao && sala.Count >= 2 && ContarAguardando() == 0;

        private string MotivoDoBloqueio()
        {
            if (!souAnfitriao)
                return "SÓ O ANFITRIÃO INICIA";

            if (sala.Count < 2)
                return "MÍNIMO 2 CORREDORES";

            int esperando = ContarAguardando();
            if (esperando == 1)
                return "FALTA 1 CONFIRMAR";
            if (esperando > 1)
                return $"FALTAM {esperando} CONFIRMAR";

            return string.Empty;
        }

        // ---------------------------------------------------------------- Desenho

        private void Redesenhar()
        {
            for (int i = 0; i < vagas.Count; i++)
            {
                Vaga vaga = vagas[i];
                if (vaga == null || vaga.raiz == null)
                    continue;

                bool ocupada = i < sala.Count;

                Ligar(vaga.estadoVazio, !ocupada);
                Ligar(vaga.estadoJogador, ocupada && !sala[i].Bot);
                Ligar(vaga.estadoBot, ocupada && sala[i].Bot);

                if (!ocupada)
                    continue;

                Ocupante o = sala[i];

                if (vaga.indice != null)
                    vaga.indice.text = (i + 1).ToString("00");

                if (vaga.nome != null)
                    vaga.nome.text = o.Local ? $"{o.Nome} (VOCÊ)" : o.Nome;

                Ligar(vaga.seloDeAnfitriao, o.Anfitriao);
                Ligar(vaga.estadoPronto, o.Pronto);
                Ligar(vaga.estadoAguardando, !o.Pronto);

                // Remover aparece só para o anfitrião e nunca na própria linha: um botão que
                // expulsaria você mesmo é um acidente esperando acontecer.
                Ligar(vaga.btnRemover != null ? vaga.btnRemover.gameObject : null,
                      souAnfitriao && !o.Local);
            }

            if (contador != null)
                contador.text = $"{sala.Count}/{vagas.Count}";

            if (contadorDeBots != null)
                contadorDeBots.text = $"{ContarBots()} BOTS";

            if (btnAdicionarBot != null)
                btnAdicionarBot.interactable = souAnfitriao && sala.Count < vagas.Count;

            if (btnRemoverBot != null)
                btnRemoverBot.interactable = souAnfitriao && ContarBots() > 0;

            Ocupante local = sala.Find(x => x.Local);
            bool localPronto = local != null && local.Pronto;
            Ligar(btnProntoEstadoPronto, localPronto);
            Ligar(btnProntoEstadoAguardando, !localPronto);

            bool pode = PodeIniciar();
            Ligar(iniciarHabilitado, pode);
            Ligar(iniciarDesabilitado, !pode);

            if (btnIniciar != null)
                btnIniciar.interactable = pode;

            if (motivoDoBloqueio != null)
                motivoDoBloqueio.text = MotivoDoBloqueio();

            RedesenharMapa();
        }

        private void RedesenharMapa()
        {
            TrackDefinition pista = seletorDePista != null ? seletorDePista.Atual : null;
            if (pista == null)
                return;

            if (nomeDoMapa != null)
                nomeDoMapa.text = pista.nome;

            if (descricaoDoMapa != null)
                descricaoDoMapa.text = pista.descricao;

            if (previewDoMapa != null && pista.miniatura != null)
                previewDoMapa.sprite = pista.miniatura;
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
