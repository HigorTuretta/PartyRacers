using System.Collections.Generic;
using PartyRacers.Race;
using PartyRacers.UI.Garage;
using PartyRacers.UI.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Binder da PARTIDA PERSONALIZADA (Screen_CustomMatch). Aqui manda a administração da sala,
    /// não o carro: o palco 3D recua e a lista de 16 ocupa a tela.
    ///
    /// Três regras de leitura que a tela existe para cumprir:
    /// • 16 vagas em DUAS COLUNAS DE 8, sem rolagem. Rolagem numa lista de sala é o que faz o
    ///   anfitrião perder de vista quem ainda não confirmou.
    /// • Bot e humano nunca se parecem. O bot tem cor própria (violeta) e rótulo próprio; um bot
    ///   disfarçado de jogador faz o anfitrião esperar uma confirmação que nunca vem.
    /// • Vaga livre não é espaço morto: ela É o botão de convite. Um "+" no lugar onde a pessoa vai
    ///   aparecer diz o que o convite faz sem precisar de legenda.
    ///
    /// Diferente do matchmaking público, aqui o MAPA e as REGRAS são manuais — e as regras valem de
    /// verdade na corrida, via <see cref="RaceRules"/>.
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
            [Tooltip("Quadradinho ao lado do nome: recebe a foto do kart do jogador.")]
            public Image miniatura;
            public GameObject seloDeAnfitriao;
            public GameObject estadoPronto;
            public GameObject estadoAguardando;
            [Tooltip("Só aparece para o anfitrião, e nunca na própria linha dele.")]
            public Button btnRemover;

            [Header("Peças de dentro de State_Empty")]
            [Tooltip("A vaga livre inteira é clicável: convidar.")]
            public Button btnConvidar;
            public TextMeshProUGUI indiceVazio;
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
        [Tooltip("Linha curta de retorno do convite. Some sozinha.")]
        [SerializeField] private TextMeshProUGUI avisoDoConvite;

        [Header("Mapa (manual — diferente do público)")]
        [Tooltip("Catálogo de pistas jogáveis. Hoje: DEMO e MiniGolfeRun.")]
        [SerializeField] private List<TrackDefinition> pistas = new List<TrackDefinition>();
        [SerializeField] private TextMeshProUGUI nomeDoMapa;
        [SerializeField] private TextMeshProUGUI descricaoDoMapa;
        [SerializeField] private Image previewDoMapa;
        [SerializeField] private TextMeshProUGUI resumoDoMapa;
        [SerializeField] private Button btnMapaAnterior;
        [SerializeField] private Button btnMapaProximo;
        [Tooltip("Fileira de tracinhos abaixo do cartão: um por pista.")]
        [SerializeField] private Transform pontosDoMapa;
        [SerializeField] private Color pontoAtivo = new Color(1f, 0.69f, 0.13f);
        [SerializeField] private Color pontoInativo = new Color(0.29f, 0.33f, 0.66f);

        [Header("Regras (valem na corrida)")]
        [SerializeField] private Regra voltas;
        [SerializeField] private Regra itens;
        [SerializeField] private Regra botsPreenchem;
        [SerializeField] private Regra danoPorColisao;

        /// <summary>Uma linha de regra: ◄ valor ►.</summary>
        [System.Serializable]
        public class Regra
        {
            public Button btnAnterior;
            public Button btnProximo;
            public TextMeshProUGUI valor;
        }

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
        [Tooltip("Fotografa o kart do jogador para a miniatura da faixa de nome.")]
        [SerializeField] private PreviewStudio estudio;
        [Tooltip("Quem comanda a sala vê os controles de remover e iniciar.")]
        [SerializeField] private bool souAnfitriao = true;

        private const string ChaveCodigo = "sala.codigo";
        private static readonly int[] VoltasPossiveis = { 1, 2, 3, 5, 7 };

        private readonly List<Ocupante> sala = new List<Ocupante>();
        private int pista;
        private float avisoAte;
        private Sprite miniaturaDoKart;

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
                btnCopiarCodigo.onClick.AddListener(Convidar);

            if (btnMapaAnterior != null)
                btnMapaAnterior.onClick.AddListener(() => TrocarMapa(-1));

            if (btnMapaProximo != null)
                btnMapaProximo.onClick.AddListener(() => TrocarMapa(+1));

            Passo(voltas, () => Mexer(ref indiceDeVoltas, -1, VoltasPossiveis.Length),
                         () => Mexer(ref indiceDeVoltas, +1, VoltasPossiveis.Length));
            Passo(itens, AlternarItens, AlternarItens);
            Passo(botsPreenchem, AlternarBots, AlternarBots);
            Passo(danoPorColisao, AlternarDano, AlternarDano);

            for (int i = 0; i < vagas.Count; i++)
            {
                int indice = i;

                if (vagas[i]?.btnRemover != null)
                    vagas[i].btnRemover.onClick.AddListener(() => Remover(indice));

                if (vagas[i]?.btnConvidar != null)
                    vagas[i].btnConvidar.onClick.AddListener(Convidar);
            }
        }

        private static void Passo(Regra r, System.Action menos, System.Action mais)
        {
            if (r == null)
                return;

            if (r.btnAnterior != null)
                r.btnAnterior.onClick.AddListener(() => menos());

            if (r.btnProximo != null)
                r.btnProximo.onClick.AddListener(() => mais());
        }

        private int indiceDeVoltas;

        private void OnEnable()
        {
            RaceRules.Carregar();

            indiceDeVoltas = System.Array.IndexOf(VoltasPossiveis, RaceRules.Voltas);
            if (indiceDeVoltas < 0)
                indiceDeVoltas = System.Array.IndexOf(VoltasPossiveis, 3);

            pista = Mathf.Max(0, Jogaveis.FindIndex(p => p.cena == RaceRules.Pista));

            if (sala.Count == 0)
            {
                sala.Add(new Ocupante
                {
                    Nome = NomeDoJogador(),
                    Anfitriao = souAnfitriao,
                    Local = true,
                    Pronto = true,
                });
            }
            else
            {
                sala[0].Nome = NomeDoJogador();
            }

            PedirMiniatura();
            Redesenhar();
        }

        private static string NomeDoJogador()
        {
            string nome = PlayerPrefs.GetString("jogador.nome", "JOGADOR").Trim();
            return string.IsNullOrEmpty(nome) ? "JOGADOR" : nome.ToUpperInvariant();
        }

        private void Update()
        {
            if (avisoDoConvite == null || avisoAte <= 0f || Time.unscaledTime < avisoAte)
                return;

            avisoAte = 0f;
            avisoDoConvite.text = string.Empty;
        }

        // ---------------------------------------------------------------- Miniatura do kart

        /// <summary>
        /// Põe o kart do jogador na faixa de nome dele.
        ///
        /// A foto vem do mesmo estúdio da garagem, então é o carro de verdade — modelo, peças e
        /// tinta equipados —, não um ícone genérico. Chega um ou dois frames depois; até lá a
        /// caixinha fica com a cor de identidade, que já distingue as linhas.
        /// </summary>
        private void PedirMiniatura()
        {
            if (estudio == null)
                return;

            estudio.PedirCarroDoJogador(tex =>
            {
                if (tex == null)
                    return;

                miniaturaDoKart = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                                new Vector2(0.5f, 0.5f));
                Redesenhar();
            });
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

        /// <summary>
        /// Convida: cria a sala online se houver online, e põe o código na área de transferência.
        ///
        /// Quem tem a verdade sobre o código é o Relay — por isso o pedido passa pelo fluxo, que já
        /// sabe hospedar. Sem online, a sala continua tendo um código próprio e o aviso diz que a
        /// partida é local; um botão que não faz nada e não explica é pior que um botão ausente.
        /// </summary>
        private void Convidar()
        {
            string codigo = Codigo();

            if (fluxo != null && fluxo.Online)
            {
                fluxo.Convidar();
                codigo = string.IsNullOrEmpty(fluxo.ConviteAtual()) ? codigo : fluxo.ConviteAtual();
                Avisar($"CÓDIGO {codigo} COPIADO — MANDE PARA A GALERA");
            }
            else if (fluxo != null)
            {
                // Tenta subir a sala; se o serviço não responder, o fluxo avisa e ficamos no local.
                fluxo.Convidar();
                Avisar($"CÓDIGO {codigo} COPIADO — PARTIDA LOCAL ATÉ O ONLINE SUBIR");
            }
            else
            {
                Avisar($"CÓDIGO {codigo} COPIADO");
            }

            GUIUtility.systemCopyBuffer = codigo;
            Redesenhar();
        }

        private void Avisar(string texto)
        {
            if (avisoDoConvite == null)
                return;

            avisoDoConvite.text = texto;
            avisoAte = Time.unscaledTime + 4f;
        }

        /// <summary>
        /// Código da sala. Sorteado uma vez e guardado: um código que muda a cada vez que a tela
        /// abre é um código que ninguém consegue passar para o amigo a tempo.
        /// </summary>
        private string Codigo()
        {
            if (fluxo != null && !string.IsNullOrEmpty(fluxo.ConviteAtual()))
                return fluxo.ConviteAtual();

            string salvo = PlayerPrefs.GetString(ChaveCodigo, string.Empty);
            if (salvo.Length == 6)
                return salvo;

            const string alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var sb = new System.Text.StringBuilder(6);
            for (int i = 0; i < 6; i++)
                sb.Append(alfabeto[Random.Range(0, alfabeto.Length)]);

            salvo = sb.ToString();
            PlayerPrefs.SetString(ChaveCodigo, salvo);
            PlayerPrefs.Save();
            return salvo;
        }

        private void Iniciar()
        {
            if (!PodeIniciar() || fluxo == null)
                return;

            SalvarRegras();

            TrackDefinition p = PistaAtual;
            if (p != null)
                fluxo.CorrerEm(p.cena);
            else
                fluxo.Correr();
        }

        // ---------------------------------------------------------------- Mapa

        private List<TrackDefinition> Jogaveis
        {
            get
            {
                var lista = new List<TrackDefinition>();
                foreach (TrackDefinition p in pistas)
                    if (p != null && p.Jogavel)
                        lista.Add(p);

                lista.Sort((a, b) => a.ordem.CompareTo(b.ordem));
                return lista;
            }
        }

        private TrackDefinition PistaAtual
        {
            get
            {
                List<TrackDefinition> lista = Jogaveis;
                return lista.Count == 0 ? null : lista[Mathf.Clamp(pista, 0, lista.Count - 1)];
            }
        }

        private void TrocarMapa(int passo)
        {
            List<TrackDefinition> lista = Jogaveis;
            if (lista.Count <= 1)
                return;

            pista = ((pista + passo) % lista.Count + lista.Count) % lista.Count;
            SalvarRegras();
            Redesenhar();
        }

        // ---------------------------------------------------------------- Regras

        private void Mexer(ref int indice, int passo, int total)
        {
            indice = ((indice + passo) % total + total) % total;
            SalvarRegras();
            Redesenhar();
        }

        private void AlternarItens()
        {
            RaceRules.Itens = !RaceRules.Itens;
            SalvarRegras();
            Redesenhar();
        }

        private void AlternarBots()
        {
            RaceRules.BotsPreenchem = !RaceRules.BotsPreenchem;
            SalvarRegras();
            Redesenhar();
        }

        private void AlternarDano()
        {
            RaceRules.DanoPorColisao = !RaceRules.DanoPorColisao;
            SalvarRegras();
            Redesenhar();
        }

        private void SalvarRegras()
        {
            RaceRules.Voltas = VoltasPossiveis[Mathf.Clamp(indiceDeVoltas, 0, VoltasPossiveis.Length - 1)];
            TrackDefinition p = PistaAtual;
            RaceRules.Pista = p != null ? p.cena : string.Empty;
            RaceRules.Salvar();
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

        // Um humano sozinho pode largar quando os bots preenchem a grade: a corrida acontece de
        // verdade, com adversários. Sem preenchimento, correr sozinho não é partida.
        private bool PodeIniciar() =>
            souAnfitriao && ContarAguardando() == 0
            && (sala.Count >= 2 || RaceRules.BotsPreenchem);

        private string MotivoDoBloqueio()
        {
            if (!souAnfitriao)
                return "SÓ O ANFITRIÃO INICIA";

            int esperando = ContarAguardando();
            if (esperando == 1)
                return "FALTA 1 CONFIRMAR";
            if (esperando > 1)
                return $"FALTAM {esperando} CONFIRMAR";

            if (sala.Count < 2 && !RaceRules.BotsPreenchem)
                return "CONVIDE ALGUÉM OU LIGUE OS BOTS";

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

                // O conteúdo da linha é o MESMO para humano e bot — número, miniatura, nome. O que
                // muda é a moldura violeta com a etiqueta BOT por cima. Trocar a linha inteira
                // obrigaria a manter dois layouts iguais e a escrever o nome do bot duas vezes.
                Ligar(vaga.estadoVazio, !ocupada);
                Ligar(vaga.estadoJogador, ocupada);
                Ligar(vaga.estadoBot, ocupada && sala[i].Bot);

                if (vaga.indiceVazio != null)
                    vaga.indiceVazio.text = (i + 1).ToString("00");

                if (!ocupada)
                    continue;

                Ocupante o = sala[i];

                if (vaga.indice != null)
                    vaga.indice.text = (i + 1).ToString("00");

                if (vaga.nome != null)
                    vaga.nome.text = o.Local ? $"{o.Nome} (VOCÊ)" : o.Nome;

                Miniatura(vaga, o);

                Ligar(vaga.seloDeAnfitriao, o.Anfitriao);
                Ligar(vaga.estadoPronto, o.Pronto);
                Ligar(vaga.estadoAguardando, !o.Pronto);

                // Remover aparece só para o anfitrião e nunca na própria linha: um botão que
                // expulsaria você mesmo é um acidente esperando acontecer.
                Ligar(vaga.btnRemover != null ? vaga.btnRemover.gameObject : null,
                      souAnfitriao && !o.Local);
            }

            if (contador != null)
                contador.text = $"{sala.Count} / {vagas.Count}";

            if (codigoDaSala != null)
                codigoDaSala.text = Codigo();

            if (contadorDeBots != null)
                contadorDeBots.text = ContarBots() == 1 ? "1 BOT" : $"{ContarBots()} BOTS";

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
            RedesenharRegras();
        }

        /// <summary>
        /// A miniatura é do kart do JOGADOR LOCAL. Para os outros ela fica com a cor de identidade
        /// deles — o projeto não recebe o visual de quem não está na máquina, e desenhar um kart
        /// qualquer no lugar seria informação inventada.
        /// </summary>
        private void Miniatura(Vaga vaga, Ocupante o)
        {
            if (vaga.miniatura == null)
                return;

            bool temFoto = o.Local && miniaturaDoKart != null;

            vaga.miniatura.sprite = temFoto ? miniaturaDoKart : null;
            vaga.miniatura.preserveAspect = true;
            vaga.miniatura.color = temFoto ? Color.white : PlayerTint.De(o.Nome);
            vaga.miniatura.enabled = true;
        }

        private void RedesenharMapa()
        {
            List<TrackDefinition> lista = Jogaveis;
            TrackDefinition p = PistaAtual;

            bool varias = lista.Count > 1;
            Ligar(btnMapaAnterior != null ? btnMapaAnterior.gameObject : null, varias);
            Ligar(btnMapaProximo != null ? btnMapaProximo.gameObject : null, varias);

            if (p == null)
            {
                if (nomeDoMapa != null)
                    nomeDoMapa.text = "SEM PISTA";
                return;
            }

            if (nomeDoMapa != null)
                nomeDoMapa.text = p.nome;

            if (descricaoDoMapa != null)
                descricaoDoMapa.text = p.descricao;

            if (previewDoMapa != null)
            {
                previewDoMapa.sprite = p.miniatura;
                previewDoMapa.color = p.miniatura != null ? Color.white : previewDoMapa.color;
                previewDoMapa.preserveAspect = true;
            }

            if (resumoDoMapa != null)
            {
                int v = VoltasPossiveis[Mathf.Clamp(indiceDeVoltas, 0, VoltasPossiveis.Length - 1)];
                resumoDoMapa.text = v == 1 ? "1 VOLTA" : $"{v} VOLTAS";
            }

            if (pontosDoMapa == null)
                return;

            int k = 0;
            foreach (Transform ponto in pontosDoMapa)
            {
                bool existe = k < lista.Count;
                if (ponto.gameObject.activeSelf != existe)
                    ponto.gameObject.SetActive(existe);

                var forma = ponto.GetComponent<Graphic>();
                if (existe && forma != null)
                    forma.color = k == pista ? pontoAtivo : pontoInativo;

                k++;
            }
        }

        private void RedesenharRegras()
        {
            Escrever(voltas, VoltasPossiveis[Mathf.Clamp(indiceDeVoltas, 0, VoltasPossiveis.Length - 1)]
                             .ToString());
            Escrever(itens, RaceRules.Itens ? "TODOS" : "NENHUM");
            Escrever(botsPreenchem, RaceRules.BotsPreenchem ? "SIM" : "NÃO");
            Escrever(danoPorColisao, RaceRules.DanoPorColisao ? "LIGADO" : "DESLIGADO");
        }

        private static void Escrever(Regra r, string texto)
        {
            if (r?.valor != null)
                r.valor.text = texto;
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
