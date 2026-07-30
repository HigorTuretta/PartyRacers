using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Palco do carro no frontend: gira sozinho, deixa arrastar com o mouse para girar à mão, e
    /// faz a troca de carro com movimento (o carro velho sai girando e encolhendo, o novo entra).
    ///
    /// O arraste não passa pelo EventSystem porque o carro é 3D, não UI: escuta o ponteiro
    /// direto e só aceita quando o cursor não está sobre um elemento de interface, senão
    /// arrastar em cima de um botão giraria o carro junto.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarStage : MonoBehaviour
    {
        [Header("Giro automático")]
        [SerializeField] private float velocidadeAutomatica = 12f;
        [Tooltip("Segundos parado depois de arrastar antes de voltar a girar sozinho.")]
        [SerializeField] private float esperaAposArraste = 2.5f;

        [Header("Arraste")]
        [SerializeField] private bool permitirArraste = true;
        [SerializeField] private float sensibilidade = 0.35f;
        [Tooltip("Quanto o giro continua depois de soltar.")]
        [SerializeField] private float inercia = 4f;

        [Header("Troca de carro")]
        [SerializeField] private float duracaoDaTroca = 0.34f;
        [SerializeField] private float giroDaTroca = 90f;
        [SerializeField] private float encolhimentoDaTroca = 0.55f;

        public bool Arrastando { get; private set; }

        private float velocidade;
        private float ocioso;
        private Transform alvo;
        private Coroutine troca;
        private Vector3 escalaBase = Vector3.one;
        private Vector2 ponteiroAnterior;

        private void Awake()
        {
            velocidade = velocidadeAutomatica;
            alvo = transform;
            escalaBase = transform.localScale;
        }

        private void Update()
        {
            if (troca != null)
                return;

            LerArraste();

            if (Arrastando)
                return;

            // volta ao giro automático depois de um tempo parado
            if (ocioso > 0f)
            {
                ocioso -= Time.unscaledDeltaTime;
                velocidade = Mathf.MoveTowards(velocidade, 0f, inercia * 12f * Time.unscaledDeltaTime);
            }
            else
            {
                velocidade = Mathf.MoveTowards(velocidade, velocidadeAutomatica, inercia * 6f * Time.unscaledDeltaTime);
            }

            alvo.Rotate(Vector3.up, velocidade * Time.unscaledDeltaTime, Space.World);
        }

        private void LerArraste()
        {
            if (!permitirArraste)
                return;

            bool pressionado = PonteiroPressionado(out Vector2 posicao);

            if (pressionado && !Arrastando)
            {
                // não sequestra o arraste quando o cursor está sobre um botão/painel da UI
                if (SobreUI(posicao))
                    return;

                Arrastando = true;
                ponteiroAnterior = posicao;
                return;
            }

            if (!pressionado)
            {
                if (Arrastando)
                {
                    Arrastando = false;
                    ocioso = esperaAposArraste;
                }
                return;
            }

            Vector2 delta = posicao - ponteiroAnterior;
            ponteiroAnterior = posicao;

            float giro = -delta.x * sensibilidade;
            alvo.Rotate(Vector3.up, giro, Space.World);

            // guarda a velocidade do gesto para o carro continuar girando ao soltar
            if (Time.unscaledDeltaTime > 0f)
                velocidade = giro / Time.unscaledDeltaTime;
            ocioso = esperaAposArraste;
        }

        private static bool PonteiroPressionado(out Vector2 posicao)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null)
            {
                posicao = mouse.position.ReadValue();
                return mouse.leftButton.isPressed;
            }

            var toque = UnityEngine.InputSystem.Touchscreen.current;
            if (toque != null && toque.primaryTouch.press.isPressed)
            {
                posicao = toque.primaryTouch.position.ReadValue();
                return true;
            }

            posicao = Vector2.zero;
            return false;
        }

        private static bool SobreUI(Vector2 posicao)
        {
            if (EventSystem.current == null)
                return false;

            var dados = new PointerEventData(EventSystem.current) { position = posicao };
            var achados = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(dados, achados);
            return achados.Count > 0;
        }

        /// <summary>
        /// Anima a troca de carro: encolhe girando, chama <paramref name="trocar"/> no ponto
        /// mais apagado e devolve o carro novo crescendo. <paramref name="sentido"/> −1/+1
        /// decide para que lado ele gira.
        ///
        /// <paramref name="aoConcluir"/> roda com o carro já na escala final — é onde quem
        /// enquadra a câmera deve agir, senão a caixa medida sai encolhida e o carro estoura
        /// a tela ao voltar ao tamanho normal.
        /// </summary>
        public void Trocar(System.Action trocar, int sentido = 1, System.Action aoConcluir = null)
        {
            if (troca != null)
            {
                // uma troca interrompida no meio deixaria escala/rotação fora de lugar
                StopCoroutine(troca);
                alvo.localScale = escalaBase;
            }
            troca = StartCoroutine(Trocando(trocar, sentido, aoConcluir));
        }

        private IEnumerator Trocando(System.Action trocar, int sentido, System.Action aoConcluir)
        {
            float metade = Mathf.Max(0.05f, duracaoDaTroca * 0.5f);
            Quaternion inicio = alvo.localRotation;
            Quaternion saida = inicio * Quaternion.Euler(0f, giroDaTroca * sentido, 0f);

            for (float t = 0f; t < metade; t += Time.unscaledDeltaTime)
            {
                float k = UIEase.OutQuad(t / metade);
                alvo.localRotation = Quaternion.Slerp(inicio, saida, k);
                alvo.localScale = escalaBase * Mathf.Lerp(1f, encolhimentoDaTroca, k);
                yield return null;
            }

            trocar?.Invoke();

            Quaternion entrada = saida * Quaternion.Euler(0f, giroDaTroca * sentido, 0f);
            alvo.localRotation = entrada;

            for (float t = 0f; t < metade; t += Time.unscaledDeltaTime)
            {
                float k = UIEase.OutBack(t / metade, 1.2f);
                alvo.localRotation = Quaternion.Slerp(entrada, saida, k);
                alvo.localScale = escalaBase * Mathf.LerpUnclamped(encolhimentoDaTroca, 1f, k);
                yield return null;
            }

            alvo.localRotation = saida;
            alvo.localScale = escalaBase;
            ocioso = esperaAposArraste;
            troca = null;

            aoConcluir?.Invoke();
        }
    }
}
