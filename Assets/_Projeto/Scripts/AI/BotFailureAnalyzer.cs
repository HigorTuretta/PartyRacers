using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Heatmap de FALHAS dos bots por trecho da rota (distância ao longo da spline/linha de
    /// corrida). Em vez de classificar a geometria estaticamente (ruidoso — a linha sempre passa
    /// perto das paredes do kit modular), este analisador mede ONDE os bots realmente falham em
    /// jogo: ficam presos, recuperam, respawnam, andam devagar ou formam scrum. É a prova-real que
    /// aponta os trechos a corrigir (mover pontos da racing line, abrir corredor, marcar zona).
    ///
    /// Divide a rota em "buckets" de N metros e acumula, por bucket: amostras, velocidade média,
    /// tempo abaixo de 70, amostras "preso", recuperações, respawns e amostras em scrum. Loga os
    /// piores buckets (com a posição mundial média e o ponto P mais próximo) periodicamente e no
    /// fim — saída direta para o relatório de validação.
    ///
    /// Autobootstrap: o RaceBotManager garante a instância quando a telemetria está ligada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotFailureAnalyzer : MonoBehaviour
    {
        [SerializeField] private float bucketSizeMeters = 10f;
        [SerializeField] private float sampleInterval = 0.25f;
        [SerializeField] private float logInterval = 60f;
        [SerializeField] private int topK = 14;
        [Tooltip("Velocidade (km/h) abaixo da qual um bot conta como 'scrum' se houver vários no mesmo bucket.")]
        [SerializeField] private float scrumSpeedKmh = 30f;
        [SerializeField] private int scrumMinBots = 3;

        private sealed class Bucket
        {
            public int Samples;
            public float SumSpeed;
            public int Low70;
            public int Stuck;
            public int Recoveries;
            public int Respawns;
            public int Scrum;
            public double SumX, SumZ;
        }

        private readonly Dictionary<int, Bucket> buckets = new Dictionary<int, Bucket>();
        private readonly Dictionary<BotDriverController, int> lastRecovery = new Dictionary<BotDriverController, int>();
        private readonly Dictionary<BotDriverController, int> lastRespawn = new Dictionary<BotDriverController, int>();
        private readonly Dictionary<int, int> tickBucketCount = new Dictionary<int, int>();

        private RaceBotManager manager;
        private List<Vector3> routePoints;
        private float nextSample;
        private float nextLog;

        private static BotFailureAnalyzer instance;

        public static BotFailureAnalyzer EnsureInstance()
        {
            if (instance != null)
                return instance;

            instance = FindAnyObjectByType<BotFailureAnalyzer>(FindObjectsInactive.Exclude);
            if (instance == null && Application.isPlaying)
            {
                var go = new GameObject("BotFailureAnalyzer");
                instance = go.AddComponent<BotFailureAnalyzer>();
            }

            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            nextLog = Time.time + logInterval;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void Update()
        {
            if (Time.time < nextSample)
                return;
            nextSample = Time.time + Mathf.Max(0.05f, sampleInterval);

            if (manager == null)
                manager = FindAnyObjectByType<RaceBotManager>(FindObjectsInactive.Exclude);
            if (manager == null || manager.SpawnedBots == null || manager.SpawnedBots.Count == 0)
                return;

            if (routePoints == null)
            {
                BotRacingLine line = FindAnyObjectByType<BotRacingLine>(FindObjectsInactive.Exclude);
                routePoints = line != null ? line.GetWorldPoints() : new List<Vector3>();
            }

            tickBucketCount.Clear();

            var bots = manager.SpawnedBots;
            for (int i = 0; i < bots.Count; i++)
            {
                KartController kart = bots[i];
                if (kart == null)
                    continue;

                BotDriverController driver = kart.GetComponent<BotDriverController>();
                BotPathFollower follower = kart.GetComponent<BotPathFollower>();
                if (driver == null || follower == null || !follower.IsReady)
                    continue;

                int bi = Mathf.FloorToInt(follower.CurrentDistanceOnPath / Mathf.Max(1f, bucketSizeMeters));
                Bucket b = Get(bi);

                float speed = kart.SpeedKmh;
                b.Samples++;
                b.SumSpeed += speed;
                if (speed < 70f) b.Low70++;

                bool stuck = driver.State == BotDriverController.BotState.Reversing
                    || driver.State == BotDriverController.BotState.Respawning
                    || (speed < 8f && driver.LastInput.Throttle > 0.35f)
                    || driver.SecondsWithoutProgress > 2.5f;
                if (stuck) b.Stuck++;

                Vector3 p = kart.transform.position;
                b.SumX += p.x; b.SumZ += p.z;

                if (lastRecovery.TryGetValue(driver, out int lr)) b.Recoveries += Mathf.Max(0, driver.RecoveryCount - lr);
                lastRecovery[driver] = driver.RecoveryCount;
                if (lastRespawn.TryGetValue(driver, out int lp)) b.Respawns += Mathf.Max(0, driver.RespawnCount - lp);
                lastRespawn[driver] = driver.RespawnCount;

                if (speed < scrumSpeedKmh)
                    tickBucketCount[bi] = tickBucketCount.TryGetValue(bi, out int c) ? c + 1 : 1;
            }

            foreach (var kv in tickBucketCount)
                if (kv.Value >= scrumMinBots)
                    Get(kv.Key).Scrum += kv.Value;

            if (Time.time >= nextLog)
            {
                nextLog = Time.time + logInterval;
                LogWorst();
            }
        }

        private Bucket Get(int index)
        {
            if (!buckets.TryGetValue(index, out Bucket b))
            {
                b = new Bucket();
                buckets[index] = b;
            }
            return b;
        }

        private static float Score(Bucket b)
        {
            return b.Stuck * 1f + b.Recoveries * 8f + b.Respawns * 15f + b.Low70 * 0.15f + b.Scrum * 0.4f;
        }

        /// <summary>Loga os piores trechos por score de falha. Chamado periodicamente e no fim.</summary>
        public void LogWorst()
        {
            if (buckets.Count == 0)
                return;

            var list = new List<KeyValuePair<int, Bucket>>(buckets);
            list.Sort((a, b) => Score(b.Value).CompareTo(Score(a.Value)));

            int k = Mathf.Min(topK, list.Count);
            var sb = new System.Text.StringBuilder();
            sb.Append("[BotFailureAnalyzer] PIORES TRECHOS (bucket ").Append(bucketSizeMeters.ToString("F0")).Append("m): ");
            for (int i = 0; i < k; i++)
            {
                Bucket b = list[i].Value;
                if (b.Samples == 0) continue;
                float d0 = list[i].Key * bucketSizeMeters;
                float avgSpeed = b.SumSpeed / b.Samples;
                float cx = (float)(b.SumX / b.Samples);
                float cz = (float)(b.SumZ / b.Samples);
                int pidx = NearestP(cx, cz);
                sb.Append("[").Append(d0.ToString("F0")).Append("m ~P").Append(pidx.ToString("000"))
                  .Append(" (").Append(cx.ToString("F0")).Append(",").Append(cz.ToString("F0")).Append(")")
                  .Append(" vM").Append(avgSpeed.ToString("F0"))
                  .Append(" preso").Append(b.Stuck)
                  .Append(" rec").Append(b.Recoveries)
                  .Append(" resp").Append(b.Respawns)
                  .Append(" scrum").Append(b.Scrum)
                  .Append("] ");
            }
            Debug.Log(sb.ToString());
        }

        private int NearestP(float x, float z)
        {
            if (routePoints == null || routePoints.Count == 0)
                return -1;
            int bi = 0; float bd = float.MaxValue;
            for (int i = 0; i < routePoints.Count; i++)
            {
                float dx = routePoints[i].x - x, dz = routePoints[i].z - z;
                float d = dx * dx + dz * dz;
                if (d < bd) { bd = d; bi = i; }
            }
            return bi;
        }
    }
}
