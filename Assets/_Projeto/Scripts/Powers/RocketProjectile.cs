using UnityEngine;

[DisallowMultipleComponent]
public class RocketProjectile : MonoBehaviour
{
    [Header("Movimento")]
    [SerializeField] private float speed = 60f;
    [SerializeField] private float lifetime = 8f;
    [SerializeField] private float maxDistance = 170f;
    [Tooltip("Raio de acerto e varredura continua do foguete.")]
    [SerializeField] private float radius = 0.85f;
    [SerializeField] private LayerMask collisionMask = ~0;
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private bool keepHorizontal = true;

    [Header("Altura sobre o chao")]
    [SerializeField] private bool maintainGroundHeight = true;
    [SerializeField] private float hoverHeight = 1.15f;
    [SerializeField] private float heightAdjustSpeed = 16f;
    [SerializeField] private LayerMask groundMask = ~0;
    [Tooltip("A partir de que inclinação a superfície conta como CHÃO (e o foguete passa por " +
             "cima) em vez de OBSTÁCULO (e explode). 0,55 ≈ 57°, o que fazia toda rampa da pista " +
             "virar parede e o foguete sumir nela. 0,3 ≈ 72°: só parede de verdade explode.")]
    [SerializeField, Range(0f, 1f)] private float groundNormalMinY = 0.3f;

    [Header("Auto-hit")]
    [SerializeField] private bool allowOwnerHitAfterIgnore = true;
    [SerializeField] private float ignoreOwnerTime = 0.35f;
    [SerializeField] private float ignoreOwnerDistance = 4f;

    [Header("Quique")]
    [SerializeField] private int maxBounces = 4;
    [SerializeField, Range(0.5f, 1f)] private float bounceSpeedRetention = 0.96f;

    [Header("Orientacao do modelo")]
    [SerializeField] private Vector3 forwardAxisOffset = new Vector3(90f, 0f, 0f);
    [SerializeField] private float modelScale = 0.07f;

    [Header("Trajeto cartunesco")]
    [SerializeField] private float wobbleAngle = 8f;
    [SerializeField] private float wobbleSpeed = 15f;

    [Header("Impacto / Spin-out")]
    [SerializeField] private float spinOutDuration = 1.6f;
    [SerializeField] private float knockbackForce = 16f;
    [SerializeField] private float knockbackTorque = 14f;
    [Tooltip("Tempo (s) de invulnerabilidade/ghost concedido ao kart atingido após o míssil. 0 desliga.")]
    [SerializeField] private float ghostDurationOnHit = 3f;

    [Header("VFX")]
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private GameObject blockVFXPrefab;
    [SerializeField] private GameObject boingVFXPrefab;
    [SerializeField] private float boingScale = 1f;
    [SerializeField] private GameObject trailPrefab;
    [SerializeField] private Vector3 trailLocalOffset = new Vector3(-0.0135f, -0.364f, -0.0002f);
    [SerializeField] private Vector3 trailLocalEuler = new Vector3(90f, 90f, 0f);
    [SerializeField] private float trailLingerAfterExplode = 1.2f;
    [SerializeField] private float vfxFallbackLifetime = 2f;
    [SerializeField] private float destroyDelayAfterImpact = 0f;

    [Header("Audio")]
    [SerializeField] private AudioClip impactSound;
    [SerializeField] private AudioClip bounceSound;
    [SerializeField] private AudioClip blockSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.85f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;
    [SerializeField] private Color debugPathColor = Color.red;
    [SerializeField] private Color debugHitColor = Color.yellow;

    private enum HitKind { Target, Obstacle }

    private struct ProjectileHit
    {
        public Collider Collider;
        public Vector3 Point;
        public Vector3 Normal;
        public float Distance;
        public HitKind Kind;
    }

    private readonly RaycastHit[] sweepHits = new RaycastHit[24];
    private float ultimoChaoY;
    private bool temChao;
    private readonly Collider[] overlapHits = new Collider[24];

    private GameObject owner;
    private Vector3 direction;
    private float lifeTimer;
    private float travelled;
    private int bounceCount;
    private bool finished;
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

    // Alimenta o arco vermelho da borda do HUD — o único aviso de ataque do design.
    private void OnEnable() => RaceThreats.Registrar(transform);
    private void OnDisable() => RaceThreats.Remover(transform);

