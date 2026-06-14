using System.Collections;
using UnityEngine;

public class ItemBox : MonoBehaviour
{
    [Header("Configuração")]
    [SerializeField] private float respawnTime = 4f;

    [Header("Estado")]
    [SerializeField] private bool available = true;

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

        if (!receivedPower)
        {
            Debug.Log("Jogador já possui um poder.");
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    private KartPowerType GetRandomPower()
    {
        int random = Random.Range(0, 3);

        return random switch
        {
            0 => KartPowerType.SwapPosition,
            1 => KartPowerType.Rocket,
            2 => KartPowerType.Shield,
            _ => KartPowerType.Shield
        };
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