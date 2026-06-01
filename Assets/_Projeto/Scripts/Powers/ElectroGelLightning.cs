using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelLightning : MonoBehaviour
{
    public enum BoltShape
    {
        OrbitalSphere,
        RadialBurst,
        ArcToEndpoint,
        ZapColumn
    }

    [Header("Material")]
    [SerializeField] private Material arcMaterial;

    [Header("Forma")]
    [SerializeField] private BoltShape shape = BoltShape.OrbitalSphere;
    [SerializeField] private int boltCount = 4;
    [SerializeField] private int segments = 12;
    [SerializeField] private float radius = 0.55f;
    [SerializeField] private float jitter = 0.18f;
    [SerializeField] private float startWidth = 0.06f;
    [SerializeField] private float endWidth = 0.02f;

    [Header("Tempo")]
    [SerializeField] private float regenerateInterval = 0.045f;
    [SerializeField] private float flickerAmount = 0.35f;
    [SerializeField] private float lifetime = -1f;

    [Header("Cores")]
    [SerializeField] private Color colorA = new Color(0.35f, 1f, 1f, 1f);
    [SerializeField] private Color colorB = new Color(0.8f, 0.45f, 1f, 1f);
    [SerializeField] private float emissionIntensity = 7.5f;

    private LineRenderer[] bolts;
    private Material[] runtimeMaterials;
    private float[] nextRegen;
    private Vector3[][] cachedPositions;
    private Vector3 endpointLocal = Vector3.forward;
    private float age;
    private bool initialized;

    public void SetEndpoint(Vector3 localEndpoint)
    {
        endpointLocal = localEndpoint;
    }

    public void SetRadius(float newRadius)
    {
        radius = newRadius;
    }

    public void SetMaterial(Material material)
    {
        arcMaterial = material;
    }

    public void ForceRegenerate()
    {
        if (!initialized)
            return;

        for (int i = 0; i < bolts.Length; i++)
            RegenerateBolt(i);
    }

    private void Awake()
    {
        Build();
        initialized = true;
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (lifetime > 0f && age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float fade = 1f;
        if (lifetime > 0f)
            fade = Mathf.Clamp01(1f - age / lifetime);

        for (int i = 0; i < bolts.Length; i++)
        {
            nextRegen[i] -= Time.deltaTime;

            if (nextRegen[i] <= 0f)
            {
                RegenerateBolt(i);
                nextRegen[i] = Random.Range(regenerateInterval * 0.55f, regenerateInterval * 1.6f);
            }

            if (runtimeMaterials[i] != null)
            {
                float flicker = 1f - Random.value * flickerAmount;
                Color tint = Color.Lerp(colorA, colorB, (Mathf.Sin(Time.time * 7f + i * 1.3f) + 1f) * 0.5f);
                Color emissive = tint * emissionIntensity * flicker * fade;
                emissive.a = tint.a * fade;

                if (runtimeMaterials[i].HasProperty("_BaseColor"))
                    runtimeMaterials[i].SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, tint.a * fade));

                if (runtimeMaterials[i].HasProperty("_EmissionColor"))
                    runtimeMaterials[i].SetColor("_EmissionColor", emissive);
            }

            float widthFlicker = 1f - Random.value * flickerAmount * 0.6f;
            bolts[i].startWidth = startWidth * widthFlicker * fade;
            bolts[i].endWidth = endWidth * widthFlicker * fade;
        }
    }

    private void Build()
    {
        int count = Mathf.Max(1, boltCount);
        bolts = new LineRenderer[count];
        runtimeMaterials = new Material[count];
        nextRegen = new float[count];
        cachedPositions = new Vector3[count][];

        for (int i = 0; i < count; i++)
        {
            GameObject boltObject = new GameObject($"LightningBolt_{i}");
            boltObject.transform.SetParent(transform, false);

            LineRenderer line = boltObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.textureMode = LineTextureMode.Stretch;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            line.positionCount = Mathf.Max(2, segments);
            line.startWidth = startWidth;
            line.endWidth = endWidth;

            if (arcMaterial != null)
            {
                Material instance = ElectroGelVisualUtility.InstantiateMaterialWithTexture(
                    arcMaterial,
                    ElectroGelVisualUtility.GetLightningStreak()
                );
                line.material = instance;
                runtimeMaterials[i] = instance;
            }

            bolts[i] = line;
            cachedPositions[i] = new Vector3[Mathf.Max(2, segments)];
            nextRegen[i] = Random.Range(0f, regenerateInterval);
            RegenerateBolt(i);
        }
    }

    private void RegenerateBolt(int index)
    {
        LineRenderer line = bolts[index];
        Vector3[] positions = cachedPositions[index];
        int segCount = positions.Length;

        Vector3 start;
        Vector3 end;

        switch (shape)
        {
            case BoltShape.OrbitalSphere:
                start = Random.onUnitSphere * radius;
                end = Random.onUnitSphere * radius;
                if (Vector3.Dot(start, end) > 0.85f)
                    end = -end;
                break;

            case BoltShape.RadialBurst:
                start = Vector3.zero;
                end = Random.onUnitSphere * radius;
                break;

            case BoltShape.ArcToEndpoint:
                start = Vector3.zero;
                end = endpointLocal + Random.insideUnitSphere * (radius * 0.15f);
                break;

            case BoltShape.ZapColumn:
                float angleA = Random.Range(0f, Mathf.PI * 2f);
                float angleB = Random.Range(0f, Mathf.PI * 2f);
                float rA = radius * Random.Range(0.6f, 1f);
                float rB = radius * Random.Range(0.6f, 1f);
                start = new Vector3(Mathf.Cos(angleA) * rA, -radius * 0.45f, Mathf.Sin(angleA) * rA);
                end = new Vector3(Mathf.Cos(angleB) * rB, radius * 1.05f, Mathf.Sin(angleB) * rB);
                break;

            default:
                start = Vector3.zero;
                end = endpointLocal;
                break;
        }

        Vector3 axis = end - start;
        float axisLength = axis.magnitude;
        Vector3 axisDir = axisLength > 0.0001f ? axis / axisLength : Vector3.forward;
        Vector3 perp = Vector3.Cross(axisDir, Random.onUnitSphere).normalized;
        if (perp.sqrMagnitude < 0.001f)
            perp = Vector3.Cross(axisDir, Vector3.up).normalized;

        for (int s = 0; s < segCount; s++)
        {
            float t = s / (float)(segCount - 1);
            float taper = Mathf.Sin(t * Mathf.PI);
            Vector3 basePoint = Vector3.Lerp(start, end, t);
            Vector3 perpA = perp;
            Vector3 perpB = Vector3.Cross(axisDir, perp).normalized;
            float offsetA = (Random.value * 2f - 1f) * jitter * taper;
            float offsetB = (Random.value * 2f - 1f) * jitter * taper;
            positions[s] = basePoint + perpA * offsetA + perpB * offsetB;
        }

        line.positionCount = segCount;
        line.SetPositions(positions);
    }

    private void OnDestroy()
    {
        if (runtimeMaterials == null)
            return;

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            if (runtimeMaterials[i] != null)
                Destroy(runtimeMaterials[i]);
        }
    }

    public static ElectroGelLightning Attach(
        Transform parent,
        Material material,
        BoltShape shape,
        int boltCount,
        float radius,
        float jitter,
        float startWidth,
        float endWidth,
        float regenerateInterval,
        Color colorA,
        Color colorB,
        float emissionIntensity,
        float lifetime = -1f)
    {
        GameObject host = new GameObject("ElectroGelLightning");
        host.transform.SetParent(parent, false);

        ElectroGelLightning lightning = host.AddComponent<ElectroGelLightning>();
        lightning.arcMaterial = material;
        lightning.shape = shape;
        lightning.boltCount = boltCount;
        lightning.radius = radius;
        lightning.jitter = jitter;
        lightning.startWidth = startWidth;
        lightning.endWidth = endWidth;
        lightning.regenerateInterval = regenerateInterval;
        lightning.colorA = colorA;
        lightning.colorB = colorB;
        lightning.emissionIntensity = emissionIntensity;
        lightning.lifetime = lifetime;

        return lightning;
    }
}
