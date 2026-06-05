#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class HUDPrefabGenerator
{
    private const string Dir = "Assets/_Projeto/Prefabs/UI";
    private const string ResourcesDir = "Assets/_Projeto/Resources";
    private const string ScreenWindPath = "Assets/_Projeto/Materials/Fullscreen effects/Prefabs/Screen wind.prefab";

    private static readonly Color Backdrop = new Color(0.02f, 0.06f, 0.085f, 0.88f);
    private static readonly Color BackdropSoft = new Color(0.04f, 0.10f, 0.13f, 0.82f);
    private static readonly Color PanelDark = new Color(0.015f, 0.025f, 0.035f, 0.94f);
    private static readonly Color Stroke = new Color(0.08f, 0.18f, 0.23f, 0.96f);
    private static readonly Color Blue = new Color(0.12f, 0.68f, 1f, 1f);
    private static readonly Color Green = new Color(0.32f, 1f, 0.18f, 1f);
    private static readonly Color Yellow = new Color(1f, 0.82f, 0.10f, 1f);
    private static readonly Color Orange = new Color(1f, 0.46f, 0.10f, 1f);
    private static readonly Color Red = new Color(1f, 0.12f, 0.08f, 1f);
    private static readonly Color Purple = new Color(0.68f, 0.24f, 1f, 1f);
    private static readonly Color TextPrimary = new Color(1f, 1f, 1f, 0.98f);
    private static readonly Color TextSecondary = new Color(0.78f, 0.88f, 0.94f, 0.92f);

    private static Sprite RoundedSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    private static Sprite CircleSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    [MenuItem("PartyRacers/HUD/Gerar Prefabs do HUD")]
    public static void GenerateAll()
    {
        EnsureDir();

        GameObject position = SaveWidget(BuildRacePosition(), "RacePositionWidget");
        GameObject leaderboard = SaveWidget(BuildLeaderboard(), "LeaderboardWidget");
        GameObject minimap = SaveWidget(BuildMinimap(), "MinimapWidget");
        GameObject lap = SaveWidget(BuildLapCounter(), "LapCounterWidget");
        GameObject power = SaveWidget(BuildPowerDisplay(), "PowerDisplayWidget");
        GameObject speedo = SaveWidget(BuildSpeedometer(), "SpeedometerWidget");
        GameObject gear = SaveWidget(BuildGear(), "GearWidget");
        GameObject nitro = SaveWidget(BuildNitroMeter(), "NitroMeterWidget");
        GameObject banner = SaveWidget(BuildEventBanner(), "EventBannerWidget");
        GameObject countdown = SaveWidget(BuildCountdown(), "CountdownWidget");

        SaveWidget(BuildArcadeElements(), "HUDArcadeElementsWidget");
        BuildAndSaveHudRoot(position, leaderboard, minimap, lap, power, speedo, gear, nitro, banner, countdown);

        AssetDatabase.SaveAssets();
        Debug.Log("[HUDPrefabGenerator] HUD arcade gerado em " + Dir + " e " + ResourcesDir + "/HUDRoot.prefab");
    }

    private static GameObject BuildRacePosition()
    {
        RectTransform root = NewUI("RacePositionWidget");
        SetSize(root, new Vector2(142f, 126f));

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.02f, 0.10f, 0.16f, 0.94f), sliced: true);
        AddOutline(bg.gameObject, Blue, new Vector2(4f, -4f));

        Image accent = AddImage(root, "Accent", RoundedSprite, Blue, sliced: true);
        Anchor(accent.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        accent.color = new Color(0.08f, 0.42f, 0.72f, 0.92f);

        Image shine = AddImage(root, "TopShine", RoundedSprite, new Color(1f, 1f, 1f, 0.12f), sliced: true);
        Anchor(shine.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -38f), new Vector2(-14f, -12f));

        TMP_Text label = AddText(root, "Label", "POS", 18f, TextAlignmentOptions.Center, TextSecondary);
        Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(100f, 28f));
        label.characterSpacing = 3f;

        TMP_Text text = AddText(root, "Position", "2<size=62%>\u00ba</size>", 70f, TextAlignmentOptions.Center, TextPrimary);
        Place(text.rectTransform, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(130f, 86f));

        var ui = root.gameObject.AddComponent<RacePositionUI>();
        Set(ui, "positionText", text);
        Set(ui, "background", bg);
        Set(ui, "accent", accent);
        return root.gameObject;
    }

    private static GameObject BuildLeaderboard()
    {
        RectTransform root = NewUI("LeaderboardWidget");
        SetSize(root, new Vector2(258f, 294f));

        Image bg = AddImageOn(root, RoundedSprite, Backdrop, sliced: true);
        AddOutline(bg.gameObject, Stroke, new Vector2(2f, -2f));

        TMP_Text title = AddText(root, "Title", "PILOTOS", 18f, TextAlignmentOptions.Left, TextSecondary);
        Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -34f), new Vector2(-18f, -8f));
        title.characterSpacing = 3f;

        RectTransform rows = NewUI("Rows", root);
        Anchor(rows, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -44f));
        VerticalLayoutGroup layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 5f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        RectTransform template = NewUI("RowTemplate", rows);
        LayoutElement templateLayout = template.gameObject.AddComponent<LayoutElement>();
        templateLayout.preferredHeight = 27f;
        templateLayout.minHeight = 27f;

        Image rowBg = AddImage(template, "Background", RoundedSprite, PanelDark, sliced: true);
        Stretch(rowBg.rectTransform);

        Image accent = AddImage(template, "Accent", RoundedSprite, Blue, sliced: true);
        Anchor(accent.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(5f, 0f));

        TMP_Text pos = AddText(template, "Position", "1", 17f, TextAlignmentOptions.Center, TextPrimary);
        Anchor(pos.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(36f, 0f));

        Image avatar = AddImage(template, "Avatar", CircleSprite, new Color(0.80f, 0.95f, 1f, 0.90f), sliced: false);
        Place(avatar.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(52f, 0f), new Vector2(18f, 18f));

        TMP_Text name = AddText(template, "Name", "ALEX", 16f, TextAlignmentOptions.Left, TextPrimary);
        Anchor(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(70f, 0f), new Vector2(-12f, 0f));
        name.enableAutoSizing = true;
        name.fontSizeMin = 11f;
        name.fontSizeMax = 16f;

        template.gameObject.SetActive(false);

        var ui = root.gameObject.AddComponent<LeaderboardUI>();
        Set(ui, "rowsContainer", rows);
        Set(ui, "rowTemplate", template);
        return root.gameObject;
    }

    private static GameObject BuildMinimap()
    {
        RectTransform root = NewUI("MinimapWidget");
        SetSize(root, new Vector2(250f, 190f));

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.02f, 0.045f, 0.055f, 0.84f), sliced: true);
        AddOutline(bg.gameObject, new Color(1f, 1f, 1f, 0.55f), new Vector2(2f, -2f));

        RectTransform map = NewUI("MapGraphic", root);
        Anchor(map, Vector2.zero, Vector2.one, new Vector2(12f, 10f), new Vector2(-12f, -10f));
        map.gameObject.AddComponent<CanvasRenderer>();
        MinimapUI minimap = map.gameObject.AddComponent<MinimapUI>();

        TMP_Text flag = AddText(root, "Flag", "\u2691", 24f, TextAlignmentOptions.Center, TextPrimary);
        Place(flag.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-22f, 18f), new Vector2(32f, 28f));
        return root.gameObject;
    }

    private static GameObject BuildLapCounter()
    {
        RectTransform root = NewUI("LapCounterWidget");
        SetSize(root, new Vector2(382f, 108f));

        Image bg = AddImageOn(root, RoundedSprite, Backdrop, sliced: true);
        AddOutline(bg.gameObject, new Color(0.09f, 0.40f, 0.55f, 1f), new Vector2(3f, -3f));

        RectTransform flag = NewUI("CheckeredFlag", root);
        Place(flag, new Vector2(0f, 0.58f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(52f, 54f));
        AddFlagSquares(flag, 4, 3);

        TMP_Text label = AddText(root, "Label", "VOLTA", 28f, TextAlignmentOptions.Left, TextPrimary);
        Anchor(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.55f, 1f), new Vector2(84f, -8f), new Vector2(-8f, -12f));
        label.characterSpacing = 2f;

        TMP_Text lapText = AddText(root, "LapText", "1/3", 42f, TextAlignmentOptions.Right, TextPrimary);
        Anchor(lapText.rectTransform, new Vector2(0.52f, 0.5f), Vector2.one, new Vector2(0f, -6f), new Vector2(-24f, -10f));

        RectTransform container = NewUI("Circles", root);
        Anchor(container, new Vector2(0f, 0f), Vector2.one, new Vector2(92f, 18f), new Vector2(-84f, -70f));
        HorizontalLayoutGroup hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        Image template = AddImage(container, "CircleTemplate", CircleSprite, new Color(1f, 1f, 1f, 0.25f), sliced: false);
        template.rectTransform.sizeDelta = new Vector2(28f, 28f);
        LayoutElement le = template.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 28f;
        le.preferredHeight = 28f;
        template.gameObject.SetActive(false);

        var ui = root.gameObject.AddComponent<LapCounterUI>();
        Set(ui, "lapText", lapText);
        Set(ui, "backdrop", bg);
        Set(ui, "circlesContainer", container);
        Set(ui, "circleTemplate", template);
        return root.gameObject;
    }

    private static GameObject BuildPowerDisplay()
    {
        RectTransform root = NewUI("PowerDisplayWidget");
        SetSize(root, new Vector2(360f, 156f));

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.11f, 0.05f, 0.025f, 0.94f), sliced: true);
        AddOutline(bg.gameObject, Orange, new Vector2(4f, -4f));

        Image accent = AddImage(root, "PanelAccent", RoundedSprite, Orange, sliced: true);
        Anchor(accent.rectTransform, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
        accent.color = new Color(1f, 0.28f, 0.08f, 0.78f);

        Image iconPlate = AddImage(root, "IconPlate", RoundedSprite, new Color(0.06f, 0.08f, 0.10f, 0.72f), sliced: true);
        Place(iconPlate.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(22f, -18f), new Vector2(104f, 104f));

        Image swatch = AddImage(root, "Swatch", CircleSprite, Orange, sliced: false);
        Place(swatch.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(92f, -86f), new Vector2(42f, 42f));

        Image icon = AddImage(iconPlate.rectTransform, "Icon", RoundedSprite, new Color(1f, 1f, 1f, 0.10f), sliced: true);
        Stretch(icon.rectTransform);
        icon.enabled = false;

        TMP_Text glyph = AddText(iconPlate.rectTransform, "Glyph", "\u25b2", 54f, TextAlignmentOptions.Center, TextPrimary);
        Stretch(glyph.rectTransform);

        TMP_Text name = AddText(root, "Name", "FOGUETE", 28f, TextAlignmentOptions.Left, TextPrimary);
        Anchor(name.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(144f, -8f), new Vector2(-64f, -34f));
        name.enableAutoSizing = true;
        name.fontSizeMin = 16f;
        name.fontSizeMax = 28f;

        TMP_Text count = AddText(root, "ItemCount", "1", 22f, TextAlignmentOptions.Center, new Color(0.02f, 0.04f, 0.06f, 1f));
        Place(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-44f, 4f), new Vector2(38f, 38f));
        Image countBg = AddImage(root, "CountCircle", CircleSprite, Yellow, sliced: false);
        countBg.transform.SetSiblingIndex(count.transform.GetSiblingIndex());
        Place(countBg.rectTransform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-44f, 4f), new Vector2(38f, 38f));

        RectTransform btn = NewUI("UseButton", root);
        Place(btn, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(220f, 42f));
        Image btnImg = btn.gameObject.AddComponent<Image>();
        btnImg.sprite = RoundedSprite;
        btnImg.type = Image.Type.Sliced;
        btnImg.color = new Color(0.025f, 0.12f, 0.17f, 0.96f);
        AddOutline(btn.gameObject, Blue, new Vector2(2f, -2f));
        Button button = btn.gameObject.AddComponent<Button>();
        button.targetGraphic = btnImg;

        Image cooldown = AddImage(btn, "CooldownFill", RoundedSprite, new Color(1f, 1f, 1f, 0.16f), sliced: true);
        Stretch(cooldown.rectTransform);
        cooldown.type = Image.Type.Filled;
        cooldown.fillMethod = Image.FillMethod.Horizontal;
        cooldown.fillOrigin = (int)Image.OriginHorizontal.Left;
        cooldown.fillAmount = 0f;

        TMP_Text btnText = AddText(btn, "Text", "USAR", 25f, TextAlignmentOptions.Center, TextPrimary);
        Anchor(btnText.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-46f, 0f));

        TMP_Text hint = AddText(btn, "ControlHint", "RT", 15f, TextAlignmentOptions.Center, TextPrimary);
        Place(hint.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(38f, 26f));
        btn.gameObject.SetActive(false);

        var ui = root.gameObject.AddComponent<PowerDisplayUI>();
        Set(ui, "background", bg);
        Set(ui, "swatch", swatch);
        Set(ui, "panelAccent", accent);
        Set(ui, "icon", icon);
        Set(ui, "iconGlyph", glyph);
        Set(ui, "powerName", name);
        Set(ui, "itemCount", count);
        Set(ui, "controlHint", hint);
        Set(ui, "useButton", button);
        Set(ui, "useCooldownFill", cooldown);
        return root.gameObject;
    }

    private static GameObject BuildSpeedometer()
    {
        RectTransform root = NewUI("SpeedometerWidget");
        SetSize(root, new Vector2(332f, 118f));

        Image bg = AddImageOn(root, RoundedSprite, Backdrop, sliced: true);
        AddOutline(bg.gameObject, new Color(0.08f, 0.18f, 0.24f, 1f), new Vector2(3f, -3f));

        TMP_Text number = AddText(root, "Number", "128", 58f, TextAlignmentOptions.Right, TextPrimary);
        Anchor(number.rectTransform, new Vector2(0f, 0.45f), Vector2.one, new Vector2(16f, -4f), new Vector2(-78f, -10f));
        number.characterSpacing = 2f;

        TMP_Text unit = AddText(root, "Unit", "KM/H", 20f, TextAlignmentOptions.Right, TextSecondary);
        Anchor(unit.rectTransform, new Vector2(1f, 0.55f), Vector2.one, new Vector2(-82f, -2f), new Vector2(-16f, -24f));

        Image track = AddImage(root, "Track", RoundedSprite, PanelDark, sliced: true);
        Anchor(track.rectTransform, Vector2.zero, Vector2.one, new Vector2(18f, 18f), new Vector2(-18f, -72f));

        Image fill = AddImage(track.rectTransform, "ContinuousFill", RoundedSprite, Blue, sliced: true);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.color = new Color(Blue.r, Blue.g, Blue.b, 0.30f);

        RectTransform segments = NewUI("Segments", root);
        Anchor(segments, Vector2.zero, Vector2.one, new Vector2(20f, 20f), new Vector2(-20f, -74f));
        HorizontalLayoutGroup hlg = segments.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        List<Image> fills = new List<Image>();
        Color[] colors = { Blue, Blue, Blue, Green, Yellow, Yellow, Orange, Orange, Red, Red };
        for (int i = 0; i < 10; i++)
        {
            RectTransform segment = NewUI($"Segment_{i}", segments);
            Image segmentBg = segment.gameObject.AddComponent<Image>();
            segmentBg.sprite = RoundedSprite;
            segmentBg.type = Image.Type.Sliced;
            segmentBg.color = new Color(0.04f, 0.08f, 0.10f, 0.95f);
            segmentBg.raycastTarget = false;

            Image segmentFill = AddImage(segment, "Fill", RoundedSprite, colors[i], sliced: true);
            Stretch(segmentFill.rectTransform);
            segmentFill.type = Image.Type.Filled;
            segmentFill.fillMethod = Image.FillMethod.Horizontal;
            segmentFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            segmentFill.fillAmount = 0f;
            fills.Add(segmentFill);
        }

        var ui = root.gameObject.AddComponent<SpeedometerUI>();
        Set(ui, "fillBar", fill);
        Set(ui, "segmentFills", fills.ToArray());
        Set(ui, "speedNumber", number);
        Set(ui, "unitLabel", unit);
        Set(ui, "greenColor", Green);
        Set(ui, "yellowColor", Yellow);
        Set(ui, "highColor", Orange);
        Set(ui, "maxColor", Red);
        Set(ui, "maxFill", 1f);
        return root.gameObject;
    }

    private static GameObject BuildGear()
    {
        RectTransform root = NewUI("GearWidget");
        SetSize(root, new Vector2(126f, 126f));

        Image bg = AddImageOn(root, CircleSprite, new Color(0.02f, 0.055f, 0.075f, 0.94f), sliced: false);
        AddOutline(bg.gameObject, Blue, new Vector2(3f, -3f));

        Image inner = AddImage(root, "InnerRing", CircleSprite, new Color(0.04f, 0.12f, 0.18f, 0.92f), sliced: false);
        Anchor(inner.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -12f));

        TMP_Text number = AddText(root, "Number", "4", 68f, TextAlignmentOptions.Center, TextPrimary);
        Anchor(number.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -22f));

        TMP_Text glyph = AddText(root, "GearGlyph", "\u2699", 20f, TextAlignmentOptions.Center, Blue);
        Place(glyph.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(44f, 24f));

        var ui = root.gameObject.AddComponent<GearUI>();
        Set(ui, "gearNumber", number);
        Set(ui, "backdrop", bg);
        Set(ui, "gearIcon", inner);
        Set(ui, "gearGlyph", glyph);
        return root.gameObject;
    }

    private static GameObject BuildNitroMeter()
    {
        RectTransform root = NewUI("NitroMeterWidget");
        SetSize(root, new Vector2(332f, 44f));

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.02f, 0.045f, 0.06f, 0.88f), sliced: true);
        AddOutline(bg.gameObject, Blue, new Vector2(2f, -2f));

        Image track = AddImage(root, "Track", RoundedSprite, PanelDark, sliced: true);
        Anchor(track.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 11f), new Vector2(-52f, -11f));

        Image fill = AddImage(track.rectTransform, "Fill", RoundedSprite, Blue, sliced: true);
        Stretch(fill.rectTransform);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        fill.fillAmount = 0f;

        TMP_Text icon = AddText(root, "Icon", "\u26a1", 32f, TextAlignmentOptions.Center, new Color(0.78f, 0.94f, 1f, 1f));
        Place(icon.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(42f, 42f));

        var ui = root.gameObject.AddComponent<NitroMeterUI>();
        Set(ui, "fill", fill);
        Set(ui, "iconLabel", icon);
        return root.gameObject;
    }

    private static GameObject BuildEventBanner()
    {
        RectTransform root = NewUI("EventBannerWidget");
        SetSize(root, new Vector2(314f, 76f));

        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.03f, 0.08f, 0.11f, 0.94f), sliced: true);
        AddOutline(bg.gameObject, Blue, new Vector2(2f, -2f));

        Image accent = AddImage(root, "Accent", RoundedSprite, Blue, sliced: true);
        Anchor(accent.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(12f, 0f));

        TMP_Text icon = AddText(root, "Icon", "\u26a1", 36f, TextAlignmentOptions.Center, Blue);
        Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(54f, 54f));

        TMP_Text message = AddText(root, "Message", "NITRO!", 24f, TextAlignmentOptions.Left, TextPrimary);
        Anchor(message.rectTransform, Vector2.zero, Vector2.one, new Vector2(88f, 0f), new Vector2(-20f, 0f));
        message.enableAutoSizing = true;
        message.fontSizeMin = 15f;
        message.fontSizeMax = 24f;

        RectTransform spikeA = NewUI("SpikeA", root);
        Image spikeImgA = spikeA.gameObject.AddComponent<Image>();
        spikeImgA.color = new Color(1f, 1f, 1f, 0.10f);
        spikeImgA.raycastTarget = false;
        Place(spikeA, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-20f, 22f), new Vector2(40f, 8f));
        spikeA.localRotation = Quaternion.Euler(0f, 0f, -16f);

        var ui = root.gameObject.AddComponent<EventBannerUI>();
        Set(ui, "panel", root);
        Set(ui, "canvasGroup", group);
        Set(ui, "background", bg);
        Set(ui, "accent", accent);
        Set(ui, "iconLabel", icon);
        Set(ui, "messageLabel", message);
        return root.gameObject;
    }

    private static GameObject BuildCountdown()
    {
        RectTransform root = NewUI("CountdownWidget");
        SetSize(root, new Vector2(286f, 492f));

        CanvasGroup rootGroup = root.gameObject.AddComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.blocksRaycasts = false;
        rootGroup.interactable = false;

        RectTransform stack = NewUI("CountdownSideStack", root);
        Stretch(stack);
        Image bg = stack.gameObject.AddComponent<Image>();
        bg.sprite = RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.02f, 0.07f, 0.10f, 0.90f);
        bg.raycastTarget = false;
        AddOutline(stack.gameObject, new Color(0.10f, 0.24f, 0.30f, 1f), new Vector2(2f, -2f));

        CanvasGroup stackGroup = stack.gameObject.AddComponent<CanvasGroup>();
        stackGroup.blocksRaycasts = false;
        stackGroup.interactable = false;

        TMP_Text title = AddText(stack, "Title", "CONTAGEM REGRESSIVA", 22f, TextAlignmentOptions.Center, TextPrimary);
        Anchor(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(12f, -42f), new Vector2(-12f, -10f));
        title.enableAutoSizing = true;
        title.fontSizeMin = 14f;
        title.fontSizeMax = 22f;

        TMP_Text[] labels = new TMP_Text[4];
        CanvasGroup[] groups = new CanvasGroup[4];
        Image[] panels = new Image[4];
        RectTransform[] rects = new RectTransform[4];
        Color[] colors = { Red, Orange, Yellow, Green };
        string[] texts = { "3", "2", "1", "VAI!" };

        for (int i = 0; i < 4; i++)
        {
            RectTransform row = NewUI($"Step_{i}", stack);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.offsetMin = new Vector2(22f, -126f - i * 92f);
            row.offsetMax = new Vector2(-22f, -58f - i * 92f);

            Image panel = row.gameObject.AddComponent<Image>();
            panel.sprite = RoundedSprite;
            panel.type = Image.Type.Sliced;
            panel.color = Color.Lerp(new Color(0.04f, 0.07f, 0.09f, 0.96f), colors[i], 0.72f);
            panel.raycastTarget = false;
            AddOutline(row.gameObject, new Color(0f, 0f, 0f, 0.65f), new Vector2(2f, -2f));

            CanvasGroup rowGroup = row.gameObject.AddComponent<CanvasGroup>();
            rowGroup.alpha = 0.24f;
            rowGroup.blocksRaycasts = false;
            rowGroup.interactable = false;

            TMP_Text label = AddText(row, "Text", texts[i], i == 3 ? 48f : 70f, TextAlignmentOptions.Center, TextPrimary);
            Stretch(label.rectTransform);

            labels[i] = label;
            groups[i] = rowGroup;
            panels[i] = panel;
            rects[i] = row;
        }

        var ui = root.gameObject.AddComponent<CountdownUI>();
        Set(ui, "canvasGroup", rootGroup);
        Set(ui, "stackRoot", stack);
        Set(ui, "stackGroup", stackGroup);
        Set(ui, "messageLabel", title);
        Set(ui, "stepLabels", labels);
        Set(ui, "stepGroups", groups);
        Set(ui, "stepBackdrops", panels);
        Set(ui, "stepRects", rects);
        return root.gameObject;
    }

    private static GameObject BuildArcadeElements()
    {
        RectTransform root = NewUI("HUDArcadeElementsWidget");
        SetSize(root, new Vector2(760f, 520f));

        Image bg = AddImageOn(root, RoundedSprite, new Color(0.02f, 0.06f, 0.08f, 0.92f), sliced: true);
        AddOutline(bg.gameObject, Stroke, new Vector2(2f, -2f));

        AddSectionTitle(root, "VARIACOES DE POSICAO", new Vector2(22f, -24f));
        Color[] badgeColors = { Yellow, Blue, Orange, Purple, new Color(0.58f, 0.68f, 0.74f, 1f) };
        for (int i = 0; i < 5; i++)
            AddSampleBadge(root, $"{i + 1}<size=58%>\u00ba</size>", badgeColors[i], new Vector2(42f + i * 112f, -86f));

        AddSectionTitle(root, "ICONES - POWER UPS", new Vector2(22f, -178f));
        string[] icons = { "\u25b2", "\u25c6", "\u25cf", "\u2620", "U", "B", "\u25ef", "\u26a1" };
        Color[] iconColors = { Orange, Blue, Green, Purple, new Color(1f, 0.18f, 0.38f, 1f), Yellow, TextSecondary, Yellow };
        for (int i = 0; i < icons.Length; i++)
        {
            int col = i % 4;
            int row = i / 4;
            AddSampleIcon(root, icons[i], iconColors[i], new Vector2(42f + col * 86f, -238f - row * 82f));
        }

        AddSectionTitle(root, "ELEMENTOS EXTRAS", new Vector2(430f, -24f));
        AddText(root, "ArrowSamples", ">>>   \u2691   *   \u26a1", 42f, TextAlignmentOptions.Left, Yellow)
            .rectTransform.anchoredPosition = new Vector2(430f, -96f);
        RectTransform dots = NewUI("DotPattern", root);
        Place(dots, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, -164f), new Vector2(220f, 80f));
        AddDotPattern(dots);

        AddSectionTitle(root, "BARRAS", new Vector2(430f, -284f));
        AddSampleBar(root, new Vector2(430f, -346f), new[] { Blue, Blue, Green, Yellow, Orange, Red });
        AddSampleBar(root, new Vector2(430f, -402f), new[] { Blue, Blue, Blue, Blue, Blue, Blue });

        return root.gameObject;
    }

    private static void BuildAndSaveHudRoot(
        GameObject positionPrefab,
        GameObject leaderboardPrefab,
        GameObject minimapPrefab,
        GameObject lapPrefab,
        GameObject powerPrefab,
        GameObject speedoPrefab,
        GameObject gearPrefab,
        GameObject nitroPrefab,
        GameObject bannerPrefab,
        GameObject countdownPrefab)
    {
        GameObject rootGo = new GameObject("HUDRoot",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HUDRootUI));

        Canvas canvas = rootGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = rootGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RacePositionUI position = NestWidget<RacePositionUI>(positionPrefab, rootGo.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -22f), new Vector2(142f, 126f));
        LeaderboardUI leaderboard = NestWidget<LeaderboardUI>(leaderboardPrefab, rootGo.transform,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -164f), new Vector2(258f, 294f));
        MinimapUI minimap = NestWidget<MinimapUI>(minimapPrefab, rootGo.transform,
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 36f), new Vector2(250f, 190f));
        LapCounterUI lap = NestWidget<LapCounterUI>(lapPrefab, rootGo.transform,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(382f, 108f));
        PowerDisplayUI power = NestWidget<PowerDisplayUI>(powerPrefab, rootGo.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(360f, 156f));
        GearUI gear = NestWidget<GearUI>(gearPrefab, rootGo.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-398f, 62f), new Vector2(126f, 126f));
        SpeedometerUI speedo = NestWidget<SpeedometerUI>(speedoPrefab, rootGo.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 76f), new Vector2(332f, 118f));
        NitroMeterUI nitro = NestWidget<NitroMeterUI>(nitroPrefab, rootGo.transform,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-36f, 28f), new Vector2(332f, 44f));
        EventBannerUI banner = NestWidget<EventBannerUI>(bannerPrefab, rootGo.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-38f, -138f), new Vector2(314f, 76f));
        CountdownUI countdown = NestWidget<CountdownUI>(countdownPrefab, rootGo.transform,
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(286f, 492f));

        HUDRootUI hud = rootGo.GetComponent<HUDRootUI>();
        Set(hud, "racePosition", position);
        Set(hud, "leaderboard", leaderboard);
        Set(hud, "minimap", minimap);
        Set(hud, "lapCounter", lap);
        Set(hud, "powerDisplay", power);
        Set(hud, "speedometer", speedo);
        Set(hud, "gear", gear);
        Set(hud, "nitroMeter", nitro);
        Set(hud, "eventBanner", banner);
        Set(hud, "screenWindPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ScreenWindPath));

        if (countdown == null)
            Debug.LogWarning("[HUDPrefabGenerator] CountdownWidget sem CountdownUI.");

        EnsureAssetFolder(ResourcesDir);
        PrefabUtility.SaveAsPrefabAsset(rootGo, $"{ResourcesDir}/HUDRoot.prefab");
        Object.DestroyImmediate(rootGo);
    }

    private static T NestWidget<T>(GameObject widgetPrefab, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        where T : Component
    {
        GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(widgetPrefab, parent);
        RectTransform rt = inst.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return inst.GetComponentInChildren<T>(true);
    }

    private static GameObject SaveWidget(GameObject go, string fileName)
    {
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, $"{Dir}/{fileName}.prefab");
        Object.DestroyImmediate(go);
        return asset;
    }

    private static void EnsureDir()
    {
        EnsureAssetFolder("Assets/_Projeto");
        EnsureAssetFolder(Dir);
        EnsureAssetFolder(ResourcesDir);
    }

    private static void EnsureAssetFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string name = Path.GetFileName(folder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            return;

        EnsureAssetFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static RectTransform NewUI(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
            go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void SetSize(RectTransform rt, Vector2 size) => rt.sizeDelta = size;

    private static Image AddImageOn(RectTransform root, Sprite sprite, Color color, bool sliced)
    {
        Image image = root.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        if (sliced && sprite != null)
            image.type = Image.Type.Sliced;
        return image;
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Color color, bool sliced)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        if (sliced && sprite != null)
            img.type = Image.Type.Sliced;
        return img;
    }

    private static TMP_Text AddText(Transform parent, string name, string content, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        tmp.alignment = align;
        tmp.color = color;
        tmp.outlineColor = new Color(0.015f, 0.018f, 0.026f, 1f);
        tmp.outlineWidth = 0.16f;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void AddOutline(GameObject target, Color color, Vector2 distance)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    private static void AddFlagSquares(RectTransform parent, int cols, int rows)
    {
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                bool dark = (x + y) % 2 == 0;
                Image square = AddImage(parent, $"Square_{x}_{y}", null,
                    dark ? new Color(0.02f, 0.025f, 0.03f, 1f) : Color.white,
                    sliced: false);

                RectTransform rt = square.rectTransform;
                rt.anchorMin = new Vector2(x / (float)cols, y / (float)rows);
                rt.anchorMax = new Vector2((x + 1f) / cols, (y + 1f) / rows);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
        }
    }

    private static void AddSectionTitle(RectTransform root, string text, Vector2 topLeft)
    {
        TMP_Text title = AddText(root, "Title_" + text, text, 17f, TextAlignmentOptions.Left, TextSecondary);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(280f, 28f));
        title.characterSpacing = 2f;
    }

    private static void AddSampleBadge(RectTransform root, string text, Color color, Vector2 topLeft)
    {
        RectTransform badge = NewUI("Badge_" + text, root);
        Place(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(86f, 56f));
        Image bg = badge.gameObject.AddComponent<Image>();
        bg.sprite = RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(new Color(0.02f, 0.05f, 0.07f, 0.95f), color, 0.65f);
        bg.raycastTarget = false;
        AddOutline(badge.gameObject, color, new Vector2(3f, -3f));

        TMP_Text label = AddText(badge, "Text", text, 36f, TextAlignmentOptions.Center, TextPrimary);
        Stretch(label.rectTransform);
    }

    private static void AddSampleIcon(RectTransform root, string icon, Color color, Vector2 topLeft)
    {
        RectTransform holder = NewUI("Icon_" + icon, root);
        Place(holder, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(56f, 56f));
        Image bg = holder.gameObject.AddComponent<Image>();
        bg.sprite = RoundedSprite;
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(new Color(0.02f, 0.05f, 0.07f, 0.95f), color, 0.48f);
        bg.raycastTarget = false;
        AddOutline(holder.gameObject, color, new Vector2(2f, -2f));

        TMP_Text label = AddText(holder, "Glyph", icon, 32f, TextAlignmentOptions.Center, TextPrimary);
        Stretch(label.rectTransform);
    }

    private static void AddDotPattern(RectTransform parent)
    {
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                Image dot = AddImage(parent, $"Dot_{x}_{y}", CircleSprite, new Color(1f, 0.82f, 0.10f, 0.85f), sliced: false);
                Place(dot.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(10f + x * 18f, -10f - y * 16f), new Vector2(5f, 5f));
            }
        }
    }

    private static void AddSampleBar(RectTransform root, Vector2 topLeft, Color[] colors)
    {
        RectTransform bar = NewUI("SampleBar", root);
        Place(bar, new Vector2(0f, 1f), new Vector2(0f, 1f), topLeft, new Vector2(236f, 28f));
        HorizontalLayoutGroup hlg = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        for (int i = 0; i < colors.Length; i++)
        {
            RectTransform seg = NewUI("Segment", bar);
            Image img = seg.gameObject.AddComponent<Image>();
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.color = colors[i];
            img.raycastTarget = false;
        }
    }

    private static void Set(object target, string field, object value)
    {
        if (target == null)
            return;

        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null)
            f.SetValue(target, value);
        else
            Debug.LogWarning($"[HUDPrefabGenerator] Campo '{field}' nao encontrado em {target.GetType().Name}.");
    }
}
#endif
