using UnityEngine;
using System.Collections;

public class GolfTrapLauncher : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform suporteBola;
    public Transform direcaoTacada;

    public float launchForce = 20f;
    public float waitBeforeShoot = 3f;
    public float ballLifetime = 5f;

    private GameObject currentBall;

    void Start()
    {
        StartCoroutine(BallLoop());
    }

    IEnumerator BallLoop()
    {
        while (true)
        {
            // Cria a bola
            currentBall = Instantiate(
                golfBallPrefab,
                suporteBola.position + Vector3.up * 5f,
                suporteBola.rotation);

            Rigidbody rb = currentBall.GetComponent<Rigidbody>();

            if (rb != null)
            {
                rb.isKinematic = true;
            }

            // Espera antes de lançar
            yield return new WaitForSeconds(waitBeforeShoot);

            // Lança a bola
            if (rb != null)
            {
                rb.isKinematic = false;

                rb.AddForce(
                    direcaoTacada.forward * launchForce,
                    ForceMode.Impulse);
            }

            // Espera a bola existir
            yield return new WaitForSeconds(ballLifetime);

            // Destrói a bola
            Destroy(currentBall);
        }
    }
}