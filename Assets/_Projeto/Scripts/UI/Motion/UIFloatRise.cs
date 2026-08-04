using UnityEngine;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Sobe e some. É o movimento dos números de dano e cura (tokens-v2 →
    /// movimento.numeroFlutuante: 40 px em 0,9 s).
    ///
    /// O objeto já existe na cena, montado e estilizado; este componente só o anima e o desliga no
    /// fim. Quem mostra o número é o binder, chamando <see cref="Disparar"/>.
    /// </summary>
    // Sem [RequireComponent(CanvasGroup)] de propósito: a exigência faz o Unity recusar
    // DestroyImmediate durante a montagem em editor, e o código abaixo já trata a ausência do
    // grupo (o número sobe, só não esmaece).
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIFloatRise : MonoBehaviour
    {
        [Header("Movimento")]
        [SerializeField] private float subida = 40f;
        [SerializeField] private float duracao = 0.9f;
        [Tooltip("Fração da duração em que o número ainda está totalmente opaco antes de começar " +
                 "a sumir. 0,35 = fica legível por um terço do tempo e some no resto.")]
        [SerializeField, Range(0f, 0.9f)] private float atrasoDoFade = 0.35f;

        private RectTransform rect;
        private CanvasGroup grupo;
        private Vector2 repouso;
        private float t = -1f;

        /// <summary>True enquanto a animação está rodando — o binder usa para escolher um slot livre.</summary>
        public bool EmUso => t >= 0f;

        private void Awake()
        {
            rect = (RectTransform)transform;
            grupo = GetComponent<CanvasGroup>();
            repouso = rect.anchoredPosition;
        }

        /// <summary>Reinicia a animação do começo. Chamado toda vez que um número novo aparece.</summary>
        public void Disparar()
        {
            if (rect == null)
            {
                rect = (RectTransform)transform;
                repouso = rect.anchoredPosition;
            }

            t = 0f;
            if (grupo != null)
                grupo.alpha = 1f;

            rect.anchoredPosition = repouso;
        }

        private void OnDisable() => t = -1f;

        private void Update()
        {
            if (t < 0f)
                return;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.01f, duracao));

            rect.anchoredPosition = repouso + Vector2.up * (subida * UIEase.OutQuad(k));

            if (grupo != null)
            {
                float fade = Mathf.InverseLerp(atrasoDoFade, 1f, k);
                grupo.alpha = 1f - fade;
            }

            if (k < 1f)
                return;

            t = -1f;
            gameObject.SetActive(false);
        }
    }
}
