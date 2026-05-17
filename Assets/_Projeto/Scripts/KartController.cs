using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    private const float KmhToMps = 1f / 3.6f;

    [Header("Referências")]
    [SerializeField] private Rigidbody rb;

    [Header("Velocidade")]
    [SerializeField] private float maxForwardSpeedKmh = 200f;
    [SerializeField] private float maxReverseSpeedKmh = 35f;

    [Header("Aceleração e Freio")]
    [SerializeField] private float acceleration = 42f;
    [SerializeField] private float reverseAcceleration = 22f;
    [SerializeField] private float brakeDeceleration = 65f;
    [SerializeField] private float naturalDeceleration = 4f;

    [Header("Direção")]
    [SerializeField] private float minSteerSpeedKmh = 6f;
    [SerializeField] private float fullSteerSpeedKmh = 35f;
    [SerializeField] private float lowSpeedTurnRate = 95f;
    [SerializeField] private float highSpeedTurnRate = 145f;
    [SerializeField] private float highSpeedSteerReduction = 0.55f;

    [Header("Drift Arcade - Ativação")]
    [SerializeField] private float driftMinActivationSpeedKmh = 35f;
    [SerializeField] private float driftMinMaintainSpeedKmh = 22f;
    [SerializeField] private float driftMinimumSteerToStart = 0.35f;
    [SerializeField] private float driftMinimumSteerToMaintain = 0.12f;
    [SerializeField] private float driftReleaseGraceTime = 0.05f;

    [Header("Drift Arcade - Feeling Sabão")]
    [SerializeField] private float driftTurnMultiplier = 1.65f;
    [SerializeField] private float driftSteerAssist = 0.5f;
    [SerializeField] private float driftEnterSpeed = 7.5f;
    [SerializeField] private float driftExitSpeed = 12f;

    [SerializeField] private float driftSideSpeedKmh = 32f;
    [SerializeField] private float driftSideBuildSpeed = 5.5f;
    [SerializeField] private float driftSideReleaseSpeed = 10f;

    [SerializeField] private float driftSpeedHoldAcceleration = 36f;
    [SerializeField] private float driftEntrySpeedRetention = 0.98f;

    [Header("Burnout / Queimar Pneu")]
    [SerializeField] private float burnoutMaxSpeedKmh = 18f;
    [SerializeField] private float burnoutHoldDeceleration = 90f;
    [SerializeField] private float burnoutCreepSpeedKmh = 4f;
    [SerializeField] private float burnoutShakeForce = 0.35f;

    [Header("Aderência")]
    [SerializeField] private float normalLateralGrip = 18f;
    [SerializeField] private float driftLateralGrip = 2.2f;

    [Header("Estabilidade")]
    [SerializeField] private float groundCheckDistance = 1.25f;
    [SerializeField] private float extraGravity = 35f;
    [SerializeField] private float speedDownforce = 0.35f;
    [SerializeField] private float groundDamping = 0.1f;
    [SerializeField] private float airDamping = 0.02f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

    [Header("Boost")]
    [SerializeField] private float boostSpeedMultiplier = 1f;
    [SerializeField] private float boostAccelerationMultiplier = 1f;
    [SerializeField] private float boostEndTime;

    [Header("Estado")]
    [SerializeField] private bool canControl = true;
    [SerializeField] private bool isGrounded;
    [SerializeField] private bool isDrifting;
    [SerializeField] private bool isBurningOut;
    [SerializeField] private int driftDirection;
    [SerializeField, Range(0f, 1f)] private float driftBlend;

    private float throttleInput;
    private float brakeInput;
    private float steerInput;
    private float moveInput;
    private bool handbrakeInput;
    private bool handbrakePressedThisFrame;

    private float driftReleaseTimer;
    private float driftEntryForwardSpeed;
    private bool wantsDrift;

    private Vector3 groundNormal = Vector3.up;

    public float MoveInput => moveInput;
    public float TurnInput => steerInput;
    public float ThrottleInput => throttleInput;
    public float BrakeInput => brakeInput;
    public bool HandbrakeInput => handbrakeInput;

    public bool IsDrifting => isDrifting;
    public bool IsBurningOut => isBurningOut;
    public float DriftBlend => driftBlend;
    public int DriftDirection => driftDirection;
    public bool IsGrounded => isGrounded;

    public Rigidbody Rigidbody => rb;
    public bool CanControl => canControl;

    public float ForwardSpeed => Vector3.Dot(rb.linearVelocity, transform.forward);
    public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
    public float Speed01 => Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / GetCurrentMaxForwardSpeedMps());
    public bool IsBoosting => Time.time < boostEndTime;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.centerOfMass += centerOfMassOffset;

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void Update()
    {
        UpdateBoostState();

        if (!canControl)
        {
            ClearInput();
            ForceEndDrift();
            isBurningOut = false;
            return;
        }

        ReadKeyboardInput();
        UpdateBurnoutState();
        UpdateDriftState();
        UpdateDriftBlend();
    }

    private void FixedUpdate()
    {
        CheckGround();

        ApplyDrive();
        ApplySteering();
        ApplyLateralGripAndSlide();
        ApplyExtraGravityAndDownforce();
        ApplyDamping();
        ClampForwardSpeed();
    }

    public void SetControlEnabled(bool enabled)
    {
        canControl = enabled;

        if (!enabled)
        {
            ClearInput();
            ForceEndDrift();
            isBurningOut = false;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    public void ApplyBoost(float duration, float speedMultiplier, float accelerationMultiplier, float instantPush)
    {
        boostEndTime = Mathf.Max(boostEndTime, Time.time + duration);
        boostSpeedMultiplier = Mathf.Max(boostSpeedMultiplier, speedMultiplier);
        boostAccelerationMultiplier = Mathf.Max(boostAccelerationMultiplier, accelerationMultiplier);

        rb.AddForce(transform.forward * instantPush, ForceMode.VelocityChange);
    }

    private void ReadKeyboardInput()
    {
        ClearInput();

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            throttleInput = 1f;

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            brakeInput = 1f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            steerInput -= 1f;

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            steerInput += 1f;

        handbrakeInput = keyboard.spaceKey.isPressed;
        handbrakePressedThisFrame = keyboard.spaceKey.wasPressedThisFrame;

        moveInput = throttleInput - brakeInput;
    }

    private void ClearInput()
    {
        throttleInput = 0f;
        brakeInput = 0f;
        steerInput = 0f;
        moveInput = 0f;
        handbrakeInput = false;
        handbrakePressedThisFrame = false;
    }

    private void UpdateBurnoutState()
    {
        bool wantsThrottle = throttleInput > 0f;
        bool holdingBrakeOrHandbrake = brakeInput > 0f || handbrakeInput;
        bool slowEnough = SpeedKmh <= burnoutMaxSpeedKmh;
        bool notDrifting = !wantsDrift && driftBlend <= 0.05f;

        isBurningOut = wantsThrottle && holdingBrakeOrHandbrake && slowEnough && isGrounded && notDrifting;
    }

    private void UpdateDriftState()
    {
        if (isBurningOut)
        {
            BeginDriftExit();
            return;
        }

        if (!wantsDrift)
        {
            TryStartDrift();
            return;
        }

        UpdateDriftMaintain();
    }

    private void TryStartDrift()
    {
        bool hasEnoughSteer = Mathf.Abs(steerInput) >= driftMinimumSteerToStart;
        bool hasEnoughSpeed = SpeedKmh >= driftMinActivationSpeedKmh;
        bool movingForward = ForwardSpeed > 1f;

        if (!handbrakePressedThisFrame)
            return;

        if (!hasEnoughSteer || !hasEnoughSpeed || !movingForward || !isGrounded)
            return;

        wantsDrift = true;
        isDrifting = true;
        driftDirection = steerInput >= 0f ? 1 : -1;
        driftReleaseTimer = 0f;

        driftEntryForwardSpeed = Mathf.Max(ForwardSpeed, 0f);
    }

    private void UpdateDriftMaintain()
    {
        bool tooSlow = SpeedKmh < driftMinMaintainSpeedKmh;
        bool airborne = !isGrounded;

        float steerInDriftDirection = steerInput * driftDirection;

        bool steeringSameDirection = steerInDriftDirection >= driftMinimumSteerToMaintain;
        bool steeringOppositeDirection = steerInDriftDirection < -0.15f;
        bool steeringReleased = Mathf.Abs(steerInput) < driftMinimumSteerToMaintain;

        if (tooSlow || airborne || steeringOppositeDirection)
        {
            BeginDriftExit();
            return;
        }

        if (steeringReleased || !steeringSameDirection)
        {
            driftReleaseTimer += Time.deltaTime;

            if (driftReleaseTimer >= driftReleaseGraceTime)
                BeginDriftExit();

            return;
        }

        driftReleaseTimer = 0f;
    }

    private void BeginDriftExit()
    {
        wantsDrift = false;
        isDrifting = false;
        driftDirection = 0;
        driftReleaseTimer = 0f;
    }

    private void ForceEndDrift()
    {
        wantsDrift = false;
        isDrifting = false;
        driftDirection = 0;
        driftBlend = 0f;
        driftReleaseTimer = 0f;
        driftEntryForwardSpeed = 0f;
    }

    private void UpdateDriftBlend()
    {
        float targetBlend = wantsDrift ? 1f : 0f;
        float blendSpeed = wantsDrift ? driftEnterSpeed : driftExitSpeed;

        driftBlend = Mathf.MoveTowards(
            driftBlend,
            targetBlend,
            blendSpeed * Time.deltaTime
        );
    }

    private void CheckGround()
    {
        Vector3 origin = transform.position + Vector3.up * 0.35f;

        float sphereRadius = 0.35f;
        float minGroundNormalY = 0.55f;

        if (Physics.SphereCast(origin, sphereRadius, Vector3.down, out RaycastHit hit, groundCheckDistance))
        {
            bool validGround = hit.normal.y >= minGroundNormalY;

            isGrounded = validGround;
            groundNormal = validGround ? hit.normal : Vector3.up;
        }
        else
        {
            isGrounded = false;
            groundNormal = Vector3.up;
        }
    }

    private void ApplyDrive()
    {
        if (!canControl || !isGrounded)
            return;

        if (isBurningOut)
        {
            ApplyBurnoutHold();
            return;
        }

        float forwardSpeed = ForwardSpeed;

        if (throttleInput > 0f)
        {
            if (forwardSpeed < -0.5f)
            {
                rb.AddForce(transform.forward * brakeDeceleration, ForceMode.Acceleration);
                return;
            }

            float speedFactor = Mathf.Clamp01(forwardSpeed / GetCurrentMaxForwardSpeedMps());
            float accelerationCurve = 1f - Mathf.Pow(speedFactor, 1.35f);
            float finalAcceleration = acceleration * boostAccelerationMultiplier * accelerationCurve;

            rb.AddForce(transform.forward * finalAcceleration, ForceMode.Acceleration);

            if (driftBlend > 0.01f)
                ApplyDriftSpeedHold();
        }

        if (brakeInput > 0f)
        {
            if (forwardSpeed > 1.5f)
            {
                rb.AddForce(-transform.forward * brakeDeceleration, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(-transform.forward * reverseAcceleration, ForceMode.Acceleration);
            }
        }

        if (throttleInput <= 0f && brakeInput <= 0f && driftBlend <= 0.01f)
        {
            Vector3 flatVelocity = rb.linearVelocity;
            flatVelocity.y = 0f;

            if (flatVelocity.sqrMagnitude > 0.1f)
            {
                Vector3 resistance = -flatVelocity.normalized * naturalDeceleration;
                rb.AddForce(resistance, ForceMode.Acceleration);
            }
        }
    }

    private void ApplyBurnoutHold()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        float creepSpeed = burnoutCreepSpeedKmh * KmhToMps;

        localVelocity.z = Mathf.MoveTowards(
            localVelocity.z,
            0f,
            burnoutHoldDeceleration * Time.fixedDeltaTime
        );

        localVelocity.z = Mathf.Clamp(localVelocity.z, -creepSpeed, creepSpeed);

        localVelocity.x = Mathf.MoveTowards(
            localVelocity.x,
            0f,
            burnoutHoldDeceleration * Time.fixedDeltaTime
        );

        rb.linearVelocity = transform.TransformDirection(localVelocity);

        Vector3 shake =
            transform.right * Random.Range(-burnoutShakeForce, burnoutShakeForce) +
            transform.forward * Random.Range(-burnoutShakeForce, burnoutShakeForce);

        rb.AddForce(shake, ForceMode.Acceleration);
    }

    private void ApplyDriftSpeedHold()
    {
        float minimumDriftSpeed = driftEntryForwardSpeed * driftEntrySpeedRetention;
        float currentForwardSpeed = ForwardSpeed;

        if (currentForwardSpeed >= minimumDriftSpeed)
            return;

        float speedGap = minimumDriftSpeed - currentForwardSpeed;
        float normalizedGap = Mathf.Clamp01(speedGap / 8f);

        float holdForce = driftSpeedHoldAcceleration * normalizedGap * driftBlend;

        rb.AddForce(transform.forward * holdForce, ForceMode.Acceleration);
    }

    private void ApplySteering()
    {
        if (!canControl || !isGrounded)
            return;

        if (isBurningOut)
            return;

        float absSpeed = Mathf.Abs(ForwardSpeed);
        float absSpeedKmh = absSpeed * 3.6f;

        if (absSpeedKmh < minSteerSpeedKmh)
            return;

        float steerRamp = Mathf.InverseLerp(minSteerSpeedKmh, fullSteerSpeedKmh, absSpeedKmh);

        float speedFactor = Mathf.Clamp01(absSpeed / GetCurrentMaxForwardSpeedMps());

        float baseTurnRate = Mathf.Lerp(lowSpeedTurnRate, highSpeedTurnRate, speedFactor);
        float highSpeedPenalty = Mathf.Lerp(1f, highSpeedSteerReduction, speedFactor);

        float finalTurnRate = baseTurnRate * highSpeedPenalty * steerRamp;

        float driftTurnBonus = Mathf.Lerp(1f, driftTurnMultiplier, driftBlend);
        finalTurnRate *= driftTurnBonus;

        float finalSteerInput = steerInput;

        if (driftBlend > 0.01f && driftDirection != 0)
        {
            float assist = driftDirection * driftSteerAssist * driftBlend;
            finalSteerInput = Mathf.Clamp(steerInput + assist, -1f, 1f);
        }

        float directionSign = ForwardSpeed >= -0.5f ? 1f : -1f;
        float turnAmount = finalSteerInput * finalTurnRate * directionSign * Time.fixedDeltaTime;

        Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);
    }

    private void ApplyLateralGripAndSlide()
    {
        if (!isGrounded)
            return;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        if (isBurningOut)
        {
            localVelocity.x = Mathf.Lerp(localVelocity.x, 0f, normalLateralGrip * Time.fixedDeltaTime);
            rb.linearVelocity = transform.TransformDirection(localVelocity);
            return;
        }

        if (driftBlend <= 0.01f)
        {
            localVelocity.x = Mathf.Lerp(
                localVelocity.x,
                0f,
                normalLateralGrip * Time.fixedDeltaTime
            );

            rb.linearVelocity = transform.TransformDirection(localVelocity);
            return;
        }

        float currentForwardSpeed = Mathf.Max(Mathf.Abs(localVelocity.z), 0.1f);
        float speedFactor = Mathf.Clamp01(currentForwardSpeed / GetCurrentMaxForwardSpeedMps());

        float targetSideSpeed = -driftDirection * driftSideSpeedKmh * KmhToMps * speedFactor;

        float sideBuildRate = Mathf.Max(driftSideBuildSpeed, driftLateralGrip);

        localVelocity.x = Mathf.Lerp(
            localVelocity.x,
            targetSideSpeed,
            sideBuildRate * driftBlend * Time.fixedDeltaTime
        );

        if (!wantsDrift)
        {
            localVelocity.x = Mathf.Lerp(
                localVelocity.x,
                0f,
                driftSideReleaseSpeed * Time.fixedDeltaTime
            );
        }

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    private void ApplyExtraGravityAndDownforce()
    {
        rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);

        if (!isGrounded)
            return;

        float downforce = Speed01 * speedDownforce * extraGravity;

        // Importante:
        // Não usamos -groundNormal aqui, porque em zebras inclinadas isso cria força lateral
        // e dá a sensação de que a direção está sendo puxada.
        rb.AddForce(Vector3.down * downforce, ForceMode.Acceleration);
    }

    private void ApplyDamping()
    {
        rb.linearDamping = isGrounded ? groundDamping : airDamping;
    }

    private void ClampForwardSpeed()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        float maxForward = GetCurrentMaxForwardSpeedMps();
        float maxReverse = maxReverseSpeedKmh * KmhToMps;

        localVelocity.z = Mathf.Clamp(localVelocity.z, -maxReverse, maxForward);

        rb.linearVelocity = transform.TransformDirection(localVelocity);
    }

    private void UpdateBoostState()
    {
        if (IsBoosting)
            return;

        boostSpeedMultiplier = 1f;
        boostAccelerationMultiplier = 1f;
    }

    private float GetCurrentMaxForwardSpeedMps()
    {
        return maxForwardSpeedKmh * KmhToMps * boostSpeedMultiplier;
    }
}
