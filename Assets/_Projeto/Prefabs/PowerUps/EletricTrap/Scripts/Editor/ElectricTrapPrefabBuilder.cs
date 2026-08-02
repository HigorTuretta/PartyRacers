#if UNITY_EDITOR
using System;
using PartyRacers.UI.Settings;
using UnityEditor;
using UnityEngine;

public static class ElectricTrapPrefabBuilder
{
    public const string RootFolder = "Assets/_Projeto/Prefabs/PowerUps/EletricTrap";
    public const string TrapPrefabPath = RootFolder + "/Prefabs/ElectricTrap.prefab";
    public const string ImpactPrefabPath = RootFolder + "/VFX/ElectricTrap_Impact.prefab";
    public const string AuraPrefabPath = RootFolder + "/VFX/ElectricTrap_TargetAura.prefab";
    public const string ArmedPrefabPath = RootFolder + "/VFX/ElectricTrap_Armed.prefab";
    public const string DefinitionPath = RootFolder + "/Config/Resources/PowerDefinitions/Power_ArmadilhaEletrica.asset";

    private const string ModelPath = RootFolder + "/EletricTrap.fbx";
    private const string ModelTexturePath = RootFolder + "/EletricTrap.jpg";
    private const string BodyMaterialPath = RootFolder + "/Materials/M_ElectricTrapBody.mat";
    private const string WarningMaterialPath = RootFolder + "/Materials/M_ElectricTrapWarning.mat";
    private const string PhysicsMaterialPath = RootFolder + "/Materials/PM_ElectricTrapNoBounce.physicMaterial";
    private const string RuntimeMaterialFolder = RootFolder + "/Materials/HovlRuntime";
    private const string ElectroHitPath = "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Electro hit.prefab";
    private const string SmokeExplosionPath = "Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Smoke AOE explosion.prefab";
    private const string LightningAuraPath = "Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Lightning aura.prefab";
    private const string LocalKartPrefabPath = "Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab";
    private const string NetworkKartPrefabPath = "Assets/_Projeto/Prefabs/Cars/PlayerKart_Network.prefab";

    [MenuItem("Party Racers/Power Ups/Instalar Armadilha Elétrica")]
    public static void Install()
    {
        EnsureFolderTree();

        GameObject model = RequireAsset<GameObject>(ModelPath);
        GameObject electroHit = RequireAsset<GameObject>(ElectroHitPath);
        GameObject smokeExplosion = RequireAsset<GameObject>(SmokeExplosionPath);
        GameObject lightningAura = RequireAsset<GameObject>(LightningAuraPath);

        Material bodyMaterial = CreateOrUpdateBodyMaterial();
        Material warningMaterial = CreateOrUpdateWarningMaterial();
        PhysicsMaterial physicsMaterial = CreateOrUpdatePhysicsMaterial();

        GameObject impactPrefab = CreateImpactWrapper(electroHit, smokeExplosion);
        GameObject auraPrefab = CreateAuraWrapper(lightningAura);
        GameObject armedPrefab = CreateArmedWrapper(lightningAura);
        GameObject trapPrefab = CreateTrapPrefab(
            model,
            bodyMaterial,
            warningMaterial,
            physicsMaterial,
            impactPrefab,
            auraPrefab,
            armedPrefab);

        CreateOrUpdateDefinition();
        WireKartPrefab(LocalKartPrefabPath, trapPrefab);
        WireKartPrefab(NetworkKartPrefabPath, trapPrefab);

        AssetDatabase.SetLabels(trapPrefab, new[] { "PowerUp", "ElectricTrap", "RuntimeReady" });
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = trapPrefab;
        Debug.Log("[ElectricTrap] Prefab, VFX, definição de HUD e karts instalados com sucesso.", trapPrefab);
    }

