using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>Como o bot enxerga o piso de uma estação do traçado.</summary>
    public enum BotSurface
    {
        /// <summary>Piso normal e contínuo.</summary>
        Normal,
        /// <summary>Subida/descida acentuada — a face da rampa NÃO é parede.</summary>
        Rampa,
        /// <summary>Decolagem de um vão que a rota atravessa de propósito (saltável).</summary>
        Decolagem,
        /// <summary>O centro da rota não tem chão: vão/buraco.</summary>
        Vao,
        /// <summary>Pouso logo depois de um vão.</summary>
        Pouso,
        /// <summary>
        /// Degrau vertical transponível — a costura entre duas peças de pista com alturas
        /// diferentes. Parece parede para um sensor, mas o kart sobe com velocidade.
        /// </summary>
        Degrau
    }

    /// <summary>
    /// Uma amostra do traçado a cada ~1 m, com o que foi MEDIDO do mundo naquele ponto.
    /// É isto que substitui o "adivinhar por sensor a 60 km/h": a geometria estática da pista
    /// (largura, buracos, rampas, paredes) é conhecida ANTES de o bot chegar lá.
    /// </summary>
    [System.Serializable]
    public struct BotTrackStation
    {
        public float Distance;        // m ao longo do traçado
        public Vector3 Position;      // centro da rota
        public Vector3 Tangent;       // planar, normalizado
        public float Curvature;       // 1/m com sinal (+ = curva para a DIREITA)
        public float Grade;           // dy/ds
        public float GroundY;         // altura do chão medida no centro da rota
        public float StepRise;        // degrau (m) para a próxima estação, se houver
        public float HalfWidthLeft;   // m livres contíguos à esquerda do centro
        public float HalfWidthRight;  // m livres contíguos à direita
        public uint FreeMask;         // bit k = amostra lateral k tem chão e está desimpedida
        public float SafeSpeedKmh;    // perfil de velocidade resolvido (curvatura + largura + frenagem)
        public float TakeoffSpeedKmh; // >0 em Decolagem: velocidade mínima para vencer o vão
        public BotSurface Surface;

        public float UsableWidth => HalfWidthLeft + HalfWidthRight;
    }

    /// <summary>
    /// Uma curva identificada no traçado — não um "ângulo lido por frame", mas um trecho com
    /// começo, ápice e fim. É a unidade de decisão do drift: o bot se COMPROMETE com a curva
    /// inteira, em vez de reagir quadro a quadro (que era a origem do zig-zag).
    /// </summary>
    [System.Serializable]
    public struct BotCorner
    {
        public float EntryDistance;
        public float ApexDistance;
        public float ExitDistance;
        public float MinRadius;       // m
        public float TotalTurnDeg;    // giro acumulado
        public int Direction;         // +1 direita, -1 esquerda
        public float ApexSpeedKmh;
        public bool WantsDrift;

        public float Length => ExitDistance - EntryDistance;
    }

    /// <summary>
    /// Perfil bakeado de uma pista: a "memória" que os bots têm do circuito.
    ///
    /// Motivação: os bots erravam porque tinham de DEDUZIR a pista em tempo real a partir de
    /// spherecasts a 140 km/h — e a dedução falha exatamente onde a pista é difícil. Uma face
    /// inclinada podia ser rampa ou muro; um vazio à frente podia ser um salto de propósito ou um
    /// precipício; uma curva era só um ângulo escalar que oscilava de frame em frame.
    ///
    /// Aqui a geometria ESTÁTICA é medida uma vez (no Editor ou no início da corrida) e
    /// consultada por distância de rota. Os sensores em runtime continuam existindo, mas só para
    /// o que é realmente dinâmico: outros karts e obstáculos que se movem.
    /// </summary>
    public sealed class BotTrackProfile
    {
        public const int LaneSamples = 25;   // amostras laterais por estação (cabe em uint)
        public const float KartWidth = 2.2f; // largura mínima de uma faixa que serve de passagem

        private readonly BotTrackStation[] stations;
        private readonly BotCorner[] corners;

        public float Spacing { get; }
        public float TotalLength { get; }
        public bool Looped { get; }
        public float LaneStep { get; }        // m entre amostras laterais
        public float CorridorHalfWidth { get; }
        public IReadOnlyList<BotCorner> Corners => corners;
        public int StationCount => stations.Length;
        public bool IsValid => stations != null && stations.Length >= 4;

        /// <summary>Trechos que o baker julga intransponíveis — feedback de level design.</summary>
        public IReadOnlyList<string> Warnings { get; }

        public BotTrackProfile(
            BotTrackStation[] stations,
            BotCorner[] corners,
            float spacing,
            float totalLength,
            bool looped,
            float laneStep,
            float corridorHalfWidth,
            List<string> warnings)
        {
            this.stations = stations;
            this.corners = corners ?? new BotCorner[0];
            Spacing = Mathf.Max(0.1f, spacing);
            TotalLength = totalLength;
            Looped = looped;
            LaneStep = laneStep;
            CorridorHalfWidth = corridorHalfWidth;
            Warnings = warnings ?? new List<string>();
        }

        // ------------------------------------------------------------------ consultas
        private int IndexAt(float distance)
        {
            if (stations.Length == 0)
                return 0;

            float d = Looped ? Mathf.Repeat(distance, Mathf.Max(0.01f, TotalLength)) : distance;
            int i = Mathf.RoundToInt(d / Spacing);
            return Looped
                ? ((i % stations.Length) + stations.Length) % stations.Length
                : Mathf.Clamp(i, 0, stations.Length - 1);
        }

        public BotTrackStation StationAt(float distance) => stations[IndexAt(distance)];

        /// <summary>Menor velocidade segura entre aqui e 'ahead' metros à frente.</summary>
        public float SpeedAheadKmh(float distance, float ahead)
        {
            float best = float.MaxValue;
            int steps = Mathf.Clamp(Mathf.CeilToInt(ahead / Spacing), 1, 400);
            for (int k = 0; k <= steps; k++)
            {
                float v = stations[IndexAt(distance + k * Spacing)].SafeSpeedKmh;
                if (v < best) best = v;
            }
            return best;
        }

        /// <summary>Altura da rota naquele ponto — base do detector de queda.</summary>
        public float RouteHeightAt(float distance) => stations[IndexAt(distance)].Position.y;

        /// <summary>
        /// Existe, entre aqui e 'ahead' m, alguma superfície que SOBE mas é transponível — rampa,
        /// lip de decolagem ou degrau entre peças de pista?
        ///
        /// É a consulta que impede o sensor de ler essas faces como parede. Era essa confusão que
        /// fazia o bot desviar na base da rampa e agarrar nela, e travar contra a costura entre
        /// duas peças de pista de alturas diferentes.
        /// </summary>
        public bool HasRampAhead(float distance, float ahead)
        {
            int steps = Mathf.Clamp(Mathf.CeilToInt(ahead / Spacing), 1, 200);
            for (int k = 0; k <= steps; k++)
            {
                BotSurface s = stations[IndexAt(distance + k * Spacing)].Surface;
                if (s == BotSurface.Rampa || s == BotSurface.Decolagem || s == BotSurface.Degrau)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Velocidade de decolagem exigida pelo próximo vão dentro de 'ahead' m (0 se não houver).
        /// </summary>
        public float RequiredTakeoffKmh(float distance, float ahead, out float distanceToTakeoff)
        {
            distanceToTakeoff = float.MaxValue;
            int steps = Mathf.Clamp(Mathf.CeilToInt(ahead / Spacing), 1, 200);
            for (int k = 0; k <= steps; k++)
            {
                BotTrackStation st = stations[IndexAt(distance + k * Spacing)];
                if (st.TakeoffSpeedKmh > 1f)
                {
                    distanceToTakeoff = k * Spacing;
                    return st.TakeoffSpeedKmh;
                }
            }
            return 0f;
        }

        // Rascunho da busca de corredor. Reutilizado entre bots: as chamadas acontecem em
        // sequência na thread principal, então um buffer compartilhado evita alocar por frame.
        private readonly float[] dpNext = new float[LaneSamples];
        private readonly float[] dpCur = new float[LaneSamples];

        private const float BlockedCost = 6f;      // atravessar uma amostra intransitável
        private const float DriftCost = 0.16f;     // andar de lado custa (prefere linha reta)
        private const float PreferenceCost = 0.55f; // por metro de desvio da linha desejada

        /// <summary>
        /// Escolhe o offset lateral (m do centro da rota) percorrendo um HORIZONTE de estações,
        /// não apenas a estação atual.
        ///
        /// Por que um horizonte: medido na área da chegada desta pista, as passagens entre os
        /// obstáculos têm 3 a 4,5 m e MUDAM DE LADO a cada metro. Escolher a melhor faixa estação
        /// por estação faz o alvo pular de um lado para o outro e o bot serpenteia até bater.
        /// Aqui é uma programação dinâmica curta (25 faixas × ~20 estações) que acha a linha
        /// contínua mais barata até o fim do horizonte, permitindo uma amostra de deslocamento
        /// lateral por metro — ou seja, ela sabe costurar um slalom.
        ///
        /// Uma faixa só conta como transitável se ela E as vizinhas estiverem livres: o kart tem
        /// 2,2 m de largura, não a largura de uma amostra.
        /// </summary>
        public float BestLateralOffset(
            float distance, float horizonMeters, float preferredOffset, float maxOffset,
            float speedMps, float maxLateralRate)
        {
            int center = LaneSamples / 2;
            float clampedPref = Mathf.Clamp(preferredOffset, -maxOffset, maxOffset);
            int steps = Mathf.Clamp(Mathf.RoundToInt(horizonMeters / Spacing), 1, 60);

            // A cada quantas estações o plano pode mudar de faixa SEM pedir do kart um
            // deslocamento lateral que ele não consegue executar. Sem esta trava a busca traçava
            // linhas lindas que exigiam 14 m/s de translação lateral: o alvo ia, o kart não
            // acompanhava, e ele passava a corrida inteira atrás do próprio plano — dentro do
            // obstáculo, não da passagem.
            int stride = 1;
            if (maxLateralRate > 0.01f && speedMps > 0.5f)
                stride = Mathf.Clamp(Mathf.CeilToInt(LaneStep * speedMps / (maxLateralRate * Spacing)), 1, 12);

            for (int k = 0; k < LaneSamples; k++)
                dpNext[k] = 0f;

            // Do fim do horizonte para trás.
            for (int i = steps; i >= 0; i--)
            {
                uint mask = stations[IndexAt(distance + i * Spacing)].FreeMask;
                bool canShift = (i % stride) == 0;

                for (int k = 0; k < LaneSamples; k++)
                {
                    float here = Fits(mask, k) ? 0f : BlockedCost;

                    if (i == steps)
                    {
                        dpCur[k] = here;
                        continue;
                    }

                    float best = dpNext[k];
                    if (canShift)
                    {
                        if (k > 0 && dpNext[k - 1] + DriftCost < best) best = dpNext[k - 1] + DriftCost;
                        if (k < LaneSamples - 1 && dpNext[k + 1] + DriftCost < best) best = dpNext[k + 1] + DriftCost;
                    }
                    dpCur[k] = here + best;
                }

                for (int k = 0; k < LaneSamples; k++)
                    dpNext[k] = dpCur[k];
            }

            // Entre as linhas viáveis, a mais próxima da que o bot queria correr.
            int bestK = center;
            float bestScore = float.MaxValue;
            for (int k = 0; k < LaneSamples; k++)
            {
                float offset = (k - center) * LaneStep;
                if (Mathf.Abs(offset) > maxOffset)
                    continue;

                float score = dpNext[k] + PreferenceCost * Mathf.Abs(offset - clampedPref);
                if (score < bestScore) { bestScore = score; bestK = k; }
            }

            return (bestK - center) * LaneStep;
        }

        /// <summary>A amostra k e suas vizinhas estão livres? (o kart não tem a largura de uma amostra)</summary>
        private static bool Fits(uint mask, int k)
        {
            if (k <= 0 || k >= LaneSamples - 1)
                return false;                       // beirada do corredor sondado: nunca "cabe"
            return (mask & (1u << (k - 1))) != 0u
                && (mask & (1u << k)) != 0u
                && (mask & (1u << (k + 1))) != 0u;
        }

        /// <summary>Curva ativa ou logo à frente (dentro de 'ahead' m), se houver.</summary>
        public bool TryGetCorner(float distance, float ahead, out BotCorner corner)
        {
            corner = default;
            if (corners.Length == 0)
                return false;

            float bestGap = float.MaxValue;
            bool found = false;
            for (int i = 0; i < corners.Length; i++)
            {
                BotCorner c = corners[i];
                float toEntry = Forward(distance, c.EntryDistance);
                float toExit = Forward(distance, c.ExitDistance);

                // Dentro da curva: entrada já passou (toEntry grande, quase uma volta) e a saída
                // ainda está à frente e perto.
                bool inside = toExit <= c.Length + 1f && toExit > 0f;
                if (inside)
                {
                    corner = c;
                    return true;
                }

                if (toEntry <= ahead && toEntry < bestGap)
                {
                    bestGap = toEntry;
                    corner = c;
                    found = true;
                }
            }
            return found;
        }

        /// <summary>Distância para frente de 'from' até 'to', ciente do loop (sempre >= 0).</summary>
        public float Forward(float from, float to)
        {
            float d = to - from;
            if (!Looped)
                return d;
            return Mathf.Repeat(d, Mathf.Max(0.01f, TotalLength));
        }
    }
}
