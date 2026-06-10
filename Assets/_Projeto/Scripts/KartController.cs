using UnityEngine;
using UnityEngine.InputSystem;
using PartyRacers.Networking;

[RequireComponent(typeof(Rigidbody))]
public class KartController : MonoBehaviour
{
    private const float KmhToMps = 1f / 3.6f;

    [Header("Referências")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private KartSuspension suspension;
    [SerializeField] private KartArcadeCollisionResponse arcadeCollisionResponse;

    [Header("Velocidade")]
    [SerializeField] private float maxForwardSpeedKmh = 200f;
    [SerializeField] private float maxReverseSpeedKmh = 35f;

    [Header("Aceleração e Freio")]
    [SerializeField] private float acceleration = 28f;
    // Velocidade em km/h até onde a aceleração cresce progressivamente (evita start instantâneo)
    [SerializeField] private float accelerationLaunchKmh = 45f;
    [SerializeField] private float reverseAcceleration = 22f;
    [SerializeField] private float brakeDeceleration = 65f;
    [SerializeField] private float handbrakeDeceleration = 95f;
    [SerializeField, Range(0f, 1f)] private float handbrakeThrottleRelief = 0.6f;
    [SerializeField, Range(0f, 1f)] private float handbrakeDriftRelief = 0.45f;
    [SerializeField] private float naturalDeceleration = 4f;

    [Header("Direção")]
    [SerializeField] private float minSteerSpeedKmh = 2f;
    [SerializeField] private float fullSteerSpeedKmh = 10f;
    [SerializeField] private float lowSpeedTurnRate = 160f;
    [SerializeField] private float highSpeedTurnRate = 145f;
    [SerializeField] private float highSpeedSteerReduction = 0.65f;
    // Tempo de graça após perder o chão antes de cortar steering/grip (evita perda de controle em zebras)
    [SerializeField] private float groundGraceTime = 0.12f;

    [Header("Curva Arcade - Pneus")]
    [SerializeField] private float hardTurnSpeedLossMinKmh = 45f;
    [SerializeField, Range(0f, 1f)] private float hardTurnSteerThreshold = 0.82f;
    [SerializeField] private float hardTurnSpeedLoss = 8f;
    [SerializeField, Range(0.1f, 1f)] private float hardTurnMaxLossAtSpeed01 = 0.8f;
    [SerializeField] private float hardTurnHoldTime = 0.22f;
    [SerializeField] private float hardTurnStressFadeSpeed = 8f;
    [SerializeField] private float hardTurnMinSlipAngleDeg = 5.5f;
    [SerializeField] private float hardTurnFullSlipAngleDeg = 14f;
    [SerializeField] private float hardTurnMinLateralSpeed = 1.8f;
    [SerializeField] private float hardTurnFullLateralSpeed = 6.5f;
    [SerializeField] private float launchSlipAccelerationThreshold = 2.5f;
    [SerializeField] private float brakeSlipAccelerationThreshold = 14f;
    [SerializeField] private float abruptSlipFadeSpeed = 7.5f;
    [SerializeField] private float abruptSlipMaxSpeedKmh = 95f;

    [Header("Drift Arcade - Ativação")]
    [SerializeField] private float driftMinActivationSpeedKmh = 35f;
    [SerializeField] private float driftMinMaintainSpeedKmh = 22f;
    [SerializeField] private float driftMinimumSteerToStart = 0.35f;
    [SerializeField] private float driftMinimumSteerToMaintain = 0.12f;
    [SerializeField] private float driftReleaseGraceTime = 0.35f;
    [SerializeField] private float driftSwitchSteerThreshold = 0.58f;
    [SerializeField] private float driftSwitchHoldTime = 0.12f;
    [SerializeField] private float driftSwitchCooldown = 0.1f;
    [SerializeField, Range(0.75f, 1f)] private float driftSwitchSpeedRetention = 0.96f;
    [SerializeField] private float driftCounterExitThreshold = 0.34f;
    [SerializeField] private float driftCounterExitHoldTime = 0.14f;

    [Header("Drift Arcade - Feeling Sabão")]
    [SerializeField] private float driftTurnMultiplier = 1.65f;
    [SerializeField] private float driftSteerAssist = 0.62f;
    [SerializeField] private float driftYawAssist = 24f;
    [SerializeField, Range(0f, 1f)] private float driftDirectSteerScale = 0.34f;
    [SerializeField, Range(0f, 1f)] private float driftCounterSteerAssistScale = 0.35f;
    [SerializeField] private float driftSteerDeadzone = 0.12f;
    [SerializeField] private float driftSteerResponseCurve = 1.75f;
    [SerializeField] private float driftSteerInputSmooth = 10f;
    [SerializeField, Range(0f, 1f)] private float driftThrottleYawDamp = 0.35f;
    [SerializeField, Range(0f, 1f)] private float driftCounterYawDamp = 0.55f;
    [SerializeField] private float driftBrakeYawBoost = 0.45f;
    [SerializeField] private float driftHandbrakeYawBoost = 0.25f;
    [SerializeField] private float driftEnterSpeed = 9.5f;
    [SerializeField] private float driftExitSpeed = 8f;

    [SerializeField] private float driftSideSpeedKmh = 72f;
    [SerializeField] private float driftSideBuildSpeed = 8.5f;
    [SerializeField] private float driftSideSwitchSpeed = 18f;
    [SerializeField] private float driftSideReleaseSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float driftSideBaseline = 0.42f;
    [SerializeField, Range(0.5f, 3f)] private float driftSideSteerResponse = 1.15f;
    [SerializeField, Range(0f, 1f)] private float driftSteerFineSlideInfluence = 0.45f;
    [SerializeField] private float driftBrakeSlideBoost = 0.36f;
    [SerializeField] private float driftHandbrakeSlideBoost = 0.28f;
    [SerializeField, Range(0f, 1f)] private float driftThrottleGrip = 0.32f;
    [SerializeField, Range(0f, 1f)] private float driftCounterSteerGrip = 0.42f;
    [SerializeField, Range(0f, 1f)] private float driftBrakeSpeedDamping = 0.35f;
    [SerializeField, Range(0f, 1f)] private float driftHandbrakeSpeedDamping = 0.45f;

    [SerializeField] private float driftSpeedHoldAcceleration = 48f;
    [SerializeField] private float driftEntrySpeedRetention = 0.97f;

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
    [SerializeField] private float extraGravity = 22f;
    [SerializeField] private float speedDownforce = 0.30f;
    [SerializeField] private float groundDamping = 0.08f;
    [SerializeField] private float airDamping = 0.01f;
    [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.55f, 0f);
    [SerializeField, Range(0f, 1f)] private float landingAbsorption = 0.45f;
    [SerializeField] private float maxGroundedUpwardVelocity = 5f;

    [Header("Ar / Queda")]
    [Tooltip("Torque que endireita o kart no ar quando muito inclinado (acima do deadzone).")]
    [SerializeField] private float airStabilizationTorque = 5f;
    [Tooltip("Inclinação mínima (graus) antes do auto-nível agir. Maior = mais liberdade de voo.")]
    [SerializeField] private float airStabilizationDeadzone = 55f;
    [Tooltip("Torque de pitch (W = mergulho/front-flip, S = empina/back-flip). Estilo GTA V.")]
    [SerializeField] private float airPitchControl = 22f;
    [Tooltip("Torque de roll (A/D inclina lateralmente o carro no ar).")]
    [SerializeField] private float airRollControl = 14f;
    [Tooltip("Torque de yaw (A/D = rotação horizontal no ar para mudar direção).")]
    [SerializeField] private float airYawControl = 12f;
    [SerializeField] private float groundAngularDamping = 4f;
    [Tooltip("Damping da rotação no ar. Menor = giros duram mais, mais arcade.")]
    [SerializeField] private float airAngularDamping = 0.4f;

    [Header("Morro")]
    [Tooltip("Fração da componente gravitacional compensada em subidas. >1 ajuda a manter velocidade.")]
    [SerializeField, Range(0f, 2.5f)] private float slopeCompensation = 1.3f;
    [Tooltip("Empurrão extra para frente quando subindo (na direção do movimento na rampa).")]
    [SerializeField, Range(0f, 30f)] private float uphillAssistForce = 8f;

    [Header("Input")]
    [SerializeField, Range(0.5f, 2f)] private float steerInputCurve = 0.85f;

    [Header("Colisão Arcade (rampas e paredes)")]
    [Tooltip("Acima desta componente Y da normal a superfície é tratada como CHÃO (a suspensão cuida) e a colisão não é redirecionada.")]
    [SerializeField, Range(0.5f, 1f)] private float collisionGroundNormalY = 0.82f;
    [Tooltip("Entre este valor e collisionGroundNormalY a superfície é uma RAMPA: a velocidade é redirecionada para subir, sem o efeito de 'parede'.")]
    [SerializeField, Range(0f, 0.8f)] private float rampMinNormalY = 0.25f;
    [Tooltip("Fração de velocidade preservada ao subir rampas (1 = não perde nada).")]
    [SerializeField, Range(0.8f, 1f)] private float rampSpeedRetention = 0.985f;
    [Tooltip("Fração da velocidade tangencial preservada ao raspar paredes (evita parar do nada).")]
    [SerializeField, Range(0.3f, 1f)] private float wallSlideRetention = 0.82f;
    [Tooltip("Velocidade mínima (m/s) para aplicar o redirecionamento de colisão.")]
    [SerializeField] private float collisionGlideMinSpeed = 2f;
    [Tooltip("Velocidade com que o sinal de impacto (usado pela câmera) decai.")]
    [SerializeField] private float impactDecaySpeed = 2.6f;
    private float recentImpact01;
    public float RecentImpact01 => recentImpact01;

    [Header("Recuperação Pós-Batida Kart-a-Kart")]
    [Tooltip("Tempo (s) em que a aderência lateral fica reduzida após uma batida, deixando o " +
             "empurrão 'deslizar' antes de o carro recolar — dá tempo de corrigir a trajetória.")]
    [SerializeField] private float collisionRecoveryDuration = 0.45f;
    [Tooltip("Aderência lateral mínima (fração) no instante da batida. Recupera suavemente até 1. " +
             "Menor = empurrão escorrega mais (mais arcade); maior = carro 'cola' de volta mais rápido.")]
    [SerializeField, Range(0.05f, 1f)] private float collisionGripFloor = 0.35f;
    [Tooltip("Amortece o giro (yaw) residual durante a recuperação, evitando rodadas descontroladas.")]
    [SerializeField, Range(0f, 20f)] private float collisionYawDamping = 6f;
    private float collisionRecoveryTimer;
    private float collisionRecoveryStrength;

    /// <summary>Fração 0..1 do tempo de recuperação de batida ainda ativo (para HUD/feedback).</summary>
    public float CollisionRecovery01 =>
        collisionRecoveryDuration > 0.001f ? Mathf.Clamp01(collisionRecoveryTimer / collisionRecoveryDuration) : 0f;

    [Header("Boost")]
    [SerializeField] private float boostSpeedMultiplier = 1f;
    [SerializeField] private float boostAccelerationMultiplier = 1f;
    [SerializeField] private float boostEndTime;
    [SerializeField] private float boostDurationWindow = 1f;

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

    // Fonte de input opcional (rede/bot). Nula => comportamento local original (teclado/gamepad).
    private IKartInputSource externalInput;

    // Usado para garantir que karts NÃO-locais (bots/remotos) jamais leiam teclado/gamepad,
    // mesmo que a fonte externa não tenha sido atribuída ainda (evita vazamento de controle).
    private KartLocalRig localRig;

    private float driftReleaseTimer;
    private float driftEntryForwardSpeed;
    private float driftSwitchTimer;
    private float driftSwitchHoldTimer;
    private float driftCounterExitTimer;
    private float driftControlInput;
    private bool wantsDrift;

    private Vector3 groundNormal = Vector3.up;
    private float lastGroundedTime;
    private bool prevGrounded;
    private Vector3 previousLocalVelocity;
    private Vector3 localAcceleration;
    private float hardTurnStress01;
    private float hardTurnStressCharge;
    private float launchSlip01;
    private float brakeSlip01;
    private float tireStress01;

    // True enquanto grounded OU dentro do grace period pós-ground (para steering e grip)
    private bool IsEffectivelyGrounded => isGrounded || (Time.time - lastGroundedTime < groundGraceTime);

    public float MoveInput => moveInput;
    public float TurnInput => steerInput;
    public float ThrottleInput => throttleInput;
    public float BrakeInput => brakeInput;
    public bool HandbrakeInput => handbrakeInput;

    public bool IsDrifting => isDrifting;
    public bool IsBurningOut => isBurningOut;
    public float DriftBlend => driftBlend;
    public int DriftDirection => driftDirection;
    public float DriftSignedAmount => driftDirection * driftBlend;
    public float DriftControlInput => driftBlend > 0.01f ? driftControlInput : steerInput;
    public float TireStress01 => tireStress01;
    public float HardTurnStress01 => hardTurnStress01;
    public float LaunchSlip01 => launchSlip01;
    public float BrakeSlip01 => brakeSlip01;
    public bool IsGrounded => isGrounded;

    public Rigidbody Rigidbody => rb;
    public bool CanControl => canControl;

    public float ForwardSpeed => Vector3.Dot(rb.linearVelocity, transform.forward);
    public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
    public float Speed01 => Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / GetCurrentMaxForwardSpeedMps());
    public bool IsBoosting => Time.time < boostEndTime;
    public float BoostRemaining01 => IsBoosting
        ? Mathf.Clamp01((boostEndTime - Time.time) / Mathf.Max(0.01f, boostDurationWindow))
        : 0f;

