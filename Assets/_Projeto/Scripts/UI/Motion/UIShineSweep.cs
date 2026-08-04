using UnityEngine;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Faixa de luz que varre um trilho da esquerda para a direita, em laço. É o segundo sinal de
    /// "disponível" da barra de escudo (o primeiro é o brilho pulsante): em recarga a varredura é
    /// desligada, e é a ausência dela que comunica indisponível — o design não tem ícone nem botão
    /// de escudo para dizer isso com palavras.
    ///
    /// A faixa e o trilho já existem na cena. Este componente só desloca a faixa dentro do trilho;
    /// não cria, não dimensiona e não pinta nada. O deslocamento é medido pela largura do PAI, então
    /// a barra pode ser redesenhada à mão sem tocar em código.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class UIShineSweep : MonoBehaviour
    {
        [Header("Ritmo")]
        [Tooltip("Segundos de uma passagem completa. Escudo pronto = 2,4 s; escudo ativo = 1 s; " +
                 "agulha do dial = 2,6 s.")]
        [SerializeField] private float periodo = 2.4f;
        [Tooltip("Suavizar a passagem (easeInOutSine). Desligado = varredura linear, usada no ativo.")]
        [SerializeField] private bool suavizar = true;

        [Header("Percurso")]
        [Tooltip("Margem extra em pixels para a faixa entrar e sair fora do trilho. Negativo puxa " +
                 "as pontas para dentro — é o que impede a agulha do dial de encostar na borda e " +
                 "parecer travada a cada inversão.")]
        [SerializeField] private float folga = 70f;

        [Tooltip("Ida e volta em vez de laço. A faixa de luz do escudo é laço; a agulha do dial " +
                 "de busca é vaivém.")]
        [SerializeField] private bool vaivem;

        private RectTransform rect;
        private RectTransform trilho;
        private float t;
        private float yBase;

        private void Awake()
        {
            rect = (RectTransform)transform;
            trilho = rect.parent as RectTransform;
            yBase = rect.anchoredPosition.y;
        }

        private void OnEnable() => t = 0f;

        /// <summary>Troca o ritmo em runtime — o escudo acelera a varredura ao ser acionado.</summary>
        public void DefinirPeriodo(float segundos)
        {
            periodo = Mathf.Max(0.05f, segundos);
        }

        private void Update()
        {
            if (rect == null)
                return;

            t += Time.unscaledDeltaTime;

            float ciclo = periodo <= 0f ? 0f : Mathf.Repeat(t / periodo, 1f);
            if (vaivem)
                ciclo = UIEase.PingPong(ciclo);

            float k = suavizar ? UIEase.InOutQuad(ciclo) : ciclo;

            float largura = trilho != null ? trilho.rect.width : rect.rect.width;
            float inicio = -largura * 0.5f - folga;
            float fim = largura * 0.5f + folga;

            rect.anchoredPosition = new Vector2(Mathf.Lerp(inicio, fim, k), yBase);
        }
    }
}
