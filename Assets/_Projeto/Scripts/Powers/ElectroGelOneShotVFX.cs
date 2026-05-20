using UnityEngine;

public enum ElectroGelVFXKind
{
    FireBurst,
    Bounce,
    Hit,
    ShieldBlock,
    Expire
}

[DisallowMultipleComponent]
public class ElectroGelOneShotVFX : MonoBehaviour
{
    [Header("Tipo")]
    [SerializeField] private ElectroGelVFXKind kind = ElectroGelVFXKind.Hit;

    [Header("Materiais")]
    [SerializeField] private Material bubbleMaterial;
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Material sparksMaterial;
    [SerializeField] private Material ringMaterial;
    [SerializeField] private Material lightningMaterial;

    [Header("Tempo")]
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private float scale = 1f;

    private float age;
    private Transform flashRoot;
    private Transform flashSphere;
    private Transform shockwave;
    private Transform haloSprite;
    private Material shockwaveMaterial;
    private Material haloMaterial;
    private Material flashMaterial;
    private Light glowLight;
    private float glowMaxIntensity;
    private float endScale;
    private float ringRadius;

    private void Awake()
    {
        endScale = GetEndScale();
        ringRadius = GetRingRadius();
        Build();
        Destroy(gameObject, lifetime + 0.45f);
    }

    private void OnDestroy()
    {
        if (shockwaveMaterial != null)
            Destroy(shockwaveMaterial);
        if (haloMaterial != null)
            Destroy(haloMaterial);
        if (flashMaterial != null)
            Destroy(flashMaterial);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
        float ease = Mathf.SmoothStep(0f, 1f, t);
        float fade = 1f - t;

        if (flashSphere != null)
        {
            float flashScale = Mathf.Lerp(0.25f * scale, GetFlashScale() * scale, ease);
            flashSphere.localScale = Vector3.one * flashScale;
            if (flashMaterial != null && flashMaterial.HasProperty("_BaseColor"))
            {
                Color c = flashMaterial.GetColor("_BaseColor");
                c.a = fade;
                flashMaterial.SetColor("_BaseColor", c);
            }
            if (flashMaterial != null && flashMaterial.HasProperty("_EmissionColor"))
            {
                Color baseEmission = GetFlashEmissiveColor();
                flashMaterial.SetColor("_EmissionColor", baseEmission * Mathf.Lerp(1f, 0.1f, ease));
            }
        }

        if (shockwave != null)
        {
            float ringScale = Mathf.Lerp(0.2f, ringRadius * 2.6f * scale, ease);
            shockwave.localScale = new Vector3(ringScale, ringScale, 1f);

            if (shockwaveMaterial != null && shockwaveMaterial.HasProperty("_BaseColor"))
            {
                Color c = shockwaveMaterial.GetColor("_BaseColor");
                c.a = Mathf.Lerp(1f, 0f, t);
                shockwaveMaterial.SetColor("_BaseColor", c);
            }
            if (shockwaveMaterial != null && shockwaveMaterial.HasProperty("_EmissionColor"))
            {
                Color ringEmission = GetRingEmissiveColor();
                shockwaveMaterial.SetColor("_EmissionColor", ringEmission * Mathf.Lerp(1f, 0.05f, ease));
            }
        }

        if (haloSprite != null)
        {
            float haloScale = Mathf.Lerp(0.2f, endScale * 2.2f, ease);
            haloSprite.localScale = new Vector3(haloScale, haloScale, 1f);
            if (haloMaterial != null && haloMaterial.HasProperty("_BaseColor"))
            {
                Color c = haloMaterial.GetColor("_BaseColor");
                c.a = Mathf.Lerp(1f, 0f, t * 1.2f);
                haloMaterial.SetColor("_BaseColor", c);
            }
        }

        if (glowLight != null)
        {
            glowLight.intensity = glowMaxIntensity * Mathf.Lerp(1f, 0f, ease);
        }
    }

    private void Build()
    {
        flashRoot = new GameObject("ElectroGelVFX_Root").transform;
        flashRoot.SetParent(transform, false);

        CreateFlashSphere();
        CreateShockwave();
        CreateHalo();
        CreateLightningBurst();
        CreateParticleBurst();
        CreateLight();
    }

