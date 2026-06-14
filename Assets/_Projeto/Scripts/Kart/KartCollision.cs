using UnityEngine;

[RequireComponent(typeof(KartController))]
[RequireComponent(typeof(Rigidbody))]
public class KartCollision : MonoBehaviour
{
    [Header("Detecção")]
    [Tooltip("LEGADO: resposta de parede própria deste componente. O KartController.HandleCollisionGlide " +
             "já faz o deslizamento arcade em paredes/rampas; com os dois ligados as duas respostas " +
             "disputavam o rb.linearVelocity no mesmo frame (ordem indefinida = batidas inconsistentes). " +
             "Mantenha desligado, a menos que esteja testando o comportamento antigo.")]
    [SerializeField] private bool enableLegacyWallResponse = false;
    [SerializeField] private float minImpactSpeed = 2.5f;
    [SerializeField] private float minWallNormalXZ = 0.85f;
    [Tooltip("Normal.y acima disso é tratado como CHÃO (não como parede). Evita travar em rampas/lombadas.")]
    [SerializeField, Range(0f, 1f)] private float maxGroundNormalY = 0.55f;

    [Header("Resposta ao Impacto")]
    [SerializeField, Range(0f, 1f)] private float speedRetentionAlongWall = 0.85f;
    [SerializeField, Range(0f, 1f)] private float bounceBackFraction = 0.15f;
    [SerializeField, Range(0f, 0.2f)] private float angularYSurvival = 0.03f;
    [SerializeField] private float maxUpwardVelocityAfterHit = 1.5f;

    [Header("Amortecimento Pós-Impacto")]
    [SerializeField] private float postHitDuration = 0.35f;
    [SerializeField] private float postHitAngularDamp = 12f;

    [Header("Ignorar Terreno (anti-travamento em rampas)")]
    [Tooltip("Raio (m) para pré-ignorar colisores de chão no Start(). 0 = desligado.")]
    [SerializeField] private bool enableGroundCollisionIgnore = false;
    [SerializeField] private float initialGroundScanRadius = 250f;
    [Tooltip("Distância à frente para detectar terreno em movimento e pré-ignorá-lo.")]
    [SerializeField] private float forwardScanDistance = 14f;
    [SerializeField] private float forwardScanRadius = 0.7f;
    [Tooltip("Velocidade mínima (m/s) para ativar a varredura à frente.")]
    [SerializeField] private float minVelocityForScan = 1.5f;

