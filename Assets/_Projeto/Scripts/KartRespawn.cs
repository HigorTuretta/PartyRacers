using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(KartRaceTracker))]
public class KartRespawn : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private KartRaceTracker raceTracker;
    [SerializeField] private KartCollision kartCollision;

    [Header("Configuração")]
    [SerializeField] private float respawnHeightOffset = 1.5f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundProbeHeight = 12f;
    [SerializeField] private float groundProbeDistance = 35f;
    [SerializeField] private float groundClearance = 0.2f;

    private Collider[] kartColliders;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (raceTracker == null)
            raceTracker = GetComponent<KartRaceTracker>();

        if (kartCollision == null)
            kartCollision = GetComponent<KartCollision>();

        kartColliders = GetComponentsInChildren<Collider>();
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
        Quaternion targetRotation = raceTracker.LastRespawnRotation;
        Vector3 targetPosition = FindSafeRespawnPosition(raceTracker.LastRespawnPosition);

        kartCollision?.ResetIgnoredCollisionState();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = targetPosition;
        rb.rotation = targetRotation;
        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();

        rb.Sleep();
        rb.WakeUp();
    }

    private Vector3 FindSafeRespawnPosition(Vector3 basePosition)
    {
        Vector3 fallback = basePosition + Vector3.up * respawnHeightOffset;
        Vector3 probeOrigin = basePosition + Vector3.up * groundProbeHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            probeOrigin,
            Vector3.down,
            groundProbeHeight + groundProbeDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        if (hits == null || hits.Length == 0)
            return fallback;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsKartCollider(hit.collider))
                continue;

            float rootToBottom = GetRootToColliderBottomOffset();
            return new Vector3(basePosition.x, hit.point.y + groundClearance + rootToBottom, basePosition.z);
        }

        return fallback;
    }

    private bool IsKartCollider(Collider collider)
    {
        if (kartColliders == null)
            return false;

        for (int i = 0; i < kartColliders.Length; i++)
        {
            if (kartColliders[i] == collider)
                return true;
        }

        return false;
    }

    private float GetRootToColliderBottomOffset()
    {
        if (kartColliders == null || kartColliders.Length == 0)
            return respawnHeightOffset;

        float minY = float.PositiveInfinity;

        for (int i = 0; i < kartColliders.Length; i++)
        {
            Collider col = kartColliders[i];
            if (col == null || col.isTrigger || !col.enabled)
                continue;

            minY = Mathf.Min(minY, col.bounds.min.y);
        }

        if (float.IsPositiveInfinity(minY))
            return respawnHeightOffset;

        return transform.position.y - minY;
    }
}
