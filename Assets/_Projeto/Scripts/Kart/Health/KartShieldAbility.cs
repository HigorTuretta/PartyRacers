using PartyRacers.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Escudo como HABILIDADE FIXA de todo kart (handoff v2 §5). Saiu da ItemBox: não se coleta mais,
/// recarrega sozinho durante a corrida e o jogador aciona quando quiser.
///
/// Recarga 22 s, 3 s ativo. Bloqueia itens de outros jogadores, armadilhas e obstáculos do mapa —
/// não bloqueia batida em parede, que continua sendo responsabilidade do piloto.
///
/// A HUD não tem botão nem ícone de escudo: a própria barra comunica o estado (brilho + varredura
/// quando disponível, nada disso em recarga). Por isso este componente expõe
/// <see cref="Cooldown01"/> e <see cref="ActiveRemaining01"/> em vez de um texto pronto.
/// </summary>
[DisallowMultipleComponent]
public class KartShieldAbility : MonoBehaviour
{
    public enum ShieldState
    {
        /// <summary>Carregado, esperando o jogador acionar.</summary>
        Ready,
        /// <summary>No ar, bloqueando.</summary>
        Active,
        /// <summary>Recarregando.</summary>
        Cooling,
    }

    [Header("Regras")]
    [Tooltip("Tempo de recarga em segundos, contado do FIM do escudo anterior.")]
    [SerializeField, Min(1f)] private float cooldown = 22f;
    [Tooltip("Quanto tempo o escudo fica no ar depois de acionado.")]
    [SerializeField, Min(0.1f)] private float activeDuration = 3f;
    [Tooltip("Começar a corrida com o escudo carregado.")]
    [SerializeField] private bool startCharged = true;

    [Header("Input")]
    [Tooltip("Tecla do escudo no teclado. O poder de item usa E; o escudo usa Q.")]
    [SerializeField] private Key activationKey = Key.Q;
    [Tooltip("Ler teclado/gamepad neste kart. Bots e karts remotos devem manter desligado — " +
             "eles acionam por KartShieldAbility.TryActivate().")]
    [SerializeField] private bool readLocalInput = true;

    [Header("Referências")]
    [SerializeField] private KartShieldVisual shieldVisual;
    [SerializeField] private KartNetworkIdentity identity;
    [SerializeField] private KartLocalRig localRig;

    [Header("VFX de bloqueio")]
    [SerializeField] private GameObject blockVFXPrefab;
    [SerializeField, Min(0.1f)] private float blockVFXLifetime = 1.5f;

    private float activeEndTime;
    private float readyTime;

    // ---------------------------------------------------------------- Estado lido pela HUD

    public bool IsActive => Time.time < activeEndTime;
    public bool IsReady => !IsActive && Time.time >= readyTime;

    public ShieldState State => IsActive ? ShieldState.Active
                             : IsReady ? ShieldState.Ready
                             : ShieldState.Cooling;

    /// <summary>Progresso da recarga 0..1 — 0 assim que o escudo acaba, 1 quando volta a estar pronto.</summary>
    public float Cooldown01
    {
        get
        {
            if (IsReady) return 1f;
            if (IsActive) return 0f;
            float restante = readyTime - Time.time;
            return Mathf.Clamp01(1f - restante / Mathf.Max(0.01f, cooldown));
        }
    }

    /// <summary>Segundos que faltam para o escudo voltar. 0 quando já está pronto.</summary>
    public float CooldownRemaining => IsReady ? 0f : Mathf.Max(0f, readyTime - Time.time);

    /// <summary>Fração restante do escudo ativo (1 no instante do acionamento).</summary>
    public float ActiveRemaining01 => IsActive
        ? Mathf.Clamp01((activeEndTime - Time.time) / Mathf.Max(0.01f, activeDuration))
        : 0f;

    /// <summary>Segundos restantes do escudo ativo.</summary>
    public float ActiveRemaining => Mathf.Max(0f, activeEndTime - Time.time);

    public float Cooldown => cooldown;
    public float ActiveDuration => activeDuration;

