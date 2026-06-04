using UnityEngine;

public class GolfTrapLauncher : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform suporteBola;
    public Transform direcaoTacada;

    public float launchForce = 20f;
    public float respawnTime = 3f;
    public float ballLifetime = 5f;

    private GameObject currentBall;

    void Start()
    {
        SpawnBall();

        InvokeRepeating(nameof(HitBall), respawnTime, respawnTime);
    }

    void SpawnBall()
    {
        currentBall = Instantiate(
            golfBallPrefab,
            suporteBola.position + Vector3.up * 2f,
            suporteBola.rotation);

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Mantém a bola parada sobre o suporte
            rb.isKinematic = true;
        }
    }

    void HitBall()
    {
        if (currentBall == null)
            return;

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();

        if (rb != null)
        {
            // Libera a física
            rb.isKinematic = false;

            // Aplica a força na direção do objeto DirecaoTacada
            rb.AddForce(
                direcaoTacada.forward * launchForce,
                ForceMode.Impulse);
        }

        // Guarda referência da bola lançada
        GameObject launchedBall = currentBall;

        // Cria imediatamente uma nova bola em cima do suporte
        SpawnBall();

        // Destrói a bola lançada após alguns segundos
        Destroy(launchedBall, ballLifetime);
    }
}