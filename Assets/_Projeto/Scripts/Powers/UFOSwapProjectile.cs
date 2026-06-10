using UnityEngine;

[DisallowMultipleComponent]
public class UFOSwapProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 58f;
    [SerializeField] private float lifetime = 9f;
    [SerializeField] private float maxDistance = 190f;
    [Tooltip("Raio do corpo do UFO usado para colisao com paredes (mantenha pequeno).")]
    [SerializeField] private float radius = 1.2f;
    [Tooltip("Raio (maior) de proximidade para capturar karts perto do trajeto, sem colisao direta.")]
    [SerializeField] private float captureRadius = 3f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [Tooltip("0 mantem voo reto. Use apenas se quiser homing leve.")]
    [SerializeField] private float homingTurnRate = 0f;

    [Header("Altura sobre o chao (igual ao foguete)")]
    [SerializeField] private float hoverHeight = 1.65f;
    [SerializeField] private float heightAdjustSpeed = 16f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Normais com Y acima disso contam como CHAO/RAMPA: o UFO sobrevoa, nunca quica.")]
    [SerializeField, Range(0f, 1f)] private float groundNormalMinY = 0.55f;
    [Tooltip("Folga minima (m) acima do chao. Trava rigida: o UFO NUNCA entra no chao mesmo se atrasar.")]
    [SerializeField] private float minGroundClearance = 0.7f;
    [Tooltip("Profundidade da sonda de chao (m). Maior evita 'sumico' ao passar por desniveis/rampas.")]
    [SerializeField] private float groundProbeDown = 40f;
    [Tooltip("Apenas superficies com normal Y abaixo disso (paredes verticais) fazem o UFO quicar.")]
    [SerializeField, Range(0f, 1f)] private float wallMaxNormalY = 0.45f;

    [Header("Auto-hit")]
    [SerializeField] private bool allowOwnerHitAfterIgnore = true;
    [SerializeField] private float ignoreOwnerTime = 0.45f;
    [SerializeField] private float ignoreOwnerDistance = 5f;

    [Header("Quique em parede")]
    [SerializeField] private int maxBounces = 4;
    [SerializeField, Range(0.5f, 1f)] private float bounceSpeedRetention = 0.95f;
    [SerializeField] private GameObject boingVFXPrefab;
    [SerializeField] private float boingScale = 1f;

    [Header("Visual")]
    [SerializeField] private float modelScale = 0.35f;
    [SerializeField] private float spinSpeed = 340f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 4f;

    [Header("Captura e Swap")]
    [SerializeField] private float captureRiseHeight = 2.4f;
    [SerializeField] private float captureRiseDuration = 0.45f;
    [SerializeField] private float swapDelay = 1.2f;
    [SerializeField] private GameObject magicCirclePrefab;
    [SerializeField] private float magicCircleLinger = 0.6f;

    [Header("VFX")]
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private Vector3 trailLocalOffset = new Vector3(0f, -0.05f, -0.65f);
    [SerializeField] private Vector3 trailLocalEuler = new Vector3(90f, 0f, 0f);
    [SerializeField] private float trailLingerAfterFinish = 0.9f;
    [SerializeField] private GameObject impactVFXPrefab;
    [SerializeField] private GameObject blockVFXPrefab;
    [Tooltip("Prefab tocado quando o UFO some por timeout/quiques/swap concluido.")]
    [SerializeField] private GameObject disappearVFXPrefab;
    [Tooltip("Campo legado usado como fallback para disappearVFXPrefab.")]
    [SerializeField] private GameObject missVFXPrefab;
    [SerializeField] private float vfxFallbackLifetime = 2f;
    [SerializeField] private float disappearVFXScale = 1f;
    [SerializeField] private float destroyDelayAfterFinish = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private Color debugPathColor = Color.cyan;
    [SerializeField] private Color debugHitColor = Color.yellow;

    private enum State { Flying, Capturing, WaitingSwap, Done }
    private enum HitKind { Target, Obstacle }

    private struct ProjectileHit
    {
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public HitKind Kind;
    }

    private readonly RaycastHit[] sweepHits = new RaycastHit[24];
    private readonly RaycastHit[] groundProbe = new RaycastHit[16];
    private readonly Collider[] overlapHits = new Collider[24];

    private GameObject owner;
    private GameObject targetKartObject;
    private KartController capturedKart;
    private State state = State.Flying;

    private Vector3 direction;
    private float lifeTimer;
    private float travelled;
    private int bounceCount;
    private float spinAngle;
    private float baseY;
    private float lastGroundY;
    private bool hasGround;

    private float captureTimer;
    private Vector3 captureStartPosition;
    private float swapTimer;
    private Transform trailInstance;

    public void Initialize(
        GameObject projectileOwner,
        Vector3 fireDirection,
        GameObject homingTarget,
        GameObject magicCircleVFX = null,
        GameObject missVFX = null,
        GameObject boingVFX = null)
    {
        owner = projectileOwner;
        targetKartObject = homingTarget;
        direction = Planar(fireDirection).sqrMagnitude > 0.0001f ? Planar(fireDirection).normalized : Vector3.forward;

        if (magicCircleVFX != null)
            magicCirclePrefab = magicCircleVFX;

        if (missVFX != null)
        {
            missVFXPrefab = missVFX;
            if (disappearVFXPrefab == null)
                disappearVFXPrefab = missVFX;
        }

        if (boingVFX != null)
            boingVFXPrefab = boingVFX;
    }

    private void Awake()
    {
        EnsurePhysics();
        transform.localScale = Vector3.one * modelScale;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Planar(transform.forward).normalized;

        baseY = transform.position.y;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Flying:
                UpdateFlight();
                break;
            case State.Capturing:
                UpdateCapture();
                break;
            case State.WaitingSwap:
                UpdateSwapWait();
                break;
        }
    }

    private void UpdateFlight()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Disappear();
            return;
        }

        if (trailInstance == null && trailPrefab != null)
            SpawnTrail();

        UpdateHoming();

        if (TryFindOverlapTarget(transform.position, out ProjectileHit overlapHit))
        {
            HandleHit(overlapHit);
            return;
        }

        float distance = speed * Time.deltaTime;
        Vector3 start = transform.position;

        if (TryFindSweepHit(distance, out ProjectileHit hit))
        {
            transform.position = hit.Point - direction * Mathf.Min(radius, distance);
            travelled += hit.Distance;
            HandleHit(hit);
            return;
        }

        transform.position += direction * distance;
        travelled += distance;

        Vector3 position = transform.position;
        MaintainHoverHeight(ref position);
        transform.position = position;
        baseY = position.y;

        AnimateSpin();
        UpdateTrail();

        if (debugMode)
            Debug.DrawLine(start, transform.position, debugPathColor, Time.deltaTime);

        if (travelled >= maxDistance)
            Disappear();
    }

    private void UpdateHoming()
    {
        if (homingTurnRate <= 0f || targetKartObject == null || !targetKartObject.activeInHierarchy)
            return;

        Vector3 toTarget = Planar(targetKartObject.transform.position - transform.position);
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        direction = Vector3.RotateTowards(
            direction,
            toTarget.normalized,
            homingTurnRate * Mathf.Deg2Rad * Time.deltaTime,
            0f).normalized;
    }

    // Mantem altura FIXA sobre o chao (igual ao foguete) e impede DEFINITIVAMENTE que o UFO entre
    // no chao: alem de seguir suavemente o terreno, aplica uma trava rigida (groundY + folga minima).
    // Sonda bem fundo (groundProbeDown) para nao "sumir" ao passar por rampas/desniveis.
    private void MaintainHoverHeight(ref Vector3 position)
    {
        if (TryGetGroundY(position, out float groundY))
        {
            hasGround = true;
            lastGroundY = groundY;

            float targetY = groundY + hoverHeight;
            position.y = Mathf.MoveTowards(position.y, targetY, heightAdjustSpeed * Time.deltaTime);

            // Trava rigida: nunca abaixo da folga minima, mesmo se o terreno subir rapido.
            float floorY = groundY + minGroundClearance;
            if (position.y < floorY)
                position.y = floorY;
        }
    }

    private bool TryGetGroundY(Vector3 position, out float groundY)
    {
        groundY = 0f;
        Vector3 origin = position + Vector3.up * 4f;
        int count = Physics.RaycastNonAlloc(
            origin, Vector3.down, groundProbe, 4f + groundProbeDown, groundMask, QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundProbe[i];
            if (hit.collider == null)
                continue;

            // Karts nao sao chao (o UFO nao "sobe" ao sobrevoar um carro).
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }
        }

        return found;
    }

    private void AnimateSpin()
    {
        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);

        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;

        // O bob nunca pode furar a folga minima sobre o chao.
        if (hasGround && position.y < lastGroundY + minGroundClearance)
            position.y = lastGroundY + minGroundClearance;

        transform.position = position;
        transform.rotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    private bool TryFindSweepHit(float distance, out ProjectileHit bestHit)
    {
        int mask = targetMask.value | obstacleMask.value | groundMask.value | collisionMask.value;
        int count = Physics.SphereCastNonAlloc(
            transform.position,
            radius,
            direction,
            sweepHits,
            distance,
            mask,
            QueryTriggerInteraction.Collide);

        bestHit = default;
        bestHit.Distance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = sweepHits[i];
            if (candidate.collider == null)
                continue;

            if (!TryClassifyHit(candidate.collider, candidate.point, candidate.normal, candidate.distance, out ProjectileHit classified))
                continue;

            if (classified.Distance < bestHit.Distance)
                bestHit = classified;
        }

        return bestHit.Collider != null;
    }

    private bool TryFindOverlapTarget(Vector3 position, out ProjectileHit hit)
    {
        int count = Physics.OverlapSphereNonAlloc(position, captureRadius, overlapHits, targetMask, QueryTriggerInteraction.Collide);
        hit = default;
        hit.Distance = float.MaxValue;
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapHits[i];
            if (candidate == null)
                continue;

            KartController kart = candidate.GetComponentInParent<KartController>();
            if (!IsValidTarget(kart, candidate))
                continue;

            Vector3 point = candidate.ClosestPoint(position);
            Vector3 normal = position - point;
            if (normal.sqrMagnitude < 0.001f)
                normal = -direction;

            float sqrDistance = (point - position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            hit = new ProjectileHit
            {
                Collider = candidate,
                Point = point,
                Normal = normal.normalized,
                Distance = 0f,
                Kind = HitKind.Target
            };
        }

        return hit.Collider != null;
    }

    private bool TryClassifyHit(Collider candidate, Vector3 point, Vector3 normal, float distance, out ProjectileHit hit)
    {
        hit = default;

        if (candidate == null || candidate.transform.IsChildOf(transform))
            return false;

        KartController kart = candidate.GetComponentInParent<KartController>();
        if (kart != null)
        {
            if (!IsValidTarget(kart, candidate))
                return false;

            hit = new ProjectileHit
            {
                Collider = candidate,
                Point = point,
                Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : -direction,
                Distance = distance,
                Kind = HitKind.Target
            };
            return true;
        }

        // Chao/rampa: qualquer superficie razoavelmente horizontal e SOBREVOADA (nunca quica).
        // O UFO mantem altura fixa sobre ela via MaintainHoverHeight. So paredes verticais
        // (normal.y < wallMaxNormalY) sao tratadas como obstaculo e fazem o UFO quicar.
        if (normal.y >= wallMaxNormalY)
            return false;

        if (candidate.isTrigger || !IsInMask(candidate.gameObject.layer, obstacleMask))
            return false;

        hit = new ProjectileHit
        {
            Collider = candidate,
            Point = point,
            Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : -direction,
            Distance = distance,
            Kind = HitKind.Obstacle
        };
        return true;
    }

    private bool IsValidTarget(KartController kart, Collider candidate)
    {
        if (kart == null || !kart.gameObject.activeInHierarchy)
            return false;

        if (!IsInMask(candidate.gameObject.layer, targetMask))
            return false;

        if (owner == null)
            return true;

        if (!candidate.transform.IsChildOf(owner.transform))
            return true;

        if (!allowOwnerHitAfterIgnore)
            return false;

        return lifeTimer >= ignoreOwnerTime && travelled >= ignoreOwnerDistance;
    }

    private void HandleHit(ProjectileHit hit)
    {
        if (state != State.Flying || hit.Collider == null)
            return;

        if (debugMode)
            Debug.DrawRay(hit.Point, hit.Normal * 2f, debugHitColor, 0.75f);

        if (hit.Kind == HitKind.Target)
        {
            HandleTargetHit(hit);
            return;
        }

        HandleObstacleHit(hit);
    }

    private void HandleTargetHit(ProjectileHit hit)
    {
        KartController kart = hit.Collider.GetComponentInParent<KartController>();
        if (kart == null)
            return;

        KartPowerUser shieldUser = kart.GetComponent<KartPowerUser>();
        if (shieldUser != null && shieldUser.IsShieldActive)
        {
            shieldUser.PulseShieldBlock(hit.Point, blockVFXPrefab);
            SpawnVFX(boingVFXPrefab, hit.Point, hit.Normal, boingScale);
            FinishWithoutDisappear();
            return;
        }

        BeginCapture(kart, hit.Point, hit.Normal);
    }

    private void HandleObstacleHit(ProjectileHit hit)
    {
        if (bounceCount < maxBounces)
        {
            bounceCount++;
            Vector3 flatNormal = Planar(hit.Normal).normalized;
            if (flatNormal.sqrMagnitude < 0.001f)
                flatNormal = -direction;

            direction = Vector3.Reflect(direction, flatNormal).normalized;
            speed *= bounceSpeedRetention;
            transform.position = hit.Point + hit.Normal * (radius + 0.05f);
            SpawnVFX(boingVFXPrefab, hit.Point, hit.Normal, boingScale);
            return;
        }

        Disappear();
    }

    private void BeginCapture(KartController hitKart, Vector3 hitPoint, Vector3 hitNormal)
    {
        state = State.Capturing;
        capturedKart = hitKart;
        captureTimer = 0f;
        captureStartPosition = transform.position;
        ReleaseTrail();
        SpawnVFX(impactVFXPrefab, hitPoint, hitNormal, 1f);
    }

    private void UpdateCapture()
    {
        if (!IsCaptureStillValid())
        {
            Disappear();
            return;
        }

        captureTimer += Time.deltaTime;
        float t = Mathf.Clamp01(captureTimer / Mathf.Max(0.05f, captureRiseDuration));
        float smooth = Mathf.SmoothStep(0f, 1f, t);

        Vector3 targetPosition = capturedKart.transform.position + Vector3.up * captureRiseHeight;
        transform.position = Vector3.Lerp(captureStartPosition, targetPosition, smooth);
        SpinInPlace();

        if (t >= 1f)
            BeginSwapWait();
    }

    private void BeginSwapWait()
    {
        state = State.WaitingSwap;
        swapTimer = 0f;

        float circleLifetime = swapDelay + magicCircleLinger;
        SpawnMagicCircle(owner != null ? owner.transform : null, circleLifetime);
        SpawnMagicCircle(capturedKart != null ? capturedKart.transform : null, circleLifetime);
    }

    private void SpawnMagicCircle(Transform target, float circleLifetime)
    {
        if (magicCirclePrefab == null || target == null)
            return;

        GameObject circle = Instantiate(magicCirclePrefab, target.position, Quaternion.identity);
        GroundFollowEffect.Attach(circle, target, circleLifetime, groundMask);
    }

    private void UpdateSwapWait()
    {
        if (!IsCaptureStillValid())
        {
            Disappear();
            return;
        }

        transform.position = capturedKart.transform.position + Vector3.up * captureRiseHeight;
        SpinInPlace();

        swapTimer += Time.deltaTime;
        if (swapTimer < swapDelay)
            return;

        ExecuteSwap();
    }

    private bool IsCaptureStillValid()
    {
        return capturedKart != null
            && capturedKart.gameObject.activeInHierarchy
            && owner != null
            && owner.activeInHierarchy;
    }

    private void ExecuteSwap()
    {
        if (state == State.Done)
            return;

        KartPowerUser targetPowerUser = capturedKart.GetComponent<KartPowerUser>();
        if (targetPowerUser != null && targetPowerUser.IsShieldActive)
        {
            targetPowerUser.PulseShieldBlock(capturedKart.transform.position + Vector3.up, blockVFXPrefab);
            FinishWithoutDisappear();
            return;
        }

        RaceHudEvents.Raise(owner, capturedKart.gameObject, RaceHudEventKind.HitOpponent, KartPowerType.SwapPosition);
        RaceHudEvents.Raise(capturedKart.gameObject, owner, RaceHudEventKind.GotHit, KartPowerType.SwapPosition);

        KartSwapUtility.SwapPositions(owner, capturedKart.gameObject);
        Disappear();
    }

    private void SpinInPlace()
    {
        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);
        transform.rotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    private void Disappear()
    {
        if (state == State.Done)
            return;

        state = State.Done;
        ReleaseTrail();

        GameObject vfx = disappearVFXPrefab != null ? disappearVFXPrefab : missVFXPrefab;
        SpawnVFX(vfx, transform.position, Vector3.up, disappearVFXScale);
        Destroy(gameObject, destroyDelayAfterFinish);
    }

    private void FinishWithoutDisappear()
    {
        if (state == State.Done)
            return;

        state = State.Done;
        ReleaseTrail();
        Destroy(gameObject, destroyDelayAfterFinish);
    }

    private void SpawnTrail()
    {
        GameObject trail = Instantiate(trailPrefab);
        trailInstance = trail.transform;
        PositionTrail();
    }

    private void UpdateTrail()
    {
        if (trailInstance != null)
            PositionTrail();
    }

    private void PositionTrail()
    {
        Quaternion flightRotation = FlightRotation();
        trailInstance.position = transform.position + flightRotation * trailLocalOffset;
        trailInstance.rotation = flightRotation * Quaternion.Euler(trailLocalEuler);
    }

    // Rotação segura na direção de voo. 'direction' pode estar (transitoriamente) zerada antes do
    // Initialize ou em casos degenerados — aí caímos no forward atual em vez de logar
    // "Look rotation viewing vector is zero" a cada frame.
    private Quaternion FlightRotation()
    {
        Vector3 dir = Planar(direction);
        if (dir.sqrMagnitude < 0.0001f)
            dir = Planar(transform.forward);
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;

        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void ReleaseTrail()
    {
        if (trailInstance == null)
            return;

        Transform trail = trailInstance;
        trailInstance = null;
        PowerVFXUtility.StopAndDestroyTrail(trail, trailLingerAfterFinish);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Vector3 normal, float scale)
    {
        if (prefab == null)
            return;

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal.normalized, Vector3.up)
            : Quaternion.identity;

        PowerVFXUtility.SpawnOneShot(prefab, position, rotation, vfxFallbackLifetime, 0f, scale);
    }

    private static Vector3 Planar(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private void EnsurePhysics()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();

        sphere.radius = radius;
        sphere.isTrigger = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugMode)
            return;

        Gizmos.color = debugPathColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawLine(transform.position, transform.position + Planar(transform.forward).normalized * 4f);
    }

    private static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
