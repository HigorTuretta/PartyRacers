using UnityEngine;

public class KartRaceTracker : MonoBehaviour
{
    [Header("Corrida")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private int totalCheckpoints = 4;

    [Header("Estado Atual")]
    [SerializeField] private int currentLap = 1;
    [SerializeField] private int nextCheckpointIndex = 1;
    [SerializeField] private bool raceFinished;

    [Header("Respawn")]
    [SerializeField] private Vector3 lastRespawnPosition;
    [SerializeField] private Quaternion lastRespawnRotation;

    public int TotalLaps => totalLaps;
    public int CurrentLap => currentLap;
    public int NextCheckpointIndex => nextCheckpointIndex;
    public bool RaceFinished => raceFinished;
    public Vector3 LastRespawnPosition => lastRespawnPosition;
    public Quaternion LastRespawnRotation => lastRespawnRotation;

    private void Awake()
    {
        lastRespawnPosition = transform.position;
        lastRespawnRotation = transform.rotation;
    }

    public void PassCheckpoint(RaceCheckpoint checkpoint)
    {
        if (raceFinished)
            return;

        int checkpointIndex = checkpoint.CheckpointIndex;

        if (checkpointIndex != nextCheckpointIndex)
        {
            Debug.Log($"Checkpoint ignorado. Esperado: {nextCheckpointIndex}, recebido: {checkpointIndex}");
            return;
        }

        SaveRespawnPoint(checkpoint);

        Debug.Log($"Checkpoint {checkpointIndex} passou.");

        if (checkpoint.IsStartFinish)
        {
            CompleteLap();
            return;
        }

        nextCheckpointIndex++;

        if (nextCheckpointIndex >= totalCheckpoints)
            nextCheckpointIndex = 0;
    }

    private void SaveRespawnPoint(RaceCheckpoint checkpoint)
    {
        lastRespawnPosition = checkpoint.RespawnPosition;
        lastRespawnRotation = checkpoint.RespawnRotation;
    }

    private void CompleteLap()
    {
        Debug.Log($"Volta {currentLap} completa.");

        if (currentLap >= totalLaps)
        {
            raceFinished = true;
            Debug.Log("Corrida finalizada!");
            return;
        }

        currentLap++;
        nextCheckpointIndex = 1;

        Debug.Log($"Iniciando volta {currentLap}.");
    }
}