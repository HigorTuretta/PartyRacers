using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelStunAura : MonoBehaviour
{
    [Header("Materiais")]
    [SerializeField] private Material auraMaterial;
    [SerializeField] private Material sparksMaterial;
    [SerializeField] private Material lightningMaterial;

    [Header("Visual")]
    [SerializeField] private float orbitRadius = 0.85f;
    [SerializeField] private float orbitHeight = 0.55f;
    [SerializeField] private float orbitSpeed = 160f;
    [SerializeField] private int iconCount = 6;
    [SerializeField] private float haloRadius = 1.6f;

    private Transform[] icons;
    private LineRenderer auraRing;
    private Material auraRingMaterial;
    private Transform haloFloor;
    private Transform haloRing;
    private ElectroGelLightning lightning;
    private ElectroGelLightning columnArcs;
    private Light glowLight;
    private float offset;

    private void Awake()
    {
        BuildAura();
        offset = Random.Range(0f, 360f);
    }

    private void OnDestroy()
    {
        if (auraRingMaterial != null)
            Destroy(auraRingMaterial);
    }

    private void Update()
    {
        float t = Time.time;
        float angularTime = t * orbitSpeed + offset;

        if (icons != null)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                float angle = (angularTime + i * (360f / icons.Length)) * Mathf.Deg2Rad;
                float bob = Mathf.Sin(t * 5f + i) * 0.08f;
                icons[i].localPosition = new Vector3(
                    Mathf.Cos(angle) * orbitRadius,
                    orbitHeight + bob,
                    Mathf.Sin(angle) * orbitRadius
                );

                float scaleBob = 1f + Mathf.Sin(t * 9f + i * 0.7f) * 0.18f;
                icons[i].localScale = Vector3.one * 0.28f * scaleBob;
            }
        }

        if (auraRing != null)
        {
            float pulse = 1f + Mathf.Sin(t * 8f) * 0.06f;
            auraRing.transform.localScale = new Vector3(pulse, pulse, pulse);

            if (auraRingMaterial != null && auraRingMaterial.HasProperty("_EmissionColor"))
            {
                float flicker = 1f - Random.value * 0.18f;
                Color emission = new Color(0.55f, 0.85f, 1f, 1f) * 3f * flicker;
                auraRingMaterial.SetColor("_EmissionColor", emission);
            }
        }

        if (haloFloor != null)
        {
            float halo = 1f + Mathf.Sin(t * 4.2f) * 0.07f;
            haloFloor.localScale = new Vector3(haloRadius * halo, haloRadius * halo, 1f);
            haloFloor.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        if (haloRing != null)
        {
            float halo = 1f + Mathf.Sin(t * 6.3f + 0.5f) * 0.12f;
            haloRing.localScale = new Vector3(haloRadius * 1.4f * halo, haloRadius * 1.4f * halo, 1f);
            haloRing.localRotation = Quaternion.Euler(90f, t * 35f, 0f);
        }

        if (glowLight != null)
            glowLight.intensity = 1.2f + Mathf.Sin(t * 10f) * 0.25f;
    }

    private void BuildAura()
    {
        int count = Mathf.Max(3, iconCount);
        icons = new Transform[count];

        for (int i = 0; i < count; i++)
        {
            GameObject iconHost = new GameObject(i % 2 == 0 ? "StunStar" : "MiniZap");
            iconHost.transform.SetParent(transform, false);
            icons[i] = iconHost.transform;

            Texture2D sprite = i % 2 == 0
                ? ElectroGelVisualUtility.GetStarSprite()
                : ElectroGelVisualUtility.GetSoftCircle();

            Transform billboard = ElectroGelVisualUtility.BuildBillboardHalo(
                iconHost.transform,
                sparksMaterial,
                sprite,
                new Vector2(1f, 1f),
                i % 2 == 0
                    ? new Color(1f, 0.95f, 0.4f, 0.95f)
                    : new Color(0.6f, 0.95f, 1f, 0.85f)
            );

            ElectroGelBillboard billboardSpinner = billboard.GetComponent<ElectroGelBillboard>();
            if (billboardSpinner != null && i % 2 == 0)
                billboardSpinner.SetSpinSpeed(180f);
        }

        GameObject ringObject = new GameObject("StunAuraRing");
        ringObject.transform.SetParent(transform, false);
        auraRing = ringObject.AddComponent<LineRenderer>();
        auraRingMaterial = auraMaterial != null ? new Material(auraMaterial) : null;
        auraRing.material = auraRingMaterial;
        auraRing.useWorldSpace = false;
        auraRing.loop = true;
        auraRing.positionCount = 48;
        auraRing.startWidth = 0.055f;
        auraRing.endWidth = 0.055f;
        auraRing.numCornerVertices = 2;
        auraRing.numCapVertices = 2;
        auraRing.alignment = LineAlignment.View;
        auraRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        auraRing.receiveShadows = false;
        auraRing.textureMode = LineTextureMode.Stretch;

        for (int i = 0; i < auraRing.positionCount; i++)
        {
            float angle = i / (float)auraRing.positionCount * Mathf.PI * 2f;
            auraRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * 1.05f, 0f, Mathf.Sin(angle) * 1.05f));
        }

        GameObject sparksObject = new GameObject("StunSparks");
        sparksObject.transform.SetParent(transform, false);
        ParticleSystem sparks = sparksObject.AddComponent<ParticleSystem>();
        ElectroGelVisualUtility.ConfigureSparks(sparks, sparksMaterial, 26f, 0.32f, 0.95f);
        ParticleSystem.MainModule sparksMain = sparks.main;
        sparksMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.95f, 1f, 1f),
            new Color(1f, 0.9f, 0.45f, 1f)
        );

        Material lightningUse = lightningMaterial != null ? lightningMaterial : (sparksMaterial != null ? sparksMaterial : auraMaterial);

        lightning = ElectroGelLightning.Attach(
            transform,
            lightningUse,
            ElectroGelLightning.BoltShape.ZapColumn,
            4,
            0.95f,
            0.22f,
            0.07f,
            0.022f,
            0.05f,
            new Color(0.55f, 0.95f, 1f, 1f),
            new Color(1f, 0.85f, 0.5f, 1f),
            4f
        );

        columnArcs = ElectroGelLightning.Attach(
            transform,
            lightningUse,
            ElectroGelLightning.BoltShape.OrbitalSphere,
            3,
            0.75f,
            0.15f,
            0.05f,
            0.013f,
            0.06f,
            new Color(0.6f, 1f, 1f, 1f),
            new Color(0.85f, 0.55f, 1f, 1f),
            3.2f
        );
        columnArcs.transform.localPosition = new Vector3(0f, 0.6f, 0f);

        Material haloMat = auraMaterial != null ? auraMaterial : sparksMaterial;

        haloFloor = ElectroGelVisualUtility.BuildFlatQuad(
            transform,
            haloMat,
            ElectroGelVisualUtility.GetSoftCircle(),
            new Vector2(haloRadius, haloRadius),
            new Color(0.55f, 0.85f, 1f, 0.55f),
            "StunGroundGlow"
        );
        haloFloor.localPosition = new Vector3(0f, 0.02f, 0f);
        haloFloor.localRotation = Quaternion.Euler(90f, 0f, 0f);

        haloRing = ElectroGelVisualUtility.BuildFlatQuad(
            transform,
            haloMat,
            ElectroGelVisualUtility.GetRingSprite(),
            new Vector2(haloRadius * 1.4f, haloRadius * 1.4f),
            new Color(0.6f, 0.95f, 1f, 0.8f),
            "StunGroundRing"
        );
        haloRing.localPosition = new Vector3(0f, 0.04f, 0f);
        haloRing.localRotation = Quaternion.Euler(90f, 0f, 0f);

        glowLight = ElectroGelVisualUtility.AttachGlowLight(
            transform,
            new Color(0.45f, 0.85f, 1f),
            1.3f,
            2.5f,
            new Vector3(0f, 0.5f, 0f)
        );
    }
}
