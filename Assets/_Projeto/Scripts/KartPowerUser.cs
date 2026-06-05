using UnityEngine;
using UnityEngine.InputSystem;

// Orquestra o uso dos poderes do kart (tecla padrão E):
//  - Escudo: delega ao KartShieldVisual (prefab Magic shield blue) envolvendo o carro.
//  - Foguete: mostra o foguete acima do carro enquanto equipado e dispara o RocketProjectile.
// Mantém a API pública usada por projéteis: IsShieldActive e PulseShieldBlock.
public class KartPowerUser : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartPowerInventory inventory;
    [SerializeField] private KartController kart;
    [SerializeField] private KartShieldVisual shieldVisual;

    [Header("Escudo")]
    [SerializeField] private float shieldDuration = 4f;
    [SerializeField] private GameObject shieldBlockVFXPrefab;

    [Header("Foguete")]
    [SerializeField] private GameObject rocketProjectilePrefab;
    [Tooltip("Prefab do foguete equipado (idle). Se vazio, usa o mesmo prefab do projétil.")]
    [SerializeField] private GameObject rocketEquippedPrefab;
    [SerializeField] private GameObject explosionVFXPrefab;
    [SerializeField] private GameObject boingVFXPrefab;
    [Tooltip("Trail do foguete em voo (ex.: VFXRocketTrail). Passado ao projétil ao disparar.")]
    [SerializeField] private GameObject rocketTrailPrefab;
    [SerializeField] private Transform rocketEquippedSocket;
    [SerializeField] private Vector3 rocketSocketLocalPosition = new Vector3(0f, 1.15f, 0.15f);
    [Tooltip("Quão à frente do socket o projétil nasce ao disparar.")]
    [SerializeField] private float rocketLaunchForwardOffset = 0.6f;

    [Header("Input")]
    [SerializeField] private Key useKey = Key.E;

    private float shieldEndTime;
    private GameObject equippedRocketInstance;

    public bool IsShieldActive => Time.time < shieldEndTime;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<KartPowerInventory>();

        if (kart == null)
            kart = GetComponent<KartController>();

        if (shieldVisual == null)
            shieldVisual = GetComponent<KartShieldVisual>();

        if (shieldVisual == null)
            shieldVisual = gameObject.AddComponent<KartShieldVisual>();

        EnsureRocketSocket();
    }

    private void Update()
    {
        ReadInput();
        UpdateShield();
        UpdateEquippedRocket();
    }

    private void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[useKey].wasPressedThisFrame)
            TryUseCurrentPower();
    }

    public void TryUseCurrentPower()
    {
        if (inventory == null || !inventory.HasPower)
            return;

        KartPowerType power = inventory.ConsumeCurrentPower();
        RaceHudEvents.Raise(gameObject, null, RaceHudEventKind.PowerUsed, power);

        switch (power)
        {
            case KartPowerType.Shield:
                ActivateShield();
                break;

            case KartPowerType.Rocket:
                FireRocket();
                break;

            case KartPowerType.SwapPosition:
                Debug.Log("Poder Troca ainda será implementado.");
                break;
        }
    }

    // ------------------------------------------------------------------ Escudo
    private void ActivateShield()
    {
        shieldEndTime = Time.time + shieldDuration;

        if (shieldVisual != null)
            shieldVisual.Activate();
    }

    private void UpdateShield()
    {
        if (shieldVisual == null)
            return;

        bool shouldBeActive = IsShieldActive;

        if (shouldBeActive == shieldVisual.IsActive)
            return;

        if (shouldBeActive)
            shieldVisual.Activate();
        else
            shieldVisual.Deactivate();
    }

    public void PulseShieldBlock(Vector3 impactPoint, GameObject blockVFXPrefab)
    {
        if (shieldVisual != null)
            shieldVisual.PulseBlock(impactPoint);

        GameObject vfx = blockVFXPrefab != null ? blockVFXPrefab : shieldBlockVFXPrefab;
        if (vfx != null)
            Instantiate(vfx, impactPoint, Quaternion.identity);
    }

    // ------------------------------------------------------------------ Foguete
    private void EnsureRocketSocket()
    {
        if (rocketEquippedSocket != null)
            return;

        Transform existing = transform.Find("RocketEquippedSocket");
        rocketEquippedSocket = existing != null
            ? existing
            : new GameObject("RocketEquippedSocket").transform;

        rocketEquippedSocket.SetParent(transform, false);
        rocketEquippedSocket.localPosition = rocketSocketLocalPosition;
        rocketEquippedSocket.localRotation = Quaternion.identity;
    }

    private void UpdateEquippedRocket()
    {
        bool shouldShow = inventory != null && inventory.CurrentPower == KartPowerType.Rocket;

        if (!shouldShow)
        {
            if (equippedRocketInstance != null)
                equippedRocketInstance.SetActive(false);

            return;
        }

        if (equippedRocketInstance == null)
            CreateEquippedRocket();

        if (equippedRocketInstance != null && !equippedRocketInstance.activeSelf)
            equippedRocketInstance.SetActive(true);
    }

    private void CreateEquippedRocket()
    {
        GameObject prefab = rocketEquippedPrefab != null ? rocketEquippedPrefab : rocketProjectilePrefab;

        if (prefab == null)
            return;

        EnsureRocketSocket();

        equippedRocketInstance = Instantiate(prefab, rocketEquippedSocket);
        equippedRocketInstance.transform.localPosition = Vector3.zero;
        equippedRocketInstance.transform.localRotation = Quaternion.identity;

        RocketEquippedVisual equipped = equippedRocketInstance.GetComponent<RocketEquippedVisual>();
        if (equipped == null)
            equipped = equippedRocketInstance.AddComponent<RocketEquippedVisual>();

        equipped.Initialize(kart);
    }

    private void FireRocket()
    {
        EnsureRocketSocket();

        if (equippedRocketInstance != null)
        {
            Destroy(equippedRocketInstance);
            equippedRocketInstance = null;
        }

        if (rocketProjectilePrefab == null || rocketEquippedSocket == null)
        {
            Debug.LogWarning("Foguete não disparado: prefab ou socket ausente.");
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 spawnPosition = rocketEquippedSocket.position + forward * rocketLaunchForwardOffset;
        Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);

        GameObject projectileObject = Instantiate(rocketProjectilePrefab, spawnPosition, spawnRotation);

        RocketProjectile projectile = projectileObject.GetComponent<RocketProjectile>();
        if (projectile == null)
            projectile = projectileObject.AddComponent<RocketProjectile>();

        projectile.Initialize(gameObject, forward, explosionVFXPrefab, boingVFXPrefab, rocketTrailPrefab);
    }
}
