using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(RectTransform))]
public class KartHUDOverlay : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private KartRaceTracker raceTracker;
    [SerializeField] private KartPowerInventory powerInventory;
    [SerializeField] private RaceManager raceManager;

    [Header("Limites do Velocímetro")]
    [SerializeField] private float maxDisplaySpeedKmh = 200f;

    [Header("Marchas")]
    [Tooltip("Limites de velocidade (km/h) que separam cada marcha visual.")]
    [SerializeField] private float[] gearTopSpeedsKmh = { 35f, 70f, 110f, 150f, 200f };

    [Header("Estilo — Paleta")]
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.06f, 0.78f);
    [SerializeField] private Color outerRingColor = new Color(0.16f, 0.18f, 0.20f, 0.92f);
    [SerializeField] private Color accentColor = new Color(0.15f, 0.72f, 1f, 1f);
    [SerializeField] private Color accentHighColor = new Color(1f, 0.42f, 0.18f, 1f);
    [SerializeField] private Color textPrimary = new Color(1f, 1f, 1f, 0.98f);
    [SerializeField] private Color textSecondary = new Color(0.82f, 0.86f, 0.94f, 0.92f);
    [SerializeField] private Color outlineColor = new Color(0.03f, 0.04f, 0.08f, 1f);
    [SerializeField, Range(0f, 1f)] private float outlineWidth = 0.32f;
    [SerializeField] private Color cartoonYellow = new Color(1f, 0.82f, 0.24f, 1f);
    [SerializeField] private Color countdownGoColor = new Color(0.42f, 1f, 0.62f, 1f);

    [Header("Layout inicial (usado só ao construir do zero)")]
    [SerializeField] private Vector2 speedoSize = new Vector2(230f, 108f);
    [SerializeField] private Vector2 speedoAnchoredPosition = new Vector2(-32f, 30f);
    [SerializeField] private Vector2 gearSize = new Vector2(86f, 108f);
    [SerializeField] private Vector2 gearAnchoredPosition = new Vector2(-276f, 30f);
    [SerializeField] private Vector2 lapSize = new Vector2(190f, 62f);
    [SerializeField] private Vector2 lapAnchoredPosition = new Vector2(0f, -24f);
    [SerializeField] private Vector2 powerSize = new Vector2(250f, 72f);
    [SerializeField] private Vector2 powerAnchoredPosition = new Vector2(-30f, -24f);
    [SerializeField] private Vector2 countdownSize = new Vector2(500f, 280f);

    [Header("Animação")]
    [SerializeField] private float speedNumberSmooth = 16f;
    [SerializeField] private float fillSmooth = 12f;
    [SerializeField] private float gearPunchScale = 1.18f;
    [SerializeField] private float gearPunchSpeed = 9f;
    [SerializeField] private float lapPunchScale = 1.28f;
    [SerializeField] private float lapPunchSpeed = 5.5f;
    [SerializeField] private float powerPunchScale = 1.22f;
    [SerializeField] private float powerPunchSpeed = 6f;
    [SerializeField] private float countdownPunchScale = 1.55f;
    [SerializeField] private float countdownPunchSpeed = 4.5f;

    [Header("Contador Inicial")]
    [SerializeField] private bool driveOwnCountdown = false;
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float goMessageDuration = 0.8f;

    // --- Widgets persistidos (preenchidos pelo botão "Build HUD") ---
    [Header("Widgets construídos (não mexer)")]
    [SerializeField] private RectTransform speedoRoot;
    [SerializeField] private Image speedoFill;
    [SerializeField] private TMP_Text speedNumberText;

    [SerializeField] private RectTransform gearRoot;
    [SerializeField] private TMP_Text gearNumberText;
    [SerializeField] private Image gearBackdrop;

    [SerializeField] private RectTransform lapRoot;
    [SerializeField] private Image lapBackdrop;
    [SerializeField] private TMP_Text lapNumberText;

    [SerializeField] private RectTransform powerRoot;
    [SerializeField] private Image powerBackdrop;
    [SerializeField] private Image powerSwatch;
    [SerializeField] private TMP_Text powerNameText;

    [SerializeField] private RectTransform countdownRoot;
    [SerializeField] private TMP_Text countdownText;
    private bool countdownDriven;

    // Estado
    private float displayedSpeed;
    private float displayedFill;
    private int previousGear = -1;
    private int previousLap = -1;
    private KartPowerType previousPower = (KartPowerType)(-1);
    private string previousCountdownText = "";
    private float gearPunchTimer;
    private float lapPunchTimer;
    private float powerPunchTimer;
    private float countdownPunchTimer;

    private void Reset()
    {
        AutoWire();

#if UNITY_EDITOR
        if (!Application.isPlaying && transform.childCount == 0)
            EditorApplication.delayCall += () => { if (this != null) BuildHUD(); };
#endif
    }

    private void Awake()
    {
        AutoWire();

        if (!HasAllWidgets())
            BuildHUD();

        if (raceManager != null && countdownText != null)
            raceManager.SetCountdownText(countdownText);
    }

    private void Start()
    {
        if (driveOwnCountdown && !countdownDriven)
            StartCoroutine(RunCountdown());
    }

    private void AutoWire()
    {
        if (kart == null) kart = FindAnyObjectByType<KartController>();
        if (raceTracker == null) raceTracker = FindAnyObjectByType<KartRaceTracker>();
        if (powerInventory == null) powerInventory = FindAnyObjectByType<KartPowerInventory>();
        if (raceManager == null) raceManager = FindAnyObjectByType<RaceManager>();
    }

    private bool HasAllWidgets()
    {
        return speedoRoot != null && speedNumberText != null
            && gearRoot != null && gearNumberText != null
            && lapRoot != null && lapNumberText != null
            && powerRoot != null && powerNameText != null
            && countdownRoot != null && countdownText != null;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        UpdateSpeedometer(dt);
        UpdateGearDisplay(dt);
        UpdateLapDisplay(dt);
        UpdatePowerDisplay(dt);
        UpdateCountdownAnim(dt);
    }

    // ---------------------------------------------------------------------
    // ContextMenu — botões disponíveis no inspector (clique no ícone de engrenagem)
    // ---------------------------------------------------------------------
    [ContextMenu("Build HUD (criar/recriar widgets)")]
    public void BuildHUD()
    {
        ClearHUD();

        BuildSpeedometer();
        BuildGearIndicator();
        BuildLapCounter();
        BuildPowerDisplay();
        BuildCountdown();

        MarkSceneDirty();
    }

    [ContextMenu("Clear HUD (apagar widgets)")]
    public void ClearHUD()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (Application.isPlaying)
                Destroy(child.gameObject);
            else
                DestroyImmediate(child.gameObject);
        }

        speedoRoot = null; speedoFill = null; speedNumberText = null;
        gearRoot = null; gearNumberText = null; gearBackdrop = null;
        lapRoot = null; lapBackdrop = null; lapNumberText = null;
        powerRoot = null; powerBackdrop = null; powerSwatch = null; powerNameText = null;
        countdownRoot = null; countdownText = null;

        MarkSceneDirty();
    }

    private void MarkSceneDirty()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
            EditorSceneManagerSetDirty();
        }
