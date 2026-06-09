using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    public GameObject ScreenWind;

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

        kart.ApplyBoost(
            2f,
            1.33f,
            1.5f,
            5f
        );
    }
}