    public event System.Action<KartShieldAbility> Activated;
    public event System.Action<KartShieldAbility> Ended;
    public event System.Action<KartShieldAbility> BecameReady;

    /// <summary>Algo foi rebatido pelo escudo. O argumento é o ponto do impacto.</summary>
    public event System.Action<KartShieldAbility, Vector3> Blocked;

    // ---------------------------------------------------------------- Ciclo

    private void Awake()
    {
        if (shieldVisual == null)
            shieldVisual = GetComponent<KartShieldVisual>();

        if (shieldVisual == null)
            shieldVisual = gameObject.AddComponent<KartShieldVisual>();

        if (identity == null)
            identity = GetComponent<KartNetworkIdentity>();

        if (localRig == null)
            localRig = GetComponent<KartLocalRig>();

        readyTime = startCharged ? 0f : Time.time + cooldown;
    }

    private void Update()
    {
        ReadInput();
        SyncVisual();
        DetectStateEdges();
    }

    private void ReadInput()
    {
        if (!readLocalInput || !ShouldReadLocalInput())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[activationKey].wasPressedThisFrame)
        {
            TryActivate();
            return;
        }

        // Gamepad: LB, oposto ao RB do item — a mesma separação mão-esquerda/mão-direita das
        // barras vitais (canto inferior esquerdo) e do slot de poder (canto inferior direito).
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && gamepad.leftShoulder.wasPressedThisFrame)
            TryActivate();
    }

    private bool ShouldReadLocalInput()
    {
        if (identity != null && !identity.IsLocalControlled)
            return false;

        if (localRig != null && !localRig.IsLocalPlayer)
            return false;

        return true;
    }

    /// <summary>Aciona o escudo se estiver carregado. Bots e o botão do celular chamam isto.</summary>
    public bool TryActivate()
    {
        if (!IsReady)
            return false;

        activeEndTime = Time.time + activeDuration;

        // A recarga só começa quando o escudo CAI. Contando do acionamento, o jogador teria
        // 22 s de ciclo dos quais 3 já foram gastos protegido — o intervalo real de vulnerabilidade
        // mudaria conforme ele segurasse ou não o escudo.
        readyTime = activeEndTime + cooldown;

        if (shieldVisual != null)
            shieldVisual.Activate();

        wasActive = true;
        wasReady = false;

        Activated?.Invoke(this);
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.ShieldActivated);
        return true;
    }

    /// <summary>Chamado por quem teve o golpe barrado (KartHealth e os projéteis).</summary>
    public void NotifyBlocked(Vector3 impactPoint)
    {
        if (shieldVisual != null)
            shieldVisual.PulseBlock(impactPoint);

        if (blockVFXPrefab != null)
            PowerVFXUtility.SpawnOneShot(blockVFXPrefab, impactPoint, Quaternion.identity, blockVFXLifetime);

        Blocked?.Invoke(this, impactPoint);
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.ShieldBlocked);
    }

    /// <summary>Devolve o escudo imediatamente. Usado pelo respawn e pela largada.</summary>
    public void ResetCharged()
    {
        activeEndTime = 0f;
        readyTime = 0f;
        wasActive = false;
        wasReady = true;

        if (shieldVisual != null)
            shieldVisual.Deactivate();
    }

    // ---------------------------------------------------------------- Bordas de estado

    private bool wasActive;
    private bool wasReady;

    private void SyncVisual()
    {
        if (shieldVisual == null)
            return;

        bool shouldBeActive = IsActive;
        if (shouldBeActive == shieldVisual.IsActive)
            return;

        if (shouldBeActive)
            shieldVisual.Activate();
        else
            shieldVisual.Deactivate();
    }

    private void DetectStateEdges()
    {
        bool active = IsActive;
        if (wasActive && !active)
            Ended?.Invoke(this);
        wasActive = active;

        bool ready = IsReady;
        if (ready && !wasReady)
        {
            BecameReady?.Invoke(this);
            RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.ShieldReady);
        }
        wasReady = ready;
    }
}
