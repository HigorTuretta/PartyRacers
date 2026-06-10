using UnityEngine;

// Puff de fumaça/nuvem low-poly estilizada. Cada puff é um pequeno mesh agrupado (vários lóbulos)
// gerado pelo CloudMeshGenerator, com sombreamento matte e uma leve variação de cor ao longo da vida
// para parecer uma nuvenzinha cartunesca em vez de uma bola branca. O comportamento de spawn
// (KartDriftPuffTrail) continua o mesmo — só o visual mudou.
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
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
    [SerializeField] private int minLobes = 3;
    [SerializeField] private int maxLobes = 5;
    [SerializeField] private float cloudBumpStrength = 0.20f;
    [SerializeField] private float cloudNoiseScale = 2.5f;

    [Header("Cor (matte, sombreada pela luz da cena)")]
    [SerializeField] private Color youngColor = new Color(0.95f, 0.97f, 1f, 1f);
    [SerializeField] private Color oldColor = new Color(0.70f, 0.77f, 0.90f, 1f);
    [SerializeField] private Color burnoutTint = new Color(0.80f, 0.78f, 0.76f, 1f);
    [SerializeField, Range(0f, 0.3f)] private float colorJitter = 0.06f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float timer;
    private Vector3 currentVelocity;
    private Vector3 randomRotationSpeed;
    private Vector3 scaleMultiplier;
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color baseYoung;
    private Color baseOld;

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

        // Nuvens de drift "deslizam", não rolam: giro suave e dominante no eixo Y,
        // mantendo a base achatada do mesh apontada para baixo.
        float rotSpeed = isBurnout ? 6f : 14f;
        randomRotationSpeed = new Vector3(
            Random.Range(-rotSpeed, rotSpeed) * 0.18f,
            Random.Range(-rotSpeed, rotSpeed),
            Random.Range(-rotSpeed, rotSpeed) * 0.18f
        );

        transform.localScale = Vector3.one * startScale;

        SetupColors(isBurnout);
        ApplyColor(0f);
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        if (meshRenderer != null)
        {
            // Dezenas de puffs minúsculos não devem custar sombras: visual mais limpo e leve.
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        if (generateCloudMesh)
            ApplyCloudMesh();

        if (currentVelocity == Vector3.zero)
            currentVelocity = initialVelocity;

        if (scaleMultiplier == Vector3.zero)
        {
            scaleMultiplier = new Vector3(
                Random.Range(1f - squashVariation, 1f + squashVariation),
                Random.Range(1f - squashVariation, 1f + squashVariation),
                Random.Range(1f - squashVariation, 1f + squashVariation)
            );
        }

        if (randomRotationSpeed == Vector3.zero)
        {
            randomRotationSpeed = new Vector3(
                Random.Range(-3f, 3f),
                Random.Range(-14f, 14f),
                Random.Range(-3f, 3f)
            );
        }

        if (baseYoung == default)
            SetupColors(false);
    }

    private void SetupColors(bool isBurnout)
    {
        baseYoung = youngColor;
        baseOld = isBurnout ? burnoutTint : oldColor;

        if (colorJitter > 0f)
        {
            float j = Random.Range(-colorJitter, colorJitter);
            baseYoung = Shift(baseYoung, j);
            baseOld = Shift(baseOld, j * 0.5f);
        }
    }

    private static Color Shift(Color c, float amount)
    {
        return new Color(
            Mathf.Clamp01(c.r + amount),
            Mathf.Clamp01(c.g + amount * 0.9f),
            Mathf.Clamp01(c.b + amount * 1.1f),
            c.a
        );
    }

    // Cache estático de meshes de nuvem: antes cada puff GERAVA um mesh procedural novo no Awake
    // (custo de CPU) e o atribuía via 'mf.mesh', que clona/vaza um Mesh por puff (GC pesado). Com
    // 16 karts derrapando isso causava engasgos. Agora pré-geramos algumas variações UMA vez e
    // reaproveitamos via sharedMesh — visual praticamente idêntico, custo perto de zero.
    private const int CloudMeshVariations = 16;
    private static Mesh[] cloudMeshCache;

    private void ApplyCloudMesh()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        if (mf == null)
            return;

        EnsureCloudMeshCache();
        mf.sharedMesh = cloudMeshCache[Random.Range(0, cloudMeshCache.Length)];
    }

    private void EnsureCloudMeshCache()
    {
        if (cloudMeshCache != null)
            return;

        cloudMeshCache = new Mesh[CloudMeshVariations];
        int loCap = Mathf.Max(1, minLobes);
        int hiCap = Mathf.Max(loCap, maxLobes);

        for (int i = 0; i < CloudMeshVariations; i++)
        {
            float seed = i * 13.37f + 1.7f;
            int lobes = loCap + (i % (hiCap - loCap + 1));
            Mesh mesh = CloudMeshGenerator.GenerateCluster(lobes, cloudBumpStrength, cloudNoiseScale, seed);
            mesh.name = $"DriftPuffCloud_{i}";
            cloudMeshCache[i] = mesh;
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / lifetime);

        transform.position += currentVelocity * Time.deltaTime;
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, damping * Time.deltaTime);

        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
        float shrink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));

        float scale = Mathf.Lerp(startScale, endScale, grow);
        scale *= Mathf.Lerp(1f, 0.05f, shrink);

        transform.localScale = Vector3.one * scale;
        transform.localScale = Vector3.Scale(transform.localScale, scaleMultiplier);

        transform.Rotate(randomRotationSpeed * Time.deltaTime, Space.Self);

        ApplyColor(t);

        if (t >= 1f)
            Destroy(gameObject);
    }

    private void ApplyColor(float t)
    {
        if (meshRenderer == null || propertyBlock == null)
            return;

        Color color = Color.Lerp(baseYoung, baseOld, Mathf.SmoothStep(0f, 1f, t));

        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}
