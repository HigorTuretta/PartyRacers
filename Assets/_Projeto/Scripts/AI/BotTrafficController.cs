using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Gerente GLOBAL de faixas/ocupação dos bots (anti-scrum + escolha de faixa DIRIGÍVEL). Divide
    /// a largura útil local da pista em FAIXAS discretas e entrega a cada bot a faixa que é, ao mesmo
    /// tempo, (a) DIRIGÍVEL — livre de parede/peg/obstáculo à frente, sondada por spherecast — e
    /// (b) LIVRE de outros bots. Assim o pelotão se ESPALHA e ATRAVESSA gargalos/campos de pegs em
    /// vez de convergir todo na mesma linha e empilhar (a causa do "bolo": 15 bots na mesma linha →
    /// o da frente encosta na parede/peg e a fila inteira trava).
    ///
    /// Não é waypoint nem collider: é uma camada de DECISÃO. O bot dirige pela física normal e os
    /// sensores continuam sendo a palavra final de segurança. A diferença para "só offset lateral +
    /// spherecast" é que aqui a escolha é GLOBAL: a faixa considera onde os OUTROS bots estão, então
    /// dois bots não escolhem o mesmo buraco entre os pegs ao mesmo tempo.
    ///
    /// Puramente geométrico/local e agnóstico de rota (funciona na principal e em qualquer branch):
    ///  1. Cada bot reporta a cada frame pose, velocidade, offset lateral ao centro da rota e o id
    ///     do caminho atual.
    ///  2. Ao consultar, para cada faixa candidata o controlador (i) sonda obstáculos à frente
    ///     NAQUELA faixa e (ii) soma a ocupação por vizinhos no mesmo caminho dentro do corredor à
    ///     frente; devolve a faixa de menor custo (dirigível + livre + perto da linha natural, com
    ///     histerese para não oscilar).
    ///  3. Trecho largo → várias faixas (pelotão abre); gargalo de 1 faixa → sinaliza CEDER (yield)
    ///     ao de trás, formando fila funcional EM MOVIMENTO em vez de bolo imóvel.
    ///
    /// Singleton autocriado em runtime. Aditivo: ausente → bots caem no comportamento antigo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BotTrafficController : MonoBehaviour
    {
        [Header("Geometria das faixas")]
        [Tooltip("Largura (m) de cada faixa dirigível. ~largura de um kart + folga.")]
        [SerializeField] private float laneWidth = 2.4f;
        [Tooltip("Número MÁXIMO de faixas consideradas mesmo em pista muito larga.")]
        [SerializeField] private int maxLanes = 5;

        [Header("Janela de tráfego")]
        [Tooltip("Quão à frente (m) um vizinho ainda conta para reservar/escolher faixa.")]
        [SerializeField] private float aheadWindow = 13f;
        [Tooltip("Quão atrás (m) um vizinho lado a lado ainda conta (evita fechar em cima dele).")]
        [SerializeField] private float behindWindow = 3.5f;
        [Tooltip("Raio (m) para descartar rapidamente vizinhos distantes.")]
        [SerializeField] private float neighborRadius = 18f;

        [Header("Sonda de obstáculo por faixa")]
        [Tooltip("Raio da spherecast que testa se a faixa está dirigível à frente.")]
        [SerializeField] private float probeRadius = 0.5f;
        [Tooltip("Acima desta normal.y a superfície é chão/rampa (não bloqueia a faixa).")]
        [SerializeField, Range(0f, 1f)] private float wallMaxNormalY = 0.6f;
        [Tooltip("Peso do bloqueio de obstáculo no custo da faixa (alto = evita faixa bloqueada).")]
        [SerializeField] private float blockedLaneCost = 3.2f;

        public sealed class Record
        {
            public BotDriverController Bot;
            public Vector3 Position;
            public Vector3 Forward;
            public float CenterOffset;   // offset lateral assinado em relação ao centro da rota (m)
            public float SpeedKmh;
            public int PathId;           // -1 = linha principal, >=0 = branch
            public float TargetLane;     // faixa-alvo escolhida no último frame (m)
            public float UpdateTime;
        }

        private readonly List<Record> records = new List<Record>();
        private readonly Dictionary<BotDriverController, Record> byBot = new Dictionary<BotDriverController, Record>();

        private static BotTrafficController instance;

        public static BotTrafficController Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = FindAnyObjectByType<BotTrafficController>(FindObjectsInactive.Exclude);
                if (instance == null && Application.isPlaying)
                {
                    var go = new GameObject("BotTrafficController");
                    instance = go.AddComponent<BotTrafficController>();
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public void Register(BotDriverController bot)
        {
            if (bot == null || byBot.ContainsKey(bot))
                return;

            var record = new Record { Bot = bot, UpdateTime = Time.time };
            byBot.Add(bot, record);
            records.Add(record);
        }

        public void Unregister(BotDriverController bot)
        {
            if (bot == null || !byBot.TryGetValue(bot, out Record record))
                return;

            byBot.Remove(bot);
            records.Remove(record);
        }

        /// <summary>Atualiza a pose/estado do bot (chamado todo frame pelo próprio bot).</summary>
        public void Report(BotDriverController bot, Vector3 position, Vector3 forward, float centerOffset, float speedKmh, int pathId)
        {
            if (bot == null)
                return;

            if (!byBot.TryGetValue(bot, out Record record))
            {
                Register(bot);
                record = byBot[bot];
            }

            record.Position = position;
            forward.y = 0f;
            record.Forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            record.CenterOffset = centerOffset;
            record.SpeedKmh = speedKmh;
            record.PathId = pathId;
            record.UpdateTime = Time.time;
        }

        /// <summary>
        /// Escolhe a melhor faixa (offset lateral assinado, em m) para o bot, dentro de
        /// [-laneHalfWidth, +laneHalfWidth]: dirigível (livre de obstáculo à frente, sondado por
        /// spherecast) E livre de outros bots E perto da linha natural. Devolve também se o bot deve
        /// CEDER por haver um carro mais lento logo à frente na faixa escolhida (fila), e a folga.
        /// </summary>
        public float QueryLane(
            BotDriverController bot,
            Vector3 myPos,
            Vector3 probeOrigin,
            Vector3 myForward,
            Vector3 myRight,
            float myCenterOffset,
            float laneHalfWidth,
            float naturalBias,
            float currentTarget,
            float probeDistance,
            LayerMask probeMask,
            out bool yield,
            out float gapAhead,
            out float leaderSpeedKmh)
        {
            yield = false;
            gapAhead = float.MaxValue;
            leaderSpeedKmh = 0f;

            myForward.y = 0f;
            myRight.y = 0f;
            if (myForward.sqrMagnitude < 0.0001f || laneHalfWidth <= 0.05f)
                return Mathf.Clamp(naturalBias, -laneHalfWidth, laneHalfWidth);
            myForward.Normalize();
            myRight.Normalize();

            int pathId = byBot.TryGetValue(bot, out Record self) ? self.PathId : -1;
            float mySpeed = self != null ? self.SpeedKmh : 0f;

            int laneCount = Mathf.Clamp(Mathf.RoundToInt((laneHalfWidth * 2f) / Mathf.Max(0.5f, laneWidth)), 1, maxLanes);
            if (laneCount <= 1)
            {
                EvaluateYield(bot, myPos, myForward, myRight, myCenterOffset, 0f, pathId, mySpeed, laneWidth,
                    out yield, out gapAhead, out leaderSpeedKmh);
                return 0f;
            }

            float step = (laneHalfWidth * 2f) / (laneCount - 1);

            float bestOffset = Mathf.Clamp(naturalBias, -laneHalfWidth, laneHalfWidth);
            float bestCost = float.MaxValue;

            for (int l = 0; l < laneCount; l++)
            {
                float offset = -laneHalfWidth + step * l;
                float cost = 0f;

                // (i) FAIXA DIRIGÍVEL? sonda obstáculo à frente naquela faixa (peg/parede/borda).
                if (probeDistance > 0.5f)
                {
                    Vector3 originLane = probeOrigin + myRight * (offset - myCenterOffset);
                    if (Physics.SphereCast(originLane, probeRadius, myForward, out RaycastHit hit, probeDistance, probeMask, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider != null
                            && hit.collider.GetComponentInParent<KartController>() == null
                            && hit.normal.y <= wallMaxNormalY)
                        {
                            float block = 1f - Mathf.Clamp01(hit.distance / probeDistance);
                            cost += blockedLaneCost * block;
                        }
                    }
                }

                // (ii) OCUPAÇÃO por vizinhos no mesmo caminho, no corredor à frente.
                for (int i = 0; i < records.Count; i++)
                {
                    Record other = records[i];
                    if (other.Bot == bot || other.PathId != pathId)
                        continue;

                    Vector3 rel = other.Position - myPos;
                    rel.y = 0f;
                    if (rel.sqrMagnitude > neighborRadius * neighborRadius)
                        continue;

                    float longi = Vector3.Dot(rel, myForward);
                    if (longi > aheadWindow || longi < -behindWindow)
                        continue;

                    float lat = Vector3.Dot(rel, myRight);
                    float otherOffset = myCenterOffset + lat;
                    float lateralDelta = Mathf.Abs(offset - otherOffset);
                    if (lateralDelta > laneWidth * 0.85f)
                        continue;

                    float lateralWeight = 1f - Mathf.Clamp01(lateralDelta / (laneWidth * 0.85f));
                    float longiWeight = longi >= 0f ? Mathf.Lerp(1f, 0.3f, longi / aheadWindow) : 0.45f;
                    float weight = lateralWeight * longiWeight;

                    if (longi > 0.5f && other.SpeedKmh < mySpeed - 8f)
                        weight *= 1.6f;

                    cost += weight;
                }

                // (iii) Preferências: linha natural do bot + histerese (não troca de faixa à toa).
                cost += Mathf.Abs(offset - naturalBias) * 0.12f;
                cost += Mathf.Abs(offset - currentTarget) * 0.20f;

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestOffset = offset;
                }
            }

            EvaluateYield(bot, myPos, myForward, myRight, myCenterOffset, bestOffset, pathId, mySpeed, laneWidth,
                out yield, out gapAhead, out leaderSpeedKmh);

            if (self != null)
                self.TargetLane = bestOffset;

            return bestOffset;
        }

        // Há um carro mais lento logo à frente, na faixa escolhida? Então CEDE (mantém distância e
        // alivia o gás) em vez de empurrar — formando fila funcional em movimento.
        private void EvaluateYield(
            BotDriverController bot,
            Vector3 myPos,
            Vector3 myForward,
            Vector3 myRight,
            float myCenterOffset,
            float chosenOffset,
            int pathId,
            float mySpeed,
            float laneTol,
            out bool yield,
            out float gapAhead,
            out float leaderSpeedKmh)
        {
            yield = false;
            gapAhead = float.MaxValue;
            leaderSpeedKmh = 0f;

            for (int i = 0; i < records.Count; i++)
            {
                Record other = records[i];
                if (other.Bot == bot || other.PathId != pathId)
                    continue;

                Vector3 rel = other.Position - myPos;
                rel.y = 0f;
                float longi = Vector3.Dot(rel, myForward);
                if (longi < 0.4f || longi > aheadWindow)
                    continue;

                float lat = Vector3.Dot(rel, myRight);
                float otherOffset = myCenterOffset + lat;
                if (Mathf.Abs(otherOffset - chosenOffset) > laneTol * 0.6f)
                    continue;

                if (longi < gapAhead)
                {
                    gapAhead = longi;
                    leaderSpeedKmh = other.SpeedKmh;
                    yield = other.SpeedKmh < mySpeed - 5f || other.SpeedKmh < 30f;
                }
            }
        }
    }
}
