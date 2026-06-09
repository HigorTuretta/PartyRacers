using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    [Header("Visual")]
    public GameObject ScreenWind;

    [Header("Boost Settings")]
    [Tooltip("Duração do boost em segundos")]
    public float boostDuration = 2f;

    [Tooltip("Multiplicador de velocidade")]
    public float speedMultiplier = 1.33f;

    [Tooltip("Multiplicador de aceleração")]
    public float accelerationMultiplier = 1.5f;

    [Tooltip("Impulso instantâneo")]
    public float instantPush = 5f;

    [Header("Preview")]
    [Tooltip("Velocidade máxima normal do kart")]
    public float baseKartSpeed = 150f;

    [Tooltip("Velocidade final durante o boost")]
    public float finalSpeed;

    private void OnValidate()
    {
        finalSpeed = baseKartSpeed * speedMultiplier;
    }

    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponent<KartController>();

        if (kart != null)
        {
            if (ScreenWind != null)
            {
                ScreenWind.SetActive(true);
                CancelInvoke(nameof(DisableEffect));
                Invoke(nameof(DisableEffect), boostDuration);
            }

            kart.ApplyBoost(
                boostDuration,
                speedMultiplier,
                accelerationMultiplier,
                instantPush
            );
        }
    }

    private void DisableEffect()
    {
        if (ScreenWind != null)
        {
            ScreenWind.SetActive(false);
        }
    }
}