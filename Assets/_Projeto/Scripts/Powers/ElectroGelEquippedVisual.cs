using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelEquippedVisual : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Material bubbleMaterial;
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Material sparksMaterial;
    [SerializeField] private Material lightningMaterial;

    [Header("Movimento")]
    [SerializeField] private Vector3 baseLocalOffset = new Vector3(-0.55f, 1.25f, -0.95f);
    [SerializeField] private float pulseSpeed = 5.2f;
    [SerializeField] private float pulseAmount = 0.08f;
    [SerializeField] private float spinSpeed = 48f;
    [SerializeField] private float driftSideOffset = 0.28f;
    [SerializeField] private float accelerationBackTilt = 0.12f;
    [SerializeField] private float followSmooth = 9f;

    private KartController kart;
    private Transform visualRoot;
    private Transform shellVisual;
    private Transform coreVisual;
    private Transform hotCoreVisual;
    private Transform haloVisual;
    private ParticleSystem sparks;
    private ElectroGelLightning lightning;
    private Light glowLight;
    private Vector3 velocity;

    public void Initialize(KartController ownerKart)
    {
        kart = ownerKart;
    }

    private void Awake()
    {
        BuildVisual();
    }

    private void OnEnable()
    {
        transform.localPosition = baseLocalOffset;
        velocity = Vector3.zero;
    }

    private void Update()
    {
        float t = Time.time;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
        float burnoutBoost = kart != null && kart.IsBurningOut ? 1.65f : 1f;

        Vector3 targetOffset = baseLocalOffset;

        if (kart != null)
        {
            targetOffset.x += -kart.DriftSignedAmount * driftSideOffset;
            targetOffset.z -= kart.ThrottleInput * accelerationBackTilt;
            targetOffset.z += kart.BrakeInput * accelerationBackTilt * 0.65f;
        }

        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetOffset,
            ref velocity,
            1f / Mathf.Max(0.01f, followSmooth)
        );

        if (visualRoot != null)
        {
            visualRoot.localScale = Vector3.one * pulse;
            visualRoot.Rotate(0f, spinSpeed * Time.deltaTime, spinSpeed * 0.45f * Time.deltaTime, Space.Self);
        }

        if (coreVisual != null)
            coreVisual.localScale = Vector3.one * Mathf.Lerp(0.30f, 0.40f, (Mathf.Sin(t * 10f) + 1f) * 0.5f);

        if (hotCoreVisual != null)
            hotCoreVisual.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.20f, (Mathf.Sin(t * 18f) + 1f) * 0.5f);

        if (haloVisual != null)
            haloVisual.localScale = new Vector3(
                Mathf.Lerp(0.95f, 1.15f, (Mathf.Sin(t * 6f) + 1f) * 0.5f),
                Mathf.Lerp(0.95f, 1.15f, (Mathf.Sin(t * 6f) + 1f) * 0.5f),
                1f
            );

        if (sparks != null)
        {
            ParticleSystem.EmissionModule emission = sparks.emission;
            emission.rateOverTime = 12f * burnoutBoost;
        }

        if (glowLight != null)
            glowLight.intensity = (1f + Mathf.Sin(t * 9f) * 0.2f) * burnoutBoost;
    }

    private void BuildVisual()
    {
        visualRoot = new GameObject("EquippedBubbleRoot").transform;
        visualRoot.SetParent(transform, false);

        shellVisual = CreateSphere("EquippedBubbleShell", visualRoot, bubbleMaterial, Vector3.one * 0.34f);
        coreVisual = CreateSphere("EquippedBubbleCore", visualRoot, coreMaterial, Vector3.one * 0.16f);
        hotCoreVisual = CreateSphere("EquippedBubbleHotCore", visualRoot, coreMaterial, Vector3.one * 0.08f);

        Material haloMat = coreMaterial != null ? coreMaterial : sparksMaterial;
        haloVisual = ElectroGelVisualUtility.BuildBillboardHalo(
            visualRoot,
            haloMat,
            ElectroGelVisualUtility.GetSoftCircle(),
            new Vector2(0.85f, 0.85f),
            new Color(0.55f, 0.95f, 1f, 0.55f)
        );

        GameObject sparksObject = new GameObject("EquippedSparks");
        sparksObject.transform.SetParent(visualRoot, false);
        sparks = sparksObject.AddComponent<ParticleSystem>();
        ElectroGelVisualUtility.ConfigureSparks(sparks, sparksMaterial, 12f, 0.22f, 0.32f);
        ParticleSystem.MainModule sparksMain = sparks.main;
        sparksMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
        sparksMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.55f);

        Material lightningUse = lightningMaterial != null ? lightningMaterial : (sparksMaterial != null ? sparksMaterial : coreMaterial);
        lightning = ElectroGelLightning.Attach(
            visualRoot,
            lightningUse,
            ElectroGelLightning.BoltShape.OrbitalSphere,
            3,
            0.32f,
            0.14f,
            0.04f,
            0.01f,
            0.05f,
            new Color(0.55f, 1f, 1f, 1f),
            new Color(0.85f, 0.55f, 1f, 1f),
            3.5f
        );

        glowLight = ElectroGelVisualUtility.AttachGlowLight(
            visualRoot,
            new Color(0.4f, 0.85f, 1f),
            1f,
            1.8f,
            Vector3.zero
        );
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
}
