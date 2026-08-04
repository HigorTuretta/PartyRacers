using System.Collections;
using System.Collections.Generic;
using PartyRacers.Networking;
using UnityEngine;

/// <summary>
/// Caixa de CURA da bifurcação (handoff v2 §5 e §9). Irmã da <see cref="ItemBox"/>: onde a pista
/// se abre em dois caminhos, um lado dá item e o outro devolve vida. A escolha é do jogador e se
/// comunica pela pista — a HUD não mostra prompt nenhum.
///
/// Passar por cima com a vida cheia NÃO consome a caixa: quem já está inteiro não deve gastar a
/// cura do adversário que vem atrás, e o jogador aprende rápido que só vale a pena desviar quando
/// está machucado.
/// </summary>
[DisallowMultipleComponent]
public class HealPickup : MonoBehaviour
{
    // Mesmo esquema de índice estável da ItemBox: o árbitro de rede numera as caixas por ordem
    // determinística e assim consegue dizer "a cura 7 foi consumida" sem um NetworkObject por caixa.
    private static readonly List<HealPickup> registry = new List<HealPickup>();
    public static IReadOnlyList<HealPickup> All => registry;

    [Header("Cura")]
    [Tooltip("HP devolvido ao passar. 40 é o valor do balanceamento inicial (tokens-v2).")]
    [SerializeField, Min(1)] private int healAmount = 40;

    [Header("Configuração")]
    [SerializeField] private float respawnTime = 6f;
    [Tooltip("Consumir a caixa mesmo com o kart de vida cheia. Desligado, quem está inteiro passa " +
             "por cima sem gastar a cura.")]
    [SerializeField] private bool consumeWhenFullHp = false;

    [Header("Estado")]
    [SerializeField] private bool available = true;

    [Header("Feedback de coleta")]
    [SerializeField] private GameObject collectVfxPrefab;
    [SerializeField] private float collectVfxLifetime = 1.4f;
    [SerializeField] private float collectVfxScale = 1f;
    [SerializeField] private Vector3 collectVfxOffset = new Vector3(0f, 0.15f, 0f);

    private Collider pickupCollider;
    private Renderer[] renderers;
    private Coroutine respawnRoutine;

    /// <summary>Índice estável atribuído pela camada de rede. -1 fora de rede.</summary>
    public int NetworkIndex { get; private set; } = -1;

    public int HealAmount => healAmount;

    private void Awake()
    {
        pickupCollider = GetComponent<Collider>();
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
        if (!RaceAuthority.HasSimulationAuthority)
            return;

        if (!available)
            return;

        KartHealth health = other.GetComponentInParent<KartHealth>();
        if (health == null)
            return;

        bool healed = health.Heal(healAmount);

        if (!healed && !consumeWhenFullHp)
            return;

        Consume();
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
        PlayCollectVfx();

        if (respawnRoutine != null)
            StopCoroutine(respawnRoutine);
        respawnRoutine = StartCoroutine(RespawnRoutine());
    }

    private void PlayCollectVfx()
    {
        if (collectVfxPrefab == null)
            return;

        Vector3 spawnPosition = transform.position;
        bool hasBounds = false;
        Bounds visualBounds = default;

        foreach (Renderer pickupRenderer in renderers)
        {
            if (pickupRenderer == null)
                continue;

            if (!hasBounds)
            {
                visualBounds = pickupRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                visualBounds.Encapsulate(pickupRenderer.bounds);
            }
        }

        if (hasBounds)
            spawnPosition = visualBounds.center;

        PowerVFXUtility.SpawnOneShot(
            collectVfxPrefab,
            spawnPosition + collectVfxOffset,
            Quaternion.identity,
            collectVfxLifetime,
            0f,
            collectVfxScale);
    }

    private IEnumerator RespawnRoutine()
    {
        available = false;

        if (pickupCollider != null)
            pickupCollider.enabled = false;

        SetVisualEnabled(false);

        yield return new WaitForSeconds(respawnTime);

        SetVisualEnabled(true);

        if (pickupCollider != null)
            pickupCollider.enabled = true;

        available = true;
        respawnRoutine = null;
    }

    private void SetVisualEnabled(bool enabled)
    {
        foreach (Renderer pickupRenderer in renderers)
        {
            if (pickupRenderer != null)
                pickupRenderer.enabled = enabled;
        }
    }
}
