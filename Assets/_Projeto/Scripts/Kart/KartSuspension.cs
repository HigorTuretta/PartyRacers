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
    [SerializeField] private float springStrength = 14000f;
    [SerializeField] private float springDamping = 2400f;
    [SerializeField, Range(0f, 1f)] private float compressionDampRatio = 0.50f;

    [Header("Visual")]
    [Tooltip("Move o visualOffsetTarget pra cima/baixo conforme a compressão. Desligue se houver conflito.")]
    [SerializeField] private bool driveVisualOffset = true;
    [Tooltip("Travel visual máximo quando a mola está COMPRIMIDA (roda subindo em direção ao chassi).")]
    [SerializeField] private float visualMaxCompression = 0.08f;
    [Tooltip("Travel visual máximo quando a mola está ESTICADA (roda descendo afastando do chassi). Maior = roda pendurada mais visível no ar.")]
    [SerializeField] private float visualMaxExtension = 0.24f;
    [SerializeField] private float visualSmooth = 22f;

    [Header("Estabilizadores")]
    [Tooltip("Torque corretivo para alinhar o chassi à normal do terreno (funciona sem lock de rotação).")]
    [SerializeField] private float uprightAssist = 40f;
    [Tooltip("Força extra que cola o kart ao terreno (na direção da normal do chão).")]
    [SerializeField] private float stickToGroundForce = 8f;

    [Header("Barra Anti-Roll")]
    [Tooltip("Índice no array Wheels de cada roda. FL=0 FR=1 RL=2 RR=3 por padrão.")]
    [SerializeField] private int frontLeftIdx = 0;
    [SerializeField] private int frontRightIdx = 1;
    [SerializeField] private int rearLeftIdx = 2;
    [SerializeField] private int rearRightIdx = 3;
    [Tooltip("Resistência ao tombamento no eixo dianteiro.")]
    [SerializeField] private float antiRollFront = 8000f;
    [Tooltip("Resistência ao tombamento no eixo traseiro.")]
    [SerializeField] private float antiRollRear = 6000f;

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
        ApplyAntiRoll(frontLeftIdx, frontRightIdx, antiRollFront);
        ApplyAntiRoll(rearLeftIdx, rearRightIdx, antiRollRear);
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

        // SphereCast em vez de Raycast: muito mais robusto em terreno irregular
        // (não cai entre arestas do mesh, detecta declives próximos antes do raio puro)
        const float castRadius = 0.12f;
        float maxRayLength = restLength + wheelRadius - castRadius;

        wheel.previousCompression = wheel.currentCompression;

        if (Physics.SphereCast(anchorPosition, castRadius, rayDirection, out RaycastHit hit, maxRayLength, groundMask, QueryTriggerInteraction.Ignore))
        {
            // hit.distance é o quanto o CENTRO da esfera viajou. Soma castRadius p/ o contato real.
            float distanceToWheelGround = hit.distance + castRadius - wheelRadius;
            float compression = Mathf.Clamp(restLength - distanceToWheelGround, 0f, restLength);

            wheel.currentCompression = compression;
            wheel.isGrounded = true;
            wheel.hitPoint = hit.point;
            wheel.hitNormal = hit.normal;

            float compressionRatio = compression / Mathf.Max(0.0001f, restLength);
            float compressionVelocity = (compression - wheel.previousCompression) / Mathf.Max(0.0001f, Time.fixedDeltaTime);

            float springForce = springStrength * compressionRatio;

            // Damping assimétrico: compressão rápida (impacto/zebra) usa fração reduzida
            // para não amplificar a força de impacto; rebound usa damping completo para
            // segurar o kart contra a mola.
            float dampRatio = compressionVelocity > 0f ? compressionDampRatio : 1f;
            float dampForce = springDamping * compressionVelocity * dampRatio;

            float totalForce = Mathf.Max(0f, springForce + dampForce);

            rb.AddForceAtPosition(chassisUp * totalForce, anchorPosition);
        }
        else
        {
            wheel.isGrounded = false;
            wheel.hitNormal = Vector3.up;
            // Decay rápido para 0 quando no ar: a roda visualmente "cai" pra posição esticada
            wheel.currentCompression = Mathf.MoveTowards(
                wheel.currentCompression, 0f,
                restLength * 6f * Time.fixedDeltaTime
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
        rb.AddForce(-AverageGroundNormal * stickToGroundForce * groundedRatio, ForceMode.Acceleration);
    }

    private void ApplyAntiRoll(int leftIdx, int rightIdx, float strength)
    {
        if (leftIdx >= wheels.Length || rightIdx >= wheels.Length) return;

        Wheel left = wheels[leftIdx];
        Wheel right = wheels[rightIdx];

        if (left == null || right == null) return;

        float leftTravel = left.isGrounded
            ? left.currentCompression / Mathf.Max(0.0001f, restLength)
            : -1f;
        float rightTravel = right.isGrounded
            ? right.currentCompression / Mathf.Max(0.0001f, restLength)
            : -1f;

        if (!left.isGrounded && !right.isGrounded) return;

        float antiRollForce = (leftTravel - rightTravel) * strength;

        if (left.isGrounded && left.wheelPivot != null)
            rb.AddForceAtPosition(rb.transform.up * -antiRollForce, left.wheelPivot.position);
        if (right.isGrounded && right.wheelPivot != null)
            rb.AddForceAtPosition(rb.transform.up * antiRollForce, right.wheelPivot.position);
    }

    private void UpdateWheelVisual(Wheel wheel)
    {
        if (wheel == null) return;

        Transform target = wheel.visualOffsetTarget != null ? wheel.visualOffsetTarget : wheel.wheelPivot;
        if (target == null) return;

        // Posição "neutra" = compressão de equilíbrio (~55% com nossos valores).
        // Acima disso a roda sobe (suspensão comprimida); abaixo desce (suspensão esticada).
        float restCompression = restLength * 0.55f;
        float travelDelta = wheel.currentCompression - restCompression;

        // Travel ASSIMÉTRICO: extensão (roda no ar / mola esticada) muito maior que compressão.
        // travelDelta positivo = comprimido (roda sobe pra chassi). negativo = esticado (roda desce).
        travelDelta = Mathf.Clamp(travelDelta, -visualMaxExtension, visualMaxCompression);

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
