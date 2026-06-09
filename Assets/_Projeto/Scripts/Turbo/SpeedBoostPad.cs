using UnityEngine;

public class SpeedBoostPad : MonoBehaviour
{
    [Header("Visual")]
    public GameObject ScreenWind;

    [Header("Boost Settings")]
    [Tooltip("Duracao do boost em segundos")]
    public float boostDuration = 2f;

    [Tooltip("Multiplicador de velocidade")]
    public float speedMultiplier = 1.33f;

    [Tooltip("Multiplicador de aceleracao")]
    public float accelerationMultiplier = 1.5f;

    [Tooltip("Impulso instantaneo")]
    public float instantPush = 5f;

    [Header("Preview")]
    [Tooltip("Velocidade maxima normal do kart")]
    public float baseKartSpeed = 150f;

    [Tooltip("Velocidade final durante o boost")]
    public float finalSpeed;

    private void OnValidate()
    {
        finalSpeed = baseKartSpeed * speedMultiplier;
    }

    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponentInParent<KartController>();
        if (kart == null)
            return;

        if (ScreenWind != null)
            ScreenWind.SetActive(false);

        KartTurboScreenEffect effect = kart.GetComponent<KartTurboScreenEffect>();
        if (effect == null)
            effect = kart.gameObject.AddComponent<KartTurboScreenEffect>();

        if (ScreenWind != null)
            effect.SetScreenWindPrefab(ScreenWind);

        kart.ApplyBoost(
            boostDuration,
            speedMultiplier,
            accelerationMultiplier,
            instantPush
        );
    }
}