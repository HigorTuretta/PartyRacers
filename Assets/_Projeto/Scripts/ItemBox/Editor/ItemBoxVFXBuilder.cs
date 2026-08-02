#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gera o VFX de quebra da caixa como um prefab real e o referencia no ItemBox.
/// O efeito fica em três camadas: lascas de madeira, poeira curta e brilho da coleta.
/// </summary>
public static class ItemBoxVFXBuilder
{
    private const string PrefabPath = "Assets/_Projeto/Prefabs/VFX/VFXItemBoxBreak.prefab";
    private const string ItemBoxPath = "Assets/_Projeto/Prefabs/Pista/ItemBox/ItemBox.prefab";
    private const string MaterialsFolder = "Assets/_Projeto/Materials/VFX";
    private const string TexturesFolder = "Assets/_Projeto/Art/VFX/Generated";

    [MenuItem("Party Racers/VFX/Gerar quebra da caixa de poder")]
    public static void Install()
    {
        EnsureFolder(MaterialsFolder);
        EnsureFolder(TexturesFolder);

        Texture2D dustTexture = CreateParticleTexture(
            TexturesFolder + "/T_ItemBoxDustSoft.png",
            DustPixel);
        Texture2D glintTexture = CreateParticleTexture(
            TexturesFolder + "/T_ItemBoxGlint.png",
            GlintPixel);

        Material wood = GetOrCreateMaterial(
            MaterialsFolder + "/M_ItemBoxWoodChunks.mat",
            FindShader("Universal Render Pipeline/Particles/Lit", "Universal Render Pipeline/Lit", "Standard"),
            Color.white,
            false,
            false);

        Material dust = GetOrCreateMaterial(
            MaterialsFolder + "/M_ItemBoxDust.mat",
            FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Universal Render Pipeline/Unlit"),
            Color.white,
            true,
            false);
        SetParticleTexture(dust, dustTexture);

        Material glint = GetOrCreateMaterial(
            MaterialsFolder + "/M_ItemBoxGlint.mat",
            FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit", "Universal Render Pipeline/Unlit"),
            Color.white,
            true,
            true);
        SetParticleTexture(glint, glintTexture);

        GameObject root = new GameObject("VFXItemBoxBreak");
        try
        {
            CreateWoodChunks(root.transform, wood);
            CreateDust(root.transform, dust);
            CreateGlints(root.transform, glint);
            CreateImpactFlash(root.transform, glint);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        WireItemBox();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ItemBox VFX] Prefab de quebra gerado e ligado ao ItemBox.");
    }

    private static void CreateWoodChunks(Transform parent, Material material)
    {
        ParticleSystem ps = CreateSystem("Wood_Splinters", parent);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 64;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(6.5f, 11.5f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(1.5f, 2.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.42f, 0.16f, 0.035f, 1f),
            new Color(0.90f, 0.49f, 0.13f, 1f));
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.22f, 0.52f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.07f, 0.18f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.07f, 0.17f);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.55f;
        shape.radiusThickness = 1f;

