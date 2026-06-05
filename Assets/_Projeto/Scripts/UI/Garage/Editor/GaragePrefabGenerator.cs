#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Gera os prefabs componentizados do lobby/garagem (item de jogador, botão de categoria) em
// Resources, com refs preenchidas. O GarageController os instancia em runtime (Resources.Load).
// Rode via menu "PartyRacers/HUD/Gerar Prefabs da Garagem".
public static class GaragePrefabGenerator
{
    private const string ResourcesDir = "Assets/_Projeto/Resources";

    private static readonly Color Panel = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    private static readonly Color Button = new Color(0.16f, 0.19f, 0.26f, 0.95f);
    private static readonly Color Accent = new Color(1f, 0.45f, 0.1f, 1f);
    private static readonly Color TextCol = new Color(1f, 1f, 1f, 0.97f);
    private static readonly Color TextDim = new Color(0.78f, 0.82f, 0.9f, 0.85f);

    private static Sprite Rounded => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    private static Sprite Circle => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

    [MenuItem("PartyRacers/HUD/Gerar Prefabs da Garagem")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(ResourcesDir))
            Directory.CreateDirectory(ResourcesDir);

        Save(BuildPlayerItem(), "LobbyPlayerItem");
        Save(BuildCategoryButton(), "CategoryButton");
        Save(BuildOptionsPanel(), "GarageOptionsPanel");
        Save(BuildLobbyPanel(), "GarageLobbyPanel");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[GaragePrefabGenerator] Prefabs da garagem gerados em " + ResourcesDir);
    }

    private static GameObject BuildPlayerItem()
    {
        RectTransform root = NewUI("LobbyPlayerItem");
        root.sizeDelta = new Vector2(320f, 40f);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 40f; le.preferredHeight = 40f;

        Image bg = AddImage(root, "RowBg", Button, sliced: true);
        Stretch(bg.rectTransform);

        TMP_Text name = AddText(root, "Name", "Player", 15, TextAlignmentOptions.MidlineLeft, TextCol);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0, 0); nrt.anchorMax = new Vector2(1, 1);
        nrt.offsetMin = new Vector2(12, 0); nrt.offsetMax = new Vector2(-72, 0);

        TMP_Text status = AddText(root, "Status", "...", 13, TextAlignmentOptions.MidlineRight, TextDim);
        var srt = status.rectTransform;
        srt.anchorMin = new Vector2(1, 0); srt.anchorMax = new Vector2(1, 1);
        srt.pivot = new Vector2(1, 0.5f); srt.sizeDelta = new Vector2(70, 0);
        srt.anchoredPosition = new Vector2(-10, 0);

        var ui = root.gameObject.AddComponent<LobbyPlayerItemUI>();
        Set(ui, "background", bg);
        Set(ui, "nameText", name);
        Set(ui, "statusText", status);
        return root.gameObject;
    }

    private static GameObject BuildCategoryButton()
    {
        RectTransform root = NewUI("CategoryButton");
        root.sizeDelta = new Vector2(440f, 46f);
        var le = root.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 46f; le.preferredHeight = 46f;

        Image bg = AddImage(root, "RowBg", new Color(Button.r, Button.g, Button.b, 0.5f), sliced: true);
        Stretch(bg.rectTransform);

        Image icon = AddImage(root, "Icon", Accent, sliced: false);
        icon.sprite = Circle;
        var irt = icon.rectTransform;
        irt.anchorMin = new Vector2(0, 0.5f); irt.anchorMax = new Vector2(0, 0.5f);
        irt.sizeDelta = new Vector2(28, 28); irt.anchoredPosition = new Vector2(26, 0);
        icon.enabled = false; // opcional

        TMP_Text label = AddText(root, "Label", "CATEGORIA", 18, TextAlignmentOptions.MidlineLeft, TextDim);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0, 0); lrt.anchorMax = new Vector2(1, 1);
        lrt.offsetMin = new Vector2(52, 0); lrt.offsetMax = new Vector2(-150, 0);
        label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 18;
        label.overflowMode = TextOverflowModes.Ellipsis;

        Button prev = AddArrow(root, "Prev", "‹", new Vector2(-150, 0));
        TMP_Text value = AddText(root, "Value", "1/1", 22, TextAlignmentOptions.Center, TextCol);
        var vrt = value.rectTransform;
        vrt.anchorMin = new Vector2(1, 0.5f); vrt.anchorMax = new Vector2(1, 0.5f);
        vrt.sizeDelta = new Vector2(64, 40); vrt.anchoredPosition = new Vector2(-86, 0);

        Image swatch = AddImage(root, "Swatch", Color.white, sliced: true);
        var wrt = swatch.rectTransform;
        wrt.anchorMin = new Vector2(1, 0.5f); wrt.anchorMax = new Vector2(1, 0.5f);
        wrt.sizeDelta = new Vector2(64, 32); wrt.anchoredPosition = new Vector2(-86, 0);
        swatch.gameObject.SetActive(false);

        Button next = AddArrow(root, "Next", "›", new Vector2(-22, 0));

        var ui = root.gameObject.AddComponent<CategoryButtonUI>();
        Set(ui, "label", label);
        Set(ui, "valueText", value);
        Set(ui, "icon", icon);
        Set(ui, "previousButton", prev);
        Set(ui, "nextButton", next);
        Set(ui, "swatch", swatch);
        return root.gameObject;
    }

    private static GameObject BuildOptionsPanel()
    {
        RectTransform root = NewUI("GarageOptionsPanel");
        SetAnchorsRaw(root, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(40, 40), new Vector2(500, -190));
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = Rounded; bg.type = Image.Type.Sliced; bg.color = Panel;

        TMP_Text title = AddText(root, "OptTitle", "CUSTOMIZAR", 22, TextAlignmentOptions.Left, TextDim);
        SetAP(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(-32, 30));
        title.characterSpacing = 4f;

        RectTransform viewport = NewUI("Viewport", root);
        viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14, 14); viewport.offsetMax = new Vector2(-14, -52);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1); content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
        AddVerticalList(content, 8f);

        var scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 24f;

        var ui = root.gameObject.AddComponent<GarageOptionsPanelUI>();
        Set(ui, "content", content);
        Set(ui, "title", title);
        return root.gameObject;
    }

    private static GameObject BuildLobbyPanel()
    {
        RectTransform root = NewUI("GarageLobbyPanel");
        SetAnchorsRaw(root, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-380, 180), new Vector2(-40, -190));
        var bg = root.gameObject.AddComponent<Image>();
        bg.sprite = Rounded; bg.type = Image.Type.Sliced; bg.color = Panel;

        TMP_Text title = AddText(root, "LobbyTitle", "LOBBY", 22, TextAlignmentOptions.Left, TextDim);
        SetAP(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -26), new Vector2(-32, 30));
        title.characterSpacing = 4f;

        TMP_Text count = AddText(root, "LobbyCount", "JOGADORES 1/16", 16, TextAlignmentOptions.Right, Accent);
        SetAP(count.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(-6, -26), new Vector2(-16, 28));

        TMP_Text status = AddText(root, "LobbyStatus", "Local (offline)", 13, TextAlignmentOptions.TopLeft, TextDim);
        SetAP(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), new Vector2(-28, 34));
        status.fontStyle = FontStyles.Italic; status.textWrappingMode = TextWrappingModes.Normal;

        RectTransform viewport = NewUI("LobbyViewport", root);
        viewport.anchorMin = new Vector2(0, 0); viewport.anchorMax = new Vector2(1, 1);
        viewport.offsetMin = new Vector2(14, 144); viewport.offsetMax = new Vector2(-14, -90);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform listContent = NewUI("LobbyContent", viewport);
        listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1);
        listContent.pivot = new Vector2(0.5f, 1); listContent.anchoredPosition = Vector2.zero;
        AddVerticalList(listContent, 6f);

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = listContent;
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 20f;

        TMP_Text joinCode = AddText(root, "LobbyJoinCode", "CODIGO --", 13, TextAlignmentOptions.MidlineLeft, Accent);
        SetAP(joinCode.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 126), new Vector2(-28, 26));

        TMP_InputField joinInput = PanelInput(root, "CODIGO", new Vector2(0, 0), new Vector2(1, 0), new Vector2(-54, 86), new Vector2(-122, 38));
        TMP_Text enterLabel = PanelButton(root, "ENTRAR", new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 86), new Vector2(104, 38), 14);
        TMP_Text readyLabel = PanelButton(root, "PRONTO", new Vector2(0, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(-6, 44), 18);
        TMP_Text inviteLabel = PanelButton(root, "CONVIDAR", new Vector2(0.5f, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(-6, 44), 18);

        var ui = root.gameObject.AddComponent<GarageLobbyPanelUI>();
        Set(ui, "title", title);
        Set(ui, "count", count);
        Set(ui, "status", status);
        Set(ui, "listContent", listContent);
        Set(ui, "joinCode", joinCode);
        Set(ui, "joinInput", joinInput);
        Set(ui, "enterButton", enterLabel.GetComponentInParent<Button>());
        Set(ui, "readyButton", readyLabel.GetComponentInParent<Button>());
        Set(ui, "readyLabel", readyLabel);
        Set(ui, "inviteButton", inviteLabel.GetComponentInParent<Button>());
        return root.gameObject;
    }

    private static void AddVerticalList(RectTransform content, float spacing)
    {
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.childControlHeight = true; vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false; vlg.childForceExpandWidth = true;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private static TMP_Text PanelButton(Transform parent, string text, Vector2 min, Vector2 max, Vector2 pos, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetAP((RectTransform)go.transform, min, max, pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = Button;
        go.GetComponent<Button>().targetGraphic = img;
        TMP_Text label = AddText((RectTransform)go.transform, "Text", text, fontSize, TextAlignmentOptions.Center, TextCol);
        Stretch(label.rectTransform);
        return label;
    }

    private static TMP_InputField PanelInput(Transform parent, string placeholder, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject("JoinCodeInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        SetAP(rt, min, max, pos, size);
        var img = go.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = new Color(Button.r, Button.g, Button.b, 0.65f);

        TMP_Text text = AddText(rt, "Text", "", 16, TextAlignmentOptions.MidlineLeft, TextCol);
        Stretch(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(12, 0); text.rectTransform.offsetMax = new Vector2(-12, 0);
        text.raycastTarget = true;

        TMP_Text ph = AddText(rt, "Placeholder", placeholder, 14, TextAlignmentOptions.MidlineLeft, TextDim);
        ph.fontStyle = FontStyles.Italic;
        Stretch(ph.rectTransform);
        ph.rectTransform.offsetMin = new Vector2(12, 0); ph.rectTransform.offsetMax = new Vector2(-12, 0);

        var input = go.GetComponent<TMP_InputField>();
        input.textComponent = (TextMeshProUGUI)text;
        input.placeholder = (TextMeshProUGUI)ph;
        input.characterLimit = 8;
        input.textViewport = rt;
        return input;
    }

    private static void SetAnchorsRaw(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    private static void SetAP(RectTransform rt, Vector2 min, Vector2 max, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    // ----------------------------------------------------------------- helpers

    private static Button AddArrow(Transform parent, string name, string glyph, Vector2 anchoredPos)
    {
        GameObject go = new GameObject("Btn_" + name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(1, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.sizeDelta = new Vector2(40, 40); rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.sprite = Rounded; img.type = Image.Type.Sliced; img.color = Button;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;

        TMP_Text label = AddText(rt, "Text", glyph, 26, TextAlignmentOptions.Center, TextCol);
        Stretch(label.rectTransform);
        return btn;
    }

    private static GameObject Save(GameObject go, string fileName)
    {
        string path = $"{ResourcesDir}/{fileName}.prefab";
        GameObject asset = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return asset;
    }

    private static RectTransform NewUI(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image AddImage(Transform parent, string name, Color color, bool sliced)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = Rounded;
        img.color = color;
        img.raycastTarget = false;
        if (sliced) img.type = Image.Type.Sliced;
        return img;
    }

    private static TMP_Text AddText(Transform parent, string name, string content, float size, TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Set(object target, string field, object value)
    {
        FieldInfo f = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (f != null) f.SetValue(target, value);
        else Debug.LogWarning($"[GaragePrefabGenerator] Campo '{field}' não encontrado em {target.GetType().Name}.");
    }
}
#endif
