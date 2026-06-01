using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelProjectile : MonoBehaviour
{
    [Header("Gameplay")]
    [SerializeField] private float speed = 34f;
    [SerializeField] private float lifetime = 4.5f;
    [SerializeField] private float radius = 0.34f;
    [SerializeField] private int maxBounces = 3;
    [SerializeField] private float stunDuration = 2.25f;
    [SerializeField] private float bounceSpeedRetention = 0.92f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Visual")]
    [SerializeField] private Material bubbleMaterial;
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Material trailMaterial;
    [SerializeField] private Material sparksMaterial;
    [SerializeField] private Material lightningMaterial;
    [SerializeField] private float visualScale = 0.62f;
    [SerializeField] private float pulseSpeed = 10f;
    [SerializeField] private float squashAmount = 0.16f;

    [Header("VFX")]
    [SerializeField] private GameObject bounceVFXPrefab;
    [SerializeField] private GameObject hitVFXPrefab;
    [SerializeField] private GameObject shieldBlockVFXPrefab;
    [SerializeField] private GameObject expireVFXPrefab;
    [SerializeField] private GameObject stunAuraPrefab;

    private readonly RaycastHit[] hits = new RaycastHit[24];
    private GameObject owner;
    private Vector3 direction;
    private float lifeTimer;
    private int bounceCount;
    private Transform visualRoot;
    private Transform shellVisual;
    private Transform coreVisual;
    private Transform hotCoreVisual;
    private Transform haloVisual;
    private Transform billboardHaloA;
    private Transform billboardHaloB;
    private TrailRenderer trail;
    private TrailRenderer trailGlow;
    private ParticleSystem sparks;
    private ParticleSystem bubbles;
    private ElectroGelLightning lightningOrbit;
    private Light glowLight;
    private bool initialized;
    private Material trailRuntimeMaterial;
    private Material trailGlowRuntimeMaterial;

    public void Initialize(
        GameObject projectileOwner,
        Vector3 fireDirection,
        GameObject bounceVFX,
        GameObject hitVFX,
        GameObject shieldBlockVFX,
        GameObject expireVFX,
        GameObject stunAura)
    {
        owner = projectileOwner;
        direction = fireDirection.sqrMagnitude > 0.001f ? fireDirection.normalized : transform.forward;
        bounceVFXPrefab = bounceVFX != null ? bounceVFX : bounceVFXPrefab;
        hitVFXPrefab = hitVFX != null ? hitVFX : hitVFXPrefab;
        shieldBlockVFXPrefab = shieldBlockVFX != null ? shieldBlockVFX : shieldBlockVFXPrefab;
        expireVFXPrefab = expireVFX != null ? expireVFX : expireVFXPrefab;
        stunAuraPrefab = stunAura != null ? stunAura : stunAuraPrefab;
        initialized = true;
    }

    private void Awake()
    {
        EnsurePhysics();
        BuildVisual();
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
        bounceCount = 0;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;
    }

    private void Update()
    {
        if (!initialized && direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        lifeTimer += Time.deltaTime;

        if (lifeTimer >= lifetime)
        {
            Expire();
            return;
        }

        MoveProjectile();
        AnimateVisual();
    }

    private void MoveProjectile()
    {
        float distance = speed * Time.deltaTime;

        if (TryFindHit(distance, out RaycastHit hit))
        {
            transform.position = hit.point - direction * Mathf.Min(radius, distance);
            HandleHit(hit);
            return;
        }

        transform.position += direction * distance;
    }

    private bool TryFindHit(float distance, out RaycastHit bestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            radius,
            direction,
            hits,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hits[i];

            if (candidate.collider == null)
                continue;

            if (candidate.collider.transform.IsChildOf(transform))
                continue;

            if (owner != null && candidate.collider.transform.IsChildOf(owner.transform))
                continue;

            if (candidate.distance < bestDistance)
            {
                bestHit = candidate;
                bestDistance = candidate.distance;
            }
        }

        return bestDistance < float.MaxValue;
    }

    private void HandleHit(RaycastHit hit)
    {
        KartPowerUser shieldUser = hit.collider.GetComponentInParent<KartPowerUser>();
        ElectroGelDummyTarget dummyTarget = hit.collider.GetComponentInParent<ElectroGelDummyTarget>();

        if (shieldUser != null && shieldUser.gameObject != owner && shieldUser.IsShieldActive)
        {
            shieldUser.PulseShieldBlock(hit.point, shieldBlockVFXPrefab);
            SpawnVFX(shieldBlockVFXPrefab, hit.point, hit.normal);
            Destroy(gameObject);
            return;
        }

        if (dummyTarget != null && dummyTarget.gameObject != owner && dummyTarget.IsShieldActive)
        {
            dummyTarget.PulseShieldBlock(hit.point, shieldBlockVFXPrefab);
            SpawnVFX(shieldBlockVFXPrefab, hit.point, hit.normal);
            Destroy(gameObject);
            return;
        }

        KartController kart = hit.collider.GetComponentInParent<KartController>();

        if (kart != null && kart.gameObject != owner)
        {
            SpawnVFX(hitVFXPrefab, hit.point, hit.normal);
            ElectroGelStunEffect.ApplyTo(kart.gameObject, stunDuration, stunAuraPrefab);
            Destroy(gameObject);
            return;
        }

        if (dummyTarget != null && dummyTarget.gameObject != owner)
        {
            SpawnVFX(hitVFXPrefab, hit.point, hit.normal);
            ElectroGelStunEffect.ApplyTo(dummyTarget.gameObject, stunDuration, stunAuraPrefab);
            Destroy(gameObject);
            return;
        }

        Bounce(hit);
    }

    private void Bounce(RaycastHit hit)
    {
        bounceCount++;
        SpawnVFX(bounceVFXPrefab, hit.point, hit.normal);

        if (bounceCount > maxBounces)
        {
            Expire();
            return;
        }

        direction = Vector3.Reflect(direction, hit.normal).normalized;
        speed *= bounceSpeedRetention;
        transform.position = hit.point + hit.normal * (radius + 0.04f);
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        if (trail != null)
            trail.Clear();

        if (trailGlow != null)
            trailGlow.Clear();

        if (lightningOrbit != null)
            lightningOrbit.ForceRegenerate();
    }

    private void Expire()
    {
        SpawnVFX(expireVFXPrefab, transform.position, -direction);
        Destroy(gameObject);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Vector3 normal)
    {
        if (prefab == null)
            return;

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal.normalized, Vector3.up)
            : Quaternion.identity;

        Instantiate(prefab, position, rotation);
    }

    private void EnsurePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider == null)
            sphereCollider = gameObject.AddComponent<SphereCollider>();

        sphereCollider.radius = radius;
        sphereCollider.isTrigger = true;
    }

    private void BuildVisual()
    {
        visualRoot = new GameObject("ElectroGel_VisualRoot").transform;
        visualRoot.SetParent(transform, false);

        shellVisual = CreateSphere("BubbleShell", visualRoot, bubbleMaterial, Vector3.one * visualScale);
        coreVisual = CreateSphere("BubbleCore", visualRoot, coreMaterial, Vector3.one * visualScale * 0.58f);
        hotCoreVisual = CreateSphere("BubbleHotCore", visualRoot, coreMaterial, Vector3.one * visualScale * 0.24f);

        Material haloMaterial = ResolveHaloMaterial();
        billboardHaloA = ElectroGelVisualUtility.BuildBillboardHalo(
            visualRoot,
            haloMaterial,
            ElectroGelVisualUtility.GetSoftCircle(),
            new Vector2(visualScale * 2.4f, visualScale * 2.4f),
            new Color(0.55f, 0.95f, 1f, 0.55f)
        );

        billboardHaloB = ElectroGelVisualUtility.BuildBillboardHalo(
            visualRoot,
            haloMaterial,
            ElectroGelVisualUtility.GetStarSprite(),
            new Vector2(visualScale * 3.4f, visualScale * 3.4f),
            new Color(0.85f, 0.5f, 1f, 0.45f)
        );
        ElectroGelBillboard billboardSpinner = billboardHaloB.GetComponent<ElectroGelBillboard>();
        if (billboardSpinner != null)
            billboardSpinner.SetSpinSpeed(90f);

        Material trailUse = trailMaterial != null ? trailMaterial : sparksMaterial;
        trailRuntimeMaterial = ElectroGelVisualUtility.InstantiateMaterialWithTexture(trailUse, ElectroGelVisualUtility.GetSoftCircle());

        GameObject trailObject = new GameObject("GelTrail");
        trailObject.transform.SetParent(transform, false);
        trail = trailObject.AddComponent<TrailRenderer>();
        trail.material = trailRuntimeMaterial;
        trail.time = 0.28f;
        trail.minVertexDistance = 0.03f;
        trail.startWidth = visualScale * 0.95f;
        trail.endWidth = 0.02f;
        trail.numCornerVertices = 3;
        trail.numCapVertices = 3;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.colorGradient = BuildTrailGradient(new Color(0.6f, 1f, 1f), new Color(0.7f, 0.45f, 1f));

        trailGlowRuntimeMaterial = ElectroGelVisualUtility.InstantiateMaterialWithTexture(trailUse, ElectroGelVisualUtility.GetSoftCircle());
        if (trailGlowRuntimeMaterial != null && trailGlowRuntimeMaterial.HasProperty("_EmissionColor"))
        {
            Color emission = trailGlowRuntimeMaterial.GetColor("_EmissionColor") * 0.55f;
            trailGlowRuntimeMaterial.SetColor("_EmissionColor", emission);
        }

        GameObject trailGlowObject = new GameObject("GelTrailGlow");
        trailGlowObject.transform.SetParent(transform, false);
        trailGlow = trailGlowObject.AddComponent<TrailRenderer>();
        trailGlow.material = trailGlowRuntimeMaterial;
        trailGlow.time = 0.45f;
        trailGlow.minVertexDistance = 0.05f;
        trailGlow.startWidth = visualScale * 1.85f;
        trailGlow.endWidth = 0.04f;
        trailGlow.numCornerVertices = 3;
        trailGlow.numCapVertices = 3;
        trailGlow.alignment = LineAlignment.View;
        trailGlow.textureMode = LineTextureMode.Stretch;
        trailGlow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trailGlow.receiveShadows = false;
        trailGlow.colorGradient = BuildTrailGradient(new Color(0.4f, 0.95f, 1f, 0.7f), new Color(0.5f, 0.3f, 1f, 0f));

        GameObject sparksObject = new GameObject("OrbitSparks");
        sparksObject.transform.SetParent(transform, false);
        sparks = sparksObject.AddComponent<ParticleSystem>();
        ElectroGelVisualUtility.ConfigureSparks(sparks, sparksMaterial, 38f, 0.32f, visualScale * 1.4f);

        GameObject bubblesObject = new GameObject("OrbitBubbles");
        bubblesObject.transform.SetParent(transform, false);
        bubbles = bubblesObject.AddComponent<ParticleSystem>();
        ElectroGelVisualUtility.ConfigureSparks(bubbles, coreMaterial, 14f, 0.45f, visualScale * 0.9f);
        ParticleSystem.MainModule bubbleMain = bubbles.main;
        bubbleMain.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.14f);
        bubbleMain.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);

        Material lightningUse = lightningMaterial != null ? lightningMaterial : (sparksMaterial != null ? sparksMaterial : coreMaterial);
        lightningOrbit = ElectroGelLightning.Attach(
            visualRoot,
            lightningUse,
            ElectroGelLightning.BoltShape.OrbitalSphere,
            5,
            visualScale * 0.95f,
            visualScale * 0.4f,
            visualScale * 0.075f,
            visualScale * 0.02f,
            0.04f,
            new Color(0.55f, 1f, 1f, 1f),
            new Color(0.85f, 0.55f, 1f, 1f),
            4.5f
        );

        glowLight = ElectroGelVisualUtility.AttachGlowLight(
            transform,
            new Color(0.35f, 0.85f, 1f),
            2.4f,
            visualScale * 6f,
            Vector3.zero
        );
    }

    private Material ResolveHaloMaterial()
    {
        if (coreMaterial != null)
            return coreMaterial;

        if (sparksMaterial != null)
            return sparksMaterial;

        if (bubbleMaterial != null)
            return bubbleMaterial;

        return null;
    }

    private Gradient BuildTrailGradient(Color start, Color end)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(start, 0f),
                new GradientColorKey(end, 1f)
            },
            new[]
            {
                new GradientAlphaKey(start.a, 0f),
                new GradientAlphaKey(0.5f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private Transform CreateSphere(string objectName, Transform parent, Material material, Vector3 scale)
    {
        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = objectName;
        sphere.transform.SetParent(parent, false);
        sphere.transform.localScale = scale;

        Collider sphereCollider = sphere.GetComponent<Collider>();
        if (sphereCollider != null)
            Destroy(sphereCollider);

        Renderer renderer = sphere.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        return sphere.transform;
    }

    private void AnimateVisual()
    {
        if (visualRoot == null)
            return;

        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * 0.07f;
        float squash = Mathf.Sin(t * pulseSpeed * 1.27f) * squashAmount;
        float instability = bounceCount * 0.03f;
        float lifeFraction = Mathf.Clamp01(lifeTimer / Mathf.Max(0.01f, lifetime));
        float lifeFade = Mathf.SmoothStep(1f, 0.65f, lifeFraction);

        if (direction.sqrMagnitude > 0.0001f)
            visualRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);

        float stretchZ = pulse + Mathf.Min(1.4f, speed * 0.018f) * 0.18f;
        Vector3 baseScale = new Vector3(
            (pulse - squash * 0.18f + instability) * lifeFade,
            (pulse - squash * 0.18f + instability) * lifeFade,
            (stretchZ + squash * 0.4f + instability) * lifeFade
        );
        visualRoot.localScale = baseScale;

        if (coreVisual != null)
        {
            float corePulse = Mathf.Lerp(0.5f, 0.66f, (Mathf.Sin(t * 14f) + 1f) * 0.5f);
            coreVisual.localScale = Vector3.one * visualScale * corePulse;
        }

        if (hotCoreVisual != null)
        {
            float hotPulse = Mathf.Lerp(0.22f, 0.32f, (Mathf.Sin(t * 22f) + 1f) * 0.5f);
            hotCoreVisual.localScale = Vector3.one * visualScale * hotPulse;
        }

        if (billboardHaloA != null)
        {
            float halo = Mathf.Lerp(2.2f, 2.7f, (Mathf.Sin(t * 6f) + 1f) * 0.5f) * lifeFade;
            billboardHaloA.localScale = new Vector3(visualScale * halo, visualScale * halo, 1f);
        }

        if (billboardHaloB != null)
        {
            float spinHalo = Mathf.Lerp(3.0f, 3.6f, (Mathf.Sin(t * 4.2f + 1.7f) + 1f) * 0.5f) * lifeFade;
            billboardHaloB.localScale = new Vector3(visualScale * spinHalo, visualScale * spinHalo, 1f);
        }

        if (glowLight != null)
        {
            float flicker = 1f - Random.value * 0.12f;
            glowLight.intensity = 2.2f * flicker * lifeFade + 0.4f;
        }

        if (lightningOrbit != null)
            lightningOrbit.SetRadius(visualScale * Mathf.Lerp(0.85f, 1.05f, (Mathf.Sin(t * 5.4f) + 1f) * 0.5f));
    }

    private void OnDestroy()
    {
        if (trailRuntimeMaterial != null)
            Destroy(trailRuntimeMaterial);

        if (trailGlowRuntimeMaterial != null)
            Destroy(trailGlowRuntimeMaterial);
    }
}
