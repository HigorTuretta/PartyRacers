using System.Collections.Generic;
using TMPro;
using UnityEngine;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Classificação do HUD v2. Cada linha tem DUAS variantes já montadas — a do jogador (âmbar,
    /// maior, com sombra dura) e a dos demais (vidro escuro, menor) — e o binder só liga uma.
    ///
    /// É um binder separado do <see cref="StandingsUI"/> de propósito: aquele usa medalhas por
    /// posição e está ligado às telas que já existem no jogo. Mudar os campos dele quebraria essas
    /// referências sem ganho nenhum.
    ///
    /// A linha do jogador está SEMPRE visível: fora do top 5, a lista mostra os que estão à frente
    /// e reserva a última faixa para ele — ver alguém em 16º precisa saber com quem está brigando,
    /// não quem lidera.
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

            [Header("Textos de dentro de cada estado")]
            public TextMeshProUGUI posicaoLocal, nomeLocal, tempoLocal;
            public TextMeshProUGUI posicaoOutro, nomeOutro, tempoOutro;
        }

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Linhas já montadas na cena")]
        [SerializeField] private List<Linha> linhas = new List<Linha>();

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
                if (lista[i].IsLocal) { indiceLocal = i; break; }
            }

            // Dentro das faixas visíveis: a lista é o topo puro. Fora: as faixas de cima mostram
            // quem está imediatamente à frente e a última guarda o jogador.
            bool localCabeNoTopo = indiceLocal >= 0 && indiceLocal < linhas.Count;
            int primeiro = localCabeNoTopo || indiceLocal < 0
                ? 0
                : Mathf.Max(0, indiceLocal - (linhas.Count - 1));

            for (int i = 0; i < linhas.Count; i++)
            {
                Linha linha = linhas[i];
                if (linha == null || linha.raiz == null)
                    continue;

                int alvo = localCabeNoTopo || indiceLocal < 0
                    ? i
                    : (i == linhas.Count - 1 ? indiceLocal : primeiro + i);

                bool temDado = alvo >= 0 && alvo < lista.Count;
                Ligar(linha.raiz, temDado);

                if (!temDado)
                    continue;

                RaceHUDDataProvider.Standing dado = lista[alvo];
                bool ehLocal = dado.IsLocal;

                Ligar(linha.estadoLocal, ehLocal);
                Ligar(linha.sombraLocal, ehLocal);
                Ligar(linha.estadoOutro, !ehLocal);

                string posicao = dado.Position.ToString();
                string tempo = HUDFormat.LapTimeShort(dado.BestLapTime);

                if (ehLocal)
                {
                    Escrever(linha.posicaoLocal, posicao);
                    Escrever(linha.nomeLocal, dado.DisplayName);
                    Escrever(linha.tempoLocal, tempo);
                }
                else
                {
                    Escrever(linha.posicaoOutro, posicao);
                    Escrever(linha.nomeOutro, dado.DisplayName);
                    Escrever(linha.tempoOutro, tempo);
                }
            }
        }

        private static void Escrever(TextMeshProUGUI alvo, string valor)
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
