using UnityEngine;

public enum RaceHudEventKind
{
    PowerCollected,
    PowerUsed,
    HitOpponent,
    GotHit,
    Nitro,

    // --- Sistema de vida (handoff v2 §5) --------------------------------------------------
    /// <summary>O kart perdeu HP. <c>Amount</c> traz quanto.</summary>
    Damaged,
    /// <summary>O kart recuperou HP (caixa de cura). <c>Amount</c> traz quanto.</summary>
    Healed,
    /// <summary>HP chegou a zero: começou o estado danificado.</summary>
    Broken,
    /// <summary>Fim do estado danificado, vida restaurada.</summary>
    Repaired,

    // --- Escudo (habilidade fixa) ---------------------------------------------------------
    ShieldActivated,
    ShieldBlocked,
    ShieldReady,
}

public static class RaceHudEvents
{
    public readonly struct EventData
    {
        public EventData(GameObject actor, GameObject target, RaceHudEventKind kind, KartPowerType powerType, float amount)
        {
            Actor = actor;
            Target = target;
            Kind = kind;
            PowerType = powerType;
            Amount = amount;
        }

        public GameObject Actor { get; }
        public GameObject Target { get; }
        public RaceHudEventKind Kind { get; }
        public KartPowerType PowerType { get; }

        /// <summary>Quantidade associada ao evento (HP perdido/recuperado). 0 quando não se aplica.</summary>
        public float Amount { get; }
    }

    public static event System.Action<EventData> Raised;

    public static void Raise(GameObject actor, GameObject target, RaceHudEventKind kind,
        KartPowerType powerType = KartPowerType.None, float amount = 0f)
    {
        Raised?.Invoke(new EventData(actor, target, kind, powerType, amount));
    }
}
