using UnityEngine;

// Resposta de colisão ARCADE entre karts (estilo NFS / Mario Kart): o contato empurra, raspa ou
// desvia o carro de forma perceptível, porém CONTROLÁVEL. A desestabilização (yaw) é forte apenas
// em batidas na traseira-lateral; toques de frente/lado quase não rodam o carro. A velocidade de
// avanço é preservada (o solver do Unity tende a "secar" o carro no impacto — aqui devolvemos a
// componente perdida) e a recuperação de aderência fica a cargo do KartController
// (NotifyKartCollisionRecovery), deixando o empurrão deslizar antes de o carro recolar.
[DisallowMultipleComponent]
[RequireComponent(typeof(KartController))]
[RequireComponent(typeof(Rigidbody))]
public class KartArcadeCollisionResponse : MonoBehaviour
{
    [Header("Deteccao")]
    [Tooltip("Velocidade relativa mínima (m/s) para tratar o contato como batida. Abaixo disso, ignora.")]
    [SerializeField] private float minRelativeSpeed = 2.0f;
    [Tooltip("Velocidade relativa (m/s) que conta como impacto FORTE (impact01 = 1).")]
    [SerializeField] private float strongImpactSpeed = 18f;
    [Tooltip("Fração mínima da velocidade de avanço pré-impacto que devolvemos (anti-trava seco).")]
    [SerializeField, Range(0f, 1f)] private float minimumSolverSpeedRetention = 0.85f;

    [Header("Empurrao (VelocityChange)")]
    [Tooltip("Empurrão base no impacto máximo. Escalonado por impact01 e pela direção do contato.")]
    [SerializeField] private float pushVelocityChange = 3.0f;
    [Tooltip("Multiplicador do empurrão em batidas laterais (disputar espaço lado a lado).")]
    [SerializeField] private float sidePushMultiplier = 1.35f;
    [Tooltip("Multiplicador do empurrão em batidas na traseira-lateral.")]
    [SerializeField] private float rearQuarterPushMultiplier = 1.5f;
    [Tooltip("Teto do empurrão (m/s) para nenhuma batida arremessar o carro.")]
    [SerializeField] private float maxPushVelocityChange = 7f;

    [Header("Desestabilizacao (yaw)")]
    [Tooltip("Giro base de desestabilização no impacto forte. Mantido BAIXO para não rodar o carro.")]
    [SerializeField] private float yawTorqueVelocityChange = 1.8f;
    [Tooltip("Giro extra em batidas na traseira-lateral (perde um pouco a trajetória, como esperado).")]
    [SerializeField] private float rearQuarterYawMultiplier = 2.4f;
    [Tooltip("Fração do giro mantida em batidas puramente laterais/frontais (0 = não roda nessas).")]
    [SerializeField, Range(0f, 1f)] private float sideYawScale = 0.25f;
    [Tooltip("Amortece pitch/roll residual logo após o contato (não mexe no yaw — esse é controlado).")]
    [SerializeField, Range(0f, 1f)] private float angularDampingAfterContact = 0.7f;
    [Tooltip("Limita a velocidade vertical após a batida (evita 'pulinhos' ao se chocar).")]
    [SerializeField] private float verticalVelocityClamp = 1.6f;

    [Header("Faiscas")]
    [Tooltip("Componente de faíscas do kart. Se vazio, é resolvido automaticamente no Awake.")]
    [SerializeField] private KartCollisionSparks sparks;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private float debugDrawSeconds = 0.35f;

    private Rigidbody body;
    private KartController kart;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        kart = GetComponent<KartController>();

