using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 07. Seis caixas de um caractere, com os estados (vazia, em foco, erro)
    /// como objetos irmãos já montados no Item_CodeBox.
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

        [Header("Estados de retorno (irmãos, um por situação)")]
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
        public UnityEngine.Events.UnityEvent<string> aoConfirmar;

        private string digitado = string.Empty;

        public string Codigo => digitado;

        private void Awake()
        {
            if (btnEntrar != null) btnEntrar.onClick.AddListener(Confirmar);
            if (btnCancelar != null) btnCancelar.onClick.AddListener(Cancelar);
        }

        private void OnEnable()
        {
            digitado = string.Empty;
            LimparEstados();
            Redesenhar();
        }

        private void Update()
        {
            foreach (char c in Input.inputString)
            {
                if (c == '\b')
                    Apagar();
                else if (c == '\n' || c == '\r')
                    Confirmar();
                else if (char.IsLetterOrDigit(c))
                    Digitar(char.ToUpperInvariant(c));
            }
        }

        public void Digitar(char c)
        {
            if (digitado.Length >= caixas.Count)
                return;

            digitado += c;
            LimparEstados();
            Redesenhar();
        }

        public void Apagar()
        {
            if (digitado.Length == 0)
                return;

            digitado = digitado.Substring(0, digitado.Length - 1);
            LimparEstados();
            Redesenhar();
        }

        public void Confirmar()
        {
            if (digitado.Length < caixas.Count)
            {
                MostrarErro(estadoCodigoInvalido);
                return;
            }

            MostrarErro(estadoConectando);
            aoConfirmar?.Invoke(digitado);
        }

        public void Cancelar()
        {
            if (roteador != null)
                roteador.Ir(telaAoCancelar);
        }

        /// <summary>Chamado pela camada de rede quando a entrada falha.</summary>
        public void MostrarCodigoInvalido() => MostrarErroNasCaixas(estadoCodigoInvalido);
        public void MostrarSalaCheia() => MostrarErro(estadoSalaCheia);

        private void Redesenhar()
        {
            for (int i = 0; i < caixas.Count; i++)
            {
                Caixa c = caixas[i];
                if (c == null)
                    continue;

                bool preenchida = i < digitado.Length;
                bool emFoco = i == digitado.Length;

                if (c.caractere != null)
                    c.caractere.text = preenchida ? digitado[i].ToString() : string.Empty;

                Ligar(c.estadoFoco, emFoco);
                Ligar(c.estadoIdle, !emFoco);
                Ligar(c.estadoErro, false);
            }
        }

        private void MostrarErroNasCaixas(GameObject faixa)
        {
            MostrarErro(faixa);
            foreach (Caixa c in caixas)
            {
                if (c == null)
                    continue;
                Ligar(c.estadoErro, true);
                Ligar(c.estadoIdle, false);
                Ligar(c.estadoFoco, false);
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
