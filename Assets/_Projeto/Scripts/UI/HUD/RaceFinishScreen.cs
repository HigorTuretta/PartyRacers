using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Fluxo de fim de corrida para o PLAYER local.
    /// Quando o player cruza a última linha de chegada:
    ///  - remove o controle manual e engata o piloto automático (KartFinishAutopilot);
    ///  - exibe um banner/tela de resultados com a classificação;
    ///  - a classificação continua atualizando enquanto os retardatários terminam;
    ///  - mostra tempo de cada volta e tempo total de quem já cruzou;
    ///  - oferece Rematch (reinicia a cena) e Voltar para Garagem (carrega a cena configurável).
    ///
    /// A UI é montada em runtime caso as referências não sejam atribuídas no Inspector — assim o
    /// fluxo funciona só largando este componente num GameObject da cena de corrida.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceFinishScreen : MonoBehaviour
    {
        [Header("Cenas")]
        [Tooltip("Cena da garagem/menu carregada por 'Voltar para Garagem'.")]
        [SerializeField] private string garageSceneName = "Garage";

        [Header("Comportamento")]
        [Tooltip("Aciona o piloto automático do player ao terminar (carro segue sozinho).")]
        [SerializeField] private bool engageAutopilotOnFinish = true;
        [Tooltip("Mostra/libera o cursor do mouse ao abrir a tela de resultados.")]
        [SerializeField] private bool showCursorOnFinish = true;
        [Tooltip("Máximo de linhas mostradas na classificação.")]
        [SerializeField] private int maxRows = 16;

        [Header("UI (opcional — montada em runtime se vazio)")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private RectTransform rowTemplate;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button garageButton;

        [Header("Estilo (runtime)")]
        [SerializeField] private Color backgroundColor = new Color(0.05f, 0.06f, 0.12f, 0.82f);
        [SerializeField] private Color panelColor = new Color(0.10f, 0.12f, 0.22f, 0.95f);
        [SerializeField] private Color localRowColor = new Color(1f, 0.78f, 0.22f, 0.30f);
        [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.06f);
        [SerializeField] private Color accentColor = new Color(1f, 0.78f, 0.22f, 1f);

        private readonly List<RectTransform> rowPool = new List<RectTransform>();
        private RaceHUDDataProvider provider;
        private KartController localKart;
        private KartRaceTracker localTracker;

        private bool shown;
        private bool subscribed;

        private static readonly string[] FallbackNames =
        {
            "ALEX", "LUCAS", "BRUNO", "MARIA", "JOAO", "ENZO", "FELIPE", "NINA",
            "TURBO", "NITRO", "BLAZE", "RAIO", "FURIA", "TROVAO", "FOGUETE", "VENTO"
        };

        private void Awake()
        {
            provider = FindAnyObjectByType<RaceHUDDataProvider>(FindObjectsInactive.Exclude);

            if (root == null)
                BuildRuntimeUI();

            HideImmediate();
            WireButtons();
        }

        private void Update()
        {
            EnsureLocalTracker();

            if (!shown)
            {
                if (localTracker != null && localTracker.RaceFinished)
                    ShowResults();
                return;
            }

            RefreshStandings();
        }

        // ----------------------------------------------------------------- Local kart

        private void EnsureLocalTracker()
        {
            if (localKart != null && localKart.gameObject.activeInHierarchy && IsLocal(localKart))
                return;

            localKart = ResolveLocalKart();
            localTracker = localKart != null ? localKart.GetComponent<KartRaceTracker>() : null;

            if (subscribed || localTracker == null)
                return;

            localTracker.RaceJustFinished += OnLocalRaceFinished;
            subscribed = true;
        }

        private KartController ResolveLocalKart()
        {
            if (provider != null && provider.LocalKart != null)
                return provider.LocalKart;

            KartController[] all = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
            KartController firstRigless = null;

            foreach (KartController kart in all)
            {
                if (kart == null)
                    continue;

                KartLocalRig rig = kart.GetComponent<KartLocalRig>();
                if (rig == null)
                {
                    if (firstRigless == null)
                        firstRigless = kart;
                    continue;
                }

                if (rig.IsLocalPlayer)
                    return kart;
            }

            return firstRigless;
        }

        private static bool IsLocal(KartController kart)
        {
            KartLocalRig rig = kart != null ? kart.GetComponent<KartLocalRig>() : null;
            return rig == null || rig.IsLocalPlayer;
        }

        private void OnLocalRaceFinished(KartRaceTracker tracker) => ShowResults();

        // ----------------------------------------------------------------- Mostrar / atualizar

        private void ShowResults()
        {
            if (shown)
                return;

            shown = true;

            if (engageAutopilotOnFinish && localKart != null)
                KartFinishAutopilot.Engage(localKart);

            if (showCursorOnFinish)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (root != null)
            {
                root.alpha = 1f;
                root.interactable = true;
                root.blocksRaycasts = true;
            }

            RefreshStandings();
        }

        private void RefreshStandings()
        {
            List<Ranked> ranked = BuildRanked();
            int rows = Mathf.Min(ranked.Count, maxRows);
            EnsureRowPool(rows);

            for (int i = 0; i < rowPool.Count; i++)
            {
                bool visible = i < rows;
                rowPool[i].gameObject.SetActive(visible);
                if (visible)
                    FillRow(rowPool[i], i + 1, ranked[i]);
            }
        }

        private List<Ranked> BuildRanked()
        {
            List<Ranked> list = new List<Ranked>();
            KartController[] karts = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);

            foreach (KartController kart in karts)
            {
                if (kart == null || !kart.gameObject.activeInHierarchy)
                    continue;

                KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
                list.Add(new Ranked
                {
                    Kart = kart,
                    Tracker = tracker,
                    Finished = tracker != null && tracker.RaceFinished,
                    FinishTime = tracker != null ? tracker.FinishRealtime : -1f,
                    Progress = CalculateProgress(tracker),
                    IsLocal = kart == localKart
                });
            }

            // Terminados primeiro (por ordem de chegada); depois os que ainda correm (por progresso).
            list.Sort((a, b) =>
            {
                if (a.Finished != b.Finished)
                    return a.Finished ? -1 : 1;

                if (a.Finished && b.Finished)
                    return a.FinishTime.CompareTo(b.FinishTime);

                return b.Progress.CompareTo(a.Progress);
            });

            return list;
        }

        private static float CalculateProgress(KartRaceTracker tracker)
        {
            if (tracker == null)
                return 0f;

            int totalCheckpoints = Mathf.Max(1, tracker.TotalCheckpoints);
            if (tracker.RaceFinished)
                return tracker.TotalLaps * totalCheckpoints + totalCheckpoints;

            int currentLap = Mathf.Max(1, tracker.CurrentLap);
            int nextCheckpoint = tracker.NextCheckpointIndex;
            int completedThisLap = nextCheckpoint <= 0
                ? totalCheckpoints - 1
                : Mathf.Clamp(nextCheckpoint - 1, 0, totalCheckpoints - 1);

            return (currentLap - 1) * totalCheckpoints + completedThisLap;
        }

        private void FillRow(RectTransform row, int position, Ranked entry)
        {
            Image bg = row.GetComponent<Image>();
            if (bg != null)
                bg.color = entry.IsLocal ? localRowColor : rowColor;

            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            if (label == null)
                return;

            string name = ResolveName(entry, position);
            string status = ResolveStatus(entry);
            string best = entry.Tracker != null && entry.Tracker.BestLapTime > 0f
                ? $"  <size=70%>Melhor {FormatTime(entry.Tracker.BestLapTime)}</size>"
                : string.Empty;

            label.text = $"<b>{position}.</b>  {name}    <align=right>{status}{best}";
            label.color = entry.IsLocal ? accentColor : Color.white;
        }

        private string ResolveStatus(Ranked entry)
        {
            if (entry.Tracker == null)
                return "—";

            if (entry.Finished)
                return $"<b>{FormatTime(entry.Tracker.TotalRaceTime)}</b>";

            return $"<color=#9AA6FF>CORRENDO V{Mathf.Max(1, entry.Tracker.CurrentLap)}/{entry.Tracker.TotalLaps}</color>";
        }

        private string ResolveName(Ranked entry, int position)
        {
            if (entry.IsLocal)
                return "VOCÊ";

            var identity = entry.Kart != null ? entry.Kart.GetComponent<PartyRacers.Networking.KartNetworkIdentity>() : null;
            if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName) && identity.DisplayName != "Player")
                return identity.DisplayName;

            return FallbackNames[Mathf.Abs(position - 1) % FallbackNames.Length];
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f)
                return "--:--";

            int minutes = Mathf.FloorToInt(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes:00}:{rest:00.000}";
        }

        // ----------------------------------------------------------------- Ações

        private void WireButtons()
        {
            if (rematchButton != null)
            {
                rematchButton.onClick.RemoveListener(Rematch);
                rematchButton.onClick.AddListener(Rematch);
            }

            if (garageButton != null)
            {
                garageButton.onClick.RemoveListener(GoToGarage);
                garageButton.onClick.AddListener(GoToGarage);
            }
        }

        public void Rematch()
        {
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex);
            else
                SceneManager.LoadScene(active.name);
        }

        public void GoToGarage()
        {
            Time.timeScale = 1f;

            if (string.IsNullOrWhiteSpace(garageSceneName))
            {
                Debug.LogWarning("[RaceFinishScreen] 'garageSceneName' vazio — configure no Inspector.");
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(garageSceneName))
                SceneManager.LoadScene(garageSceneName);
            else
                Debug.LogWarning($"[RaceFinishScreen] Cena '{garageSceneName}' não está no Build Settings.");
        }

        // ----------------------------------------------------------------- Visibilidade

        private void HideImmediate()
        {
            if (root == null)
                return;

            root.alpha = 0f;
            root.interactable = false;
            root.blocksRaycasts = false;
        }

        private void OnDestroy()
        {
            if (subscribed && localTracker != null)
                localTracker.RaceJustFinished -= OnLocalRaceFinished;
        }

        // ----------------------------------------------------------------- Construção runtime da UI

        private void EnsureRowPool(int count)
        {
            if (rowTemplate == null || rowsContainer == null)
                return;

            while (rowPool.Count < count)
            {
                RectTransform clone = Instantiate(rowTemplate, rowsContainer);
                clone.gameObject.SetActive(true);
                clone.name = $"ResultRow_{rowPool.Count + 1}";
                rowPool.Add(clone);
            }
        }

        private void BuildRuntimeUI()
        {
            // Canvas dedicado por cima de tudo.
            GameObject canvasGo = new GameObject("RaceFinishCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root = canvasGo.GetComponent<CanvasGroup>();

            // Fundo escurecido.
            Image background = CreateChildImage(canvasGo.transform, "Background", backgroundColor);
            Stretch(background.rectTransform);

            // Painel central.
            Image panel = CreateChildImage(canvasGo.transform, "Panel", panelColor);
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(900f, 820f);

            // Título.
            titleLabel = CreateChildText(panelRect, "Title", "CORRIDA FINALIZADA", 56, accentColor, TextAlignmentOptions.Center);
            RectTransform titleRect = titleLabel.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -28f);
            titleRect.sizeDelta = new Vector2(-60f, 80f);

            // Container das linhas (com VerticalLayoutGroup).
            GameObject rowsGo = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowsGo.transform.SetParent(panelRect, false);
            rowsContainer = rowsGo.GetComponent<RectTransform>();
            rowsContainer.anchorMin = new Vector2(0f, 0f);
            rowsContainer.anchorMax = new Vector2(1f, 1f);
            rowsContainer.offsetMin = new Vector2(36f, 120f);
            rowsContainer.offsetMax = new Vector2(-36f, -120f);

            VerticalLayoutGroup layout = rowsGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            // Template de linha (desativado — clonado pelo pool).
            Image rowImg = CreateChildImage(rowsContainer, "RowTemplate", rowColor);
            rowTemplate = rowImg.rectTransform;
            rowTemplate.sizeDelta = new Vector2(0f, 44f);
            LayoutElement le = rowImg.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;
            TMP_Text rowLabel = CreateChildText(rowTemplate, "Label", "", 26, Color.white, TextAlignmentOptions.Left);
            Stretch(rowLabel.rectTransform, 18f, 0f);
            rowTemplate.gameObject.SetActive(false);

            // Botões.
            rematchButton = CreateButton(panelRect, "RematchButton", "REMATCH", accentColor,
                new Vector2(-150f, 50f));
            garageButton = CreateButton(panelRect, "GarageButton", "GARAGEM", new Color(0.4f, 0.45f, 0.6f, 1f),
                new Vector2(150f, 50f));
        }

        private Image CreateChildImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private TMP_Text CreateChildText(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.richText = true;
            return label;
        }

        private Button CreateButton(RectTransform parent, string name, string text, Color color, Vector2 anchoredPos)
        {
            Image img = CreateChildImage(parent, name, color);
            RectTransform rect = img.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(260f, 70f);
            rect.anchoredPosition = anchoredPos;

            Button button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            TMP_Text label = CreateChildText(rect, "Label", text, 30, Color.white, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);

            return button;
        }

        private static void Stretch(RectTransform rect, float padX = 0f, float padY = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padX, padY);
            rect.offsetMax = new Vector2(-padX, -padY);
        }

        private struct Ranked
        {
            public KartController Kart;
            public KartRaceTracker Tracker;
            public bool Finished;
            public float FinishTime;
            public float Progress;
            public bool IsLocal;
        }
    }
}
