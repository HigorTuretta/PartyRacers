using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 11. Mantém um pool de Item_ResultRow alimentado pela corrida real e liga a
    /// moldura certa (pódio, jogador local, ainda correndo, desconectado). A tabela é responsiva,
    /// rolável e se reordena conforme os retardatários cruzam a linha.
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultScreenUI : MonoBehaviour
    {
        public enum Situacao { Terminou, Correndo, Desconectado, TempoInvalido }

        public struct Resultado
        {
            public int posicao;
            public string nome;
            public float tempoTotal;
            public float melhorVolta;
            public bool ehLocal;
            public Situacao situacao;
            public int voltaAtual, totalVoltas;
        }

        [Header("Prefab de linha (Prefabs/UI/Items)")]
        [SerializeField] private GameObject prefabLinha;
        [SerializeField] private Transform containerTabela;

        [Header("Tabela responsiva")]
        [SerializeField] private RectTransform viewportTabela;
        [SerializeField] private RectTransform conteudoTabela;
        [SerializeField] private GridLayoutGroup gradeTabela;
        [SerializeField] private GameObject cabecalhoDireito;
        [SerializeField] private float larguraMaximaLinha = 900f;
        [SerializeField] private float alturaLinha = 68f;
        [SerializeField] private float alturaMinimaLinha = 32f;
        [SerializeField] private Vector2 espacamentoGrade = new Vector2(18f, 9f);

        [Header("Resumo")]
        [SerializeField] private TextMeshProUGUI textoSuaPosicao;
        [SerializeField] private TextMeshProUGUI textoTempoTotal;
        [SerializeField] private TextMeshProUGUI textoMelhorVolta;
        [SerializeField] private TextMeshProUGUI textoAindaCorrendo;
        [SerializeField] private GameObject blocoAindaCorrendo;

        [Header("Ações")]
        [SerializeField] private Button btnVoltarGaragem;
        [SerializeField] private Button btnJogarNovamente;

        [Header("Navegação")]
        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private string telaGaragem = "Garagem";

        [Header("Eventos")]
        public UnityEngine.Events.UnityEvent aoJogarNovamente;

        private readonly List<GameObject> linhas = new List<GameObject>();
        private bool tabelaInicializada;
        private float ultimaLargura = -1f;
        private float ultimaAltura = -1f;
        private int ultimaQuantidade;

        private void Awake()
        {
            GarantirTabelaInicializada();

            if (btnVoltarGaragem != null)
                btnVoltarGaragem.onClick.AddListener(() => { if (roteador != null) roteador.Ir(telaGaragem); });
            if (btnJogarNovamente != null)
                btnJogarNovamente.onClick.AddListener(() => aoJogarNovamente?.Invoke());
        }

        /// <summary>Preenche a tabela. Chamado pelo fim da corrida.</summary>
        public void Mostrar(IReadOnlyList<Resultado> resultados)
        {
            GarantirTabelaInicializada();

            int quantidade = resultados != null ? resultados.Count : 0;
            ultimaQuantidade = quantidade;
            while (linhas.Count < quantidade && prefabLinha != null && containerTabela != null)
            {
                GameObject linha = Instantiate(prefabLinha, containerTabela);
                linha.name = $"Resultado_{linhas.Count + 1:00}";
                linhas.Add(linha);
            }

            int correndo = 0;
            bool encontrouLocal = false;

            for (int i = 0; i < linhas.Count; i++)
            {
                GameObject go = linhas[i];
                bool deveExibir = i < quantidade;
                if (go != null && go.activeSelf != deveExibir)
                    go.SetActive(deveExibir);

                if (!deveExibir || go == null)
                    continue;

                Resultado r = resultados[i];
                if (r.situacao == Situacao.Correndo)
                    correndo++;

                Preencher(go, r);

                if (!r.ehLocal)
                    continue;

                encontrouLocal = true;
                if (textoSuaPosicao != null) textoSuaPosicao.text = r.posicao + "º";
                if (textoTempoTotal != null) textoTempoTotal.text = HUDFormat.LapTime(r.tempoTotal);
                if (textoMelhorVolta != null) textoMelhorVolta.text = HUDFormat.LapTime(r.melhorVolta);
            }

            if (!encontrouLocal)
            {
                if (textoSuaPosicao != null) textoSuaPosicao.text = "—";
                if (textoTempoTotal != null) textoTempoTotal.text = "--:--.---";
                if (textoMelhorVolta != null) textoMelhorVolta.text = "--:--.---";
            }

            if (blocoAindaCorrendo != null)
                blocoAindaCorrendo.SetActive(correndo > 0);
            if (textoAindaCorrendo != null)
                textoAindaCorrendo.text = correndo == 1 ? "1 ainda correndo" : $"{correndo} ainda correndo";

            RecalcularLayout(forcar: true);
        }

        private void GarantirTabelaInicializada()
        {
            if (tabelaInicializada)
                return;

            tabelaInicializada = true;

            if (conteudoTabela == null)
                conteudoTabela = containerTabela as RectTransform;
            if (containerTabela == null && conteudoTabela != null)
                containerTabela = conteudoTabela;
            if (viewportTabela == null && conteudoTabela != null)
                viewportTabela = conteudoTabela.parent as RectTransform;
            if (gradeTabela == null && conteudoTabela != null)
                gradeTabela = conteudoTabela.GetComponent<GridLayoutGroup>();

            Transform raizCabecalhos = conteudoTabela != null ? conteudoTabela : containerTabela;
            if (cabecalhoDireito == null && raizCabecalhos != null)
            {
                Transform cabecalho = raizCabecalhos.Find("Cabecalho_Dir");
                if (cabecalho != null) cabecalhoDireito = cabecalho.gameObject;
            }

            // Versões antigas do prefab traziam 16 linhas de demonstração. Elas não pertencem
            // ao resultado e duplicavam a tabela real. Desanexa antes do Destroy adiado para
            // que nunca participem do layout nem apareçam por um frame.
            if (containerTabela != null)
            {
                for (int i = containerTabela.childCount - 1; i >= 0; i--)
                {
                    Transform child = containerTabela.GetChild(i);
                    if (!EhLinhaDeDemonstracao(child.name))
                        continue;

                    child.SetParent(null, false);
                    Destroy(child.gameObject);
                }
            }

            RecalcularLayout(forcar: true);
        }

        private static bool EhLinhaDeDemonstracao(string nome)
        {
            return !string.IsNullOrEmpty(nome)
                && (nome.StartsWith("Linha_", System.StringComparison.Ordinal)
                    || nome.StartsWith("Item_ResultRow", System.StringComparison.Ordinal));
        }

        private void OnRectTransformDimensionsChange()
        {
            if (tabelaInicializada && isActiveAndEnabled)
                RecalcularLayout(forcar: false);
        }

        private void RecalcularLayout(bool forcar)
        {
            if (gradeTabela == null)
                return;

            RectTransform medida = viewportTabela != null ? viewportTabela : conteudoTabela;
            if (medida == null)
                return;

            float largura = medida.rect.width;
            float altura = medida.rect.height;
            if (largura <= 1f || altura <= 1f)
                return;
            if (!forcar
                && Mathf.Abs(largura - ultimaLargura) < 0.5f
                && Mathf.Abs(altura - ultimaAltura) < 0.5f)
                return;

            ultimaLargura = largura;
            ultimaAltura = altura;

            // Ate 16 corredores: duas colunas mantem todos os cards na tela sem rolagem.
            int colunas = ultimaQuantidade > 1 ? 2 : 1;
            int linhasDeResultado = Mathf.CeilToInt(ultimaQuantidade / (float)colunas);
            int linhasDaGrade = Mathf.Max(1, linhasDeResultado + 1); // cabecalho + cards

            float espacamentoX = colunas > 1
                ? Mathf.Min(espacamentoGrade.x, Mathf.Max(4f, largura * 0.012f))
                : 0f;
            float larguraUtil = largura - gradeTabela.padding.horizontal - espacamentoX * (colunas - 1);
            float larguraLinha = Mathf.Min(larguraMaximaLinha, Mathf.Max(1f, larguraUtil / colunas));

            float alturaUtil = Mathf.Max(1f, altura - gradeTabela.padding.vertical);
            int quantidadeDeEspacos = Mathf.Max(0, linhasDaGrade - 1);
            float espacamentoMaximo = quantidadeDeEspacos > 0
                ? Mathf.Max(0f, (alturaUtil - linhasDaGrade * alturaMinimaLinha) / quantidadeDeEspacos)
                : 0f;
            float espacamentoY = Mathf.Min(espacamentoGrade.y, espacamentoMaximo);
            float alturaCalculada = (alturaUtil - espacamentoY * quantidadeDeEspacos) / linhasDaGrade;
            float alturaDoCard = Mathf.Min(alturaLinha, Mathf.Max(1f, alturaCalculada));

            gradeTabela.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gradeTabela.constraintCount = colunas;
            gradeTabela.childAlignment = TextAnchor.UpperCenter;
            gradeTabela.spacing = new Vector2(espacamentoX, espacamentoY);
            gradeTabela.cellSize = new Vector2(larguraLinha, alturaDoCard);

            if (cabecalhoDireito != null && cabecalhoDireito.activeSelf != (colunas > 1))
                cabecalhoDireito.SetActive(colunas > 1);

            if (conteudoTabela != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(conteudoTabela);
        }

        private static void Preencher(GameObject go, Resultado r)
        {
            Escrever(go, "Nome", r.situacao == Situacao.TempoInvalido ? "TEMPO INVÁLIDO" : r.nome);

            var badge = go.transform.Find("Posicao/Valor")?.GetComponent<TextMeshProUGUI>();
            if (badge != null)
            {
                badge.text = r.posicao.ToString();
                badge.fontStyle = FontStyles.Bold;
                badge.color = Color.white;
            }

            bool mostraStatus = r.situacao == Situacao.Correndo || r.situacao == Situacao.Desconectado;
            Ligar(go, "TempoTotal", !mostraStatus);
            Ligar(go, "State_Status", mostraStatus);

            if (mostraStatus)
            {
                Escrever(go, "State_Status/Label", r.situacao == Situacao.Correndo
                    ? $"CORRENDO · V{r.voltaAtual}/{r.totalVoltas}"
                    : "DESCONECTOU");
            }
            else
            {
                Escrever(go, "TempoTotal", r.situacao == Situacao.TempoInvalido
                    ? "--:--.---"
                    : HUDFormat.LapTime(r.tempoTotal));
            }

            Escrever(go, "MelhorVolta", r.melhorVolta > 0f ? HUDFormat.LapTimeShort(r.melhorVolta) : "—");

            // O podio tem prioridade visual sobre os demais estados do card.
            string destaque = r.posicao == 1 ? "Destaque_Ouro"
                            : r.posicao == 2 ? "Destaque_Prata"
                            : r.posicao == 3 ? "Destaque_Bronze"
                            : r.ehLocal ? "Destaque_IsLocal"
                            : r.situacao == Situacao.Correndo ? "Destaque_Correndo"
                            : r.situacao == Situacao.Desconectado ? "Destaque_Desconectado"
                            : null;

            foreach (string d in new[]{ "Destaque_Ouro", "Destaque_Prata", "Destaque_Bronze",
                                        "Destaque_IsLocal", "Destaque_Correndo", "Destaque_Desconectado" })
                Ligar(go, d, d == destaque);
        }

        private static void Escrever(GameObject raiz, string caminho, string texto)
        {
            var t = raiz.transform.Find(caminho)?.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = texto;
        }

        private static void Ligar(GameObject raiz, string caminho, bool ativo)
        {
            Transform t = raiz.transform.Find(caminho);
            if (t != null && t.gameObject.activeSelf != ativo)
                t.gameObject.SetActive(ativo);
        }
    }
}
