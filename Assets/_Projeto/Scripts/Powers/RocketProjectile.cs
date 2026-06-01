using UnityEngine;

[DisallowMultipleComponent]
public class RocketProjectile : MonoBehaviour
{
    [Header("Trajeto")]
    [SerializeField] private float speed = 38f;
    [SerializeField] private float maxDistance = 60f;
    [SerializeField] private float maxLifetime = 6f;
    [SerializeField] private float collisionRadius = 0.35f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Bounce em paredes")]
    [SerializeField] private int maxBounces = 3;
    [SerializeField, Range(0f, 1.5f)] private float bounceSpeedMultiplier = 0.95f;
    [SerializeField] private float postBounceCooldown = 0.05f;

    [Header("Animacao do voo")]
    [Tooltip("Amplitude do balanço lateral durante o voo (metros).")]
    [SerializeField] private float wobbleAmount = 0.04f;
    [Tooltip("Velocidade do balanço lateral.")]
    [SerializeField] private float wobbleSpeed = 4f;
    [Tooltip("Velocidade de rotação em torno do eixo de voo (giro de estabilização).")]
    [SerializeField] private float rollSpeed = 60f;
    [Tooltip("Quão rápido o nariz acompanha mudanças de direção (após bounce).")]
    [SerializeField] private float aimSmooth = 22f;
    [Tooltip("Quanto a wobble decai com o tempo (1 = constante, 0 = some).")]
    [SerializeField, Range(0f, 1f)] private float wobbleSustain = 0.65f;
    [Tooltip("Punch de escala no lançamento.")]
    [SerializeField] private float launchPunchScale = 1.25f;
    [SerializeField] private float launchPunchDuration = 0.18f;

