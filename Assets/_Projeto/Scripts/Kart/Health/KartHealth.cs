using System.Collections.Generic;
using UnityEngine;

/// <summary>De onde veio o dano. Define a regra aplicada e a cor/forma que a HUD usa.</summary>
public enum KartDamageKind
{
    /// <summary>Parede ou elemento do cenário. Escala por velocidade. O escudo NÃO protege.</summary>
    Wall,

    /// <summary>Armadilha ou obstáculo do mapa (moinho, taco, canhão). O escudo protege.</summary>
    Trap,

    /// <summary>Item disparado por outro jogador. O escudo protege.</summary>
    Item,
}

/// <summary>
/// Vida do kart (handoff v2 §5). HP 100, dano por faixa de velocidade em paredes, 10 de armadilha,
/// 15 de item, cooldown de 0,75 s entre danos e estado DANIFICADO de 2,5 s ao zerar — com
/// penalidade de velocidade, aceleração e esterço, seguida de recuperação automática para 100.
///
/// O componente não desenha nada: expõe estado e eventos, e a HUD lê. Também não conhece rede: as
/// fontes de dano já decidem sozinhas quem aplica (a ItemBox e os projéteis passam pelo
/// RaceAuthority); aqui a simulação é local em cada máquina, como a do resto da física arcade.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KartController))]
public class KartHealth : MonoBehaviour
{
    /// <summary>Faixa de velocidade → dano. Bate com <c>tokens-v2.json → gameplay.danoParede</c>.</summary>
    [System.Serializable]
    public struct WallDamageBand
    {
        [Tooltip("Velocidade mínima da faixa, em km/h (inclusive).")]
        public float minKmh;
        [Tooltip("Velocidade máxima da faixa, em km/h (inclusive).")]
        public float maxKmh;
        [Tooltip("Dano aplicado quando a batida cai nesta faixa.")]
        public int damage;
    }

    /// <summary>Descreve um golpe já resolvido. A HUD usa para o número flutuante e o toast.</summary>
    public readonly struct DamageReport
    {
        public DamageReport(int amount, KartDamageKind kind, Vector3 point, GameObject source, bool blockedByShield)
        {
            Amount = amount;
            Kind = kind;
            Point = point;
            Source = source;
            BlockedByShield = blockedByShield;
        }

        public int Amount { get; }
        public KartDamageKind Kind { get; }
        public Vector3 Point { get; }
        public GameObject Source { get; }
        public bool BlockedByShield { get; }
    }

    [Header("Vida")]
    [SerializeField, Min(1)] private int maxHp = 100;

    [Header("Dano por batida no cenário")]
    [Tooltip("Faixas de velocidade (km/h) → dano. Fora de qualquer faixa = 0 de dano.")]
    [SerializeField]
    private WallDamageBand[] wallDamageBands =
    {
        new WallDamageBand { minKmh = 0f,   maxKmh = 74f,  damage = 0 },
        new WallDamageBand { minKmh = 75f,  maxKmh = 110f, damage = 4 },
        new WallDamageBand { minKmh = 111f, maxKmh = 150f, damage = 6 },
        new WallDamageBand { minKmh = 151f, maxKmh = 200f, damage = 8 },
    };

    [Tooltip("Frontalidade mínima (0 = raspão paralelo, 1 = de cara) para a batida contar como " +
             "impacto. Abaixo disso é lataria raspando na mureta e não tira vida.")]
    [SerializeField, Range(0f, 1f)] private float minWallFrontality = 0.35f;

    [Header("Outras fontes")]
    [SerializeField, Min(0)] private int trapDamage = 10;
    [SerializeField, Min(0)] private int itemDamage = 15;

    [Header("Cooldown de contato")]
    [Tooltip("Tempo mínimo entre dois danos. É o que impede o moinho de drenar a vida inteira " +
             "enquanto o kart está preso nele.")]
    [SerializeField, Min(0f)] private float contactCooldown = 0.75f;

    [Header("Estado danificado")]
    [SerializeField, Min(0.1f)] private float brokenDuration = 2.5f;
    [Tooltip("Perda de velocidade máxima durante o estado danificado. 0,35 = −35%.")]
    [SerializeField, Range(0f, 0.9f)] private float brokenSpeedPenalty = 0.35f;
    [Tooltip("Perda de aceleração durante o estado danificado. 0,5 = −50%.")]
    [SerializeField, Range(0f, 0.9f)] private float brokenAccelerationPenalty = 0.5f;
    [Tooltip("Perda de resposta de esterço durante o estado danificado. 0,25 = −25%.")]
    [SerializeField, Range(0f, 0.9f)] private float brokenSteerPenalty = 0.25f;

