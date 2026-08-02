using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Camada de dados/adaptação da HUD. Resolve o kart do jogador LOCAL (local e online),
    /// coleta todos os corredores da corrida e produz um snapshot por frame que os widgets
    /// consomem. Nenhum widget fala direto com os scripts de carro — tudo passa por aqui.
    /// Substitui a coleta de dados que antes vivia em HUDRootUI + KartHUDOverlay.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceHUDDataProvider : MonoBehaviour
    {
        public readonly struct Standing
        {
            public Standing(KartController kart, int position, string displayName, bool isLocal, float bestLapTime)
            {
                Kart = kart;
                Position = position;
                DisplayName = displayName;
                IsLocal = isLocal;
                BestLapTime = bestLapTime;
            }

            public KartController Kart { get; }
            public int Position { get; }
            public string DisplayName { get; }
            public bool IsLocal { get; }
            public float BestLapTime { get; }
        }

        [Header("Contexto de cena")]
        [Tooltip("Cena de lobby/garagem onde a HUD de corrida NÃO deve aparecer.")]
        [SerializeField] private string lobbySceneName = "Garage";

        [Header("Efeito de turbo do kart local")]
        [Tooltip("Prefab de vento/turbo aplicado ao kart local (KartTurboScreenEffect).")]
        [SerializeField] private GameObject screenWindPrefab;

        // ---- Snapshot (lido pelos widgets) -------------------------------------------------
        public KartController LocalKart { get; private set; }
        public bool HasLocalKart => LocalKart != null;
        public bool InLobby { get; private set; }

        public float SpeedKmh { get; private set; }
        public float Speed01 { get; private set; }
        public bool IsBoosting { get; private set; }
        public float Boost01 { get; private set; }

        public int CurrentLap { get; private set; } = 1;
        public int TotalLaps { get; private set; } = 3;
        public bool RaceFinished { get; private set; }
        public float CurrentLapTime { get; private set; }
        public float LastLapTime { get; private set; } = -1f;
        public float BestLapTime { get; private set; } = -1f;

        public KartPowerType CurrentPower { get; private set; } = KartPowerType.None;
        public bool HasPower { get; private set; }
        public string PowerName { get; private set; } = string.Empty;

        public int LocalPosition { get; private set; } = 1;
        public int RacerCount { get; private set; }
        public IReadOnlyList<Standing> Standings => standings;

        // ---- Estado interno ----------------------------------------------------------------
        private KartRaceTracker localTracker;
        private KartPowerInventory localInventory;
        private KartPowerUser localPowerUser;
        private RaceManager raceManager;
        private RaceCheckpoint[] checkpoints;

        private readonly List<KartController> discoveredKarts = new List<KartController>();
        private readonly List<RankedKart> ranked = new List<RankedKart>();
        private readonly List<Standing> standings = new List<Standing>();

        private int lastRefreshFrame = -1;

        /// <summary>Recalcula o snapshot. Idempotente por frame — pode ser chamado por vários widgets.</summary>
        public void Refresh()
        {
            if (lastRefreshFrame == Time.frameCount)
                return;
            lastRefreshFrame = Time.frameCount;

            InLobby = !string.IsNullOrEmpty(lobbySceneName)
                      && gameObject.scene.IsValid()
                      && gameObject.scene.name == lobbySceneName;

            ResolveLocalKart();
            EnsureRaceContext();
            ReadLocalKart();
            BuildStandings();
        }

        private void Update() => Refresh();

        /// <summary>Tenta usar o poder atual do kart local (chamado pelo botão da HUD).</summary>
        public void RequestUsePower()
        {
            if (localPowerUser != null)
                localPowerUser.TryUseCurrentPower();
        }

        // ----------------------------------------------------------------- Local kart

        private void ResolveLocalKart()
        {
            if (LocalKart != null && IsLocal(LocalKart) && LocalKart.gameObject.activeInHierarchy)
                return;

            KartController resolved = FindLocalKart();
            if (resolved == LocalKart)
                return;

            LocalKart = resolved;
            localTracker = null;
            localInventory = null;
            localPowerUser = null;

            if (LocalKart != null)
            {
                localTracker = LocalKart.GetComponent<KartRaceTracker>();
                localInventory = LocalKart.GetComponent<KartPowerInventory>();
                localPowerUser = LocalKart.GetComponent<KartPowerUser>();
                EnsureTurboEffect(LocalKart);
            }
        }

        private static bool IsLocal(KartController kart)
        {
            if (kart == null)
                return false;

            KartLocalRig rig = kart.GetComponent<KartLocalRig>();
            return rig == null || rig.IsLocalPlayer;
        }

        // Online: dono (KartLocalRig.IsLocalPlayer == true). Offline: primeiro kart sem rig.
        private static KartController FindLocalKart()
        {
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

        private void EnsureTurboEffect(KartController kart)
        {
            if (kart == null || screenWindPrefab == null)
                return;

            KartTurboScreenEffect effect = kart.GetComponent<KartTurboScreenEffect>();
            if (effect == null)
                effect = kart.gameObject.AddComponent<KartTurboScreenEffect>();

            effect.SetScreenWindPrefab(screenWindPrefab);
        }

        // ----------------------------------------------------------------- Read snapshot

        private void EnsureRaceContext()
        {
            if (raceManager == null)
                raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);

            if (checkpoints == null || checkpoints.Length == 0)
                checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
        }

        private void ReadLocalKart()
        {
            SpeedKmh = LocalKart != null ? LocalKart.SpeedKmh : 0f;
            Speed01 = LocalKart != null ? LocalKart.Speed01 : 0f;
            IsBoosting = LocalKart != null && LocalKart.IsBoosting;
            Boost01 = LocalKart != null ? LocalKart.BoostRemaining01 : 0f;

            if (localTracker != null)
            {
                CurrentLap = localTracker.CurrentLap;
                TotalLaps = localTracker.TotalLaps;
                RaceFinished = localTracker.RaceFinished;
                CurrentLapTime = localTracker.CurrentLapTime;
                LastLapTime = localTracker.LastLapTime;
                BestLapTime = localTracker.BestLapTime;
            }
            else
            {
                CurrentLap = 1;
                TotalLaps = 3;
                RaceFinished = false;
                CurrentLapTime = 0f;
                LastLapTime = -1f;
                BestLapTime = -1f;
            }

            if (localInventory != null)
            {
                CurrentPower = localInventory.CurrentPower;
                HasPower = localInventory.HasPower;
                PowerName = localInventory.GetPowerDisplayName();
            }
            else
            {
                CurrentPower = KartPowerType.None;
                HasPower = false;
                PowerName = string.Empty;
            }
        }

        // ----------------------------------------------------------------- Standings

        private IReadOnlyList<KartController> GetRaceKarts()
        {
            if (raceManager != null && raceManager.Karts != null && raceManager.Karts.Count > 0)
                return raceManager.Karts;

            discoveredKarts.Clear();
            KartController[] found = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
            foreach (KartController kart in found)
            {
                if (kart != null)
                    discoveredKarts.Add(kart);
            }

            return discoveredKarts;
        }

        private void BuildStandings()
        {
            ranked.Clear();
            standings.Clear();

            IReadOnlyList<KartController> karts = GetRaceKarts();
            for (int i = 0; i < karts.Count; i++)
            {
                KartController kart = karts[i];
                if (kart == null || !kart.gameObject.activeInHierarchy)
                    continue;

                KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
                ranked.Add(new RankedKart
                {
                    Kart = kart,
                    Tracker = tracker,
                    Progress = CalculateProgress(tracker),
                    Tiebreak = CalculateTiebreak(kart, tracker)
                });
            }

            ranked.Sort(CompareStandings);

            LocalPosition = ranked.Count > 0 ? ranked.Count : 1;
            RacerCount = ranked.Count;

            for (int i = 0; i < ranked.Count; i++)
            {
                RankedKart entry = ranked[i];
                int position = i + 1;
                bool isLocal = entry.Kart == LocalKart;
                if (isLocal)
                    LocalPosition = position;

                float bestLap = entry.Tracker != null ? entry.Tracker.BestLapTime : -1f;
                standings.Add(new Standing(entry.Kart, position, ResolveDisplayName(entry.Kart, position), isLocal, bestLap));
            }

            // Garante ao menos o kart local na lista (cena single antes de registrar na corrida).
            if (standings.Count == 0 && LocalKart != null)
            {
                LocalPosition = 1;
                RacerCount = 1;
                standings.Add(new Standing(LocalKart, 1, ResolveDisplayName(LocalKart, 1), true, BestLapTime));
            }
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

        private float CalculateTiebreak(KartController kart, KartRaceTracker tracker)
        {
            if (kart == null)
                return 0f;

            RaceCheckpoint next = tracker != null ? FindCheckpoint(tracker.NextCheckpointIndex) : null;
            if (next != null)
                return -Vector3.Distance(kart.transform.position, next.transform.position);

            return kart.SpeedKmh * 0.01f;
        }

        /// <summary>
        /// A classificacao final e definida pela passagem na linha de chegada. O tempo total e a
        /// melhor volta sao estatisticas e nunca podem reordenar quem ja terminou. Enquanto ainda
        /// estao correndo, progresso e distancia ao proximo checkpoint continuam sendo usados.
        /// </summary>
        private static int CompareStandings(RankedKart a, RankedKart b)
        {
            bool aFinished = a.Tracker != null && a.Tracker.RaceFinished;
            bool bFinished = b.Tracker != null && b.Tracker.RaceFinished;

            if (aFinished != bFinished)
                return aFinished ? -1 : 1;

            if (aFinished)
            {
                float aFinish = a.Tracker.FinishRealtime >= 0f
                    ? a.Tracker.FinishRealtime
                    : float.PositiveInfinity;
                float bFinish = b.Tracker.FinishRealtime >= 0f
                    ? b.Tracker.FinishRealtime
                    : float.PositiveInfinity;

                int byArrival = aFinish.CompareTo(bFinish);
                if (byArrival != 0)
                    return byArrival;
            }
            else
            {
                int byProgress = b.Progress.CompareTo(a.Progress);
                if (byProgress != 0)
                    return byProgress;

                int byTrackPosition = b.Tiebreak.CompareTo(a.Tiebreak);
                if (byTrackPosition != 0)
                    return byTrackPosition;
            }

            int aId = a.Kart != null ? a.Kart.GetInstanceID() : int.MaxValue;
            int bId = b.Kart != null ? b.Kart.GetInstanceID() : int.MaxValue;
            return aId.CompareTo(bId);
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

        private string ResolveDisplayName(KartController kart, int position)
        {
            KartNetworkIdentity identity = kart != null ? kart.GetComponent<KartNetworkIdentity>() : null;
            if (identity != null && !string.IsNullOrWhiteSpace(identity.DisplayName) && identity.DisplayName != "Player")
                return identity.DisplayName;

            if (kart == LocalKart)
            {
                RacePlayerInfo local = RacePlayerRegistry.Instance != null
                    ? RacePlayerRegistry.Instance.LocalPlayer
                    : null;
                if (local != null && !string.IsNullOrWhiteSpace(local.DisplayName))
                    return local.DisplayName;

                return "VOCÊ";
            }

            // Em cenas offline antigas pode não existir KartNetworkIdentity. Nesse caso usamos
            // a identidade do próprio objeto em vez de inventar nomes de jogadores.
            if (kart != null)
            {
                string objectName = kart.gameObject.name.Replace("(Clone)", string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(objectName) && objectName != "PlayerKart")
                    return objectName;
            }

            return $"JOGADOR {position}";
        }

        private struct RankedKart
        {
            public KartController Kart;
            public KartRaceTracker Tracker;
            public float Progress;
            public float Tiebreak;
        }
    }
}
