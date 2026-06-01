using UnityEngine;

public enum KartPowerType
{
    None,
    SwapPosition,
    StunShot,
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

        currentPower = powerType;
        Debug.Log($"Poder recebido: {GetPowerDisplayName()}");

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
            KartPowerType.SwapPosition => "Troca",
            KartPowerType.StunShot => "Bolha Eletro-Gel",
            KartPowerType.Shield => "Escudo",
            _ => "Desconhecido"
        };
    }
}
