using UnityEngine;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Flutuação vertical em laço — a keyframe `prBob` do protótipo:
    /// <code>0%,100% { translateY(0) }  50% { translateY(-8px) }</code>
    ///
    /// É o que faz os cards ITEM e CURA da bifurcação parecerem pickups flutuando em vez de
    /// cartazes colados na tela. Os dois usam o mesmo ritmo com 0,4 s de defasagem entre si: em
    /// fase, subiriam juntos e o par viraria um bloco só.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIBob : MonoBehaviour
    {
        [SerializeField] private float periodo = 1.6f;
        [SerializeField] private float amplitude = 8f;
        [Tooltip("Defasagem em segundos. O segundo card da bifurcação usa 0,4.")]
        [SerializeField] private float atraso;

        private RectTransform rect;
        private Vector2 repouso;
        private float t;

        private void Awake()
        {
            rect = (RectTransform)transform;
            repouso = rect.anchoredPosition;
        }

        private void OnEnable()
        {
            t = -atraso;
            rect.anchoredPosition = repouso;
        }

        private void OnDisable()
        {
            if (rect != null)
                rect.anchoredPosition = repouso;
        }

        private void Update()
        {
            t += Time.unscaledDeltaTime;
            if (t < 0f)
                return;

            float k = UIEase.InOutQuad(UIEase.PingPong(Mathf.Repeat(t / Mathf.Max(0.05f, periodo), 1f)));
            rect.anchoredPosition = repouso + Vector2.up * (amplitude * k);
        }
    }
}
