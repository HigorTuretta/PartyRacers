using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ithappy;
using PartyRacers.Networking;

// UI limpa e responsiva da Garagem. Constrói a interface por código (mesmo estilo
// procedural do KartHUDOverlay) sobre um Canvas com CanvasScaler "Scale With Screen
// Size", então respeita qualquer resolução. Liga os botões ao KartVisualCustomizer
// do carro em exibição e carrega a cena de corrida ao clicar em CORRER.
public class GarageController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartVisualCustomizer customizer;
    [SerializeField] private Canvas canvas;

    [Header("Fluxo")]
    [SerializeField] private string raceSceneName = "MiniGolfeRun";

    [Header("Câmera (auto-enquadramento)")]
    [SerializeField] private Camera previewCamera;
    [SerializeField] private float camPitch = 8f;
    [SerializeField] private float camYaw = 18f;
    [SerializeField] private float framePadding = 1.2f;
    [SerializeField, Range(0f, 0.6f)] private float carRightBias = 0.24f;

    [Header("Paleta")]
    [SerializeField] private Color panelColor = new Color(0.05f, 0.06f, 0.09f, 0.82f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.19f, 0.26f, 0.95f);
    [SerializeField] private Color accentColor = new Color(1f, 0.45f, 0.1f, 1f);
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.97f);
    [SerializeField] private Color textDimColor = new Color(0.78f, 0.82f, 0.9f, 0.85f);
    [SerializeField] private Color raceColor = new Color(0.16f, 0.74f, 0.36f, 1f);

    private static readonly Dictionary<CarElementName, string> Labels = new Dictionary<CarElementName, string>
    {
        { CarElementName.Wheel, "RODAS" },
        { CarElementName.FrontBumper, "FRENTE" },
        { CarElementName.RearBumper, "TRASEIRA" },
        { CarElementName.Pipe, "ESCAPE" },
        { CarElementName.Headlight, "FARÓIS" },
        { CarElementName.Decals, "ADESIVOS" },
        { CarElementName.FogLight, "MILHA" },
        { CarElementName.Engine, "MOTOR" },
        { CarElementName.Spoiler, "AEROFÓLIO" },
        { CarElementName.Racer, "PILOTO" },
    };

    private TMP_Text _carNameText;
    private Image _colorSwatch;
    private RectTransform _optionsContent;
    private readonly List<System.Action> _valueRefreshers = new List<System.Action>();
    private Sprite _roundSprite;

    // --- Lobby (garagem como lobby online) ---
    private RacePlayerRegistry _registry;
    private NetworkBootstrap _bootstrap;
    private TMP_Text _lobbyStatusText;
    private TMP_Text _lobbyCountText;
    private RectTransform _lobbyListContent;
    private TMP_Text _readyButtonLabel;
    private TMP_Text _lobbyJoinCodeText;
    private TMP_InputField _joinCodeInput;
    private Button _raceButton;
    private TMP_Text _raceButtonLabel;

    private void Start()
    {
        if (customizer == null)
            customizer = FindAnyObjectByType<KartVisualCustomizer>();

        if (canvas == null)
            canvas = GetComponentInChildren<Canvas>();

        if (previewCamera == null)
            previewCamera = Camera.main;

        _roundSprite = BuildRoundSprite();

        EnsureNetworkObjects();

        BuildStaticUI();

        if (customizer != null)
        {
            customizer.EnsureBuilt();
            customizer.CarRebuilt += OnCarRebuilt;
        }

        if (_registry != null)
            _registry.Changed += RefreshLobby;

        if (_bootstrap != null)
            _bootstrap.StatusChanged += OnNetworkStatusChanged;

        RefreshCarName();
        RebuildOptions();
        RefreshLobby();
        FrameCamera();
    }

    private void OnDestroy()
    {
        if (customizer != null)
            customizer.CarRebuilt -= OnCarRebuilt;

        if (_registry != null)
            _registry.Changed -= RefreshLobby;

        if (_bootstrap != null)
            _bootstrap.StatusChanged -= OnNetworkStatusChanged;
    }

    // Garante os sistemas de rede/registro (singletons, persistentes). Funcionam offline:
    // por padrão registram só o jogador local. A camada online popula remotos quando habilitada.
    private void EnsureNetworkObjects()
    {
        _registry = RacePlayerRegistry.Instance;
        _bootstrap = NetworkBootstrap.Instance;

        if (_registry == null || _bootstrap == null)
        {
            GameObject systems = new GameObject("NetworkSystems");

            _registry = RacePlayerRegistry.Instance != null
                ? RacePlayerRegistry.Instance
                : systems.AddComponent<RacePlayerRegistry>();

            _bootstrap = NetworkBootstrap.Instance != null
                ? NetworkBootstrap.Instance
                : systems.AddComponent<NetworkBootstrap>();
        }

        if (_registry != null)
            _registry.EnsureLocalPlayer();
    }

    private void OnCarRebuilt()
    {
        RefreshCarName();
        RebuildOptions();
        FrameCamera();
    }

    // Enquadra a câmera no carro atual (qualquer tamanho de carro fica bem enquadrado),
    // deslocando-o um pouco para a direita para não ficar atrás do painel de opções.
    private void FrameCamera()
    {
        if (previewCamera == null || customizer == null || customizer.CurrentRig == null)
            return;

        var renderers = customizer.CurrentRig.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        Bounds b = renderers[0].bounds;
        foreach (var r in renderers)
            b.Encapsulate(r.bounds);

        float radius = b.extents.magnitude;
        float vFov = previewCamera.fieldOfView * Mathf.Deg2Rad;
        float dist = (radius / Mathf.Tan(vFov * 0.5f)) * framePadding;

        Quaternion rot = Quaternion.Euler(camPitch, camYaw, 0f);
        Vector3 viewDir = rot * Vector3.forward;
        previewCamera.transform.position = b.center - viewDir * dist;
        previewCamera.transform.rotation = rot;

        // mira um pouco à esquerda do centro => o carro aparece à direita da tela
        Vector3 lookPoint = b.center - previewCamera.transform.right * (radius * carRightBias);
        previewCamera.transform.LookAt(lookPoint);
    }

    // ---------------------------------------------------------------- UI estática
    private void BuildStaticUI()
    {
        RectTransform root = canvas.transform as RectTransform;

        TMP_Text title = CreateText(root, "Title", "GARAGEM", 46, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(700f, 70f));
        title.characterSpacing = 8f;

        Image titleAccent = CreateImage(root, "TitleAccent", _roundSprite, accentColor);
        Anchor(titleAccent.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(200f, 5f));

        // Seletor de carro (topo)
        RectTransform carBar = CreatePanel(root, "CarBar", new Color(0, 0, 0, 0));
        Anchor(carBar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(560f, 70f));

        CreateButton(carBar, "‹", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(64f, 64f), buttonColor, () => customizer?.PreviousCar());
        CreateButton(carBar, "›", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(64f, 64f), buttonColor, () => customizer?.NextCar());

        _carNameText = CreateText(carBar, "CarName", "CARRO", 34, FontStyles.Bold, TextAlignmentOptions.Center, accentColor);
        Anchor(_carNameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 64f));

        // Painel de opções (lateral esquerda, altura flexível para qualquer resolução)
        RectTransform panel = CreatePanel(root, "OptionsPanel", panelColor);
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.offsetMin = new Vector2(40f, 40f);
        panel.offsetMax = new Vector2(500f, -190f);

        TMP_Text optTitle = CreateText(panel, "OptTitle", "CUSTOMIZAR", 22, FontStyles.Bold, TextAlignmentOptions.Left, textDimColor);
        Anchor(optTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-32f, 30f));
        optTitle.characterSpacing = 4f;

        // ScrollRect para acomodar muitas categorias sem cortar
        RectTransform viewport = CreateUI("Viewport", panel);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(14f, 14f);
        viewport.offsetMax = new Vector2(-14f, -52f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateUI("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);
        var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8f;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;

        _optionsContent = content;

        // Painel de lobby (lado direito) — garagem funciona como lobby online.
        BuildLobbyPanel(root);

        // Botão CORRER / INICIAR (host). Canto inferior direito.
        _raceButtonLabel = CreateButton(root, "CORRER", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 64f), new Vector2(300f, 96f), raceColor, StartRace, 34);
        _raceButton = _raceButtonLabel != null ? _raceButtonLabel.GetComponentInParent<Button>() : null;
    }

    // ---------------------------------------------------------------- Lobby
    private void BuildLobbyPanel(RectTransform root)
    {
        RectTransform panel = CreatePanel(root, "LobbyPanel", panelColor);
        panel.anchorMin = new Vector2(1f, 0f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.offsetMin = new Vector2(-380f, 180f);
        panel.offsetMax = new Vector2(-40f, -190f);

        TMP_Text title = CreateText(panel, "LobbyTitle", "LOBBY", 22, FontStyles.Bold, TextAlignmentOptions.Left, textDimColor);
        Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(-32f, 30f));
        title.characterSpacing = 4f;

        _lobbyCountText = CreateText(panel, "LobbyCount", "JOGADORES 1/16", 16, FontStyles.Bold, TextAlignmentOptions.Right, accentColor);
        Anchor(_lobbyCountText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -26f), new Vector2(-16f, 28f));

        _lobbyStatusText = CreateText(panel, "LobbyStatus", "Local (offline)", 13, FontStyles.Italic, TextAlignmentOptions.TopLeft, textDimColor);
        Anchor(_lobbyStatusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -52f), new Vector2(-28f, 34f));
        _lobbyStatusText.textWrappingMode = TextWrappingModes.Normal;

        // Lista rolável de jogadores.
        RectTransform viewport = CreateUI("LobbyViewport", panel);
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(14f, 144f);
        viewport.offsetMax = new Vector2(-14f, -90f);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform listContent = CreateUI("LobbyContent", viewport);
        listContent.anchorMin = new Vector2(0f, 1f);
        listContent.anchorMax = new Vector2(1f, 1f);
        listContent.pivot = new Vector2(0.5f, 1f);
        listContent.anchoredPosition = Vector2.zero;
        var vlg = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 6f;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        var fitter = listContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = listContent;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 20f;
        _lobbyListContent = listContent;

        _lobbyJoinCodeText = CreateText(panel, "LobbyJoinCode", "CODIGO --", 13, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, accentColor);
        Anchor(_lobbyJoinCodeText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 126f), new Vector2(-28f, 26f));

        _joinCodeInput = CreateInputField(panel, "JoinCodeInput", "CODIGO", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-54f, 86f), new Vector2(-122f, 38f));
        CreateButton(panel, "ENTRAR", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 86f), new Vector2(104f, 38f), buttonColor, JoinOnlineGame, 14);

        // Botões de ação (PRONTO / CONVIDAR) na base do painel.
        var ready = CreateButton(panel, "PRONTO", new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(-6f, 44f), buttonColor, ToggleReady, 18);
        _readyButtonLabel = ready;

        CreateButton(panel, "CONVIDAR", new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(0f, 30f), new Vector2(-6f, 44f), buttonColor, InvitePlayers, 18);
    }

    private void RefreshLobby()
    {
        if (_registry == null)
            return;

        if (_lobbyCountText != null)
            _lobbyCountText.text = $"JOGADORES {_registry.Count}/{RaceConstants.MaxPlayers}";

        if (_readyButtonLabel != null && _registry.LocalPlayer != null)
            _readyButtonLabel.text = _registry.LocalPlayer.IsReady ? "PRONTO ✓" : "PRONTO";

        if (_lobbyStatusText != null && _bootstrap != null)
            _lobbyStatusText.text = _bootstrap.Status;

        if (_lobbyJoinCodeText != null && _bootstrap != null)
            _lobbyJoinCodeText.text = _bootstrap.HasJoinCode ? $"CODIGO {_bootstrap.CurrentJoinCode}" : "CODIGO --";

        RefreshRaceButtonState();

        if (_lobbyListContent == null)
            return;

        for (int i = _lobbyListContent.childCount - 1; i >= 0; i--)
            Destroy(_lobbyListContent.GetChild(i).gameObject);

        foreach (RacePlayerInfo info in _registry.Players)
            BuildPlayerRow(info);
    }

    private void BuildPlayerRow(RacePlayerInfo info)
    {
        RectTransform row = CreateUI("Player_" + info.DisplayName, _lobbyListContent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        Color rowColor = info.IsLocal ? Color.Lerp(buttonColor, accentColor, 0.25f) : buttonColor * 0.5f;
        Image bg = CreateImage(row, "RowBg", _roundSprite, rowColor);
        StretchFull(bg.rectTransform);

        string kindTag = info.IsLocal ? " (você)" : info.IsBot ? " (bot)" : "";
        string hostTag = info.IsHost ? "★ " : "";
        TMP_Text name = CreateText(row, "Name", hostTag + info.DisplayName + kindTag, 15, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        var nrt = name.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(12f, 0f);
        nrt.offsetMax = new Vector2(-72f, 0f);

        TMP_Text status = CreateText(row, "Ready", info.IsReady ? "PRONTO" : "...", 13, FontStyles.Bold, TextAlignmentOptions.MidlineRight,
            info.IsReady ? raceColor : textDimColor);
        var srt = status.rectTransform;
        srt.anchorMin = new Vector2(1f, 0f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(1f, 0.5f);
        srt.sizeDelta = new Vector2(70f, 0f);
        srt.anchoredPosition = new Vector2(-10f, 0f);
    }

    private void ToggleReady()
    {
        if (_bootstrap != null && _bootstrap.IsOnline && _registry != null && _registry.LocalPlayer != null)
        {
            _bootstrap.SetLocalReady(!_registry.LocalPlayer.IsReady);
            return;
        }

        if (_registry != null)
            _registry.ToggleLocalReady();
    }

    private void InvitePlayers()
    {
        if (_bootstrap == null)
            return;

        if (_bootstrap.IsOnline && _bootstrap.HasJoinCode)
        {
            GUIUtility.systemCopyBuffer = _bootstrap.CurrentJoinCode;
            Debug.Log($"Convite: codigo do lobby copiado: {_bootstrap.CurrentJoinCode}");

            if (_lobbyStatusText != null)
                _lobbyStatusText.text = $"Codigo copiado: {_bootstrap.CurrentJoinCode}";

            return;
        }

        _bootstrap.HostGame();
    }

    private void JoinOnlineGame()
    {
        if (_bootstrap == null || _joinCodeInput == null)
            return;

        _bootstrap.JoinGame(_joinCodeInput.text);
    }

    private void OnNetworkStatusChanged(string status)
    {
        if (_lobbyStatusText != null)
            _lobbyStatusText.text = status;

        if (_lobbyJoinCodeText != null && _bootstrap != null)
            _lobbyJoinCodeText.text = _bootstrap.HasJoinCode ? $"CODIGO {_bootstrap.CurrentJoinCode}" : "CODIGO --";

        RefreshRaceButtonState();
    }

    private void StartRace()
    {
        customizer?.EnsureBuilt();
        KartGarageSelection.Save();
        _registry?.SetLocalPlayerVisual(KartGarageSelection.Capture());

        if (_bootstrap != null && _bootstrap.IsOnline)
        {
            if (_registry == null || !_registry.AllReady())
            {
                if (_lobbyStatusText != null)
                    _lobbyStatusText.text = "Todos os jogadores precisam estar prontos.";

                return;
            }

            _bootstrap.StartRaceScene(raceSceneName);
            return;
        }

        if (!string.IsNullOrEmpty(raceSceneName))
            UnityEngine.SceneManagement.SceneManager.LoadScene(raceSceneName);
    }

    private void RefreshRaceButtonState()
    {
        bool online = _bootstrap != null && _bootstrap.IsOnline;
        bool canRace = !online || (_registry != null && _registry.AllReady());

        if (_raceButton != null)
            _raceButton.interactable = canRace;

        if (_raceButtonLabel != null)
            _raceButtonLabel.text = online && !canRace ? "AGUARDANDO" : "CORRER";
    }

    // ---------------------------------------------------------------- Opções dinâmicas
    private void RefreshCarName()
    {
        if (_carNameText == null || customizer == null)
            return;

        _carNameText.text = $"CARRO {customizer.CarIndex + 1:00}";
    }

    private void RebuildOptions()
    {
        if (_optionsContent == null)
            return;

        for (int i = _optionsContent.childCount - 1; i >= 0; i--)
            Destroy(_optionsContent.GetChild(i).gameObject);
        _valueRefreshers.Clear();

        // Linha de cor
        BuildColorRow();

        // Linhas por elemento (apenas os que têm variação)
        if (customizer != null && customizer.CurrentRig != null)
        {
            foreach (var element in customizer.CurrentRig.Elements)
            {
                if (element.Elements == null || element.Elements.Count <= 1)
                    continue;

                BuildElementRow(element.ElementName, element.Elements.Count);
            }
        }
    }

    private void BuildColorRow()
    {
        RectTransform row = CreateRow("COR");

        CreateButton(row, "‹", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(40f, 40f), buttonColor,
            () => customizer.SetColor(customizer.ColorIndex - 1));

        _colorSwatch = CreateImage(row, "Swatch", _roundSprite, Color.white);
        Anchor(_colorSwatch.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-86f, 0f), new Vector2(64f, 32f));

        CreateButton(row, "›", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(40f, 40f), buttonColor,
            () => customizer.SetColor(customizer.ColorIndex + 1));

        void Refresh()
        {
            if (_colorSwatch != null && customizer.ColorCount > 0)
                _colorSwatch.color = customizer.PaintPalette[Mathf.Clamp(customizer.ColorIndex, 0, customizer.ColorCount - 1)];
        }
        _valueRefreshers.Add(Refresh);
        Refresh();
    }

    private void BuildElementRow(CarElementName element, int count)
    {
        string label = Labels.TryGetValue(element, out var l) ? l : element.ToString().ToUpperInvariant();
        RectTransform row = CreateRow(label);

        CreateButton(row, "‹", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-150f, 0f), new Vector2(40f, 40f), buttonColor,
            () => customizer.SetElement(element, customizer.GetElementIndex(element) - 1));

        TMP_Text value = CreateText(row, "Value", "1/" + count, 22, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        Anchor(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-86f, 0f), new Vector2(64f, 40f));

        CreateButton(row, "›", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-22f, 0f), new Vector2(40f, 40f), buttonColor,
            () => customizer.SetElement(element, customizer.GetElementIndex(element) + 1));

        void Refresh()
        {
            int idx = Mathf.Clamp(customizer.GetElementIndex(element), 0, count - 1);
            value.text = (idx + 1) + "/" + count;
        }
        _valueRefreshers.Add(Refresh);
        Refresh();
    }

    private void RefreshValues()
    {
        foreach (var r in _valueRefreshers)
            r?.Invoke();
    }

    private RectTransform CreateRow(string label)
    {
        RectTransform row = CreateUI("Row_" + label, _optionsContent);
        var le = row.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 46f;
        le.preferredHeight = 46f;

        Image bg = CreateImage(row, "RowBg", _roundSprite, buttonColor * 0.5f);
        StretchFull(bg.rectTransform);

        TMP_Text lbl = CreateText(row, "Label", label, 18, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textDimColor);
        var lrt = lbl.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(16f, 0f);
        lrt.offsetMax = new Vector2(-150f, 0f);
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 12f;
        lbl.fontSizeMax = 18f;
        lbl.overflowMode = TextOverflowModes.Ellipsis;

        return row;
    }

    // ---------------------------------------------------------------- Helpers UI
    private static RectTransform CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        RectTransform rt = CreateUI(name, parent);
        if (color.a > 0f)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = _roundSprite;
            img.type = Image.Type.Sliced;
            img.color = color;
        }
        return rt;
    }

    private Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text CreateText(Transform parent, string name, string content, float size, FontStyles style, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private TMP_Text CreateButton(Transform parent, string text, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, float fontSize = 26)
    {
        var go = new GameObject("Btn_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.sprite = _roundSprite;
        img.type = Image.Type.Sliced;
        img.color = color;

        var btn = go.GetComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
        colors.fadeDuration = 0.06f;
        btn.colors = colors;
        btn.onClick.AddListener(onClick);
        btn.onClick.AddListener(RefreshValues);

        var label = CreateText(rt, "Text", text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center, textColor);
        StretchFull(label.rectTransform);
        return label;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, string placeholder, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var img = go.GetComponent<Image>();
        img.sprite = _roundSprite;
        img.type = Image.Type.Sliced;
        img.color = buttonColor * 0.65f;

        TMP_Text text = CreateText(rt, "Text", "", 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, textColor);
        StretchFull(text.rectTransform);
        text.rectTransform.offsetMin = new Vector2(12f, 0f);
        text.rectTransform.offsetMax = new Vector2(-12f, 0f);
        text.raycastTarget = true;

        TMP_Text placeholderText = CreateText(rt, "Placeholder", placeholder, 14, FontStyles.Italic, TextAlignmentOptions.MidlineLeft, textDimColor);
        StretchFull(placeholderText.rectTransform);
        placeholderText.rectTransform.offsetMin = new Vector2(12f, 0f);
        placeholderText.rectTransform.offsetMax = new Vector2(-12f, 0f);

        TMP_InputField input = go.GetComponent<TMP_InputField>();
        input.textComponent = (TextMeshProUGUI)text;
        input.placeholder = (TextMeshProUGUI)placeholderText;
        input.characterLimit = 8;
        input.textViewport = rt;
        input.onValueChanged.AddListener(value =>
        {
            string upper = value.ToUpperInvariant();
            if (value != upper)
                input.SetTextWithoutNotify(upper);
        });

        return input;
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 anchoredPos, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Sprite BuildRoundSprite()
    {
        const int res = 32, r = 8;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[res * res];
        for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float dx = Mathf.Max(0f, r - x, x - (res - 1 - r));
                float dy = Mathf.Max(0f, r - y, y - (res - 1 - r));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * res + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d));
            }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
    }
}
