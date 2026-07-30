using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 11. Instancia Item_ResultRow no container que já tem Grid Layout na cena
    /// e liga a moldura de destaque certa (pódio, jogador local, ainda correndo, desconectado).
    /// A lista se reordena conforme os retardatários cruzam a linha.
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

        private void Awake()
        {
            if (btnVoltarGaragem != null)
                btnVoltarGaragem.onClick.AddListener(() => { if (roteador != null) roteador.Ir(telaGaragem); });
            if (btnJogarNovamente != null)
                btnJogarNovamente.onClick.AddListener(() => aoJogarNovamente?.Invoke());
        }

        /// <summary>Preenche a tabela. Chamado pelo fim da corrida.</summary>
        public void Mostrar(IReadOnlyList<Resultado> resultados)
        {
            // ver StoreScreenUI: Destroy é adiado, então tira do pai antes de destruir
            foreach (GameObject go in linhas)
            {
                if (go == null) continue;
                go.transform.SetParent(null, false);
                Destroy(go);
            }
            linhas.Clear();

            int correndo = 0;

            foreach (Resultado r in resultados)
            {
                if (r.situacao == Situacao.Correndo)
                    correndo++;

                if (prefabLinha == null || containerTabela == null)
                    continue;

                GameObject go = Instantiate(prefabLinha, containerTabela);
                linhas.Add(go);
                Preencher(go, r);

                if (!r.ehLocal)
                    continue;

                if (textoSuaPosicao != null) textoSuaPosicao.text = r.posicao + "º";
                if (textoTempoTotal != null) textoTempoTotal.text = HUDFormat.LapTime(r.tempoTotal);
                if (textoMelhorVolta != null) textoMelhorVolta.text = HUDFormat.LapTime(r.melhorVolta);
            }

            if (blocoAindaCorrendo != null)
                blocoAindaCorrendo.SetActive(correndo > 0);
            if (textoAindaCorrendo != null)
                textoAindaCorrendo.text = correndo == 1 ? "1 ainda correndo" : $"{correndo} ainda correndo";
        }

        private static void Preencher(GameObject go, Resultado r)
        {
            Escrever(go, "Nome", r.situacao == Situacao.TempoInvalido ? "TEMPO INVÁLIDO" : r.nome);

            var badge = go.transform.Find("Posicao/Valor")?.GetComponent<TextMeshProUGUI>();
            if (badge != null) badge.text = r.posicao.ToString();

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

            // molduras de destaque: irmãs, uma por situação
            string destaque = r.ehLocal ? "Destaque_IsLocal"
                            : r.situacao == Situacao.Correndo ? "Destaque_Correndo"
                            : r.situacao == Situacao.Desconectado ? "Destaque_Desconectado"
                            : r.posicao == 1 ? "Destaque_Ouro"
                            : r.posicao == 2 ? "Destaque_Prata"
                            : r.posicao == 3 ? "Destaque_Bronze"
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
