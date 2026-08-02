using System.Collections.Generic;
using PartyRacers.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Entrada de código com seis caixas e feedback explícito de conexão/erro.
    /// </summary>
    [DisallowMultipleComponent]
    public class JoinCodeUI : MonoBehaviour
    {
        [System.Serializable]
        public class Caixa
        {
            public GameObject raiz;
            public TextMeshProUGUI caractere;
            public GameObject estadoIdle;
            public GameObject estadoFoco;
            public GameObject estadoErro;
        }

        [Header("Caixas já montadas na cena")]
        [SerializeField] private List<Caixa> caixas = new List<Caixa>();

        [Header("Estados de retorno")]
        [SerializeField] private GameObject estadoCodigoInvalido;
        [SerializeField] private GameObject estadoSalaCheia;
        [SerializeField] private GameObject estadoConectando;

        [Header("Ações")]
        [SerializeField] private Button btnEntrar;
        [SerializeField] private Button btnCancelar;

        [Header("Navegação")]
        [SerializeField] private ScreenRouter roteador;
        [SerializeField] private string telaAoCancelar = "Lobby";

        [Header("Eventos")]
        public UnityEvent<string> aoConfirmar = new UnityEvent<string>();

        private string digitado = string.Empty;
        private bool conectando;
        private TextMeshProUGUI textoErro;
        private string mensagemCodigoInvalido;
        private Keyboard tecladoInscrito;

        public string Codigo => digitado;
        public bool EstaConectando => conectando;

        private void Awake()
        {
            if (btnEntrar != null)
                btnEntrar.onClick.AddListener(Confirmar);
            if (btnCancelar != null)
                btnCancelar.onClick.AddListener(Cancelar);

            textoErro = estadoCodigoInvalido != null
                ? estadoCodigoInvalido.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            mensagemCodigoInvalido = textoErro != null ? textoErro.text : string.Empty;

            foreach (Button button in GetComponentsInChildren<Button>(true))
            {
                UIPress press = button.GetComponent<UIPress>();
                if (press == null)
                    press = button.gameObject.AddComponent<UIPress>();
                press.SetEmphasized(button == btnEntrar);
            }
        }

        private void OnEnable()
        {
            InscreverNoTecladoAtual();
            digitado = string.Empty;
            DefinirConectando(false);
            Redesenhar();
        }

        private void OnDisable()
        {
            DesinscreverDoTeclado();
        }

        private void Update()
        {
            if (conectando)
                return;

            if (tecladoInscrito != Keyboard.current)
                InscreverNoTecladoAtual();

            if (tecladoInscrito == null)
                return;

            if (tecladoInscrito.backspaceKey.wasPressedThisFrame)
                Apagar();
            else if (tecladoInscrito.enterKey.wasPressedThisFrame)
                Confirmar();
            else if (tecladoInscrito.escapeKey.wasPressedThisFrame)
                Cancelar();
        }

        private void InscreverNoTecladoAtual()
        {
            DesinscreverDoTeclado();
            tecladoInscrito = Keyboard.current;
            if (tecladoInscrito != null)
                tecladoInscrito.onTextInput += ReceberTexto;
        }

        private void DesinscreverDoTeclado()
        {
            if (tecladoInscrito != null)
                tecladoInscrito.onTextInput -= ReceberTexto;
            tecladoInscrito = null;
        }

        private void ReceberTexto(char caractere)
        {
            if (!conectando && char.IsLetterOrDigit(caractere))
                Digitar(char.ToUpperInvariant(caractere));
        }

        public void Digitar(char caractere)
        {
            if (conectando || digitado.Length >= caixas.Count)
                return;

            digitado += caractere;
            LimparEstados();
            Redesenhar();
        }

        public void Apagar()
        {
            if (conectando || digitado.Length == 0)
                return;

            digitado = digitado.Substring(0, digitado.Length - 1);
            LimparEstados();
            Redesenhar();
        }

        public void Confirmar()
        {
            if (conectando)
                return;

            if (digitado.Length < caixas.Count)
            {
                MostrarCodigoInvalido();
                return;
            }

            DefinirConectando(true);
            aoConfirmar?.Invoke(digitado);
        }

        public void Cancelar()
        {
            if (conectando)
                return;

            if (roteador != null)
                roteador.Ir(telaAoCancelar);
        }

        public void DefinirConectando(bool valor)
        {
            conectando = valor;
            if (btnEntrar != null)
                btnEntrar.interactable = !valor;
            if (btnCancelar != null)
                btnCancelar.interactable = !valor;

            if (valor)
                MostrarErro(estadoConectando);
            else
                LimparEstados();
        }

        /// <summary>Chamado pela camada de rede quando a entrada falha.</summary>
        public void MostrarCodigoInvalido()
        {
            conectando = false;
            RestaurarBotoes();
            if (textoErro != null)
                textoErro.text = mensagemCodigoInvalido;
            MostrarErroNasCaixas(estadoCodigoInvalido);
        }

        public void MostrarSalaCheia()
        {
            conectando = false;
            RestaurarBotoes();
            MostrarErro(estadoSalaCheia);
        }

        public void MostrarFalhaConexao(string mensagem)
        {
            conectando = false;
            RestaurarBotoes();
            if (textoErro != null)
                textoErro.text = string.IsNullOrWhiteSpace(mensagem)
                    ? "NÃO FOI POSSÍVEL CONECTAR. TENTE NOVAMENTE."
                    : mensagem.ToUpperInvariant();
            MostrarErroNasCaixas(estadoCodigoInvalido);
        }

        private void RestaurarBotoes()
        {
            if (btnEntrar != null)
                btnEntrar.interactable = true;
            if (btnCancelar != null)
                btnCancelar.interactable = true;
        }

        private void Redesenhar()
        {
            for (int i = 0; i < caixas.Count; i++)
            {
                Caixa caixa = caixas[i];
                if (caixa == null)
                    continue;

                bool preenchida = i < digitado.Length;
                bool emFoco = !conectando && i == digitado.Length;

                if (caixa.caractere != null)
                    caixa.caractere.text = preenchida ? digitado[i].ToString() : string.Empty;

                Ligar(caixa.estadoFoco, emFoco);
                Ligar(caixa.estadoIdle, !emFoco);
                Ligar(caixa.estadoErro, false);
            }
        }

        private void MostrarErroNasCaixas(GameObject faixa)
        {
            MostrarErro(faixa);
            foreach (Caixa caixa in caixas)
            {
                if (caixa == null)
                    continue;
                Ligar(caixa.estadoErro, true);
                Ligar(caixa.estadoIdle, false);
                Ligar(caixa.estadoFoco, false);
            }
        }

        private void MostrarErro(GameObject qual)
        {
            Ligar(estadoCodigoInvalido, qual == estadoCodigoInvalido);
            Ligar(estadoSalaCheia, qual == estadoSalaCheia);
            Ligar(estadoConectando, qual == estadoConectando);
        }

        private void LimparEstados() => MostrarErro(null);

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
