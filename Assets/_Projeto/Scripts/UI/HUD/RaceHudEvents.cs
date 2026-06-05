using UnityEngine;

public enum RaceHudEventKind
{
    PowerCollected,
    PowerUsed,
    HitOpponent,
    GotHit,
    Nitro
}

public static class RaceHudEvents
{
    public readonly struct EventData
    {
        public EventData(GameObject actor, GameObject target, RaceHudEventKind kind, KartPowerType powerType)
        {
            Actor = actor;
            Target = target;
            Kind = kind;
            PowerType = powerType;
        }

        public GameObject Actor { get; }
        public GameObject Target { get; }
        public RaceHudEventKind Kind { get; }
        public KartPowerType PowerType { get; }
    }

    public static event System.Action<EventData> Raised;

    public static void Raise(GameObject actor, GameObject target, RaceHudEventKind kind, KartPowerType powerType = KartPowerType.None)
    {
        Raised?.Invoke(new EventData(actor, target, kind, powerType));
    }
}
