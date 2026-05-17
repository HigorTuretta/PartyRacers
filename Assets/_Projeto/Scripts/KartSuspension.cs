using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class KartSuspension : MonoBehaviour
{
    [System.Serializable]
    public class Wheel
    {
        [Tooltip("Pivot da roda (geralmente WheelPivot_X). O raio sai daqui + anchorOffsetUp pra cima.")]
        public Transform wheelPivot;

        [Tooltip("Acumula travel visual (suspension visual). Se nulo, usa wheelPivot.")]
        public Transform visualOffsetTarget;

        [HideInInspector] public Vector3 visualTargetBaseLocalPos;
        [HideInInspector] public float currentCompression;
        [HideInInspector] public float previousCompression;
        [HideInInspector] public bool isGrounded;
        [HideInInspector] public Vector3 hitPoint;
        [HideInInspector] public Vector3 hitNormal;
    }

    [Header("Referências")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Geometria")]
    [Tooltip("Quanto a âncora fica acima do wheelPivot, no espaço up do chassi.")]
    [SerializeField] private float anchorOffsetUp = 0.4f;
    [Tooltip("Comprimento total da mola (curso da suspensão).")]
    [SerializeField] private float restLength = 0.4f;
    [Tooltip("Raio do pneu (parte do raycast que conta como 'pneu' antes do chão).")]
    [SerializeField] private float wheelRadius = 0.3f;

    [Header("Mola")]
    [SerializeField] private float springStrength = 100000f;
    [SerializeField] private float springDamping = 8000f;

    [Header("Visual")]
    [Tooltip("Move o visualOffsetTarget pra cima/baixo conforme a compressão. Desligue se houver conflito.")]
    [SerializeField] private bool driveVisualOffset = true;
    [SerializeField] private float visualMaxTravel = 0.12f;
    [SerializeField] private float visualSmooth = 22f;

    [Header("Estabilizadores")]
    [Tooltip("Aplica torque corretivo para o carro tender a ficar paralelo ao chão (anti-capotamento).")]
    [SerializeField] private float uprightAssist = 18f;
    [Tooltip("Quanto da gravidade artificial extra aplicar quando todas as rodas estão no chão.")]
    [SerializeField] private float stickToGroundForce = 12f;

    [Header("Debug")]
    [SerializeField] private bool drawDebugGizmos = true;

    [SerializeField] private Wheel[] wheels = new Wheel[4];

    public bool IsAnyWheelGrounded { get; private set; }
    public int GroundedWheelCount { get; private set; }
    public Vector3 AverageGroundNormal { get; private set; } = Vector3.up;
    public IReadOnlyList<Wheel> Wheels => wheels;

    private void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        for (int i = 0; i < wheels.Length; i++)
        {
            Wheel wheel = wheels[i];
            if (wheel == null) continue;

            Transform target = wheel.visualOffsetTarget != null ? wheel.visualOffsetTarget : wheel.wheelPivot;
            if (target != null)
                wheel.visualTargetBaseLocalPos = target.localPosition;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        int grounded = 0;
        Vector3 normalSum = Vector3.zero;

        for (int i = 0; i < wheels.Length; i++)
        {
            Wheel wheel = wheels[i];
            if (wheel == null) continue;

            UpdateWheelPhysics(wheel);

            if (wheel.isGrounded)
            {
                grounded++;
                normalSum += wheel.hitNormal;
            }
        }

        GroundedWheelCount = grounded;
        IsAnyWheelGrounded = grounded > 0;
        AverageGroundNormal = grounded > 0 ? (normalSum / grounded).normalized : Vector3.up;

        ApplyUprightAssist();
        ApplyStickToGround();
    }

    private void LateUpdate()
    {
        if (!driveVisualOffset) return;

        for (int i = 0; i < wheels.Length; i++)
            UpdateWheelVisual(wheels[i]);
    }

    private void UpdateWheelPhysics(Wheel wheel)
    {
        if (wheel.wheelPivot == null) return;

        Vector3 chassisUp = rb.transform.up;
        Vector3 anchorPosition = wheel.wheelPivot.position + chassisUp * anchorOffsetUp;
        Vector3 rayDirection = -chassisUp;
        float maxRayLength = restLength + wheelRadius;

        wheel.previousCompression = wheel.currentCompression;

        if (Physics.Raycast(anchorPosition, rayDirection, out RaycastHit hit, maxRayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            float distanceToWheelGround = hit.distance - wheelRadius;
            float compression = Mathf.Clamp(restLength - distanceToWheelGround, 0f, restLength);

            wheel.currentCompression = compression;
            wheel.isGrounded = true;
            wheel.hitPoint = hit.point;
            wheel.hitNormal = hit.normal;

            float compressionRatio = compression / Mathf.Max(0.0001f, restLength);
            float compressionVelocity = (compression - wheel.previousCompression) / Mathf.Max(0.0001f, Time.fixedDeltaTime);

            float springForce = springStrength * compressionRatio;
            float dampForce = springDamping * compressionVelocity;
            float totalForce = Mathf.Max(0f, springForce + dampForce);

            rb.AddForceAtPosition(chassisUp * totalForce, anchorPosition);
        }
        else
        {
            wheel.isGrounded = false;
            wheel.hitNormal = Vector3.up;
            wheel.currentCompression = Mathf.MoveTowards(
                wheel.currentCompression, 0f,
                restLength * 4f * Time.fixedDeltaTime
            );
        }
    }

    private void ApplyUprightAssist()
    {
        if (uprightAssist <= 0f) return;

        Vector3 chassisUp = rb.transform.up;
        Vector3 target = IsAnyWheelGrounded ? AverageGroundNormal : Vector3.up;

        Vector3 torque = Vector3.Cross(chassisUp, target) * uprightAssist;
        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    private void ApplyStickToGround()
    {
        if (!IsAnyWheelGrounded || stickToGroundForce <= 0f) return;

        float groundedRatio = GroundedWheelCount / (float)wheels.Length;
        rb.AddForce(-rb.transform.up * stickToGroundForce * groundedRatio, ForceMode.Acceleration);
    }

    private void UpdateWheelVisual(Wheel wheel)
    {
        if (wheel == null) return;

        Transform target = wheel.visualOffsetTarget != null ? wheel.visualOffsetTarget : wheel.wheelPivot;
        if (target == null) return;

        float restCompression = restLength * 0.5f;
        float travelDelta = wheel.currentCompression - restCompression;
        travelDelta = Mathf.Clamp(travelDelta, -visualMaxTravel, visualMaxTravel);

        Transform parent = target.parent;
        Vector3 worldOffset = rb.transform.up * travelDelta;
        Vector3 localOffset = parent != null ? parent.InverseTransformVector(worldOffset) : worldOffset;

        Vector3 desiredLocal = wheel.visualTargetBaseLocalPos + localOffset;
        target.localPosition = Vector3.Lerp(
            target.localPosition, desiredLocal,
            Time.deltaTime * visualSmooth
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos || wheels == null) return;

        Transform chassis = rb != null ? rb.transform : transform;
        Vector3 chassisUp = chassis.up;

        foreach (Wheel wheel in wheels)
        {
            if (wheel == null || wheel.wheelPivot == null) continue;

            Vector3 anchor = wheel.wheelPivot.position + chassisUp * anchorOffsetUp;
            Vector3 restCenter = wheel.wheelPivot.position;
            Vector3 maxExtensionCenter = anchor - chassisUp * restLength;

            // Wheel at rest (visual reference - should match the visual wheel)
            Gizmos.color = wheel.isGrounded ? Color.green : Color.cyan;
            Gizmos.DrawWireSphere(restCenter, wheelRadius);

            // Anchor (spring attachment to chassis)
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(anchor, 0.04f);

            // Suspension travel envelope
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
            Gizmos.DrawLine(anchor, maxExtensionCenter);
        }
    }
}
