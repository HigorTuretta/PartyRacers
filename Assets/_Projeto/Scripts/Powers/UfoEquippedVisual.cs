using UnityEngine;

// Disco voador EQUIPADO (idle) orbitando o kart enquanto o poder não é disparado.
// A órbita é pequena e próxima, como um item realmente acoplado ao carro: gira/flutua de leve,
// mantém altura coerente e nunca entra no chão (clamp de altura mínima) nem se afasta do veículo.
[DisallowMultipleComponent]
public class UfoEquippedVisual : MonoBehaviour
{
    [Header("Model")]
    [SerializeField, Min(0.05f)] private float modelScale = 0.35f;

    [Header("Idle Orbit (pequena e próxima)")]
    [Tooltip("Raio da órbita ao redor do socket. Pequeno = disco coladinho no carro.")]
    [SerializeField, Min(0f)] private float orbitDistance = 0.45f;
    [Tooltip("Altura do disco em relação ao socket/carro.")]
    [SerializeField] private float height = 0.95f;
    [Tooltip("Deslocamento local fixo do centro da órbita (relativo ao socket).")]
    [SerializeField] private Vector3 localOffset = Vector3.zero;
    [SerializeField] private float orbitDegreesPerSecond = 80f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.06f;

    [Header("Float Animation (sutil)")]
    [SerializeField, Min(0f)] private float bobAmplitude = 0.07f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.6f;
    [SerializeField, Min(0f)] private float radialDriftAmplitude = 0.04f;
    [SerializeField, Min(0f)] private float radialDriftSpeed = 1.2f;

    [Header("Rotation")]
    [SerializeField] private float spinSpeed = 170f;
    [SerializeField] private float tiltAngle = 4f;
    [SerializeField] private bool faceOrbitDirection = true;

    [Header("Limite de altura ao chão")]
    [Tooltip("Folga mínima (m) acima do chão. O disco nunca desce abaixo disso mesmo no fundo do bob.")]
    [SerializeField] private float minGroundClearance = 0.4f;
    [SerializeField] private LayerMask groundMask = ~0;

    private float orbitAngle;
    private float spinAngle;
    private float seed;
    private Vector3 smoothVelocity;

    private static readonly RaycastHit[] GroundHits = new RaycastHit[8];

    private void Awake()
    {
        seed = Random.Range(0f, 100f);
        orbitAngle = Random.Range(0f, 360f);
        transform.localScale = Vector3.one * modelScale;
        DisablePhysics();
    }

    private void OnEnable()
    {
        smoothVelocity = Vector3.zero;
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        float t = Time.time + seed;

        orbitAngle = Mathf.Repeat(orbitAngle + orbitDegreesPerSecond * deltaTime, 360f);
        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * deltaTime, 360f);

        float angleRad = orbitAngle * Mathf.Deg2Rad;
        Vector3 orbit = new Vector3(Mathf.Cos(angleRad), 0f, Mathf.Sin(angleRad));
        float drift = Mathf.Sin(t * radialDriftSpeed) * radialDriftAmplitude;
        float bob = Mathf.Sin(t * bobSpeed) * bobAmplitude;
        Vector3 targetLocal = localOffset + orbit * (orbitDistance + drift) + Vector3.up * (height + bob);

        if (followSmoothTime > 0f)
        {
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                targetLocal,
                ref smoothVelocity,
                followSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }
        else
        {
            transform.localPosition = targetLocal;
        }

        ClampAboveGround();

        Quaternion facing = Quaternion.identity;
        if (faceOrbitDirection && orbit.sqrMagnitude > 0.0001f)
        {
            Vector3 tangent = new Vector3(-orbit.z, 0f, orbit.x);
            facing = Quaternion.LookRotation(tangent, Vector3.up);
        }

        Quaternion tilt = Quaternion.Euler(
            Mathf.Sin(t * bobSpeed * 0.7f) * tiltAngle,
            spinAngle,
            Mathf.Cos(t * bobSpeed * 0.9f) * tiltAngle);

        transform.localRotation = facing * tilt;
    }

    // Garante que o disco nunca afunde no chão: sonda abaixo da posição mundial e eleva se preciso.
    private void ClampAboveGround()
    {
        if (minGroundClearance <= 0f)
            return;

        Vector3 worldPos = transform.position;
        Vector3 origin = worldPos + Vector3.up * 3f;

        int count = Physics.RaycastNonAlloc(origin, Vector3.down, GroundHits, 12f, groundMask, QueryTriggerInteraction.Ignore);
        float bestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = GroundHits[i];
            if (hit.collider == null)
                continue;

            // O próprio kart não é chão.
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            if (hit.point.y > bestY)
            {
                bestY = hit.point.y;
                found = true;
            }
        }

        if (!found)
            return;

        float minY = bestY + minGroundClearance;
        if (worldPos.y < minY)
        {
            worldPos.y = minY;
            transform.position = worldPos;
        }
    }

    private void DisablePhysics()
    {
        foreach (Collider col in GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
    }
}