    private void Update()
    {
        if (finished)
            return;

        lifeTimer += Time.deltaTime;

        if (trailInstance == null && trailPrefab != null)
            SpawnTrail();

        if (lifeTimer >= lifetime)
        {
            Finish(transform.position, -direction, explosionVFXPrefab, impactSound);
            return;
        }

        MoveProjectile();

        if (!finished)
        {
            Aim();
            UpdateTrail();
        }
    }

    private void MoveProjectile()
    {
        if (TryFindOverlapHit(transform.position, out ProjectileHit overlapHit))
        {
            HandleHit(overlapHit);
            return;
        }

        float distance = speed * Time.deltaTime;
        Vector3 start = transform.position;

        if (TryFindSweepHit(distance, out ProjectileHit hit))
        {
            transform.position = hit.Point - direction * Mathf.Min(radius, distance);
            travelled += hit.Distance;
            HandleHit(hit);
            return;
        }

        transform.position += direction * distance;
        travelled += distance;

        if (maintainGroundHeight)
        {
            Vector3 position = transform.position;
            if (ProjectileGroundHover.TryAdjustHeight(ref position, hoverHeight, heightAdjustSpeed, groundMask))
            {
                temChao = true;
                ultimoChaoY = position.y - hoverHeight;
            }
            else if (temChao)
            {
                // Sem chão sob a sonda (vão, buraco, borda de rampa) o ajuste não acontecia e o
                // foguete continuava descendo até sumir sob o cenário. Segura na última altura
                // de chão conhecida até o terreno reaparecer.
                position.y = Mathf.Max(position.y, ultimoChaoY + hoverHeight);
            }
            transform.position = position;
        }

        // SphereCast nao reporta de forma confiavel quando o frame comeca ou termina dentro de
        // uma borda fina. A verificacao volumetrica fecha essa brecha sem depender do framerate.
        if (TryFindOverlapHit(transform.position, out ProjectileHit postMoveOverlap))
        {
            HandleHit(postMoveOverlap);
            return;
        }

        if (debugMode)
            Debug.DrawLine(start, transform.position, debugPathColor, Time.deltaTime);

        if (travelled >= maxDistance)
            Finish(transform.position, -direction, explosionVFXPrefab, impactSound);
    }

    private bool TryFindSweepHit(float distance, out ProjectileHit bestHit)
    {
        int mask = targetMask.value | obstacleMask.value | groundMask.value | collisionMask.value;
        int count = Physics.SphereCastNonAlloc(
            transform.position,
            radius,
            direction,
            sweepHits,
            distance,
            mask,
            QueryTriggerInteraction.Collide);

        bestHit = default;
        bestHit.Distance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            RaycastHit candidate = sweepHits[i];
            if (candidate.collider == null)
                continue;

            if (!TryClassifyHit(candidate.collider, candidate.point, candidate.normal, candidate.distance, out ProjectileHit classified))
                continue;

            if (classified.Distance < bestHit.Distance)
                bestHit = classified;
        }

