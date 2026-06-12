using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GolfTrapLauncher : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform suporteBola;
    public Transform direcaoTacada;

    public float launchForce = 20f;
    public float spawnInterval = 2f;   // 🔥 nova bola a cada 2s
    public float ballLifetime = 5f;

    void Start()
    {
        // Referências ausentes derrubavam a coroutine com exceção a cada spawn.
        // Tenta resolver pelos filhos antes de desistir, e desliga com aviso claro se faltar algo.
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

            rb.AddForce(
                direcaoTacada.forward * launchForce,
                ForceMode.Impulse
            );
        }

        // destrói depois do tempo de vida
        Destroy(ball, ballLifetime);
    }
}