    [Header("Referências")]
    [SerializeField] private KartController kart;
    [SerializeField] private KartShieldAbility shield;

    private float damageCooldownEndTime;
    private float brokenEndTime;
    private bool penaltyApplied;

    // ---------------------------------------------------------------- Estado lido pela HUD

    public int MaxHp => maxHp;
    public int CurrentHp { get; private set; }
    public float Hp01 => maxHp > 0 ? Mathf.Clamp01(CurrentHp / (float)maxHp) : 0f;

    /// <summary>True durante os 2,5 s de penalidade que seguem o HP chegar a zero.</summary>
    public bool IsBroken { get; private set; }

    /// <summary>Segundos restantes do estado danificado.</summary>
    public float BrokenRemaining => IsBroken ? Mathf.Max(0f, brokenEndTime - Time.time) : 0f;

    /// <summary>Reparo restante 0..1 — 1 quando entra no estado, 0 quando a vida volta.</summary>
    public float BrokenRemaining01 => brokenDuration > 0f ? Mathf.Clamp01(BrokenRemaining / brokenDuration) : 0f;

    /// <summary>True enquanto o kart está imune por ter levado dano há pouco.</summary>
    public bool OnDamageCooldown => Time.time < damageCooldownEndTime;

    /// <summary>Imunidade restante 0..1, para a régua IMUNE da HUD.</summary>
    public float DamageCooldown01 => contactCooldown > 0f
        ? Mathf.Clamp01((damageCooldownEndTime - Time.time) / contactCooldown)
        : 0f;

    /// <summary>Dano aplicado (ou bloqueado pelo escudo, com <c>BlockedByShield</c>).</summary>
    public event System.Action<KartHealth, DamageReport> Damaged;

    /// <summary>Cura recebida. O argumento é quanto de HP entrou de fato.</summary>
    public event System.Action<KartHealth, int> Healed;

    /// <summary>HP chegou a zero: começou o estado danificado.</summary>
    public event System.Action<KartHealth> Broke;

    /// <summary>Fim do estado danificado: vida restaurada para o máximo.</summary>
    public event System.Action<KartHealth> Repaired;

    // ---------------------------------------------------------------- Ciclo

    private void Awake()
    {
        if (kart == null)
            kart = GetComponent<KartController>();

        if (shield == null)
            shield = GetComponent<KartShieldAbility>();

        CurrentHp = maxHp;
    }

    private void OnEnable()
    {
        if (kart != null)
            kart.WallImpact += OnWallImpact;
    }

    private void OnDisable()
    {
        if (kart != null)
            kart.WallImpact -= OnWallImpact;

        ClearPenalty();
    }

    private void Update()
    {
        if (!IsBroken || Time.time < brokenEndTime)
            return;

        FinishBrokenState();
    }

    // ---------------------------------------------------------------- Entradas de dano

    private void OnWallImpact(float speedKmh, float frontality)
    {
        if (frontality < minWallFrontality)
            return;

        int amount = WallDamageFor(speedKmh);
        if (amount <= 0)
            return;

        ApplyDamage(amount, KartDamageKind.Wall, transform.position, null);
    }

    /// <summary>Dano da tabela para uma velocidade. Fora de todas as faixas → 0.</summary>
    public int WallDamageFor(float speedKmh)
    {
        if (wallDamageBands == null)
            return 0;

        int worst = 0;
        for (int i = 0; i < wallDamageBands.Length; i++)
        {
            WallDamageBand band = wallDamageBands[i];
            if (speedKmh >= band.minKmh && speedKmh <= band.maxKmh)
                return band.damage;

            // Acima do teto da última faixa (boost extremo) vale o dano da faixa mais alta,
            // em vez de sair impune por ter passado do limite da tabela.
            if (speedKmh > band.maxKmh && band.damage > worst)
                worst = band.damage;
        }

        return worst;
    }

    /// <summary>Dano de armadilha/obstáculo do mapa. Respeita o escudo.</summary>
    public bool ApplyTrapDamage(Vector3 point, GameObject source)
        => ApplyDamage(trapDamage, KartDamageKind.Trap, point, source);

