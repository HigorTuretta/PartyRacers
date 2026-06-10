using UnityEngine;
using UnityEngine.InputSystem;
using PartyRacers.Networking;

// Orchestrates kart powers. Player input is gated to the local kart; bots call
// TryUseCurrentPower directly through their AI controller.
public class KartPowerUser : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartPowerInventory inventory;
    [SerializeField] private KartController kart;
    [SerializeField] private KartShieldVisual shieldVisual;
    [SerializeField] private KartNetworkIdentity identity;
    [SerializeField] private KartLocalRig localRig;

    [Header("Escudo")]
    [SerializeField] private float shieldDuration = 4f;
    [SerializeField] private GameObject shieldBlockVFXPrefab;
    [SerializeField, Min(0.1f)] private float shieldBlockVFXFallbackLifetime = 1.5f;

    [Header("Foguete")]
    [SerializeField] private GameObject rocketProjectilePrefab;
    [Tooltip("Prefab do foguete equipado (idle). Se vazio, usa o mesmo prefab do projetil.")]
    [SerializeField] private GameObject rocketEquippedPrefab;
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private GameObject boingVFXPrefab;
    [Tooltip("Trail do foguete em voo (ex.: VFXRocketTrail). Passado ao projetil ao disparar.")]
    [SerializeField] private GameObject rocketTrailPrefab;
    [SerializeField] private Transform rocketEquippedSocket;
    [SerializeField] private Vector3 rocketSocketLocalPosition = new Vector3(0f, 1.15f, 0.15f);
    [Tooltip("Quao a frente do socket o projetil nasce ao disparar.")]
    [SerializeField] private float rocketLaunchForwardOffset = 0.6f;

    [Header("Disco Voador (Swap)")]
    [Tooltip("Prefab do projétil do disco voador (com UFOSwapProjectile).")]
    [SerializeField] private GameObject ufoProjectilePrefab;
    [Tooltip("Prefab do disco equipado (idle acima do carro). Se vazio, usa o prefab do projétil.")]
    [SerializeField] private GameObject ufoEquippedPrefab;
    [Tooltip("Círculo mágico exibido no chão ao redor dos dois carros antes da troca.")]
    [SerializeField] private GameObject ufoMagicCirclePrefab;
    [Tooltip("Efeito ativado dentro do disco quando ele não acerta ninguém (ex.: AoE slash blue).")]
    [SerializeField] private GameObject ufoMissVFXPrefab;
    [SerializeField] private Transform ufoEquippedSocket;
    [SerializeField] private Vector3 ufoSocketLocalPosition = Vector3.zero;
    [Tooltip("Quão à frente do socket o disco nasce ao disparar.")]
    [SerializeField] private float ufoLaunchForwardOffset = 1.2f;

    [Header("Input")]
    [SerializeField] private Key useKey = Key.E;

    private float shieldEndTime;
    private GameObject equippedRocketInstance;
    private GameObject equippedUfoInstance;

    public bool IsShieldActive => Time.time < shieldEndTime;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<KartPowerInventory>();

        if (kart == null)
            kart = GetComponent<KartController>();

        if (identity == null)
            identity = GetComponent<KartNetworkIdentity>();

        if (localRig == null)
            localRig = GetComponent<KartLocalRig>();

        if (shieldVisual == null)
            shieldVisual = GetComponent<KartShieldVisual>();

        if (shieldVisual == null)
            shieldVisual = gameObject.AddComponent<KartShieldVisual>();

        EnsureRocketSocket();
    }

    private void Update()
    {
        ReadInput();
        UpdateShield();
        UpdateEquippedRocket();
        UpdateEquippedUfo();
    }

    private void ReadInput()
    {
        if (!ShouldReadLocalInput())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[useKey].wasPressedThisFrame)
            TryUseCurrentPower();
    }

    private bool ShouldReadLocalInput()
    {
        if (identity != null && !identity.IsLocalControlled)
            return false;

        if (localRig != null && !localRig.IsLocalPlayer)
            return false;

        return true;
    }

    public bool TryUseCurrentPower()
    {
        if (inventory == null || !inventory.HasPower)
            return false;

        KartPowerType power = inventory.CurrentPower;
        GameObject target = ResolvePowerTarget(power);

        inventory.ConsumeCurrentPower();
        RaceHudEvents.Raise(gameObject, target, RaceHudEventKind.PowerUsed, power);

        switch (power)
        {
            case KartPowerType.Shield:
                ActivateShield();
                return true;

            case KartPowerType.Rocket:
                FireRocket();
                return true;

            case KartPowerType.SwapPosition:
                FireUfo(target);
                return true;

            default:
                return false;
        }
    }

    private GameObject ResolvePowerTarget(KartPowerType power)
    {
        return power == KartPowerType.SwapPosition ? FindSwapTarget() : null;
    }

    // ------------------------------------------------------------------ Escudo
    private void ActivateShield()
    {
        shieldEndTime = Time.time + shieldDuration;

        if (shieldVisual != null)
            shieldVisual.Activate();
    }

    private void UpdateShield()
    {
        if (shieldVisual == null)
            return;

        bool shouldBeActive = IsShieldActive;

        if (shouldBeActive == shieldVisual.IsActive)
            return;

        if (shouldBeActive)
            shieldVisual.Activate();
        else
            shieldVisual.Deactivate();
    }

    public void PulseShieldBlock(Vector3 impactPoint, GameObject blockVFXPrefab)
    {
        if (shieldVisual != null)
            shieldVisual.PulseBlock(impactPoint);

        GameObject vfx = blockVFXPrefab != null ? blockVFXPrefab : shieldBlockVFXPrefab;
        if (vfx != null)
            PowerVFXUtility.SpawnOneShot(vfx, impactPoint, Quaternion.identity, shieldBlockVFXFallbackLifetime);
    }

    // ------------------------------------------------------------------ Foguete
    private void EnsureRocketSocket()
    {
        if (rocketEquippedSocket != null)
            return;

        Transform existing = transform.Find("RocketEquippedSocket");
        rocketEquippedSocket = existing != null
            ? existing
            : new GameObject("RocketEquippedSocket").transform;

        rocketEquippedSocket.SetParent(transform, false);
        rocketEquippedSocket.localPosition = rocketSocketLocalPosition;
        rocketEquippedSocket.localRotation = Quaternion.identity;
    }

    private void UpdateEquippedRocket()
    {
        bool shouldShow = inventory != null && inventory.CurrentPower == KartPowerType.Rocket;

        if (!shouldShow)
        {
            if (equippedRocketInstance != null)
                equippedRocketInstance.SetActive(false);

            return;
        }

        if (equippedRocketInstance == null)
            CreateEquippedRocket();

        if (equippedRocketInstance != null && !equippedRocketInstance.activeSelf)
            equippedRocketInstance.SetActive(true);
    }

    private void CreateEquippedRocket()
    {
        GameObject prefab = rocketEquippedPrefab != null ? rocketEquippedPrefab : rocketProjectilePrefab;

        if (prefab == null)
            return;

        EnsureRocketSocket();

        equippedRocketInstance = Instantiate(prefab, rocketEquippedSocket);
        equippedRocketInstance.transform.localPosition = Vector3.zero;
        equippedRocketInstance.transform.localRotation = Quaternion.identity;

        RocketEquippedVisual equipped = equippedRocketInstance.GetComponent<RocketEquippedVisual>();
        if (equipped == null)
            equipped = equippedRocketInstance.AddComponent<RocketEquippedVisual>();

        equipped.Initialize(kart);
    }

    private void FireRocket()
    {
        EnsureRocketSocket();

        if (equippedRocketInstance != null)
        {
            Destroy(equippedRocketInstance);
            equippedRocketInstance = null;
        }

        if (rocketProjectilePrefab == null || rocketEquippedSocket == null)
        {
            Debug.LogWarning("Foguete nao disparado: prefab ou socket ausente.");
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 spawnPosition = rocketEquippedSocket.position + forward * rocketLaunchForwardOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject projectileObject = Instantiate(rocketProjectilePrefab, spawnPosition, spawnRotation);

        RocketProjectile projectile = projectileObject.GetComponent<RocketProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<RocketProjectile>();

        projectile.Initialize(gameObject, forward, explosionVFXPrefab, boingVFXPrefab, rocketTrailPrefab);
    }

    // ------------------------------------------------------------------ Disco Voador (Swap)
    private void EnsureUfoSocket()
    {
        if (ufoEquippedSocket != null)
            return;

        Transform existing = transform.Find("UfoEquippedSocket");
        ufoEquippedSocket = existing != null
            ? existing
            : new GameObject("UfoEquippedSocket").transform;

        ufoEquippedSocket.SetParent(transform, false);
        ufoEquippedSocket.localPosition = ufoSocketLocalPosition;
        ufoEquippedSocket.localRotation = Quaternion.identity;
    }

    private void UpdateEquippedUfo()
    {
        bool shouldShow = inventory != null && inventory.CurrentPower == KartPowerType.SwapPosition;

        if (!shouldShow)
        {
            if (equippedUfoInstance != null)
                equippedUfoInstance.SetActive(false);

            return;
        }

        if (equippedUfoInstance == null)
            CreateEquippedUfo();

        if (equippedUfoInstance != null && !equippedUfoInstance.activeSelf)
            equippedUfoInstance.SetActive(true);
    }

    private void CreateEquippedUfo()
    {
        GameObject prefab = ufoEquippedPrefab != null ? ufoEquippedPrefab : ufoProjectilePrefab;

        if (prefab == null)
            return;

        EnsureUfoSocket();

        equippedUfoInstance = Instantiate(prefab, ufoEquippedSocket);
        equippedUfoInstance.transform.localPosition = Vector3.zero;
        equippedUfoInstance.transform.localRotation = Quaternion.identity;

        // O prefab do projétil pode trazer o UFOSwapProjectile — em idle ele não deve voar.
        UFOSwapProjectile projectile = equippedUfoInstance.GetComponent<UFOSwapProjectile>();
        if (projectile != null)
            projectile.enabled = false;

        // Colliders desligados no idle: o disco equipado não pode disparar ItemBox/checkpoints.
        foreach (Collider col in equippedUfoInstance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody body in equippedUfoInstance.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        foreach (TrailRenderer trail in equippedUfoInstance.GetComponentsInChildren<TrailRenderer>(true))
            trail.enabled = false;

        if (equippedUfoInstance.GetComponent<UfoEquippedVisual>() == null)
            equippedUfoInstance.AddComponent<UfoEquippedVisual>();
    }

    private void FireUfo(GameObject target)
    {
        EnsureUfoSocket();

        if (equippedUfoInstance != null)
        {
            Destroy(equippedUfoInstance);
            equippedUfoInstance = null;
        }

        if (ufoProjectilePrefab == null || ufoEquippedSocket == null)
        {
            Debug.LogWarning("Disco voador nao disparado: prefab ou socket ausente.");
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 spawnPosition = ufoEquippedSocket.position + forward * ufoLaunchForwardOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject projectileObject = Instantiate(ufoProjectilePrefab, spawnPosition, spawnRotation);

        UFOSwapProjectile projectile = projectileObject.GetComponent<UFOSwapProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<UFOSwapProjectile>();

        projectile.enabled = true;
        projectile.Initialize(gameObject, forward, target, ufoMagicCirclePrefab, ufoMissVFXPrefab, boingVFXPrefab);
    }

    // ------------------------------------------------------------------ Alvo da troca
    private GameObject FindSwapTarget()
    {
        if (kart == null)
            return null;

        RaceManager raceManager = FindAnyObjectByType<RaceManager>(FindObjectsInactive.Exclude);
        if (raceManager == null || raceManager.Karts == null)
            return FindClosestKartTarget();

        KartRaceTracker ownTracker = kart.GetComponent<KartRaceTracker>();
        float ownProgress = CalculateRaceProgress(ownTracker);

        KartController bestAhead = null;
        float bestLead = float.MaxValue;
        KartController fallbackClosest = null;
        float fallbackDistance = float.MaxValue;

        foreach (KartController other in raceManager.Karts)
        {
            if (other == null || other == kart || !other.gameObject.activeInHierarchy)
                continue;

            float sqrDistance = PlanarSqrDistance(kart.transform.position, other.transform.position);
            if (sqrDistance < fallbackDistance)
            {
                fallbackDistance = sqrDistance;
                fallbackClosest = other;
            }

            float otherProgress = CalculateRaceProgress(other.GetComponent<KartRaceTracker>());
            float lead = otherProgress - ownProgress;
            if (lead <= 0f || lead >= bestLead)
                continue;

            bestLead = lead;
            bestAhead = other;
        }

        KartController target = bestAhead != null ? bestAhead : fallbackClosest;
        return target != null ? target.gameObject : null;
    }

    private GameObject FindClosestKartTarget()
    {
        KartController[] karts = FindObjectsByType<KartController>(FindObjectsInactive.Exclude);
        KartController closest = null;
        float best = float.MaxValue;

        foreach (KartController other in karts)
        {
            if (other == null || other == kart)
                continue;

            float sqrDistance = PlanarSqrDistance(kart.transform.position, other.transform.position);
            if (sqrDistance >= best)
                continue;

            best = sqrDistance;
            closest = other;
        }

        return closest != null ? closest.gameObject : null;
    }

    private static float CalculateRaceProgress(KartRaceTracker tracker)
    {
        if (tracker == null)
            return 0f;

        int totalCheckpoints = Mathf.Max(1, tracker.TotalCheckpoints);
        if (tracker.RaceFinished)
            return tracker.TotalLaps * totalCheckpoints + totalCheckpoints;

        int nextCheckpoint = tracker.NextCheckpointIndex;
        int completedThisLap = nextCheckpoint <= 0
            ? totalCheckpoints - 1
            : Mathf.Clamp(nextCheckpoint - 1, 0, totalCheckpoints - 1);

        return (Mathf.Max(1, tracker.CurrentLap) - 1) * totalCheckpoints + completedThisLap;
    }

    private static float PlanarSqrDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }
}
