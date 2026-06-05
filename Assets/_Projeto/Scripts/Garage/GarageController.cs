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
    private RectTransform _optionsContent;
    private readonly List<System.Action> _valueRefreshers = new List<System.Action>();
    private Sprite _roundSprite;

    // Prefabs componentizados (Resources). Lista de jogadores e categorias agora são prefabs.
    private GameObject _playerItemPrefab;
    private GameObject _categoryButtonPrefab;
    private GameObject PlayerItemPrefab => _playerItemPrefab != null
        ? _playerItemPrefab : (_playerItemPrefab = Resources.Load<GameObject>("LobbyPlayerItem"));
    private GameObject CategoryButtonPrefab => _categoryButtonPrefab != null
        ? _categoryButtonPrefab : (_categoryButtonPrefab = Resources.Load<GameObject>("CategoryButton"));

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

        // Painel de opções (prefab componentizado, lado esquerdo).
        GameObject optionsPanel = InstantiatePanel("GarageOptionsPanel");
        if (optionsPanel != null)
        {
            var optUi = optionsPanel.GetComponent<GarageOptionsPanelUI>();
            _optionsContent = optUi != null ? optUi.Content : null;
        }

        // Painel de lobby (prefab componentizado, lado direito) — garagem funciona como lobby online.
        SetupLobbyPanel();

        // Botão CORRER / INICIAR (host). Canto inferior direito.
        _raceButtonLabel = CreateButton(root, "CORRER", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-190f, 64f), new Vector2(300f, 96f), raceColor, StartRace, 34);
        _raceButton = _raceButtonLabel != null ? _raceButtonLabel.GetComponentInParent<Button>() : null;
    }

    // ---------------------------------------------------------------- Lobby
    // Instancia um prefab de painel (Resources) sob o canvas. O prefab já tem suas âncoras
    // (posição relativa ao canvas) — basta parentar.
    private GameObject InstantiatePanel(string resourceName)
    {
        GameObject prefab = Resources.Load<GameObject>(resourceName);
        if (prefab == null)
        {
            Debug.LogError($"{resourceName}.prefab ausente em Resources. Rode 'PartyRacers/HUD/Gerar Prefabs da Garagem'.", this);
            return null;
        }
        return Instantiate(prefab, canvas.transform);
    }

    // Instancia o prefab de lobby e liga suas referências/botões à lógica existente.
    private void SetupLobbyPanel()
    {
        GameObject panel = InstantiatePanel("GarageLobbyPanel");
        if (panel == null)
            return;

        var ui = panel.GetComponent<GarageLobbyPanelUI>();
        if (ui == null)
        {
            Debug.LogError("GarageLobbyPanel.prefab sem GarageLobbyPanelUI.", this);
            return;
        }

        _lobbyCountText = ui.Count;
        _lobbyStatusText = ui.Status;
        _lobbyListContent = ui.ListContent;
        _readyButtonLabel = ui.ReadyLabel;
        _lobbyJoinCodeText = ui.JoinCode;
        _joinCodeInput = ui.JoinInput;

        if (ui.JoinInput != null)
            ui.JoinInput.onValueChanged.AddListener(value =>
            {
                string upper = value.ToUpperInvariant();
                if (value != upper)
                    ui.JoinInput.SetTextWithoutNotify(upper);
            });

        if (ui.ReadyButton != null) ui.ReadyButton.onClick.AddListener(ToggleReady);
        if (ui.InviteButton != null) ui.InviteButton.onClick.AddListener(InvitePlayers);
        if (ui.EnterButton != null) ui.EnterButton.onClick.AddListener(JoinOnlineGame);
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
        GameObject prefab = PlayerItemPrefab;
        if (prefab == null)
        {
            Debug.LogError("LobbyPlayerItem.prefab ausente em Resources. Rode 'PartyRacers/HUD/Gerar Prefabs da Garagem'.", this);
            return;
        }

        GameObject go = Instantiate(prefab, _lobbyListContent);
        var item = go.GetComponent<LobbyPlayerItemUI>();
        if (item != null)
            item.Set(info.DisplayName, info.IsHost, info.IsLocal, info.IsBot, info.IsReady);
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
        // Dono da sala = quem criou o convite = host real do NGO (Mode == Host). Offline, o jogador
        // local é o "dono". Amarrar ao Mode (e não só ao HostId do lobby) garante estado seguro se o
        // host sair: nenhum cliente vira dono funcional, então o início fica bloqueado para todos.
        bool isOwner = !online || (_bootstrap != null && _bootstrap.Mode == NetworkBootstrap.SessionMode.Host);
        bool allReady = _registry != null && _registry.AllReady();
        bool canRace = isOwner && (!online || allReady);

        // Só o DONO vê/usa o botão Iniciar Corrida.
        if (_raceButton != null)
        {
            bool show = isOwner || !online;
            if (_raceButton.gameObject.activeSelf != show)
                _raceButton.gameObject.SetActive(show);
            _raceButton.interactable = canRace;
        }

        if (_raceButtonLabel != null)
            _raceButtonLabel.text = online && !allReady ? "AGUARDANDO" : "CORRER";
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
        CategoryButtonUI cat = SpawnCategory("COR");
        if (cat == null)
            return;

        cat.UseSwatch(true);
        cat.AddNavListeners(
            () => { customizer.SetColor(customizer.ColorIndex - 1); RefreshValues(); },
            () => { customizer.SetColor(customizer.ColorIndex + 1); RefreshValues(); });

        void Refresh()
        {
            if (customizer.ColorCount > 0)
                cat.SetSwatchColor(customizer.PaintPalette[Mathf.Clamp(customizer.ColorIndex, 0, customizer.ColorCount - 1)]);
        }
        _valueRefreshers.Add(Refresh);
        Refresh();
    }

    private void BuildElementRow(CarElementName element, int count)
    {
        string label = Labels.TryGetValue(element, out var l) ? l : element.ToString().ToUpperInvariant();
        CategoryButtonUI cat = SpawnCategory(label);
        if (cat == null)
            return;

        cat.UseSwatch(false);
        cat.AddNavListeners(
            () => { customizer.SetElement(element, customizer.GetElementIndex(element) - 1); RefreshValues(); },
            () => { customizer.SetElement(element, customizer.GetElementIndex(element) + 1); RefreshValues(); });

        void Refresh()
        {
            int idx = Mathf.Clamp(customizer.GetElementIndex(element), 0, count - 1);
            cat.SetValue((idx + 1) + "/" + count);
        }
        _valueRefreshers.Add(Refresh);
        Refresh();
    }

    // Instancia o prefab componentizado de categoria sob o painel de opções.
    private CategoryButtonUI SpawnCategory(string label)
    {
        GameObject prefab = CategoryButtonPrefab;
        if (prefab == null)
        {
            Debug.LogError("CategoryButton.prefab ausente em Resources. Rode 'PartyRacers/HUD/Gerar Prefabs da Garagem'.", this);
            return null;
        }

        GameObject go = Instantiate(prefab, _optionsContent);
        var cat = go.GetComponent<CategoryButtonUI>();
        cat?.Configure(label, null, null, null);
        return cat;
    }

    private void RefreshValues()
    {
        foreach (var r in _valueRefreshers)
            r?.Invoke();
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