    /// <summary>Dano de item de outro jogador. Respeita o escudo.</summary>
    public bool ApplyItemDamage(Vector3 point, GameObject source)
        => ApplyDamage(itemDamage, KartDamageKind.Item, point, source);

    /// <summary>
    /// Aplica dano respeitando escudo, cooldown e estado danificado.
    /// Retorna true quando o HP realmente caiu.
    /// </summary>
    public bool ApplyDamage(int amount, KartDamageKind kind, Vector3 point, GameObject source)
    {
        if (amount <= 0 || IsBroken)
            return false;

        // O escudo é a defesa contra o que os OUTROS fazem — item, armadilha e obstáculo.
        // Bater na parede continua sendo por conta do piloto (handoff v2 §5).
        if (kind != KartDamageKind.Wall && shield != null && shield.IsActive)
        {
            shield.NotifyBlocked(point);
            Damaged?.Invoke(this, new DamageReport(0, kind, point, source, true));
            return false;
        }

        if (OnDamageCooldown)
            return false;

        CurrentHp = Mathf.Max(0, CurrentHp - amount);
        damageCooldownEndTime = Time.time + contactCooldown;

        Damaged?.Invoke(this, new DamageReport(amount, kind, point, source, false));
        RaceHudEvents.Raise(gameObject, source, RaceHudEventKind.Damaged, KartPowerType.None, amount);

        if (CurrentHp <= 0)
            EnterBrokenState();

        return true;
    }

    /// <summary>Cura da caixa da bifurcação. Não funciona durante o estado danificado.</summary>
    public bool Heal(int amount)
    {
        if (amount <= 0 || IsBroken || CurrentHp >= maxHp)
            return false;

        int before = CurrentHp;
        CurrentHp = Mathf.Min(maxHp, CurrentHp + amount);

        int recovered = CurrentHp - before;
        if (recovered <= 0)
            return false;

        Healed?.Invoke(this, recovered);
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.Healed, KartPowerType.None, recovered);
        return true;
    }

    /// <summary>Volta ao estado inicial. Usado pelo respawn e pela largada.</summary>
    public void ResetToFull()
    {
        ClearPenalty();
        IsBroken = false;
        brokenEndTime = 0f;
        damageCooldownEndTime = 0f;
        CurrentHp = maxHp;
    }

    // ---------------------------------------------------------------- Estado danificado

    private void EnterBrokenState()
    {
        IsBroken = true;
        brokenEndTime = Time.time + brokenDuration;

        if (kart != null)
        {
            kart.SetSpeedLimitMultiplier(this, 1f - brokenSpeedPenalty);
            kart.SetHandlingPenalty(this, 1f - brokenAccelerationPenalty, 1f - brokenSteerPenalty);
            penaltyApplied = true;
        }

        Broke?.Invoke(this);
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.Broken);
    }

    private void FinishBrokenState()
    {
        ClearPenalty();
        IsBroken = false;
        CurrentHp = maxHp;

        // O cooldown de contato é renovado aqui de propósito: sem isso o kart sai do estado
        // danificado ainda encostado no moinho que o quebrou e leva dano no primeiro frame,
        // gastando a vida recém-devolvida antes de o jogador conseguir reagir.
        damageCooldownEndTime = Time.time + contactCooldown;

        Repaired?.Invoke(this);
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.Repaired);
    }

    private void ClearPenalty()
    {
        if (!penaltyApplied || kart == null)
        {
            penaltyApplied = false;
            return;
        }

        kart.RemoveSpeedLimitMultiplier(this);
        kart.RemoveHandlingPenalty(this);
        penaltyApplied = false;
    }

    // ---------------------------------------------------------------- Utilidades

    /// <summary>Acha o KartHealth a partir de qualquer colisor filho do kart.</summary>
    public static KartHealth FromCollider(Component collider)
        => collider != null ? collider.GetComponentInParent<KartHealth>() : null;

    private static readonly List<KartHealth> reusableBuffer = new List<KartHealth>();

    /// <summary>Todos os KartHealth vivos na cena (usado pela HUD e por ferramentas de editor).</summary>
    public static IReadOnlyList<KartHealth> FindAll()
    {
        reusableBuffer.Clear();
        reusableBuffer.AddRange(FindObjectsByType<KartHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        return reusableBuffer;
    }
}