    private void CreateFlashSphere()
    {
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "GelFlash";
        flash.transform.SetParent(flashRoot, false);
        flash.transform.localScale = Vector3.one * 0.25f * scale;

        Collider col = flash.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer renderer = flash.GetComponent<Renderer>();
        Material baseFlashMat = kind == ElectroGelVFXKind.ShieldBlock ? coreMaterial : (coreMaterial != null ? coreMaterial : bubbleMaterial);
        if (baseFlashMat != null)
        {
            flashMaterial = new Material(baseFlashMat);
            if (flashMaterial.HasProperty("_EmissionColor"))
                flashMaterial.SetColor("_EmissionColor", GetFlashEmissiveColor());
            renderer.sharedMaterial = flashMaterial;
        }
        else
        {
            renderer.sharedMaterial = bubbleMaterial;
        }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        flashSphere = flash.transform;
    }

    private void CreateShockwave()
    {
        Material baseMat = ringMaterial != null ? ringMaterial : sparksMaterial;
        if (baseMat == null)
            return;

        shockwaveMaterial = new Material(baseMat);
        if (shockwaveMaterial.HasProperty("_EmissionColor"))
            shockwaveMaterial.SetColor("_EmissionColor", GetRingEmissiveColor());

        GameObject ringObject = new GameObject("Shockwave");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localRotation = Quaternion.identity;
        MeshFilter meshFilter = ringObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ElectroGelVisualUtility.GetQuadMesh();

        MeshRenderer renderer = ringObject.AddComponent<MeshRenderer>();
        Material withTexture = ElectroGelVisualUtility.InstantiateMaterialWithTexture(shockwaveMaterial, ElectroGelVisualUtility.GetRingSprite());
        Destroy(shockwaveMaterial);
        shockwaveMaterial = withTexture;
        renderer.sharedMaterial = shockwaveMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        ringObject.AddComponent<ElectroGelBillboard>();
        shockwave = ringObject.transform;
    }

    private void CreateHalo()
    {
        Material baseMat = coreMaterial != null ? coreMaterial : sparksMaterial;
        if (baseMat == null)
            return;

        haloMaterial = new Material(baseMat);
        if (haloMaterial.HasProperty("_EmissionColor"))
            haloMaterial.SetColor("_EmissionColor", GetRingEmissiveColor() * 1.2f);

        Material withTexture = ElectroGelVisualUtility.InstantiateMaterialWithTexture(haloMaterial, ElectroGelVisualUtility.GetSoftCircle());
        Destroy(haloMaterial);
        haloMaterial = withTexture;

        GameObject haloObject = new GameObject("Halo");
        haloObject.transform.SetParent(transform, false);
        MeshFilter meshFilter = haloObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = ElectroGelVisualUtility.GetQuadMesh();

        MeshRenderer renderer = haloObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = haloMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        haloObject.AddComponent<ElectroGelBillboard>();
        haloSprite = haloObject.transform;
    }

    private void CreateLightningBurst()
    {
        Material lightningUse = lightningMaterial != null ? lightningMaterial : (sparksMaterial != null ? sparksMaterial : coreMaterial);
        if (lightningUse == null)
            return;

        int boltCount = kind switch
        {
            ElectroGelVFXKind.FireBurst => 4,
            ElectroGelVFXKind.Bounce => 3,
            ElectroGelVFXKind.Hit => 7,
            ElectroGelVFXKind.ShieldBlock => 6,
            ElectroGelVFXKind.Expire => 4,
            _ => 4
        };

        float radius = endScale * 0.9f;

        Color colorA = new Color(0.5f, 1f, 1f, 1f);
        Color colorB = new Color(0.85f, 0.5f, 1f, 1f);

        if (kind == ElectroGelVFXKind.ShieldBlock)
        {
            colorA = new Color(0.7f, 1f, 1f, 1f);
            colorB = new Color(0.85f, 0.95f, 1f, 1f);
        }

        ElectroGelLightning.Attach(
            transform,
            lightningUse,
            ElectroGelLightning.BoltShape.RadialBurst,
            boltCount,
            radius,
            radius * 0.25f,
            0.07f * scale,
            0.012f * scale,
            0.035f,
            colorA,
            colorB,
            4f,
            lifetime * 0.85f
        );
    }

