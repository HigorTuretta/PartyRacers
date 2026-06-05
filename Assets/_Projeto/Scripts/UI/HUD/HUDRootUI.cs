using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;

public class HUDRootUI : MonoBehaviour
{
    [Header("Widgets")]
    [SerializeField] private RacePositionUI racePosition;
    [SerializeField] private LeaderboardUI leaderboard;
    [SerializeField] private MinimapUI minimap;
    [SerializeField] private LapCounterUI lapCounter;
    [SerializeField] private PowerDisplayUI powerDisplay;
    [SerializeField] private SpeedometerUI speedometer;
    [SerializeField] private GearUI gear;
    [SerializeField] private NitroMeterUI nitroMeter;
    [SerializeField] private EventBannerUI eventBanner;

    [Header("Fontes de dados")]
    [SerializeField] private KartController kart;
    [SerializeField] private KartRaceTracker raceTracker;
    [SerializeField] private KartPowerInventory powerInventory;
    [SerializeField] private KartPowerUser powerUser;
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private RaceCheckpoint[] checkpoints;

    [Header("Efeitos")]
    [SerializeField] private GameObject screenWindPrefab;

    [Header("Configuracao")]
    [SerializeField] private float maxDisplaySpeedKmh = 200f;
    [SerializeField] private float minimapRefreshInterval = 0.08f;
    [Tooltip("Limites de velocidade (km/h) que separam cada marcha visual.")]
    [SerializeField] private float[] gearTopSpeedsKmh = { 35f, 70f, 110f, 150f, 200f };

    private readonly List<KartController> discoveredKarts = new List<KartController>();
    private readonly List<Standing> standings = new List<Standing>();
    private readonly List<LeaderboardUI.Entry> leaderboardEntries = new List<LeaderboardUI.Entry>();

    private bool powerEventWired;
    private bool wasBoosting;
    private float minimapTimer;

    private static readonly Color[] LeaderboardColors =
    {
        new Color(1f, 0.82f, 0.10f, 1f),
        new Color(0.16f, 0.70f, 1f, 1f),
        new Color(1f, 0.48f, 0.16f, 1f),
        new Color(0.62f, 0.24f, 1f, 1f),
        new Color(0.55f, 0.65f, 0.72f, 1f)
    };

    private readonly string[] fallbackNames =
    {
        "ALEX",
        "LUCAS",
        "BRUNO",
        "MARIA",
        "JOAO",
        "ENZO",
        "FELIPE",
        "NINA"
    };

    private void Awake() => WirePowerButton();

    private void OnEnable()
    {
        RaceHudEvents.Raised += OnRaceHudEvent;
        WirePowerButton();
    }

    private void OnDisable()
    {
        RaceHudEvents.Raised -= OnRaceHudEvent;
    }

    private void Start() => WirePowerButton();

    public void Bind(KartController boundKart)
    {
        if (kart == boundKart)
        {
            WirePowerButton();
            EnsureRaceContext();
            EnsureTurboEffect();
            return;
        }

        kart = boundKart;
        raceTracker = null;
        powerInventory = null;
        powerUser = null;
        wasBoosting = false;

        if (kart != null)
        {
            raceTracker = kart.GetComponent<KartRaceTracker>();
            powerInventory = kart.GetComponent<KartPowerInventory>();
            powerUser = kart.GetComponent<KartPowerUser>();
        }

        EnsureRaceContext();
        EnsureTurboEffect();
        WirePowerButton();
    }

    private void WirePowerButton()
    {
        if (powerEventWired || powerDisplay == null)
            return;

        powerDisplay.onUsePower.AddListener(OnUsePowerRequested);
        powerEventWired = true;
    }

    private void OnUsePowerRequested()
    {
        if (powerUser != null)
            powerUser.TryUseCurrentPower();
    }

    private void Update()
    {
        EnsureRaceContext();
        UpdateSpeedAndGear();
        UpdateLaps();
        UpdatePower();
        UpdateRaceOrder();
        UpdateMinimap();
        UpdateNitro();
    }

    private void EnsureRaceContext()
    {
        if (raceManager == null)
            raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);

