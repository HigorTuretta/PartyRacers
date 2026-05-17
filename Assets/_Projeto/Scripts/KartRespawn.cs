using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(KartRaceTracker))]
public class KartRespawn : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private KartRaceTracker raceTracker;

    [Header("Configuração")]
    [SerializeField] private float respawnHeightOffset = 1.5f;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (raceTracker == null)
            raceTracker = GetComponent<KartRaceTracker>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
            Respawn();
    }

    public void Respawn()
    {
        Vector3 targetPosition = raceTracker.LastRespawnPosition + Vector3.up * respawnHeightOffset;
        Quaternion targetRotation = raceTracker.LastRespawnRotation;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        transform.SetPositionAndRotation(targetPosition, targetRotation);
    }
}