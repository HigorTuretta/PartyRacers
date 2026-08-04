using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class ElectricTrapPower : MonoBehaviour
{
    public enum TrapState
    {
        Equipped,
        Falling,
        Preparing,
        Armed,
        Triggered
    }

    [Header("Referências do prefab")]
    [SerializeField] private Rigidbody body;
    [SerializeField] private Collider physicalCollider;
    [SerializeField] private SphereCollider activationTrigger;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private GameObject armedVfx;
    [SerializeField] private Light warningLight;
    [SerializeField] private Renderer warningRenderer;
    [SerializeField] private GameObject impactVfxPrefab;
    [SerializeField] private GameObject auraVfxPrefab;

    [Header("Gameplay")]
    [SerializeField, Range(0.05f, 1f)] private float speedMultiplier = 0.5f;
    [SerializeField, Min(0.1f)] private float shockDuration = 3f;
    [SerializeField, Min(0f)] private float armingDelay = 0.5f;
    [SerializeField, Min(0.2f)] private float triggerRadius = 1.65f;
    [SerializeField] private bool canHitOwner;
    [SerializeField] private LayerMask targetMask = ~0;

    [Header("Queda e assentamento")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField, Range(0f, 1f)] private float inheritedVelocityFactor = 0.5f;
    [SerializeField, Min(0f)] private float rearReleaseSpeed = 2.25f;
    [SerializeField] private float upwardReleaseSpeed = 0.35f;
    [SerializeField] private float controlledYawSpeed = 75f;
    [SerializeField, Min(0.1f)] private float landingProbeDistance = 1.25f;
    [SerializeField, Min(0.01f)] private float landingSnapDistance = 0.42f;
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.28f;
    [SerializeField, Min(0f)] private float groundOffset = 0.33f;
    [Tooltip("Limpeza de segurança somente se a armadilha cair para fora do mundo sem tocar a pista.")]
    [SerializeField, Min(5f)] private float maximumFallDistance = 80f;
    [SerializeField, Min(0.05f)] private float landingCompressionDuration = 0.22f;
    [SerializeField, Min(0.05f)] private float visualHalfHeight = 0.34f;

    [Header("Luz e apresentação")]
    [SerializeField] private Color warningColor = new Color(1f, 0.035f, 0.02f, 1f);
    [SerializeField, Min(0f)] private float warningLightIntensity = 2.1f;
    [SerializeField, Min(0.1f)] private float warningLightRange = 3.25f;
    [SerializeField, Min(0.1f)] private float equippedPulseSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float preparingBlinkSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float armedBlinkSpeed = 5.5f;
    [SerializeField, Min(0.1f)] private float triggeredBlinkSpeed = 11f;
    [SerializeField, Range(0f, 4f)] private float emissionIntensity = 2.2f;
    [SerializeField, Min(0.1f)] private float appearanceDuration = 0.24f;
    [SerializeField, Min(0.05f)] private float triggeredVisualDuration = 0.32f;
    [SerializeField, Min(0.1f)] private float impactVfxScale = 0.8f;
    [SerializeField, Min(0.1f)] private float auraScale = 1.08f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly RaycastHit[] GroundHits = new RaycastHit[12];

    // Unity can restore a MonoBehaviour after a script/domain reload without rerunning
    // non-serialized field initializers. Keep this cache lazy so the warning emission
    // remains safe in Edit/Play mode without allocating every frame.
    private MaterialPropertyBlock warningProperties;
    private Collider[] trapColliders;
    private Collider[] ownerColliders;
    private GameObject owner;
    private KartController ownerKart;
    private TrapState state = TrapState.Equipped;
    private float stateStartedAt;
    private Vector3 deployedPosition;
    private Vector3 baseVisualScale = Vector3.one;
    private bool ownerCollisionsIgnored;
    private bool initialized;

    public TrapState CurrentState => state;
    public GameObject Owner => owner;
    public bool IsArmed => state == TrapState.Armed;
    public bool HasTriggered => state == TrapState.Triggered;
    public float SpeedMultiplier => speedMultiplier;
    public float ShockDuration => shockDuration;
    public float ArmingDelay => armingDelay;
    public float TriggerRadius => triggerRadius;
    public float VisualHalfHeight => visualHalfHeight;

    private void Awake()
    {
        EnsureWarningProperties();
        CacheReferences();
        ConfigureAsEquipped();
    }

    private void EnsureWarningProperties()
    {
        if (warningProperties == null)
            warningProperties = new MaterialPropertyBlock();
    }

    private void CacheReferences()
    {
        if (initialized)
            return;

        if (body == null)
            body = GetComponent<Rigidbody>();

        if (activationTrigger == null)
        {
            SphereCollider[] spheres = GetComponentsInChildren<SphereCollider>(true);
            for (int i = 0; i < spheres.Length; i++)
            {
                if (spheres[i].isTrigger)
                {
                    activationTrigger = spheres[i];
                    break;
                }
            }
        }

        if (physicalCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger)
                {
                    physicalCollider = colliders[i];
                    break;
                }
            }
        }

        if (visualRoot == null)
        {
            Transform candidate = transform.Find("VisualRoot");
            visualRoot = candidate != null ? candidate : transform;
        }

        baseVisualScale = visualRoot.localScale;
        trapColliders = GetComponentsInChildren<Collider>(true);

        if (activationTrigger != null)
        {
            activationTrigger.isTrigger = true;
            activationTrigger.radius = triggerRadius;
        }

        if (warningLight != null)
        {
            warningLight.color = warningColor;
            warningLight.range = warningLightRange;
            warningLight.shadows = LightShadows.None;
        }

        initialized = true;
    }

    public void SetEquipped(GameObject ownerObject, KartController ownerController)
    {
        CacheReferences();
        owner = ownerObject;
        ownerKart = ownerController != null
            ? ownerController
            : ownerObject != null ? ownerObject.GetComponent<KartController>() : null;
        ownerColliders = ownerObject != null ? ownerObject.GetComponentsInChildren<Collider>(true) : null;
        SetOwnerCollisionsIgnored(true);

        state = TrapState.Equipped;
        stateStartedAt = Time.time;
        ConfigureAsEquipped();

        if (visualRoot != null)
            visualRoot.localScale = baseVisualScale * 0.12f;
    }

    public void Deploy(
        GameObject ownerObject,
        KartController ownerController,
        Vector3 inheritedVelocity,
        Vector3 ownerForward)
    {
        CacheReferences();
        owner = ownerObject;
        ownerKart = ownerController != null
            ? ownerController
            : ownerObject != null ? ownerObject.GetComponent<KartController>() : null;
        ownerColliders = ownerObject != null ? ownerObject.GetComponentsInChildren<Collider>(true) : null;
        SetOwnerCollisionsIgnored(true);

        transform.SetParent(null, true);
        state = TrapState.Falling;
        stateStartedAt = Time.time;
        deployedPosition = transform.position;

        if (visualRoot != null)
            visualRoot.localScale = baseVisualScale;

        if (armedVfx != null)
            armedVfx.SetActive(false);

        if (activationTrigger != null)
            activationTrigger.enabled = false;

        if (physicalCollider != null)
            physicalCollider.enabled = true;

        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            Vector3 forward = ownerForward.sqrMagnitude > 0.001f
                ? ownerForward.normalized
                : transform.forward;
            body.linearVelocity = inheritedVelocity * inheritedVelocityFactor
                - forward * rearReleaseSpeed
                + Vector3.up * upwardReleaseSpeed;
            body.angularVelocity = Vector3.up * (controlledYawSpeed * Mathf.Deg2Rad);
            body.WakeUp();
        }

        Physics.SyncTransforms();
    }

    private void ConfigureAsEquipped()
    {
        if (activationTrigger != null)
            activationTrigger.enabled = false;

        if (physicalCollider != null)
            physicalCollider.enabled = false;

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }

        if (armedVfx != null)
            armedVfx.SetActive(false);
    }

    private void Update()
    {
        float now = Time.time;

        switch (state)
        {
            case TrapState.Equipped:
                UpdateEquippedVisual(now);
                break;

            case TrapState.Preparing:
                UpdateLandingCompression(now);
                if (now - stateStartedAt >= armingDelay)
                    Arm();
                break;

            case TrapState.Armed:
                UpdateArmedVisual(now);
                break;

            case TrapState.Triggered:
                UpdateTriggeredVisual(now);
                if (now - stateStartedAt >= triggeredVisualDuration)
                    Destroy(gameObject);
                break;

        }

        UpdateWarningLight(now);
    }

    private void FixedUpdate()
    {
        if (state != TrapState.Falling)
            return;

        if (TryFindGround(out RaycastHit groundHit))
        {
            float verticalSpeed = body != null ? Vector3.Dot(body.linearVelocity, -groundHit.normal) : 0f;
            if (groundHit.distance <= landingSnapDistance && verticalSpeed >= -1f)
            {
                SettleOnGround(groundHit.point, groundHit.normal);
                return;
            }
        }

        // Não existe tempo de vida para a armadilha armada. Esta única limpeza protege contra
        // uma soltura que atravesse a pista e continue caindo para fora do mundo.
        if (transform.position.y < deployedPosition.y - maximumFallDistance)
            Destroy(gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (state != TrapState.Falling || collision == null || collision.collider == null)
            return;

        if (!IsValidGroundCollider(collision.collider) || collision.contactCount == 0)
            return;

        ContactPoint best = collision.GetContact(0);
        for (int i = 1; i < collision.contactCount; i++)
        {
            ContactPoint candidate = collision.GetContact(i);
            if (candidate.normal.y > best.normal.y)
                best = candidate;
        }

        if (best.normal.y >= minimumGroundNormalY)
            SettleOnGround(best.point, best.normal);
    }

    private bool TryFindGround(out RaycastHit bestHit)
    {
        bestHit = default;
        Vector3 origin = transform.position + transform.up * 0.15f;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            GroundHits,
            landingProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.collider == null || hit.normal.y < minimumGroundNormalY || !IsValidGroundCollider(hit.collider))
                continue;

            if (hit.distance >= bestDistance)
                continue;

            bestDistance = hit.distance;
            bestHit = hit;
            found = true;
        }

        return found;
    }

    private bool IsValidGroundCollider(Collider candidate)
    {
        if (candidate == null || candidate.isTrigger || candidate.transform.IsChildOf(transform))
            return false;

        if (candidate.GetComponentInParent<KartController>() != null)
            return false;

        return candidate.GetComponentInParent<ElectricTrapPower>() == null;
    }

    private void SettleOnGround(Vector3 point, Vector3 normal)
    {
        if (state != TrapState.Falling)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal);
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.ProjectOnPlane(transform.right, normal);

        Quaternion alignedRotation = Quaternion.LookRotation(forward.normalized, normal);
        transform.SetPositionAndRotation(point + normal * groundOffset, alignedRotation);

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = true;
        }

        if (physicalCollider != null)
            physicalCollider.enabled = false;

        if (activationTrigger != null)
            activationTrigger.enabled = false;

        if (armedVfx != null)
            armedVfx.SetActive(true);

        state = TrapState.Preparing;
        stateStartedAt = Time.time;
        Physics.SyncTransforms();
    }

    private void Arm()
    {
        if (state != TrapState.Preparing)
            return;

        state = TrapState.Armed;
        stateStartedAt = Time.time;

        if (activationTrigger != null)
        {
            activationTrigger.radius = triggerRadius;
            activationTrigger.enabled = true;
        }

        Physics.SyncTransforms();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (state != TrapState.Armed || other == null)
            return;

        int layerBit = 1 << other.gameObject.layer;
        if ((targetMask.value & layerBit) == 0)
            return;

        KartController targetKart = other.GetComponentInParent<KartController>();
        if (targetKart == null || !targetKart.gameObject.activeInHierarchy)
            return;

        if (!canHitOwner && (targetKart == ownerKart || targetKart.gameObject == owner))
            return;

        Trigger(targetKart, other);
    }

    private void Trigger(KartController targetKart, Collider targetCollider)
    {
        if (state != TrapState.Armed)
            return;

        state = TrapState.Triggered;
        stateStartedAt = Time.time;

        if (activationTrigger != null)
            activationTrigger.enabled = false;
        if (physicalCollider != null)
            physicalCollider.enabled = false;
        if (armedVfx != null)
            armedVfx.SetActive(false);

        Vector3 impactPoint = targetCollider != null
            ? targetCollider.ClosestPoint(transform.position)
            : targetKart.transform.position;
        if ((impactPoint - transform.position).sqrMagnitude < 0.001f)
            impactPoint = targetKart.transform.position + Vector3.up * 0.45f;

        KartPowerUser targetPowerUser = targetKart.GetComponent<KartPowerUser>();
        if (targetPowerUser != null && targetPowerUser.IsShieldActive)
        {
            targetPowerUser.PulseShieldBlock(impactPoint, null);
            return;
        }

        if (impactVfxPrefab != null)
        {
            Vector3 direction = targetKart.transform.position - transform.position;
            direction.y = 0f;
            Quaternion rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(direction.normalized, Vector3.up)
                : Quaternion.identity;
            PowerVFXUtility.SpawnOneShot(
                impactVfxPrefab,
                impactPoint,
                rotation,
                2.5f,
                0f,
                impactVfxScale);
        }

        RaceHudEvents.Raise(owner, targetKart.gameObject, RaceHudEventKind.HitOpponent, KartPowerType.ElectricTrap);
        RaceHudEvents.Raise(targetKart.gameObject, owner, RaceHudEventKind.GotHit, KartPowerType.ElectricTrap);

        // Dano de ITEM (15). O teste de escudo acima já devolveu quando a bolha estava no ar.
        KartHealth targetHealth = targetKart.GetComponent<KartHealth>();
        if (targetHealth != null)
            targetHealth.ApplyItemDamage(impactPoint, owner);

        KartElectricShockEffect.ApplyTo(
            targetKart.gameObject,
            shockDuration,
            speedMultiplier,
            auraVfxPrefab,
            auraScale);
    }

    private void UpdateEquippedVisual(float now)
    {
        if (visualRoot == null)
            return;

        float t = Mathf.Clamp01((now - stateStartedAt) / appearanceDuration);
        float eased = t * t * (3f - 2f * t);
        visualRoot.localScale = Vector3.LerpUnclamped(baseVisualScale * 0.12f, baseVisualScale, eased);
    }

    private void UpdateLandingCompression(float now)
    {
        if (visualRoot == null)
            return;

        float t = Mathf.Clamp01((now - stateStartedAt) / landingCompressionDuration);
        float compression = Mathf.Sin(t * Mathf.PI);
        visualRoot.localScale = new Vector3(
            baseVisualScale.x * (1f + compression * 0.08f),
            baseVisualScale.y * (1f - compression * 0.18f),
            baseVisualScale.z * (1f + compression * 0.08f));
    }

    private void UpdateArmedVisual(float now)
    {
        if (visualRoot == null)
            return;

        float pulse = 1f + Mathf.Sin(now * armedBlinkSpeed * Mathf.PI * 2f) * 0.018f;
        visualRoot.localScale = baseVisualScale * pulse;
    }

    private void UpdateTriggeredVisual(float now)
    {
        if (visualRoot == null)
            return;

        float t = Mathf.Clamp01((now - stateStartedAt) / triggeredVisualDuration);
        float flashExpansion = 1f + Mathf.Sin(Mathf.Min(1f, t * 2f) * Mathf.PI) * 0.16f;
        float fadeScale = Mathf.Lerp(1f, 0.05f, t * t);
        visualRoot.localScale = baseVisualScale * flashExpansion * fadeScale;
    }

    private void UpdateWarningLight(float now)
    {
        float speed;
        float minimum;
        float maximum;

        switch (state)
        {
            case TrapState.Equipped:
                speed = equippedPulseSpeed;
                minimum = 0.08f;
                maximum = 0.28f;
                break;
            case TrapState.Falling:
            case TrapState.Preparing:
                speed = preparingBlinkSpeed;
                minimum = 0.08f;
                maximum = 0.72f;
                break;
            case TrapState.Armed:
                speed = armedBlinkSpeed;
                minimum = 0.05f;
                maximum = 1f;
                break;
            case TrapState.Triggered:
                speed = triggeredBlinkSpeed;
                minimum = 0.15f;
                maximum = 1.25f;
                break;
            default:
                speed = armedBlinkSpeed;
                minimum = 0f;
                maximum = 0f;
                break;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(now * speed * Mathf.PI * 2f);
        wave = Mathf.SmoothStep(0f, 1f, wave);
        float level = Mathf.Lerp(minimum, maximum, wave);

        if (warningLight != null)
        {
            warningLight.enabled = level > 0.01f;
            warningLight.color = warningColor;
            warningLight.range = warningLightRange;
            warningLight.intensity = warningLightIntensity * level;
        }

        if (warningRenderer != null)
        {
            EnsureWarningProperties();
            warningRenderer.GetPropertyBlock(warningProperties);
            warningProperties.SetColor(EmissionColorId, warningColor * (emissionIntensity * level));
            warningRenderer.SetPropertyBlock(warningProperties);
        }
    }

    private void SetOwnerCollisionsIgnored(bool ignored)
    {
        if (ownerCollisionsIgnored == ignored || trapColliders == null || ownerColliders == null)
            return;

        for (int i = 0; i < trapColliders.Length; i++)
        {
            Collider trapCollider = trapColliders[i];
            if (trapCollider == null)
                continue;

            for (int j = 0; j < ownerColliders.Length; j++)
            {
                Collider ownerCollider = ownerColliders[j];
                if (ownerCollider != null)
                    Physics.IgnoreCollision(trapCollider, ownerCollider, ignored);
            }
        }

        ownerCollisionsIgnored = ignored;
    }

    private void OnDisable()
    {
        SetOwnerCollisionsIgnored(false);
    }

    private void OnDestroy()
    {
        SetOwnerCollisionsIgnored(false);
    }
}
