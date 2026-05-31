using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponent<KartController>();

        if (kart != null)
        {
            kart.ApplyBoost(
                2f,    // duração em segundos
                1.33f, // multiplicador de velocidade
                1.5f,  // multiplicador de aceleração
                5f     // empurrão instantâneo
            );
        }
    }
}