        return bestHit.Collider != null;
    }

    private bool TryFindOverlapTarget(Vector3 position, out ProjectileHit hit)
    {
        int count = Physics.OverlapSphereNonAlloc(position, radius, overlapHits, targetMask, QueryTriggerInteraction.Collide);
        hit = default;
        hit.Distance = float.MaxValue;
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapHits[i];
            if (candidate == null)
                continue;

            KartController kart = candidate.GetComponentInParent<KartController>();
            if (!IsValidTarget(kart, candidate))
                continue;

            Vector3 point = candidate.ClosestPoint(position);
            Vector3 normal = position - point;
            if (normal.sqrMagnitude < 0.001f)
                normal = -direction;

            float sqrDistance = (point - position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            hit = new ProjectileHit
            {
                Collider = candidate,
                Point = point,
                Normal = normal.normalized,
                Distance = 0f,
                Kind = HitKind.Target
            };
        }

        return hit.Collider != null;
    }

    private bool TryFindOverlapHit(Vector3 position, out ProjectileHit hit)
    {
        if (TryFindOverlapTarget(position, out hit))
            return true;

        int mask = obstacleMask.value | collisionMask.value | groundMask.value;
        int count = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            overlapHits,
            mask,
            QueryTriggerInteraction.Ignore);

        hit = default;
        hit.Distance = float.MaxValue;
        float bestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider candidate = overlapHits[i];
            if (candidate == null || candidate.transform.IsChildOf(transform))
                continue;

            Vector3 point = candidate.ClosestPoint(position);
            Vector3 normal = position - point;
            if (normal.sqrMagnitude < 0.001f)
                normal = -direction;

            if (!TryClassifyHit(candidate, point, normal.normalized, 0f, out ProjectileHit classified)
                || classified.Kind != HitKind.Obstacle)
                continue;

            float sqrDistance = (point - position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            bestSqrDistance = sqrDistance;
            hit = classified;
        }

        return hit.Collider != null;
    }

    private bool TryClassifyHit(Collider candidate, Vector3 point, Vector3 normal, float distance, out ProjectileHit hit)
    {
        hit = default;

        if (candidate == null || candidate.transform.IsChildOf(transform))
            return false;

        KartController kart = candidate.GetComponentInParent<KartController>();
        if (kart != null)
        {
            if (!IsValidTarget(kart, candidate))
                return false;

            hit = new ProjectileHit
            {
                Collider = candidate,
                Point = point,
                Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : -direction,
                Distance = distance,
                Kind = HitKind.Target
            };
            return true;
        }

        if (maintainGroundHeight && IsGroundContact(point, normal))
            return false;

        if (candidate.isTrigger || !IsInMask(candidate.gameObject.layer, obstacleMask))
            return false;

        hit = new ProjectileHit
        {
            Collider = candidate,
            Point = point,
            Normal = normal.sqrMagnitude > 0.001f ? normal.normalized : -direction,
            Distance = distance,
            Kind = HitKind.Obstacle
        };
        return true;
    }

    private bool IsGroundContact(Vector3 point, Vector3 normal)
    {
        if (normal.y < groundNormalMinY)
            return false;

        // Chao/rampa fica claramente abaixo do centro do foguete. Um topo chanfrado de borda
        // tocado pela lateral nao satisfaz essa relacao e continua sendo obstaculo.
        float minimumBelowCenter = Mathf.Max(0.15f, radius * 0.42f);
        return transform.position.y - point.y >= minimumBelowCenter;
    }

    private bool IsValidTarget(KartController kart, Collider candidate)
    {
        if (kart == null || !kart.gameObject.activeInHierarchy)
            return false;

        if (!IsInMask(candidate.gameObject.layer, targetMask))
            return false;

        if (owner == null)
            return true;

        if (!candidate.transform.IsChildOf(owner.transform))
            return true;

        if (!allowOwnerHitAfterIgnore)
            return false;

        return lifeTimer >= ignoreOwnerTime && travelled >= ignoreOwnerDistance;
    }

    private void HandleHit(ProjectileHit hit)
    {
        if (hit.Collider == null)
            return;

        if (debugMode)
            Debug.DrawRay(hit.Point, hit.Normal * 2f, debugHitColor, 0.75f);

        if (hit.Kind == HitKind.Target)
        {
            HandleTargetHit(hit);
            return;
        }

        HandleObstacleHit(hit);
    }

    private void HandleTargetHit(ProjectileHit hit)
    {
        KartController kart = hit.Collider.GetComponentInParent<KartController>();
        if (kart == null)
            return;

        KartPowerUser shieldUser = kart.GetComponent<KartPowerUser>();
        if (shieldUser != null && shieldUser.IsShieldActive)
        {
            shieldUser.PulseShieldBlock(hit.Point, blockVFXPrefab);
            SpawnVFX(boingVFXPrefab, hit.Point, hit.Normal, boingScale);
            PlaySound(blockSound, hit.Point);
            FinishWithoutImpactVFX();
            return;
        }

        RaceHudEvents.Raise(owner, kart.gameObject, RaceHudEventKind.HitOpponent, KartPowerType.Rocket);
        RaceHudEvents.Raise(kart.gameObject, owner, RaceHudEventKind.GotHit, KartPowerType.Rocket);

        // Dano de ITEM (15). Vem depois do teste de escudo acima: se a bolha estava no ar o
        // método já retornou e nada disto roda.
        KartHealth health = kart.GetComponent<KartHealth>();
        if (health != null)
            health.ApplyItemDamage(hit.Point, owner);

        KartSpinOutEffect.ApplyTo(kart.gameObject, spinOutDuration, direction, knockbackForce, knockbackTorque);

        // Estado ghost: o atingido fica brevemente intangível para outros karts (não para a pista),
        // evitando ser atropelado em sequência por quem vem atrás enquanto se recupera.
        if (ghostDurationOnHit > 0f)
            KartTemporaryGhostState.Apply(kart.gameObject, ghostDurationOnHit);

        Finish(hit.Point, hit.Normal, explosionVFXPrefab, impactSound);
    }

    private void HandleObstacleHit(ProjectileHit hit)
    {
        if (bounceCount < maxBounces)
        {
            bounceCount++;
            direction = Normalize(Vector3.Reflect(direction, hit.Normal));
            speed *= bounceSpeedRetention;
            transform.position = hit.Point + hit.Normal * (radius + 0.05f);
            SpawnVFX(boingVFXPrefab, hit.Point, hit.Normal, boingScale);
            PlaySound(bounceSound, hit.Point);
            return;
        }

        Finish(hit.Point, hit.Normal, explosionVFXPrefab, impactSound);
    }

    private void SpawnTrail()
    {
        GameObject trail = Instantiate(trailPrefab);
        trailInstance = trail.transform;
        PositionTrail();
    }

    private void UpdateTrail()
    {
        if (trailInstance != null)
            PositionTrail();
    }

    private void PositionTrail()
    {
        trailInstance.position = transform.position + transform.rotation * trailLocalOffset;
        trailInstance.rotation = transform.rotation * Quaternion.Euler(trailLocalEuler);
    }

    private void ReleaseTrail()
    {
        if (trailInstance == null)
            return;

        Transform trail = trailInstance;
        trailInstance = null;
        PowerVFXUtility.StopAndDestroyTrail(trail, trailLingerAfterExplode);
    }

    private void Aim()
    {
        Quaternion baseRotation = Quaternion.LookRotation(direction, Vector3.up);
        float t = Time.time * wobbleSpeed;
        Quaternion wobble = Quaternion.Euler(
            Mathf.Sin(t) * wobbleAngle * 0.55f,
            0f,
            Mathf.Sin(t * 1.3f) * wobbleAngle);

        transform.rotation = baseRotation * wobble * Quaternion.Euler(forwardAxisOffset);
    }

    private void Finish(Vector3 position, Vector3 normal, GameObject vfxPrefab, AudioClip sound)
    {
        if (finished)
            return;

        finished = true;
        ReleaseTrail();
        SpawnVFX(vfxPrefab, position, normal, 1f);
        PlaySound(sound, position);
        Destroy(gameObject, destroyDelayAfterImpact);
    }

    private void FinishWithoutImpactVFX()
    {
        if (finished)
            return;

        finished = true;
        ReleaseTrail();
        Destroy(gameObject, destroyDelayAfterImpact);
    }

    private void SpawnVFX(GameObject prefab, Vector3 position, Vector3 normal, float scale)
    {
        if (prefab == null)
            return;

        Quaternion rotation = normal.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(normal.normalized, Vector3.up)
            : Quaternion.identity;

        PowerVFXUtility.SpawnOneShot(prefab, position, rotation, vfxFallbackLifetime, 0f, scale);
    }

    private void PlaySound(AudioClip clip, Vector3 position)
    {
        if (clip != null && soundVolume > 0f)
            AudioSource.PlayClipAtPoint(clip, position, soundVolume);
    }

    private Vector3 Normalize(Vector3 value)
    {
        if (keepHorizontal)
            value.y = 0f;

        return value.sqrMagnitude > 0.0001f ? value.normalized : transform.forward;
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

    private void OnDrawGizmosSelected()
    {
        if (!debugMode)
            return;

        Gizmos.color = debugPathColor;
        Gizmos.DrawWireSphere(transform.position, radius);
        Gizmos.DrawLine(transform.position, transform.position + Normalize(transform.forward) * 4f);
    }

    private static bool IsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }
}
