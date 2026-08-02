using UnityEngine;
using UnityEngine.Serialization;

// Mostra o prefab de escudo (Magic shield blue, baseado em ParticleSystem) envolvendo o carro.
// Reutilizável e independente do modelo: cria um ShieldRoot centralizado no bounds real do carro
// (medido em runtime, já que o KartVisualCustomizer reconstrói o veículo), dimensiona o escudo para
// envolvê-lo com folga e mantém tudo parentado para acompanhar movimento, drift e rotações.
[DisallowMultipleComponent]
public class KartShieldVisual : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab do escudo (ex.: Magic shield blue). Instanciado uma vez e reaproveitado.")]
    [SerializeField] private GameObject shieldPrefab;
    [Tooltip("Âncora do escudo. Criada automaticamente (ShieldRoot) se ficar vazia.")]
    [SerializeField] private Transform shieldRoot;

    [Header("Ajuste ao carro")]
    [Tooltip("Folga aplicada sobre o raio do carro para o escudo envolvê-lo.")]
    [SerializeField] private float fitMultiplier = 1.15f;
    [Tooltip("Se > 0, ignora a medição automática e usa esta escala fixa para o prefab.")]
    [SerializeField] private float manualScale = 0f;
    [Tooltip("Deslocamento extra da âncora em relação ao centro medido do carro.")]
    [SerializeField] private Vector3 rootLocalOffset = Vector3.zero;
    [Tooltip("Ajuste fino depois de centralizar a esfera no carro.")]
    [SerializeField] private float verticalOffset = -0.18f;
    [Tooltip("Altura local do centro visual da esfera no prefab. No Magic shield blue, o centro fica em Y=1.")]
    [FormerlySerializedAs("groundSink")]
    [SerializeField, Range(0f, 2f)] private float prefabSphereCenterY = 1f;
    [Tooltip("Correção de rotação aplicada ao prefab do escudo, se necessário.")]
    [SerializeField] private Vector3 prefabEulerOffset = Vector3.zero;
    [Tooltip("Trechos de nome ignorados ao medir o carro (evita medir o próprio escudo, HUD, etc.).")]
    [SerializeField] private string[] ignoreNameContains =
        { "Shield", "HUD", "Canvas", "Camera", "Rocket", "Foguete", "Puff", "Smoke" };

    [Header("Animação")]
    [Tooltip("Velocidade do 'pop-in' de escala ao ativar.")]
    [SerializeField] private float popInSpeed = 10f;
    [SerializeField, Range(0.1f, 1f)] private float activationStartScale = 0.45f;
    [SerializeField] private float blockPulseScale = 1.18f;

    private GameObject shieldInstance;
    private ParticleSystem[] shieldSystems;
    private float baseScale = 1f;
    private float currentScale;
    private Vector3 shieldCenterLocal;
    private bool active;

    public bool IsActive => active;

    private void Awake()
    {
        EnsureRoot();
    }

    public void Activate()
    {
        EnsureInstance();

        if (shieldInstance == null)
            return;

        active = true;

        if (!shieldInstance.activeSelf)
            shieldInstance.SetActive(true);

        currentScale = baseScale * activationStartScale;
        ApplyScale();

        if (shieldSystems != null)
        {
            foreach (ParticleSystem ps in shieldSystems)
            {
                if (ps == null)
                    continue;

                ps.Clear(true);
                ps.Play(true);
            }
        }
    }

    public void Deactivate()
    {
        active = false;

        if (shieldInstance == null)
            return;

        if (shieldSystems != null)
        {
            foreach (ParticleSystem ps in shieldSystems)
            {
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        shieldInstance.SetActive(false);
    }

    // Pequeno "soco" no escudo quando algo bate nele (usado pelo foguete/projétil).
    public void PulseBlock(Vector3 worldImpactPoint)
    {
        if (!active || shieldInstance == null)
            return;

        currentScale = baseScale * blockPulseScale;
        ApplyScale();
    }

    private void Update()
    {
        if (!active || shieldInstance == null)
            return;

        currentScale = Mathf.MoveTowards(currentScale, baseScale, baseScale * popInSpeed * Time.deltaTime);
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (shieldInstance != null)
        {
            shieldInstance.transform.localScale = Vector3.one * currentScale;
            UpdateRootPosition(currentScale);
        }
    }

    private void UpdateRootPosition(float visualScale)
    {
        if (shieldRoot == null)
            return;

        shieldRoot.localPosition = shieldCenterLocal
            + rootLocalOffset
            + Vector3.up * (verticalOffset - prefabSphereCenterY * visualScale);
    }

    private void EnsureRoot()
    {
        if (shieldRoot == null)
        {
            Transform existing = transform.Find("ShieldRoot");
            shieldRoot = existing != null
                ? existing
                : new GameObject("ShieldRoot").transform;
            shieldRoot.SetParent(transform, false);
        }

        shieldRoot.localRotation = Quaternion.identity;
    }

    private void EnsureInstance()
    {
        if (shieldInstance != null || shieldPrefab == null)
            return;

        EnsureRoot();

        MeasureCar(out Vector3 localCenter, out float radius);
        shieldCenterLocal = localCenter;
        baseScale = manualScale > 0f ? manualScale : Mathf.Max(0.25f, radius * fitMultiplier);

        // A esfera principal do prefab fica centrada em Y=1. Compensar esse centro depois da
        // escala coloca o carro no meio da bola e faz a metade inferior entrar no terreno.
        UpdateRootPosition(baseScale);

        shieldInstance = Instantiate(shieldPrefab, shieldRoot);
        shieldInstance.transform.localPosition = Vector3.zero;
        shieldInstance.transform.localRotation = Quaternion.Euler(prefabEulerOffset);
        shieldInstance.transform.localScale = Vector3.one;

        foreach (Collider col in shieldInstance.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody body in shieldInstance.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }

        shieldSystems = shieldInstance.GetComponentsInChildren<ParticleSystem>(true);

        currentScale = baseScale;
        ApplyScale();

        shieldInstance.SetActive(false);
    }

    private void MeasureCar(out Vector3 localCenter, out float radius)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool has = false;
        Bounds bounds = default;

        foreach (Renderer r in renderers)
        {
            if (r == null || ShouldIgnore(r.transform))
                continue;

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        if (!has)
        {
            localCenter = Vector3.up * 0.5f;
            radius = 2f;
            return;
        }

        localCenter = transform.InverseTransformPoint(bounds.center);
        radius = bounds.extents.magnitude;
    }

    private bool ShouldIgnore(Transform target)
    {
        if (shieldInstance != null && target.IsChildOf(shieldInstance.transform))
            return true;

        for (Transform cur = target; cur != null; cur = cur.parent)
        {
            foreach (string token in ignoreNameContains)
            {
                if (!string.IsNullOrEmpty(token) &&
                    cur.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
        }

        return false;
    }
}
