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

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // A cor da nuvem era aplicada com MaterialPropertyBlock por puff, por frame. Isso desliga o SRP
    // Batcher para cada puff: medido em playmode, os ~900 puffs vivos custavam 1246 draw calls e 885
    // setPass calls extras — cerca de 11 ms dos 19 ms do frame, mais que todo o resto da cena junta.
    //
    // A troca: em vez de uma cor contínua por puff, uma RAMPA de materiais compartilhados. Todos os
    // puffs no mesmo estágio de vida usam o MESMO material, então o SRP Batcher volta a agrupá-los e
    // o custo cai para alguns setPass no total. Com EstagiosDeCor estágios ao longo de uma vida de
    // ~0,7 s, o degrau entre estágios dura ~0,09 s e não é perceptível numa fumaça que já está
    // crescendo e sumindo.
    private const int EstagiosDeCor = 8;
    private static Material[] rampaNormal;
    private static Material[] rampaBurnout;
    private static Material materialModelo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimparRampas()
    {
        rampaNormal = null;
        rampaBurnout = null;
        materialModelo = null;
    }

    private Material[] rampaAtual;
    private int estagioAplicado = -1;

    private float timer;
    private Vector3 currentVelocity;
    private Vector3 randomRotationSpeed;
    private Vector3 scaleMultiplier;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private DriftPuffBubble prefabDoPool;

    /// <summary>
    /// Marca este puff como reciclável e guarda de qual prefab ele saiu, para o
    /// <see cref="DriftPuffPool"/> devolvê-lo à pilha certa no fim da vida.
    /// </summary>
    public void MarcarComoDoPool(DriftPuffBubble prefab)
    {
        prefabDoPool = prefab;
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

        // Sorteia a variação de nuvem a cada spawn (e não uma vez no Awake): com o pool ligado,
        // um puff reciclado ficaria preso na mesma silhueta pelo resto da partida.
        if (generateCloudMesh)
            ApplyCloudMesh();

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
        meshFilter = GetComponent<MeshFilter>();

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

        if (rampaAtual == null)
            SetupColors(false);
    }

    private void SetupColors(bool isBurnout)
    {
        rampaAtual = GarantirRampa(isBurnout);
        estagioAplicado = -1;
    }

    /// <summary>
    /// Constrói (uma vez por sessão) a rampa de materiais compartilhados que substitui o
    /// MaterialPropertyBlock por puff. Os materiais saem do próprio material do prefab, então
    /// shader, textura e demais parâmetros continuam idênticos — só a cor varia entre os estágios.
    /// </summary>
    private Material[] GarantirRampa(bool isBurnout)
    {
        Material[] cache = isBurnout ? rampaBurnout : rampaNormal;
        if (cache != null)
            return cache;

        // O modelo é capturado UMA vez, antes de qualquer puff trocar o próprio material: se fosse
        // relido depois, a rampa de burnout acabaria derivada de um material já da rampa normal.
        if (materialModelo == null)
        {
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            materialModelo = meshRenderer != null ? meshRenderer.sharedMaterial : null;
        }

        Material modelo = materialModelo;
        if (modelo == null)
            return null;

        Color fim = isBurnout ? burnoutTint : oldColor;
        cache = new Material[EstagiosDeCor];

        for (int i = 0; i < EstagiosDeCor; i++)
        {
            float t = EstagiosDeCor == 1 ? 0f : i / (float)(EstagiosDeCor - 1);
            Color cor = Color.Lerp(youngColor, fim, Mathf.SmoothStep(0f, 1f, t));

            var m = new Material(modelo)
            {
                name = $"{modelo.name}_{(isBurnout ? "burnout" : "drift")}_{i}",
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true,
            };

            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, cor);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, cor);

            cache[i] = m;
        }

        if (isBurnout) rampaBurnout = cache;
        else rampaNormal = cache;

        return cache;
    }

    // Cache estático de meshes de nuvem: antes cada puff GERAVA um mesh procedural novo no Awake
    // (custo de CPU) e o atribuía via 'mf.mesh', que clona/vaza um Mesh por puff (GC pesado). Com
    // 16 karts derrapando isso causava engasgos. Agora pré-geramos algumas variações UMA vez e
    // reaproveitamos via sharedMesh — visual praticamente idêntico, custo perto de zero.
    private const int CloudMeshVariations = 16;
    private static Mesh[] cloudMeshCache;

    private void ApplyCloudMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        if (meshFilter == null)
            return;

        EnsureCloudMeshCache();
        meshFilter.sharedMesh = cloudMeshCache[Random.Range(0, cloudMeshCache.Length)];
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

        if (t < 1f)
            return;

        if (prefabDoPool != null)
            DriftPuffPool.Devolver(this, prefabDoPool);
        else
            Destroy(gameObject);
    }

    private void ApplyColor(float t)
    {
        if (meshRenderer == null || rampaAtual == null)
            return;

        int estagio = Mathf.Clamp(Mathf.RoundToInt(t * (EstagiosDeCor - 1)), 0, EstagiosDeCor - 1);

        // Só toca no renderer quando o estágio realmente muda: na maioria dos frames isto é um
        // comparativo de int e nada mais.
        if (estagio == estagioAplicado)
            return;

        estagioAplicado = estagio;
        meshRenderer.sharedMaterial = rampaAtual[estagio];
    }
}