        if (sparks == null)
            sparks = GetComponent<KartCollisionSparks>();
    }

    public bool TryHandleCollision(
        Collision collision,
        KartController otherKart,
        Vector3 preImpactVelocity,
        float maxForwardSpeedMps,
        out float impact01)
    {
        impact01 = 0f;

        if (collision == null || otherKart == null || otherKart == kart || body == null)
            return false;

        Rigidbody otherBody = otherKart.Rigidbody != null ? otherKart.Rigidbody : collision.rigidbody;
        Vector3 relativeVelocity = body.linearVelocity - (otherBody != null ? otherBody.linearVelocity : Vector3.zero);
        float relativeSpeed = Planar(relativeVelocity).magnitude;

        if (relativeSpeed < minRelativeSpeed)
            return false;

        if (!TryGetContactFrame(collision, otherKart.transform, out Vector3 contactPoint, out Vector3 pushDirection))
            return false;

        impact01 = Mathf.InverseLerp(minRelativeSpeed, strongImpactSpeed, relativeSpeed);

        // Classifica o ponto de contato no referencial do próprio kart:
        //  side01      -> quão lateral foi o toque (0 = de frente/traseira, 1 = de lado);
        //  rear01      -> quão atrás foi o toque (0 = à frente, 1 = bem atrás);
        //  rearQuarter -> traseira-lateral (combinação) -> mais desestabilização.
        Vector3 localContact = transform.InverseTransformPoint(contactPoint);
        float side01 = Mathf.InverseLerp(0.22f, 0.72f, Mathf.Abs(localContact.x));
        float rear01 = Mathf.InverseLerp(-0.1f, -0.95f, localContact.z);
        float rearQuarter01 = Mathf.Clamp01(side01 * rear01);

        // ---- Empurrão (desliza o carro, sem arremessar) ----
        float pushScale = 1f
            + side01 * (sidePushMultiplier - 1f)
            + rearQuarter01 * (rearQuarterPushMultiplier - 1f);

        float push = Mathf.Min(maxPushVelocityChange, pushVelocityChange * impact01 * pushScale);
        body.AddForce(pushDirection * push, ForceMode.VelocityChange);

        RestoreSolverSpeed(preImpactVelocity, pushDirection);

        // ---- Desestabilização (yaw) — quase nula de frente/lado, relevante na traseira-lateral ----
        float yawSign = Mathf.Abs(localContact.x) > 0.05f
            ? -Mathf.Sign(localContact.x)
            : Mathf.Sign(Vector3.SignedAngle(transform.forward, pushDirection, Vector3.up));

        float yawScale = Mathf.Lerp(sideYawScale, 1f, rearQuarter01);
        float yawTorque = yawTorqueVelocityChange * impact01 * yawScale * Mathf.Lerp(1f, rearQuarterYawMultiplier, rearQuarter01);
        body.AddTorque(Vector3.up * yawSign * yawTorque, ForceMode.VelocityChange);

        // Amortece somente pitch/roll (x/z) — o yaw é deixado para o KartController controlar.
        Vector3 angular = body.angularVelocity;
        angular.x *= angularDampingAfterContact;
        angular.z *= angularDampingAfterContact;
        body.angularVelocity = angular;

        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -verticalVelocityClamp, verticalVelocityClamp);
        body.linearVelocity = velocity;

        // Faíscas no ponto de contato (respeita força mínima e cooldown do próprio componente).
        if (sparks != null)
            sparks.TryEmit(contactPoint, -pushDirection, impact01);

        if (debugMode)
        {
            Debug.DrawRay(contactPoint, pushDirection * 3f, Color.magenta, debugDrawSeconds);
            Debug.DrawRay(transform.position + Vector3.up, Vector3.up * yawSign, Color.yellow, debugDrawSeconds);
        }

        return true;
    }

    private void RestoreSolverSpeed(Vector3 preImpactVelocity, Vector3 pushDirection)
    {
        Vector3 preFlat = Planar(preImpactVelocity);
        Vector3 currentFlat = Planar(body.linearVelocity);

        if (preFlat.sqrMagnitude < 0.25f)
            return;

        float minimumSpeed = preFlat.magnitude * minimumSolverSpeedRetention;
        if (currentFlat.magnitude >= minimumSpeed)
            return;

        Vector3 restoreDirection = Vector3.ProjectOnPlane(preFlat.normalized, pushDirection);
        if (restoreDirection.sqrMagnitude < 0.01f)
            restoreDirection = preFlat.normalized;

        float missingSpeed = minimumSpeed - currentFlat.magnitude;
        body.AddForce(restoreDirection.normalized * missingSpeed, ForceMode.VelocityChange);
    }

    private bool TryGetContactFrame(
        Collision collision,
        Transform otherTransform,
        out Vector3 contactPoint,
        out Vector3 pushDirection)
    {
        contactPoint = transform.position;
        pushDirection = Planar(transform.position - otherTransform.position);

        Vector3 contactSum = Vector3.zero;
        Vector3 normalSum = Vector3.zero;
        int contacts = collision.contactCount;

        for (int i = 0; i < contacts; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            contactSum += contact.point;
            normalSum += Planar(contact.normal);
        }

        if (contacts > 0)
        {
            contactPoint = contactSum / contacts;

            if (normalSum.sqrMagnitude > 0.01f)
                pushDirection = normalSum;
        }

        if (pushDirection.sqrMagnitude < 0.01f)
            pushDirection = Planar(transform.position - otherTransform.position);

        if (pushDirection.sqrMagnitude < 0.01f)
            return false;

        pushDirection.Normalize();
        return true;
    }

    private static Vector3 Planar(Vector3 value)
    {
        value.y = 0f;
        return value;
    }
}
