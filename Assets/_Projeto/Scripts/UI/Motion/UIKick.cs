using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Um empurrão de escala, disparado por código: cresce num quadro e assenta com folga.
    ///
    /// Diferente do <see cref="UIPulse"/>, que respira sem parar. Pulso contínuo comunica "isto
    /// está vivo"; empurrão comunica "isto acabou de MUDAR" — e num HUD de corrida é a segunda
    /// coisa que importa, porque o jogador não está olhando para o número quando ele muda. O
    /// movimento é o que traz o olho de volta.
    ///
    /// Também pode piscar a cor, para dano: o pisca chega antes da leitura do número.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIKick : MonoBehaviour
    {
        [Tooltip("Quanto cresce no pico.")]
        [SerializeField] private float pico = 1.22f;

        [Tooltip("Segundos até assentar.")]
        [SerializeField, Min(0.05f)] private float duracao = 0.28f;

        [Tooltip("Quando ligado, o gráfico pisca nesta cor junto com o empurrão.")]
        [SerializeField] private bool piscar;
        [SerializeField] private Color corDoPisca = Color.white;

        private Vector3 escalaBase = Vector3.one;
        private Graphic[] graficos;
        private Color[] coresBase;
        private float t = -1f;

        private void Awake()
        {
            escalaBase = transform.localScale;
            graficos = GetComponentsInChildren<Graphic>(true);
            coresBase = new Color[graficos.Length];
            for (int i = 0; i < graficos.Length; i++)
                coresBase[i] = graficos[i] != null ? graficos[i].color : Color.white;
        }

        /// <summary>Dispara o empurrão. Chamar de novo no meio reinicia — o último evento manda.</summary>
        public void Chutar() => t = 0f;

        private void OnDisable()
        {
            t = -1f;
            transform.localScale = escalaBase;
            Repintar(0f);
        }

        private void Update()
        {
            if (t < 0f)
                return;

            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracao);

            // Sobe rápido e volta devagar: o inverso parece elástico de brinquedo.
            float forma = k < 0.25f
                ? UIEase.OutQuad(k / 0.25f)
                : 1f - UIEase.OutQuad((k - 0.25f) / 0.75f);

            transform.localScale = escalaBase * Mathf.LerpUnclamped(1f, pico, forma);
            Repintar(forma);

            if (k < 1f)
                return;

            t = -1f;
            transform.localScale = escalaBase;
            Repintar(0f);
        }

        private void Repintar(float forma)
        {
            if (!piscar || graficos == null)
                return;

            for (int i = 0; i < graficos.Length; i++)
            {
                if (graficos[i] == null)
                    continue;

                graficos[i].color = Color.LerpUnclamped(coresBase[i], corDoPisca, forma);
            }
        }
    }
}
