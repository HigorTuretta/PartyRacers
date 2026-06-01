using UnityEngine;

public static class ElectroGelVisualUtility
{
    private static Texture2D cachedSoftCircle;
    private static Texture2D cachedStarSprite;
    private static Texture2D cachedRingSprite;
    private static Texture2D cachedLightningStreak;

    public static Color ColorCyan = new Color(0.35f, 1f, 1f, 1f);
    public static Color ColorViolet = new Color(0.7f, 0.4f, 1f, 1f);
    public static Color ColorWhiteHot = new Color(1f, 1f, 1f, 1f);
    public static Color ColorElectric = new Color(0.55f, 0.95f, 1f, 1f);

    public static Texture2D GetSoftCircle()
    {
        if (cachedSoftCircle != null)
            return cachedSoftCircle;

        const int size = 64;
        cachedSoftCircle = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "ElectroGel_SoftCircle"
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float falloff = Mathf.Clamp01(1f - dist);
                float intensity = falloff * falloff * falloff;
                pixels[y * size + x] = new Color(intensity, intensity, intensity, intensity);
            }
        }

        cachedSoftCircle.SetPixels(pixels);
        cachedSoftCircle.Apply(false, false);
        return cachedSoftCircle;
    }

    public static Texture2D GetStarSprite()
    {
        if (cachedStarSprite != null)
            return cachedStarSprite;

        const int size = 64;
        cachedStarSprite = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "ElectroGel_Star"
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x - center.x, y - center.y);
                float dist = delta.magnitude / maxDistance;
                float angle = Mathf.Atan2(delta.y, delta.x);
                float spikes = Mathf.Abs(Mathf.Cos(angle * 2f)) * 0.6f + 0.4f;
                float falloff = Mathf.Clamp01(1f - dist / spikes);
                float center_glow = Mathf.Clamp01(1f - dist * 1.8f);
                float intensity = Mathf.Max(falloff * falloff * 0.9f, center_glow * center_glow * center_glow);
                pixels[y * size + x] = new Color(intensity, intensity, intensity, intensity);
            }
        }

        cachedStarSprite.SetPixels(pixels);
        cachedStarSprite.Apply(false, false);
        return cachedStarSprite;
    }

    public static Texture2D GetRingSprite()
    {
        if (cachedRingSprite != null)
            return cachedRingSprite;

        const int size = 128;
        cachedRingSprite = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "ElectroGel_Ring"
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float maxDistance = size * 0.5f;
        const float ringCenter = 0.78f;
        const float ringWidth = 0.18f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center) / maxDistance;
                float distFromRing = Mathf.Abs(dist - ringCenter);
                float intensity = Mathf.Clamp01(1f - distFromRing / ringWidth);
                intensity = intensity * intensity;
                pixels[y * size + x] = new Color(intensity, intensity, intensity, intensity);
            }
        }

        cachedRingSprite.SetPixels(pixels);
        cachedRingSprite.Apply(false, false);
        return cachedRingSprite;
    }

    public static Texture2D GetLightningStreak()
    {
        if (cachedLightningStreak != null)
            return cachedLightningStreak;

        const int width = 32;
        const int height = 64;
        cachedLightningStreak = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave,
            name = "ElectroGel_LightningStreak"
        };

        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float distFromCenter = Mathf.Abs(v - 0.5f) * 2f;
            float core = Mathf.Clamp01(1f - distFromCenter * 6f);
            float glow = Mathf.Clamp01(1f - distFromCenter);
            float intensity = Mathf.Max(core, glow * glow * 0.55f);

            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float endFade = Mathf.SmoothStep(0f, 1f, Mathf.Sin(u * Mathf.PI));
                float final = intensity * Mathf.Lerp(0.4f, 1f, endFade);
                pixels[y * width + x] = new Color(final, final, final, final);
            }
        }

        cachedLightningStreak.SetPixels(pixels);
        cachedLightningStreak.Apply(false, false);
        return cachedLightningStreak;
    }

    public static Material InstantiateMaterialWithTexture(Material source, Texture2D texture)
    {
        if (source == null)
            return null;

        Material instance = new Material(source);

        if (texture == null)
            return instance;

        if (instance.HasProperty("_BaseMap"))
            instance.SetTexture("_BaseMap", texture);

        if (instance.HasProperty("_MainTex"))
            instance.SetTexture("_MainTex", texture);

        // Emission map defaults to white in URP/Particles/Unlit, so HDR emission would
        // render over the entire quad regardless of the base map's alpha. Wiring the
        // same procedural sprite into _EmissionMap makes the soft falloff actually
        // gate the glow and stops the visuals from looking like flat white rectangles.
        if (instance.HasProperty("_EmissionMap"))
            instance.SetTexture("_EmissionMap", texture);

        return instance;
    }

    public static void ConfigureSparks(
        ParticleSystem sparks,
        Material material,
        float rateOverTime,
        float lifetime,
        float radius)
    {
        Configure(sparks, material, rateOverTime, lifetime, radius, true);
    }

    public static void ConfigureBurst(
        ParticleSystem particles,
        Material material,
        int burstCount,
        float lifetime,
        float speed,
        float radius,
        float startSize,
        float duration,
        Color colorA,
        Color colorB)
    {
        ParticleSystem.MainModule main = particles.main;
        main.duration = duration;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.55f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(startSize * 0.6f, startSize);
        main.startColor = new ParticleSystem.MinMaxGradient(colorA, colorB);
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(64, burstCount * 2);

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystem.ColorOverLifetimeModule colorOverLife = particles.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.7f),
                new GradientColorKey(Color.white, 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.18f),
                new GradientAlphaKey(0.65f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = fade;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = particles.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.25f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.VelocityOverLifetimeModule velocityOverLife = particles.velocityOverLifetime;
        velocityOverLife.enabled = true;
        velocityOverLife.space = ParticleSystemSimulationSpace.World;
        AnimationCurve drag = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );
        velocityOverLife.speedModifier = new ParticleSystem.MinMaxCurve(1f, drag);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 1.4f;
        noise.scrollSpeed = 1.6f;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = InstantiateMaterialWithTexture(material, GetSoftCircle());
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.55f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        particles.Play(true);
    }

    public static void ConfigureStarOrbit(
        ParticleSystem particles,
        Material material,
        float rateOverTime,
        float lifetime,
        float radius)
    {
        Configure(particles, material, rateOverTime, lifetime, radius, false);

        ParticleSystem.MainModule main = particles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);

        ParticleSystem.NoiseModule noise = particles.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.6f;

        ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.material = InstantiateMaterialWithTexture(material, GetStarSprite());
    }

    private static void Configure(
        ParticleSystem sparks,
        Material material,
        float rateOverTime,
        float lifetime,
        float radius,
        bool useSoftCircle)
    {
        ParticleSystem.MainModule main = sparks.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.7f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(ColorCyan, ColorViolet);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 256;

        ParticleSystem.EmissionModule emission = sparks.emission;
        emission.rateOverTime = rateOverTime;

        ParticleSystem.ShapeModule shape = sparks.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystem.ColorOverLifetimeModule colorOverLife = sparks.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 1f, 1f), 0f),
                new GradientColorKey(ColorCyan, 0.5f),
                new GradientColorKey(ColorViolet, 1f)
            },
            new[] {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(0.7f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = gradient;

        ParticleSystem.SizeOverLifetimeModule sizeOverLife = sparks.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0f)
        );
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.NoiseModule noise = sparks.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 1.1f;
        noise.scrollSpeed = 1.2f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        ParticleSystem.RotationOverLifetimeModule rotation = sparks.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-120f, 240f);

        ParticleSystemRenderer renderer = sparks.GetComponent<ParticleSystemRenderer>();
        renderer.material = InstantiateMaterialWithTexture(material, useSoftCircle ? GetSoftCircle() : GetStarSprite());
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 0.55f;
    }

    public static Light AttachGlowLight(
        Transform parent,
        Color color,
        float intensity,
        float range,
        Vector3 localOffset)
    {
        GameObject lightObject = new GameObject("ElectroGel_GlowLight");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localOffset;

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.bounceIntensity = 0f;
        light.renderMode = LightRenderMode.Auto;

        return light;
    }

    public static Transform BuildBillboardHalo(
        Transform parent,
        Material baseMaterial,
        Texture2D sprite,
        Vector2 size,
        Color tint)
    {
        Transform halo = BuildFlatQuad(parent, baseMaterial, sprite, size, tint, "ElectroGel_Halo");
        halo.gameObject.AddComponent<ElectroGelBillboard>();
        return halo;
    }

    public static Transform BuildFlatQuad(
        Transform parent,
        Material baseMaterial,
        Texture2D sprite,
        Vector2 size,
        Color tint,
        string objectName)
    {
        GameObject halo = new GameObject(objectName);
        halo.transform.SetParent(parent, false);

        MeshFilter meshFilter = halo.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = GetQuadMesh();

        MeshRenderer renderer = halo.AddComponent<MeshRenderer>();
        Material instance = InstantiateMaterialWithTexture(baseMaterial, sprite);
        if (instance != null && instance.HasProperty("_BaseColor"))
            instance.SetColor("_BaseColor", tint);
        renderer.sharedMaterial = instance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        halo.transform.localScale = new Vector3(size.x, size.y, 1f);

        return halo.transform;
    }

    private static Mesh cachedQuad;

    public static Mesh GetQuadMesh()
    {
        if (cachedQuad != null)
            return cachedQuad;

        cachedQuad = new Mesh
        {
            name = "ElectroGel_BillboardQuad",
            hideFlags = HideFlags.HideAndDontSave,
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f)
            },
            triangles = new[] { 0, 2, 1, 0, 3, 2 },
            normals = new[]
            {
                Vector3.back,
                Vector3.back,
                Vector3.back,
                Vector3.back
            }
        };

        cachedQuad.RecalculateBounds();
        return cachedQuad;
    }
}
