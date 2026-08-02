using System.Collections;
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

    private void Awake()
    {
        itemCollider = GetComponent<Collider>();
        renderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnTriggerEnter(Collider other)
    {
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

        PlayBreakVfx();
        StartCoroutine(RespawnRoutine());
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
