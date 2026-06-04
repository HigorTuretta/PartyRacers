using UnityEngine;
using System.Collections;

public class GolfTrapLauncher : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform suporteBola;

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
            suporteBola.position,
            suporteBola.rotation);

        Rigidbody rb = currentBall.GetComponent<Rigidbody>();

        if (rb != null)
        {
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
            rb.isKinematic = false;

            // MUDE O EIXO AQUI
            rb.AddForce(
                Vector3.forward * launchForce,
                ForceMode.Impulse);
        }

        Destroy(currentBall, ballLifetime);

        SpawnBall();
    }
}