#endif
    }

#if UNITY_EDITOR
    private void EditorSceneManagerSetDirty()
    {
        if (gameObject.scene.IsValid())
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif

    // ---------------------------------------------------------------------
    // Atualizações
    // ---------------------------------------------------------------------
    private void UpdateSpeedometer(float dt)
    {
        if (kart == null || speedNumberText == null)
            return;

        float speedKmh = kart.SpeedKmh;
        float speed01 = Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxDisplaySpeedKmh));

        displayedSpeed = Mathf.MoveTowards(displayedSpeed, speedKmh, speedNumberSmooth * Mathf.Max(speedKmh, 30f) * dt);
        displayedFill = Mathf.MoveTowards(displayedFill, speed01, fillSmooth * dt);

        speedNumberText.text = Mathf.RoundToInt(displayedSpeed).ToString();

        if (speedoFill != null)
        {
            speedoFill.fillAmount = displayedFill * 0.75f;
            speedoFill.color = Color.Lerp(accentColor, accentHighColor, Mathf.Clamp01((displayedFill - 0.65f) / 0.35f));
        }
    }

    private void UpdateGearDisplay(float dt)
    {
        if (gearNumberText == null) return;

        int gear = ComputeGearIndex(kart != null ? kart.SpeedKmh : 0f);
        int gearCount = gearTopSpeedsKmh != null && gearTopSpeedsKmh.Length > 0 ? gearTopSpeedsKmh.Length : 5;

        if (gear != previousGear)
        {
            if (previousGear >= 0)
                gearPunchTimer = 1f;
            previousGear = gear;
        }

        gearPunchTimer = Mathf.MoveTowards(gearPunchTimer, 0f, gearPunchSpeed * dt);

        gearNumberText.text = gear.ToString();
        float scale = 1f + (gearPunchScale - 1f) * gearPunchTimer;
        gearNumberText.rectTransform.localScale = new Vector3(scale, scale, 1f);

        float gear01 = (gear - 1f) / Mathf.Max(1f, gearCount - 1f);
        gearNumberText.color = Color.Lerp(textPrimary, accentHighColor, Mathf.Clamp01(gear01));

        if (gearBackdrop != null)
            gearBackdrop.color = Color.Lerp(backdropColor, accentColor, gearPunchTimer * 0.35f);
    }

    private int ComputeGearIndex(float speedKmh)
    {
        if (gearTopSpeedsKmh == null || gearTopSpeedsKmh.Length == 0)
            return 1;

        for (int i = 0; i < gearTopSpeedsKmh.Length; i++)
        {
            if (speedKmh <= gearTopSpeedsKmh[i])
                return i + 1;
        }

        return gearTopSpeedsKmh.Length;
    }

    private void UpdateLapDisplay(float dt)
    {
        if (lapNumberText == null) return;

        int currentLap = 1;
        int totalLaps = 3;
        bool finished = false;

        if (raceTracker != null)
        {
            currentLap = raceTracker.CurrentLap;
            totalLaps = raceTracker.TotalLaps;
            finished = raceTracker.RaceFinished;
        }

        if (currentLap != previousLap)
        {
            if (previousLap >= 0)
                lapPunchTimer = 1f;
            previousLap = currentLap;
        }

        lapPunchTimer = Mathf.MoveTowards(lapPunchTimer, 0f, lapPunchSpeed * dt);

        lapNumberText.text = finished ? "FIM" : $"{currentLap}/{totalLaps}";
        lapNumberText.color = cartoonYellow;

        float scale = 1f + (lapPunchScale - 1f) * lapPunchTimer;
        lapNumberText.rectTransform.localScale = new Vector3(scale, scale, 1f);

        if (lapBackdrop != null)
            lapBackdrop.color = Color.Lerp(backdropColor, accentColor, lapPunchTimer * 0.4f);
    }

    private void UpdatePowerDisplay(float dt)
    {
        if (powerNameText == null) return;

        KartPowerType currentPower = powerInventory != null ? powerInventory.CurrentPower : KartPowerType.None;

        if (currentPower != previousPower)
        {
            if (previousPower != (KartPowerType)(-1) && currentPower != KartPowerType.None)
                powerPunchTimer = 1f;
            previousPower = currentPower;
        }

        powerPunchTimer = Mathf.MoveTowards(powerPunchTimer, 0f, powerPunchSpeed * dt);

        powerNameText.text = powerInventory != null ? powerInventory.GetPowerDisplayName().ToUpperInvariant() : "NENHUM";

        Color swatchTarget = GetPowerColor(currentPower);

        if (powerSwatch != null)
        {
            powerSwatch.color = Color.Lerp(powerSwatch.color, swatchTarget, dt * 8f);
            float pulse = currentPower != KartPowerType.None ? 1f + Mathf.Sin(Time.time * 4f) * 0.08f : 1f;
            float punch = 1f + (powerPunchScale - 1f) * powerPunchTimer;
            powerSwatch.rectTransform.localScale = new Vector3(pulse * punch, pulse * punch, 1f);
        }

        if (powerBackdrop != null)
            powerBackdrop.color = Color.Lerp(backdropColor, swatchTarget, powerPunchTimer * 0.45f);

        powerNameText.color = currentPower == KartPowerType.None ? textSecondary : textPrimary;
    }

    private void UpdateCountdownAnim(float dt)
    {
        if (countdownText == null) return;

        string current = countdownText.text;

        if (current != previousCountdownText && countdownText.gameObject.activeSelf && !string.IsNullOrEmpty(current))
        {
            countdownPunchTimer = 1f;
            previousCountdownText = current;
        }

        countdownPunchTimer = Mathf.MoveTowards(countdownPunchTimer, 0f, countdownPunchSpeed * dt);

        float scale = 1f + (countdownPunchScale - 1f) * countdownPunchTimer;
        countdownText.rectTransform.localScale = new Vector3(scale, scale, 1f);

        bool isGoMessage = !string.IsNullOrEmpty(current) && (current.StartsWith("V") || current.StartsWith("G"));

        if (isGoMessage)
            countdownText.color = Color.Lerp(cartoonYellow, countdownGoColor, 0.5f + Mathf.Sin(Time.time * 14f) * 0.5f);
        else
            countdownText.color = Color.Lerp(cartoonYellow, accentHighColor, countdownPunchTimer);
    }

    private Color GetPowerColor(KartPowerType type)
    {
        switch (type)
        {
            case KartPowerType.SwapPosition: return new Color(0.30f, 0.78f, 1f, 1f);
            case KartPowerType.Rocket: return new Color(1f, 0.45f, 0.18f, 1f);
            case KartPowerType.Shield: return new Color(0.35f, 1f, 0.55f, 1f);
            case KartPowerType.None:
            default: return new Color(0.45f, 0.48f, 0.55f, 1f);
        }
    }

    // ---------------------------------------------------------------------
    // Construção procedural — chamada pelo botão "Build HUD"
    // ---------------------------------------------------------------------
    private void BuildSpeedometer()
    {
        speedoRoot = CreateUIElement("Speedometer", transform as RectTransform);
        AnchorBottomRight(speedoRoot, speedoSize, speedoAnchoredPosition);

        Image backdrop = CreateImage(speedoRoot, "Backdrop", GetRoundedSprite(), backdropColor);
        StretchFull(backdrop.rectTransform);
        backdrop.type = Image.Type.Sliced;

        Image topLine = CreateImage(speedoRoot, "TopLine", GetRoundedSprite(), outerRingColor);
        AnchorRect(topLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), new Vector2(-18f, 4f));
        topLine.type = Image.Type.Sliced;

        speedoFill = CreateImage(speedoRoot, "SpeedFill", GetRoundedSprite(), accentColor);
        AnchorRect(speedoFill.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(-22f, 12f));
        speedoFill.type = Image.Type.Filled;
        speedoFill.fillMethod = Image.FillMethod.Horizontal;
        speedoFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        speedoFill.fillAmount = 0f;

        Image fillTrack = CreateImage(speedoRoot, "SpeedTrack", GetRoundedSprite(), outerRingColor);
        AnchorRect(fillTrack.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 12f), new Vector2(-22f, 12f));
        fillTrack.type = Image.Type.Sliced;
        fillTrack.transform.SetSiblingIndex(speedoFill.transform.GetSiblingIndex());

        speedoFill.transform.SetAsLastSibling();

        speedNumberText = CreateText(speedoRoot, "SpeedNumber", "0", 58, FontStyles.Bold, TextAlignmentOptions.Right, textPrimary, withOutline: true);
        AnchorRect(speedNumberText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(-70f, 2f), new Vector2(-42f, -22f));

        TMP_Text unit = CreateText(speedoRoot, "SpeedUnit", "KM/H", 18, FontStyles.Bold, TextAlignmentOptions.Left, accentColor, withOutline: true);
        AnchorRect(unit.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, -6f), new Vector2(54f, 28f));
        unit.characterSpacing = 2f;

        TMP_Text label = CreateText(speedoRoot, "SpeedLabel", "VELOCIDADE", 14, FontStyles.Bold, TextAlignmentOptions.Left, textSecondary, withOutline: false);
        AnchorRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -20f), new Vector2(-34f, 22f));
        label.characterSpacing = 3f;
    }

    private void BuildGearIndicator()
    {
        gearRoot = CreateUIElement("GearIndicator", transform as RectTransform);
        AnchorBottomRight(gearRoot, gearSize, gearAnchoredPosition);

        gearBackdrop = CreateImage(gearRoot, "Backdrop", GetRoundedSprite(), backdropColor);
        StretchFull(gearBackdrop.rectTransform);
        gearBackdrop.type = Image.Type.Sliced;

        TMP_Text label = CreateText(gearRoot, "GearLabel", "MARCHA", 12, FontStyles.Bold, TextAlignmentOptions.Center, textSecondary, withOutline: false);
        AnchorRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -17f), new Vector2(-14f, 20f));
        label.characterSpacing = 2f;

        gearNumberText = CreateText(gearRoot, "GearNumber", "1", 58, FontStyles.Bold, TextAlignmentOptions.Center, textPrimary, withOutline: true);
        AnchorRect(gearNumberText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, -8f), new Vector2(-14f, -30f));
    }

    private void BuildLapCounter()
    {
        lapRoot = CreateUIElement("LapCounter", transform as RectTransform);
        AnchorTopCenter(lapRoot, lapSize, lapAnchoredPosition);

        lapBackdrop = CreateImage(lapRoot, "Backdrop", GetRoundedSprite(), backdropColor);
        StretchFull(lapBackdrop.rectTransform);
        lapBackdrop.type = Image.Type.Sliced;

        TMP_Text lapLabel = CreateText(lapRoot, "LapLabel", "VOLTA", 14, FontStyles.Bold, TextAlignmentOptions.Left, textSecondary, withOutline: false);
        AnchorRect(lapLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(18f, 0f), new Vector2(-18f, -14f));
        lapLabel.characterSpacing = 3f;

        lapNumberText = CreateText(lapRoot, "LapNumber", "1/3", 34, FontStyles.Bold, TextAlignmentOptions.Right, cartoonYellow, withOutline: true);
        AnchorRect(lapNumberText.rectTransform, new Vector2(0.45f, 0f), new Vector2(1f, 1f), new Vector2(-18f, -1f), new Vector2(-20f, -12f));
    }

    private void BuildPowerDisplay()
    {
        powerRoot = CreateUIElement("PowerDisplay", transform as RectTransform);
        AnchorTopRight(powerRoot, powerSize, powerAnchoredPosition);

        powerBackdrop = CreateImage(powerRoot, "Backdrop", GetRoundedSprite(), backdropColor);
        StretchFull(powerBackdrop.rectTransform);
        powerBackdrop.type = Image.Type.Sliced;

        powerSwatch = CreateImage(powerRoot, "Swatch", GetCircleSprite(), GetPowerColor(KartPowerType.None));
        AnchorRect(powerSwatch.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(36f, 36f));

        TMP_Text powerLabel = CreateText(powerRoot, "PowerLabel", "PODER", 12, FontStyles.Bold, TextAlignmentOptions.Left, textSecondary, withOutline: false);
        AnchorRect(powerLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(66f, -18f), new Vector2(-82f, 20f));
        powerLabel.characterSpacing = 3f;

        powerNameText = CreateText(powerRoot, "PowerName", "NENHUM", 25, FontStyles.Bold, TextAlignmentOptions.Left, textPrimary, withOutline: true);
        AnchorRect(powerNameText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(66f, -8f), new Vector2(-86f, -28f));
        powerNameText.enableAutoSizing = true;
        powerNameText.fontSizeMin = 16;
        powerNameText.fontSizeMax = 25;
    }

    private void BuildCountdown()
    {
        countdownRoot = CreateUIElement("Countdown", transform as RectTransform);
        AnchorCenter(countdownRoot, countdownSize, Vector2.zero);

        countdownText = CreateText(countdownRoot, "CountdownText", "", 210, FontStyles.Bold, TextAlignmentOptions.Center, cartoonYellow, withOutline: true);
        StretchFull(countdownText.rectTransform);
        countdownText.outlineWidth = 0.42f;
        countdownText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        countdownText.gameObject.SetActive(false);
        previousCountdownText = "";
    }

    private System.Collections.IEnumerator RunCountdown()
    {
        countdownDriven = true;

        if (kart != null)
            kart.SetControlEnabled(false);

        countdownText.gameObject.SetActive(true);

        countdownText.text = "3";
        yield return new WaitForSeconds(countdownStepDuration);

        countdownText.text = "2";
        yield return new WaitForSeconds(countdownStepDuration);

        countdownText.text = "1";
        yield return new WaitForSeconds(countdownStepDuration);

        countdownText.text = "VAI!";

        if (kart != null)
            kart.SetControlEnabled(true);

        yield return new WaitForSeconds(goMessageDuration);

        countdownText.gameObject.SetActive(false);
    }

    public TMP_Text CountdownText => countdownText;

    // ---------------------------------------------------------------------
    // Helpers UI
    // ---------------------------------------------------------------------
    private static RectTransform CreateUIElement(string name, RectTransform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, int size, FontStyles style, TextAlignmentOptions align, Color color, bool withOutline = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        if (withOutline)
        {
            tmp.ForceMeshUpdate();

            if (tmp.fontMaterial != null)
                tmp.fontMaterial = new Material(tmp.fontMaterial);

            tmp.outlineColor = outlineColor;
            tmp.outlineWidth = outlineWidth;
        }

        return tmp;
    }

    private static void AnchorBottomRight(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void AnchorTopRight(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void AnchorTopCenter(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void AnchorCenter(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void AnchorRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private const string GeneratedFolder = "Assets/_Projeto/UI/Generated";
    private const string CircleSpritePath = GeneratedFolder + "/HUD_Circle.png";
    private const string RoundedSpritePath = GeneratedFolder + "/HUD_Rounded.png";
    private const int RoundedSpriteBorder = 18;

    private static Sprite cachedCircle;
    private static Sprite cachedRounded;

    private static Sprite GetCircleSprite()
    {
        if (cachedCircle != null) return cachedCircle;

#if UNITY_EDITOR
        cachedCircle = EnsureGeneratedSprite(CircleSpritePath, () => BuildCircleTexture(256), 0);
        if (cachedCircle != null) return cachedCircle;
#endif

        // Runtime fallback (build sem asset salvo)
        cachedCircle = SpriteFromTexture(BuildCircleTexture(256), 0);
        return cachedCircle;
    }

    private static Sprite GetRoundedSprite()
    {
        if (cachedRounded != null) return cachedRounded;

#if UNITY_EDITOR
        cachedRounded = EnsureGeneratedSprite(RoundedSpritePath, () => BuildRoundedTexture(64, RoundedSpriteBorder), RoundedSpriteBorder);
        if (cachedRounded != null) return cachedRounded;
#endif

        cachedRounded = SpriteFromTexture(BuildRoundedTexture(64, RoundedSpriteBorder), RoundedSpriteBorder);
        return cachedRounded;
    }

    private static Sprite SpriteFromTexture(Texture2D tex, int border)
    {
        Vector4 borderVec = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, borderVec);
    }

#if UNITY_EDITOR
    private static Sprite EnsureGeneratedSprite(string assetPath, System.Func<Texture2D> textureBuilder, int border)
    {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null) return existing;

        EnsureFolder(GeneratedFolder);

        Texture2D tex = textureBuilder();
        File.WriteAllBytes(assetPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(assetPath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;

            if (border > 0)
            {
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteBorder = new Vector4(border, border, border, border);
                importer.SetTextureSettings(settings);
            }

            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath).Replace('\\', '/');
        string leaf = Path.GetFileName(folderPath);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
#endif

    private static Texture2D BuildCircleTexture(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float center = resolution * 0.5f;
        float radius = center - 1f;

        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius - dist);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }

    private static Texture2D BuildRoundedTexture(int resolution, int cornerRadius)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float dx = Mathf.Max(0f, cornerRadius - x, x - (resolution - 1 - cornerRadius));
                float dy = Mathf.Max(0f, cornerRadius - y, y - (resolution - 1 - cornerRadius));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(cornerRadius - dist);
                pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }
}
