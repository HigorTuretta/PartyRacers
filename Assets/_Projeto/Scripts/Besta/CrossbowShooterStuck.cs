using UnityEngine;
using System.Collections;

public class CrossbowShooterStuck : MonoBehaviour
{
    [Header("Flecha")]
    public GameObject arrowPrefab;
    public Transform spawnPoint;
    public CrossbowArrowAnimation arrowAnimation;

    [Header("Disparo")]
    public float arrowSpeed = 40f;
    public float shootInterval = 2f;
    public float stuckTime = 3f;

    [Header("Efeito Disparo")]
    public GameObject shootEffectPrefab;
    public float shootEffectScale = 2f;
    public float effectLifetime = 2f;

    [Header("Impacto")]
    public GameObject impactPrefab;
    public float impactScale = 1f;
    public float impactLifetime = 2f;

    [Header("Empurrão")]
    public float pushForce = 6f;

    private AudioSource audioSource;
    private GameObject currentArrow;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        while (true)
        {
            Fire();

            while (currentArrow != null)
                yield return null;

            yield return new WaitForSeconds(shootInterval);
        }
    }

    void Fire()
    {
        if (arrowAnimation != null)
            arrowAnimation.ReleaseArrow();

        if (audioSource != null)
            audioSource.Play();

        if (shootEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                shootEffectPrefab,
                spawnPoint.position,
                spawnPoint.rotation);

            effect.transform.localScale = Vector3.one * shootEffectScale;

            Destroy(effect, effectLifetime);
        }

        currentArrow = Instantiate(
            arrowPrefab,
            spawnPoint.position,
            spawnPoint.rotation);

        Rigidbody rb = currentArrow.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = spawnPoint.forward * arrowSpeed;
        }

        ArrowCollisionStuck collision = currentArrow.AddComponent<ArrowCollisionStuck>();

        collision.owner = this;
        collision.stuckTime = stuckTime;
        collision.impactPrefab = impactPrefab;
        collision.impactScale = impactScale;
        collision.impactLifetime = impactLifetime;
        collision.pushForce = pushForce;
    }

    public void ArrowDestroyed()
    {
        currentArrow = null;
    }
}
public class ArrowCollisionStuck : MonoBehaviour
{
    public CrossbowShooterStuck owner;

    public float stuckTime = 3f;

    public GameObject impactPrefab;
    public float impactScale = 1f;
    public float impactLifetime = 2f;

    public float pushForce = 6f;

    private bool hit = false;

    void OnCollisionEnter(Collision collision)
    {
        if (hit)
            return;

        hit = true;

        ContactPoint contact = collision.contacts[0];

        // Efeito de impacto
        if (impactPrefab != null)
        {
            GameObject impact = Instantiate(
                impactPrefab,
                contact.point,
                Quaternion.LookRotation(contact.normal));

            impact.transform.localScale = Vector3.one * impactScale;

            Destroy(impact, impactLifetime);
        }

        // Se acertou um objeto com Rigidbody (carro), empurra e destrói
        Rigidbody hitRb = collision.collider.GetComponentInParent<Rigidbody>();

        if (hitRb != null)
        {
            Vector3 dir = transform.forward;
            dir.y = 0f;
            dir.Normalize();

            hitRb.AddForce(dir * pushForce, ForceMode.Impulse);

            if (owner != null)
                owner.ArrowDestroyed();

            Destroy(gameObject);
            return;
        }

        // Cenário: fincar a flecha

        transform.position = contact.point;

        Rigidbody rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        // Mantém o collider ativo para os carros baterem na flecha
        Collider col = GetComponent<Collider>();

        if (col != null)
        {
            col.enabled = true;
        }

        StartCoroutine(DestroyLater());
    }

    IEnumerator DestroyLater()
    {
        yield return new WaitForSeconds(stuckTime);

        if (owner != null)
            owner.ArrowDestroyed();

        Destroy(gameObject);
    }
}