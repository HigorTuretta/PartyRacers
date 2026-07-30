using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Pulso contínuo de alfa e/ou escala. Usado no arco de perigo (tokens.json →
    /// movimento.arcoPerigo: 0,8 s aproximando, 0,25 s iminente), no ponto do chip AO VIVO e no
    /// destaque do botão pronto.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPulse : MonoBehaviour
    {
        [Header("Ritmo")]
        [Tooltip("Segundos de um ciclo completo (ida e volta).")]
        [SerializeField] private float periodo = 0.8f;

        [Header("O que pulsa")]
        [SerializeField] private bool alfa = true;
        [SerializeField] private float alfaMin = 0.35f;
        [SerializeField] private float alfaMax = 1f;

        [SerializeField] private bool escala;
        [SerializeField] private float escalaMin = 1f;
        [SerializeField] private float escalaMax = 1.08f;

        private Graphic grafico;
        private CanvasGroup grupo;
        private Vector3 escalaBase;
        private float t;

        private void Awake()
        {
            grupo = GetComponent<CanvasGroup>();
            grafico = GetComponent<Graphic>();
            escalaBase = transform.localScale;
        }

        private void OnEnable() => t = 0f;

        private void OnDisable()
        {
            transform.localScale = escalaBase;
            if (grupo != null) grupo.alpha = alfaMax;
            else if (grafico != null) Pintar(alfaMax);
        }

        /// <summary>Troca o ritmo em runtime — o arco alterna entre aproximando e iminente.</summary>
        public void DefinirPeriodo(float segundos)
        {
            periodo = Mathf.Max(0.05f, segundos);
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            float ciclo = periodo <= 0f ? 0f : Mathf.Repeat(t / periodo, 1f);
            float k = UIEase.InOutQuad(UIEase.PingPong(ciclo));

            if (alfa)
            {
                float a = Mathf.Lerp(alfaMin, alfaMax, k);
                if (grupo != null) grupo.alpha = a;
                else if (grafico != null) Pintar(a);
            }

            if (escala)
                transform.localScale = escalaBase * Mathf.Lerp(escalaMin, escalaMax, k);
        }

        private void Pintar(float a)
        {
            Color c = grafico.color;
            c.a = a;
            grafico.color = c;
        }
    }
}
