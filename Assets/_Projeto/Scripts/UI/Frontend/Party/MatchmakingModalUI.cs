using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>
    /// Binder do modal BUSCANDO PARTIDA (Screen_Matchmaking). É um modal sobre o lobby, não uma
    /// tela nova: o jogador não perde de vista o grupo enquanto espera, e cancelar é fechar em vez
    /// de "voltar".
    ///
    /// A metáfora é SINTONIZAR o canal da oficina, não um spinner. A agulha varre o dial, cada
    /// piloto encontrado acende um blip e a faixa de etapas conta o progresso em palavras.
    ///
    /// O que esta tela NUNCA mostra: o limite de 40 s. O jogador vê o tempo DECORRIDO. Uma
    /// contagem regressiva ensina a esperar o estouro; o tempo decorrido só diz "estamos
    /// procurando", e a cor do número faz o trabalho de urgência sem prometer prazo.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchmakingModalUI : MonoBehaviour
    {
        /// <summary>Um card da grade de 16 vagas, já montado com seus quatro estados.</summary>
        [System.Serializable]
        public class CardDeVaga
        {
            public GameObject raiz;
            [Tooltip("Companheiro do meu grupo — contorno âmbar.")]
            public GameObject estadoCompanheiro;
            public GameObject estadoHumano;
            public GameObject estadoBot;
            public GameObject estadoVazio;
            public TextMeshProUGUI nome;
            [Tooltip("Quadrado de identidade. A cor vem do nome do piloto.")]
            public UnityEngine.UI.Graphic avatar;
        }

        /// <summary>Uma chapinha da faixa de progresso, com os três estados do handoff.</summary>
        [System.Serializable]
        public class ChapinhaDeEtapa
        {
            public MatchmakingStage etapa;
            public GameObject feito;
            public GameObject agora;
            public GameObject aFazer;
        }

        [Header("Raiz do modal")]
        [SerializeField] private GameObject raiz;

        [Header("Dial de rádio")]
        [Tooltip("Componente de vaivém na agulha (UIShineSweep com 'vaivem' ligado, período 2,6 s). " +
                 "O movimento mora no componente de motion; aqui só se liga e se desliga.")]
        [SerializeField] private PartyRacers.UI.Motion.UIShineSweep varreduraDaAgulha;
        [Tooltip("Agulha travada em verde quando a busca fecha.")]
        [SerializeField] private GameObject agulhaTravada;
        [SerializeField] private GameObject agulhaVarrendo;

        [Header("Blips de piloto")]
        [Tooltip("Blips já montados no dial. Um é aceso por piloto encontrado.")]
        [SerializeField] private List<GameObject> blips = new List<GameObject>();
        [SerializeField] private List<TextMeshProUGUI> nomesDosBlips = new List<TextMeshProUGUI>();

        [Header("Tempo decorrido")]
        [SerializeField] private TextMeshProUGUI textoDoTempo;
        [Tooltip("Estados de cor do cronômetro. Nenhum deles diz qual é o limite.")]
        [SerializeField] private GameObject tempoNormal;
        [SerializeField] private GameObject tempoAtencao;
        [SerializeField] private GameObject tempoCritico;
        [SerializeField, Range(0f, 1f)] private float fracaoAtencao = 0.625f;   // 25 s de 40
        [SerializeField, Range(0f, 1f)] private float fracaoCritica = 0.875f;   // 35 s de 40

        [Header("Faixa de etapas")]
        [SerializeField] private List<ChapinhaDeEtapa> chapinhas = new List<ChapinhaDeEtapa>();

        [Header("Sala em formação")]
        [SerializeField] private List<CardDeVaga> vagas = new List<CardDeVaga>();
        [SerializeField] private TextMeshProUGUI contadorDeJogadores;
        [SerializeField] private TextMeshProUGUI nomeDoMapa;
        [SerializeField] private GameObject blocoDoMapa;

        [Header("Ações")]
        [SerializeField] private Button btnCancelar;

        [Header("Fluxo")]
        [SerializeField] private PartyController controlador;

        private int blipsAcesos;

        private MatchmakingService Servico => controlador != null ? controlador.Matchmaking : null;

        // ---------------------------------------------------------------- Ciclo

        private void Awake()
        {
            if (btnCancelar != null)
                btnCancelar.onClick.AddListener(Cancelar);
        }

        private void OnEnable()
        {
            MatchmakingService servico = Servico;
            if (servico == null)
                return;

            servico.StageChanged += AoTrocarEtapa;
            servico.SlotAdded += AoEntrarCorredor;
        }

        private void OnDisable()
        {
            MatchmakingService servico = Servico;
            if (servico == null)
                return;

            servico.StageChanged -= AoTrocarEtapa;
            servico.SlotAdded -= AoEntrarCorredor;
        }

        public void Abrir()
        {
            blipsAcesos = 0;

            ApagarBlips();
            LimparVagas();

            Ligar(raiz, true);
            Ligar(blocoDoMapa, false);

            AoTrocarEtapa(Servico != null ? Servico.Stage : MatchmakingStage.WaitingParty);
        }

        public void Fechar() => Ligar(raiz, false);

        private void Cancelar()
        {
            controlador?.CancelarBusca();
            Fechar();
        }

        private void Update()
        {
            MatchmakingService servico = Servico;
            if (servico == null || raiz == null || !raiz.activeSelf)
                return;

            AtualizarAgulha(servico);
            AtualizarTempo(servico);
            AtualizarSala(servico);
        }

        // ---------------------------------------------------------------- Dial

        private void AtualizarAgulha(MatchmakingService servico)
        {
            // A agulha só varre enquanto se procura. Fechada a busca ela trava em verde: é o
            // sinal de "achei", e uma agulha que continuasse varrendo diria o contrário.
            bool varrendo = servico.Stage == MatchmakingStage.Searching;

            Ligar(agulhaVarrendo, varrendo);
            Ligar(agulhaTravada, !varrendo && servico.Stage != MatchmakingStage.WaitingParty);

            if (varreduraDaAgulha != null && varreduraDaAgulha.gameObject.activeSelf != varrendo)
                varreduraDaAgulha.gameObject.SetActive(varrendo);
        }

        private void AoEntrarCorredor(MatchSlot slot)
        {
            // Bot não acende blip: o dial mostra QUEM foi encontrado de verdade. Acender blip de
            // bot faria o preenchimento parecer sucesso da busca.
            if (slot.Bot || blipsAcesos >= blips.Count)
                return;

            GameObject blip = blips[blipsAcesos];
            if (blip != null)
                blip.SetActive(true);

            if (blipsAcesos < nomesDosBlips.Count && nomesDosBlips[blipsAcesos] != null)
                nomesDosBlips[blipsAcesos].text = slot.Nome;

            blipsAcesos++;
        }

        private void ApagarBlips()
        {
            foreach (GameObject blip in blips)
                Ligar(blip, false);
        }

        // ---------------------------------------------------------------- Tempo

        private void AtualizarTempo(MatchmakingService servico)
        {
            if (textoDoTempo != null)
            {
                int segundos = Mathf.FloorToInt(servico.ElapsedSearch);
                textoDoTempo.text = $"{segundos / 60:00}:{segundos % 60:00}";
            }

            float f = servico.Search01;
            bool critico = f >= fracaoCritica;
            bool atencao = !critico && f >= fracaoAtencao;

            Ligar(tempoCritico, critico);
            Ligar(tempoAtencao, atencao);
            Ligar(tempoNormal, !critico && !atencao);
        }

        // ---------------------------------------------------------------- Etapas e sala

        private void AoTrocarEtapa(MatchmakingStage etapa)
        {
            foreach (ChapinhaDeEtapa chapinha in chapinhas)
            {
                if (chapinha == null)
                    continue;

                // Done/Now/Todo são FILHOS, não cores calculadas: a faixa inteira já está montada
                // na cena e o binder só escolhe qual filho de cada chapinha aparece.
                bool feito = chapinha.etapa < etapa;
                bool agora = chapinha.etapa == etapa;

                Ligar(chapinha.feito, feito);
                Ligar(chapinha.agora, agora);
                Ligar(chapinha.aFazer, !feito && !agora);
            }

            if (etapa == MatchmakingStage.MatchFound || etapa == MatchmakingStage.LoadingMap)
            {
                Ligar(blocoDoMapa, true);

                if (nomeDoMapa != null && Servico != null)
                    nomeDoMapa.text = Servico.MapaSorteado ?? "SORTEADO PELO JOGO";
            }
        }

        private void AtualizarSala(MatchmakingService servico)
        {
            IReadOnlyList<MatchSlot> slots = servico.Slots;

            for (int i = 0; i < vagas.Count; i++)
            {
                CardDeVaga vaga = vagas[i];
                if (vaga == null || vaga.raiz == null)
                    continue;

                bool ocupada = i < slots.Count;

                // O quadrado de identidade só faz sentido com alguém na vaga; vazio ele seria uma
                // mancha colorida sugerindo que há um piloto ali.
                if (vaga.avatar != null)
                {
                    vaga.avatar.enabled = ocupada;
                    if (ocupada)
                        vaga.avatar.color = PlayerTint.De(slots[i].Nome);
                }

                if (!ocupada)
                {
                    Ligar(vaga.estadoCompanheiro, false);
                    Ligar(vaga.estadoHumano, false);
                    Ligar(vaga.estadoBot, false);
                    Ligar(vaga.estadoVazio, true);

                    // O nome fica no card, não no estado: sem limpar, a vaga vazia continuava
                    // exibindo quem esteve ali na busca anterior.
                    if (vaga.nome != null)
                        vaga.nome.text = string.Empty;

                    continue;
                }

                MatchSlot slot = slots[i];

                Ligar(vaga.estadoCompanheiro, slot.DoMeuGrupo);
                Ligar(vaga.estadoHumano, !slot.DoMeuGrupo && !slot.Bot);
                Ligar(vaga.estadoBot, slot.Bot);
                Ligar(vaga.estadoVazio, false);

                if (vaga.nome != null)
                    vaga.nome.text = slot.Nome;
            }

            if (contadorDeJogadores != null)
                contadorDeJogadores.text = $"{slots.Count}/{servico.JogadoresPorSala}";
        }

        private void LimparVagas()
        {
            foreach (CardDeVaga vaga in vagas)
            {
                if (vaga == null)
                    continue;

                Ligar(vaga.estadoCompanheiro, false);
                Ligar(vaga.estadoHumano, false);
                Ligar(vaga.estadoBot, false);
                Ligar(vaga.estadoVazio, true);
            }
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
