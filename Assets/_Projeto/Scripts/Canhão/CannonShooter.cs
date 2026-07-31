using UnityEngine;

public class CannonShooter : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public GameObject muzzleEffectPrefab;

    public Transform spawnPoint;

    public float launchForce = 30f;
    public float shootInterval = 5f;
    public float ballLifetime = 5f;

    // Configurações da explosão
    public float effectScale = 5f;
    public float effectLifetime = 3f;
    public float effectOffset = 0.5f;

    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();

        InvokeRepeating(nameof(Fire), 0f, shootInterval);
    }

    void Fire()
    {
        // Toca o som do disparo
        if (audioSource != null)
        {
            audioSource.Play();
        }

        // Efeito visual do disparo
        if (muzzleEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                muzzleEffectPrefab,
                spawnPoint.position + spawnPoint.forward * effectOffset,
                Quaternion.identity
            );

            effect.transform.localScale = Vector3.one * effectScale;

            Destroy(effect, effectLifetime);
        }

        // Cria a bola
        GameObject ball = Instantiate(
            golfBallPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        // Aplica força
        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // VelocityChange (e não Impulse): 'launchForce' passa a ser a velocidade de saída em
            // m/s, independente da massa da bola. Com Impulse, deixar a bola mais pesada
            // (GolfBallKartImpact calcula a massa pelo raio) fazia o tiro sair devagar.
            rb.AddForce(
                spawnPoint.forward * launchForce,
                ForceMode.VelocityChange
            );
        }

        // Destroi a bola após alguns segundos
        Destroy(ball, ballLifetime);
    }
}