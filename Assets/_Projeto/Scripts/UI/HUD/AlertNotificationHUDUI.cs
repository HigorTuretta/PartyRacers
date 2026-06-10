using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Alertas/notificações temporárias (ex.: "VOCÊ ACERTOU!", "FOI ATINGIDO!", "NITRO!").
    /// Fila simples: um banner por vez, com entrada e saída animadas. Todas as durações,
    /// escalas e posições ficam expostas no Inspector.
    /// </summary>
    public class AlertNotificationHUDUI : MonoBehaviour
    {
        [System.Serializable]
        public struct AlertConfig
        {
            public RaceHudEventKind kind;
            public string text;
            public Color color;
            public Sprite background;
        }

        [Header("Banner reutilizável")]
        [SerializeField] private RectTransform item;
        [SerializeField] private CanvasGroup itemGroup;
        [SerializeField] private TMP_Text itemLabel;
        [SerializeField] private Image itemBackground;

        [Header("Tempos (s)")]
        [SerializeField] private float displayTime = 1.1f;
        [SerializeField] private float fadeInDuration = 0.18f;
        [SerializeField] private float fadeOutDuration = 0.28f;

        [Header("Escala")]
        [SerializeField] private float scaleStart = 0.6f;
        [SerializeField] private float scaleEnd = 1f;

        [Header("Posição (offset relativo à posição base do banner)")]
        [SerializeField] private Vector2 enterOffset = new Vector2(0f, 40f);
        [SerializeField] private Vector2 exitOffset = new Vector2(0f, -30f);

        [Header("Mensagens por evento")]
        [SerializeField]
        private AlertConfig[] configs =
        {
            new AlertConfig { kind = RaceHudEventKind.HitOpponent, text = "VOCÊ ACERTOU!", color = new Color(1f, 0.78f, 0.18f, 1f) },
            new AlertConfig { kind = RaceHudEventKind.GotHit, text = "FOI ATINGIDO!", color = new Color(1f, 0.32f, 0.28f, 1f) },
            new AlertConfig { kind = RaceHudEventKind.PowerCollected, text = "PODER!", color = new Color(0.45f, 0.85f, 1f, 1f) },
            new AlertConfig { kind = RaceHudEventKind.PowerUsed, text = "USOU PODER", color = new Color(0.75f, 0.7f, 1f, 1f) },
            new AlertConfig { kind = RaceHudEventKind.Nitro, text = "NITRO!", color = new Color(0.35f, 1f, 0.55f, 1f) }
        };

        private readonly Queue<AlertRequest> queue = new Queue<AlertRequest>();
        private Vector2 basePosition;
        private bool playing;
        private bool initialized;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
                return;

            if (item != null)
                basePosition = item.anchoredPosition;
            HideItem();
            initialized = true;
        }

        public void Show(RaceHudEventKind kind)
        {
            if (TryGetConfig(kind, out AlertConfig config))
                Show(config.text, config.color, config.background);
        }

        public void Show(string text, Color color, Sprite background = null)
        {
            if (string.IsNullOrEmpty(text))
                return;

            Initialize();
            queue.Enqueue(new AlertRequest { Text = text, Color = color, Background = background });

            if (!playing && isActiveAndEnabled)
                StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            playing = true;

            while (queue.Count > 0)
            {
                AlertRequest request = queue.Dequeue();
                yield return PlayOne(request);
            }

            playing = false;
            HideItem();
        }

        private IEnumerator PlayOne(AlertRequest request)
        {
            if (item == null)
                yield break;

            if (itemLabel != null)
            {
                itemLabel.text = request.Text;
                itemLabel.color = request.Color;
            }

            if (itemBackground != null)
            {
                if (request.Background != null)
                    itemBackground.sprite = request.Background;
                itemBackground.enabled = itemBackground.sprite != null;
            }

            // Entrada
            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, fadeInDuration)));
                Apply(k, Vector2.Lerp(basePosition + enterOffset, basePosition, k), Mathf.Lerp(scaleStart, scaleEnd, k));
                yield return null;
            }
            Apply(1f, basePosition, scaleEnd);

            // Espera
            float hold = 0f;
            while (hold < displayTime)
            {
                hold += Time.unscaledDeltaTime;
                yield return null;
            }

            // Saída
            t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeOutDuration));
                Apply(1f - k, Vector2.Lerp(basePosition, basePosition + exitOffset, k), scaleEnd);
                yield return null;
            }

            HideItem();
        }

        private void Apply(float alpha, Vector2 position, float scale)
        {
            if (itemGroup != null)
                itemGroup.alpha = alpha;
            if (item != null)
            {
                item.anchoredPosition = position;
                item.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void HideItem()
        {
            if (itemGroup != null)
                itemGroup.alpha = 0f;
            if (item != null)
            {
                item.anchoredPosition = basePosition;
                item.localScale = Vector3.one;
            }
        }

        private bool TryGetConfig(RaceHudEventKind kind, out AlertConfig config)
        {
            if (configs != null)
            {
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i].kind == kind)
                    {
                        config = configs[i];
                        return true;
                    }
                }
            }

            config = default;
            return false;
        }

        private struct AlertRequest
        {
            public string Text;
            public Color Color;
            public Sprite Background;
        }
    }
}
