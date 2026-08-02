using UnityEngine;

[DisallowMultipleComponent]
public sealed class KartElectricShockEffect : MonoBehaviour
{
    private KartController kart;
    private GameObject auraInstance;
    private GameObject auraSourcePrefab;
    private float endTime;
    private float appliedSpeedMultiplier = 1f;
    private bool modifierApplied;

    public bool IsActive => modifierApplied && Time.time < endTime;
    public float RemainingSeconds => Mathf.Max(0f, endTime - Time.time);
    public float AppliedSpeedMultiplier => appliedSpeedMultiplier;
    public GameObject AuraInstance => auraInstance;

    public static KartElectricShockEffect ApplyTo(
        GameObject target,
        float duration,
        float speedMultiplier,
        GameObject auraPrefab,
        float auraScale = 1f)
    {
        if (target == null)
            return null;

        KartController targetKart = target.GetComponent<KartController>();
        if (targetKart == null)
            targetKart = target.GetComponentInParent<KartController>();
        if (targetKart == null)
            return null;

        KartElectricShockEffect effect = targetKart.GetComponent<KartElectricShockEffect>();
        if (effect == null)
            effect = targetKart.gameObject.AddComponent<KartElectricShockEffect>();

        effect.Refresh(duration, speedMultiplier, auraPrefab, auraScale);
        return effect;
    }

    public static void ClearFrom(GameObject target)
    {
        if (target == null)
            return;

        KartElectricShockEffect effect = target.GetComponentInParent<KartElectricShockEffect>();
        if (effect == null)
            effect = target.GetComponentInChildren<KartElectricShockEffect>(true);
        if (effect == null)
            return;

        effect.Cancel();
        Destroy(effect);
    }

    private void Awake()
    {
        kart = GetComponent<KartController>();
    }

    private void Refresh(float duration, float speedMultiplier, GameObject auraPrefab, float auraScale)
    {
        if (kart == null)
            kart = GetComponent<KartController>();
        if (kart == null)
            return;

        // Uma nova armadilha renova a janela completa. O mesmo componente é a mesma fonte no
        // KartController, portanto 50% nunca vira 25% por reaplicação.
        endTime = Time.time + Mathf.Max(0.01f, duration);
        appliedSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.05f, 1f);
        kart.SetSpeedLimitMultiplier(this, appliedSpeedMultiplier);
        modifierApplied = true;
        enabled = true;

        EnsureAura(auraPrefab, auraScale);
        RestartAuraParticles();
    }

    private void Update()
    {
        if (!modifierApplied || Time.time < endTime)
            return;

        Cancel();
        Destroy(this);
    }

    public void Cancel()
    {
        if (modifierApplied && kart != null)
            kart.RemoveSpeedLimitMultiplier(this);

        modifierApplied = false;
        endTime = 0f;

        if (auraInstance != null)
        {
            Destroy(auraInstance);
            auraInstance = null;
            auraSourcePrefab = null;
        }
    }

    private void EnsureAura(GameObject auraPrefab, float auraScale)
    {
        if (auraPrefab == null)
        {
            if (auraInstance != null)
                Destroy(auraInstance);
            auraInstance = null;
            auraSourcePrefab = null;
            return;
        }

        if (auraInstance == null || auraSourcePrefab != auraPrefab)
        {
            if (auraInstance != null)
                Destroy(auraInstance);

            auraInstance = Instantiate(auraPrefab, transform);
            auraInstance.name = "ElectricTrap_TargetAura";
            auraSourcePrefab = auraPrefab;
        }

        Bounds bounds = CalculateKartVisualBounds();
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        radius = Mathf.Max(0.65f, radius) * Mathf.Max(0.1f, auraScale);

        auraInstance.transform.position = bounds.center;
        auraInstance.transform.rotation = transform.rotation;

        Vector3 parentScale = transform.lossyScale;
        auraInstance.transform.localScale = new Vector3(
            radius / Mathf.Max(0.001f, Mathf.Abs(parentScale.x)),
            radius / Mathf.Max(0.001f, Mathf.Abs(parentScale.y)),
            radius / Mathf.Max(0.001f, Mathf.Abs(parentScale.z)));
    }

    private Bounds CalculateKartVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(false);
        Bounds bounds = new Bounds(transform.position + transform.up * 0.55f, new Vector3(1.5f, 1f, 2.2f));
        bool found = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled ||
                renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer)
            {
                continue;
            }

            if (IsEffectRenderer(renderer.transform))
                continue;

            if (!found)
            {
                bounds = renderer.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private static bool IsEffectRenderer(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            string objectName = current.name;
            if (objectName.IndexOf("VFX", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Aura", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("PowerSocket", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                objectName.IndexOf("EquippedSocket", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void RestartAuraParticles()
    {
        if (auraInstance == null)
            return;

        ParticleSystem[] particles = auraInstance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            ParticleSystem.MainModule main = particle.main;
            main.loop = true;
            particle.Clear(true);
            particle.Play(true);
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnDestroy()
    {
        Cancel();
    }
}
