using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.UI.Frontend.Party
{
    /// <summary>Etapas do fluxo de busca. A faixa de progresso da tela desenha exatamente estas.</summary>
    public enum MatchmakingStage
    {
        /// <summary>Esperando o grupo inteiro confirmar. A agulha do dial fica parada.</summary>
        WaitingParty,
        /// <summary>Procurando humanos. A agulha varre e cada piloto encontrado vira um blip.</summary>
        Searching,
        /// <summary>Fecha a lista de humanos.</summary>
        PlayersFound,
        /// <summary>Preenchendo as vagas restantes com bots.</summary>
        FillingWithBots,
        /// <summary>Sala montada, mapa sorteado.</summary>
        MatchFound,
        /// <summary>Carregando a pista.</summary>
        LoadingMap,
    }

    /// <summary>Um corredor da sala em formação — humano do grupo, humano de fora ou bot.</summary>
    public readonly struct MatchSlot
    {
        public MatchSlot(string nome, bool bot, bool doMeuGrupo)
        {
            Nome = nome;
            Bot = bot;
            DoMeuGrupo = doMeuGrupo;
        }

        public string Nome { get; }
        public bool Bot { get; }
        /// <summary>Membros do grupo aparecem primeiro na grade e com contorno âmbar.</summary>
        public bool DoMeuGrupo { get; }
    }

    /// <summary>
    /// Busca de partida pública (handoff v2 §6).
    ///
    /// Regras que este serviço existe para garantir:
    /// • A busca NUNCA passa de 40 s. Ao bater o limite, fecha a lista de humanos encontrados,
    ///   completa as 16 vagas com bots e sorteia o mapa.
    /// • O limite de 40 s é regra interna e NÃO aparece na tela: o jogador vê o tempo decorrido,
    ///   não uma contagem regressiva. Um cronômetro correndo para o fim ensina a esperar o
    ///   estouro; o tempo decorrido só diz "estamos procurando".
    /// • Não há escolha de mapa no público — é sorteado.
    ///
    /// Hoje os "humanos encontrados" são simulados em ritmo decrescente, para que a tela possa ser
    /// construída e testada antes do NGO. Trocar a simulação pelo serviço real é trocar o corpo de
    /// <see cref="TentarEncontrarJogador"/>: nem a máquina de estados nem a tela mudam.
    /// </summary>
    [DisallowMultipleComponent]
    public class MatchmakingService : MonoBehaviour
    {
        [Header("Regras (tokens-v2 → gameplay.matchmaking)")]
        [Tooltip("Teto absoluto da busca em segundos. NÃO é exibido na tela.")]
        [SerializeField, Min(5f)] private float limiteDeBusca = 40f;
        [SerializeField, Min(2)] private int jogadoresPorSala = 16;

        [Header("Ritmo das etapas")]
        [SerializeField] private float duracaoJogadoresEncontrados = 0.9f;
        [SerializeField] private float duracaoPreenchendoBots = 1.2f;
        [SerializeField] private float duracaoPartidaEncontrada = 1.1f;

        [Header("Simulação de encontro (provisório, até o NGO)")]
        [Tooltip("Intervalo inicial entre dois humanos encontrados.")]
        [SerializeField] private float intervaloInicial = 1.4f;
        [Tooltip("Intervalo no fim da janela — a fila esvazia e os encontros ficam raros.")]
        [SerializeField] private float intervaloFinal = 4.5f;
        [SerializeField]
        private string[] nomesSimulados =
        {
            "LEO_99", "MARINA", "TIAGO", "PEDRO_H", "BIA", "NANDO", "KIKA", "ZECA",
            "DUDA", "RAFA", "MEL", "JOCA", "VIVI", "TOM", "LARA",
        };

        [Header("Mapas sorteáveis")]
        [Tooltip("Cenas de pista do sorteio do público. Vazio = usa a cena configurada no fluxo.")]
        [SerializeField] private string[] mapas = { "MiniGolfeRun", "DEMO" };

        // ---------------------------------------------------------------- Estado

        public MatchmakingStage Stage { get; private set; } = MatchmakingStage.WaitingParty;
        public bool Running { get; private set; }

        /// <summary>Segundos DECORRIDOS de busca. É o único número de tempo que a tela mostra.</summary>
        public float ElapsedSearch { get; private set; }

        /// <summary>Progresso 0..1 dentro do teto de busca. Alimenta a cor do cronômetro.</summary>
        public float Search01 => limiteDeBusca > 0f ? Mathf.Clamp01(ElapsedSearch / limiteDeBusca) : 0f;

        public string MapaSorteado { get; private set; }

        private readonly List<MatchSlot> slots = new List<MatchSlot>();
        public IReadOnlyList<MatchSlot> Slots => slots;

        public int Humanos { get; private set; }
        public int Bots => Mathf.Max(0, slots.Count - Humanos);
        public int JogadoresPorSala => jogadoresPorSala;

        /// <summary>Mudou de etapa. A faixa de progresso e a agulha do dial reagem a isto.</summary>
        public event System.Action<MatchmakingStage> StageChanged;

        /// <summary>Entrou um corredor novo na sala. O dial cria um blip por chamada.</summary>
        public event System.Action<MatchSlot> SlotAdded;

        /// <summary>Sala pronta: o fluxo carrega o mapa.</summary>
        public event System.Action<string> MatchReady;

        private PartyState grupo;
        private float proximoEncontro;
        private float fimDaEtapa;
        private int proximoNome;

        // ---------------------------------------------------------------- Controle

        /// <summary>Começa a busca. Só o líder chama, e só com o grupo inteiro pronto.</summary>
        public void Iniciar(PartyState party)
        {
            if (Running)
                return;

            grupo = party;
            slots.Clear();
            Humanos = 0;
            ElapsedSearch = 0f;
            proximoNome = 0;
            MapaSorteado = null;

            // O grupo entra na sala primeiro e em bloco — é o que garante que os amigos fiquem
            // juntos na grade em vez de espalhados entre desconhecidos.
            if (grupo != null)
            {
                foreach (PartyMember membro in grupo.Members)
                    Adicionar(new MatchSlot(membro.DisplayName, false, true));
            }

            Running = true;
            proximoEncontro = IntervaloAtual();
            TrocarEtapa(MatchmakingStage.Searching);
        }

        /// <summary>Cancela e volta para o lobby. O grupo continua montado.</summary>
        public void Cancelar()
        {
            if (!Running)
                return;

            Running = false;
            slots.Clear();
            Humanos = 0;
            ElapsedSearch = 0f;
            TrocarEtapa(MatchmakingStage.WaitingParty);
        }

        private void Update()
        {
            if (!Running)
                return;

            switch (Stage)
            {
                case MatchmakingStage.Searching:
                    AtualizarBusca();
                    break;

                case MatchmakingStage.PlayersFound:
                    if (Time.time >= fimDaEtapa)
                        ComecarPreenchimento();
                    break;

                case MatchmakingStage.FillingWithBots:
                    if (Time.time >= fimDaEtapa)
                        ConcluirSala();
                    break;

                case MatchmakingStage.MatchFound:
                    if (Time.time >= fimDaEtapa)
                        Carregar();
                    break;
            }
        }

        // ---------------------------------------------------------------- Etapas

        private void AtualizarBusca()
        {
            ElapsedSearch += Time.deltaTime;

            // Sala cheia de gente de verdade antes do limite: não há por que esperar os 40 s.
            if (slots.Count >= jogadoresPorSala)
            {
                FecharBusca();
                return;
            }

            if (ElapsedSearch >= limiteDeBusca)
            {
                FecharBusca();
                return;
            }

            proximoEncontro -= Time.deltaTime;
            if (proximoEncontro > 0f)
                return;

            proximoEncontro = IntervaloAtual();
            TentarEncontrarJogador();
        }

        /// <summary>Ponto de troca para o matchmaking real: hoje inventa um humano plausível.</summary>
        private void TentarEncontrarJogador()
        {
            if (slots.Count >= jogadoresPorSala || nomesSimulados.Length == 0)
                return;

            string nome = nomesSimulados[proximoNome % nomesSimulados.Length];
            proximoNome++;

            Adicionar(new MatchSlot(nome, false, false));
        }

        /// <summary>A fila esvazia com o tempo: os encontros ficam cada vez mais espaçados.</summary>
        private float IntervaloAtual() => Mathf.Lerp(intervaloInicial, intervaloFinal, Search01);

        private void FecharBusca()
        {
            TrocarEtapa(MatchmakingStage.PlayersFound);
            fimDaEtapa = Time.time + duracaoJogadoresEncontrados;
        }

        private void ComecarPreenchimento()
        {
            TrocarEtapa(MatchmakingStage.FillingWithBots);
            fimDaEtapa = Time.time + duracaoPreenchendoBots;

            int faltam = jogadoresPorSala - slots.Count;
            for (int i = 0; i < faltam; i++)
                Adicionar(new MatchSlot($"BOT {i + 1}", true, false));
        }

        private void ConcluirSala()
        {
            MapaSorteado = SortearMapa();

            TrocarEtapa(MatchmakingStage.MatchFound);
            fimDaEtapa = Time.time + duracaoPartidaEncontrada;
        }

        private void Carregar()
        {
            TrocarEtapa(MatchmakingStage.LoadingMap);
            Running = false;
            MatchReady?.Invoke(MapaSorteado);
        }

        private string SortearMapa()
        {
            if (mapas == null || mapas.Length == 0)
                return null;

            return mapas[Random.Range(0, mapas.Length)];
        }

        private void Adicionar(MatchSlot slot)
        {
            if (slots.Count >= jogadoresPorSala)
                return;

            slots.Add(slot);

            if (!slot.Bot)
                Humanos++;

            SlotAdded?.Invoke(slot);
        }

        private void TrocarEtapa(MatchmakingStage nova)
        {
            if (Stage == nova)
                return;

            Stage = nova;
            StageChanged?.Invoke(nova);
        }
    }
}
