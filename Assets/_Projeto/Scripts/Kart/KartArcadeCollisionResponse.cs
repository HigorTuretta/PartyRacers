using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(KartController))]
[RequireComponent(typeof(Rigidbody))]
public class KartArcadeCollisionResponse : MonoBehaviour
{
    [Header("Deteccao")]
    [SerializeField] private float minRelativeSpeed = 2.2f;
    [SerializeField] private float strongImpactSpeed = 16f;
    [SerializeField, Range(0f, 1f)] private float minimumSolverSpeedRetention = 0.82f;

    [Header("Empurrao")]
    [SerializeField] private float pushVelocityChange = 3.8f;
    [SerializeField] private float sidePushMultiplier = 1.25f;
    [SerializeField] private float rearQuarterPushMultiplier = 1.45f;
    [SerializeField] private float maxPushVelocityChange = 7.5f;

    [Header("Desestabilizacao")]
    [SerializeField] private float yawTorqueVelocityChange = 4.8f;
    [SerializeField] private float rearQuarterYawMultiplier = 1.65f;
    [SerializeField] private float angularDampingAfterContact = 0.82f;
    [SerializeField] private float verticalVelocityClamp = 2f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private float debugDrawSeconds = 0.35f;

    private Rigidbody body;
    private KartController kart;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        kart = GetComponent<KartController>();
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

        Vector3 localContact = transform.InverseTransformPoint(contactPoint);
        float side01 = Mathf.InverseLerp(0.22f, 0.72f, Mathf.Abs(localContact.x));
        float rear01 = Mathf.InverseLerp(-0.1f, -0.95f, localContact.z);
        float rearQuarter01 = Mathf.Clamp01(side01 * rear01);

        float pushScale = 1f
            + side01 * (sidePushMultiplier - 1f)
            + rearQuarter01 * (rearQuarterPushMultiplier - 1f);

        float push = Mathf.Min(maxPushVelocityChange, pushVelocityChange * impact01 * pushScale);
        body.AddForce(pushDirection * push, ForceMode.VelocityChange);

        RestoreSolverSpeed(preImpactVelocity, pushDirection);

        float yawSign = Mathf.Abs(localContact.x) > 0.05f
            ? -Mathf.Sign(localContact.x)
            : Mathf.Sign(Vector3.SignedAngle(transform.forward, pushDirection, Vector3.up));

        float yawTorque = yawTorqueVelocityChange * impact01 * Mathf.Lerp(1f, rearQuarterYawMultiplier, rearQuarter01);
        body.AddTorque(Vector3.up * yawSign * yawTorque, ForceMode.VelocityChange);

        Vector3 angular = body.angularVelocity;
        angular.x *= angularDampingAfterContact;
        angular.z *= angularDampingAfterContact;
        body.angularVelocity = angular;

        Vector3 velocity = body.linearVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -verticalVelocityClamp, verticalVelocityClamp);
        body.linearVelocity = velocity;

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
