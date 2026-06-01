using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class KartPowerUser : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartPowerInventory inventory;
    [SerializeField] private KartController kart;
    [SerializeField] private GameObject shieldVisual;

    [Header("Escudo")]
    [SerializeField] private float shieldDuration = 4f;

    [Header("Escudo - Shader")]
    [SerializeField] private Renderer[] shieldRenderers;
    [SerializeField] private Material shieldMaterialOverride;
    [SerializeField] private Shader shieldShaderOverride;
    [SerializeField] private bool createRuntimeMaterialInstance = true;

    [Header("Escudo - Animacao")]
    [SerializeField] private bool animateShieldVisual = true;
    [SerializeField] private float activationScaleSpeed = 10f;
    [SerializeField, Range(0.05f, 1f)] private float activationStartScale = 0.35f;
    [SerializeField] private float pulseSpeed = 5.5f;
    [SerializeField, Range(0f, 0.25f)] private float pulseAmount = 0.055f;
    [SerializeField] private Vector3 hologramWobbleAngle = new Vector3(1.4f, 0f, 2.2f);
    [SerializeField] private float hologramWobbleSpeed = 2.2f;
    [SerializeField] private float textureScrollSpeed = 0.55f;
    [SerializeField] private float secondaryTextureScrollSpeed = -0.38f;
    [SerializeField] private float baseEmissionStrength = 0.35f;
    [SerializeField] private float emissionPulseAmount = 0.22f;
    [SerializeField] private float baseFresnelPower = 4f;
    [SerializeField] private float fresnelPulseAmount = 0.55f;
    [SerializeField] private float baseThreshold = 0.236f;
    [SerializeField] private float thresholdPulseAmount = 0.035f;
    [SerializeField] private string opacityProperty = "_Opacity_Global";
    [SerializeField, Range(0f, 1f)] private float activeOpacity = 1f;

    [Header("Foguete")]
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private Transform equippedVisualSocket;
    [SerializeField] private GameObject rocketProjectilePrefab;
    [SerializeField] private GameObject rocketEquippedPrefab;
    [SerializeField] private GameObject rocketBoingVFXPrefab;
    [SerializeField] private GameObject rocketExplosionVFXPrefab;
    [SerializeField] private GameObject rocketFireBurstPrefab;
    [SerializeField] private Vector3 projectileSpawnLocalPosition = new Vector3(0f, 0.92f, 1.8f);
    [SerializeField] private Vector3 equippedSocketLocalPosition = Vector3.zero;

    [Header("Estado")]
    [SerializeField] private float shieldEndTime;

    public bool IsShieldActive => Time.time < shieldEndTime;

    private readonly List<Material> runtimeShieldMaterials = new List<Material>();
    private Vector3 shieldBaseLocalScale = Vector3.one;
    private Quaternion shieldBaseLocalRotation = Quaternion.identity;
    private float shieldActivationTime;
    private bool shieldVisualWasActive;
    private GameObject rocketEquippedInstance;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<KartPowerInventory>();

        if (kart == null)
            kart = GetComponent<KartController>();

        EnsureRocketSockets();
        CacheShieldRenderers();
        CacheShieldBaseTransform();
        ApplyShieldMaterial();
        DisableShieldPhysics();
        UpdateShieldVisual();
    }

    private void Update()
    {
        ReadPowerInput();
        UpdateShieldVisual();
        AnimateShieldVisual();
        UpdateRocketEquippedVisual();
    }

    private void OnValidate()
    {
        CacheShieldRenderers();

        if (shieldVisual != null && shieldBaseLocalScale == Vector3.zero)
            shieldBaseLocalScale = shieldVisual.transform.localScale;
    }

    private void ReadPowerInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame)
            TryUseCurrentPower();
    }

    public void TryUseCurrentPower()
    {
        if (inventory == null)
            return;

        if (!inventory.HasPower)
            return;

        KartPowerType power = inventory.ConsumeCurrentPower();

        switch (power)
        {
            case KartPowerType.Shield:
                ActivateShield();
                break;

            case KartPowerType.Rocket:
                FireRocket();
                break;

            case KartPowerType.SwapPosition:
                Debug.Log("Poder Troca ainda será implementado.");
                break;
        }
    }

    private void ActivateShield()
    {
        shieldEndTime = Time.time + shieldDuration;
        shieldActivationTime = Time.time;
        Debug.Log("Escudo ativado.");
        UpdateShieldVisual();
    }

    public void PulseShieldBlock(Vector3 impactPoint, GameObject blockVFXPrefab)
    {
        if (shieldVisual == null)
            return;

        shieldActivationTime = Time.time;
        SetShieldOpacity(activeOpacity);

        if (shieldVisual.activeSelf)
            shieldVisual.transform.localScale = shieldBaseLocalScale * 1.12f;
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual == null)
            return;

        bool isActive = IsShieldActive;

        if (shieldVisualWasActive == isActive && shieldVisual.activeSelf == isActive)
            return;

        shieldVisual.SetActive(isActive);
        shieldVisualWasActive = isActive;

        if (isActive)
        {
            shieldActivationTime = Time.time;

            if (animateShieldVisual)
            {
                shieldVisual.transform.localScale = shieldBaseLocalScale * activationStartScale;
                SetShieldOpacity(0f);
            }
            else
            {
                shieldVisual.transform.localScale = shieldBaseLocalScale;
                shieldVisual.transform.localRotation = shieldBaseLocalRotation;
                SetShieldOpacity(activeOpacity);
            }
        }
        else
        {
            shieldVisual.transform.localScale = shieldBaseLocalScale;
            shieldVisual.transform.localRotation = shieldBaseLocalRotation;
            SetShieldOpacity(activeOpacity);
        }
    }

    private void DisableShieldPhysics()
    {
        if (shieldVisual == null)
            return;

        Collider[] colliders = shieldVisual.GetComponentsInChildren<Collider>(true);

        foreach (Collider shieldCollider in colliders)
        {
            shieldCollider.enabled = false;
        }

        Rigidbody[] rigidbodies = shieldVisual.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody shieldRigidbody in rigidbodies)
        {
            shieldRigidbody.isKinematic = true;
            shieldRigidbody.detectCollisions = false;
        }
    }

    private void EnsureRocketSockets()
    {
        if (projectileSpawnPoint == null)
            projectileSpawnPoint = FindOrCreateChild("ProjectileSpawnPoint", projectileSpawnLocalPosition);

        if (equippedVisualSocket == null)
            equippedVisualSocket = FindOrCreateChild("RocketEquippedSocket", equippedSocketLocalPosition);
    }

    private Transform FindOrCreateChild(string childName, Vector3 localPosition)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
            return existing;

        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        return child.transform;
    }

    private void UpdateRocketEquippedVisual()
    {
        bool shouldShow = inventory != null && inventory.CurrentPower == KartPowerType.Rocket;

        if (!shouldShow)
        {
            if (rocketEquippedInstance != null)
                rocketEquippedInstance.SetActive(false);

            return;
        }

        if (rocketEquippedInstance == null)
            CreateRocketEquippedVisual();

        if (rocketEquippedInstance != null && !rocketEquippedInstance.activeSelf)
            rocketEquippedInstance.SetActive(true);
    }

    private void CreateRocketEquippedVisual()
    {
        if (rocketEquippedPrefab == null || equippedVisualSocket == null)
            return;

        rocketEquippedInstance = Instantiate(rocketEquippedPrefab, equippedVisualSocket);

        // Ensure the equipped visual component drives the idle animation.
        RocketEquippedVisual equippedComponent = rocketEquippedInstance.GetComponent<RocketEquippedVisual>();
        if (equippedComponent == null)
            equippedComponent = rocketEquippedInstance.AddComponent<RocketEquippedVisual>();
        equippedComponent.Initialize(kart);
    }

    private void FireRocket()
    {
        EnsureRocketSockets();

        if (rocketEquippedInstance != null)
            rocketEquippedInstance.SetActive(false);

        if (projectileSpawnPoint == null || rocketProjectilePrefab == null)
        {
            Debug.LogWarning("Foguete nao disparado: prefab ou ProjectileSpawnPoint ausente.");
            return;
        }

        if (rocketFireBurstPrefab != null)
            Instantiate(rocketFireBurstPrefab, projectileSpawnPoint.position, projectileSpawnPoint.rotation);

        GameObject projectileObject = Instantiate(
            rocketProjectilePrefab,
            projectileSpawnPoint.position,
            projectileSpawnPoint.rotation
        );

        RocketProjectile projectile = projectileObject.GetComponent<RocketProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<RocketProjectile>();

        projectile.Initialize(
            gameObject,
            projectileSpawnPoint.forward,
            rocketBoingVFXPrefab,
            rocketExplosionVFXPrefab
        );
    }

    private void CacheShieldRenderers()
    {
        if (shieldVisual == null)
            return;

        if (shieldRenderers != null && shieldRenderers.Length > 0)
            return;

        shieldRenderers = shieldVisual.GetComponentsInChildren<Renderer>(true);
    }

    private void CacheShieldBaseTransform()
    {
        if (shieldVisual == null)
            return;

        shieldBaseLocalScale = shieldVisual.transform.localScale;
        shieldBaseLocalRotation = shieldVisual.transform.localRotation;
    }

    private void ApplyShieldMaterial()
    {
        if (shieldRenderers == null || shieldRenderers.Length == 0)
            return;

        CleanupRuntimeMaterials();

        for (int i = 0; i < shieldRenderers.Length; i++)
        {
            Renderer shieldRenderer = shieldRenderers[i];

            if (shieldRenderer == null)
                continue;

            Material[] sourceMaterials = shieldRenderer.sharedMaterials;

            if (sourceMaterials == null || sourceMaterials.Length == 0)
                sourceMaterials = new Material[1];

            Material[] assignedMaterials = new Material[sourceMaterials.Length];

            for (int materialIndex = 0; materialIndex < assignedMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = shieldMaterialOverride != null
                    ? shieldMaterialOverride
                    : sourceMaterials[materialIndex];

                if (sourceMaterial == null)
                    continue;

                Material materialToApply = sourceMaterial;

                if (Application.isPlaying && createRuntimeMaterialInstance)
                {
                    materialToApply = new Material(sourceMaterial);
                    runtimeShieldMaterials.Add(materialToApply);
                }

                if (shieldShaderOverride != null)
                    materialToApply.shader = shieldShaderOverride;

                assignedMaterials[materialIndex] = materialToApply;
            }

            shieldRenderer.sharedMaterials = assignedMaterials;
        }
    }

    private void AnimateShieldVisual()
    {
        if (!animateShieldVisual || shieldVisual == null || !IsShieldActive)
            return;

        float age = Mathf.Max(0f, Time.time - shieldActivationTime);
        float activation = Mathf.SmoothStep(0f, 1f, age * activationScaleSpeed);
        float pulse = Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        float shimmer = (Mathf.Sin(Time.time * pulseSpeed * 1.7f) + 1f) * 0.5f;
        float scale = Mathf.Lerp(activationStartScale, 1f, activation) * (1f + pulse);
        Vector3 breathingScale = new Vector3(1f + pulse * 0.35f, 1f + pulse * 0.75f, 1f + pulse * 0.35f);

        shieldVisual.transform.localScale = Vector3.Scale(shieldBaseLocalScale, breathingScale) * scale;
        shieldVisual.transform.localRotation = shieldBaseLocalRotation * CalculateHologramWobble();
        AnimateShieldShader(age, activation, shimmer);
    }

    private Quaternion CalculateHologramWobble()
    {
        float time = Time.time * hologramWobbleSpeed;
        float wobbleX = Mathf.Sin(time) * hologramWobbleAngle.x;
        float wobbleY = Mathf.Sin(time * 0.73f) * hologramWobbleAngle.y;
        float wobbleZ = Mathf.Cos(time * 1.17f) * hologramWobbleAngle.z;

        return Quaternion.Euler(wobbleX, wobbleY, wobbleZ);
    }

    private void AnimateShieldShader(float age, float activation, float shimmer)
    {
        float opacity = activeOpacity * activation * Mathf.Lerp(0.86f, 1f, shimmer);

        SetShieldOpacity(opacity);
        SetShieldFloat("_Animation_Y", textureScrollSpeed);
        SetShieldFloat("_Text_2_Animation_Y", secondaryTextureScrollSpeed);
        SetShieldFloat("_Texture_Opacity", Mathf.Lerp(0.75f, 1f, shimmer));
        SetShieldFloat("_Texture_2_Opacity", Mathf.Lerp(0.12f, 0.32f, shimmer));
        SetShieldFloat("_Emission_Strength", baseEmissionStrength + shimmer * emissionPulseAmount);
        SetShieldFloat("_Power", baseFresnelPower + Mathf.Sin(age * pulseSpeed * 0.85f) * fresnelPulseAmount);
        SetShieldFloat("_Threshold", baseThreshold + Mathf.Sin(age * pulseSpeed * 1.35f) * thresholdPulseAmount);
    }

    private void SetShieldOpacity(float opacity)
    {
        if (shieldRenderers == null || shieldRenderers.Length == 0)
            return;

        for (int i = 0; i < shieldRenderers.Length; i++)
        {
            Renderer shieldRenderer = shieldRenderers[i];

            if (shieldRenderer == null)
                continue;

            Material[] materials = shieldRenderer.sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(opacityProperty) && material.HasProperty(opacityProperty))
                    material.SetFloat(opacityProperty, opacity);

                SetColorAlpha(material, "_BaseColor", opacity);
                SetColorAlpha(material, "_Color", opacity);
            }
        }
    }

    private void SetColorAlpha(Material material, string propertyName, float opacity)
    {
        if (!material.HasProperty(propertyName))
            return;

        Color color = material.GetColor(propertyName);
        color.a = opacity;
        material.SetColor(propertyName, color);
    }

    private void SetShieldFloat(string propertyName, float value)
    {
        if (shieldRenderers == null || shieldRenderers.Length == 0)
            return;

        for (int i = 0; i < shieldRenderers.Length; i++)
        {
            Renderer shieldRenderer = shieldRenderers[i];

            if (shieldRenderer == null)
                continue;

            Material[] materials = shieldRenderer.sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];

                if (material != null && material.HasProperty(propertyName))
                    material.SetFloat(propertyName, value);
            }
        }
    }

    private void CleanupRuntimeMaterials()
    {
        for (int i = 0; i < runtimeShieldMaterials.Count; i++)
        {
            Material material = runtimeShieldMaterials[i];

            if (material != null)
                Destroy(material);
        }

        runtimeShieldMaterials.Clear();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMaterials();
    }
}
