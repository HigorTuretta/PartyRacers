using UnityEngine;

public enum KartPowerType
{
    None,
    SwapPosition,
    Rocket,
    Shield,
    ElectricTrap
}

public class KartPowerInventory : MonoBehaviour
{
    [Header("Poder Atual")]
    [SerializeField] private KartPowerType currentPower = KartPowerType.None;

    public KartPowerType CurrentPower => currentPower;
    public bool HasPower => currentPower != KartPowerType.None;

    /// <summary>
    /// Disparado quando o poder muda por decisão LOCAL (coleta autoritativa ou uso). A camada de
    /// rede assina isto para replicar o estado — assim o inventário continua sem conhecer Netcode.
    /// Mudanças que já vieram da rede não disparam o evento (evita eco).
    /// </summary>
    public event System.Action<KartPowerType> PowerChangedLocally;

    public bool TryGivePower(KartPowerType powerType)
    {
        if (HasPower)
            return false;

        // Sem Debug.Log aqui: com 16 karts na pista isto disparava dezenas de vezes por segundo,
        // enterrava qualquer log de diagnóstico no console e o custo de capturar stack trace
        // aparecia no frame. O evento de HUD abaixo já é o canal oficial de feedback.
        currentPower = powerType;
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.PowerCollected, powerType);
        PowerChangedLocally?.Invoke(currentPower);

        return true;
    }

    public KartPowerType ConsumeCurrentPower()
    {
        KartPowerType consumedPower = currentPower;
        currentPower = KartPowerType.None;
        PowerChangedLocally?.Invoke(currentPower);

        return consumedPower;
    }

    public void ClearPower()
    {
        if (currentPower == KartPowerType.None)
            return;

        currentPower = KartPowerType.None;
        PowerChangedLocally?.Invoke(currentPower);
    }

    /// <summary>
    /// Aplica o poder que o SERVIDOR decidiu. Não dispara <see cref="PowerChangedLocally"/> — é a
    /// rede escrevendo no inventário, não o contrário. Mantém o feedback de HUD para que o dono do
    /// kart veja o item aparecer no slot mesmo quem sorteou tendo sido a outra máquina.
    /// </summary>
    public void ApplyNetworkPower(KartPowerType powerType)
    {
        if (currentPower == powerType)
            return;

        bool collected = currentPower == KartPowerType.None && powerType != KartPowerType.None;
        currentPower = powerType;

        if (collected)
            RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.PowerCollected, powerType);
    }

    public string GetPowerDisplayName()
    {
        return currentPower switch
        {
            KartPowerType.None => "Nenhum",
            KartPowerType.SwapPosition => "Swap Position",
            KartPowerType.Rocket => "Rocket",
            KartPowerType.Shield => "Shield",
            KartPowerType.ElectricTrap => "Armadilha Elétrica",
            _ => "Desconhecido"
        };
    }
}
