using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Contagem regressiva central (3 / 2 / 1 / VAI!) usando os spritesheets de Art/HUD.
    /// Cada número é um spritesheet 4x4 (16 frames) com a entrada e a saída JÁ animadas nos
    /// frames — então cada fase apenas reproduz sua sequência de frames uma vez.
    /// Dirigido pela largada REAL: assina os eventos estáticos de RaceManager.
    /// </summary>
    [DisallowMultipleComponent]
    public class CountdownHUDUI : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image numberImage;
        [SerializeField] private TMP_Text messageLabel;

        [Header("Frames por etapa (spritesheet 4x4 = 16 frames, em ordem)")]
        [SerializeField] private Sprite[] framesThree;
        [SerializeField] private Sprite[] framesTwo;
        [SerializeField] private Sprite[] framesOne;
        [SerializeField] private Sprite[] framesGo;

        [Header("Animação")]
        [Tooltip("Velocidade de reprodução dos frames (frames por segundo).")]
        [SerializeField] private float framesPerSecond = 19f;
        [Tooltip("Segura o último frame após a sequência (em vez de sumir bruscamente).")]
        [SerializeField] private bool holdLastFrame = true;
        [SerializeField] private float fadeOutDuration = 0.25f;

        private Sprite[] activeFrames;
        private bool playing;
        private bool fadingOut;
        private float playTimer;
        private float fadeTimer;

        private void Awake() => HideImmediate();

        private void OnEnable()
        {
            RaceManager.CountdownPhaseChanged += OnPhaseChanged;
            RaceManager.CountdownMessageChanged += OnMessageChanged;
            RaceManager.CountdownHidden += OnHidden;
        }

        private void OnDisable()
        {
            RaceManager.CountdownPhaseChanged -= OnPhaseChanged;
            RaceManager.CountdownMessageChanged -= OnMessageChanged;
            RaceManager.CountdownHidden -= OnHidden;
        }

        private void Update()
        {
            if (playing)
                AdvanceFrames();

            if (!fadingOut)
                return;

            fadeTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(fadeTimer / Mathf.Max(0.01f, fadeOutDuration));
            SetAlpha(1f - t);
            if (t >= 1f)
                HideImmediate();
        }

        private void AdvanceFrames()
        {
            if (activeFrames == null || activeFrames.Length == 0)
                return;

            playTimer += Time.unscaledDeltaTime;
            int index = Mathf.FloorToInt(playTimer * Mathf.Max(1f, framesPerSecond));

            if (index >= activeFrames.Length)
            {
                index = activeFrames.Length - 1;
                playing = false; // terminou a sequência; segura/limpa o último frame
                if (!holdLastFrame)
                {
                    if (numberImage != null) numberImage.enabled = false;
                    return;
                }
            }

            Sprite frame = activeFrames[Mathf.Clamp(index, 0, activeFrames.Length - 1)];
            if (numberImage != null && frame != null)
            {
                numberImage.sprite = frame;
                numberImage.enabled = true;
            }
        }

        private void OnPhaseChanged(RaceManager.CountdownPhase phase)
        {
            Sprite[] frames = phase switch
            {
                RaceManager.CountdownPhase.Three => framesThree,
                RaceManager.CountdownPhase.Two => framesTwo,
                RaceManager.CountdownPhase.One => framesOne,
                RaceManager.CountdownPhase.Go => framesGo,
                _ => null
            };

            if (frames == null || frames.Length == 0)
            {
                HideImmediate();
                return;
            }

            PlaySequence(frames);
        }

        private void OnMessageChanged(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                HideImmediate();
                return;
            }

            playing = false;
            fadingOut = false;
            SetAlpha(1f);

            if (numberImage != null)
                numberImage.enabled = false;

            if (messageLabel != null)
            {
                messageLabel.gameObject.SetActive(true);
                messageLabel.text = message.ToUpperInvariant();
            }
        }

        private void OnHidden()
        {
            if (group == null || group.alpha <= 0f)
            {
                HideImmediate();
                return;
            }

            playing = false;
            fadingOut = true;
            fadeTimer = 0f;
        }

        private void PlaySequence(Sprite[] frames)
        {
            activeFrames = frames;
            playing = true;
            fadingOut = false;
            playTimer = 0f;
            fadeTimer = 0f;
            SetAlpha(1f);

            if (messageLabel != null)
                messageLabel.gameObject.SetActive(false);

            if (numberImage != null)
            {
                numberImage.sprite = frames[0];
                numberImage.enabled = true;
            }
        }

        private void HideImmediate()
        {
            playing = false;
            fadingOut = false;
            playTimer = 0f;
            fadeTimer = 0f;
            activeFrames = null;
            SetAlpha(0f);

            if (numberImage != null)
                numberImage.enabled = false;
            if (messageLabel != null)
                messageLabel.gameObject.SetActive(false);
        }

        private void SetAlpha(float alpha)
        {
            if (group != null)
                group.alpha = alpha;
        }
    }
}
