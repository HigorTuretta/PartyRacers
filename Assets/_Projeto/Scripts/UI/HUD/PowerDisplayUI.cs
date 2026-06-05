using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Widget de exibição de poder (apresentação pura + intenção de uso).
// Recebe estado via SetPower()/ClearPower(); dispara onUsePower quando o jogador clica "Usar"
// (mouse) — o HUDRootUI conecta isso ao KartPowerUser. Teclado/gamepad continuam pelo input do
// KartPowerUser; o botão também é navegável pelo EventSystem.
public class PowerDisplayUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Image background;
    [SerializeField] private Image swatch;
    [SerializeField] private Image panelAccent;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text iconGlyph;
    [SerializeField] private TMP_Text powerName;
    [SerializeField] private TMP_Text itemCount;
    [SerializeField] private TMP_Text controlHint;
    [SerializeField] private Button useButton;
    [SerializeField] private Image useCooldownFill;

    [Header("Ícones por poder (opcional)")]
    [SerializeField] private Sprite iconNone;
    [SerializeField] private Sprite iconSwap;
    [SerializeField] private Sprite iconRocket;
    [SerializeField] private Sprite iconShield;

    [Header("Cores por poder")]
    [SerializeField] private Color colorNone = new Color(0.45f, 0.48f, 0.55f, 1f);
    [SerializeField] private Color colorSwap = new Color(0.30f, 0.78f, 1f, 1f);
    [SerializeField] private Color colorRocket = new Color(1f, 0.50f, 0.15f, 1f);
    [SerializeField] private Color colorShield = new Color(0.35f, 1f, 0.55f, 1f);
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.06f, 0.78f);

    [Header("Animacao")]
    [SerializeField] private float readySweepDuration = 1.15f;
    [SerializeField] private float iconPunchScale = 1.12f;
    [SerializeField] private float iconPunchSpeed = 7.5f;

    [Header("Eventos")]
    [Tooltip("Disparado quando o jogador aciona 'Usar' (mouse/teclado/gamepad via EventSystem).")]
    public UnityEvent onUsePower;

    private KartPowerType currentType = KartPowerType.None;
    private bool showingUseButton;
    private float readyTimer;
    private float iconPunch;

    private void Awake()
    {
        if (useButton != null)
            useButton.onClick.AddListener(HandleUseClicked);
        WarnMissing();
    }

    private void OnDestroy()
    {
        if (useButton != null)
            useButton.onClick.RemoveListener(HandleUseClicked);
    }

    /// <summary>Define o poder atual e se está pronto para uso (botão "Usar" visível).</summary>
    private void Update()
    {
        iconPunch = Mathf.MoveTowards(iconPunch, 0f, iconPunchSpeed * Time.unscaledDeltaTime);

        if (iconGlyph != null)
        {
            float scale = 1f + (iconPunchScale - 1f) * iconPunch;
            iconGlyph.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        if (!showingUseButton)
        {
            readyTimer = 0f;
            if (useCooldownFill != null)
                useCooldownFill.fillAmount = 0f;
            return;
        }

        readyTimer += Time.unscaledDeltaTime;

        if (useCooldownFill != null)
        {
            useCooldownFill.fillAmount = Mathf.Repeat(readyTimer / Mathf.Max(0.1f, readySweepDuration), 1f);
            useCooldownFill.color = new Color(1f, 1f, 1f, 0.18f);
        }
    }

    public void SetPower(KartPowerType type, string displayName, bool isReady)
    {
        bool changed = currentType != type;
        currentType = type;

        Color color = GetColor(type);

        if (background != null)
            background.color = Color.Lerp(backdropColor, color, 0.18f);

        if (swatch != null)
            swatch.color = color;

        if (panelAccent != null)
            panelAccent.color = color;

        if (icon != null)
        {
            Sprite sprite = GetIcon(type);
            icon.sprite = sprite;
            icon.color = Color.white;
            icon.enabled = sprite != null;
        }

        if (iconGlyph != null)
        {
            iconGlyph.text = GetGlyph(type);
            iconGlyph.color = type == KartPowerType.None
                ? new Color(0.82f, 0.86f, 0.94f, 0.88f)
                : color;
        }

        if (powerName != null)
        {
            powerName.text = string.IsNullOrEmpty(displayName)
                ? (type == KartPowerType.None ? "NENHUM" : type.ToString().ToUpperInvariant())
                : displayName.ToUpperInvariant();
            powerName.color = type == KartPowerType.None
                ? new Color(0.82f, 0.86f, 0.94f, 0.92f)
                : Color.white;
        }

        if (itemCount != null)
            itemCount.text = isReady && type != KartPowerType.None ? "1" : "0";

        if (controlHint != null)
            controlHint.text = "RT";

        bool showUse = isReady && type != KartPowerType.None;
        if (useButton != null && useButton.gameObject.activeSelf != showUse)
            useButton.gameObject.SetActive(showUse);
        showingUseButton = showUse;

        if (changed && type != KartPowerType.None)
            iconPunch = 1f;
    }

    /// <summary>Limpa o poder (estado "Nenhum", esconde botão).</summary>
    public void ClearPower() => SetPower(KartPowerType.None, "Nenhum", false);

    private void HandleUseClicked()
    {
        if (currentType == KartPowerType.None)
            return;

        onUsePower?.Invoke();
    }

    private Color GetColor(KartPowerType type) => type switch
    {
        KartPowerType.SwapPosition => colorSwap,
        KartPowerType.Rocket => colorRocket,
        KartPowerType.Shield => colorShield,
        _ => colorNone,
    };

    private Sprite GetIcon(KartPowerType type) => type switch
    {
        KartPowerType.SwapPosition => iconSwap != null ? iconSwap : iconNone,
        KartPowerType.Rocket => iconRocket != null ? iconRocket : iconNone,
        KartPowerType.Shield => iconShield != null ? iconShield : iconNone,
        _ => iconNone,
    };

    private string GetGlyph(KartPowerType type) => type switch
    {
        KartPowerType.SwapPosition => "\u21c4",
        KartPowerType.Rocket => "\u25b2",
        KartPowerType.Shield => "\u25c6",
        _ => "\u25cb",
    };

    private void WarnMissing()
    {
        if (powerName == null)
            Debug.LogWarning($"{name}: PowerDisplayUI sem 'powerName' (TMP_Text). Nome do poder não será exibido.", this);
    }
}
