using UnityEngine;

[DisallowMultipleComponent]
public class ElectroGelDummyTarget : MonoBehaviour
{
    [Header("Teste")]
    [SerializeField] private bool shieldActive;
    [SerializeField] private Material bodyMaterial;
    [SerializeField] private Material shieldMaterial;

    [Header("Visual")]
    [SerializeField] private Vector3 bodyScale = new Vector3(1.35f, 0.65f, 2.2f);
    [SerializeField] private Vector3 shieldScale = new Vector3(2.2f, 1.45f, 3.1f);

    private Transform shieldVisual;
    private float shieldPulseEndTime;

    public bool IsShieldActive => shieldActive;

    public void PulseShieldBlock(Vector3 impactPoint, GameObject blockVFXPrefab)
    {
        shieldPulseEndTime = Time.time + 0.32f;
    }

    private void Awake()
    {
        BuildVisual();
        EnsureCollider();
    }

    private void Update()
    {
        if (shieldVisual == null)
            return;

        shieldVisual.gameObject.SetActive(shieldActive);

        float pulse = Time.time < shieldPulseEndTime
            ? 1.18f + Mathf.Sin(Time.time * 45f) * 0.08f
            : 1f + Mathf.Sin(Time.time * 4f) * 0.025f;

        shieldVisual.localScale = shieldScale * pulse;
    }

    private void BuildVisual()
    {
        if (GetComponentInChildren<Renderer>() != null)
            return;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "DummyKartBody";
        body.transform.SetParent(transform, false);
        body.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        body.transform.localScale = bodyScale;

        Collider bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null)
            Destroy(bodyCollider);

        Renderer bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.sharedMaterial = bodyMaterial;

        GameObject shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.name = "DummyShieldPreview";
        shield.transform.SetParent(transform, false);
        shield.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        shield.transform.localScale = shieldScale;

        Collider shieldCollider = shield.GetComponent<Collider>();
        if (shieldCollider != null)
            Destroy(shieldCollider);

        Renderer shieldRenderer = shield.GetComponent<Renderer>();
        shieldRenderer.sharedMaterial = shieldMaterial;
        shieldRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        shieldRenderer.receiveShadows = false;
        shieldVisual = shield.transform;
    }

    private void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        box.size = new Vector3(1.6f, 1.2f, 2.55f);
        box.center = new Vector3(0f, 0.65f, 0f);
    }
}