        if (checkpoints == null || checkpoints.Length == 0)
            checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
    }

    private void EnsureTurboEffect()
    {
        if (kart == null)
            return;

        KartTurboScreenEffect effect = kart.GetComponent<KartTurboScreenEffect>();
        if (effect == null)
            effect = kart.gameObject.AddComponent<KartTurboScreenEffect>();

        if (screenWindPrefab != null)
            effect.SetScreenWindPrefab(screenWindPrefab);
    }

    private void UpdateSpeedAndGear()
    {
        float speedKmh = kart != null ? kart.SpeedKmh : 0f;

        speedometer?.SetSpeed(speedKmh, maxDisplaySpeedKmh);

        int gearCount = gearTopSpeedsKmh != null && gearTopSpeedsKmh.Length > 0 ? gearTopSpeedsKmh.Length : 5;
        gear?.SetGear(ComputeGear(speedKmh), gearCount);
    }

    private int ComputeGear(float speedKmh)
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

    private void UpdateLaps()
    {
        if (lapCounter == null)
            return;

        int currentLap = 1;
        int totalLaps = 3;
        bool finished = false;

        if (raceTracker != null)
        {
            currentLap = raceTracker.CurrentLap;
            totalLaps = raceTracker.TotalLaps;
            finished = raceTracker.RaceFinished;
        }

        if (finished)
            lapCounter.SetFinished(totalLaps);
        else
            lapCounter.SetLap(currentLap, totalLaps);
    }

    private void UpdatePower()
    {
        if (powerDisplay == null)
            return;

        if (powerInventory == null)
        {
            powerDisplay.ClearPower();
            return;
        }

        KartPowerType type = powerInventory.CurrentPower;
        powerDisplay.SetPower(type, powerInventory.GetPowerDisplayName(), powerInventory.HasPower);
    }

    private void UpdateRaceOrder()
    {
        IReadOnlyList<KartController> karts = GetRaceKarts();
        BuildStandings(karts);

        int localPosition = 1;
        leaderboardEntries.Clear();

        for (int i = 0; i < standings.Count; i++)
        {
            KartController currentKart = standings[i].Kart;
            bool isLocal = currentKart == kart;
            int position = i + 1;

            if (isLocal)
                localPosition = position;

            leaderboardEntries.Add(new LeaderboardUI.Entry
            {
                Position = position,
                DisplayName = ResolveDisplayName(currentKart, position),
                IsLocal = isLocal,
                AccentColor = GetPositionColor(position)
            });
        }

        if (standings.Count == 0 && kart != null)
        {
            leaderboardEntries.Add(new LeaderboardUI.Entry
            {
                Position = 1,
                DisplayName = ResolveDisplayName(kart, 1),
                IsLocal = true,
                AccentColor = GetPositionColor(1)
            });
        }

        racePosition?.SetPosition(localPosition);
        leaderboard?.SetEntries(leaderboardEntries);
    }

    private IReadOnlyList<KartController> GetRaceKarts()
    {
        if (raceManager != null && raceManager.Karts != null && raceManager.Karts.Count > 0)
            return raceManager.Karts;

        discoveredKarts.Clear();
        KartController[] found = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);

        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null)
                discoveredKarts.Add(found[i]);
        }

        return discoveredKarts;
    }

    private void BuildStandings(IReadOnlyList<KartController> karts)
    {
        standings.Clear();

        if (karts == null)
            return;

        for (int i = 0; i < karts.Count; i++)
        {
            KartController candidate = karts[i];
            if (candidate == null || !candidate.gameObject.activeInHierarchy)
                continue;

            KartRaceTracker tracker = candidate.GetComponent<KartRaceTracker>();
            standings.Add(new Standing
            {
                Kart = candidate,
                Progress = CalculateProgress(tracker),
                Tiebreak = CalculateTiebreak(candidate, tracker)
            });
        }

        standings.Sort((a, b) =>
        {
            int progressCompare = b.Progress.CompareTo(a.Progress);
            return progressCompare != 0 ? progressCompare : b.Tiebreak.CompareTo(a.Tiebreak);
        });
    }

    private float CalculateProgress(KartRaceTracker tracker)
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

    private float CalculateTiebreak(KartController candidate, KartRaceTracker tracker)
    {
        if (candidate == null)
            return 0f;

        RaceCheckpoint next = tracker != null ? FindCheckpoint(tracker.NextCheckpointIndex) : null;
        if (next != null)
            return -Vector3.Distance(candidate.transform.position, next.transform.position);

        return candidate.SpeedKmh * 0.01f;
    }

    private RaceCheckpoint FindCheckpoint(int index)
    {
        if (checkpoints == null)
            return null;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            RaceCheckpoint checkpoint = checkpoints[i];
            if (checkpoint != null && checkpoint.CheckpointIndex == index)
                return checkpoint;
        }

        return null;
    }

    private void UpdateMinimap()
    {
        if (minimap == null)
            return;

        minimapTimer -= Time.unscaledDeltaTime;
        if (minimapTimer > 0f)
            return;

        minimapTimer = Mathf.Max(0.02f, minimapRefreshInterval);
        minimap.SetRaceData(GetRaceKarts(), kart, checkpoints);
    }

    private void UpdateNitro()
    {
        bool boosting = kart != null && kart.IsBoosting;
        nitroMeter?.SetNitro(kart != null ? kart.BoostRemaining01 : 0f, boosting);

        if (boosting && !wasBoosting)
            eventBanner?.Show(RaceHudEventKind.Nitro);

        wasBoosting = boosting;
    }

    private void OnRaceHudEvent(RaceHudEvents.EventData data)
    {
        if (kart == null || data.Actor != kart.gameObject)
            return;

        eventBanner?.Show(data.Kind, data.PowerType);
    }

    private string ResolveDisplayName(KartController candidate, int position)
    {
        if (candidate == kart)
            return "VOC\u00ca";

        KartNetworkIdentity identity = candidate != null ? candidate.GetComponent<KartNetworkIdentity>() : null;
        if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName) && identity.DisplayName != "Player")
            return identity.DisplayName;

        int fallbackIndex = Mathf.Abs(position - 1) % fallbackNames.Length;
        return fallbackNames[fallbackIndex];
    }

    private static Color GetPositionColor(int position)
    {
        return LeaderboardColors[Mathf.Clamp(position - 1, 0, LeaderboardColors.Length - 1)];
    }

    private struct Standing
    {
        public KartController Kart;
        public float Progress;
        public float Tiebreak;
    }
}
