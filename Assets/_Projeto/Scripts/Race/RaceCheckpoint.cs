using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    [SerializeField] private int checkpointIndex;
    [SerializeField] private bool isStartFinish;

    [Header("Respawn")]
    [SerializeField] private Transform respawnPoint;

    public int CheckpointIndex => checkpointIndex;
    public bool IsStartFinish => isStartFinish;

    public Vector3 RespawnPosition
    {
        get
        {
            if (respawnPoint != null)
                return respawnPoint.position;

            return transform.position + Vector3.up * 2f;
        }
    }

    public Quaternion RespawnRotation
    {
        get
        {
            if (respawnPoint != null)
                return respawnPoint.rotation;

            return transform.rotation;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        KartRaceTracker tracker = other.GetComponentInParent<KartRaceTracker>();

        if (tracker == null)
            return;

        tracker.PassCheckpoint(this);
    }
}