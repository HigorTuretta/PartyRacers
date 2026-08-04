using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Emula o `box-shadow` animado do protótipo — a keyframe `prGlow`:
    /// <code>0%,100% { 0 0 16px rgba(53,167,255,.45) }  50% { 0 0 34px rgba(90,200,245,.85) }</code>
    ///
    /// No CSS o glow cresce em RAIO e em opacidade ao mesmo tempo. No UGUI não existe box-shadow,
    /// então o equivalente é uma Image de falloff radial atrás do elemento: o raio vira ESCALA e a
    /// opacidade vira alfa. Pulsar só o alfa (que é o que o UIPulse faz) perde metade do efeito —
    /// é a respiração do tamanho que faz o escudo pronto "chamar" pelo canto do olho.
    ///
    /// A Image e o sprite de falloff já estão na cena; este componente só anima os dois valores.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIGlowPulse : MonoBehaviour
    {
        [Header("Ritmo")]
        [Tooltip("Segundos do ciclo completo. prGlow do escudo pronto = 1,8 s.")]
        [SerializeField] private float periodo = 1.8f;

        [Header("Raio (vira escala)")]
        [Tooltip("Raio do glow no vale do ciclo, em pixels de CSS.")]
        [SerializeField] private float raioMin = 16f;
        [Tooltip("Raio do glow no pico do ciclo.")]
        [SerializeField] private float raioMax = 34f;
        [Tooltip("Tamanho do elemento que recebe o glow, em pixels. O sprite de falloff é escalado " +
                 "para cobrir o elemento MAIS o raio; sem isto o glow de uma barra larga ficaria " +
                 "com a mesma sobra de um chip pequeno.")]
        [SerializeField] private Vector2 tamanhoBase = new Vector2(486f, 24f);

        [Header("Opacidade")]
        [SerializeField, Range(0f, 1f)] private float alfaMin = 0.45f;
        [SerializeField, Range(0f, 1f)] private float alfaMax = 0.85f;

        [Header("Alvo")]
        [Tooltip("Grupo que reúne as camadas do halo. Vazio usa o deste GameObject.")]
        [SerializeField] private CanvasGroup alvo;

        private RectTransform rect;
        private float t;

        private void Awake()
        {
            rect = (RectTransform)transform;

            if (alvo == null)
                alvo = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            t = 0f;
            Aplicar(0f);
        }

        /// <summary>Troca o ritmo e a intensidade em runtime — o escudo ativo é mais forte que o pronto.</summary>
        public void Definir(float novoPeriodo, float novoRaioMin, float novoRaioMax,
                            float novoAlfaMin, float novoAlfaMax)
        {
            periodo = Mathf.Max(0.05f, novoPeriodo);
            raioMin = novoRaioMin;
            raioMax = novoRaioMax;
            alfaMin = novoAlfaMin;
            alfaMax = novoAlfaMax;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            Aplicar(UIEase.InOutQuad(UIEase.PingPong(Mathf.Repeat(t / Mathf.Max(0.05f, periodo), 1f))));
        }

        private void Aplicar(float k)
        {
            if (rect == null)
                return;

            // O raio do box-shadow vira crescimento do grupo: as camadas do halo esticam junto,
            // então o degradê inteiro abre e fecha em vez de só clarear.
            float raio = Mathf.Lerp(raioMin, raioMax, k);
            rect.sizeDelta = tamanhoBase + Vector2.one * ((raio - raioMin) * 2f);

            if (alvo != null)
                alvo.alpha = Mathf.Lerp(alfaMin, alfaMax, k);
        }
    }
}
