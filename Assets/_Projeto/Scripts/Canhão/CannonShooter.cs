using UnityEngine;

public class CannonShooter : MonoBehaviour
{
    public GameObject golfBallPrefab;
    public Transform spawnPoint;
    public float launchForce = 30f;
    public float shootInterval = 5f;
    public float ballLifetime = 5f;

    void Start()
    {
        InvokeRepeating(nameof(Fire), 0f, shootInterval);
    }

    void Fire()
    {
        GameObject ball = Instantiate(
            golfBallPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Rigidbody rb = ball.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(
                spawnPoint.forward * launchForce,
                ForceMode.Impulse
            );
        }

        Destroy(ball, ballLifetime);
    }
}