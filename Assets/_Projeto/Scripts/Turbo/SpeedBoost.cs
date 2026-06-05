using UnityEngine;

public class SpeedBoost : MonoBehaviour
{
    [Header("Boost")]
    [SerializeField] private float boostDuration = 2f;
    [SerializeField] private float speedMultiplier = 1.33f;
    [SerializeField] private float accelerationMultiplier = 1.5f;
    [SerializeField] private float instantPush = 5f;

    [Header("VFX legado (compartilhado)")]
    [Tooltip("Efeito de tela ANTIGO, num objeto único da cena. Ativá-lo acendia para TODOS os " +
             "jogadores (vazamento). O efeito de turbo agora é local por dono via " +
             "KartTurboScreenEffect (Screen wind.prefab). Mantido desligado por compatibilidade.")]
    [SerializeField] private bool useLegacyScreenVfx = false;
    [SerializeField] private GameObject vfx_Hyperdrive_01;

    private void OnTriggerEnter(Collider other)
    {
        KartController kart = other.GetComponentInParent<KartController>();

        if (kart == null)
            return;

        // Aplica o boost (gameplay) — roda no cliente que detecta o trigger.
        kart.ApplyBoost(boostDuration, speedMultiplier, accelerationMultiplier, instantPush);

        // VFX legado só se explicitamente reativado E apenas para o dono local do kart,
        // evitando o vazamento de tela para os outros jogadores.
        if (useLegacyScreenVfx && vfx_Hyperdrive_01 != null && IsLocalKart(kart))
        {
            vfx_Hyperdrive_01.SetActive(true);
            CancelInvoke(nameof(DisableEffect));
            Invoke(nameof(DisableEffect), boostDuration);
        }
    }

    private static bool IsLocalKart(KartController kart)
    {
        KartLocalRig rig = kart.GetComponent<KartLocalRig>();
        return rig == null || rig.IsLocalPlayer;
    }

    private void DisableEffect()
    {
        if (vfx_Hyperdrive_01 != null)
            vfx_Hyperdrive_01.SetActive(false);
    }
}
