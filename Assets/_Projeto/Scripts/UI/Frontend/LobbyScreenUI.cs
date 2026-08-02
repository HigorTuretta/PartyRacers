using System.Collections.Generic;
using PartyRacers.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela de lobby. A lista de vagas e toda a arte continuam autoradas no prefab;
    /// este componente somente projeta jogadores e o estado atual da sessão.
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
        public UnityEvent aoAcionarConvite = new UnityEvent();
        public UnityEvent aoSairDaSala = new UnityEvent();
        public UnityEvent aoIniciarPartida = new UnityEvent();

        private string codigo = string.Empty;
        private Button botaoPrincipal;
        private TextMeshProUGUI textoBotaoCopiar;
        private TextMeshProUGUI textoBotaoEntrar;
        private TextMeshProUGUI textoBotaoSair;
        private TextMeshProUGUI textoAcaoPrincipal;
        private TextMeshProUGUI textoEstadoAguardando;

        private void Awake()
        {
            botaoPrincipal = estadoPronto != null ? estadoPronto.GetComponentInChildren<Button>(true) : null;
            textoBotaoCopiar = EncontrarRotulo(btnCopiar);
            textoBotaoEntrar = EncontrarRotulo(btnEntrarPorCodigo);
            textoBotaoSair = EncontrarRotulo(btnSairDaSala);
            textoAcaoPrincipal = estadoPronto != null ? estadoPronto.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            textoEstadoAguardando = estadoAguardando != null ? estadoAguardando.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            if (btnCopiar != null)
                btnCopiar.onClick.AddListener(AcionarConvite);

            if (btnEntrarPorCodigo != null)
                btnEntrarPorCodigo.onClick.AddListener(AbrirEntradaPorCodigo);

            if (btnSairDaSala != null)
                btnSairDaSala.onClick.AddListener(() => aoSairDaSala?.Invoke());

            if (botaoPrincipal != null)
                botaoPrincipal.onClick.AddListener(() => aoIniciarPartida?.Invoke());
            else if (estadoPronto != null)
                Debug.LogWarning("[Lobby] State_Pronto não contém Button; a ação principal ficará inerte.", estadoPronto);

            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                bool destaque = button == botaoPrincipal || button == btnCopiar;
                UIPress press = button.GetComponent<UIPress>();
                if (press == null)
                    press = button.gameObject.AddComponent<UIPress>();
                press.SetEmphasized(destaque);
            }
        }

        public void DefinirCodigo(string valor)
        {
            codigo = (valor ?? string.Empty).Trim().ToUpperInvariant();
            if (textoCodigo != null)
            {
                textoCodigo.text = string.IsNullOrEmpty(codigo) ? "SEM SALA" : codigo;
                textoCodigo.fontSize = string.IsNullOrEmpty(codigo) ? 34f : 44f;
                textoCodigo.characterSpacing = string.IsNullOrEmpty(codigo) ? 2f : 10f;
            }
        }

        public void DefinirAviso(string aviso)
        {
            if (textoAviso != null && !string.IsNullOrWhiteSpace(aviso))
                textoAviso.text = aviso;
        }

        /// <summary>
        /// Atualiza botões e mensagens sem misturar estado offline com uma sala online real.
        /// </summary>
        public void MostrarEstadoSessao(
            string codigoSala,
            bool online,
            bool ocupado,
            bool ehHost,
            bool localPronto,
            bool todosProntos,
            string status)
        {
            DefinirCodigo(online ? codigoSala : string.Empty);

            if (textoBotaoCopiar != null)
                textoBotaoCopiar.text = ocupado ? "CONECTANDO..." : online ? "COPIAR CÓDIGO" : "CRIAR SALA";

            if (textoBotaoEntrar != null)
                textoBotaoEntrar.text = online ? "JÁ CONECTADO" : "ENTRAR POR CÓDIGO";

            if (textoBotaoSair != null)
                textoBotaoSair.text = online ? "SAIR DA SALA" : "GARAGEM";

            if (btnCopiar != null)
                btnCopiar.interactable = !ocupado;
            if (btnEntrarPorCodigo != null)
                btnEntrarPorCodigo.interactable = !ocupado && !online;
            if (btnSairDaSala != null)
                btnSairDaSala.interactable = !ocupado;

            Ligar(estadoAguardando, ocupado);
            Ligar(estadoPronto, !ocupado);

            if (textoEstadoAguardando != null)
                textoEstadoAguardando.text = online ? "SINCRONIZANDO" : "CONECTANDO";

            if (botaoPrincipal != null)
                botaoPrincipal.interactable = !ocupado;

            if (textoAcaoPrincipal != null)
            {
                textoAcaoPrincipal.fontSize = 29f;
                if (!online)
                    textoAcaoPrincipal.text = "JOGAR LOCAL";
                else if (ehHost && todosProntos)
                    textoAcaoPrincipal.text = "INICIAR CORRIDA";
                else if (localPronto)
                {
                    textoAcaoPrincipal.text = "CANCELAR PRONTO";
                    textoAcaoPrincipal.fontSize = 26f;
                }
                else
                    textoAcaoPrincipal.text = "FICAR PRONTO";
            }

            DefinirAviso(status);
        }

        /// <summary>Redesenha as vagas. O estado da sessão é definido separadamente.</summary>
        public void Mostrar(IReadOnlyList<Participante> participantes, int maximo = 16)
        {
            int ocupadas = 0;
            int quantidade = participantes?.Count ?? 0;

            for (int i = 0; i < vagas.Count; i++)
            {
                GameObject vaga = vagas[i];
                if (vaga == null)
                    continue;

                bool temDado = i < quantidade;
                Participante participante = temDado ? participantes[i] : default;
                EstadoVaga estado = temDado ? participante.estado : EstadoVaga.Livre;

                Ligar(vaga, "State_Player", estado == EstadoVaga.Ocupada);
                Ligar(vaga, "State_Disconnected", estado == EstadoVaga.Desconectado);
                Ligar(vaga, "State_Empty", estado == EstadoVaga.Livre);

                if (estado == EstadoVaga.Livre)
                    continue;

                ocupadas++;
                string sufixo = participante.ehDono && participante.ehLocal ? " (dono · você)"
                    : participante.ehLocal ? " (você)"
                    : participante.ehDono ? " (dono)"
                    : participante.ehBot ? " (bot)"
                    : string.Empty;

                string raiz = estado == EstadoVaga.Desconectado ? "State_Disconnected" : "State_Player";
                Escrever(vaga, raiz + "/Nome", (participante.nome ?? "JOGADOR") + sufixo);

                if (estado != EstadoVaga.Ocupada)
                    continue;

                Ligar(vaga, "State_Player/State_Ready", participante.pronto);
                Ligar(vaga, "State_Player/State_Waiting", !participante.pronto);
                Ligar(vaga, "State_Player/Destaque_IsLocal", participante.ehLocal);
            }

            if (textoQuantidade != null)
                textoQuantidade.text = ocupadas.ToString();
            if (textoMaximo != null)
                textoMaximo.text = "/" + maximo;
        }

        private void AcionarConvite()
        {
            if (!string.IsNullOrEmpty(codigo))
                GUIUtility.systemCopyBuffer = codigo;

            aoAcionarConvite?.Invoke();
        }

        private void AbrirEntradaPorCodigo()
        {
            if (roteador != null)
                roteador.Ir(telaJoinCode);
        }

        private static TextMeshProUGUI EncontrarRotulo(Button button)
        {
            if (button == null)
                return null;

            Transform visualRoot = button.name == "Bg" && button.transform.parent != null
                ? button.transform.parent
                : button.transform;
            return visualRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private static void Escrever(GameObject raiz, string caminho, string texto)
        {
            TextMeshProUGUI target = raiz.transform.Find(caminho)?.GetComponent<TextMeshProUGUI>();
            if (target != null)
                target.text = texto;
        }

        private static void Ligar(GameObject raiz, string caminho, bool ativo)
        {
            Transform target = raiz.transform.Find(caminho);
            if (target != null && target.gameObject.activeSelf != ativo)
                target.gameObject.SetActive(ativo);
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