    public float SlipAngleDeg
    {
        get
        {
            Vector3 flatVelocity = rb.linearVelocity;
            flatVelocity.y = 0f;

            if (flatVelocity.sqrMagnitude < 0.25f)
                return 0f;

            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;

            return Vector3.SignedAngle(flatForward, flatVelocity, Vector3.up);
        }
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (suspension == null)
            suspension = GetComponent<KartSuspension>();

        if (arcadeCollisionResponse == null)
            arcadeCollisionResponse = GetComponent<KartArcadeCollisionResponse>();

        if (arcadeCollisionResponse == null)
            arcadeCollisionResponse = gameObject.AddComponent<KartArcadeCollisionResponse>();

        localRig = GetComponent<KartLocalRig>();

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.centerOfMass += centerOfMassOffset;
        rb.angularDamping = groundAngularDamping;
        previousLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
    }

    private void Update()
    {
        UpdateBoostState();

        recentImpact01 = Mathf.MoveTowards(recentImpact01, 0f, impactDecaySpeed * Time.deltaTime);

        if (!canControl)
        {
            ClearInput();
            ForceEndDrift();
            isBurningOut = false;
            return;
        }

        ReadInput();
        UpdateBurnoutState();
        UpdateDriftState();
        UpdateDriftBlend();
    }