    private static void EnsureFolderTree()
    {
        EnsureFolder("Assets/_Projeto/Prefabs/PowerUps", "EletricTrap");
        EnsureFolder(RootFolder, "Prefabs");
        EnsureFolder(RootFolder, "Scripts");
        EnsureFolder(RootFolder, "Animations");
        EnsureFolder(RootFolder, "VFX");
        EnsureFolder(RootFolder, "Materials");
        EnsureFolder(RootFolder + "/Materials", "HovlRuntime");
        EnsureFolder(RootFolder, "Audio");
        EnsureFolder(RootFolder, "Config");
        EnsureFolder(RootFolder, "Tests");
        EnsureFolder(RootFolder + "/Scripts", "Editor");
        EnsureFolder(RootFolder + "/Config", "Resources");
        EnsureFolder(RootFolder + "/Config/Resources", "PowerDefinitions");
        EnsureFolder(RootFolder + "/Tests", "Editor");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static T RequireAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("Asset obrigatório não encontrado: " + path);
        return asset;
    }

    private static Material CreateOrUpdateWarningMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(WarningMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader) { name = "M_ElectricTrapWarning" };
            AssetDatabase.CreateAsset(material, WarningMaterialPath);
        }

        Color baseColor = new Color(0.55f, 0.008f, 0.004f, 1f);
        Color emission = new Color(2.4f, 0.025f, 0.01f, 1f);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", baseColor);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", emission);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateBodyMaterial()
    {
        Texture2D atlas = RequireAsset<Texture2D>(ModelTexturePath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader) { name = "M_ElectricTrapBody" };
            AssetDatabase.CreateAsset(material, BodyMaterialPath);
        }

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", atlas);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", atlas);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0.08f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.28f);

        EditorUtility.SetDirty(material);
        return material;
    }

    private static PhysicsMaterial CreateOrUpdatePhysicsMaterial()
    {
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(PhysicsMaterialPath);
        if (material == null)
        {
            material = new PhysicsMaterial("PM_ElectricTrapNoBounce");
            AssetDatabase.CreateAsset(material, PhysicsMaterialPath);
        }

        material.dynamicFriction = 0.88f;
        material.staticFriction = 0.95f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Maximum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateImpactWrapper(GameObject electroHit, GameObject smokeExplosion)
    {
        GameObject root = new GameObject("ElectricTrap_Impact");
        try
        {
            AddNestedPrefab(root.transform, electroHit, "ElectroHit", new Vector3(0f, 0.1f, 0f), Vector3.one * 1.05f);
            AddNestedPrefab(root.transform, smokeExplosion, "SmokeAOE", Vector3.zero, Vector3.one * 0.72f);
            RemoveVfxColliders(root);
            UseLocalRuntimeMaterials(root);
            return PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateAuraWrapper(GameObject lightningAura)
    {
        GameObject root = new GameObject("ElectricTrap_TargetAura");
        try
        {
            // O sistema de partículas original tem a esfera centrada em Y=1. O wrapper recentra
            // essa esfera na origem para que o ajuste automático envolva o kart inteiro.
            AddNestedPrefab(root.transform, lightningAura, "LightningAura", Vector3.down, Vector3.one);
            RemoveVfxColliders(root);
            UseLocalRuntimeMaterials(root);
            return PrefabUtility.SaveAsPrefabAsset(root, AuraPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateArmedWrapper(GameObject lightningAura)
    {
        GameObject root = new GameObject("ElectricTrap_Armed");
        try
        {
            const float scale = 0.42f;
            AddNestedPrefab(root.transform, lightningAura, "ArmedElectricField", Vector3.down * scale, Vector3.one * scale);
            RemoveVfxColliders(root);
            UseLocalRuntimeMaterials(root);
            return PrefabUtility.SaveAsPrefabAsset(root, ArmedPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void AddNestedPrefab(
        Transform parent,
        GameObject source,
        string instanceName,
        Vector3 localPosition,
        Vector3 localScale)
    {
        GameObject nested = PrefabUtility.InstantiatePrefab(source) as GameObject;
        if (nested == null)
            nested = UnityEngine.Object.Instantiate(source);

        nested.name = instanceName;
        nested.transform.SetParent(parent, false);
        nested.transform.localPosition = localPosition;
        nested.transform.localRotation = Quaternion.identity;
        nested.transform.localScale = localScale;
    }

    private static void RemoveVfxColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            UnityEngine.Object.DestroyImmediate(colliders[i]);
    }

    private static void UseLocalRuntimeMaterials(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            bool changed = false;
            for (int j = 0; j < materials.Length; j++)
            {
                Material source = materials[j];
                string sourcePath = AssetDatabase.GetAssetPath(source);
                if (source == null || string.IsNullOrEmpty(sourcePath) ||
                    !sourcePath.StartsWith("Assets/Hovl Studio/", StringComparison.Ordinal))
                {
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(sourcePath);
                string safeName = source.name.Replace(' ', '_').Replace('/', '_').Replace('\\', '_');
                string localPath = RuntimeMaterialFolder + "/M_ET_" + safeName + "_" + guid.Substring(0, 8) + ".mat";
                Material local = AssetDatabase.LoadAssetAtPath<Material>(localPath);
                if (local == null)
                {
                    local = new Material(source.shader);
                    AssetDatabase.CreateAsset(local, localPath);
                }

                EditorUtility.CopySerialized(source, local);
                local.name = "M_ET_" + source.name;
                EditorUtility.SetDirty(local);
                materials[j] = local;
                changed = true;
            }

            if (changed)
                renderers[i].sharedMaterials = materials;
        }
    }

    private static GameObject CreateTrapPrefab(
        GameObject model,
        Material bodyMaterial,
        Material warningMaterial,
        PhysicsMaterial physicsMaterial,
        GameObject impactPrefab,
        GameObject auraPrefab,
        GameObject armedPrefab)
    {
        GameObject root = new GameObject("ElectricTrap");
        try
        {
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1.8f;
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 1.25f;
            body.angularDamping = 4.5f;
            body.constraints = RigidbodyConstraints.FreezeAll;

            BoxCollider physicalCollider = root.AddComponent<BoxCollider>();
            physicalCollider.center = new Vector3(0f, -0.015f, 0f);
            physicalCollider.size = new Vector3(0.84f, 0.5f, 0.86f);
            physicalCollider.material = physicsMaterial;
            physicalCollider.enabled = false;

            Transform visualRoot = new GameObject("VisualRoot").transform;
            visualRoot.SetParent(root.transform, false);

            GameObject modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (modelInstance == null)
                modelInstance = UnityEngine.Object.Instantiate(model);
            modelInstance.name = "ElectricTrap_Model";
            modelInstance.transform.SetParent(visualRoot, false);
            modelInstance.transform.localPosition = Vector3.zero;
            modelInstance.transform.localRotation = Quaternion.identity;
            modelInstance.transform.localScale = Vector3.one;
            RemoveModelPhysics(modelInstance);
            ApplyBodyMaterial(modelInstance, bodyMaterial);

            GameObject lens = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lens.name = "WarningLens_Emission";
            lens.transform.SetParent(visualRoot, false);
            lens.transform.localPosition = new Vector3(0f, 0.345f, 0f);
            lens.transform.localRotation = Quaternion.identity;
            lens.transform.localScale = new Vector3(0.34f, 0.035f, 0.34f);
            UnityEngine.Object.DestroyImmediate(lens.GetComponent<Collider>());
            MeshRenderer warningRenderer = lens.GetComponent<MeshRenderer>();
            warningRenderer.sharedMaterial = warningMaterial;

            GameObject lightObject = new GameObject("WarningLight_Red");
            lightObject.transform.SetParent(visualRoot, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            Light warningLight = lightObject.AddComponent<Light>();
            warningLight.type = LightType.Point;
            warningLight.color = new Color(1f, 0.025f, 0.01f, 1f);
            warningLight.intensity = 0f;
            warningLight.range = 3.25f;
            warningLight.shadows = LightShadows.None;
            warningLight.renderMode = LightRenderMode.Auto;
            warningLight.enabled = false;

            GameObject armedVfx = PrefabUtility.InstantiatePrefab(armedPrefab) as GameObject;
            if (armedVfx == null)
                armedVfx = UnityEngine.Object.Instantiate(armedPrefab);
            armedVfx.name = "ArmedElectricField_VFX";
            armedVfx.transform.SetParent(visualRoot, false);
            armedVfx.transform.localPosition = Vector3.zero;
            armedVfx.transform.localRotation = Quaternion.identity;
            armedVfx.transform.localScale = Vector3.one;
            armedVfx.SetActive(false);

            GameObject triggerObject = new GameObject("ActivationTrigger");
            triggerObject.transform.SetParent(root.transform, false);
            SphereCollider activationTrigger = triggerObject.AddComponent<SphereCollider>();
            activationTrigger.isTrigger = true;
            activationTrigger.radius = 1.65f;
            activationTrigger.enabled = false;

            ElectricTrapPower power = root.AddComponent<ElectricTrapPower>();
            SerializedObject serializedPower = new SerializedObject(power);
            SetObject(serializedPower, "body", body);
            SetObject(serializedPower, "physicalCollider", physicalCollider);
            SetObject(serializedPower, "activationTrigger", activationTrigger);
            SetObject(serializedPower, "visualRoot", visualRoot);
            SetObject(serializedPower, "armedVfx", armedVfx);
            SetObject(serializedPower, "warningLight", warningLight);
            SetObject(serializedPower, "warningRenderer", warningRenderer);
            SetObject(serializedPower, "impactVfxPrefab", impactPrefab);
            SetObject(serializedPower, "auraVfxPrefab", auraPrefab);
            SetFloat(serializedPower, "speedMultiplier", 0.5f);
            SetFloat(serializedPower, "shockDuration", 3f);
            SetFloat(serializedPower, "armingDelay", 0.5f);
            SetFloat(serializedPower, "triggerRadius", 1.65f);
            SetFloat(serializedPower, "groundOffset", 0.33f);
            SetFloat(serializedPower, "warningLightIntensity", 2.1f);
            SetFloat(serializedPower, "warningLightRange", 3.25f);
            SetFloat(serializedPower, "armedBlinkSpeed", 5.5f);
            SetFloat(serializedPower, "auraScale", 1.08f);
            SerializedProperty canHitOwner = serializedPower.FindProperty("canHitOwner");
            if (canHitOwner != null)
                canHitOwner.boolValue = false;
            serializedPower.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, TrapPrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void RemoveModelPhysics(GameObject model)
    {
        Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            UnityEngine.Object.DestroyImmediate(colliders[i]);

        Rigidbody[] bodies = model.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
            UnityEngine.Object.DestroyImmediate(bodies[i]);
    }

    private static void ApplyBodyMaterial(GameObject model, Material bodyMaterial)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            for (int j = 0; j < materials.Length; j++)
                materials[j] = bodyMaterial;
            renderers[i].sharedMaterials = materials;
        }
    }

    private static void CreateOrUpdateDefinition()
    {
        PowerDefinition definition = AssetDatabase.LoadAssetAtPath<PowerDefinition>(DefinitionPath);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<PowerDefinition>();
            AssetDatabase.CreateAsset(definition, DefinitionPath);
        }

        definition.tipo = KartPowerType.ElectricTrap;
        definition.nomeExibido = "ARMADILHA ELÉTRICA";
        definition.iconeColorido = RequireAsset<Sprite>("Assets/_Projeto/Art/UI/Powers/Power_Mine_Color.png");
        definition.iconeMono = RequireAsset<Sprite>("Assets/_Projeto/Art/UI/Powers/Power_Mine_Mono.png");
        definition.iconeCinza = RequireAsset<Sprite>("Assets/_Projeto/Art/UI/Powers/Power_Mine_Gray.png");
        EditorUtility.SetDirty(definition);
        AssetDatabase.SetLabels(definition, new[] { "PowerDefinition", "ElectricTrap" });
    }

    private static void WireKartPrefab(string kartPrefabPath, GameObject trapPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(kartPrefabPath);
        try
        {
            KartPowerUser user = root.GetComponent<KartPowerUser>();
            if (user == null)
                user = root.GetComponentInChildren<KartPowerUser>(true);
            if (user == null)
                throw new InvalidOperationException("KartPowerUser ausente no prefab: " + kartPrefabPath);

            SerializedObject serializedUser = new SerializedObject(user);
            SerializedProperty trapProperty = serializedUser.FindProperty("electricTrapPrefab");
            if (trapProperty == null)
                throw new InvalidOperationException("Campo electricTrapPrefab não encontrado em KartPowerUser.");
            trapProperty.objectReferenceValue = trapPrefab;
            serializedUser.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, kartPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Campo serializado não encontrado: " + propertyName);
        property.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Campo serializado não encontrado: " + propertyName);
        property.floatValue = value;
    }
}
#endif
