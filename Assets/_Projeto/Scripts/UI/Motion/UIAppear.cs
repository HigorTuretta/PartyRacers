using UnityEngine;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Entrada de um bloco de UI: aparece deslizando e crescendo. Serve para painel de tela e
    /// para item de lista — no item, <see cref="atrasoPorIrmao"/> faz a lista entrar escalonada
    /// em vez de tudo de uma vez.
    ///
    /// Só mexe em alfa, escala e posição de algo que já está montado; a posição de repouso é a
    /// que o designer deixou no Inspector, e a animação sempre termina exatamente nela.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIAppear : MonoBehaviour
    {
        [Header("Tempo")]
        [SerializeField] private float duracao = 0.22f;
        [SerializeField] private float atraso;
        [Tooltip("Multiplicado pelo índice do objeto entre os irmãos: entrada em cascata.")]
        [SerializeField] private float atrasoPorIrmao;

        [Header("De onde vem")]
        [SerializeField] private Vector2 deslocamento = new Vector2(0f, -28f);
        [SerializeField] private float escalaInicial = 0.94f;
        [SerializeField] private bool comFade = true;

        [Header("Curva")]
        [Tooltip("Passa do alvo e volta — deixa o movimento mais 'cartoon'.")]
        [SerializeField] private bool comRecuo = true;

        private RectTransform rect;
        private CanvasGroup grupo;
        private Vector2 repouso;
        private Vector3 escalaRepouso;
        private float tempo, espera;
        private bool animando;

        private void Awake()
        {
            rect = (RectTransform)transform;
            repouso = rect.anchoredPosition;
            escalaRepouso = rect.localScale;

            if (comFade)
            {
                grupo = GetComponent<CanvasGroup>();
                if (grupo == null) grupo = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable() => Tocar();

        /// <summary>Recomeça a entrada. O binder chama isto quando repovoa uma lista.</summary>
        public void Tocar()
        {
            if (rect == null)
                return;

            espera = atraso + atrasoPorIrmao * Mathf.Max(0, transform.GetSiblingIndex());
            tempo = 0f;
            animando = true;
            Aplicar(0f);
        }

        private void Update()
        {
            if (!animando)
                return;

            if (espera > 0f)
            {
                espera -= Time.unscaledDeltaTime;
                return;
            }

            tempo += Time.unscaledDeltaTime;
            float t = duracao <= 0f ? 1f : Mathf.Clamp01(tempo / duracao);
            Aplicar(t);

            if (t < 1f)
                return;

            animando = false;
            // garante o repouso exato: erro de float não pode deixar o layout 1px torto
            rect.anchoredPosition = repouso;
            rect.localScale = escalaRepouso;
            if (grupo != null) grupo.alpha = 1f;
        }

        private void Aplicar(float t)
        {
            float k = comRecuo ? UIEase.OutBack(t, 1.1f) : UIEase.OutQuad(t);
            rect.anchoredPosition = repouso + deslocamento * (1f - k);
            rect.localScale = escalaRepouso * Mathf.LerpUnclamped(escalaInicial, 1f, k);
            if (grupo != null) grupo.alpha = UIEase.OutQuad(t);
        }
    }
}