        ParticleSystem.RotationOverLifetimeModule rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.separateAxes = true;
        rotation.x = new ParticleSystem.MinMaxCurve(-7f, 7f);
        rotation.y = new ParticleSystem.MinMaxCurve(-9f, 9f);
        rotation.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = BuiltinCubeMesh();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void CreateDust(Transform parent, Material material)
    {
        ParticleSystem ps = CreateSystem("Dust_Burst", parent);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 56;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 4.6f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.48f, 0.26f, 0.11f, 0.78f),
            new Color(0.82f, 0.61f, 0.34f, 0.58f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 26) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.55f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(
            new Color(0.76f, 0.48f, 0.22f, 0.72f),
            new Color(0.43f, 0.22f, 0.08f, 0f)));

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.28f),
            new Keyframe(0.28f, 1f),
            new Keyframe(1f, 1.35f)));

        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.3f;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 1f;
    }

    private static void CreateGlints(Transform parent, Material material)
    {
        ParticleSystem ps = CreateSystem("Pickup_Glints", parent);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.16f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.8f, 9f);
        main.gravityModifier = -0.12f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.38f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.69f, 0.125f, 1f),
            new Color(1f, 0.97f, 0.88f, 1f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 66f;
        shape.radius = 0.34f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(
            new Color(1f, 0.69f, 0.125f, 1f),
            new Color(1f, 0.97f, 0.88f, 0f)));

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.velocityScale = 0.18f;
        renderer.lengthScale = 1.25f;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 2f;
    }

    private static void CreateImpactFlash(Transform parent, Material material)
    {
        ParticleSystem ps = CreateSystem("Impact_Flash", parent);
        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.12f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 8;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.67f, 0.12f, 1f),
            new Color(1f, 0.96f, 0.78f, 1f));

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 4) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeGradient(
            new Color(1f, 0.82f, 0.32f, 1f),
            new Color(1f, 0.48f, 0.08f, 0f)));

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f),
            new Keyframe(0.18f, 1f),
            new Keyframe(1f, 0.15f)));

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingFudge = 3f;
    }

    private static ParticleSystem CreateSystem(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<ParticleSystem>();
    }

    private static Gradient FadeGradient(Color start, Color end)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(start.a, 0f), new GradientAlphaKey(end.a, 1f) });
        return gradient;
    }

    private static Mesh BuiltinCubeMesh()
    {
        Mesh mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        if (mesh != null)
            return mesh;

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh = cube.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(cube);
        return mesh;
    }

    private static Material GetOrCreateMaterial(
        string path,
        Shader shader,
        Color color,
        bool transparent,
        bool additive)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);

        if (transparent)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", additive ? 2f : 0f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat(
                "_DstBlend",
                (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D CreateParticleTexture(string assetPath, System.Func<float, float, Color> pixel)
    {
        const int size = 96;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = Path.GetFileNameWithoutExtension(assetPath),
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = ((x + 0.5f) / size) * 2f - 1f;
            float v = ((y + 0.5f) / size) * 2f - 1f;
            texture.SetPixel(x, y, pixel(u, v));
        }

        texture.Apply(false, false);
        string absolutePath = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            assetPath.Substring("Assets/".Length)));
        File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(assetPath) is TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static Color DustPixel(float u, float v)
    {
        float radius = Mathf.Sqrt(u * u + v * v);
        float soft = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(radius));
        float breakup = Mathf.Lerp(0.82f, 1f, Mathf.PerlinNoise((u + 1f) * 4.2f, (v + 1f) * 4.2f));
        return new Color(1f, 1f, 1f, Mathf.Pow(soft, 1.35f) * breakup);
    }

    private static Color GlintPixel(float u, float v)
    {
        float horizontal = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 28f)
            * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 1.8f);
        float vertical = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u)), 28f)
            * Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(v)), 1.8f);
        float core = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 5f);
        float alpha = Mathf.Clamp01(Mathf.Max(horizontal, vertical) + core * 0.75f);
        return new Color(1f, 1f, 1f, alpha);
    }

    private static void SetParticleTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        EditorUtility.SetDirty(material);
    }

    private static Shader FindShader(params string[] names)
    {
        foreach (string name in names)
        {
            Shader shader = Shader.Find(name);
            if (shader != null)
                return shader;
        }

        throw new System.InvalidOperationException("Nenhum shader compatível foi encontrado para o VFX da caixa.");
    }

    private static void WireItemBox()
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ItemBoxPath);
        GameObject vfx = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (source == null || vfx == null)
            throw new System.InvalidOperationException("ItemBox ou VFX de quebra não foi encontrado.");

        GameObject contents = PrefabUtility.LoadPrefabContents(ItemBoxPath);
        try
        {
            ItemBox itemBox = contents.GetComponentInChildren<ItemBox>(true);
            if (itemBox == null)
                throw new System.InvalidOperationException("O prefab ItemBox não possui o componente ItemBox.");

            var serialized = new SerializedObject(itemBox);
            serialized.FindProperty("breakVfxPrefab").objectReferenceValue = vfx;
            serialized.FindProperty("breakVfxLifetime").floatValue = 2.2f;
            serialized.FindProperty("breakVfxScale").floatValue = 1.9f;
            serialized.FindProperty("breakVfxOffset").vector3Value = new Vector3(0f, 0.2f, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(contents, ItemBoxPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
