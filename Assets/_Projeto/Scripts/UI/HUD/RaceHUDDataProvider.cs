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
            public Standing(KartController kart, int position, string displayName, bool isLocal,
                            float bestLapTime, float gapToAhead, bool gapKnown)
            {
                Kart = kart;
                Position = position;
                DisplayName = displayName;
                IsLocal = isLocal;
                BestLapTime = bestLapTime;
                GapToAhead = gapToAhead;
                GapKnown = gapKnown;
            }

            public KartController Kart { get; }
            public int Position { get; }
            public string DisplayName { get; }
            public bool IsLocal { get; }
            public float BestLapTime { get; }

            /// <summary>Segundos até o carro da FRENTE. 0 para o líder.</summary>
            public float GapToAhead { get; }

            /// <summary>Falso quando não dá para medir (pista sem traçado, kart parado).</summary>
            public bool GapKnown { get; }
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

        // ---- Vida e escudo (handoff v2 §5) --------------------------------------------------
        /// <summary>Componente de vida do kart local. Null em cenas antigas sem o sistema.</summary>
        public KartHealth LocalHealth { get; private set; }
        /// <summary>Habilidade de escudo do kart local. Null em cenas antigas sem o sistema.</summary>
        public KartShieldAbility LocalShield { get; private set; }

        public bool HasHealthSystem => LocalHealth != null;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; } = 100;
        public float Hp01 { get; private set; } = 1f;
        public bool IsBroken { get; private set; }
        public float BrokenRemaining { get; private set; }
        public float BrokenRemaining01 { get; private set; }
        public bool OnDamageCooldown { get; private set; }
        public float DamageCooldown01 { get; private set; }

        public bool HasShieldSystem => LocalShield != null;
        public bool ShieldActive { get; private set; }
        public bool ShieldReady { get; private set; }
        public float ShieldCooldown01 { get; private set; }
        public float ShieldCooldownRemaining { get; private set; }
        public float ShieldActiveRemaining { get; private set; }
        /// <summary>Fração do escudo ATIVO que ainda resta (1 = acabou de ligar, 0 = expirou).</summary>
        public float ShieldActive01 { get; private set; }

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
            LocalHealth = null;
            LocalShield = null;

            if (LocalKart != null)
            {
                localTracker = LocalKart.GetComponent<KartRaceTracker>();
                localInventory = LocalKart.GetComponent<KartPowerInventory>();
                localPowerUser = LocalKart.GetComponent<KartPowerUser>();
                LocalHealth = LocalKart.GetComponent<KartHealth>();
                LocalShield = LocalKart.GetComponent<KartShieldAbility>();
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

            ReadVitals();
        }

        private void ReadVitals()
        {
            if (LocalHealth != null)
            {
                Hp = LocalHealth.CurrentHp;
                MaxHp = LocalHealth.MaxHp;
                Hp01 = LocalHealth.Hp01;
                IsBroken = LocalHealth.IsBroken;
                BrokenRemaining = LocalHealth.BrokenRemaining;
                BrokenRemaining01 = LocalHealth.BrokenRemaining01;
                OnDamageCooldown = LocalHealth.OnDamageCooldown;
                DamageCooldown01 = LocalHealth.DamageCooldown01;
            }
            else
            {
                // Sem o sistema na cena a HUD mostra vida cheia em vez de zerada: uma barra vazia
                // faria o jogador achar que está prestes a quebrar numa pista que nem tem dano.
                Hp = MaxHp;
                Hp01 = 1f;
                IsBroken = false;
                BrokenRemaining = 0f;
                BrokenRemaining01 = 0f;
                OnDamageCooldown = false;
                DamageCooldown01 = 0f;
            }

            if (LocalShield != null)
            {
                ShieldActive = LocalShield.IsActive;
                ShieldReady = LocalShield.IsReady;
                ShieldCooldown01 = LocalShield.Cooldown01;
                ShieldCooldownRemaining = LocalShield.CooldownRemaining;
                ShieldActiveRemaining = LocalShield.ActiveRemaining;
                ShieldActive01 = LocalShield.ActiveRemaining01;
            }
            else
            {
                ShieldActive = false;
                ShieldReady = false;
                ShieldCooldown01 = 0f;
                ShieldCooldownRemaining = 0f;
                ShieldActiveRemaining = 0f;
            }
        }

        /// <summary>Aciona o escudo do kart local (botão do celular / atalho da HUD).</summary>
        public void RequestUseShield()
        {
            if (LocalShield != null)
                LocalShield.TryActivate();
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
                    Progress = RaceProgress.Measure(kart, tracker)
                });
            }

            ranked.Sort(CompareStandings);

            LocalPosition = ranked.Count > 0 ? ranked.Count : 1;
            RacerCount = ranked.Count;
            LocalGapAhead = 0f;
            LocalGapKnown = false;

            for (int i = 0; i < ranked.Count; i++)
            {
                RankedKart entry = ranked[i];
                int position = i + 1;
                bool isLocal = entry.Kart == LocalKart;
                if (isLocal)
                    LocalPosition = position;

                float bestLap = entry.Tracker != null ? entry.Tracker.BestLapTime : -1f;
                float intervalo = 0f;
                bool temIntervalo = i > 0 && MedirIntervalo(ranked[i - 1], entry, out intervalo);

                if (isLocal)
                {
                    LocalGapAhead = intervalo;
                    LocalGapKnown = temIntervalo;
                }

                standings.Add(new Standing(entry.Kart, position,
                                           ResolveDisplayName(entry.Kart, position), isLocal,
                                           bestLap, intervalo, temIntervalo));
            }

            // Garante ao menos o kart local na lista (cena single antes de registrar na corrida).
            if (standings.Count == 0 && LocalKart != null)
            {
                LocalPosition = 1;
                RacerCount = 1;
                standings.Add(new Standing(LocalKart, 1, ResolveDisplayName(LocalKart, 1), true,
                                           BestLapTime, 0f, false));
            }

            LimparIntervalosVelhos();
        }

        /// <summary>Segundos até o carro da frente. 0 quando o jogador lidera.</summary>
        public float LocalGapAhead { get; private set; }

        /// <summary>Falso quando o intervalo não pode ser medido — a HUD mostra "--" e não zero.</summary>
        public bool LocalGapKnown { get; private set; }

        /// <summary>
        /// Intervalo, em segundos, entre dois karts consecutivos — a coluna "Interval" da F1.
        ///
        /// A conta é a distância que os separa PELA PISTA dividida pela velocidade de quem vem
        /// atrás: é assim que o número responde ao que o jogador vê, encolhendo quando ele
        /// aproxima e crescendo quando perde terreno. Distância em linha reta não serve — numa
        /// curva fechada dois karts a 200 m de pista ficam a 30 m um do outro.
        ///
        /// O valor é SUAVIZADO. A projeção do kart sobre o traçado oscila alguns centímetros por
        /// frame, e sem filtro o último dígito tremia sem parar, que é o tipo de ruído que faz o
        /// jogador parar de olhar para o número.
        /// </summary>
        private bool MedirIntervalo(RankedKart frente, RankedKart tras, out float segundos)
        {
            segundos = 0f;

            if (!RaceProgress.TryMeasureMeters(frente.Kart, frente.Tracker, out float metrosFrente)
                || !RaceProgress.TryMeasureMeters(tras.Kart, tras.Tracker, out float metrosTras))
                return false;

            float distancia = metrosFrente - metrosTras;
            if (distancia < 0f)
                return false;

            // Piso de velocidade: parado, a conta iria ao infinito e o mostrador viraria lixo. A
            // 25 km/h o intervalo já fica grande o bastante para comunicar "muito longe".
            float velocidade = Mathf.Max(tras.Kart.SpeedKmh / 3.6f, 7f);
            float bruto = distancia / velocidade;

            int chave = tras.Kart.GetInstanceID();
            float anterior = intervalos.TryGetValue(chave, out Intervalo guardado)
                             && guardado.Frame >= Time.frameCount - 4
                ? guardado.Valor
                : bruto;

            segundos = Mathf.Lerp(anterior, bruto, 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime));
            intervalos[chave] = new Intervalo { Valor = segundos, Frame = Time.frameCount };
            return true;
        }

        private struct Intervalo
        {
            public float Valor;
            public int Frame;
        }

        private readonly Dictionary<int, Intervalo> intervalos = new Dictionary<int, Intervalo>();
        private readonly List<int> intervalosParaTirar = new List<int>();

        private void LimparIntervalosVelhos()
        {
            if (intervalos.Count <= ranked.Count + 8)
                return;

            intervalosParaTirar.Clear();
            foreach (KeyValuePair<int, Intervalo> par in intervalos)
                if (par.Value.Frame < Time.frameCount - 120)
                    intervalosParaTirar.Add(par.Key);

            foreach (int chave in intervalosParaTirar)
                intervalos.Remove(chave);
        }

        /// <summary>
        /// Ordena a classificação. A regra vive em <see cref="RaceProgress"/> para que a HUD, a
        /// tela de resultado e a mira do disco voador não possam discordar entre si.
        /// </summary>
        private static int CompareStandings(RankedKart a, RankedKart b)
        {
            int byProgress = RaceProgress.Compare(a.Progress, b.Progress);
            if (byProgress != 0)
                return byProgress;

            int aId = a.Kart != null ? a.Kart.GetInstanceID() : int.MaxValue;
            int bId = b.Kart != null ? b.Kart.GetInstanceID() : int.MaxValue;
            return aId.CompareTo(bId);
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
            public RaceProgress.Sample Progress;
        }
    }
}
