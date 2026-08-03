using UnityEngine;
using PartyRacers.AI;

/// <summary>
/// Mede o quanto um kart avançou na corrida. Fonte ÚNICA de verdade para a posição — a HUD, a tela
/// de resultado e a mira do disco voador consultam daqui, então os três sempre concordam.
///
/// A medida tem dois níveis:
///
///  • <b>Grosso</b> (voltas + checkpoints validados): é o que impede atalho. Nunca é substituído
///    pelo nível fino, senão dava para "ganhar posição" cortando caminho.
///
///  • <b>Fino</b> (distância percorrida ao longo do traçado): desempata quem está entre os mesmos
///    dois checkpoints. Antes o desempate era a distância em LINHA RETA até o próximo checkpoint —
///    numa pista sinuosa isso inverte posições descaradamente, porque um kart já bem à frente pela
///    pista pode estar em linha reta mais longe do checkpoint do que outro que ficou para trás numa
///    curva. Era essa a razão de estar em primeiro e a HUD mostrar quinto.
/// </summary>
public static class RaceProgress
{
    public readonly struct Sample
    {
        public Sample(float coarse, float fine, bool finished, float finishTime, int networkRank)
        {
            Coarse = coarse;
            Fine = fine;
            Finished = finished;
            FinishTime = finishTime;
            NetworkRank = networkRank;
        }

        /// <summary>Voltas e checkpoints validados. Domina a comparação.</summary>
        public float Coarse { get; }
        /// <summary>Avanço dentro do trecho atual. Só desempata.</summary>
        public float Fine { get; }
        public bool Finished { get; }
        public float FinishTime { get; }
        /// <summary>Posição final decidida pelo servidor (0 = não decidida).</summary>
        public int NetworkRank { get; }
    }

    public static Sample Measure(KartController kart, KartRaceTracker tracker)
    {
        if (tracker == null)
            return new Sample(0f, 0f, false, float.PositiveInfinity, 0);

        int totalCheckpoints = Mathf.Max(1, tracker.TotalCheckpoints);

        if (tracker.RaceFinished)
        {
            float coarseFinal = (tracker.TotalLaps + 1) * totalCheckpoints;
            return new Sample(
                coarseFinal,
                0f,
                true,
                tracker.FinishRealtime >= 0f ? tracker.FinishRealtime : float.PositiveInfinity,
                tracker.NetworkRank);
        }

        int currentLap = Mathf.Max(1, tracker.CurrentLap);
        int nextCheckpoint = tracker.NextCheckpointIndex;
        int completedThisLap = nextCheckpoint <= 0
            ? totalCheckpoints - 1
            : Mathf.Clamp(nextCheckpoint - 1, 0, totalCheckpoints - 1);

        float coarse = (currentLap - 1) * totalCheckpoints + completedThisLap;
        return new Sample(coarse, MeasureFine(kart, tracker), false, float.PositiveInfinity, 0);
    }

    /// <summary>
    /// Quanto falta, ANDANDO PELA PISTA, até o próximo checkpoint — com sinal invertido, para que
    /// "maior" continue significando "mais à frente".
    ///
    /// Medir a distância bruta percorrida no traçado não serve: ela zera ao passar pela origem da
    /// rota. Dois karts no mesmo trecho podiam marcar 1830 m e 10 m, e o que estava atrás era
    /// classificado à frente. Medindo o que FALTA até o próximo checkpoint, a volta ao zero deixa
    /// de existir.
    /// </summary>
    private static float MeasureFine(KartController kart, KartRaceTracker tracker)
    {
        if (kart == null)
            return 0f;

        RaceCheckpoint next = FindCheckpoint(tracker.NextCheckpointIndex);

        if (BotRacingLine.TryGetNearestRouteInfo(
                kart.transform.position,
                out _,
                out float kartDistance,
                out float routeLength,
                out bool looped)
            && next != null
            && TryGetCheckpointRouteDistance(next, out float checkpointDistance))
        {
            return -ForwardDistance(kartDistance, checkpointDistance, routeLength, looped);
        }

        // Cenas sem BotRacingLine (pistas antigas) caem no critério anterior: quanto mais perto do
        // próximo checkpoint, melhor. Impreciso, mas é o que dá para medir sem traçado.
        if (next != null)
            return -Vector3.Distance(kart.transform.position, next.transform.position);

        return kart.SpeedKmh * 0.01f;
    }

    private static float ForwardDistance(float from, float to, float length, bool looped)
    {
        if (!looped || length <= 0.01f)
            return Mathf.Max(0f, to - from);

        float delta = (to - from) % length;
        if (delta < 0f)
            delta += length;

        return delta;
    }

    // A posição de cada checkpoint sobre o traçado é fixa: mede uma vez por cena e reaproveita.
    private static readonly System.Collections.Generic.Dictionary<int, float> checkpointRouteDistance =
        new System.Collections.Generic.Dictionary<int, float>();

    private static bool TryGetCheckpointRouteDistance(RaceCheckpoint checkpoint, out float distance)
    {
        int key = checkpoint.GetInstanceID();
        if (checkpointRouteDistance.TryGetValue(key, out distance))
            return true;

        if (!BotRacingLine.TryGetNearestRouteInfo(checkpoint.transform.position, out _, out distance, out _, out _))
            return false;

        checkpointRouteDistance[key] = distance;
        return true;
    }

    /// <summary>
    /// Ordena do primeiro para o último. Quem terminou vem sempre antes de quem ainda corre, e a
    /// ordem de chegada é a que o servidor decidiu — estatística de tempo nunca reordena o pódio.
    /// </summary>
    public static int Compare(Sample a, Sample b)
    {
        if (a.Finished != b.Finished)
            return a.Finished ? -1 : 1;

        if (a.Finished)
        {
            // Classificação oficial do servidor tem prioridade sobre o relógio local.
            if (a.NetworkRank > 0 && b.NetworkRank > 0 && a.NetworkRank != b.NetworkRank)
                return a.NetworkRank.CompareTo(b.NetworkRank);

            int byArrival = a.FinishTime.CompareTo(b.FinishTime);
            if (byArrival != 0)
                return byArrival;

            return 0;
        }

        int byCoarse = b.Coarse.CompareTo(a.Coarse);
        if (byCoarse != 0)
            return byCoarse;

        return b.Fine.CompareTo(a.Fine);
    }

    private static RaceCheckpoint[] cachedCheckpoints;
    private static int cachedCheckpointFrame = -1;

    /// <summary>Limpa os caches ao trocar de pista (os checkpoints da cena anterior morreram).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCaches()
    {
        cachedCheckpoints = null;
        cachedCheckpointFrame = -1;
        checkpointRouteDistance.Clear();
        UnityEngine.SceneManagement.SceneManager.activeSceneChanged += (_, __) =>
        {
            cachedCheckpoints = null;
            cachedCheckpointFrame = -1;
            checkpointRouteDistance.Clear();
        };
    }

    private static RaceCheckpoint FindCheckpoint(int index)
    {
        if (cachedCheckpoints == null || cachedCheckpointFrame != Time.frameCount)
        {
            cachedCheckpoints = Object.FindObjectsByType<RaceCheckpoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            cachedCheckpointFrame = Time.frameCount;
        }

        for (int i = 0; i < cachedCheckpoints.Length; i++)
        {
            RaceCheckpoint checkpoint = cachedCheckpoints[i];
            if (checkpoint != null && checkpoint.CheckpointIndex == index)
                return checkpoint;
        }

        return null;
    }
}