    private void CreateParticleBurst()
    {
        int count;
        float particleLifetime;
        float speed;
        float radius;
        float startSize;

        switch (kind)
        {
            case ElectroGelVFXKind.FireBurst:
                count = 34;
                particleLifetime = 0.4f;
                speed = 3f;
                radius = 0.4f;
                startSize = 0.15f;
                break;
            case ElectroGelVFXKind.Bounce:
                count = 18;
                particleLifetime = 0.28f;
                speed = 1.7f;
                radius = 0.25f;
                startSize = 0.12f;
                break;
            case ElectroGelVFXKind.Hit:
                count = 52;
                particleLifetime = 0.5f;
                speed = 3.5f;
                radius = 0.55f;
                startSize = 0.18f;
                break;
            case ElectroGelVFXKind.ShieldBlock:
                count = 44;
                particleLifetime = 0.42f;
                speed = 2.6f;
                radius = 0.5f;
                startSize = 0.15f;
                break;
            case ElectroGelVFXKind.Expire:
                count = 30;
                particleLifetime = 0.45f;
                speed = 1.9f;
                radius = 0.45f;
                startSize = 0.13f;
                break;
            default:
                count = 26;
                particleLifetime = 0.4f;
                speed = 2f;
                radius = 0.4f;
                startSize = 0.14f;
                break;
        }

        Color colorA = new Color(0.55f, 1f, 1f, 1f);
        Color colorB = new Color(0.85f, 0.45f, 1f, 1f);

        if (kind == ElectroGelVFXKind.ShieldBlock)
        {
            colorA = new Color(0.95f, 1f, 1f, 1f);
            colorB = new Color(0.55f, 0.95f, 1f, 1f);
        }

        GameObject particlesObject = new GameObject("GelSparks");
        particlesObject.transform.SetParent(transform, false);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        ElectroGelVisualUtility.ConfigureBurst(
            particles,
            sparksMaterial,
            count,
            particleLifetime,
            speed * scale,
            radius * scale,
            startSize * scale,
            lifetime,
            colorA,
            colorB
        );
    }

    private void CreateLight()
    {
        glowMaxIntensity = kind switch
        {
            ElectroGelVFXKind.FireBurst => 3.5f,
            ElectroGelVFXKind.Bounce => 2f,
            ElectroGelVFXKind.Hit => 5f,
            ElectroGelVFXKind.ShieldBlock => 4f,
            ElectroGelVFXKind.Expire => 2.5f,
            _ => 2.5f
        };

        glowLight = ElectroGelVisualUtility.AttachGlowLight(
            transform,
            new Color(0.5f, 0.9f, 1f),
            glowMaxIntensity,
            endScale * 6f,
            Vector3.zero
        );
    }

    private float GetEndScale()
    {
        return kind switch
        {
            ElectroGelVFXKind.FireBurst => 1.2f * scale,
            ElectroGelVFXKind.Bounce => 0.75f * scale,
            ElectroGelVFXKind.Hit => 1.55f * scale,
            ElectroGelVFXKind.ShieldBlock => 1.35f * scale,
            ElectroGelVFXKind.Expire => 1.05f * scale,
            _ => scale
        };
    }

    private float GetFlashScale()
    {
        return kind switch
        {
            ElectroGelVFXKind.FireBurst => 0.95f,
            ElectroGelVFXKind.Bounce => 0.5f,
            ElectroGelVFXKind.Hit => 1.25f,
            ElectroGelVFXKind.ShieldBlock => 1.05f,
            ElectroGelVFXKind.Expire => 0.8f,
            _ => 0.6f
        };
    }

    private float GetRingRadius()
    {
        return kind switch
        {
            ElectroGelVFXKind.FireBurst => 0.6f * scale,
            ElectroGelVFXKind.Bounce => 0.42f * scale,
            ElectroGelVFXKind.Hit => 0.9f * scale,
            ElectroGelVFXKind.ShieldBlock => 1.1f * scale,
            ElectroGelVFXKind.Expire => 0.68f * scale,
            _ => 0.55f * scale
        };
    }

    private Color GetFlashEmissiveColor()
    {
        return kind switch
        {
            ElectroGelVFXKind.ShieldBlock => new Color(2.5f, 4f, 5.5f, 1f),
            ElectroGelVFXKind.Hit => new Color(1.8f, 4.5f, 6f, 1f),
            _ => new Color(1.5f, 4f, 5.5f, 1f)
        };
    }

    private Color GetRingEmissiveColor()
    {
        return kind switch
        {
            ElectroGelVFXKind.ShieldBlock => new Color(1.5f, 3.5f, 5f, 1f),
            ElectroGelVFXKind.Hit => new Color(1.2f, 4f, 5.5f, 1f),
            ElectroGelVFXKind.FireBurst => new Color(1f, 3.5f, 4.5f, 1f),
            ElectroGelVFXKind.Bounce => new Color(0.9f, 3f, 4f, 1f),
            ElectroGelVFXKind.Expire => new Color(0.8f, 2.5f, 4f, 1f),
            _ => new Color(1f, 3f, 4.5f, 1f)
        };
    }
}