    [Header("VFX")]
    [SerializeField] private GameObject boingVFXPrefab;
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField, Range(0f, 5f)] private float vfxScale = 1f;

    [Header("Som / Debug")]
    [SerializeField] private bool drawDebug;

    private GameObject owner;
    private Vector3 velocityDir;
    private Vector3 spawnPosition;
    private float spawnTime;
    private int bouncesUsed;
    private float nextCollisionAllowedTime;
    private bool exploded;
    private Transform visualRoot;
    private Vector3 visualBaseScale = Vector3.one;
    private float wobblePhase;

    public void Initialize(GameObject ownerObject, Vector3 forwardDirection, GameObject boingPrefab, GameObject explosionPrefab)
    {
        owner = ownerObject;
        velocityDir = forwardDirection.sqrMagnitude > 0.001f ? forwardDirection.normalized : transform.forward;
        boingVFXPrefab = boingPrefab != null ? boingPrefab : boingVFXPrefab;
        explosionVFXPrefab = explosionPrefab != null ? explosionPrefab : explosionVFXPrefab;
        spawnPosition = transform.position;
        spawnTime = Time.time;
        // Rocket mesh has its tip along local +Y, so align +Y with the velocity direction.
        transform.rotation = Quaternion.FromToRotation(Vector3.up, velocityDir);
        wobblePhase = Random.value * Mathf.PI * 2f;
    }

    private void Awake()
    {
        BuildVisualHierarchy();
        spawnTime = Time.time;
    }

    private void BuildVisualHierarchy()
    {
        // Move existing renderers/particles under a "Visual" child so we can
        // animate them (wobble, roll, punch) without affecting collision casts.
        if (transform.Find("Visual") != null)
        {
            visualRoot = transform.Find("Visual");
            visualBaseScale = visualRoot.localScale;
            return;
        }

        GameObject visualGO = new GameObject("Visual");
        visualRoot = visualGO.transform;
        visualRoot.SetParent(transform, false);
        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;

        // Move children, mesh filter, mesh renderer under visualRoot.
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == visualRoot)
                continue;
            child.SetParent(visualRoot, true);
        }

        MeshFilter rootFilter = GetComponent<MeshFilter>();
        MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
        if (rootFilter != null && rootRenderer != null)
        {
            GameObject body = new GameObject("RocketBody");
            body.transform.SetParent(visualRoot, false);

            MeshFilter bf = body.AddComponent<MeshFilter>();
            bf.sharedMesh = rootFilter.sharedMesh;
            MeshRenderer br = body.AddComponent<MeshRenderer>();
            br.sharedMaterials = rootRenderer.sharedMaterials;
            br.shadowCastingMode = rootRenderer.shadowCastingMode;
            br.receiveShadows = rootRenderer.receiveShadows;

            Destroy(rootRenderer);
            Destroy(rootFilter);
        }

        visualBaseScale = visualRoot.localScale;
    }

    private void Update()
    {
        if (exploded)
            return;

        float dt = Time.deltaTime;
        float age = Time.time - spawnTime;

        if (age >= maxLifetime)
        {
            Explode(transform.position, -velocityDir);
            return;
        }

        float traveled = Vector3.Distance(spawnPosition, transform.position);
        if (traveled >= maxDistance)
        {
            Explode(transform.position, -velocityDir);
            return;
        }

        float step = speed * dt;
        Vector3 origin = transform.position;
        Vector3 dir = velocityDir;

        if (Time.time >= nextCollisionAllowedTime && step > 0f)
        {
            if (Physics.SphereCast(origin, collisionRadius, dir, out RaycastHit hit, step, collisionMask, QueryTriggerInteraction.Ignore))
            {
                HandleHit(hit);
                if (exploded) return;
                // bounce path: consumed up to hit.distance, reflect rest
                float consumed = Mathf.Max(0f, hit.distance);
                transform.position = origin + dir * consumed;
                return;
            }
        }

        transform.position += dir * step;

        AnimateVisual(dt, age);

        if (drawDebug)
            Debug.DrawRay(transform.position, velocityDir * 2f, Color.red, 0.05f);
    }

    private void AnimateVisual(float dt, float age)
    {
        if (visualRoot == null) return;

        // Smoothly aim at velocity direction (rocket tip is local +Y).
        Quaternion targetRot = Quaternion.FromToRotation(Vector3.up, velocityDir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, dt * aimSmooth);

        // Lateral wobble — subtle "fin correction" sway in local X/Z (perpendicular to local +Y flight axis).
        // Decays smoothly so the rocket settles into a clean straight line after launch turbulence.
        float t = age * wobbleSpeed + wobblePhase;
        float decay = Mathf.Lerp(1f, wobbleSustain, Mathf.Clamp01(age * 0.6f));
        Vector3 lateralOffset = new Vector3(
            Mathf.Sin(t) * wobbleAmount * decay,
            0f,
            Mathf.Cos(t * 0.7f) * wobbleAmount * 0.5f * decay
        );
        visualRoot.localPosition = lateralOffset;

        // Gentle roll around the flight axis (local +Y), like a fin-stabilized rocket.
        visualRoot.Rotate(0f, rollSpeed * dt, 0f, Space.Self);

        // Launch punch (squash/stretch along flight axis = local +Y).
        float punchT = Mathf.Clamp01(age / Mathf.Max(0.001f, launchPunchDuration));
        float yPunch = Mathf.Lerp(launchPunchScale, 1f, punchT);
        float xzPunch = Mathf.Lerp(2f - launchPunchScale, 1f, punchT);
        visualRoot.localScale = new Vector3(
            visualBaseScale.x * xzPunch,
            visualBaseScale.y * yPunch,
            visualBaseScale.z * xzPunch
        );
    }

    private void HandleHit(RaycastHit hit)
    {
        // Ignore the owner kart so the rocket doesn't blow up at the muzzle.
        if (owner != null)
        {
            Transform t = hit.collider.transform;
            while (t != null)
            {
                if (t.gameObject == owner) return;
                t = t.parent;
            }
        }

        KartController hitKart = hit.collider.GetComponentInParent<KartController>();
        if (hitKart != null && hitKart.gameObject != owner)
        {
            KartPowerUser hitUser = hit.collider.GetComponentInParent<KartPowerUser>();
            if (hitUser != null && hitUser.IsShieldActive)
                hitUser.PulseShieldBlock(hit.point, explosionVFXPrefab);

            Explode(hit.point, hit.normal);
            return;
        }

        // Wall: bounce
        SpawnBoing(hit.point, hit.normal);

        velocityDir = Vector3.Reflect(velocityDir, hit.normal).normalized;
        // Nudge out of the surface to avoid re-colliding next frame
        transform.position = hit.point + hit.normal * (collisionRadius * 1.05f);
        speed *= bounceSpeedMultiplier;
        nextCollisionAllowedTime = Time.time + postBounceCooldown;

        // Reset wobble phase so bounce shows fresh oscillation
        wobblePhase += Mathf.PI * 0.5f;

        bouncesUsed++;
        if (bouncesUsed >= maxBounces)
        {
            Explode(transform.position, hit.normal);
        }
    }

    private void SpawnBoing(Vector3 position, Vector3 normal)
    {
        if (boingVFXPrefab == null) return;
        Quaternion rot = Quaternion.LookRotation(normal, Vector3.up);
        GameObject vfx = Instantiate(boingVFXPrefab, position + normal * 0.15f, rot);
        if (vfxScale > 0f && Mathf.Abs(vfxScale - 1f) > 0.001f)
            vfx.transform.localScale *= vfxScale;
    }

    private void Explode(Vector3 position, Vector3 normal)
    {
        if (exploded) return;
        exploded = true;

        if (explosionVFXPrefab != null)
        {
            Quaternion rot = Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.up, Vector3.up);
            GameObject vfx = Instantiate(explosionVFXPrefab, position, rot);
            if (vfxScale > 0f && Mathf.Abs(vfxScale - 1f) > 0.001f)
                vfx.transform.localScale *= vfxScale;
        }

        Destroy(gameObject);
    }
}
