using UnityEngine;

// Projétil de foguete cartunesco. Voa para frente com trajetória controlada (sphere-cast),
// quica nas paredes até 'maxBounces' vezes (VFXBoing a cada quique) e só explode quando:
//  - atinge um carro inimigo (explode + spin-out + knockback),
//  - excede o alcance/tempo de vida, ou
//  - bate numa parede DEPOIS de já ter quicado o máximo de vezes.
// Mantém o trail embutido do próprio prefab Rocket. NÃO explode ao ser disparado.
[DisallowMultipleComponent]
public class RocketProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [Tooltip("Velocidade de voo (m/s). Deve ser maior que a velocidade máxima dos carros.")]
    [SerializeField] private float speed = 52f;
    [SerializeField] private float lifetime = 6f;
    [SerializeField] private float maxDistance = 110f;
    [SerializeField] private float radius = 0.45f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("Mantém o voo horizontal (recomendado para bater nas paredes verticais da pista).")]
    [SerializeField] private bool keepHorizontal = true;

    [Header("Altura sobre o chão")]
    [Tooltip("Mantém o míssil a uma altura fixa em relação ao chão (não cai nem sobe demais).")]
    [SerializeField] private bool maintainGroundHeight = true;
    [Tooltip("Altura fixa mantida em relação ao chão durante o voo (m).")]
    [SerializeField] private float hoverHeight = 1.1f;
    [SerializeField] private float heightAdjustSpeed = 12f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("Normais com Y acima disso são chão (não disparam quique/explosão; a altura cuida).")]
    [SerializeField, Range(0f, 1f)] private float groundNormalMinY = 0.55f;

    [Header("Quique")]
    [SerializeField] private int maxBounces = 3;
    [SerializeField, Range(0.5f, 1f)] private float bounceSpeedRetention = 0.96f;

    [Header("Orientação do modelo")]
    [Tooltip("Correção para alinhar a frente do modelo com a direção de voo.")]
    [SerializeField] private Vector3 forwardAxisOffset = new Vector3(90f, 0f, 0f);
    [SerializeField] private float modelScale = 0.07f;

    [Header("Trajeto cartunesco (sem perder a trajetória)")]
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleSpeed = 15f;

    [Header("Impacto / Spin-out")]
    [SerializeField] private float spinOutDuration = 1.6f;
    [SerializeField] private float knockbackForce = 14f;
    [SerializeField] private float knockbackTorque = 12f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private GameObject boingVFXPrefab;
    [SerializeField] private float boingScale = 1f;
    [Tooltip("Trail do foguete em voo (ex.: VFXRocketTrail). Instanciado SEM parent para não herdar o modelScale do foguete.")]
    [SerializeField] private GameObject trailPrefab;
    [Tooltip("Posição do trail na CAUDA do foguete, no espaço LOCAL do foguete (em metros). Medido da montagem na cena DEMO.")]
    [SerializeField] private Vector3 trailLocalOffset = new Vector3(-0.0135f, -0.364f, -0.0002f);
    [Tooltip("Rotação do trail relativa ao foguete (euler). Medido da montagem na cena DEMO.")]
    [SerializeField] private Vector3 trailLocalEuler = new Vector3(90f, 90f, 0f);
    [Tooltip("Tempo que o trail continua (esvaindo) após o foguete explodir.")]
    [SerializeField] private float trailLingerAfterExplode = 1.2f;

    private readonly RaycastHit[] hits = new RaycastHit[16];
    private GameObject owner;
    private Vector3 direction;
    private float lifeTimer;
    private float travelled;
    private int bounceCount;
    private bool exploded;
    private Transform trailInstance;

    public void Initialize(GameObject projectileOwner, Vector3 fireDirection, GameObject explosionVFX, GameObject boingVFX, GameObject trailVFX = null)
    {
        owner = projectileOwner;
        direction = Normalize(fireDirection);

        if (explosionVFX != null)
            explosionVFXPrefab = explosionVFX;

        if (boingVFX != null)
            boingVFXPrefab = boingVFX;

        if (trailVFX != null)
            trailPrefab = trailVFX;
    }

    private void Awake()
    {
        EnsurePhysics();
        transform.localScale = Vector3.one * modelScale;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Normalize(transform.forward);
    }

    private void Update()
    {
        if (exploded)
            return;

        lifeTimer += Time.deltaTime;

        if (trailInstance == null && trailPrefab != null)
            SpawnTrail();

        if (lifeTimer >= lifetime)
        {
            Explode(transform.position, -direction);
            return;
        }

        MoveProjectile();

        if (!exploded)
        {
            Aim();
            UpdateTrail();
        }
    }

    private void SpawnTrail()
    {
        // Sem parent: o foguete tem localScale = modelScale (~0.07); parentar encolheria o trail
        // (a montagem na cena usa o trail em escala mundial 1).
        GameObject trail = Instantiate(trailPrefab);
        trailInstance = trail.transform;
        PositionTrail();
    }

    private void UpdateTrail()
    {
        if (trailInstance == null)
            return;

        PositionTrail();
    }

    // Coloca o trail na CAUDA do foguete reproduzindo a montagem da cena DEMO: offset e rotação
    // medidos no espaço LOCAL do foguete e aplicados sobre a orientação de voo atual.
    private void PositionTrail()
    {
        trailInstance.position = transform.position + transform.rotation * trailLocalOffset;
        trailInstance.rotation = transform.rotation * Quaternion.Euler(trailLocalEuler);
    }

    // Solta o trail do foguete e o deixa esvair sozinho (para de emitir e destrói depois).
    private void ReleaseTrail()
    {
        if (trailInstance == null)
            return;

        Transform trail = trailInstance;
        trailInstance = null;

        foreach (ParticleSystem ps in trail.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        foreach (TrailRenderer tr in trail.GetComponentsInChildren<TrailRenderer>(true))
            tr.emitting = false;

        Destroy(trail.gameObject, trailLingerAfterExplode);
    }

    private void MoveProjectile()
    {
        float distance = speed * Time.deltaTime;

        if (TryFindHit(distance, out RaycastHit hit))
        {
            // Chão/rampa não é obstáculo quando o míssil mantém altura: reposiciona acima
            // da superfície e segue voando (o ajuste de altura cuida da subida).
            if (maintainGroundHeight && hit.normal.y >= groundNormalMinY)
            {
                transform.position = hit.point + hit.normal * (radius + 0.05f);
                travelled += hit.distance;
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
            travelled += distance;
        }

        if (maintainGroundHeight)
        {
            Vector3 position = transform.position;
            ProjectileGroundHover.TryAdjustHeight(ref position, hoverHeight, heightAdjustSpeed, groundMask);
            transform.position = position;
        }

        if (travelled >= maxDistance)
            Explode(transform.position, -direction);
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
            QueryTriggerInteraction.Ignore
        );

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
        // Escudo inimigo ativo: o foguete "quica" e some sem causar dano.
        KartPowerUser shieldUser = hit.collider.GetComponentInParent<KartPowerUser>();
        if (shieldUser != null && shieldUser.gameObject != owner && shieldUser.IsShieldActive)
        {
            shieldUser.PulseShieldBlock(hit.point, null);
            SpawnVFX(boingVFXPrefab, hit.point, hit.normal, boingScale);
            ReleaseTrail();
            Destroy(gameObject);
            return;
        }

        // Carro inimigo: explode imediatamente + spin-out.
        KartController kart = hit.collider.GetComponentInParent<KartController>();
        if (kart != null && kart.gameObject != owner)
        {
            RaceHudEvents.Raise(owner, kart.gameObject, RaceHudEventKind.HitOpponent, KartPowerType.Rocket);
            RaceHudEvents.Raise(kart.gameObject, owner, RaceHudEventKind.GotHit, KartPowerType.Rocket);
            KartSpinOutEffect.ApplyTo(kart.gameObject, spinOutDuration, direction, knockbackForce, knockbackTorque);
            Explode(hit.point, hit.normal);
            return;
        }

        // Parede / obstáculo: quica ou explode (se já passou do limite de quiques).
        if (bounceCount < maxBounces)
        {
            bounceCount++;
            direction = Normalize(Vector3.Reflect(direction, hit.normal));
            speed *= bounceSpeedRetention;
            transform.position = hit.point + hit.normal * (radius + 0.05f);
            SpawnVFX(boingVFXPrefab, hit.point, hit.normal, boingScale);
        }
        else
        {
            Explode(hit.point, hit.normal);
        }
    }

    private void Aim()
    {
        Quaternion baseRotation = Quaternion.LookRotation(direction, Vector3.up);
        float t = Time.time * wobbleSpeed;
        Quaternion wobble = Quaternion.Euler(
            Mathf.Sin(t) * wobbleAngle * 0.55f,
            0f,
            Mathf.Sin(t * 1.3f) * wobbleAngle
        );

        transform.rotation = baseRotation * wobble * Quaternion.Euler(forwardAxisOffset);
    }

    private void Explode(Vector3 position, Vector3 normal)
    {
        if (exploded)
            return;

        exploded = true;
        ReleaseTrail();
        SpawnVFX(explosionVFXPrefab, position, normal.sqrMagnitude > 0.001f ? normal : Vector3.up, 1f);
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

    private Vector3 Normalize(Vector3 v)
    {
        if (keepHorizontal)
            v.y = 0f;

        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
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
