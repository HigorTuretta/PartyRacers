using UnityEngine;

[DisallowMultipleComponent]
public class UfoEquippedVisual : MonoBehaviour
{
    [Header("Model")]
    [SerializeField, Min(0.05f)] private float modelScale = 0.35f;

    [Header("Idle Orbit")]
    [SerializeField, Min(0f)] private float orbitDistance = 1.35f;
    [SerializeField] private float height = 1.55f;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0f, 0.2f);
    [SerializeField] private float orbitDegreesPerSecond = 95f;
    [SerializeField, Min(0f)] private float followSmoothTime = 0.08f;

    [Header("Float Animation")]
    [SerializeField, Min(0f)] private float bobAmplitude = 0.12f;
    [SerializeField, Min(0f)] private float bobSpeed = 2.8f;
    [SerializeField, Min(0f)] private float radialDriftAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float radialDriftSpeed = 1.35f;

    [Header("Rotation")]
    [SerializeField] private float spinSpeed = 190f;
    [SerializeField] private float tiltAngle = 5f;
    [SerializeField] private bool faceOrbitDirection = true;

    private float orbitAngle;
    private float spinAngle;
    private float seed;
    private Vector3 smoothVelocity;

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