    private void FixedUpdate()
    {
        CheckGround();

        AbsorbLandingImpact();
        ClampGroundedUpwardVelocity();
        UpdateDriftControlInput();
        UpdateLocalAcceleration();
        UpdateTireStressSignals();

        prevGrounded = isGrounded;

        TickCollisionRecovery();

        ApplyDrive();
        ApplySteering();
        ApplyHardTurnSpeedLoss();
        ApplyLateralGripAndSlide();
        ApplyExtraGravityAndDownforce();
        ApplyAirStabilization();
        ApplyDamping();
        ClampForwardSpeed();
        CapturePostPhysicsVelocity();
    }

    private void AbsorbLandingImpact()
    {
        if (!isGrounded || prevGrounded)
            return;

        float yVel = rb.linearVelocity.y;
        if (yVel >= -0.5f)
            return;

        Vector3 vel = rb.linearVelocity;
        vel.y = yVel * (1f - landingAbsorption);
        rb.linearVelocity = vel;
    }

    private void ClampGroundedUpwardVelocity()
    {
        if (!isGrounded) return;

        // CRÍTICO: medimos a velocidade na direção da NORMAL do terreno, NÃO no eixo Y do mundo.
        // Em rampas, o eixo Y do mundo é uma componente legítima do movimento ao longo da rampa
        // (carro a 55 m/s subindo rampa de 15° tem Y = 14.4 m/s naturalmente).
        // Usar groundNormal isola o "quique de mola" real (perpendicular à superfície) do
        // movimento de subida (paralelo à superfície). Em movimento natural na rampa, este
        // valor é ~0 mesmo em alta velocidade.
        float velAlongNormal = Vector3.Dot(rb.linearVelocity, groundNormal);

        if (velAlongNormal <= maxGroundedUpwardVelocity) return;

        // Remove APENAS o excesso na direção da normal — preserva intacto o movimento ao longo da rampa
        float excess = velAlongNormal - maxGroundedUpwardVelocity;
        rb.linearVelocity -= groundNormal * excess;
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
            previousLocalVelocity = Vector3.zero;
            localAcceleration = Vector3.zero;
            ClearTireStressSignals();
        }
    }

    public void ApplyBoost(float duration, float speedMultiplier, float accelerationMultiplier, float instantPush)
    {
        float targetEndTime = Time.time + duration;
        if (targetEndTime >= boostEndTime)
        {
            boostDurationWindow = Mathf.Max(0.01f, duration);
        }

        boostEndTime = Mathf.Max(boostEndTime, targetEndTime);
        boostSpeedMultiplier = Mathf.Max(boostSpeedMultiplier, speedMultiplier);
        boostAccelerationMultiplier = Mathf.Max(boostAccelerationMultiplier, accelerationMultiplier);

        rb.AddForce(transform.forward * instantPush, ForceMode.VelocityChange);
    }

    // Chamado pela KartArcadeCollisionResponse logo após uma batida kart-a-kart. Abre uma janela
    // curta em que a aderência lateral é reduzida (o empurrão escorrega em vez de ser anulado no
    // mesmo frame) e o yaw residual é amortecido — o carro pode ser empurrado/raspado e ainda
    // assim corrigido pelo jogador/bot, sem rodar descontroladamente.
    public void NotifyKartCollisionRecovery(float impact01)
    {
        collisionRecoveryStrength = Mathf.Max(collisionRecoveryStrength, Mathf.Clamp01(impact01));
        collisionRecoveryTimer = collisionRecoveryDuration;
    }

    // Fração da aderência lateral vigente: 1 normalmente; reduzida logo após uma batida e
    // recuperando suavemente até 1 ao longo de collisionRecoveryDuration.
    private float CurrentLateralGripScale()
    {
        if (collisionRecoveryTimer <= 0f)
            return 1f;

        float progress = 1f - Mathf.Clamp01(collisionRecoveryTimer / Mathf.Max(0.01f, collisionRecoveryDuration));
        float reducedGrip = Mathf.Lerp(collisionGripFloor, 1f, progress);
        return Mathf.Lerp(1f, reducedGrip, collisionRecoveryStrength);
    }

    private void TickCollisionRecovery()
    {
        if (collisionRecoveryTimer <= 0f)
            return;

        collisionRecoveryTimer -= Time.fixedDeltaTime;

        // Amortece apenas o yaw (giro horizontal) para que a batida não vire rodada; preserva
        // a velocidade de avanço e o controle de direção.
        if (collisionYawDamping > 0f && isGrounded)
        {
            Vector3 angular = rb.angularVelocity;
            angular.y *= Mathf.Max(0f, 1f - collisionYawDamping * Time.fixedDeltaTime);
            rb.angularVelocity = angular;
        }

        if (collisionRecoveryTimer <= 0f)
            collisionRecoveryStrength = 0f;
    }

    // Permite a uma camada de rede/bot fornecer input sem alterar a física.
    // Passar null restaura o controle local (teclado/gamepad).
    public void SetInputSource(IKartInputSource source)
    {
        externalInput = source;
    }

    private void ReadInput()
    {
        ClearInput();

        if (externalInput != null)
        {
            ApplyExternalInput(externalInput.Read());
            return;
        }

        // Kart não-local sem fonte externa (bot ainda inicializando / remoto): fica neutro.
        // Nunca lê teclado/gamepad — só o kart do jogador local pode fazer isso.
        if (localRig != null && !localRig.IsLocalPlayer)
            return;

        Keyboard keyboard = Keyboard.current;
        Gamepad gamepad = Gamepad.current;

        // --- Throttle / Brake ---
        if (gamepad != null)
        {
            float rt = gamepad.rightTrigger.ReadValue();
            float lt = gamepad.leftTrigger.ReadValue();

            if (rt > 0.05f) throttleInput = rt;
            if (lt > 0.05f) brakeInput = lt;
        }

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                throttleInput = 1f;

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                brakeInput = 1f;
        }

        // --- Steer: gamepad analogue tem prioridade se significativo ---
        float gamepadSteerRaw = gamepad != null ? gamepad.leftStick.x.ReadValue() : 0f;
        bool gamepadSteering = gamepad != null && Mathf.Abs(gamepadSteerRaw) > 0.05f;

        if (gamepadSteering)
        {
            float abs = Mathf.Abs(gamepadSteerRaw);
            steerInput = Mathf.Sign(gamepadSteerRaw) * Mathf.Pow(abs, steerInputCurve);
        }
        else if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                steerInput -= 1f;

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                steerInput += 1f;
        }

        // --- Handbrake ---
        bool gamepadHB = gamepad != null && gamepad.buttonSouth.isPressed;
        bool keyboardHB = keyboard != null && keyboard.spaceKey.isPressed;

        handbrakeInput = gamepadHB || keyboardHB;
        handbrakePressedThisFrame =
            (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame) ||
            (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);

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

    private void ApplyExternalInput(KartInputState state)
    {
        throttleInput = Mathf.Clamp01(state.Throttle);
        brakeInput = Mathf.Clamp01(state.Brake);
        steerInput = Mathf.Clamp(state.Steer, -1f, 1f);
        handbrakeInput = state.Handbrake;
        handbrakePressedThisFrame = state.HandbrakePressed;
        moveInput = throttleInput - brakeInput;
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
        if (driftSwitchTimer > 0f)
            driftSwitchTimer -= Time.deltaTime;

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
        SetDriftDirection(steerInput >= 0f ? 1 : -1);
        driftReleaseTimer = 0f;

        driftEntryForwardSpeed = Mathf.Max(ForwardSpeed, 0f);
    }

    private void UpdateDriftMaintain()
    {
        bool tooSlow = SpeedKmh < driftMinMaintainSpeedKmh;
        bool airborne = !isGrounded;

        if (tooSlow || airborne)
        {
            BeginDriftExit();
            return;
        }

        float absSteer = Mathf.Abs(steerInput);

        int steerDirection = steerInput >= 0f ? 1 : -1;
        bool isCounterSteering = steerDirection != driftDirection
            && absSteer >= driftCounterExitThreshold;
        bool hasSideSwitchIntent = handbrakeInput || brakeInput > 0.15f;
        bool wantsSideSwitch = isCounterSteering
            && hasSideSwitchIntent
            && absSteer >= driftSwitchSteerThreshold
            && driftSwitchTimer <= 0f;

        if (wantsSideSwitch)
        {
            driftSwitchHoldTimer += Time.deltaTime;

            if (driftSwitchHoldTimer >= driftSwitchHoldTime)
            {
                SetDriftDirection(steerDirection);
                driftSwitchHoldTimer = 0f;
            }

            driftReleaseTimer = 0f;
            return;
        }

        driftSwitchHoldTimer = 0f;

        if (isCounterSteering && !hasSideSwitchIntent)
        {
            driftCounterExitTimer += Time.deltaTime;

            if (driftCounterExitTimer >= driftCounterExitHoldTime)
                BeginDriftExit();

            return;
        }

        driftCounterExitTimer = 0f;

        bool hasDriftInput = handbrakeInput || absSteer >= driftMinimumSteerToMaintain;

        if (!hasDriftInput)
        {
            driftReleaseTimer += Time.deltaTime;

            if (driftReleaseTimer >= driftReleaseGraceTime)
                BeginDriftExit();

            return;
        }

        driftReleaseTimer = 0f;
    }

    private void SetDriftDirection(int direction)
    {
        if (direction == 0 || direction == driftDirection)
            return;

        driftDirection = direction;
        driftSwitchTimer = driftSwitchCooldown;
        driftSwitchHoldTimer = 0f;
        driftCounterExitTimer = 0f;
        driftReleaseTimer = 0f;

        float currentForwardSpeed = Mathf.Max(ForwardSpeed, 0f);
        float retainedEntrySpeed = driftEntryForwardSpeed * driftSwitchSpeedRetention;
        driftEntryForwardSpeed = Mathf.Max(retainedEntrySpeed, currentForwardSpeed, driftMinMaintainSpeedKmh * KmhToMps);
    }

    private void BeginDriftExit()
    {
        wantsDrift = false;
        isDrifting = false;
        driftDirection = 0;
        driftReleaseTimer = 0f;
        driftSwitchTimer = 0f;
        driftSwitchHoldTimer = 0f;
        driftCounterExitTimer = 0f;
    }

    private void ForceEndDrift()
    {
        wantsDrift = false;
        isDrifting = false;
        driftDirection = 0;
        driftBlend = 0f;
        driftReleaseTimer = 0f;
        driftEntryForwardSpeed = 0f;
        driftSwitchTimer = 0f;
        driftSwitchHoldTimer = 0f;
        driftCounterExitTimer = 0f;
        driftControlInput = 0f;
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

    private void UpdateDriftControlInput()
    {
        if (driftBlend <= 0.01f)
        {
            driftControlInput = Mathf.MoveTowards(
                driftControlInput,
                steerInput,
                driftSteerInputSmooth * Time.fixedDeltaTime
            );
            return;
        }

        float targetInput = ShapeDriftSteerInput(steerInput);
        driftControlInput = Mathf.Lerp(
            driftControlInput,
            targetInput,
            Mathf.Clamp01(driftSteerInputSmooth * Time.fixedDeltaTime)
        );
    }

    private float ShapeDriftSteerInput(float input)
    {
        float absInput = Mathf.Abs(input);

        if (absInput <= driftSteerDeadzone)
            return 0f;

        float normalizedInput = Mathf.InverseLerp(driftSteerDeadzone, 1f, absInput);
        float shapedInput = Mathf.Pow(normalizedInput, Mathf.Max(0.1f, driftSteerResponseCurve));

        return Mathf.Sign(input) * shapedInput;
    }

    private void UpdateLocalAcceleration()
    {
        Vector3 currentLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        localAcceleration = (currentLocalVelocity - previousLocalVelocity) / Mathf.Max(Time.fixedDeltaTime, 0.001f);
    }

    private void CapturePostPhysicsVelocity()
    {
        previousLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
    }

    private void UpdateTireStressSignals()
    {
        if (!canControl || !isGrounded)
        {
            ClearTireStressSignals();
            return;
        }

        hardTurnStress01 = CalculateHardTurnStress();
        launchSlip01 = CalculateLaunchSlip();
        brakeSlip01 = CalculateBrakeSlip();

        tireStress01 = Mathf.Max(
            Mathf.Clamp01(driftBlend),
            isBurningOut ? 1f : 0f,
            hardTurnStress01,
            launchSlip01,
            brakeSlip01
        );
    }

    private void ClearTireStressSignals()
    {
        hardTurnStress01 = 0f;
        hardTurnStressCharge = 0f;
        launchSlip01 = 0f;
        brakeSlip01 = 0f;
        tireStress01 = 0f;
    }

    private float CalculateHardTurnStress()
    {
        if (isBurningOut || driftBlend > 0.05f)
        {
            DecayHardTurnCharge();
            return 0f;
        }

        float absSteer = Mathf.Abs(steerInput);
        if (absSteer <= hardTurnSteerThreshold)
        {
            DecayHardTurnCharge();
            return 0f;
        }

        float speedKmh = SpeedKmh;
        if (speedKmh < hardTurnSpeedLossMinKmh)
        {
            DecayHardTurnCharge();
            return 0f;
        }

        hardTurnStressCharge = Mathf.MoveTowards(
            hardTurnStressCharge,
            1f,
            Time.fixedDeltaTime / Mathf.Max(0.01f, hardTurnHoldTime)
        );

        float steerStress = Mathf.InverseLerp(hardTurnSteerThreshold, 1f, absSteer);
        float maxLossSpeedKmh = maxForwardSpeedKmh * hardTurnMaxLossAtSpeed01;
        float speedStress = Mathf.InverseLerp(hardTurnSpeedLossMinKmh, maxLossSpeedKmh, speedKmh);
        float slipStress = Mathf.InverseLerp(hardTurnMinSlipAngleDeg, hardTurnFullSlipAngleDeg, Mathf.Abs(SlipAngleDeg));
        float lateralSpeed = Mathf.Abs(transform.InverseTransformDirection(rb.linearVelocity).x);
        float lateralStress = Mathf.InverseLerp(hardTurnMinLateralSpeed, hardTurnFullLateralSpeed, lateralSpeed);
        float scrubStress = Mathf.Max(slipStress, lateralStress);
        float sustainedStress = Mathf.SmoothStep(0f, 1f, hardTurnStressCharge);

        return Mathf.Clamp01(steerStress * speedStress * scrubStress * sustainedStress);
    }

    private void DecayHardTurnCharge()
    {
        hardTurnStressCharge = Mathf.MoveTowards(
            hardTurnStressCharge,
            0f,
            hardTurnStressFadeSpeed * Time.fixedDeltaTime
        );
    }

    private float CalculateLaunchSlip()
    {
        if (isBurningOut || driftBlend > 0.1f || throttleInput < 0.35f)
            return 0f;

        if (SpeedKmh > abruptSlipMaxSpeedKmh)
            return 0f;

        float accelerationStress = Mathf.InverseLerp(
            launchSlipAccelerationThreshold,
            launchSlipAccelerationThreshold + abruptSlipFadeSpeed,
            localAcceleration.z
        );

        float lowSpeedBias = 1f - Mathf.InverseLerp(12f, 65f, SpeedKmh);
        float throttleStress = Mathf.InverseLerp(0.35f, 1f, throttleInput);

        return Mathf.Clamp01(accelerationStress * lowSpeedBias * throttleStress);
    }

    private float CalculateBrakeSlip()
    {
        if (isBurningOut || driftBlend > 0.1f)
            return 0f;

        float brakeIntent = Mathf.Max(brakeInput, handbrakeInput ? 0.75f : 0f);
        if (brakeIntent <= 0.2f)
            return 0f;

        float speedStress = Mathf.InverseLerp(18f, abruptSlipMaxSpeedKmh, SpeedKmh);
        float decelerationStress = Mathf.InverseLerp(
            brakeSlipAccelerationThreshold,
            brakeSlipAccelerationThreshold + abruptSlipFadeSpeed,
            -localAcceleration.z
        );

        return Mathf.Clamp01(decelerationStress * speedStress * Mathf.InverseLerp(0.2f, 1f, brakeIntent));
    }

    private void CheckGround()
    {
        if (suspension != null)
        {
            isGrounded = suspension.IsAnyWheelGrounded;
            groundNormal = suspension.AverageGroundNormal;
        }
        else
        {
            Vector3 origin = transform.position + Vector3.up * 0.35f;

            if (Physics.SphereCast(origin, 0.35f, Vector3.down, out RaycastHit hit, groundCheckDistance))
            {
                bool validGround = hit.normal.y >= 0.55f;
                isGrounded = validGround;
                groundNormal = validGround ? hit.normal : Vector3.up;
            }
            else
            {
                isGrounded = false;
                groundNormal = Vector3.up;
            }
        }

        if (isGrounded)
            lastGroundedTime = Time.time;
    }

    private void ApplyDrive()
    {
        if (!canControl || !IsEffectivelyGrounded)
            return;

        if (isBurningOut)
        {
            ApplyBurnoutHold();
            return;
        }

        float forwardSpeed = ForwardSpeed;

        // Direção de aceleração projetada na superfície do terreno para seguir rampas corretamente
        Vector3 slopeFwd = Vector3.ProjectOnPlane(transform.forward, groundNormal);
        Vector3 driveDir = slopeFwd.sqrMagnitude > 0.001f ? slopeFwd.normalized : transform.forward;

        if (throttleInput > 0f)
        {
            if (forwardSpeed < -0.5f)
            {
                rb.AddForce(driveDir * brakeDeceleration, ForceMode.Acceleration);
                return;
            }

            float speedFactor = Mathf.Clamp01(forwardSpeed / GetCurrentMaxForwardSpeedMps());

            // ARCADE: largada com mais "soco". Antes a força começava em 15% (resposta lenta);
            // agora começa em 55% e cresce rápido até accelerationLaunchKmh. Ajuste fino aqui.
            float launchRamp = Mathf.SmoothStep(0.55f, 1f, Mathf.Clamp01(SpeedKmh / accelerationLaunchKmh));
            // Curva mais plana: motor mantém quase força máxima até 70% e cai só nos últimos 30%
            float topEndDropoff = speedFactor < 0.7f
                ? 1f
                : 1f - Mathf.Pow((speedFactor - 0.7f) / 0.3f, 1.5f);
            float accelerationCurve = launchRamp * topEndDropoff;

            float finalAcceleration = acceleration * boostAccelerationMultiplier * accelerationCurve;

            rb.AddForce(driveDir * finalAcceleration, ForceMode.Acceleration);

            if (driftBlend > 0.01f)
                ApplyDriftSpeedHold();

            ApplySlopeCompensation(driveDir);
        }

        if (brakeInput > 0f)
        {
            if (forwardSpeed > 1.5f)
            {
                float driftBrakeScale = Mathf.Lerp(1f, driftBrakeSpeedDamping, driftBlend);
                rb.AddForce(-driveDir * brakeDeceleration * driftBrakeScale, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(-driveDir * reverseAcceleration, ForceMode.Acceleration);
            }
        }

        ApplyHandbrakeForce(forwardSpeed);

        if (throttleInput <= 0f && brakeInput <= 0f && !handbrakeInput && driftBlend <= 0.01f)
        {
            // Desaceleração natural na direção do movimento projetado no terreno
            Vector3 coastDir = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
            if (coastDir.sqrMagnitude > 0.1f)
                rb.AddForce(-coastDir.normalized * naturalDeceleration, ForceMode.Acceleration);
        }
    }

    private void ApplyHandbrakeForce(float forwardSpeed)
    {
        if (!handbrakeInput || isBurningOut)
            return;

        float throttleScale = 1f - handbrakeThrottleRelief * throttleInput;
        float driftScale = 1f - handbrakeDriftRelief * driftBlend;
        float intensity = handbrakeDeceleration * throttleScale * driftScale;
        intensity *= Mathf.Lerp(1f, driftHandbrakeSpeedDamping, driftBlend);

        if (Mathf.Abs(forwardSpeed) > 1f)
        {
            float brakeSign = forwardSpeed > 0f ? -1f : 1f;
            rb.AddForce(transform.forward * intensity * brakeSign, ForceMode.Acceleration);
            return;
        }

        if (throttleInput > 0.1f || driftBlend > 0.05f)
            return;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float step = intensity * Time.fixedDeltaTime;

        localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, step);
        localVelocity.z = Mathf.MoveTowards(localVelocity.z, 0f, step);

        rb.linearVelocity = transform.TransformDirection(localVelocity);
        rb.angularVelocity = Vector3.MoveTowards(rb.angularVelocity, Vector3.zero, step);
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

    private void ApplySlopeCompensation(Vector3 driveDir)
    {
        if (!IsEffectivelyGrounded || slopeCompensation <= 0f) return;

        // Calcula a componente da gravidade que se opõe ao driveDir usando o groundNormal real,
        // independente de quanto o chassi inclinou fisicamente.
        float totalGravityMag = Physics.gravity.magnitude + extraGravity;
        float gravAlongDrive = Vector3.Dot(Vector3.down * totalGravityMag, driveDir);

        // Só compensa em subida (gravidade opondo o movimento)
        if (gravAlongDrive >= 0f) return;

        // Compensação gravitacional + assistência adicional proporcional à inclinação
        float slopeIntensity = -gravAlongDrive / Mathf.Max(0.01f, totalGravityMag); // 0..1
        float compensation = (-gravAlongDrive) * slopeCompensation + uphillAssistForce * slopeIntensity;

        rb.AddForce(driveDir * compensation, ForceMode.Acceleration);
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
        if (!canControl || !IsEffectivelyGrounded)
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
        float driftYawPedalScale = 1f
            + brakeInput * driftBrakeYawBoost
            + (handbrakeInput ? driftHandbrakeYawBoost : 0f)
            - throttleInput * driftThrottleYawDamp;
        finalTurnRate += driftYawAssist * driftBlend * Mathf.Max(0.35f, driftYawPedalScale);

        float finalSteerInput = steerInput;

        if (driftBlend > 0.01f && driftDirection != 0)
        {
            float steerIntoDrift = Mathf.Clamp01(driftControlInput * driftDirection);
            float counterSteer = Mathf.Clamp01(-driftControlInput * driftDirection);
            bool straighteningDrift = counterSteer > 0.01f && brakeInput <= 0.15f && !handbrakeInput;
            float assistScale = 1f
                + steerIntoDrift * 0.35f
                + brakeInput * driftBrakeYawBoost
                + (handbrakeInput ? driftHandbrakeYawBoost : 0f)
                - throttleInput * driftThrottleYawDamp
                - counterSteer * driftCounterYawDamp;

            assistScale = Mathf.Clamp(assistScale, 0.2f, 1.8f);

            float assist = driftDirection * driftSteerAssist * driftBlend * assistScale;
            assist *= Mathf.Lerp(1f, driftCounterSteerAssistScale, counterSteer);
            float directSteer = driftControlInput * driftDirectSteerScale;

            if (straighteningDrift)
            {
                float straightenDamp = Mathf.Lerp(1f, 0.2f, counterSteer);
                assist *= straightenDamp;
                directSteer *= Mathf.Lerp(1f, 0.25f, counterSteer);
            }

            finalSteerInput = Mathf.Clamp(directSteer + assist, -1f, 1f);
        }

        float directionSign = ForwardSpeed >= -0.5f ? 1f : -1f;
        float turnAmount = finalSteerInput * finalTurnRate * directionSign * Time.fixedDeltaTime;

        Quaternion turnRotation = Quaternion.Euler(0f, turnAmount, 0f);
        rb.MoveRotation(rb.rotation * turnRotation);
    }

    private void ApplyHardTurnSpeedLoss()
    {
        if (hardTurnStress01 <= 0f)
            return;

        if (!canControl || !IsEffectivelyGrounded || isBurningOut || driftBlend > 0.05f)
            return;

        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, groundNormal);
        if (planarVelocity.sqrMagnitude < 1f)
            return;

        rb.AddForce(-planarVelocity.normalized * hardTurnSpeedLoss * hardTurnStress01, ForceMode.Acceleration);
    }

    private void ApplyLateralGripAndSlide()
    {
        if (!IsEffectivelyGrounded)
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
            // Aderência reduzida logo após batida (recupera suavemente) faz o empurrão deslizar
            // de forma arcade em vez de ser anulado no mesmo frame.
            float gripScale = CurrentLateralGripScale();

            localVelocity.x = Mathf.Lerp(
                localVelocity.x,
                0f,
                normalLateralGrip * gripScale * Time.fixedDeltaTime
            );

            rb.linearVelocity = transform.TransformDirection(localVelocity);
            return;
        }

        if (driftDirection == 0)
        {
            localVelocity.x = Mathf.Lerp(
                localVelocity.x,
                0f,
                driftSideReleaseSpeed * Time.fixedDeltaTime
            );

            rb.linearVelocity = transform.TransformDirection(localVelocity);
            return;
        }

        float currentForwardSpeed = Mathf.Max(Mathf.Abs(localVelocity.z), 0.1f);
        float speedFactor = Mathf.Clamp01(currentForwardSpeed / GetCurrentMaxForwardSpeedMps());

        float steerIntoDrift = Mathf.Clamp01(driftControlInput * driftDirection);
        float counterSteer = Mathf.Clamp01(-driftControlInput * driftDirection);
        float shapedSteerSlide = Mathf.Pow(steerIntoDrift, 1f / Mathf.Max(0.01f, driftSideSteerResponse));
        float pedalSlide =
            brakeInput * driftBrakeSlideBoost +
            (handbrakeInput ? driftHandbrakeSlideBoost : 0f) -
            throttleInput * driftThrottleGrip -
            counterSteer * driftCounterSteerGrip;

        float steerSlideScale = driftSideBaseline
            + shapedSteerSlide * driftSteerFineSlideInfluence
            + pedalSlide;

        steerSlideScale = Mathf.Clamp(steerSlideScale, 0.12f, 1.22f);

        float targetSideSpeed = -driftDirection * driftSideSpeedKmh * KmhToMps * speedFactor * steerSlideScale;

        float sideBuildRate = Mathf.Max(driftSideBuildSpeed, driftLateralGrip);
        bool crossingSides = Mathf.Abs(localVelocity.x) > 0.25f
            && Mathf.Sign(localVelocity.x) != Mathf.Sign(targetSideSpeed);

        if (crossingSides)
            sideBuildRate = Mathf.Max(sideBuildRate, driftSideSwitchSpeed);

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

        // Downforce ao longo da normal do terreno: cola o carro na pista sem criar componente
        // contrária ao movimento em rampas (ao contrário de aplicar Vector3.down direto).
        float downforce = Speed01 * speedDownforce * Physics.gravity.magnitude;
        rb.AddForce(-groundNormal * downforce, ForceMode.Acceleration);
    }

    private void ApplyAirStabilization()
    {
        if (isGrounded) return;

        // Auto-nivelar SUAVE: só age quando muito inclinado (deadzone), preservando voos naturais
        float tiltAngle = Vector3.Angle(transform.up, Vector3.up);
        if (tiltAngle > airStabilizationDeadzone)
        {
            float overTilt = (tiltAngle - airStabilizationDeadzone) / Mathf.Max(1f, 90f - airStabilizationDeadzone);
            Vector3 levelTorque = Vector3.Cross(transform.up, Vector3.up) * airStabilizationTorque * Mathf.Clamp01(overTilt);
            rb.AddTorque(levelTorque, ForceMode.Acceleration);
        }

        // Controle do jogador no ar (estilo GTA V / NFS arcade)
        if (!canControl) return;

        // Pitch: W = mergulha nariz (front-flip), S = empina nariz (back-flip).
        // Sinal negativo no torque: torque positivo em transform.right = nariz pra cima,
        // mas o jogador espera W (acelerar) = inclinar pra frente, então invertemos.
        float pitchInput = throttleInput - brakeInput;
        if (Mathf.Abs(pitchInput) > 0.05f)
            rb.AddTorque(transform.right * -pitchInput * airPitchControl, ForceMode.Acceleration);

        // Roll + Yaw com steerInput. A/D inclina e gira simultaneamente — mais reativo,
        // permite curvas reais no ar (ajustar direção pra próximo trecho da pista).
        if (Mathf.Abs(steerInput) > 0.05f)
        {
            rb.AddTorque(transform.forward * -steerInput * airRollControl, ForceMode.Acceleration);
            rb.AddTorque(transform.up * steerInput * airYawControl, ForceMode.Acceleration);
        }
    }

    private void ApplyDamping()
    {
        rb.linearDamping = isGrounded ? groundDamping : airDamping;
        rb.angularDamping = isGrounded ? groundAngularDamping : airAngularDamping;
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

    // Colisão "arcade": o BoxCollider do corpo, ao tocar a face de uma rampa ou parede, sofre
    // um impulso normal que mata a velocidade ("efeito parede"). Aqui redirecionamos o movimento
    // ao longo da superfície — sobe rampas preservando a velocidade e desliza em paredes — em vez
    // de parar do nada. Rodado em OnCollisionStay/Enter (após o solver), então sobrescreve o stop.
    private void OnCollisionEnter(Collision collision) => HandleCollisionGlide(collision);
    private void OnCollisionStay(Collision collision) => HandleCollisionGlide(collision);

    private void HandleCollisionGlide(Collision collision)
    {
        if (rb == null)
            return;

        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;
        if (speed < collisionGlideMinSpeed)
            return;

        // Normal que mais se opõe ao movimento = a superfície que está freando o kart.
        Vector3 blockingNormal = Vector3.zero;
        float worstInto = 0f;
        int contactCount = collision.contactCount;

        for (int i = 0; i < contactCount; i++)
        {
            Vector3 n = collision.GetContact(i).normal;
            float into = Vector3.Dot(velocity, -n);
            if (into > worstInto)
            {
                worstInto = into;
                blockingNormal = n;
            }
        }

        if (worstInto <= 0f)
            return;

        // Chão quase plano: a suspensão (raycast) cuida disso — não interferir.
        if (blockingNormal.y >= collisionGroundNormalY)
            return;

        KartController otherKart = collision.rigidbody != null ? collision.rigidbody.GetComponent<KartController>() : null;
        if (otherKart != null && otherKart != this)
        {
            if (arcadeCollisionResponse != null &&
                arcadeCollisionResponse.TryHandleCollision(
                    collision,
                    otherKart,
                    transform.TransformDirection(previousLocalVelocity),
                    GetCurrentMaxForwardSpeedMps(),
                    out float kartImpact01))
            {
                recentImpact01 = Mathf.Clamp01(Mathf.Max(recentImpact01, kartImpact01));
                NotifyKartCollisionRecovery(kartImpact01);
            }
            return;
        }

        Vector3 along = Vector3.ProjectOnPlane(velocity, blockingNormal);
        if (along.sqrMagnitude < 0.0001f)
            along = Vector3.ProjectOnPlane(transform.forward, blockingNormal);

        if (blockingNormal.y >= rampMinNormalY)
        {
            // Rampa: converte o movimento para subir a superfície, preservando a velocidade.
            rb.linearVelocity = along.normalized * speed * rampSpeedRetention;
        }
        else
        {
            // Parede: desliza ao longo dela mantendo a maior parte da velocidade tangencial.
            rb.linearVelocity = along * wallSlideRetention;
        }

        // Sinaliza o impacto para a câmera (forte quanto mais perpendicular for a batida).
        float impact = worstInto / Mathf.Max(1f, GetCurrentMaxForwardSpeedMps());
        recentImpact01 = Mathf.Clamp01(Mathf.Max(recentImpact01, impact));
    }

    private float GetCurrentMaxForwardSpeedMps()
    {
        return maxForwardSpeedKmh * KmhToMps * boostSpeedMultiplier;
    }
}
