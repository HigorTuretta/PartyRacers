using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class KartCameraDynamics : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private KartController kart;
    [SerializeField] private Transform cameraFollowTarget;
    [SerializeField] private Transform cameraLookTarget;
    [SerializeField] private CinemachineCamera virtualCamera;

    [Header("Chase Central")]
    [SerializeField] private Vector3 baseFollowLocalPosition = new Vector3(0f, 1.08f, 0.72f);
    [SerializeField] private Vector3 baseLookLocalPosition = new Vector3(0f, 1.02f, 3.45f);
    [SerializeField] private float speedPullback = -0.85f;
    [SerializeField] private float speedHeightLift = 0.16f;
    [SerializeField] private float speedLookAhead = 1.55f;

    [Header("Aceleracao / Freada")]
    [SerializeField] private float accelerationBackOffset = -0.35f;
    [SerializeField] private float brakeForwardOffset = 0.45f;
    [SerializeField] private float brakeUpOffset = 0.15f;

    [Header("Antecipacao de Curva")]
    [SerializeField] private float turnLookAheadOffset = 0.35f;
    [SerializeField] private float driftLookAheadOffset = 0.85f;
    [SerializeField] private float driftPullback = -0.55f;
    [SerializeField] private float driftHeightLift = 0.18f;

    [Header("Drift Camera (olhar pra frente)")]
    [SerializeField, Range(0f, 1f)] private float driftLookYawFactor = 0.88f;
    [SerializeField] private float driftLookYawMax = 44f;
    [SerializeField, Range(0f, 1f)] private float driftFollowYawFactor = 0.58f;
    [SerializeField] private float driftFollowYawMax = 34f;
    [SerializeField] private float driftYawSmooth = 12f;

    [Header("FOV de Velocidade")]
    [SerializeField] private float baseFieldOfView = 61f;
    [SerializeField] private float speedFieldOfView = 72f;
    [SerializeField] private float boostFieldOfViewBonus = 6f;
    [SerializeField] private float driftFieldOfViewBonus = 5f;
    [SerializeField] private float fieldOfViewSmooth = 8f;

    [Header("Mouse")]
    [SerializeField] private bool requireRightMouseButton = true;
    [SerializeField] private float mouseSensitivityX = 0.18f;
    [SerializeField] private float mouseSensitivityY = 0.08f;
    [SerializeField] private float maxMouseYaw = 55f;
    [SerializeField] private float minMousePitchOffset = -0.35f;
    [SerializeField] private float maxMousePitchOffset = 0.45f;
    [SerializeField] private float mouseReturnSpeed = 6f;

    [Header("Antecipacao de Velocidade Lateral")]
    [SerializeField] private float velocityLookAheadStrength = 0.58f;
    [SerializeField] private float velocityLookAheadSmooth = 7f;

    [Header("Inclinacao de Curva")]
    [SerializeField] private float steerBankAngle = 3.5f;
    [SerializeField] private float driftBankAngle = 8f;
    [SerializeField] private float bankSmooth = 10f;

    [Header("Oscilacao")]
    [SerializeField] private float speedShakeAmount = 0.018f;
    [SerializeField] private float driftShakeAmount = 0.026f;
    [SerializeField] private float burnoutShakeAmount = 0.045f;
    [SerializeField] private float shakeFrequency = 18f;
    [Tooltip("Tranco da câmera ao bater em paredes/rampas ou aterrissar forte (reage ao impacto do kart).")]
    [SerializeField] private float impactShakeAmount = 0.20f;
    [SerializeField] private float impactShakeFrequency = 34f;

    [Header("Suavizacao")]
    [SerializeField] private float followSmooth = 10f;
    [SerializeField] private float lookSmooth = 8f;
    [SerializeField] private float rotationSmooth = 12f;

    private float mouseYaw;
    private float mousePitchOffset;
    private int lastDriftDirection = 1;
    private float currentFieldOfView;
    private float currentDriftFollowYaw;
    private float currentDriftLookYaw;
    private Vector3 smoothedVelocityLookOffset;
    private float currentBankAngle;

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        if (cameraFollowTarget == null)
            cameraFollowTarget = transform.Find("CameraFollowTarget");

        if (cameraLookTarget == null)
            cameraLookTarget = transform.Find("CameraLookTarget");

        if (virtualCamera == null)
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();

        currentFieldOfView = virtualCamera != null
            ? virtualCamera.Lens.FieldOfView
            : baseFieldOfView;

        LinkVirtualCameraTargets();
    }

    private void LateUpdate()
    {
        if (kart == null || cameraFollowTarget == null)
            return;

        UpdateMouseCamera();
        LinkVirtualCameraTargets();

        float speed01 = kart.Speed01;
        float driftBlend = kart.DriftBlend;

        if (kart.DriftDirection != 0)
            lastDriftDirection = kart.DriftDirection;

        Vector3 followPosition = baseFollowLocalPosition;
        Vector3 lookPosition = baseLookLocalPosition;

        followPosition.z += speedPullback * speed01;
        followPosition.y += speedHeightLift * speed01;
        lookPosition.z += speedLookAhead * speed01;

        if (kart.ThrottleInput > 0f)
            followPosition.z += accelerationBackOffset * speed01;

        if (kart.BrakeInput > 0f && kart.ForwardSpeed > 2f)
        {
            followPosition.z += brakeForwardOffset * speed01;
            followPosition.y += brakeUpOffset * speed01;
        }

        if (driftBlend > 0.01f)
        {
            followPosition.z += driftPullback * driftBlend;
            followPosition.y += driftHeightLift * driftBlend;
            lookPosition.x += lastDriftDirection * driftLookAheadOffset * driftBlend;
        }

        lookPosition.x += kart.TurnInput * turnLookAheadOffset * speed01;

        // look-ahead lateral baseado na velocidade real do kart (util no drift)
        Vector3 localVelocity = transform.InverseTransformDirection(kart.Rigidbody.linearVelocity);
        Vector3 velocityLookTarget = new Vector3(localVelocity.x * velocityLookAheadStrength, 0f, 0f);
        smoothedVelocityLookOffset = Vector3.Lerp(smoothedVelocityLookOffset, velocityLookTarget, Time.deltaTime * velocityLookAheadSmooth);
        lookPosition += smoothedVelocityLookOffset;

        followPosition.y += mousePitchOffset;
        followPosition += CalculateShake(speed01, driftBlend);

        // inclinacao suave em curvas e drift
        float driftBankDirection = kart.DriftDirection != 0 ? kart.DriftDirection : lastDriftDirection;
        float targetBank = -(kart.TurnInput * steerBankAngle + driftBankDirection * driftBankAngle * driftBlend) * speed01;
        currentBankAngle = Mathf.Lerp(currentBankAngle, targetBank, Time.deltaTime * bankSmooth);

        UpdateDriftYaw(driftBlend);

        Quaternion driftLookRotation = Quaternion.Euler(0f, currentDriftLookYaw, 0f);
        Vector3 rotatedLookPosition = driftLookRotation * lookPosition;

        cameraFollowTarget.localPosition = Vector3.Lerp(
            cameraFollowTarget.localPosition,
            followPosition,
            Time.deltaTime * followSmooth
        );

        cameraFollowTarget.localRotation = Quaternion.Slerp(
            cameraFollowTarget.localRotation,
            Quaternion.Euler(0f, mouseYaw + currentDriftFollowYaw, currentBankAngle),
            Time.deltaTime * rotationSmooth
        );

        if (cameraLookTarget != null)
        {
            cameraLookTarget.localPosition = Vector3.Lerp(
                cameraLookTarget.localPosition,
                rotatedLookPosition,
                Time.deltaTime * lookSmooth
            );

            cameraLookTarget.localRotation = Quaternion.identity;
        }

        UpdateFieldOfView(speed01, driftBlend);
    }

    private void UpdateDriftYaw(float driftBlend)
    {
        float slip = kart.SlipAngleDeg;

        float lookTarget = Mathf.Clamp(
            -slip * driftLookYawFactor,
            -driftLookYawMax,
            driftLookYawMax
        ) * driftBlend;

        float followTarget = Mathf.Clamp(
            slip * driftFollowYawFactor,
            -driftFollowYawMax,
            driftFollowYawMax
        ) * driftBlend;

        currentDriftLookYaw = Mathf.Lerp(
            currentDriftLookYaw,
            lookTarget,
            Time.deltaTime * driftYawSmooth
        );

        currentDriftFollowYaw = Mathf.Lerp(
            currentDriftFollowYaw,
            followTarget,
            Time.deltaTime * driftYawSmooth
        );
    }

    private void UpdateMouseCamera()
    {
        Mouse mouse = Mouse.current;

        if (mouse == null)
            return;

        bool canMoveCamera = !requireRightMouseButton || mouse.rightButton.isPressed;

        if (canMoveCamera)
        {
            Vector2 delta = mouse.delta.ReadValue();

            mouseYaw += delta.x * mouseSensitivityX;
            mousePitchOffset -= delta.y * mouseSensitivityY;

            mouseYaw = Mathf.Clamp(mouseYaw, -maxMouseYaw, maxMouseYaw);
            mousePitchOffset = Mathf.Clamp(mousePitchOffset, minMousePitchOffset, maxMousePitchOffset);

            return;
        }

        mouseYaw = Mathf.Lerp(mouseYaw, 0f, Time.deltaTime * mouseReturnSpeed);
        mousePitchOffset = Mathf.Lerp(mousePitchOffset, 0f, Time.deltaTime * mouseReturnSpeed);
    }

    private void LinkVirtualCameraTargets()
    {
        if (virtualCamera == null)
            return;

        virtualCamera.Follow = cameraFollowTarget;

        if (cameraLookTarget != null)
            virtualCamera.LookAt = cameraLookTarget;
    }

    private Vector3 CalculateShake(float speed01, float driftBlend)
    {
        float shakeAmount = speedShakeAmount * speed01;
        shakeAmount += driftShakeAmount * driftBlend;

        if (kart.IsBurningOut)
            shakeAmount += burnoutShakeAmount;

        float impact = kart.RecentImpact01;

        if (shakeAmount <= 0.0001f && impact <= 0.0001f)
            return Vector3.zero;

        float time = Time.time * shakeFrequency;

        Vector3 ambient = new Vector3(
            (Mathf.PerlinNoise(time * 0.73f, 0f)          - 0.5f) * 2f * shakeAmount,
            (Mathf.PerlinNoise(0f,           time * 0.91f) - 0.5f) * 2f * shakeAmount * 0.45f,
            0f
        );

        if (impact <= 0.0001f)
            return ambient;

        // Tranco rápido e direcional no impacto, decaindo com kart.RecentImpact01.
        float impactTime = Time.time * impactShakeFrequency;
        float kick = impactShakeAmount * impact;
        Vector3 impactShake = new Vector3(
            Mathf.Sin(impactTime * 1.7f) * kick,
            Mathf.Sin(impactTime * 2.3f) * kick * 0.7f,
            -kick * 0.6f
        );

        return ambient + impactShake;
    }

    private void UpdateFieldOfView(float speed01, float driftBlend)
    {
        if (virtualCamera == null)
            return;

        float targetFieldOfView = Mathf.Lerp(baseFieldOfView, speedFieldOfView, speed01);

        if (kart.IsBoosting)
            targetFieldOfView += boostFieldOfViewBonus;

        targetFieldOfView += driftFieldOfViewBonus * driftBlend;

        currentFieldOfView = Mathf.Lerp(
            currentFieldOfView,
            targetFieldOfView,
            Time.deltaTime * fieldOfViewSmooth
        );

        LensSettings lens = virtualCamera.Lens;
        lens.FieldOfView = currentFieldOfView;
        virtualCamera.Lens = lens;
    }
}
