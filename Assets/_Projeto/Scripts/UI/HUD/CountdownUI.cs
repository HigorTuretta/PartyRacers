using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CountdownUI : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform stackRoot;
    [SerializeField] private CanvasGroup stackGroup;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private TMP_Text[] stepLabels;
    [SerializeField] private CanvasGroup[] stepGroups;
    [SerializeField] private Image[] stepBackdrops;
    [SerializeField] private RectTransform[] stepRects;

    [Header("Cores por etapa")]
    [SerializeField] private Color colorThree = new Color(1f, 0.18f, 0.14f, 1f);
    [SerializeField] private Color colorTwo = new Color(1f, 0.47f, 0.10f, 1f);
    [SerializeField] private Color colorOne = new Color(1f, 0.82f, 0.12f, 1f);
    [SerializeField] private Color colorGo = new Color(0.34f, 1f, 0.18f, 1f);
    [SerializeField] private Color inactiveColor = new Color(0.05f, 0.10f, 0.13f, 0.86f);

    [Header("Animacao")]
    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float punchSpeed = 6.5f;
    [SerializeField] private float fadeOutDuration = 0.32f;

    private static CountdownUI sceneInstance;
    private bool built;
    private bool fadingOut;
    private float punch;
    private float fadeTimer;
    private int activeStep = -1;

    public static CountdownUI EnsureSceneInstance()
    {
        if (sceneInstance != null && sceneInstance.gameObject != null && sceneInstance.isActiveAndEnabled)
            return sceneInstance;

        CountdownUI existing = FindAnyObjectByType<CountdownUI>(FindObjectsInactive.Exclude);
        if (existing != null)
        {
            sceneInstance = existing;
            sceneInstance.EnsureBuilt();
            return sceneInstance;
        }

        GameObject go = new GameObject("CountdownUI");
        sceneInstance = go.AddComponent<CountdownUI>();
        return sceneInstance;
    }

    private void Awake()
    {
        if (sceneInstance != null && sceneInstance != this && sceneInstance.isActiveAndEnabled)
        {
            enabled = false;
            return;
        }

        sceneInstance = this;
        EnsureBuilt();
        HideImmediate();
    }

    private void OnDestroy()
    {
        if (sceneInstance == this)
            sceneInstance = null;
    }

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
        if (stackRoot == null || !stackRoot.gameObject.activeSelf)
            return;

        punch = Mathf.MoveTowards(punch, 0f, punchSpeed * Time.unscaledDeltaTime);
        ApplyStepScale();

        if (!fadingOut)
            return;

        fadeTimer += Time.unscaledDeltaTime;
        float fade01 = Mathf.Clamp01(fadeTimer / Mathf.Max(0.01f, fadeOutDuration));
        SetAlpha(1f - fade01);

        if (fade01 >= 1f)
            HideImmediate();
    }

    private void OnPhaseChanged(RaceManager.CountdownPhase phase)
    {
        switch (phase)
        {
            case RaceManager.CountdownPhase.Three: ShowStep(0); break;
            case RaceManager.CountdownPhase.Two: ShowStep(1); break;
            case RaceManager.CountdownPhase.One: ShowStep(2); break;
            case RaceManager.CountdownPhase.Go: ShowStep(3); break;
            default: HideImmediate(); break;
        }
    }

    private void OnMessageChanged(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            HideImmediate();
            return;
        }

        EnsureBuilt();
        ShowRoot();
        activeStep = -1;
        punch = 0f;

        if (messageLabel != null)
            messageLabel.text = message.ToUpperInvariant();

        for (int i = 0; i < StepCount; i++)
            SetStepAlpha(i, 0.28f);
    }

    private void OnHidden() => BeginFadeOut();

    private void ShowStep(int index)
    {
        EnsureBuilt();
        ShowRoot();

        activeStep = Mathf.Clamp(index, 0, StepCount - 1);
        punch = 1f;

        if (messageLabel != null)
            messageLabel.text = "CONTAGEM REGRESSIVA";

        for (int i = 0; i < StepCount; i++)
        {
            Color stepColor = GetStepColor(i);
            SetStepAlpha(i, i == activeStep ? 1f : (i < activeStep ? 0.58f : 0.24f));

            if (stepLabels != null && i < stepLabels.Length && stepLabels[i] != null)
            {
                stepLabels[i].text = GetStepText(i);
                stepLabels[i].color = i == activeStep ? Color.white : stepColor;
            }

            if (stepBackdrops != null && i < stepBackdrops.Length && stepBackdrops[i] != null)
            {
                stepBackdrops[i].color = i == activeStep
                    ? Color.Lerp(new Color(0.04f, 0.07f, 0.09f, 0.96f), stepColor, 0.72f)
                    : inactiveColor;
            }
        }

        fadingOut = false;
        fadeTimer = 0f;
        SetAlpha(1f);
    }

    private void BeginFadeOut()
    {
        if (stackRoot == null || !stackRoot.gameObject.activeSelf)
        {
            HideImmediate();
            return;
        }

        fadingOut = true;
        fadeTimer = 0f;
    }

    private void HideImmediate()
    {
        fadingOut = false;
        fadeTimer = 0f;
        punch = 0f;
        activeStep = -1;
        SetAlpha(0f);

        if (stackRoot != null)
            stackRoot.gameObject.SetActive(false);
    }

    private void ShowRoot()
    {
        if (stackRoot != null && !stackRoot.gameObject.activeSelf)
            stackRoot.gameObject.SetActive(true);

        fadingOut = false;
        fadeTimer = 0f;
        SetAlpha(1f);
    }

    private void SetAlpha(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = alpha;
        if (stackGroup != null)
            stackGroup.alpha = alpha;
    }

    private void SetStepAlpha(int index, float alpha)
    {
        if (stepGroups == null || index < 0 || index >= stepGroups.Length || stepGroups[index] == null)
            return;

        stepGroups[index].alpha = alpha;
    }

    private void ApplyStepScale()
    {
        for (int i = 0; i < StepCount; i++)
        {
            if (stepRects == null || i >= stepRects.Length || stepRects[i] == null)
                continue;

            float scale = i == activeStep ? 1f + (punchScale - 1f) * punch : 1f;
            stepRects[i].localScale = new Vector3(scale, scale, 1f);
        }
    }

    private int StepCount => stepLabels != null ? Mathf.Min(stepLabels.Length, 4) : 0;

    private Color GetStepColor(int index) => index switch
    {
        0 => colorThree,
        1 => colorTwo,
        2 => colorOne,
        3 => colorGo,
        _ => Color.white
    };

    private static string GetStepText(int index) => index switch
    {
        0 => "3",
        1 => "2",
        2 => "1",
        3 => "VAI!",
        _ => string.Empty
    };

    private void EnsureBuilt()
    {
        if (built && stackRoot != null && StepCount >= 4)
            return;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        if (stackRoot == null || stepLabels == null || stepLabels.Length < 4)
            BuildFallback();

        built = true;
    }

    private void BuildFallback()
    {
        Transform parent = transform;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            GameObject canvasGo = new GameObject("CountdownCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            parent = canvasGo.transform;
        }

        stackRoot = NewRect("CountdownSideStack", parent);
        stackRoot.anchorMin = new Vector2(1f, 0.5f);
        stackRoot.anchorMax = new Vector2(1f, 0.5f);
        stackRoot.pivot = new Vector2(1f, 0.5f);
        stackRoot.anchoredPosition = new Vector2(-34f, 0f);
        stackRoot.sizeDelta = new Vector2(286f, 492f);

        Image backdrop = stackRoot.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.07f, 0.10f, 0.86f);
        backdrop.raycastTarget = false;

        stackGroup = stackRoot.gameObject.AddComponent<CanvasGroup>();
        stackGroup.blocksRaycasts = false;
        stackGroup.interactable = false;

        messageLabel = AddText("Title", "CONTAGEM REGRESSIVA", 24f, stackRoot, TextAlignmentOptions.Center);
        Anchor(messageLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(14f, -44f), new Vector2(-14f, -10f));

        stepLabels = new TMP_Text[4];
        stepGroups = new CanvasGroup[4];
        stepBackdrops = new Image[4];
        stepRects = new RectTransform[4];

        for (int i = 0; i < 4; i++)
        {
            RectTransform row = NewRect($"Step_{i}", stackRoot);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.offsetMin = new Vector2(24f, -126f - i * 92f);
            row.offsetMax = new Vector2(-24f, -58f - i * 92f);

            Image bg = row.gameObject.AddComponent<Image>();
            bg.color = inactiveColor;
            bg.raycastTarget = false;

            CanvasGroup rowGroup = row.gameObject.AddComponent<CanvasGroup>();
            rowGroup.blocksRaycasts = false;
            rowGroup.interactable = false;

            TMP_Text text = AddText("Text", GetStepText(i), i == 3 ? 48f : 70f, row, TextAlignmentOptions.Center);
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            stepLabels[i] = text;
            stepGroups[i] = rowGroup;
            stepBackdrops[i] = bg;
            stepRects[i] = row;
        }
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static TMP_Text AddText(string name, string text, float size, Transform parent, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        tmp.outlineColor = new Color(0.02f, 0.025f, 0.035f, 1f);
        tmp.outlineWidth = 0.18f;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        return tmp;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
