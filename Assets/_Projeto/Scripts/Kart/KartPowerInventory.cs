using UnityEngine;

public enum KartPowerType
{
    None,
    SwapPosition,
    Rocket,
    Shield
}

public class KartPowerInventory : MonoBehaviour
{
    [Header("Poder Atual")]
    [SerializeField] private KartPowerType currentPower = KartPowerType.None;

    public KartPowerType CurrentPower => currentPower;
    public bool HasPower => currentPower != KartPowerType.None;

    public bool TryGivePower(KartPowerType powerType)
    {
        if (HasPower)
            return false;

        // Sem Debug.Log aqui: com 16 karts na pista isto disparava dezenas de vezes por segundo,
        // enterrava qualquer log de diagnóstico no console e o custo de capturar stack trace
        // aparecia no frame. O evento de HUD abaixo já é o canal oficial de feedback.
        currentPower = powerType;
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.PowerCollected, powerType);

        return true;
    }

    public KartPowerType ConsumeCurrentPower()
    {
        KartPowerType consumedPower = currentPower;
        currentPower = KartPowerType.None;

        return consumedPower;
    }

    public void ClearPower()
    {
        currentPower = KartPowerType.None;
    }

    public string GetPowerDisplayName()
    {
        return currentPower switch
        {
            KartPowerType.None => "Nenhum",
            KartPowerType.SwapPosition => "Swap Position",
            KartPowerType.Rocket => "Rocket",
            KartPowerType.Shield => "Shield",
            _ => "Desconhecido"
        };
    }
}
