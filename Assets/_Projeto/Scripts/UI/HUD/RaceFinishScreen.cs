using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Fluxo de fim de corrida para o PLAYER local. Vive FISICAMENTE dentro do prefab RaceHUD
    /// (painel editável: título, placas, textos, botões, ícones). Quando o player cruza a última
    /// linha de chegada:
    ///  - remove o controle manual e engata o piloto automático (KartFinishAutopilot);
    ///  - exibe o painel de resultados (CanvasGroup) com a classificação;
    ///  - a classificação continua atualizando enquanto os retardatários terminam;
    ///  - mostra o tempo total de quem cruzou e a melhor volta de cada corredor;
    ///  - oferece Rematch (reinicia a cena) e Voltar para Garagem (cena configurável).
    /// Toda a UI é referência serializada — nada é montado em runtime.
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
        [Tooltip("Máximo de linhas (placas) mostradas na classificação.")]
        [SerializeField] private int maxRows = 12;
        [Tooltip("Duração (s) da animação de entrada do painel.")]
        [SerializeField] private float fadeInDuration = 0.4f;

        [Header("UI (referências físicas no prefab)")]
        [SerializeField] private CanvasGroup root;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private RectTransform rowsContainer;
        [SerializeField] private RaceFinishRowUI rowTemplate;
        [SerializeField] private Button rematchButton;
        [SerializeField] private Button garageButton;

        [Header("Textos")]
        [SerializeField] private string titleText = "CORRIDA FINALIZADA";

        private readonly List<RaceFinishRowUI> rowPool = new List<RaceFinishRowUI>();
        private RaceHUDDataProvider provider;
        private KartController localKart;
        private KartRaceTracker localTracker;

        private bool shown;
        private bool subscribed;
        private float fadeTimer;

        private static readonly string[] FallbackNames =
        {
            "ALEX", "LUCAS", "BRUNO", "MARIA", "JOAO", "ENZO", "FELIPE", "NINA",
            "TURBO", "NITRO", "BLAZE", "RAIO", "FURIA", "TROVAO", "FOGUETE", "VENTO"
        };

        private void Awake()
        {
            provider = GetComponentInParent<RaceHUDDataProvider>();
            if (provider == null)
                provider = FindAnyObjectByType<RaceHUDDataProvider>(FindObjectsInactive.Exclude);

            if (titleLabel != null && !string.IsNullOrEmpty(titleText))
                titleLabel.text = titleText;

            if (rowTemplate != null)
                rowTemplate.SetVisible(false);

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

            if (fadeTimer < fadeInDuration && root != null)
            {
                fadeTimer += Time.unscaledDeltaTime;
                root.alpha = Mathf.Clamp01(fadeTimer / Mathf.Max(0.01f, fadeInDuration));
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
            fadeTimer = 0f;

            if (engageAutopilotOnFinish && localKart != null)
                KartFinishAutopilot.Engage(localKart);

            if (showCursorOnFinish)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (root != null)
            {
                root.gameObject.SetActive(true);
                root.alpha = 0f;
                root.interactable = true;
                root.blocksRaycasts = true;
            }

            RefreshStandings();
        }

        private void RefreshStandings()
        {
            if (rowTemplate == null || rowsContainer == null)
                return;

            List<Ranked> ranked = BuildRanked();
            int rows = Mathf.Min(ranked.Count, maxRows);
            EnsureRowPool(rows);

            for (int i = 0; i < rowPool.Count; i++)
            {
                bool visible = i < rows;
                rowPool[i].SetVisible(visible);

                if (!visible)
                    continue;

                Ranked entry = ranked[i];
                int position = i + 1;
                rowPool[i].Bind(
                    position,
                    ResolveName(entry, position),
                    ResolveStatus(entry),
                    entry.Finished,
                    ResolveBestLap(entry),
                    entry.IsLocal);
            }

            if (subtitleLabel != null && localTracker != null)
                subtitleLabel.text = $"Seu tempo: <b>{FormatTime(localTracker.TotalRaceTime)}</b>";
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

        private string ResolveStatus(Ranked entry)
        {
            if (entry.Tracker == null)
                return "—";

            if (entry.Finished)
                return FormatTime(entry.Tracker.TotalRaceTime);

            return $"CORRENDO · V{Mathf.Max(1, entry.Tracker.CurrentLap)}/{entry.Tracker.TotalLaps}";
        }

        private string ResolveBestLap(Ranked entry)
        {
            if (entry.Tracker == null || entry.Tracker.BestLapTime <= 0f)
                return string.Empty;

            return FormatTime(entry.Tracker.BestLapTime);
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
            root.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (subscribed && localTracker != null)
                localTracker.RaceJustFinished -= OnLocalRaceFinished;
        }

        private void EnsureRowPool(int count)
        {
            while (rowPool.Count < count)
            {
                RaceFinishRowUI clone = Instantiate(rowTemplate, rowsContainer);
                clone.gameObject.SetActive(true);
                clone.name = $"ResultRow_{rowPool.Count + 1}";
                rowPool.Add(clone);
            }
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
