using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    public GameObject vfx_Hyperdrive_01;

    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponent<KartController>();

        if (kart != null)
        {
            // Ativa o efeito
            if (vfx_Hyperdrive_01 != null)
            {
                vfx_Hyperdrive_01.SetActive(true);
                Invoke(nameof(DisableEffect), 2f);
            }

            // Aplica o boost
            kart.ApplyBoost(
                2f,    // duração em segundos
                1.33f, // multiplicador de velocidade
                1.5f,  // multiplicador de aceleração
                5f     // empurrão instantâneo
            );
        }
    }

    void DisableEffect()
    {
        if (vfx_Hyperdrive_01 != null)
        {
            vfx_Hyperdrive_01.SetActive(false);
        }
    }
}