using UnityEngine;
using UnityEngine.Serialization;

public class KartArcadeVisuals : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private Transform body;

    [Header("Rodas")]
    [SerializeField] private Transform wheelFrontLeft;
    [SerializeField] private Transform wheelFrontRight;
    [SerializeField] private Transform wheelRearLeft;
    [SerializeField] private Transform wheelRearRight;

    [Header("Eixo das Rodas")]
    [SerializeField] private Vector3 wheelSpinAxis = Vector3.forward;
    [SerializeField] private float wheelSpinDirection = 1f;
    [SerializeField] private bool createRuntimeWheelPivots = true;

    [Header("Corpo Arcade")]
    [SerializeField] private float turnLeanAngle = 4f;
    [SerializeField] private float accelerationPitchAngle = 2f;
    [SerializeField] private float brakePitchAngle = 5f;
    [SerializeField] private float driftLeanMultiplier = 1.35f;
    [SerializeField] private float driftCounterLeanAngle = 5.5f;
    [SerializeField] private float driftNoseDiveAngle = 1.2f;
    [SerializeField] private float bodyVerticalBounce = 0.018f;
    [SerializeField] private float bodyLiftOnLean = 0.035f;
    [SerializeField] private float bodyLiftOnPitch = 0.004f;
    [SerializeField] private float accelerationCompression = 0.035f;
    [SerializeField] private float brakeCompression = 0.045f;
    [FormerlySerializedAs("driftCompression")]
    [SerializeField] private float driftBodyLift = 0.04f;
    [SerializeField] private float suspensionResponse = 9f;
    [SerializeField] private float visualSmooth = 12f;

    [Header("Rodas")]
    [SerializeField] private float wheelSpinSpeed = 900f;
    [SerializeField] private float burnoutWheelSpinSpeed = 1600f;
    [SerializeField] private float frontWheelSteerAngle = 25f;

    [Header("Burnout")]
    [SerializeField] private float burnoutShakePosition = 0.025f;
    [SerializeField] private float burnoutShakeRotation = 1.6f;
    [SerializeField] private float burnoutShakeFrequency = 38f;

    private Quaternion visualRootBaseRotation;
    private Vector3 visualRootBasePosition;

    private Quaternion bodyBaseRotation;
    private Vector3 bodyBasePosition;

    private WheelVisual frontLeftWheel;
    private WheelVisual frontRightWheel;
    private WheelVisual rearLeftWheel;
    private WheelVisual rearRightWheel;

    private float frontWheelSpin;
    private float rearWheelSpin;
    private float bodyVerticalOffset;
    private Vector3 previousLocalVelocity;

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        wheelSpinAxis = wheelSpinAxis.sqrMagnitude > 0.0001f
            ? wheelSpinAxis.normalized
            : Vector3.forward;

        if (visualRoot != null)
        {
            visualRootBaseRotation = visualRoot.localRotation;
            visualRootBasePosition = visualRoot.localPosition;
        }

        if (body != null)
        {
            bodyBaseRotation = body.localRotation;
            bodyBasePosition = body.localPosition;
        }

        frontLeftWheel = CreateWheelVisual(wheelFrontLeft, "FrontLeft");
        frontRightWheel = CreateWheelVisual(wheelFrontRight, "FrontRight");
        rearLeftWheel = CreateWheelVisual(wheelRearLeft, "RearLeft");
        rearRightWheel = CreateWheelVisual(wheelRearRight, "RearRight");

        previousLocalVelocity = GetLocalVelocity();
    }

    // Reatribui as 4 rodas em runtime e reconstrói o cache de pivôs.
    // Usado pelo KartVisualCustomizer quando o modelo do carro (e suas rodas)
    // é instanciado dinamicamente. A lógica de animação permanece inalterada.
    public void SetWheels(Transform frontLeft, Transform frontRight, Transform rearLeft, Transform rearRight)
    {
        wheelFrontLeft = frontLeft;
        wheelFrontRight = frontRight;
        wheelRearLeft = rearLeft;
        wheelRearRight = rearRight;

        frontLeftWheel = CreateWheelVisual(wheelFrontLeft, "FrontLeft");
        frontRightWheel = CreateWheelVisual(wheelFrontRight, "FrontRight");
        rearLeftWheel = CreateWheelVisual(wheelRearLeft, "RearLeft");
        rearRightWheel = CreateWheelVisual(wheelRearRight, "RearRight");
    }

    private void LateUpdate()
    {
        if (kart == null)
            return;

        StabilizeVisualRoot();
        AnimateBody();
        AnimateWheels();
    }

    private void StabilizeVisualRoot()
    {
        if (visualRoot == null)
            return;

        visualRoot.localPosition = Vector3.Lerp(
            visualRoot.localPosition,
            visualRootBasePosition,
            Time.deltaTime * visualSmooth
        );

        visualRoot.localRotation = Quaternion.Slerp(
            visualRoot.localRotation,
            visualRootBaseRotation,
            Time.deltaTime * visualSmooth
        );
    }

    private void AnimateBody()
    {
        if (body == null)
            return;

        float speedFactor = kart.Speed01;
        float driftFactor = kart.DriftBlend;
        Vector3 localAcceleration = GetLocalAcceleration();

        float leanMultiplier = Mathf.Lerp(1f, driftLeanMultiplier, driftFactor);

        float targetRoll = kart.TurnInput * turnLeanAngle * speedFactor * leanMultiplier;
        targetRoll -= kart.DriftSignedAmount * driftCounterLeanAngle * speedFactor;
        float accelerationLoad = Mathf.Clamp01(Mathf.Max(0f, localAcceleration.z) / 14f);
        float brakeLoad = Mathf.Clamp01(Mathf.Max(0f, -localAcceleration.z) / 18f);

        if (kart.ThrottleInput > 0f)
            accelerationLoad = Mathf.Max(accelerationLoad, 0.35f * speedFactor);

        if (kart.BrakeInput > 0f && kart.ForwardSpeed > 1f)
            brakeLoad = Mathf.Max(brakeLoad, 0.7f * speedFactor);

        if (kart.IsBurningOut)
            accelerationLoad = Mathf.Max(accelerationLoad, 0.18f);

        float targetPitch = (brakePitchAngle * brakeLoad) - (accelerationPitchAngle * accelerationLoad);
        targetPitch += driftFactor * driftNoseDiveAngle * speedFactor;

        float bounce = (Mathf.PerlinNoise(Time.time * 2.8f, 0f) - 0.5f) * 2f * bodyVerticalBounce * speedFactor;

        float leanLift = Mathf.Abs(targetRoll) / Mathf.Max(1f, turnLeanAngle);
        float pitchLift = Mathf.Abs(targetPitch) / Mathf.Max(1f, brakePitchAngle);

        float protectiveLift =
            Mathf.Clamp01(leanLift) * bodyLiftOnLean +
            Mathf.Clamp01(pitchLift) * bodyLiftOnPitch;

        float targetVerticalOffset =
            bounce +
            protectiveLift +
            (driftFactor * driftBodyLift) -
            (accelerationLoad * accelerationCompression) -
            (brakeLoad * brakeCompression);

        bodyVerticalOffset = Mathf.Lerp(
            bodyVerticalOffset,
            targetVerticalOffset,
            Time.deltaTime * suspensionResponse
        );

        Vector3 targetPosition = bodyBasePosition + new Vector3(
            0f,
            bodyVerticalOffset,
            0f
        );

        Quaternion targetRotation = bodyBaseRotation * Quaternion.Euler(
            targetPitch,
            0f,
            targetRoll
        );

        if (kart.IsBurningOut)
        {
            float noiseA = Mathf.Sin(Time.time * burnoutShakeFrequency);
            float noiseB = Mathf.Cos(Time.time * burnoutShakeFrequency * 0.73f);

            Vector3 shakeOffset = new Vector3(
                0f,
                Mathf.Abs(noiseB) * burnoutShakePosition * 0.5f,
                0f
            );

            targetPosition += shakeOffset;

            targetRotation *= Quaternion.Euler(
                noiseA * burnoutShakeRotation * 0.5f,
                0f,
                noiseB * burnoutShakeRotation * 0.2f
            );
        }

        body.localPosition = Vector3.Lerp(
            body.localPosition,
            targetPosition,
            Time.deltaTime * visualSmooth
        );

        body.localRotation = Quaternion.Slerp(
            body.localRotation,
            targetRotation,
            Time.deltaTime * visualSmooth
        );
    }

    private void AnimateWheels()
    {
        float forwardSpeed = kart.ForwardSpeed;

        float normalSpinAmount = forwardSpeed * wheelSpinSpeed * Time.deltaTime * wheelSpinDirection;

        frontWheelSpin += normalSpinAmount;
        rearWheelSpin += normalSpinAmount;

        if (kart.IsBurningOut)
        {
            float burnoutDirection = kart.ThrottleInput > 0f ? 1f : -1f;
            rearWheelSpin += burnoutWheelSpinSpeed * burnoutDirection * Time.deltaTime * wheelSpinDirection;
        }

        float steerAmount = kart.TurnInput * frontWheelSteerAngle;

        RotateWheel(frontLeftWheel, frontWheelSpin, steerAmount, true);
        RotateWheel(frontRightWheel, frontWheelSpin, steerAmount, true);
        RotateWheel(rearLeftWheel, rearWheelSpin, 0f, false);
        RotateWheel(rearRightWheel, rearWheelSpin, 0f, false);
    }

    private void RotateWheel(WheelVisual wheel, float spinAmount, float steerAmount, bool canSteer)
    {
        if (wheel == null || wheel.Transform == null)
            return;

        Quaternion steerRotation = canSteer ? Quaternion.Euler(0f, steerAmount, 0f) : Quaternion.identity;
        Quaternion spinRotation = Quaternion.AngleAxis(spinAmount * wheel.SpinSign, wheelSpinAxis);

        wheel.Transform.localRotation = wheel.BaseLocalRotation * steerRotation * spinRotation;
    }

    private Vector3 GetLocalVelocity()
    {
        if (kart == null || kart.Rigidbody == null)
            return Vector3.zero;

        return kart.transform.InverseTransformDirection(kart.Rigidbody.linearVelocity);
    }

    private Vector3 GetLocalAcceleration()
    {
        Vector3 localVelocity = GetLocalVelocity();
        Vector3 localAcceleration = Time.deltaTime > 0f
            ? (localVelocity - previousLocalVelocity) / Time.deltaTime
            : Vector3.zero;

        previousLocalVelocity = localVelocity;

        return Vector3.ClampMagnitude(localAcceleration, 30f);
    }

    private WheelVisual CreateWheelVisual(Transform wheel, string wheelName)
    {
        if (wheel == null)
            return null;

        Transform pivot;
        if (!createRuntimeWheelPivots || wheel.parent == null)
        {
            pivot = wheel;
        }
        else
        {
            Vector3 pivotWorldPosition = GetRendererBoundsCenter(wheel);
            GameObject pivotObject = new GameObject($"{wheel.name}_{wheelName}_RuntimePivot");
            pivot = pivotObject.transform;
            pivot.SetParent(wheel.parent, false);
            pivot.SetPositionAndRotation(pivotWorldPosition, wheel.rotation);
            pivot.localScale = Vector3.one;
            wheel.SetParent(pivot, true);
        }

        // Hub axis (wheelSpinAxis) in wheel local space → projected on kart's lateral axis (right).
        // Wheels whose hub points opposite to kart.right must spin with opposite local sign so the
        // wheel rolls forward in world space regardless of which side it sits on.
        Vector3 worldHubAxis = pivot.TransformDirection(wheelSpinAxis);
        Vector3 kartRight = kart != null ? kart.transform.right : Vector3.right;
        float dot = Vector3.Dot(worldHubAxis, kartRight);
        float spinSign = dot >= 0f ? 1f : -1f;

        return new WheelVisual(pivot, spinSign);
    }

    private Vector3 GetRendererBoundsCenter(Transform wheel)
    {
        Renderer[] renderers = wheel.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return wheel.position;

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.center;
    }

    private sealed class WheelVisual
    {
        public WheelVisual(Transform transform, float spinSign = 1f)
        {
            Transform = transform;
            BaseLocalRotation = transform.localRotation;
            SpinSign = spinSign;
        }

        public Transform Transform { get; }
        public Quaternion BaseLocalRotation { get; }
        public float SpinSign { get; }
    }
}
