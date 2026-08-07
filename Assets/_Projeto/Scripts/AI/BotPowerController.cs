using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    [DisallowMultipleComponent]
    public class BotPowerController : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField] private float minUseDelay = 0.65f;
        [SerializeField] private float maxUseDelay = 1.85f;
        [SerializeField] private float powerCheckInterval = 0.15f;
        [SerializeField] private float launchPowerDelay = 2.5f;

        [Header("Shield")]
        [SerializeField] private float shieldThreatRadius = 9f;
        [SerializeField] private float shieldMaxHoldSeconds = 4f;
        [Tooltip("Fração de vida abaixo da qual um adversário colado já conta como ameaça.")]
        [SerializeField, Range(0f, 1f)] private float shieldLowHpThreshold = 0.35f;

        [Tooltip("Distância em que um projétil vindo na direção do bot conta como ameaça.")]
        [SerializeField, Min(1f)] private float raioDeProjetil = 26f;

        [Tooltip("Antecedência, em segundos, com que o bot levanta o escudo antes de um obstáculo. " +
                 "Muito alto e ele escuda a pista inteira; muito baixo e o escudo sobe depois da " +
                 "pancada.")]
        [SerializeField, Range(0.1f, 2f)] private float segundosDeAntecedencia = 0.6f;

        [Header("Rocket")]
        [SerializeField] private float rocketMinDistance = 7f;
        [SerializeField] private float rocketMaxDistance = 60f;
        [SerializeField] private float rocketMaxLateralOffset = 12f;
        [SerializeField] private float rocketMaxAngle = 32f;

        [Header("Swap")]
        [SerializeField] private float swapMaxHoldSeconds = 3f;

        [Header("Armadilha Elétrica")]
        [Tooltip("Distância máxima atrás do bot em que um adversário torna a soltura útil.")]
        [SerializeField] private float trapRearDetectionDistance = 28f;
        [SerializeField] private float trapMaxLateralOffset = 10f;
        [SerializeField] private float trapMinRearDistance = 2.5f;
        [SerializeField] private float trapMaxHoldSeconds = 4f;

        private KartController kart;
        private KartPowerInventory inventory;
        private KartPowerUser powerUser;
        private KartShieldAbility shieldAbility;
        private KartHealth health;
        private RaceManager raceManager;
        private KartRaceTracker tracker;
        private KartPowerType observedPower = KartPowerType.None;
        private KartPowerType lastUsedPower = KartPowerType.None;
        private float acquiredPowerTime;
        private float nextUseTime;
        private float nextCheckTime;
        private float controlEnabledTime = float.NegativeInfinity;
        private bool wasControlEnabled;
        private int seed;

        public int UseCount { get; private set; }
        public KartPowerType LastUsedPower => lastUsedPower;

        public void Initialize(KartController kartController, int botSeed)
        {
            kart = kartController;
            seed = botSeed;
            ResolveReferences();
            ResetObservedPower();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (Time.time < nextCheckTime)
                return;

            nextCheckTime = Time.time + powerCheckInterval;
            ResolveReferences();

            if (kart == null || inventory == null || powerUser == null)
                return;

            if (!kart.CanControl)
            {
                wasControlEnabled = false;
                return;
            }

            if (!wasControlEnabled)
            {
                wasControlEnabled = true;
                controlEnabledTime = Time.time;
            }

            if (Time.time - controlEnabledTime < launchPowerDelay)
                return;

            // O escudo não vem mais da caixa: é habilidade fixa e recarrega sozinha, então o bot
            // decide usá-la à parte do inventário — senão ele só se defenderia quando por acaso
            // estivesse sem item na mão.
            TryUseShieldAbility();

            if (!inventory.HasPower)
            {
                ResetObservedPower();
                return;
            }

            KartPowerType currentPower = inventory.CurrentPower;
            if (currentPower != observedPower)
                ObservePower(currentPower);

            if (Time.time < nextUseTime)
                return;

            if (!ShouldUsePower(currentPower))
                return;

            if (powerUser.TryUseCurrentPower())
            {
                UseCount++;
                lastUsedPower = currentPower;
                ResetObservedPower();
            }
        }

        /// <summary>
        /// Levanta o escudo quando há AMEAÇA, não quando ele está pronto.
        ///
        /// A regra antiga era "tem alguém a 9 metros" — com 16 karts na pista isso é verdade quase
        /// o tempo todo, e o resultado era a grade inteira correndo permanentemente escudada. Um
        /// escudo que está sempre ligado não defende de nada: ele só deixa de ser um recurso.
        ///
        /// As três ameaças que valem gastar a recarga, em ordem de urgência:
        ///  • projétil vindo na sua direção (foguete, disco) — é o que o escudo existe para parar;
        ///  • obstáculo do cenário logo à frente, agora que o escudo também protege dele;
        ///  • adversário colado ATRÁS com a vida já baixa, que é quando uma pancada quebra o carro.
        /// </summary>
        private void TryUseShieldAbility()
        {
            if (shieldAbility == null || !shieldAbility.IsReady)
                return;

            if (Time.time < proximaAvaliacaoDeEscudo)
                return;

            proximaAvaliacaoDeEscudo = Time.time + 0.2f;

            if (ProjetilVindo() || ObstaculoIminente() || PressionadoAtras())
                shieldAbility.TryActivate();
        }

        private float proximaAvaliacaoDeEscudo;

        /// <summary>Projétil de outro jogador se aproximando — a ameaça mais cara de ignorar.</summary>
        private bool ProjetilVindo()
        {
            Vector3 minha = transform.position;

            foreach (Rigidbody corpo in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
            {
                if (corpo == null || corpo.gameObject == gameObject)
                    continue;

                if (corpo.GetComponent<RocketProjectile>() == null
                    && corpo.GetComponent<UFOSwapProjectile>() == null)
                    continue;

                Vector3 ateMim = minha - corpo.position;
                float distancia = ateMim.magnitude;

                if (distancia > raioDeProjetil || distancia < 0.5f)
                    continue;

                // Só conta o que vem NA MINHA direção: um foguete passando de raspão para outro
                // alvo não justifica queimar a recarga.
                Vector3 v = corpo.linearVelocity;
                if (v.sqrMagnitude < 1f || Vector3.Dot(v.normalized, ateMim.normalized) < 0.6f)
                    continue;

                return true;
            }

            return false;
        }

        /// <summary>Obstáculo do cenário a poucos décimos de segundo à frente.</summary>
        private bool ObstaculoIminente()
        {
            if (kart == null)
                return false;

            float velocidade = Mathf.Max(6f, kart.SpeedKmh / 3.6f);
            float alcance = velocidade * segundosDeAntecedencia;

            if (!Physics.SphereCast(transform.position + Vector3.up * 0.5f, 1.2f, transform.forward,
                                    out RaycastHit hit, alcance, ~0, QueryTriggerInteraction.Collide))
                return false;

            return hit.collider.GetComponentInParent<IKartImpactObstacle>() != null
                || hit.collider.GetComponentInParent<AutoGolfSwing>() != null;
        }

        /// <summary>Adversário colado atrás com a vida já baixa: a próxima pancada quebra.</summary>
        private bool PressionadoAtras()
        {
            if (health == null || health.IsBroken || health.Hp01 > shieldLowHpThreshold)
                return false;

            return HasNearbyOpponent(shieldThreatRadius);
        }

        private void ResolveReferences()
        {
            if (shieldAbility == null)
                shieldAbility = GetComponent<KartShieldAbility>();

            if (health == null)
                health = GetComponent<KartHealth>();

            if (kart == null)
                kart = GetComponent<KartController>();

            if (inventory == null)
                inventory = GetComponent<KartPowerInventory>();

            if (powerUser == null)
                powerUser = GetComponent<KartPowerUser>();

            if (tracker == null && kart != null)
                tracker = kart.GetComponent<KartRaceTracker>();

            if (raceManager == null)
                raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);
        }

        private void ObservePower(KartPowerType power)
        {
            observedPower = power;
            acquiredPowerTime = Time.time;
            nextUseTime = Time.time + RandomizedDelay();
        }

        private void ResetObservedPower()
        {
            observedPower = inventory != null ? inventory.CurrentPower : KartPowerType.None;
            acquiredPowerTime = Time.time;
            nextUseTime = Time.time + RandomizedDelay();
        }

        private float RandomizedDelay()
        {
            float t = Mathf.Abs(Mathf.Sin((seed * 0.001f + UseCount * 37f + 17.31f) * 12.9898f));
            return Mathf.Lerp(minUseDelay, maxUseDelay, t);
        }

        private bool ShouldUsePower(KartPowerType power)
        {
            switch (power)
            {
                case KartPowerType.Shield:
                    return HasNearbyOpponent(shieldThreatRadius)
                        || Time.time - acquiredPowerTime >= shieldMaxHoldSeconds;

                case KartPowerType.Rocket:
                    return HasRocketShot();

                case KartPowerType.SwapPosition:
                    return HasOpponentAheadByProgress()
                        || Time.time - acquiredPowerTime >= swapMaxHoldSeconds;

                case KartPowerType.ElectricTrap:
                    return HasUsefulTrapDrop()
                        || Time.time - acquiredPowerTime >= trapMaxHoldSeconds;

                default:
                    return false;
            }
        }

        private bool HasNearbyOpponent(float radius)
        {
            IReadOnlyList<KartController> karts = GetRaceKarts();
            if (karts == null)
                return false;

            float radiusSqr = radius * radius;
            Vector3 origin = transform.position;
            for (int i = 0; i < karts.Count; i++)
            {
                KartController other = karts[i];
                if (!IsValidOpponent(other))
                    continue;

                if (PlanarSqrDistance(origin, other.transform.position) <= radiusSqr)
                    return true;
            }

            return false;
        }

        private bool HasRocketShot()
        {
            IReadOnlyList<KartController> karts = GetRaceKarts();
            if (karts == null)
                return false;

            Vector3 origin = transform.position;
            for (int i = 0; i < karts.Count; i++)
            {
                KartController other = karts[i];
                if (!IsValidOpponent(other))
                    continue;

                Vector3 toOther = other.transform.position - origin;
                Vector3 local = transform.InverseTransformDirection(toOther);
                if (local.z < rocketMinDistance || local.z > rocketMaxDistance)
                    continue;

                if (Mathf.Abs(local.x) > rocketMaxLateralOffset)
                    continue;

                toOther.y = 0f;
                if (toOther.sqrMagnitude < 0.01f)
                    continue;

                float angle = Vector3.Angle(transform.forward, toOther.normalized);
                if (angle <= rocketMaxAngle)
                    return true;
            }

            return false;
        }

        private bool HasOpponentAheadByProgress()
        {
            IReadOnlyList<KartController> karts = GetRaceKarts();
            if (karts == null)
                return false;

            float ownProgress = CalculateRaceProgress(tracker);
            bool hasProgress = tracker != null && tracker.TotalCheckpoints > 0;

            for (int i = 0; i < karts.Count; i++)
            {
                KartController other = karts[i];
                if (!IsValidOpponent(other))
                    continue;

                if (hasProgress)
                {
                    KartRaceTracker otherTracker = other.GetComponent<KartRaceTracker>();
                    if (CalculateRaceProgress(otherTracker) > ownProgress + 0.1f)
                        return true;
                }
                else if (transform.InverseTransformPoint(other.transform.position).z > 3f)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasUsefulTrapDrop()
        {
            IReadOnlyList<KartController> karts = GetRaceKarts();
            if (karts == null)
                return false;

            Vector3 origin = transform.position;
            for (int i = 0; i < karts.Count; i++)
            {
                KartController other = karts[i];
                if (!IsValidOpponent(other))
                    continue;

                Vector3 local = transform.InverseTransformPoint(other.transform.position);
                float rearDistance = -local.z;
                if (rearDistance < trapMinRearDistance || rearDistance > trapRearDetectionDistance)
                    continue;

                if (Mathf.Abs(local.x) <= trapMaxLateralOffset)
                    return true;
            }

            return false;
        }

        private IReadOnlyList<KartController> GetRaceKarts()
        {
            if (raceManager != null && raceManager.Karts != null && raceManager.Karts.Count > 0)
                return raceManager.Karts;

            raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);
            if (raceManager != null && raceManager.Karts != null && raceManager.Karts.Count > 0)
                return raceManager.Karts;

            return FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
        }

        private bool IsValidOpponent(KartController other)
        {
            return other != null
                && other != kart
                && other.gameObject.activeInHierarchy;
        }

        private static float CalculateRaceProgress(KartRaceTracker raceTracker)
        {
            if (raceTracker == null)
                return 0f;

            int totalCheckpoints = Mathf.Max(1, raceTracker.TotalCheckpoints);
            if (raceTracker.RaceFinished)
                return raceTracker.TotalLaps * totalCheckpoints + totalCheckpoints;

            int nextCheckpoint = raceTracker.NextCheckpointIndex;
            int completedThisLap = nextCheckpoint <= 0
                ? totalCheckpoints - 1
                : Mathf.Clamp(nextCheckpoint - 1, 0, totalCheckpoints - 1);

            return (Mathf.Max(1, raceTracker.CurrentLap) - 1) * totalCheckpoints + completedThisLap;
        }

        private static float PlanarSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
