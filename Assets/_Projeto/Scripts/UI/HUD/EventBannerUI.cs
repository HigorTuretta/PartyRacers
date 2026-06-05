using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventBannerUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Image background;
    [SerializeField] private Image accent;
    [SerializeField] private TMP_Text iconLabel;
    [SerializeField] private TMP_Text messageLabel;

    [Header("Animacao")]
    [SerializeField] private Vector2 hiddenOffset = new Vector2(360f, 0f);
    [SerializeField] private float slideSpeed = 10f;
    [SerializeField] private float holdDuration = 1.35f;
    [SerializeField] private float punchScale = 1.08f;
    [SerializeField] private float punchSpeed = 8f;

    private float visibleAmount;
    private float holdTimer;
    private float punch;
    private Vector2 shownPosition;
    private bool initialized;

    private void Awake()
    {
        EnsureReferences();
        HideImmediate();
    }

    private void Update()
    {
        EnsureReferences();

        holdTimer = Mathf.MoveTowards(holdTimer, 0f, Time.unscaledDeltaTime);
        float target = holdTimer > 0f ? 1f : 0f;
        visibleAmount = Mathf.MoveTowards(visibleAmount, target, slideSpeed * Time.unscaledDeltaTime);
        punch = Mathf.MoveTowards(punch, 0f, punchSpeed * Time.unscaledDeltaTime);

        float eased = 1f - Mathf.Pow(1f - visibleAmount, 3f);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = eased;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (panel != null)
        {
            panel.anchoredPosition = Vector2.Lerp(shownPosition + hiddenOffset, shownPosition, eased);
            float scale = 1f + (punchScale - 1f) * punch;
            panel.localScale = new Vector3(scale, scale, 1f);
        }

        if (gameObject.activeSelf != (visibleAmount > 0.001f || target > 0f))
            gameObject.SetActive(visibleAmount > 0.001f || target > 0f);
    }

    public void Show(RaceHudEventKind kind, KartPowerType powerType = KartPowerType.None)
    {
        EnsureReferences();
        ApplyStyle(kind, powerType);

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        holdTimer = holdDuration;
        visibleAmount = Mathf.Max(visibleAmount, 0.12f);
        punch = 1f;
    }

    private void ApplyStyle(RaceHudEventKind kind, KartPowerType powerType)
    {
        Color color = GetColor(kind, powerType);

        if (background != null)
            background.color = Color.Lerp(new Color(0.02f, 0.05f, 0.07f, 0.92f), color, 0.24f);
        if (accent != null)
            accent.color = color;
        if (iconLabel != null)
        {
            iconLabel.text = GetIcon(kind, powerType);
            iconLabel.color = color;
        }
        if (messageLabel != null)
        {
            messageLabel.text = GetMessage(kind);
            messageLabel.color = Color.white;
        }
    }

    private static string GetMessage(RaceHudEventKind kind) => kind switch
    {
        RaceHudEventKind.HitOpponent => "VOCE ACERTOU!",
        RaceHudEventKind.GotHit => "FOI ATINGIDO!",
        RaceHudEventKind.Nitro => "NITRO!",
        RaceHudEventKind.PowerCollected => "POWER-UP!",
        RaceHudEventKind.PowerUsed => "USOU!",
        _ => "EVENTO!"
    };

    private static string GetIcon(RaceHudEventKind kind, KartPowerType powerType)
    {
        if (kind == RaceHudEventKind.Nitro)
            return "\u26a1";
        if (kind == RaceHudEventKind.GotHit)
            return "!";
        if (kind == RaceHudEventKind.HitOpponent)
            return "\u25cf";

        return powerType switch
        {
            KartPowerType.Rocket => "\u25b2",
            KartPowerType.Shield => "\u25c6",
            KartPowerType.SwapPosition => "\u21c4",
            _ => "*"
        };
    }

    private static Color GetColor(RaceHudEventKind kind, KartPowerType powerType)
    {
        if (kind == RaceHudEventKind.GotHit)
            return new Color(1f, 0.12f, 0.10f, 1f);
        if (kind == RaceHudEventKind.HitOpponent)
            return new Color(0.15f, 0.72f, 1f, 1f);
        if (kind == RaceHudEventKind.Nitro)
            return new Color(0.42f, 1f, 0.12f, 1f);

        return powerType switch
        {
            KartPowerType.Rocket => new Color(1f, 0.50f, 0.15f, 1f),
            KartPowerType.Shield => new Color(0.35f, 1f, 0.55f, 1f),
            KartPowerType.SwapPosition => new Color(0.30f, 0.78f, 1f, 1f),
            _ => new Color(1f, 0.82f, 0.24f, 1f)
        };
    }

    private void EnsureReferences()
    {
        if (initialized)
            return;

        if (panel == null)
            panel = transform as RectTransform;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (panel != null)
            shownPosition = panel.anchoredPosition;

        initialized = true;
    }

    private void HideImmediate()
    {
        visibleAmount = 0f;
        holdTimer = 0f;
        punch = 0f;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        if (panel != null)
            panel.anchoredPosition = shownPosition + hiddenOffset;

        gameObject.SetActive(false);
    }
}
