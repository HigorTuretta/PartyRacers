using UnityEngine;

public class CrossbowShooter : MonoBehaviour
{
    [Header("Flecha")]
    public GameObject arrowPrefab;
    public Transform spawnPoint;
    public CrossbowArrowAnimation arrowAnimation;

    [Header("Disparo")]
    public float arrowSpeed = 40f;
    public float shootInterval = 3f;
    public float arrowLifetime = 5f;

    [Header("Efeito Disparo")]
    public GameObject shootEffectPrefab;
    public float shootEffectScale = 2f;
    public float effectLifetime = 2f;

    [Header("Impacto da Flecha")]
    public GameObject impactPrefab;
    public float impactScale = 1f;
    public float impactLifetime = 2f;

    [Header("Empurrão")]
    public float pushForce = 6f;

    private AudioSource audioSource;


    private void Start()
    {
        audioSource = GetComponent<AudioSource>();

        InvokeRepeating(nameof(Fire), 0f, shootInterval);
    }


    private void Fire()
    {
        // Anima a flecha visual da besta
        if (arrowAnimation != null)
            arrowAnimation.ReleaseArrow();


        // Som da besta
        if (audioSource != null)
            audioSource.Play();


        // Efeito de disparo
        if (shootEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                shootEffectPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );

            effect.transform.localScale = Vector3.one * shootEffectScale;

            Destroy(effect, effectLifetime);
        }


        // Criar flecha
        GameObject arrow = Instantiate(
            arrowPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );


        Rigidbody rb = arrow.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = spawnPoint.forward * arrowSpeed;
        }


        // Adiciona comportamento de impacto
        ArrowCollision arrowCollision = arrow.AddComponent<ArrowCollision>();

        arrowCollision.impactPrefab = impactPrefab;
        arrowCollision.impactScale = impactScale;
        arrowCollision.impactLifetime = impactLifetime;
        arrowCollision.pushForce = pushForce;


        Destroy(arrow, arrowLifetime);
    }
}



public class ArrowCollision : MonoBehaviour
{
    public GameObject impactPrefab;
    public float impactScale = 1f;
    public float impactLifetime = 2f;

    public float pushForce = 6f;

    private bool hit = false;



    private void OnCollisionEnter(Collision collision)
    {
        if (hit)
            return;

        hit = true;


        // Ponto exato da colisão
        ContactPoint contact = collision.contacts[0];


        // Criar efeito de impacto
        if (impactPrefab != null)
        {
            GameObject impact = Instantiate(
                impactPrefab,
                contact.point,
                Quaternion.LookRotation(contact.normal)
            );


            impact.transform.localScale = Vector3.one * impactScale;


            Destroy(impact, impactLifetime);
        }



        // Empurrar carro ou objeto
        Rigidbody rb = collision.collider.GetComponentInParent<Rigidbody>();

        if (rb != null)
        {
            Vector3 direction = transform.forward;

            direction.y = 0f;
            direction.Normalize();


            rb.AddForce(
                direction * pushForce,
                ForceMode.Impulse
            );
        }



        Destroy(gameObject);
    }
}