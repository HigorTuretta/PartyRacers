using UnityEngine;

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
    [SerializeField] private float bodyVerticalBounce = 0.018f;
    [SerializeField] private float bodyLiftOnLean = 0.008f;
    [SerializeField] private float bodyLiftOnPitch = 0.004f;
    [SerializeField] private float accelerationCompression = 0.035f;
    [SerializeField] private float brakeCompression = 0.045f;
    [SerializeField] private float driftCompression = 0.035f;
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
        float accelerationLoad = Mathf.Clamp01(Mathf.Max(0f, localAcceleration.z) / 14f);
        float brakeLoad = Mathf.Clamp01(Mathf.Max(0f, -localAcceleration.z) / 18f);

        if (kart.ThrottleInput > 0f)
            accelerationLoad = Mathf.Max(accelerationLoad, 0.35f * speedFactor);

        if (kart.BrakeInput > 0f && kart.ForwardSpeed > 1f)
            brakeLoad = Mathf.Max(brakeLoad, 0.7f * speedFactor);

        if (kart.IsBurningOut)
            accelerationLoad = Mathf.Max(accelerationLoad, 0.45f);

        float targetPitch = (brakePitchAngle * brakeLoad) - (accelerationPitchAngle * accelerationLoad);

        float bounce = Mathf.Sin(Time.time * 18f) * bodyVerticalBounce * speedFactor;

        float leanLift = Mathf.Abs(targetRoll) / Mathf.Max(1f, turnLeanAngle);
        float pitchLift = Mathf.Abs(targetPitch) / Mathf.Max(1f, brakePitchAngle);

        float protectiveLift =
            Mathf.Clamp01(leanLift) * bodyLiftOnLean +
            Mathf.Clamp01(pitchLift) * bodyLiftOnPitch;

        float targetVerticalOffset =
            bounce +
            protectiveLift -
            (accelerationLoad * accelerationCompression) -
            (brakeLoad * brakeCompression) -
            (driftFactor * driftCompression);

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
                noiseA * burnoutShakePosition,
                noiseB * burnoutShakePosition * 0.28f,
                noiseB * burnoutShakePosition
            );

            targetPosition += shakeOffset;

            targetRotation *= Quaternion.Euler(
                noiseB * burnoutShakeRotation,
                noiseA * burnoutShakeRotation,
                noiseA * burnoutShakeRotation * 0.25f
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
        Quaternion spinRotation = Quaternion.AngleAxis(spinAmount, wheelSpinAxis);

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

        if (!createRuntimeWheelPivots)
            return new WheelVisual(wheel);

        Transform parent = wheel.parent;

        if (parent == null)
            return new WheelVisual(wheel);

        Vector3 pivotWorldPosition = GetRendererBoundsCenter(wheel);
        GameObject pivotObject = new GameObject($"{wheel.name}_{wheelName}_RuntimePivot");
        Transform pivot = pivotObject.transform;

        pivot.SetParent(parent, false);
        pivot.SetPositionAndRotation(pivotWorldPosition, wheel.rotation);
        pivot.localScale = Vector3.one;

        wheel.SetParent(pivot, true);

        return new WheelVisual(pivot);
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
        public WheelVisual(Transform transform)
        {
            Transform = transform;
            BaseLocalRotation = transform.localRotation;
        }

        public Transform Transform { get; }
        public Quaternion BaseLocalRotation { get; }
    }
}