    private Rigidbody rb;
    private Collider[] kartColliders;
    private readonly System.Collections.Generic.HashSet<Collider> ignoredColliders = new();
    private float postHitTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        kartColliders = GetComponentsInChildren<Collider>();
        ApplyZeroBounceMaterial();
    }

    private void Start()
    {
        if (enableGroundCollisionIgnore && initialGroundScanRadius > 0f)
            IgnoreNearbyGroundColliders();
    }

    private void FixedUpdate()
    {
        if (postHitTimer > 0f)
        {
            postHitTimer -= Time.fixedDeltaTime;

            float yAngular = Mathf.Lerp(rb.angularVelocity.y, 0f, postHitAngularDamp * Time.fixedDeltaTime);
            rb.angularVelocity = new Vector3(rb.angularVelocity.x, yAngular, rb.angularVelocity.z);
        }

        if (enableGroundCollisionIgnore)
            ScanForwardForGround();
    }

    // -----------------------------------------------------------------------
    // PRÉ-IGNORE DE CHÃO
    // A engine física do Unity aplica a resposta de impulso ANTES do nosso
    // código rodar. Para evitar que o BoxCollider crashe contra rampas/lombadas
    // (resultando em queda brutal de velocidade), pré-cancelamos a colisão
    // entre o(s) collider(s) do kart e qualquer collider identificado como chão.
    // A suspensão usa SphereCast direto na engine de física, que NÃO respeita
    // IgnoreCollision — então o chão continua sendo detectado normalmente.
    // -----------------------------------------------------------------------

    private void IgnoreNearbyGroundColliders()
    {
        if (kartColliders == null || kartColliders.Length == 0) return;

        Collider[] nearby = Physics.OverlapSphere(transform.position, initialGroundScanRadius);

        foreach (var col in nearby)
        {
            if (col == null) continue;
            if (col.isTrigger) continue;
            if (System.Array.IndexOf(kartColliders, col) >= 0) continue;
            if (col.attachedRigidbody != null && !col.attachedRigidbody.isKinematic) continue;

            if (IsGroundLikeCollider(col))
                IgnoreCollider(col);
        }
    }

    private bool IsGroundLikeCollider(Collider col)
    {
        // Heurística: lança raio de cima pra baixo no centro do bounds.
        // Se acerta este mesmo collider e a normal é majoritariamente vertical, é chão.
        Vector3 origin = col.bounds.center + Vector3.up * 100f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.collider == col && hit.normal.y > maxGroundNormalY;
        }
        return false;
    }

    private void ScanForwardForGround()
    {
        if (kartColliders == null || kartColliders.Length == 0) return;
        if (rb.linearVelocity.sqrMagnitude < minVelocityForScan * minVelocityForScan) return;

        Vector3 origin = transform.position + transform.up * 0.4f;
        Vector3 castDir = rb.linearVelocity.normalized;

        if (Physics.SphereCast(origin, forwardScanRadius, castDir, out RaycastHit hit,
            forwardScanDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.isTrigger
                && hit.normal.y > maxGroundNormalY
                && System.Array.IndexOf(kartColliders, hit.collider) < 0)
            {
                IgnoreCollider(hit.collider);
            }
        }
    }

    private void IgnoreCollider(Collider other)
    {
        if (other == null)
            return;

        ignoredColliders.Add(other);

        for (int i = 0; i < kartColliders.Length; i++)
        {
            if (kartColliders[i] != null)
                Physics.IgnoreCollision(kartColliders[i], other, true);
        }
    }

    public void ResetIgnoredCollisionState()
    {
        if (kartColliders == null || kartColliders.Length == 0 || ignoredColliders.Count == 0)
            return;

        foreach (Collider other in ignoredColliders)
        {
            if (other == null)
                continue;

            for (int i = 0; i < kartColliders.Length; i++)
            {
                if (kartColliders[i] != null)
                    Physics.IgnoreCollision(kartColliders[i], other, false);
            }
        }

        ignoredColliders.Clear();
    }

    // -----------------------------------------------------------------------
    // COLISÃO
    // -----------------------------------------------------------------------

    // Contato kart-a-kart é tratado como "esbarrão arcade" pelo KartController.HandleCollisionGlide.
    // Aqui ignoramos para não aplicar resposta de parede em dobro (que travava os carros secamente).
    private static bool IsAnotherKart(Collision col)
        => col.rigidbody != null && col.rigidbody.GetComponent<KartController>() != null;

    private void OnCollisionEnter(Collision col)
    {
        if (!enableLegacyWallResponse) return;
        if (col.contactCount == 0) return;
        if (IsAnotherKart(col)) return;

        // Se qualquer contato e chao, deixa a fisica normal resolver sem aplicar resposta de parede.
        for (int i = 0; i < col.contactCount; i++)
        {
            if (col.contacts[i].normal.y > maxGroundNormalY)
            {
                return;
            }
        }

        float impactSpeed = col.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;
        if (!TryGetWallNormal(col, out Vector3 wallNormal)) return;

        ApplyWallResponse(wallNormal);
        postHitTimer = postHitDuration;
    }

    private void OnCollisionStay(Collision col)
    {
        if (!enableLegacyWallResponse) return;
        if (col.contactCount == 0) return;
        if (IsAnotherKart(col)) return;
        if (!TryGetWallNormal(col, out Vector3 wallNormal)) return;

        // Remove velocidade que ainda esteja empurrando o kart pra dentro da parede
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float dot = Vector3.Dot(flatVel, wallNormal);

        if (dot < 0f)
        {
            Vector3 correction = wallNormal * dot;
            rb.linearVelocity -= new Vector3(correction.x, 0f, correction.z);
        }

        float yAngular = rb.angularVelocity.y * Mathf.Max(0f, 1f - 18f * Time.fixedDeltaTime);
        rb.angularVelocity = new Vector3(rb.angularVelocity.x, yAngular, rb.angularVelocity.z);
    }

    private bool TryGetWallNormal(Collision col, out Vector3 wallNormal)
    {
        Vector3 best = Vector3.zero;
        float bestXZ = 0f;

        for (int i = 0; i < col.contactCount; i++)
        {
            Vector3 n = col.contacts[i].normal;

            if (n.y > maxGroundNormalY) continue;

            float xz = new Vector2(n.x, n.z).magnitude;
            if (xz > bestXZ)
            {
                bestXZ = xz;
                best = n;
            }
        }

        if (bestXZ < minWallNormalXZ)
        {
            wallNormal = Vector3.zero;
            return false;
        }

        wallNormal = new Vector3(best.x, 0f, best.z).normalized;
        return true;
    }

    private void ApplyWallResponse(Vector3 wallNormal)
    {
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float dotIntoWall = Vector3.Dot(flatVelocity, wallNormal);

        if (dotIntoWall >= 0f) return;

        Vector3 perpComponent = wallNormal * dotIntoWall;
        Vector3 paraComponent = flatVelocity - perpComponent;

        Vector3 newFlatVelocity = paraComponent * speedRetentionAlongWall
                                + (-perpComponent) * bounceBackFraction;

        float yVel = Mathf.Min(rb.linearVelocity.y, maxUpwardVelocityAfterHit);
        rb.linearVelocity = new Vector3(newFlatVelocity.x, yVel, newFlatVelocity.z);

        rb.angularVelocity = new Vector3(
            rb.angularVelocity.x,
            rb.angularVelocity.y * angularYSurvival,
            rb.angularVelocity.z
        );
    }

    private void ApplyZeroBounceMaterial()
    {
        var mat = new PhysicsMaterial("KartImpact")
        {
            bounciness = 0f,
            dynamicFriction = 0f,
            staticFriction = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Multiply,
        };

        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            if (!col.isTrigger)
                col.material = mat;
        }
    }
}
