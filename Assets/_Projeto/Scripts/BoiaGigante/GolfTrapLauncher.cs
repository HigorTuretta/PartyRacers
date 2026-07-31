using UnityEngine;
using System.Collections;

public class GolfTrapLauncher : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform suporteBola;
    public Transform direcaoTacada;

    public float launchForce = 20f;

    [Header("Configuração de Spawn")]
    public float spawnInterval = 4f;
    public float startDelay = 0f;
    public float ballLifetime = 5f;

    void Start()
    {
        // Tenta encontrar as referências automaticamente
        if (suporteBola == null)
            suporteBola = transform.Find("SuporteBola");

        if (direcaoTacada == null)
            direcaoTacada = suporteBola != null ? suporteBola : transform;

        if (golfBallPrefab == null || suporteBola == null)
        {
            Debug.LogWarning($"[GolfTrapLauncher] '{name}' sem 'golfBallPrefab' ou 'suporteBola' — armadilha desativada.", this);
            enabled = false;
            return;
        }

        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        // Espera o tempo configurado antes de iniciar
        if (startDelay > 0f)
            yield return new WaitForSeconds(startDelay);

        while (true)
        {
            SpawnBall();
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    void SpawnBall()
    {
        GameObject ball = Instantiate(
            golfBallPrefab,
            suporteBola.position + Vector3.up * 6.5f,
            suporteBola.rotation
        );

        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // VelocityChange (e não Impulse): 'launchForce' passa a ser a velocidade de saída em
            // m/s, independente da massa da bola. Com Impulse, deixar a bola mais pesada
            // (GolfBallKartImpact calcula a massa pelo raio) fazia o lançamento sair devagar.
            rb.AddForce(
                direcaoTacada.forward * launchForce,
                ForceMode.VelocityChange
            );
        }

        Destroy(ball, ballLifetime);
    }
}