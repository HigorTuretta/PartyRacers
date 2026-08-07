using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Classificação do HUD v2: o TOP 5 e, abaixo, a faixa do jogador.
    ///
    /// É um binder separado do <see cref="StandingsUI"/> de propósito: aquele usa medalhas por
    /// posição e está ligado às telas que já existem no jogo. Mudar os campos dele quebraria essas
    /// referências sem ganho nenhum.
    ///
    /// Cada faixa tem quatro colunas, na ordem em que a informação é procurada:
    /// <b>posição</b>, <b>nome</b>, <b>melhor volta</b> e <b>intervalo</b>. O intervalo é o tempo
    /// até o carro da FRENTE, medido a cada frame — a mesma coluna que a F1 chama de "Interval".
    /// Quem lidera não tem ninguém à frente, então ali se lê LÍDER.
    ///
    /// A faixa do jogador está SEMPRE visível: dentro do top 5 é a linha dele que ganha o
    /// destaque; fora dele, a faixa de baixo aparece. Quem corre em 12º precisa saber com quem
    /// está brigando, não só quem lidera.
    /// </summary>
    [DisallowMultipleComponent]
    public class StandingsV2UI : MonoBehaviour
    {
        [System.Serializable]
        public class Linha
        {
            public GameObject raiz;

            [Header("Estados (irmãos mutuamente exclusivos)")]
            public GameObject estadoLocal;
            [Tooltip("Sombra dura da linha local. É objeto IRMÃO, então não acompanha o SetActive " +
                     "do estado — sem ligá-la junto, sobra uma mancha escura na faixa.")]
            public GameObject sombraLocal;
            public GameObject estadoOutro;

            [Tooltip("Moldura âmbar acesa quando ESTA faixa do topo é a do jogador. As cinco de " +
                     "cima não têm variante local própria — quem marca que a linha é sua é isto.")]
            public GameObject destaque;

            [Header("Colunas de cada estado")]
            public TextMeshProUGUI posicaoLocal, nomeLocal, voltaLocal, intervaloLocal;
            public TextMeshProUGUI posicaoOutro, nomeOutro, voltaOutro, intervaloOutro;
        }

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Linhas já montadas na cena (as 5 do topo + a do jogador)")]
        [SerializeField] private List<Linha> linhas = new List<Linha>();

        [Tooltip("Placa de fundo. Encolhe quando a faixa do jogador não está em uso — painel com " +
                 "um terço vazio parece lista cortada.")]
        [SerializeField] private RectTransform painel;
        [SerializeField] private float alturaCompleta = 268f;
        [SerializeField] private float alturaSoTopo = 214f;

        [Header("Tendência do intervalo")]
        [Tooltip("Intervalo encolhendo: você está alcançando.")]
        [SerializeField] private Color corAproximando = new Color(0.24f, 0.86f, 0.59f);
        [Tooltip("Intervalo crescendo: está perdendo terreno.")]
        [SerializeField] private Color corAfastando = new Color(1f, 0.42f, 0.48f);
        [SerializeField] private Color corEstavel = new Color(1f, 0.69f, 0.13f);
        [Tooltip("Variação por segundo a partir da qual a cor muda. Abaixo disso é ruído.")]
        [SerializeField] private float limiarDeTendencia = 0.25f;

        private const int Topo = 5;

        private readonly Dictionary<int, float> intervaloAnterior = new Dictionary<int, float>();
        private float relogioDaTendencia;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        private void Awake()
        {
            if (dados == null)
                dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        private void Update()
        {
            if (dados == null || linhas.Count == 0)
                return;

            dados.Refresh();
            IReadOnlyList<RaceHUDDataProvider.Standing> lista = dados.Standings;

            int indiceLocal = -1;
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i].IsLocal)
                {
                    indiceLocal = i;
                    break;
                }
            }

            // As cinco primeiras faixas são o top 5, sempre. A sexta é a do jogador e só existe
            // quando ele está fora dele — dentro, ela seria uma repetição da linha destacada acima.
            bool localNoTopo = indiceLocal >= 0 && indiceLocal < Topo;

            if (painel != null)
            {
                float altura = localNoTopo ? alturaSoTopo : alturaCompleta;
                if (!Mathf.Approximately(painel.sizeDelta.y, altura))
                    painel.sizeDelta = new Vector2(painel.sizeDelta.x, altura);
            }

            for (int i = 0; i < linhas.Count; i++)
            {
                Linha linha = linhas[i];
                if (linha == null || linha.raiz == null)
                    continue;

                bool ehFaixaDoJogador = i >= Topo;
                int alvo = ehFaixaDoJogador ? indiceLocal : i;
                bool temDado = alvo >= 0 && alvo < lista.Count
                               && (!ehFaixaDoJogador || !localNoTopo);

                Ligar(linha.raiz, temDado);

                if (!temDado)
                    continue;

                Escrever(linha, lista[alvo]);
                Tingir(linha, lista[alvo]);
            }

            if (Time.unscaledTime - relogioDaTendencia < 0.5f)
                return;

            relogioDaTendencia = Time.unscaledTime;
            foreach (RaceHUDDataProvider.Standing d in lista)
                if (d.Kart != null && d.GapKnown)
                    intervaloAnterior[d.Kart.GetInstanceID()] = d.GapToAhead;
        }

        /// <summary>
        /// A cor do intervalo diz para onde ele ANDA: verde encolhendo, vermelho crescendo.
        ///
        /// Um número sozinho obriga a lembrar quanto era há um segundo. A cor responde isso sem
        /// leitura, e é a única coisa da classificação que serve de instrução de pilotagem —
        /// "insiste" ou "desiste desta e defende a posição".
        ///
        /// A comparação é a cada meio segundo, não a cada frame: entre dois frames a variação é
        /// ruído de medição, e a linha ficaria piscando verde e vermelho sem parar.
        /// </summary>
        private void Tingir(Linha linha, RaceHUDDataProvider.Standing dado)
        {
            TextMeshProUGUI alvo = dado.IsLocal && linha.estadoLocal != null
                ? linha.intervaloLocal
                : linha.intervaloOutro;

            if (alvo == null)
                return;

            // A faixa âmbar do jogador tem texto escuro; tingir de verde ali sumiria no fundo.
            if (dado.IsLocal && linha.estadoLocal != null)
                return;

            Color cor = corEstavel;

            if (dado.Position > 1 && dado.GapKnown && dado.Kart != null
                && intervaloAnterior.TryGetValue(dado.Kart.GetInstanceID(), out float antes))
            {
                float variacao = dado.GapToAhead - antes;
                if (variacao < -limiarDeTendencia * 0.5f) cor = corAproximando;
                else if (variacao > limiarDeTendencia * 0.5f) cor = corAfastando;
            }

            if (alvo.color != cor)
                alvo.color = cor;
        }

        private static void Escrever(Linha linha, RaceHUDDataProvider.Standing dado)
        {
            // As cinco faixas do topo têm só a variante "outro" — a variante âmbar existe uma vez
            // só, na faixa de baixo. Ligar um estado que não existe apagava a linha inteira: quem
            // subia para o pódio desaparecia da própria classificação. Sem variante própria, a
            // faixa continua sendo a comum e ganha a moldura de destaque.
            bool ehLocal = dado.IsLocal;
            bool usarLocal = ehLocal && linha.estadoLocal != null;

            Ligar(linha.estadoLocal, usarLocal);
            Ligar(linha.sombraLocal, usarLocal);
            Ligar(linha.estadoOutro, !usarLocal);
            Ligar(linha.destaque, ehLocal && !usarLocal);

            string posicao = dado.Position.ToString();
            string volta = HUDFormat.LapTimeShort(dado.BestLapTime);
            string intervalo = Intervalo(dado);

            if (usarLocal)
            {
                Texto(linha.posicaoLocal, posicao);
                Texto(linha.nomeLocal, dado.DisplayName);
                Texto(linha.voltaLocal, volta);
                Texto(linha.intervaloLocal, intervalo);
            }
            else
            {
                Texto(linha.posicaoOutro, posicao);
                Texto(linha.nomeOutro, dado.DisplayName);
                Texto(linha.voltaOutro, volta);
                Texto(linha.intervaloOutro, intervalo);
            }
        }

        /// <summary>
        /// Coluna de intervalo. Líder não tem ninguém à frente; medida indisponível vira "--", e
        /// não zero — zero significaria "colado", que é exatamente o oposto de "não sei".
        /// </summary>
        public static string Intervalo(RaceHUDDataProvider.Standing dado)
        {
            if (dado.Position <= 1)
                return "LÍDER";

            if (!dado.GapKnown)
                return "--";

            return dado.GapToAhead >= 100f ? "+99" : "+" + dado.GapToAhead.ToString("0.0");
        }

        private static void Texto(TextMeshProUGUI alvo, string valor)
        {
            if (alvo != null && alvo.text != valor)
                alvo.text = valor;
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
