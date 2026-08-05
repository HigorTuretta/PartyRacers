using UnityEngine;
using System.Collections;

public class TowerBombLauncher : MonoBehaviour
{
    [Header("Disparo")]
    public GameObject bombPrefab;
    public Transform firePoint;
    public float shootForce = 18f;
    public float fireRate = 2f;

    [Header("Efeito")]
    public GameObject shootEffectPrefab;

    [Header("Rotação")]
    public float rotationAngle = 15f;
    public float rotationSpeed = 50f;

    private bool shootRight = true;
    private float initialY;

    void Start()
    {
        // Guarda a rotação inicial da torre
        initialY = transform.localEulerAngles.y;

        StartCoroutine(FireRoutine());
    }

    IEnumerator FireRoutine()
    {
        while (true)
        {
            float targetAngle = initialY + (shootRight ? rotationAngle : -rotationAngle);

            while (Mathf.Abs(Mathf.DeltaAngle(transform.localEulerAngles.y, targetAngle)) > 0.5f)
            {
                float angle = Mathf.MoveTowardsAngle(
                    transform.localEulerAngles.y,
                    targetAngle,
                    rotationSpeed * Time.deltaTime);

                transform.localEulerAngles = new Vector3(
                    transform.localEulerAngles.x,
                    angle,
                    transform.localEulerAngles.z);

                yield return null;
            }

            Shoot();

            shootRight = !shootRight;

            yield return new WaitForSeconds(fireRate);
        }
    }

    void Shoot()
    {
        // Cria o efeito de disparo
        if (shootEffectPrefab != null)
        {
            GameObject effect = Instantiate(
                shootEffectPrefab,
                firePoint.position,
                firePoint.rotation);

            Destroy(effect, 2f);
        }

        // Cria a bomba
        GameObject bomb = Instantiate(
            bombPrefab,
            firePoint.position,
            firePoint.rotation);

        // Aplica força
        Rigidbody rb = bomb.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.AddForce(firePoint.forward * shootForce, ForceMode.Impulse);
        }
    }
}