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

    [Header("Ancoragem adaptativa (Rocket/UFO)")]
    [Tooltip("Ajusta a ALTURA dos sockets do Rocket/UFO pelos bounds reais do carro — em carros " +
             "altos os assets não clipam mais dentro da carroceria. Carros baixos mantêm a posição configurada.")]
    [SerializeField] private bool fitSocketsToCarBounds = true;
    [Tooltip("Se o modelo do carro tiver um filho com este nome, ele vira a âncora do foguete (prioridade máxima).")]
    [SerializeField] private string rocketAnchorName = "RocketAnchor";
    [Tooltip("Se o modelo do carro tiver um filho com este nome, ele vira a âncora do disco voador.")]
    [SerializeField] private string ufoAnchorName = "UfoAnchor";
    [Tooltip("Âncora genérica usada quando não existe âncora específica do poder.")]
    [SerializeField] private string genericAnchorName = "PowerAnchor";
    [Tooltip("Folga (m) entre o topo da carroceria e o foguete (já cobre o bob da animação).")]
    [SerializeField] private float rocketClearance = 0.35f;
    [Tooltip("Folga (m) entre o topo da carroceria e a parte de baixo do disco voador.")]
    [SerializeField] private float ufoClearance = 0.2f;

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
        FitRocketSocket();

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
    /// <summary>Altura do disco acima do socket, lida do prefab (fallback = padrão do visual).</summary>
    private float UfoVisualHeight()
    {
        GameObject prefab = ufoEquippedPrefab != null ? ufoEquippedPrefab : ufoProjectilePrefab;
        UfoEquippedVisual visual = prefab != null ? prefab.GetComponent<UfoEquippedVisual>() : null;
        return visual != null ? visual.Height : 0.95f;
    }

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

        UfoEquippedVisual ufoVisual = equippedUfoInstance.GetComponent<UfoEquippedVisual>();
        if (ufoVisual == null)
            ufoVisual = equippedUfoInstance.AddComponent<UfoEquippedVisual>();

        FitUfoSocket(ufoVisual);
    }

    // ------------------------------------------------------------------ Ancoragem adaptativa
    // Posiciona os sockets do Rocket/UFO ACIMA da carroceria real do carro. Recalculado a cada
    // criação do visual equipado (cobre troca de modelo na garagem/customização dos bots).
    // Prioridade: âncora explícita no modelo (RocketAnchor/UfoAnchor/PowerAnchor) > bounds.
    private void FitRocketSocket()
    {
        Transform anchor = FindDeepChild(transform, rocketAnchorName) ?? FindDeepChild(transform, genericAnchorName);
        if (anchor != null)
        {
            rocketEquippedSocket.position = anchor.position;
            rocketEquippedSocket.rotation = transform.rotation;
            return;
        }

        if (!fitSocketsToCarBounds)
            return;

        float topY = ComputeBodyTopLocalY();
        Vector3 local = rocketSocketLocalPosition;
        local.y = Mathf.Max(local.y, topY + rocketClearance);
        rocketEquippedSocket.localPosition = local;
    }

    private void FitUfoSocket(UfoEquippedVisual visual)
    {
        EnsureUfoSocket();

        Transform anchor = FindDeepChild(transform, ufoAnchorName) ?? FindDeepChild(transform, genericAnchorName);
        if (anchor != null)
        {
            ufoEquippedSocket.position = anchor.position;
            ufoEquippedSocket.rotation = transform.rotation;
            return;
        }

        if (!fitSocketsToCarBounds)
            return;

        // O disco orbita 'Height' acima do socket e desce 'BobAmplitude' no fundo do bob:
        // garante que mesmo o ponto mais baixo da órbita fique acima da carroceria.
        float visualHeight = visual != null ? visual.Height : 0.95f;
        float bob = visual != null ? visual.BobAmplitude : 0.07f;

        float topY = ComputeBodyTopLocalY();
        float requiredY = topY + ufoClearance + bob - visualHeight;

        Vector3 local = ufoSocketLocalPosition;
        local.y = Mathf.Max(local.y, requiredY);
        ufoEquippedSocket.localPosition = local;
    }

    // Topo da carroceria em Y LOCAL do kart, medindo os renderers reais do modelo atual.
    // Ignora visuais de poderes (sockets/escudo) e emissores (partículas/trilhas).
    private float ComputeBodyTopLocalY()
    {
        float topY = 0.9f; // fallback razoável se o carro não tiver renderers ativos

        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        bool found = false;

        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled)
                continue;

            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                continue;

            Transform t = r.transform;
            if (rocketEquippedSocket != null && t.IsChildOf(rocketEquippedSocket))
                continue;
            if (ufoEquippedSocket != null && t.IsChildOf(ufoEquippedSocket))
                continue;
            if (IsUnderIgnoredVisual(t))
                continue;

            // Converte o AABB mundial do renderer para o espaço local do kart (topo conservador).
            Bounds b = r.bounds;
            Vector3 min = b.min;
            Vector3 max = b.max;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);

                float localY = transform.InverseTransformPoint(corner).y;
                if (!found || localY > topY)
                {
                    topY = found ? Mathf.Max(topY, localY) : localY;
                    found = true;
                }
            }
        }

        return found ? topY : 0.9f;
    }

    private static bool IsUnderIgnoredVisual(Transform target)
    {
        for (Transform cur = target; cur != null; cur = cur.parent)
        {
            string n = cur.name;
            if (n.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("VFX", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Transform FindDeepChild(Transform root, string childName)
    {
        if (string.IsNullOrEmpty(childName))
            return null;

        foreach (Transform child in root)
        {
            if (child.name == childName)
                return child;

            Transform nested = FindDeepChild(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    private void FireUfo(GameObject target)
    {
        EnsureUfoSocket();

        // De ONDE o disco sai: do lugar em que ele estava visivelmente flutuando, não do socket.
        // O socket fica de propósito ABAIXO do teto do carro (o visual equipado orbita 'Height'
        // acima dele) — nascer nele punha o projétil dentro da carroceria, quase no asfalto.
        Vector3 launchOrigin = ufoEquippedSocket != null ? ufoEquippedSocket.position : transform.position;
        if (equippedUfoInstance != null)
        {
            launchOrigin = equippedUfoInstance.transform.position;
            Destroy(equippedUfoInstance);
            equippedUfoInstance = null;
        }
        else if (ufoEquippedSocket != null)
        {
            // Disparo no mesmo frame da coleta: o visual ainda não existe. Usa a altura que ele
            // teria (o socket sozinho aponta para dentro do carro).
            launchOrigin += Vector3.up * UfoVisualHeight();
        }

        if (ufoProjectilePrefab == null || ufoEquippedSocket == null)
        {
            Debug.LogWarning("Disco voador nao disparado: prefab ou socket ausente.");
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 spawnPosition = launchOrigin + forward * ufoLaunchForwardOffset;
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
