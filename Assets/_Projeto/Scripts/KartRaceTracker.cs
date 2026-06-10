using UnityEngine;

public class KartRaceTracker : MonoBehaviour
{
    [Header("Corrida")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private int totalCheckpoints = 8;
    [SerializeField] private bool autoDetectCheckpointCount = true;

    [Header("Estado Atual")]
    [SerializeField] private int currentLap = 1;
    [SerializeField] private int nextCheckpointIndex = 1;
    [SerializeField] private bool raceFinished;

    [Header("Respawn")]
    [SerializeField] private Vector3 lastRespawnPosition;
    [SerializeField] private Quaternion lastRespawnRotation;

    // Cronometragem de volta (additiva — não altera a regra de checkpoints/voltas).
    // O cronômetro NÃO inicia no "VAI": ele é apenas ARMADO na largada e só começa a contar
    // quando o kart cruza fisicamente a linha de largada/chegada (trigger IsStartFinish).
    private float lapStartTime;
    private float currentLapTime;
    private float lastLapTime = -1f;
    private float bestLapTime = -1f;
    private bool timing;
    private bool timingArmed;

    public int TotalLaps => totalLaps;
    public int TotalCheckpoints => totalCheckpoints;
    public int CurrentLap => currentLap;
    public int NextCheckpointIndex => nextCheckpointIndex;
    public bool RaceFinished => raceFinished;
    public Vector3 LastRespawnPosition => lastRespawnPosition;
    public Quaternion LastRespawnRotation => lastRespawnRotation;

    /// <summary>Tempo decorrido da volta atual, em segundos. 0 antes da largada.</summary>
    public float CurrentLapTime => currentLapTime;
    /// <summary>Tempo da última volta concluída, em segundos. -1 se nenhuma volta foi concluída.</summary>
    public float LastLapTime => lastLapTime;
    /// <summary>Melhor tempo de volta, em segundos. -1 se nenhuma volta foi concluída.</summary>
    public float BestLapTime => bestLapTime;
    /// <summary>True quando o cronômetro de volta está rodando.</summary>
    public bool IsTiming => timing;

    private void Awake()
    {
        AutoConfigureCheckpointCount();

        lastRespawnPosition = transform.position;
        lastRespawnRotation = transform.rotation;
        lapStartTime = Time.time;
    }

    private void Start()
    {
        AutoConfigureCheckpointCount();

        // Fallback: se nenhum RaceManager chamar NotifyRaceStarted (cena solta/teste),
        // o cronômetro fica armado mesmo assim — e inicia ao cruzar a linha de largada.
        if (!timing && !timingArmed)
            ArmLapTimer();
    }

    private void Update()
    {
        if (timing && !raceFinished)
            currentLapTime = Time.time - lapStartTime;
    }

    public void ConfigureCheckpointCount(int checkpointCount)
    {
        if (checkpointCount < 2)
            return;

        totalCheckpoints = checkpointCount;
        nextCheckpointIndex = Mathf.Clamp(nextCheckpointIndex, 0, totalCheckpoints - 1);
    }

    private void AutoConfigureCheckpointCount()
    {
        if (!autoDetectCheckpointCount)
            return;

        ConfigureCheckpointCount(DetectSceneCheckpointCount());
    }

    public static int DetectSceneCheckpointCount()
    {
        RaceCheckpoint[] checkpoints = FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude);
        if (checkpoints == null || checkpoints.Length == 0)
            return 0;

        int maxIndex = -1;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            RaceCheckpoint checkpoint = checkpoints[i];
            if (checkpoint != null)
                maxIndex = Mathf.Max(maxIndex, checkpoint.CheckpointIndex);
        }

        return maxIndex + 1;
    }

    /// <summary>
    /// Chamado pela largada (RaceManager) no "VAI!". ARMA o cronômetro — a contagem da volta
    /// só inicia quando o kart cruzar a linha de largada/chegada de verdade.
    /// </summary>
    public void NotifyRaceStarted() => ArmLapTimer();

    private void ArmLapTimer()
    {
        timingArmed = true;
        timing = false;
        currentLapTime = 0f;
    }

    private void BeginLapTimer()
    {
        lapStartTime = Time.time;
        currentLapTime = 0f;
        timing = true;
        timingArmed = false;
    }

    public void PassCheckpoint(RaceCheckpoint checkpoint)
    {
        if (raceFinished)
            return;

        // Primeiro cruzamento da linha de largada após o "VAI": inicia o cronômetro da volta 1.
        // Independe do índice esperado, pois o grid fica atrás da linha e este cruzamento
        // não conta como volta (nextCheckpointIndex já começa em 1).
        if (checkpoint.IsStartFinish && timingArmed && !timing)
            BeginLapTimer();

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
        RecordLapTime();

        Debug.Log($"Volta {currentLap} completa.");

        if (currentLap >= totalLaps)
        {
            raceFinished = true;
            timing = false;
            Debug.Log("Corrida finalizada!");
            return;
        }

        currentLap++;
        nextCheckpointIndex = 1;

        Debug.Log($"Iniciando volta {currentLap}.");
    }

    private void RecordLapTime()
    {
        if (!timing)
            return;

        lastLapTime = Time.time - lapStartTime;
        if (bestLapTime < 0f || lastLapTime < bestLapTime)
            bestLapTime = lastLapTime;

        lapStartTime = Time.time;
        currentLapTime = 0f;
    }
}
