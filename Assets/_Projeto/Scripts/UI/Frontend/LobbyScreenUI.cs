using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 06 — a primeira tela do jogo (não existe tela inicial).
    /// As 16 vagas já estão montadas na cena; este script só troca o estado de cada uma
    /// (jogador, vaga livre, desconectado) e preenche nome/prontidão.
    /// </summary>
    [DisallowMultipleComponent]
    public class LobbyScreenUI : MonoBehaviour
    {
        public enum EstadoVaga { Livre, Ocupada, Desconectado }

        public struct Participante
        {
            public string nome;
            public bool pronto;
            public bool ehLocal;
            public bool ehDono;
            public bool ehBot;
            public EstadoVaga estado;
        }

        [Header("Vagas já montadas na cena (16)")]
        [SerializeField] private List<GameObject> vagas = new List<GameObject>();

        [Header("Código da sala")]
        [SerializeField] private TextMeshProUGUI textoCodigo;
        [SerializeField] private Button btnCopiar;

        [Header("Contagem")]
        [SerializeField] private TextMeshProUGUI textoQuantidade;
        [SerializeField] private TextMeshProUGUI textoMaximo;

        [Header("Aviso")]
        [SerializeField] private TextMeshProUGUI textoAviso;

        [Header("Ações")]
        [SerializeField] private Button btnEntrarPorCodigo;
        [SerializeField] private Button btnSairDaSala;
        [SerializeField] private GameObject estadoAguardando;
        [SerializeField] private GameObject estadoPronto;

        [Header("Navegação")]
        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private string telaJoinCode = "JoinCode";

        [Header("Eventos")]
        public UnityEngine.Events.UnityEvent aoSairDaSala;
        public UnityEngine.Events.UnityEvent aoIniciarPartida;

        private string codigo = string.Empty;

        private void Awake()
        {
            if (btnCopiar != null) btnCopiar.onClick.AddListener(Copiar);
            if (btnEntrarPorCodigo != null)
                btnEntrarPorCodigo.onClick.AddListener(() => { if (roteador != null) roteador.Ir(telaJoinCode); });
            if (btnSairDaSala != null) btnSairDaSala.onClick.AddListener(() => aoSairDaSala?.Invoke());

            // o botão fica num filho ("Bg") por causa da moldura: procurar só no próprio objeto
            // deixava CORRER sem nenhum listener — clicar não fazia nada
            var iniciar = estadoPronto != null ? estadoPronto.GetComponentInChildren<Button>(true) : null;
            if (iniciar != null) iniciar.onClick.AddListener(() => aoIniciarPartida?.Invoke());
            else if (estadoPronto != null)
                Debug.LogWarning("[Lobby] 'estadoPronto' não tem Button em lugar nenhum — CORRER fica inerte.", estadoPronto);
        }

        public void DefinirCodigo(string valor)
        {
            codigo = valor;
            if (textoCodigo != null) textoCodigo.text = valor;
        }

        /// <summary>Redesenha a sala inteira. Chamado quando a lista de participantes muda.</summary>
        public void Mostrar(IReadOnlyList<Participante> participantes, int maximo = 16)
        {
            int ocupadas = 0, pendentes = 0;

            for (int i = 0; i < vagas.Count; i++)
            {
                GameObject vaga = vagas[i];
                if (vaga == null)
                    continue;

                bool temDado = i < participantes.Count;
                Participante p = temDado ? participantes[i] : default;
                EstadoVaga estado = temDado ? p.estado : EstadoVaga.Livre;

                Ligar(vaga, "State_Player", estado == EstadoVaga.Ocupada);
                Ligar(vaga, "State_Disconnected", estado == EstadoVaga.Desconectado);
                Ligar(vaga, "State_Empty", estado == EstadoVaga.Livre);

                if (estado == EstadoVaga.Livre)
                    continue;

                ocupadas++;
                if (estado == EstadoVaga.Ocupada && !p.pronto)
                    pendentes++;

                string sufixo = p.ehDono && p.ehLocal ? " (dono · você)"
                              : p.ehLocal ? " (você)"
                              : p.ehBot ? " (bot)"
                              : string.Empty;

                string raiz = estado == EstadoVaga.Desconectado ? "State_Disconnected" : "State_Player";
                Escrever(vaga, raiz + "/Nome", p.nome + sufixo);

                if (estado != EstadoVaga.Ocupada)
                    continue;

                Ligar(vaga, "State_Player/State_Ready", p.pronto);
                Ligar(vaga, "State_Player/State_Waiting", !p.pronto);
                Ligar(vaga, "State_Player/Destaque_IsLocal", p.ehLocal);
            }

            if (textoQuantidade != null) textoQuantidade.text = ocupadas.ToString();
            if (textoMaximo != null) textoMaximo.text = "/" + maximo;

            if (textoAviso != null)
            {
                textoAviso.text = pendentes == 0
                    ? "Todos prontos — a partida pode começar"
                    : pendentes == 1
                        ? "Aguardando todos ficarem prontos — 1 jogador pendente"
                        : $"Aguardando todos ficarem prontos — {pendentes} jogadores pendentes";
            }

            Ligar(estadoAguardando, pendentes > 0);
            Ligar(estadoPronto, pendentes == 0 && ocupadas > 0);
        }

        private void Copiar()
        {
            if (!string.IsNullOrEmpty(codigo))
                GUIUtility.systemCopyBuffer = codigo;
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

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
