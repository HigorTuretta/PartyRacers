using UnityEngine;

public class CannonBallEffect : MonoBehaviour
{
    [Header("Explosão")]
    public GameObject explosionPrefab;
    public float explosionScale = 2f;
    public float explosionLifetime = 3f;

    [Header("Impacto")]
    public float pushForce = 10f;

    private bool exploded = false;

    private void OnCollisionEnter(Collision collision)
    {
        if (exploded)
            return;

        exploded = true;

        // Ponto exato da colisão
        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;

        // Cria a explosão
        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(
                explosionPrefab,
                hitPoint,
                Quaternion.identity);

            explosion.transform.localScale = Vector3.one * explosionScale;

            Destroy(explosion, explosionLifetime);
        }

        // Procura um Rigidbody no objeto atingido ou em seus pais
        Rigidbody rb = collision.collider.GetComponentInParent<Rigidbody>();

        if (rb != null)
        {
            Vector3 direction = (rb.worldCenterOfMass - hitPoint).normalized;
            direction.y = 0f;
            direction.Normalize();

            rb.AddForce(direction * pushForce, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }
}