using UnityEngine;

// Projétil do power-up "Disco Voador de Swap".
//
// Comportamento:
//  - Voa para frente mantendo altura FIXA em relação ao chão (configurável), girando como um OVNI.
//  - Se houver um alvo (carro à frente), faz homing suave em direção a ele.
//  - Parede: quica com o mesmo "boing" do foguete (até maxBounces; depois conta como erro).
//  - Carro válido: sobe suavemente para cima do carro atingido, ativa o círculo mágico PRESO AO
//    CHÃO em volta dos dois carros (atirador e atingido) e, após 'swapDelay', troca as posições
//    dos dois com orientação coerente com a pista (KartSwapUtility).
//  - Escudo ativo no alvo: o poder é bloqueado (pulso do escudo + boing) e o disco some.
//  - Sem acertar ninguém (tempo/alcance/quiques esgotados): ativa o efeito de "desaparecimento"
//    (AoE slash blue) na própria posição e some.
[DisallowMultipleComponent]
public class UFOSwapProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade de voo (m/s). Deve ser maior que a velocidade máxima dos carros.")]
    [SerializeField] private float speed = 52f;
    [SerializeField] private float lifetime = 7f;
    [SerializeField] private float maxDistance = 130f;
    [SerializeField] private float radius = 0.8f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Velocidade de giro do homing em direção ao alvo (graus/s). 0 = voa reto.")]
    [SerializeField] private float homingTurnRate = 140f;

    [Header("Altura sobre o chão")]
    [Tooltip("Altura fixa mantida em relação ao chão durante o voo (m).")]
    [SerializeField] private float hoverHeight = 1.6f;
    [SerializeField] private float heightAdjustSpeed = 14f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Normais com Y acima disso são chão (ignoradas pela colisão; a altura cuida delas).")]
    [SerializeField, Range(0f, 1f)] private float groundNormalMinY = 0.55f;

    [Header("Quique em parede (boing)")]
    [SerializeField] private int maxBounces = 2;
    [SerializeField, Range(0.5f, 1f)] private float bounceSpeedRetention = 0.95f;
    [SerializeField] private GameObject boingVFXPrefab;
    [SerializeField] private float boingScale = 1f;

    [Header("Visual")]
    [Tooltip("Escala aplicada ao transform raiz (o modelo do disco é filho).")]
    [SerializeField] private float modelScale = 0.35f;
    [Tooltip("Velocidade de rotação do disco em voo (graus/s).")]
    [SerializeField] private float spinSpeed = 320f;
    [SerializeField] private float bobAmplitude = 0.15f;
    [SerializeField] private float bobSpeed = 4f;

    [Header("Captura e Swap")]
    [Tooltip("Altura acima do carro atingido para onde o disco sobe.")]
    [SerializeField] private float captureRiseHeight = 2.4f;
    [Tooltip("Duração da subida suave até em cima do carro atingido (s).")]
    [SerializeField] private float captureRiseDuration = 0.55f;
    [Tooltip("Delay entre ativar os círculos mágicos e efetivar a troca de posições (s).")]
    [SerializeField] private float swapDelay = 1.5f;
    [Tooltip("Círculo mágico exibido no chão ao redor dos dois carros.")]
    [SerializeField] private GameObject magicCirclePrefab;
    [Tooltip("Tempo extra que os círculos permanecem após a troca (s).")]
    [SerializeField] private float magicCircleLinger = 0.6f;

    [Header("Erro (não acertou ninguém)")]
    [Tooltip("Efeito ativado dentro do disco antes de desaparecer (ex.: AoE slash blue).")]
    [SerializeField] private GameObject missVFXPrefab;
    [SerializeField] private float missVFXScale = 1f;

    private enum State { Flying, Capturing, WaitingSwap, Done }

    private readonly RaycastHit[] hits = new RaycastHit[16];

    private GameObject owner;
    private GameObject targetKartObject; // alvo do homing (pode ser nulo: voo reto)
    private KartController capturedKart;
    private State state = State.Flying;

    private Vector3 direction;
    private float lifeTimer;
    private float travelled;
    private int bounceCount;
    private float spinAngle;
    private float baseY;

    private float captureTimer;
    private Vector3 captureStartPosition;
    private float swapTimer;

    public void Initialize(
        GameObject projectileOwner,
        Vector3 fireDirection,
        GameObject homingTarget,
        GameObject magicCircleVFX = null,
        GameObject missVFX = null,
        GameObject boingVFX = null)
    {
        owner = projectileOwner;
        targetKartObject = homingTarget;
        direction = Planar(fireDirection).sqrMagnitude > 0.0001f ? Planar(fireDirection).normalized : Vector3.forward;

        if (magicCircleVFX != null)
            magicCirclePrefab = magicCircleVFX;

        if (missVFX != null)
            missVFXPrefab = missVFX;

        if (boingVFX != null)
            boingVFXPrefab = boingVFX;
    }

    private void Awake()
    {
        EnsurePhysics();
        transform.localScale = Vector3.one * modelScale;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Planar(transform.forward).normalized;

        baseY = transform.position.y;
    }

    private void Update()
    {
        switch (state)
        {
            case State.Flying:
                UpdateFlight();
                break;

            case State.Capturing:
                UpdateCapture();
                break;

            case State.WaitingSwap:
                UpdateSwapWait();
                break;
        }
    }

    // ------------------------------------------------------------------ voo
    private void UpdateFlight()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Miss();
            return;
        }

        UpdateHoming();

        float distance = speed * Time.deltaTime;

        if (TryFindHit(distance, out RaycastHit hit))
        {
            // Chão/rampa não é obstáculo: o controle de altura cuida da subida.
            if (hit.normal.y >= groundNormalMinY)
            {
                transform.position = hit.point + hit.normal * (radius + 0.05f);
            }
            else
            {
                transform.position = hit.point - direction * Mathf.Min(radius, distance);
                travelled += hit.distance;
                HandleHit(hit);
                return;
            }
        }
        else
        {
            transform.position += direction * distance;
        }

        travelled += distance;

        Vector3 position = transform.position;
        ProjectileGroundHover.TryAdjustHeight(ref position, hoverHeight, heightAdjustSpeed, groundMask);
        transform.position = position;
        baseY = position.y;

        AnimateSpin();

        if (travelled >= maxDistance)
            Miss();
    }

    private void UpdateHoming()
    {
        if (homingTurnRate <= 0f || targetKartObject == null || !targetKartObject.activeInHierarchy)
            return;

        Vector3 toTarget = Planar(targetKartObject.transform.position - transform.position);
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        direction = Vector3.RotateTowards(
            direction,
            toTarget.normalized,
            homingTurnRate * Mathf.Deg2Rad * Time.deltaTime,
            0f).normalized;
    }

    private void AnimateSpin()
    {
        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);

        Vector3 position = transform.position;
        position.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = position;

        transform.rotation = Quaternion.Euler(0f, spinAngle, 0f);
    }

    private bool TryFindHit(float distance, out RaycastHit bestHit)
    {
        int hitCount = Physics.SphereCastNonAlloc(
            transform.position,
            radius,
            direction,
            hits,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        bestHit = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit candidate = hits[i];

            if (candidate.collider == null)
                continue;

            if (candidate.collider.transform.IsChildOf(transform))
                continue;

            if (owner != null && candidate.collider.transform.IsChildOf(owner.transform))
                continue;

            if (candidate.distance < bestDistance)
            {
                bestHit = candidate;
                bestDistance = candidate.distance;
            }
        }

        return bestDistance < float.MaxValue;
    }

    private void HandleHit(RaycastHit hit)
    {
        // Escudo ativo no alvo: poder bloqueado, sem troca.
        KartPowerUser shieldUser = hit.collider.GetComponentInParent<KartPowerUser>();
        if (shieldUser != null && shieldUser.gameObject != owner && shieldUser.IsShieldActive)
        {
            shieldUser.PulseShieldBlock(hit.point, null);
            SpawnVFX(boingVFXPrefab, hit.point, hit.normal, boingScale);
            Destroy(gameObject);
            return;
        }

        // Carro válido: inicia a captura (subir + círculos + swap).
        KartController kart = hit.collider.GetComponentInParent<KartController>();
        if (kart != null && kart.gameObject != owner)
        {
            BeginCapture(kart);
            return;
        }

        // Parede: boing e quica (ou erro, se esgotou os quiques).
        if (bounceCount < maxBounces)
        {
            bounceCount++;
            Vector3 flatNormal = Planar(hit.normal).normalized;
            if (flatNormal.sqrMagnitude < 0.001f)
                flatNormal = -direction;

            direction = Vector3.Reflect(direction, flatNormal).normalized;
            speed *= bounceSpeedRetention;
            transform.position = hit.point + hit.normal * (radius + 0.05f);
            SpawnVFX(boingVFXPrefab, hit.point, hit.normal, boingScale);
        }
        else
        {
            Miss();
        }
    }

    // ------------------------------------------------------------------ captura/swap
    private void BeginCapture(KartController hitKart)
    {
        state = State.Capturing;
        capturedKart = hitKart;
        captureTimer = 0f;
        captureStartPosition = transform.position;
    }

    private void UpdateCapture()
    {
        if (!IsCaptureStillValid())
        {
            Miss();
            return;
        }

        captureTimer += Time.deltaTime;
        float t = Mathf.Clamp01(captureTimer / Mathf.Max(0.05f, captureRiseDuration));
        float smooth = Mathf.SmoothStep(0f, 1f, t);

        // Sobe suavemente até pairar sobre o carro atingido (acompanhando-o em movimento).
        Vector3 targetPosition = capturedKart.transform.position + Vector3.up * captureRiseHeight;
        transform.position = Vector3.Lerp(captureStartPosition, targetPosition, smooth);

        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);
        transform.rotation = Quaternion.Euler(0f, spinAngle, 0f);

        if (t >= 1f)
            BeginSwapWait();
    }

    private void BeginSwapWait()
    {
        state = State.WaitingSwap;
        swapTimer = 0f;

        float circleLifetime = swapDelay + magicCircleLinger;
        SpawnMagicCircle(owner != null ? owner.transform : null, circleLifetime);
        SpawnMagicCircle(capturedKart != null ? capturedKart.transform : null, circleLifetime);
    }

    private void SpawnMagicCircle(Transform target, float circleLifetime)
    {
        if (magicCirclePrefab == null || target == null)
            return;

        GameObject circle = Instantiate(magicCirclePrefab, target.position, Quaternion.identity);
        GroundFollowEffect.Attach(circle, target, circleLifetime, groundMask);
    }

    private void UpdateSwapWait()
    {
        if (!IsCaptureStillValid())
        {
            Miss();
            return;
        }

        // Continua pairando sobre o carro capturado durante o delay.
        transform.position = capturedKart.transform.position + Vector3.up * captureRiseHeight;
        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);
        transform.rotation = Quaternion.Euler(0f, spinAngle, 0f);

        swapTimer += Time.deltaTime;
        if (swapTimer < swapDelay)
            return;

        ExecuteSwap();
    }

    private bool IsCaptureStillValid()
    {
        return capturedKart != null
            && capturedKart.gameObject.activeInHierarchy
            && owner != null
            && owner.activeInHierarchy;
    }

    private void ExecuteSwap()
    {
        state = State.Done;

        // Última checagem de escudo (o alvo pode ter ativado durante o delay).
        KartPowerUser targetPowerUser = capturedKart.GetComponent<KartPowerUser>();
        if (targetPowerUser != null && targetPowerUser.IsShieldActive)
        {
            targetPowerUser.PulseShieldBlock(capturedKart.transform.position + Vector3.up, null);
            Destroy(gameObject);
            return;
        }

        RaceHudEvents.Raise(owner, capturedKart.gameObject, RaceHudEventKind.HitOpponent, KartPowerType.SwapPosition);
        RaceHudEvents.Raise(capturedKart.gameObject, owner, RaceHudEventKind.GotHit, KartPowerType.SwapPosition);

        KartSwapUtility.SwapPositions(owner, capturedKart.gameObject);

        Destroy(gameObject);
    }

    // ------------------------------------------------------------------ erro
    private void Miss()
    {
        if (state == State.Done)
            return;

        state = State.Done;

        if (missVFXPrefab != null)
        {
            GameObject vfx = Instantiate(missVFXPrefab, transform.position, Quaternion.identity);
            if (!Mathf.Approximately(missVFXScale, 1f))
                vfx.transform.localScale *= missVFXScale;
        }

        Destroy(gameObject);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Vector3 normal, float scale)
    {
        if (prefab == null)
            return;

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal.normalized, Vector3.up)
            : Quaternion.identity;

        GameObject vfx = Instantiate(prefab, position, rotation);

        if (!Mathf.Approximately(scale, 1f))
            vfx.transform.localScale *= scale;
    }

    private static Vector3 Planar(Vector3 v)
    {
        v.y = 0f;
        return v;
    }

    private void EnsurePhysics()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();

        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        SphereCollider sphere = GetComponent<SphereCollider>();
        if (sphere == null)
            sphere = gameObject.AddComponent<SphereCollider>();

        sphere.radius = radius;
        sphere.isTrigger = true;
    }
}
