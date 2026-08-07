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
    [Tooltip("Velocidade máxima de referência do kart, em km/h. É a régua do dano proporcional.")]
    [SerializeField, Min(1f)] private float referenciaDeVelocidadeKmh = 190f;

    [Tooltip("Fração da velocidade máxima a partir da qual a batida tira vida. Abaixo disso é " +
             "encostada de manobra, e cobrar por ela punia quem estava só se recolocando na pista.")]
    [SerializeField, Range(0f, 1f)] private float limiarDeParede = 0.5f;

    [Tooltip("Dano de uma batida na velocidade máxima. Entre o limiar e ela o valor é proporcional.")]
    [SerializeField, Min(0)] private int danoMaximoDeParede = 22;

    [Tooltip("Frontalidade mínima (0 = raspão paralelo, 1 = de cara) para a batida contar como " +
             "impacto. Abaixo disso é lataria raspando na mureta e não tira vida.")]
    [SerializeField, Range(0f, 1f)] private float minWallFrontality = 0.35f;

    [Header("Outras fontes")]
    [SerializeField, Min(0)] private int trapDamage = 10;
    [SerializeField, Min(0)] private int itemDamage = 15;

    [Header("Escudo")]
    [Tooltip("O escudo também segura o dano do CENÁRIO — parede, moinho, taco, bola. Desligado, " +
             "ele volta a valer só contra item e armadilha, como no handoff original.")]
    [SerializeField] private bool escudoProtegeDoCenario = true;

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
        // Regra da sala privada. Desligado, a batida continua sacudindo o kart e soltando faísca —
        // o que sai é só a perda de vida. Tirar o retorno físico junto faria a parede parecer um
        // bug, e não uma escolha de partida.
        if (!PartyRacers.Race.RaceRules.DanoPorColisao)
            return;

        if (frontality < minWallFrontality)
            return;

        int amount = WallDamageFor(speedKmh);
        if (amount <= 0)
            return;

        ApplyDamage(amount, KartDamageKind.Wall, transform.position, null);
    }

    /// <summary>
    /// Dano de batida em parede: nada abaixo do limiar, proporcional acima dele.
    ///
    /// A tabela de faixas que existia aqui cobrava 4 de dano a 75 km/h e 8 a 200 — o dobro de
    /// velocidade pelo dobro do dano, mas em degraus, e com a faixa mais baixa punindo manobra
    /// normal de reposicionamento. Metade da velocidade máxima é o ponto em que a batida deixa de
    /// ser manobra e vira erro; daí para cima o preço sobe junto com a velocidade.
    /// </summary>
    public int WallDamageFor(float speedKmh)
        => DanoProporcional(speedKmh, danoMaximoDeParede, limiarDeParede);

    /// <summary>
    /// Dano proporcional à velocidade, com piso e teto.
    ///
    /// A mesma conta serve para parede, bola de golfe e taco: o que muda é o TETO que cada um
    /// passa. Sem uma régua comum, cada obstáculo inventaria a própria escala e o jogador não
    /// conseguiria prever nada.
    /// </summary>
    public int DanoProporcional(float speedKmh, int teto, float limiar)
    {
        if (teto <= 0)
            return 0;

        float maxima = Mathf.Max(1f, referenciaDeVelocidadeKmh);
        float minima = maxima * Mathf.Clamp01(limiar);

        if (speedKmh <= minima)
            return 0;

        float t = Mathf.Clamp01((speedKmh - minima) / Mathf.Max(1f, maxima - minima));
        return Mathf.Clamp(Mathf.RoundToInt(teto * t), 1, teto);
    }

    /// <summary>Dano de armadilha/obstáculo do mapa. Respeita o escudo.</summary>
    public bool ApplyTrapDamage(Vector3 point, GameObject source)
        => ApplyDamage(trapDamage, KartDamageKind.Trap, point, source);

    /// <summary>
    /// Dano de obstáculo PROPORCIONAL à velocidade do kart, com teto próprio do obstáculo.
    ///
    /// A bola de golfe usa isto com teto 30: atravessá-la parado é um encosto, atravessá-la a
    /// 180 km/h é um acidente, e o mesmo obstáculo não pode cobrar igual pelos dois.
    /// </summary>
    public bool ApplyImpactDamage(Vector3 point, GameObject source, int teto, float limiar = 0f)
    {
        float velocidade = kart != null ? kart.SpeedKmh : 0f;
        int dano = DanoProporcional(velocidade, teto, limiar);
        return dano > 0 && ApplyDamage(dano, KartDamageKind.Trap, point, source);
    }

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

        // O escudo é a defesa contra o que ACONTECE com o kart. Antes ele valia só para item e
        // armadilha; agora o cenário entra junto (parede, moinho, taco, bola), porque um escudo
        // que some ao encostar num obstáculo do mapa não parece escudo — parece bug.
        bool protegido = escudoProtegeDoCenario || kind != KartDamageKind.Wall;

        if (protegido && shield != null && shield.IsActive)
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
