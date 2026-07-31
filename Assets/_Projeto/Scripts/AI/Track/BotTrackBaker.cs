using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PartyRacers.AI
{
    /// <summary>
    /// Mede a pista uma vez e produz um <see cref="BotTrackProfile"/>.
    ///
    /// A ideia central: tudo que é ESTÁTICO na pista (largura, muros, buracos, rampas, raio das
    /// curvas) pode ser medido com calma, offline, com precisão de centímetros — em vez de
    /// deduzido por spherecast a 140 km/h, que é onde a IA antiga errava. Nenhuma autoria manual
    /// é necessária: qualquer pista nova é bakeada do mesmo jeito.
    ///
    /// O que continua em runtime: karts e obstáculos que se MOVEM (moinho, taco, bolas).
    /// </summary>
    public static class BotTrackBaker
    {
        [System.Serializable]
        public class Settings
        {
            [Tooltip("Distância (m) entre estações amostradas ao longo do traçado.")]
            public float spacing = 1f;
            [Tooltip("Meia-largura (m) do corredor sondado para cada lado do centro da rota.")]
            public float corridorHalfWidth = 9f;
            [Tooltip("Camadas consideradas chão/obstáculo estático.")]
            public LayerMask mask = ~0;
            [Tooltip("Altura (m) acima da rota de onde parte a sonda vertical.")]
            public float probeUp = 2f;
            [Tooltip("Desnível (m) tolerado entre uma amostra lateral e o centro da rota. Além " +
                     "disso a amostra é buraco (abaixo) ou muro/degrau alto (acima).")]
            public float heightTolerance = 1.6f;

            [Header("Modelo dinâmico do kart (para o perfil de velocidade)")]
            public float maxSpeedKmh = 150f;
            [Tooltip("Aceleração lateral (m/s²) que o kart sustenta em curva sem drift. NÃO é um " +
                     "valor de kart real: a direção é arcade (rotação imposta + grip lateral alto), " +
                     "então o kart vira muito além de 1 g. Calibrado para reproduzir as velocidades " +
                     "de curva que os bots já praticavam (~70 km/h numa curva de 90° de raio ~13 m).")]
            public float lateralAccel = 24f;
            [Tooltip("Ganho máximo (m) de raio por usar a largura da pista para abrir a curva.")]
            public float maxApexWidthGain = 4f;
            [Tooltip("Desaceleração (m/s²) usada para calcular o PONTO DE FREADA exato.")]
            public float brakeAccel = 13f;
            [Tooltip("Aceleração (m/s²) na saída de curva.")]
            public float driveAccel = 7.5f;
            [Tooltip("Velocidade mínima que o perfil pode exigir em qualquer trecho.")]
            public float minSpeedKmh = 62f;

            [Header("Largura")]
            [Tooltip("Largura útil (m) abaixo da qual o trecho é 'apertado' e o teto de velocidade cai.")]
            public float narrowWidth = 3f;
            [Tooltip("Largura útil (m) a partir da qual não há penalidade de velocidade.")]
            public float wideWidth = 9f;
            public float narrowSpeedKmh = 70f;

            [Header("Curvas / drift")]
            [Tooltip("Raio (m) abaixo do qual a estação faz parte de uma curva.")]
            public float cornerRadius = 45f;
            [Tooltip("Raio (m) abaixo do qual a curva pede DRIFT (o grip normal não fecha o raio).")]
            public float driftRadius = 28f;
            [Tooltip("Giro acumulado (graus) mínimo para a curva valer um drift.")]
            public float driftMinTurnDeg = 40f;
            public float driftMinSpeedKmh = 50f;

            [Header("Vãos / saltos")]
            [Tooltip("Margem multiplicada sobre a velocidade balística mínima de decolagem.")]
            public float takeoffMargin = 1.3f;
            [Tooltip("Folga (m) somada ao comprimento do vão (o kart tem tamanho).")]
            public float gapClearance = 2.5f;
            [Tooltip("Largura livre (m) ao lado de um buraco que já constitui uma passagem de verdade. " +
                     "Acima disso o bot CONTORNA (mais confiável que saltar); abaixo, salta.")]
            public float bypassWidth = 3.5f;
            [Tooltip("Quanto o kart pode chegar abaixo do nível da beirada oposta e ainda 'enganchar' " +
                     "nela em vez de bater de frente. Sem esta folga um vão plano seria intransponível " +
                     "no papel — e os vãos desta pista são planos.")]
            public float landingCatchHeight = 1.2f;

            [Header("Rampas")]
            [Tooltip("Inclinação (dy/ds) a partir da qual a estação é rampa.")]
            public float rampGrade = 0.11f;

            [Header("Degraus entre peças de pista")]
            [Tooltip("Subida (m) entre estações vizinhas a partir da qual existe um degrau.")]
            public float stepMinRise = 0.2f;
            [Tooltip("Degrau (m) que o kart ainda sobe com velocidade. Acima disso é parede de " +
                     "verdade e vira aviso de level design.")]
            public float maxClimbableStep = 1.2f;
            [Tooltip("Distância (m) à frente em que se procura a passagem depois de um degrau. A " +
                     "quina de uma peça de pista pode bloquear duas ou três estações seguidas.")]
            public float stepLookAhead = 4f;
        }

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];
        private static readonly Collider[] OverlapBuffer = new Collider[8];

        /// <summary>Altura do centro do corpo do kart acima do chão, para o teste de espaço livre.</summary>
        private const float KartClearance = 0.9f;
        private const float KartRadius = 0.5f;

        // ================================================================== bake
        public static BotTrackProfile Bake(BotPath path, Settings s, IList<BotTrackZone> zones, out string report)
        {
            report = string.Empty;
            if (path == null || !path.IsValid)
                return null;

            s = s ?? new Settings();
            float spacing = Mathf.Max(0.25f, s.spacing);
            int count = Mathf.Max(4, Mathf.FloorToInt(path.TotalLength / spacing));
            var stations = new BotTrackStation[count];
            var warnings = new List<string>();

            int center = BotTrackProfile.LaneSamples / 2;
            float laneStep = s.corridorHalfWidth / center;

            // ---------- 1. geometria do traçado ----------
            for (int i = 0; i < count; i++)
            {
                float d = i * spacing;
                Vector3 p = path.PointAtDistance(d);
                Vector3 pNext = path.PointAtDistance(d + spacing);
                Vector3 pPrev = path.PointAtDistance(d - spacing);

                Vector3 t = pNext - pPrev;
                t.y = 0f;
                if (t.sqrMagnitude < 1e-4f) t = Vector3.forward;
                t.Normalize();

                stations[i] = new BotTrackStation
                {
                    Distance = d,
                    Position = p,
                    Tangent = t,
                    Grade = (pNext.y - pPrev.y) / (2f * spacing),
                    Surface = BotSurface.Normal
                };
            }

            // Curvatura assinada por três pontos (Menger), suavizada.
            for (int i = 0; i < count; i++)
            {
                Vector3 a = stations[Wrap(i - 3, count)].Position;
                Vector3 b = stations[i].Position;
                Vector3 c = stations[Wrap(i + 3, count)].Position;
                stations[i].Curvature = SignedCurvature(a, b, c);
            }
            SmoothCurvature(stations, 3);

            // ---------- 2. sondagem do corredor ----------
            for (int i = 0; i < count; i++)
            {
                Vector3 p = stations[i].Position;
                Vector3 right = Vector3.Cross(Vector3.up, stations[i].Tangent).normalized;

                uint free = 0u;
                float probeDown = s.probeUp + s.heightTolerance + 0.4f;

                // Altura do chão no CENTRO, medida antes de qualquer filtro — é a base da
                // detecção de degraus entre peças de pista.
                stations[i].GroundY = GroundBelow(p + Vector3.up * s.probeUp, probeDown, s.mask, out float centerY, out _)
                    ? centerY
                    : float.NaN;

                for (int k = 0; k < BotTrackProfile.LaneSamples; k++)
                {
                    float off = (k - center) * laneStep;
                    Vector3 sample = p + right * off + Vector3.up * s.probeUp;

                    // (i) tem chão, e no nível da rota?
                    if (!GroundBelow(sample, probeDown, s.mask, out float hitY, out Collider ground))
                        continue;                                   // buraco
                    if (Mathf.Abs(hitY - p.y) > s.heightTolerance)
                        continue;                                   // degrau alto / nível errado

                    // (ii) cabe um kart em pé aí? A sonda vertical SOZINHA não vê um muro alto:
                    //      o raio nasce dentro do collider do muro, a Unity não reporta esse hit,
                    //      o raio segue e acha o piso embaixo — e a amostra passaria por "livre".
                    //      Sem este teste a pista inteira parecia ter 18 m de largura livre.
                    if (Obstructed(new Vector3(sample.x, hitY, sample.z), ground, s.mask))
                        continue;

                    free |= 1u << k;
                }

                stations[i].FreeMask = free;

                // Largura livre CONTÍGUA a partir do centro (é o que o kart pode usar sem saltar
                // por cima de um muro ou de um buraco).
                bool centerFree = (free & (1u << center)) != 0u;
                float left = 0f, right2 = 0f;
                if (centerFree)
                {
                    for (int k = center - 1; k >= 0 && (free & (1u << k)) != 0u; k--) left += laneStep;
                    for (int k = center + 1; k < BotTrackProfile.LaneSamples && (free & (1u << k)) != 0u; k++) right2 += laneStep;
                }
                stations[i].HalfWidthLeft = left;
                stations[i].HalfWidthRight = right2;
                if (!centerFree)
                    stations[i].Surface = BotSurface.Vao;
            }

            // ---------- 2b. degraus entre peças de pista ----------
            ResolveSteps(stations, s, warnings);

            // ---------- 3. rampas ----------
            for (int i = 0; i < count; i++)
            {
                if (stations[i].Surface == BotSurface.Vao)
                    continue;
                if (Mathf.Abs(stations[i].Grade) >= s.rampGrade)
                    stations[i].Surface = BotSurface.Rampa;
            }

            // ---------- 4. vãos: saltar ou contornar ----------
            ResolveGaps(stations, spacing, s, laneStep, warnings);

            // ---------- 5. perfil de velocidade ----------
            BuildSpeedProfile(stations, spacing, s, zones, path.TotalLength);

            // ---------- 6. curvas ----------
            BotCorner[] corners = ExtractCorners(stations, spacing, s);

            var profile = new BotTrackProfile(
                stations, corners, spacing, path.TotalLength, path.Looped,
                laneStep, s.corridorHalfWidth, warnings);

            report = BuildReport(profile, stations, corners, warnings);
            return profile;
        }

        // ------------------------------------------------------------------ degraus
        /// <summary>
        /// Encontra as costuras verticais entre peças de pista de alturas diferentes.
        ///
        /// Medido nesta pista, no ponto onde os bots mais travavam (x≈113, 410 m): a rota corre
        /// sobre o TERRENO a y=19,00 e a peça seguinte começa a y=19,83 — um degrau de 0,83 m
        /// atravessando os 18 m de largura, sem desvio possível. Para o sensor aquilo é uma
        /// parede; para o kart é um solavanco, desde que ele chegue com velocidade.
        ///
        /// A sondagem de corredor zera a máscara nessas estações (o corpo do kart realmente
        /// colide com a face do degrau), então aqui a máscara é reaproveitada de DEPOIS do
        /// degrau: o que interessa é para onde o bot vai, não onde ele encosta.
        /// </summary>
        private static void ResolveSteps(BotTrackStation[] st, Settings s, List<string> warnings)
        {
            int count = st.Length;
            int lookAhead = Mathf.Max(1, Mathf.CeilToInt(s.stepLookAhead / Mathf.Max(0.25f, s.spacing)));

            for (int i = 0; i < count; i++)
            {
                if (float.IsNaN(st[i].GroundY))
                    continue;

                int next = Wrap(i + 1, count);
                if (!float.IsNaN(st[next].GroundY))
                    st[i].StepRise = st[next].GroundY - st[i].GroundY;

                // Procura, alguns metros à frente, a primeira estação com passagem de verdade. A
                // transição não cabe sempre num único metro: a base de uma rampa e a quina de uma
                // peça de pista podem bloquear duas ou três estações seguidas.
                for (int k = 1; k <= lookAhead; k++)
                {
                    int j = Wrap(i + k, count);
                    if (st[j].FreeMask == 0u || float.IsNaN(st[j].GroundY))
                        continue;

                    float rise = st[j].GroundY - st[i].GroundY;
                    if (rise <= s.stepMinRise)
                        break;   // plano, ou descida — descer um degrau nunca prendeu ninguém

                    if (rise > s.maxClimbableStep)
                    {
                        warnings.Add(
                            $"Degrau de {rise:F2} m em {st[i].Distance:F0} m {Fmt(st[i].Position)} — acima do " +
                            $"que o kart sobe ({s.maxClimbableStep:F2} m). Os bots vão bater nessa quina.");
                        break;
                    }

                    st[i].Surface = BotSurface.Degrau;
                    if (st[i].FreeMask == 0u)
                    {
                        st[i].FreeMask = st[j].FreeMask;
                        st[i].HalfWidthLeft = st[j].HalfWidthLeft;
                        st[i].HalfWidthRight = st[j].HalfWidthRight;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ vãos
        private static void ResolveGaps(BotTrackStation[] st, float spacing, Settings s, float laneStep, List<string> warnings)
        {
            int count = st.Length;
            int i = 0;
            int guard = 0;

            while (i < count && guard++ < count * 2)
            {
                if (st[i].Surface != BotSurface.Vao) { i++; continue; }

                int start = i;
                int end = i;
                while (end + 1 < count && st[end + 1].Surface == BotSurface.Vao) end++;

                int before = Wrap(start - 1, count);
                int after = Wrap(end + 1, count);
                float length = (end - start + 1) * spacing + s.gapClearance;

                // Ângulo de decolagem = a maior inclinação dos últimos metros antes da borda (o
                // "kicker"). Ler só a estação da borda perde a rampa que dá o impulso.
                float launchGrade = 0f;
                int lookBack = Mathf.CeilToInt(8f / spacing);
                for (int k = 1; k <= lookBack; k++)
                    launchGrade = Mathf.Max(launchGrade, st[Wrap(start - k, count)].Grade);

                float required = BallisticTakeoffKmh(
                    length, st[after].Position.y - st[before].Position.y, launchGrade, s);

                // Dá para desviar por terra? (buraco parcial no meio da pista)
                float widestFree = 0f;
                for (int k = start; k <= end; k++)
                    widestFree = Mathf.Max(widestFree, WidestFreeRun(st[k].FreeMask, laneStep));

                bool canBypass = widestFree >= s.bypassWidth;
                bool canJump = required > 1f && required <= s.maxSpeedKmh * 0.98f;

                if (canBypass)
                {
                    // Buraco PARCIAL: sobra faixa de chão ao lado. Contornar é muito mais confiável
                    // que saltar, e o planejador lateral já faz isso sozinho pela FreeMask — nada a
                    // marcar aqui. É este caso que resolve "buraco no meio da pista onde caem".
                }
                else if (canJump)
                {
                    // Vão de lado a lado: só dá para passar voando. Marca a decolagem e a
                    // velocidade exigida; o perfil de velocidade garante o embalo na aproximação.
                    st[before].Surface = BotSurface.Decolagem;
                    st[before].TakeoffSpeedKmh = required;
                    st[after].Surface = BotSurface.Pouso;
                }
                else
                {
                    warnings.Add(
                        $"Vão de {length:F1} m em {st[start].Distance:F0} m {Fmt(st[start].Position)} " +
                        $"sem passagem lateral e exigindo {required:F0} km/h de decolagem " +
                        $"(máx {s.maxSpeedKmh:F0}) — os bots NÃO conseguem passar aqui.");
                }

                i = end + 1;
            }
        }

        /// <summary>
        /// Velocidade (km/h) para vencer um vão de comprimento L com desnível dh, decolando com a
        /// inclinação θ do lip: v² = g·L² / (2·cos²θ·(L·tanθ − Δh)).
        ///
        /// O termo <see cref="Settings.landingCatchHeight"/> existe porque os vãos desta pista são
        /// PLANOS (medido: os dois saltos têm piso a 34,83 dos dois lados, sem kicker). Puramente
        /// balístico, um vão plano seria intransponível. Na prática o kart chega alguns decímetros
        /// abaixo do nível e a resolução de colisão arcade o joga por cima da beirada oposta.
        /// Calibrado em 1,2 m: reproduz os 75/90 km/h que o próprio designer autorou nas zonas de
        /// Salto desta pista.
        /// </summary>
        private static float BallisticTakeoffKmh(float length, float dh, float grade, Settings s)
        {
            float theta = Mathf.Atan(grade);
            float cos = Mathf.Cos(theta);
            float effectiveDrop = dh - s.landingCatchHeight;
            float denom = 2f * cos * cos * (length * Mathf.Tan(theta) - effectiveDrop);
            if (denom <= 0.05f)
                return float.MaxValue;
            float v = Mathf.Sqrt(9.81f * length * length / denom);
            return v * 3.6f * s.takeoffMargin;
        }

        private static float WidestFreeRun(uint mask, float laneStep)
        {
            int best = 0, run = 0;
            for (int k = 0; k < BotTrackProfile.LaneSamples; k++)
            {
                if ((mask & (1u << k)) != 0u) { run++; if (run > best) best = run; }
                else run = 0;
            }
            return best * laneStep;
        }

        // ------------------------------------------------------------------ perfil de velocidade
        private static void BuildSpeedProfile(BotTrackStation[] st, float spacing, Settings s, IList<BotTrackZone> zones, float totalLength)
        {
            int count = st.Length;

            // (a) teto local: curvatura, largura e zonas autoradas.
            for (int i = 0; i < count; i++)
            {
                float v = s.maxSpeedKmh;

                float k = Mathf.Abs(st[i].Curvature);
                if (k > 1e-4f)
                {
                    float radius = Mathf.Clamp(1f / k, 4f, 1000f);
                    // A curvatura medida é a do TRAÇADO AUTORADO, que passa pelo meio da pista.
                    // Um piloto abre a curva usando a largura disponível (entra por fora, corta o
                    // ápice), o que aumenta o raio efetivo. Sem isto o perfil manda o bot a 40 km/h
                    // numa curva que ele faz a 70.
                    float apexGain = Mathf.Min(st[i].UsableWidth * 0.5f, s.maxApexWidthGain);
                    v = Mathf.Min(v, Mathf.Sqrt(s.lateralAccel * (radius + apexGain)) * 3.6f);
                }

                float width = st[i].UsableWidth;
                if (width > 0.01f && width < s.wideWidth)
                {
                    float t = Mathf.InverseLerp(s.narrowWidth, s.wideWidth, width);
                    v = Mathf.Min(v, Mathf.Lerp(s.narrowSpeedKmh, s.maxSpeedKmh, t));
                }

                st[i].SafeSpeedKmh = Mathf.Max(s.minSpeedKmh, v);
            }

            ApplyZoneOverrides(st, zones, totalLength, s);

            // (b) rampas e decolagens não podem ser freadas: garantem embalo.
            for (int i = 0; i < count; i++)
            {
                if (st[i].TakeoffSpeedKmh > 1f)
                    st[i].SafeSpeedKmh = Mathf.Max(st[i].SafeSpeedKmh, Mathf.Min(s.maxSpeedKmh, st[i].TakeoffSpeedKmh));
            }

            // (c) passe PARA TRÁS: o ponto de freada exato para chegar ao ápice na velocidade certa.
            //     É isto que elimina a freada "por precaução" longe da curva.
            //     Duas voltas para convergir num traçado fechado.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int n = count - 1; n >= 0; n--)
                {
                    int i = n, j = Wrap(n + 1, count);
                    float reachable = Mathf.Sqrt(Sq(st[j].SafeSpeedKmh / 3.6f) + 2f * s.brakeAccel * spacing) * 3.6f;
                    if (reachable < st[i].SafeSpeedKmh)
                        st[i].SafeSpeedKmh = reachable;
                }
            }

            // (d) passe PARA FRENTE: aceleração realista na saída (não teleporta para a máxima).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int n = 0; n < count; n++)
                {
                    int i = n, j = Wrap(n - 1, count);
                    float reachable = Mathf.Sqrt(Sq(st[j].SafeSpeedKmh / 3.6f) + 2f * s.driveAccel * spacing) * 3.6f;
                    if (reachable < st[i].SafeSpeedKmh)
                        st[i].SafeSpeedKmh = reachable;
                }
            }

            // (e) piso final: nunca planejar um rastejo.
            for (int i = 0; i < count; i++)
                st[i].SafeSpeedKmh = Mathf.Clamp(st[i].SafeSpeedKmh, s.minSpeedKmh, s.maxSpeedKmh);
        }

        private static void ApplyZoneOverrides(BotTrackStation[] st, IList<BotTrackZone> zones, float totalLength, Settings s)
        {
            if (zones == null || zones.Count == 0)
                return;

            int count = st.Length;
            for (int z = 0; z < zones.Count; z++)
            {
                BotTrackZone zone = zones[z];
                if (zone == null) continue;
                if (zone.MaxSpeedKmh <= 1f && zone.MinSpeedKmh <= 1f && zone.RecommendedSpeedKmh <= 1f)
                    continue;

                // Projeta o centro da zona na estação mais próxima.
                int best = -1;
                float bestSqr = float.MaxValue;
                Vector3 zp = zone.transform.position;
                for (int i = 0; i < count; i++)
                {
                    float sq = (st[i].Position - zp).sqrMagnitude;
                    if (sq < bestSqr) { bestSqr = sq; best = i; }
                }
                if (best < 0) continue;

                int before = Mathf.CeilToInt(zone.MetersBefore / Mathf.Max(0.25f, s.spacing));
                int after = Mathf.CeilToInt(zone.MetersAfter / Mathf.Max(0.25f, s.spacing));
                for (int o = -before; o <= after; o++)
                {
                    int i = Wrap(best + o, count);
                    if (zone.MaxSpeedKmh > 1f)
                        st[i].SafeSpeedKmh = Mathf.Min(st[i].SafeSpeedKmh, zone.MaxSpeedKmh);
                    if (zone.RecommendedSpeedKmh > 1f)
                        st[i].SafeSpeedKmh = Mathf.Min(st[i].SafeSpeedKmh, zone.RecommendedSpeedKmh);
                    if (zone.MinSpeedKmh > 1f)
                        st[i].SafeSpeedKmh = Mathf.Max(st[i].SafeSpeedKmh, zone.MinSpeedKmh);
                }
            }
        }

        // ------------------------------------------------------------------ curvas
        private static BotCorner[] ExtractCorners(BotTrackStation[] st, float spacing, Settings s)
        {
            int count = st.Length;
            float enterK = 1f / Mathf.Max(1f, s.cornerRadius);
            var corners = new List<BotCorner>();

            var inCorner = new bool[count];
            for (int i = 0; i < count; i++)
                inCorner[i] = Mathf.Abs(st[i].Curvature) >= enterK;

            int start = -1;
            for (int n = 0; n < count * 2; n++)
            {
                int i = n % count;
                if (n >= count && start < 0) break;

                if (inCorner[i] && start < 0)
                {
                    start = n;
                }
                else if (!inCorner[i] && start >= 0)
                {
                    AddCorner(corners, st, spacing, s, start, n - 1, count);
                    start = -1;
                    if (n >= count) break;
                }
            }
            if (start >= 0)
                AddCorner(corners, st, spacing, s, start, count - 1, count);

            return corners.ToArray();
        }

        private static void AddCorner(List<BotCorner> list, BotTrackStation[] st, float spacing, Settings s, int from, int to, int count)
        {
            int len = to - from + 1;
            if (len < 3) return; // ruído de curvatura, não é curva

            float minRadius = float.MaxValue;
            float turnDeg = 0f;
            int apex = from;
            float apexK = 0f;
            float dirSum = 0f;

            for (int n = from; n <= to; n++)
            {
                int i = Wrap(n, count);
                float k = Mathf.Abs(st[i].Curvature);
                if (k > 1e-5f) minRadius = Mathf.Min(minRadius, 1f / k);
                turnDeg += k * spacing * Mathf.Rad2Deg;
                dirSum += st[i].Curvature;
                if (k > apexK) { apexK = k; apex = i; }
            }

            if (minRadius == float.MaxValue) return;

            var c = new BotCorner
            {
                EntryDistance = st[Wrap(from, count)].Distance,
                ApexDistance = st[apex].Distance,
                ExitDistance = st[Wrap(to, count)].Distance,
                MinRadius = minRadius,
                TotalTurnDeg = turnDeg,
                Direction = dirSum >= 0f ? 1 : -1,
                ApexSpeedKmh = st[apex].SafeSpeedKmh
            };

            // O drift só entra quando o grip normal NÃO fecha o raio. Fora disso ele só custa
            // velocidade — era exatamente o "zig zag derrapando antes de todas as curvas".
            c.WantsDrift = minRadius <= s.driftRadius
                && turnDeg >= s.driftMinTurnDeg
                && c.ApexSpeedKmh >= s.driftMinSpeedKmh;

            list.Add(c);
        }

        // ------------------------------------------------------------------ utilidades
        private static bool GroundBelow(Vector3 origin, float maxDist, LayerMask mask, out float y, out Collider ground)
        {
            y = 0f;
            ground = null;
            int n = Physics.RaycastNonAlloc(origin, Vector3.down, HitBuffer, maxDist, mask, QueryTriggerInteraction.Ignore);
            float bestDist = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = HitBuffer[i];
                if (h.collider == null) continue;
                if (h.collider.GetComponentInParent<KartController>() != null) continue; // karts não são pista
                if (h.distance < bestDist) { bestDist = h.distance; y = h.point.y; ground = h.collider; found = true; }
            }
            return found;
        }

        /// <summary>Há algo sólido no espaço que o kart ocuparia sobre este ponto de chão?</summary>
        private static bool Obstructed(Vector3 groundPoint, Collider ground, LayerMask mask)
        {
            Vector3 c = groundPoint + Vector3.up * KartClearance;
            int n = Physics.OverlapSphereNonAlloc(c, KartRadius, OverlapBuffer, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = OverlapBuffer[i];
                if (col == null || col == ground) continue;
                if (col.GetComponentInParent<KartController>() != null) continue;
                return true;
            }
            return false;
        }

        private static float SignedCurvature(Vector3 a, Vector3 b, Vector3 c)
        {
            a.y = b.y = c.y = 0f;
            Vector3 ab = b - a, bc = c - b, ac = c - a;
            float la = ab.magnitude, lb = bc.magnitude, lc = ac.magnitude;
            if (la < 1e-3f || lb < 1e-3f || lc < 1e-3f) return 0f;

            float cross = ab.x * bc.z - ab.z * bc.x;   // >0 = vira para a ESQUERDA no plano XZ
            float area2 = Mathf.Abs(cross);
            float k = 2f * area2 / (la * lb * lc);
            return cross < 0f ? k : -k;                // + = direita
        }

        private static void SmoothCurvature(BotTrackStation[] st, int radius)
        {
            int count = st.Length;
            var tmp = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                int n = 0;
                for (int o = -radius; o <= radius; o++) { sum += st[Wrap(i + o, count)].Curvature; n++; }
                tmp[i] = sum / n;
            }
            for (int i = 0; i < count; i++) st[i].Curvature = tmp[i];
        }

        private static int Wrap(int i, int count) => ((i % count) + count) % count;
        private static float Sq(float v) => v * v;
        private static string Fmt(Vector3 v) => $"({v.x:F0},{v.y:F0},{v.z:F0})";

        private static string BuildReport(BotTrackProfile p, BotTrackStation[] st, BotCorner[] corners, List<string> warnings)
        {
            var sb = new StringBuilder();
            int vao = 0, rampa = 0, decolagem = 0, estreito = 0, degrau = 0;
            float minWidth = float.MaxValue, sumSpeed = 0f, minSpeed = float.MaxValue;
            Vector3 narrowest = Vector3.zero, slowest = Vector3.zero;

            for (int i = 0; i < st.Length; i++)
            {
                switch (st[i].Surface)
                {
                    case BotSurface.Vao: vao++; break;
                    case BotSurface.Rampa: rampa++; break;
                    case BotSurface.Decolagem: decolagem++; break;
                    case BotSurface.Degrau: degrau++; break;
                }
                float w = st[i].UsableWidth;
                if (w > 0.01f && w < minWidth) { minWidth = w; narrowest = st[i].Position; }
                if (w > 0.01f && w < 4f) estreito++;
                sumSpeed += st[i].SafeSpeedKmh;
                if (st[i].SafeSpeedKmh < minSpeed) { minSpeed = st[i].SafeSpeedKmh; slowest = st[i].Position; }
            }

            int driftCorners = 0;
            for (int i = 0; i < corners.Length; i++) if (corners[i].WantsDrift) driftCorners++;

            sb.AppendLine($"[BotTrackBaker] {st.Length} estações / {p.TotalLength:F0} m (espaçamento {p.Spacing:F2} m)");
            sb.AppendLine($"  superfície: {rampa} rampa, {decolagem} decolagem, {vao} vão, {degrau} degrau, {estreito} estreito(<4m)");
            sb.AppendLine($"  largura mínima {minWidth:F1} m em {Fmt(narrowest)}");
            sb.AppendLine($"  velocidade planejada: média {sumSpeed / st.Length:F0} km/h, mínima {minSpeed:F0} km/h em {Fmt(slowest)}");
            sb.AppendLine($"  curvas: {corners.Length} (com drift: {driftCorners})");
            for (int i = 0; i < corners.Length; i++)
            {
                BotCorner c = corners[i];
                sb.AppendLine($"    curva {i}: {c.EntryDistance:F0}→{c.ExitDistance:F0} m, raio {c.MinRadius:F0} m, " +
                              $"giro {c.TotalTurnDeg:F0}°, ápice {c.ApexSpeedKmh:F0} km/h, " +
                              (c.WantsDrift ? "DRIFT" : "sem drift"));
            }
            if (warnings.Count > 0)
            {
                sb.AppendLine($"  AVISOS DE LEVEL DESIGN ({warnings.Count}):");
                for (int i = 0; i < warnings.Count; i++) sb.AppendLine("    - " + warnings[i]);
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Um perfil por traçado, compartilhado entre todos os bots (o bake é caro; a consulta não).
    /// </summary>
    public static class BotTrackProfileCache
    {
        private static readonly Dictionary<int, BotTrackProfile> byLine = new Dictionary<int, BotTrackProfile>();

        public static BotTrackProfile GetOrBake(Object owner, BotPath path, IList<BotTrackZone> zones, BotTrackBaker.Settings settings)
        {
            if (owner == null || path == null || !path.IsValid)
                return null;

            int key = owner.GetInstanceID();
            // Valida o comprimento: um instanceID pode ser reciclado entre sessões de play, e um
            // perfil de outra pista serviria um mapa errado em silêncio.
            if (byLine.TryGetValue(key, out BotTrackProfile cached)
                && cached != null
                && Mathf.Abs(cached.TotalLength - path.TotalLength) < 1f)
                return cached;

            float t0 = Time.realtimeSinceStartup;
            BotTrackProfile profile = BotTrackBaker.Bake(path, settings, zones, out string report);
            if (profile == null)
                return null;

            byLine[key] = profile;
            Debug.Log(report + $"  bake em {(Time.realtimeSinceStartup - t0) * 1000f:F0} ms");
            return profile;
        }

        public static void Clear() => byLine.Clear();
    }
}
