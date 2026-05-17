using UnityEngine;
using UnityEngine.InputSystem;

public class KartPowerUser : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartPowerInventory inventory;
    [SerializeField] private GameObject shieldVisual;

    [Header("Escudo")]
    [SerializeField] private float shieldDuration = 4f;

    [Header("Estado")]
    [SerializeField] private float shieldEndTime;

    public bool IsShieldActive => Time.time < shieldEndTime;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<KartPowerInventory>();

        DisableShieldPhysics();
        UpdateShieldVisual();
    }

    private void Update()
    {
        ReadPowerInput();
        UpdateShieldVisual();
    }

    private void ReadPowerInput()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame)
            TryUseCurrentPower();
    }

    public void TryUseCurrentPower()
    {
        if (inventory == null)
            return;

        if (!inventory.HasPower)
            return;

        KartPowerType power = inventory.ConsumeCurrentPower();

        switch (power)
        {
            case KartPowerType.Shield:
                ActivateShield();
                break;

            case KartPowerType.StunShot:
                Debug.Log("Poder Stun ainda será implementado.");
                break;

            case KartPowerType.SwapPosition:
                Debug.Log("Poder Troca ainda será implementado.");
                break;
        }
    }

    private void ActivateShield()
    {
        shieldEndTime = Time.time + shieldDuration;
        Debug.Log("Escudo ativado.");
        UpdateShieldVisual();
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual == null)
            return;

        shieldVisual.SetActive(IsShieldActive);
    }

    private void DisableShieldPhysics()
    {
        if (shieldVisual == null)
            return;

        Collider[] colliders = shieldVisual.GetComponentsInChildren<Collider>(true);

        foreach (Collider shieldCollider in colliders)
        {
            shieldCollider.enabled = false;
        }

        Rigidbody[] rigidbodies = shieldVisual.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody shieldRigidbody in rigidbodies)
        {
            shieldRigidbody.isKinematic = true;
            shieldRigidbody.detectCollisions = false;
        }
    }
}