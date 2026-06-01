using UnityEngine;
using UnityEngine.Rendering;

public class DriftPuffBubble : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float lifetime = 0.85f;

    [Header("Escala")]
    [SerializeField] private float startScale = 0.25f;
    [SerializeField] private float endScale = 1.15f;

    [Header("Movimento")]
    [SerializeField] private Vector3 initialVelocity = new Vector3(0f, 0.18f, -0.05f);
    [SerializeField] private float damping = 2.8f;

    [Header("Forma")]
    [SerializeField] private float squashVariation = 0.22f;

    [Header("Cloud Mesh")]
    [SerializeField] private bool generateCloudMesh = true;
    [SerializeField] private float cloudBumpStrength = 0.28f;
    [SerializeField] private float cloudNoiseScale = 3.25f;

    [Header("Low Poly Cloud")]
    [SerializeField] private int lobeCountMin = 6;
    [SerializeField] private int lobeCountMax = 8;
    [SerializeField] private float lobeRadius = 0.52f;
    [SerializeField] private float lobeSpread = 1.78f;
    [SerializeField] private float internalDriftStrength = 0.18f;

    [Header("Cor")]
    [SerializeField] private Color smokeLitColor = new Color(0.82f, 0.80f, 0.72f, 1f);
    [SerializeField] private Color smokeMidColor = new Color(0.60f, 0.59f, 0.53f, 1f);
    [SerializeField] private Color smokeShadowColor = new Color(0.38f, 0.37f, 0.34f, 1f);

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.08f;
    [SerializeField] private float fadeOutStart = 0.58f;

    private const int MeshCacheSize = 8;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Mesh[] meshCache;

    private float timer;
    private Vector3 currentVelocity;
    private Vector3 randomRotationSpeed;
    private Vector3 scaleMultiplier;
    private Lobe[] lobes;
    private MaterialPropertyBlock propertyBlock;

    private struct Lobe
    {
        public Transform Transform;
        public MeshRenderer Renderer;
        public Vector3 BaseOffset;
        public Vector3 DriftOffset;
        public Vector3 BaseScale;
        public Vector3 Spin;
        public Color Color;
    }

    public void Initialize(
        float customLifetime,
        float customStartScale,
        float customEndScale,
        Vector3 customVelocity,
        bool isBurnout = false)
    {
        lifetime = customLifetime;
        startScale = customStartScale;
        endScale = customEndScale;
        currentVelocity = customVelocity;
        timer = 0f;

        scaleMultiplier = new Vector3(
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation)
        );

        float rotSpeed = isBurnout ? 8f : 45f;
        randomRotationSpeed = new Vector3(
            Random.Range(-rotSpeed, rotSpeed),
            Random.Range(-rotSpeed, rotSpeed),
            Random.Range(-rotSpeed, rotSpeed)
        );

        transform.localScale = Vector3.one * startScale;
    }

    private void Awake()
    {
        BuildLowPolyCloud();

        if (currentVelocity == Vector3.zero)
            currentVelocity = initialVelocity;

        scaleMultiplier = new Vector3(
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation)
        );

        randomRotationSpeed = new Vector3(
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f)
        );
    }

    private void BuildLowPolyCloud()
    {
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        if (sourceRenderer == null || sourceFilter == null)
            return;

        sourceRenderer.enabled = false;
        sourceRenderer.shadowCastingMode = ShadowCastingMode.Off;
        sourceRenderer.receiveShadows = false;

        int lobeCount = Mathf.Clamp(Random.Range(lobeCountMin, lobeCountMax + 1), 3, 10);
        lobes = new Lobe[lobeCount];
        propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < lobeCount; i++)
        {
            GameObject lobeObject = new GameObject($"SmokeLobe_{i:00}");
            lobeObject.transform.SetParent(transform, false);

            MeshFilter meshFilter = lobeObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = generateCloudMesh
                ? GetCloudMesh(Random.Range(0, MeshCacheSize))
                : sourceFilter.sharedMesh;

            MeshRenderer meshRenderer = lobeObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            bool center = i == 0;
            float angle = center ? 0f : (Mathf.PI * 2f * (i - 1) / Mathf.Max(1, lobeCount - 1)) + Random.Range(-0.45f, 0.45f);
            float ring = center ? 0f : Random.Range(lobeRadius * 0.74f, lobeRadius);
            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * ring,
                Random.Range(-0.08f, 0.14f),
                Mathf.Sin(angle) * ring * 0.82f
            );

            float lobeSize = center ? Random.Range(0.72f, 0.9f) : Random.Range(0.44f, 0.72f);
            Vector3 baseScale = new Vector3(
                lobeSize * Random.Range(0.82f, 1.24f),
                lobeSize * Random.Range(0.72f, 1.08f),
                lobeSize * Random.Range(0.78f, 1.18f)
            );

            Color lobeColor = Color.Lerp(smokeShadowColor, smokeLitColor, center ? 0.78f : Random.Range(0.25f, 0.9f));
            lobeColor = Color.Lerp(lobeColor, smokeMidColor, Random.Range(0f, 0.22f));

            lobes[i] = new Lobe
            {
                Transform = lobeObject.transform,
                Renderer = meshRenderer,
                BaseOffset = offset,
                DriftOffset = new Vector3(
                    Random.Range(-internalDriftStrength, internalDriftStrength),
                    Random.Range(internalDriftStrength * 0.25f, internalDriftStrength),
                    Random.Range(-internalDriftStrength, internalDriftStrength)
                ),
                BaseScale = baseScale,
                Spin = new Vector3(
                    Random.Range(-26f, 26f),
                    Random.Range(-34f, 34f),
                    Random.Range(-24f, 24f)
                ),
                Color = lobeColor
            };

            lobeObject.transform.localPosition = offset;
            lobeObject.transform.localRotation = Random.rotation;
            lobeObject.transform.localScale = baseScale;
        }
    }

    private Mesh GetCloudMesh(int index)
    {
        if (meshCache == null)
        {
            meshCache = new Mesh[MeshCacheSize];
            for (int i = 0; i < MeshCacheSize; i++)
                meshCache[i] = CloudMeshGenerator.Generate(cloudBumpStrength, cloudNoiseScale, i * 17.37f);
        }

        return meshCache[Mathf.Abs(index) % meshCache.Length];
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / lifetime);

        transform.position += currentVelocity * Time.deltaTime;
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, damping * Time.deltaTime);

        float grow   = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
        float shrink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));

        float scale = Mathf.Lerp(startScale, endScale, grow);
        scale *= Mathf.Lerp(1f, 0.12f, shrink);

        transform.localScale = Vector3.one * scale;
        transform.localScale = Vector3.Scale(transform.localScale, scaleMultiplier);

        transform.Rotate(randomRotationSpeed * Time.deltaTime, Space.Self);
        UpdateCloudLobes(t, grow, shrink);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void UpdateCloudLobes(float t, float grow, float shrink)
    {
        if (lobes == null)
            return;

        float spread = Mathf.Lerp(0.55f, lobeSpread, grow);
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.001f, fadeInDuration)));
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeOutStart, 1f, t));
        float alpha = fadeIn * fadeOut;

        for (int i = 0; i < lobes.Length; i++)
        {
            Lobe lobe = lobes[i];
            if (lobe.Transform == null)
                continue;

            lobe.Transform.localPosition = lobe.BaseOffset * spread + lobe.DriftOffset * t;
            lobe.Transform.localScale = lobe.BaseScale * Mathf.Lerp(0.82f, 1.08f, grow) * Mathf.Lerp(1f, 0.72f, shrink);
            lobe.Transform.Rotate(lobe.Spin * Time.deltaTime, Space.Self);

            if (lobe.Renderer != null)
            {
                Color color = Color.Lerp(lobe.Color, smokeShadowColor, Mathf.SmoothStep(0f, 1f, t) * 0.35f);
                color.a *= alpha;

                propertyBlock.Clear();
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                lobe.Renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
