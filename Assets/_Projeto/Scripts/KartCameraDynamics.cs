using UnityEngine;
using UnityEngine.InputSystem;

public class KartCameraDynamics : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private Transform cameraFollowTarget;

    [Header("Posição Base")]
    [SerializeField] private Vector3 baseLocalPosition = new Vector3(0f, 1.2f, 0.8f);

    [Header("Aceleração / Freada")]
    [SerializeField] private float accelerationBackOffset = -0.35f;
    [SerializeField] private float brakeForwardOffset = 0.45f;
    [SerializeField] private float brakeUpOffset = 0.15f;

    [Header("Drift Cinematográfico")]
    [SerializeField] private float driftSideOffset = 3.2f;
    [SerializeField] private float driftYawAngle = 42f;
    [SerializeField] private float driftForwardOffset = 1.1f;
    [SerializeField] private float driftHeightOffset = 0.25f;

    [Header("Mouse")]
    [SerializeField] private bool requireRightMouseButton = false;
    [SerializeField] private float mouseSensitivityX = 0.18f;
    [SerializeField] private float mouseSensitivityY = 0.08f;
    [SerializeField] private float maxMouseYaw = 120f;
    [SerializeField] private float minMousePitchOffset = -0.7f;
    [SerializeField] private float maxMousePitchOffset = 0.9f;
    [SerializeField] private float mouseReturnSpeed = 3.5f;

    [Header("Oscilação")]
    [SerializeField] private float speedBobAmount = 0.06f;
    [SerializeField] private float speedBobFrequency = 9f;

    [Header("Suavização")]
    [SerializeField] private float positionSmooth = 12f;
    [SerializeField] private float rotationSmooth = 11f;

    private float mouseYaw;
    private float mousePitchOffset;
    private int lastDriftDirection = 1;

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        if (cameraFollowTarget == null)
        {
            Transform foundTarget = transform.Find("CameraFollowTarget");

            if (foundTarget != null)
                cameraFollowTarget = foundTarget;
        }
    }

    private void LateUpdate()
    {
        if (kart == null || cameraFollowTarget == null)
            return;

        UpdateMouseCamera();

        float speed01 = kart.Speed01;
        float driftBlend = kart.DriftBlend;

        if (kart.DriftDirection != 0)
            lastDriftDirection = kart.DriftDirection;

        Vector3 targetPosition = baseLocalPosition;

        if (kart.ThrottleInput > 0f)
            targetPosition.z += accelerationBackOffset * speed01;

        if (kart.BrakeInput > 0f && kart.ForwardSpeed > 2f)
        {
            targetPosition.z += brakeForwardOffset;
            targetPosition.y += brakeUpOffset;
        }

        if (driftBlend > 0.01f)
        {
            targetPosition.x += -lastDriftDirection * driftSideOffset * driftBlend;
            targetPosition.z += driftForwardOffset * driftBlend;
            targetPosition.y += driftHeightOffset * driftBlend;
        }

        targetPosition.y += mousePitchOffset;

        float bob = Mathf.Sin(Time.time * speedBobFrequency) * speedBobAmount * speed01;
        targetPosition.y += bob;

        cameraFollowTarget.localPosition = Vector3.Lerp(
            cameraFollowTarget.localPosition,
            targetPosition,
            Time.deltaTime * positionSmooth
        );

        float driftYaw = driftBlend > 0.01f
            ? lastDriftDirection * driftYawAngle * driftBlend
            : 0f;

        float targetYaw = driftYaw + mouseYaw;

        Quaternion targetRotation = Quaternion.Euler(0f, targetYaw, 0f);

        cameraFollowTarget.localRotation = Quaternion.Slerp(
            cameraFollowTarget.localRotation,
            targetRotation,
            Time.deltaTime * rotationSmooth
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
}