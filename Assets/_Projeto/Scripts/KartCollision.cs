using UnityEngine;

[RequireComponent(typeof(KartController))]
[RequireComponent(typeof(Rigidbody))]
public class KartCollision : MonoBehaviour
{
    [Header("Detecção")]
    [SerializeField] private float minImpactSpeed = 2.5f;
    [SerializeField] private float minWallNormalXZ = 0.45f;

    [Header("Resposta ao Impacto")]
    [SerializeField, Range(0f, 1f)] private float speedRetentionAlongWall = 0.70f;
    [SerializeField, Range(0f, 1f)] private float bounceBackFraction = 0.28f;
    [SerializeField, Range(0f, 0.2f)] private float angularYSurvival = 0.03f;
    // Velocidade Y máxima que pode resultar de uma batida em parede (evita ser lançado pra cima)
    [SerializeField] private float maxUpwardVelocityAfterHit = 1.5f;

    [Header("Amortecimento Pós-Impacto")]
    [SerializeField] private float postHitDuration = 0.35f;
    [SerializeField] private float postHitAngularDamp = 12f;

    private Rigidbody rb;
    private float postHitTimer;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        ApplyZeroBounceMaterial();
    }

    private void FixedUpdate()
    {
        if (postHitTimer <= 0f)
            return;

        postHitTimer -= Time.fixedDeltaTime;

        float yAngular = Mathf.Lerp(rb.angularVelocity.y, 0f, postHitAngularDamp * Time.fixedDeltaTime);
        rb.angularVelocity = new Vector3(rb.angularVelocity.x, yAngular, rb.angularVelocity.z);
    }

    private void OnCollisionEnter(Collision col)
    {
        if (col.contactCount == 0)
            return;

        float impactSpeed = col.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed)
            return;

        if (!TryGetWallNormal(col, out Vector3 wallNormal))
            return;

        ApplyWallResponse(wallNormal);

        postHitTimer = postHitDuration;
    }

    private void OnCollisionStay(Collision col)
    {
        if (col.contactCount == 0)
            return;

        if (!TryGetWallNormal(col, out Vector3 wallNormal))
            return;

        // Remove qualquer velocidade que ainda esteja empurrando o kart para dentro da parede
        Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float dot = Vector3.Dot(flatVel, wallNormal);

        if (dot < 0f)
        {
            Vector3 correction = wallNormal * dot;
            rb.linearVelocity -= new Vector3(correction.x, 0f, correction.z);
        }

        // Mantém o spin Y amortecido durante o contato contínuo
        float yAngular = rb.angularVelocity.y * Mathf.Max(0f, 1f - 18f * Time.fixedDeltaTime);
        rb.angularVelocity = new Vector3(rb.angularVelocity.x, yAngular, rb.angularVelocity.z);
    }

    private bool TryGetWallNormal(Collision col, out Vector3 wallNormal)
    {
        Vector3 surfaceNormal = col.contacts[0].normal;
        float xzMagnitude = new Vector2(surfaceNormal.x, surfaceNormal.z).magnitude;

        if (xzMagnitude < minWallNormalXZ)
        {
            wallNormal = Vector3.zero;
            return false;
        }

        wallNormal = new Vector3(surfaceNormal.x, 0f, surfaceNormal.z).normalized;
        return true;
    }

    private void ApplyWallResponse(Vector3 wallNormal)
    {
        Vector3 flatVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float dotIntoWall = Vector3.Dot(flatVelocity, wallNormal);

        if (dotIntoWall >= 0f)
            return;

        Vector3 perpComponent = wallNormal * dotIntoWall;
        Vector3 paraComponent = flatVelocity - perpComponent;

        Vector3 newFlatVelocity = paraComponent * speedRetentionAlongWall
                                + (-perpComponent) * bounceBackFraction;

        // Clamp Y: a batida na parede não deve lançar o kart pra cima
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
            // Multiply garante que 0 * qualquer_coisa = 0, mesmo se a parede tiver bounceCombine=Maximum
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
