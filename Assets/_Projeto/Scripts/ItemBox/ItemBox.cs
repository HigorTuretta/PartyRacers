using System.Collections;
using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;

public class ItemBox : MonoBehaviour
{
    private static readonly KartPowerType[] DefaultPowerPool =
    {
        KartPowerType.SwapPosition,
        KartPowerType.Rocket,
        KartPowerType.Shield,
        KartPowerType.ElectricTrap
    };

    // Todas as caixas vivas da cena. O árbitro de rede usa esta lista para dar a cada caixa o
    // MESMO índice em todas as máquinas — é assim que o servidor consegue dizer "a caixa 27 foi
    // consumida" sem que as caixas precisem de um NetworkObject cada uma.
    private static readonly List<ItemBox> registry = new List<ItemBox>();
    public static IReadOnlyList<ItemBox> All => registry;

    [Header("Configuração")]
    [SerializeField] private float respawnTime = 4f;
    [Tooltip("Poderes que esta caixa pode sortear. Vazio usa o conjunto padrão do jogo.")]
    [SerializeField] private KartPowerType[] availablePowers =
    {
        KartPowerType.SwapPosition,
        KartPowerType.Rocket,
        KartPowerType.Shield,
        KartPowerType.ElectricTrap
    };

    [Header("Estado")]
    [SerializeField] private bool available = true;

    [Header("Feedback de quebra")]
    [Tooltip("VFX one-shot com lascas, poeira e brilho de coleta.")]
    [SerializeField] private GameObject breakVfxPrefab;
    [SerializeField] private float breakVfxLifetime = 1.4f;
    [SerializeField] private float breakVfxScale = 1f;
    [SerializeField] private Vector3 breakVfxOffset = new Vector3(0f, 0.15f, 0f);

    private Collider itemCollider;
    private Renderer[] renderers;
    private Coroutine respawnRoutine;

    /// <summary>Índice estável atribuído pelo <see cref="RaceNetworkDirector"/>. -1 fora de rede.</summary>
    public int NetworkIndex { get; private set; } = -1;

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnEnable() => registry.Add(this);

    private void OnDisable()
    {
        registry.Remove(this);
        NetworkIndex = -1;
    }

    public void AssignNetworkIndex(int index) => NetworkIndex = index;

    /// <summary>Chave de ordenação determinística: idêntica no host e em cada cliente.</summary>
    public string SortKey
    {
        get
        {
            Vector3 p = transform.position;
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{0:F2}|{1:F2}|{2:F2}|{3}",
                p.x, p.y, p.z, HierarchyPath());
        }
    }

    private string HierarchyPath()
    {
        Transform t = transform;
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }

        return path;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Online, só o servidor decide o sorteio e o consumo da caixa. Antes cada máquina sorteava
        // o seu próprio poder no mesmo contato, e por isso os jogadores viam itens diferentes.
        if (!RaceAuthority.HasSimulationAuthority)
            return;

        if (!available)
            return;

        KartPowerInventory inventory = other.GetComponentInParent<KartPowerInventory>();

        if (inventory == null)
            return;

        KartPowerType randomPower = GetRandomPower();

        bool receivedPower = inventory.TryGivePower(randomPower);

        // Passar por uma caixa já tendo poder é o caso NORMAL, não um aviso: logar isso com 16
        // karts na pista inundava o console e custava frame (captura de stack trace).
        if (!receivedPower)
            return;

        Consume();
        RaceNetworkDirector.NotifyBoxConsumed(this);
    }

    /// <summary>Consumo replicado: o cliente reproduz o que o servidor já decidiu.</summary>
    public void ConsumeFromNetwork()
    {
        if (!available)
            return;

        Consume();
    }

    private void Consume()
    {
        PlayBreakVfx();

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);
        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private void PlayBreakVfx()
    {
        if (breakVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position;
        bool hasBounds = false;
        Bounds visualBounds = default;

        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer == null)
                continue;

            if (!hasBounds)
            {
                visualBounds = itemRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                visualBounds.Encapsulate(itemRenderer.bounds);
            }
        }

        if (hasBounds)
            spawnPosition = visualBounds.center;

        PowerVFXUtility.SpawnOneShot(
            breakVfxPrefab,
            spawnPosition + breakVfxOffset,
            Quaternion.identity,
            breakVfxLifetime,
            0f,
            breakVfxScale);
    }

    private KartPowerType GetRandomPower()
    {
        KartPowerType[] pool = availablePowers != null && availablePowers.Length > 0
            ? availablePowers
            : DefaultPowerPool;

        return pool[Random.Range(0, pool.Length)];
    }

    private IEnumerator RespawnRoutine()
    {
        available = false;

        if (itemCollider != null)
            itemCollider.enabled = false;

        SetVisualEnabled(false);

        yield return new WaitForSeconds(respawnTime);

        SetVisualEnabled(true);

        if (itemCollider != null)
            itemCollider.enabled = true;

        available = true;
        respawnRoutine = null;
    }

    private void SetVisualEnabled(bool enabled)
    {
        foreach (Renderer itemRenderer in renderers)
        {
            if (itemRenderer != null)
                itemRenderer.enabled = enabled;
        }
    }
}
