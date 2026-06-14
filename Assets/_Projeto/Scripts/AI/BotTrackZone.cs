using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PartyRacers.AI
{
    public enum BotTrackZoneType
    {
        Rampa,
        Salto,
        CurvaForte,
        AreaEstreita,
        Obstaculo,
        ZonaDeRisco,
        RespawnSeguro
    }

    /// <summary>Postura do bot no trecho — escala a velocidade-alvo e o quanto ele arrisca.</summary>
    public enum BotAggressiveness
    {
        Conservador,
        Normal,
        Agressivo
    }

    /// <summary>
    /// Marca um trecho ESPECIAL da rota dos bots (rampa, salto, curva forte, área estreita,
    /// obstáculo, zona de risco, ponto seguro de respawn). O trecho influencia o comportamento
    /// do bot: alinhamento com o traçado, velocidade mínima/máxima, supressão de drift/desvios.
    ///
    /// COMO USAR:
    /// 1. Crie um GameObject FILHO do BotRacingLine (ex.: "Zona_Rampa_01") e adicione este
    ///    componente. Filhos com BotTrackZone NÃO contam como pontos do traçado principal.
    /// 2. Posicione o objeto SOBRE a rota, no centro do trecho especial (a posição é projetada
    ///    automaticamente na linha principal).
    /// 3. Ajuste 'metersBefore'/'metersAfter' para cobrir o trecho, e 'approachDistance' para
    ///    o bot começar a se preparar (alinhar/ajustar velocidade) antes de entrar.
    /// 4. Use o menu de contexto "AI/Aplicar Preset Do Tipo" para valores padrão do tipo.
    ///
    /// Os gizmos desenham o trecho coberto na cor do tipo, direto sobre a curva da rota.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotTrackZone : MonoBehaviour
    {
        [Header("Tipo")]
        [SerializeField] private BotTrackZoneType type = BotTrackZoneType.Rampa;

        [Header("Cobertura na rota (m)")]
        [Tooltip("Quantos metros ANTES da posição projetada deste objeto a zona começa.")]
        [SerializeField, Min(0f)] private float metersBefore = 6f;
        [Tooltip("Quantos metros DEPOIS da posição projetada deste objeto a zona termina.")]
        [SerializeField, Min(0f)] private float metersAfter = 12f;
        [Tooltip("Distância antes do INÍCIO da zona em que o bot começa a se preparar (alinhar, ajustar velocidade).")]
        [SerializeField, Min(0f)] private float approachDistance = 20f;

        [Header("Velocidade (km/h — 0 = sem limite)")]
        [Tooltip("Velocidade MÍNIMA na zona/aproximação. Use em rampas que exigem embalo: o bot " +
                 "segura o acelerador e não freia abaixo disso.")]
        [SerializeField, Min(0f)] private float minSpeedKmh = 0f;
        [Tooltip("Velocidade MÁXIMA na zona. Use em curvas fortes/zonas de risco. 0 = sem teto " +
                 "(a IA decide pela curvatura/sensores — NÃO use teto em zona de obstáculo, deixe a " +
                 "IA desviar em velocidade).")]
        [SerializeField, Min(0f)] private float maxSpeedKmh = 0f;
        [Tooltip("Velocidade RECOMENDADA (alvo sugerido) no trecho. 0 = a IA escolhe sozinha. " +
                 "Diferente do teto: é um alvo, não um limite rígido.")]
        [SerializeField, Min(0f)] private float recommendedSpeedKmh = 0f;
        [Tooltip("Postura no trecho: Conservador reduz a velocidade-alvo, Agressivo aumenta.")]
        [SerializeField] private BotAggressiveness aggressiveness = BotAggressiveness.Normal;
        [Tooltip("Suprime o RESPAWN AUTOMÁTICO por falta de progresso nesta região (o bot ainda " +
                 "respawna em queda real/death zone). Use em trechos lentos/técnicos legítimos.")]
        [SerializeField] private bool avoidAutoRespawn = false;

        [Header("Alinhamento")]
        [Tooltip("Se ligado, o bot abandona a 'linha própria' e se alinha com o traçado central na aproximação/zona.")]
        [SerializeField] private bool alignToRoute = true;
        [Tooltip("Força do alinhamento (0 = leve, 1 = total).")]
        [SerializeField, Range(0f, 1f)] private float alignmentStrength = 0.85f;
        [Tooltip("Offset lateral máximo (m) permitido da linha de corrida dentro da zona.")]
        [SerializeField, Min(0f)] private float maxLateralOffset = 0.4f;

        [Header("Comportamento")]
        [Tooltip("Permite drift/handbrake dentro da zona (desligue em rampas/saltos/áreas estreitas).")]
        [SerializeField] private bool allowDrift = false;
        [Tooltip("Suprime o esterço de desvio de outros karts dentro da zona (áreas estreitas/rampas).")]
        [SerializeField] private bool suppressKartAvoidance = false;

        public BotTrackZoneType Type => type;
        public float MetersBefore => metersBefore;
        public float MetersAfter => metersAfter;
        public float ApproachDistance => approachDistance;
        public float MinSpeedKmh => minSpeedKmh;
        public float MaxSpeedKmh => maxSpeedKmh;
        public float RecommendedSpeedKmh => recommendedSpeedKmh;
        public BotAggressiveness Aggressiveness => aggressiveness;
        public bool AvoidAutoRespawn => avoidAutoRespawn;
        public bool AlignToRoute => alignToRoute;
        public float AlignmentStrength => alignmentStrength;
        public float MaxLateralOffset => maxLateralOffset;
        public bool AllowDrift => allowDrift;
        public bool SuppressKartAvoidance => suppressKartAvoidance;
        public float TotalLength => metersBefore + metersAfter;

        /// <summary>Multiplicador de velocidade-alvo conforme a postura (1 = Normal).</summary>
        public float AggressivenessSpeedScale =>
            aggressiveness == BotAggressiveness.Agressivo ? 1.12f :
            aggressiveness == BotAggressiveness.Conservador ? 0.82f : 1f;

        public Color GizmoColor
        {
            get
            {
                switch (type)
                {
                    case BotTrackZoneType.Rampa: return new Color(0.25f, 0.7f, 1f);
                    case BotTrackZoneType.Salto: return new Color(0.5f, 0.4f, 1f);
                    case BotTrackZoneType.CurvaForte: return new Color(1f, 0.55f, 0.1f);
                    case BotTrackZoneType.AreaEstreita: return new Color(1f, 0.85f, 0.1f);
                    case BotTrackZoneType.Obstaculo: return new Color(1f, 0.3f, 0.25f);
                    case BotTrackZoneType.ZonaDeRisco: return new Color(0.9f, 0.1f, 0.5f);
                    case BotTrackZoneType.RespawnSeguro: return new Color(0.2f, 1f, 0.4f);
                    default: return Color.white;
                }
            }
        }

        [ContextMenu("AI/Aplicar Preset Do Tipo")]
        public void ApplyTypePreset()
        {
            switch (type)
            {
                case BotTrackZoneType.Rampa:
                    metersBefore = 8f; metersAfter = 12f; approachDistance = 25f;
                    minSpeedKmh = 55f; maxSpeedKmh = 0f;
                    alignToRoute = true; alignmentStrength = 0.9f; maxLateralOffset = 0.3f;
                    allowDrift = false; suppressKartAvoidance = true;
                    break;

                case BotTrackZoneType.Salto:
                    metersBefore = 6f; metersAfter = 18f; approachDistance = 22f;
                    minSpeedKmh = 60f; maxSpeedKmh = 0f;
                    alignToRoute = true; alignmentStrength = 1f; maxLateralOffset = 0.2f;
                    allowDrift = false; suppressKartAvoidance = true;
                    break;

                case BotTrackZoneType.CurvaForte:
                    metersBefore = 8f; metersAfter = 10f; approachDistance = 18f;
                    minSpeedKmh = 0f; maxSpeedKmh = 55f;
                    alignToRoute = true; alignmentStrength = 0.6f; maxLateralOffset = 1f;
                    allowDrift = true; suppressKartAvoidance = false;
                    break;

                case BotTrackZoneType.AreaEstreita:
                    metersBefore = 5f; metersAfter = 10f; approachDistance = 15f;
                    minSpeedKmh = 0f; maxSpeedKmh = 0f;
                    alignToRoute = true; alignmentStrength = 0.8f; maxLateralOffset = 0.25f;
                    allowDrift = false; suppressKartAvoidance = true;
                    break;

                case BotTrackZoneType.Obstaculo:
                    // SEM teto de velocidade: o bot deve ATRAVESSAR RÁPIDO desviando, não andar
                    // devagar. Alinhamento leve + bastante folga lateral para escolher faixa livre.
                    // A frenagem fica por conta dos SENSORES (só freia se não houver escapatória).
                    metersBefore = 8f; metersAfter = 10f; approachDistance = 18f;
                    minSpeedKmh = 0f; maxSpeedKmh = 0f;
                    alignToRoute = true; alignmentStrength = 0.25f; maxLateralOffset = 3f;
                    allowDrift = false; suppressKartAvoidance = false;
                    aggressiveness = BotAggressiveness.Normal; avoidAutoRespawn = false;
                    break;

                case BotTrackZoneType.ZonaDeRisco:
                    metersBefore = 6f; metersAfter = 10f; approachDistance = 18f;
                    minSpeedKmh = 0f; maxSpeedKmh = 50f;
                    alignToRoute = true; alignmentStrength = 0.85f; maxLateralOffset = 0.3f;
                    allowDrift = false; suppressKartAvoidance = true;
                    break;

                case BotTrackZoneType.RespawnSeguro:
                    metersBefore = 4f; metersAfter = 4f; approachDistance = 0f;
                    minSpeedKmh = 0f; maxSpeedKmh = 0f;
                    alignToRoute = false; alignmentStrength = 0f; maxLateralOffset = 3f;
                    allowDrift = true; suppressKartAvoidance = false;
                    avoidAutoRespawn = true;
                    break;
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        private void OnDrawGizmos()
        {
            Color color = GizmoColor;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, 1.4f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 4f);

#if UNITY_EDITOR
            DrawCoverageOnRoute(color);
            Handles.color = color;
            string spd = "";
            if (minSpeedKmh > 0f) spd += $" min{minSpeedKmh:F0}";
            if (maxSpeedKmh > 0f) spd += $" max{maxSpeedKmh:F0}";
            if (recommendedSpeedKmh > 0f) spd += $" rec{recommendedSpeedKmh:F0}";
            string ag = aggressiveness != BotAggressiveness.Normal ? $" [{aggressiveness}]" : "";
            string nr = avoidAutoRespawn ? " [no-respawn]" : "";
            Handles.Label(transform.position + Vector3.up * 4.5f,
                $"{type} (-{metersBefore:F0}/+{metersAfter:F0}m){spd}{ag}{nr}");
#endif
        }

#if UNITY_EDITOR
        // Desenha o trecho coberto pela zona em cima da curva REAL da rota (preview do pai).
        private void DrawCoverageOnRoute(Color color)
        {
            BotRacingLine line = GetComponentInParent<BotRacingLine>();
            if (line == null)
                return;

            BotPath path = line.GetEditorPreviewPath();
            if (path == null || !path.IsValid)
                return;

            float center = path.FindNearestGlobal(transform.position).DistanceOnPath;
            float start = center - metersBefore;
            float end = center + metersAfter;

            Gizmos.color = new Color(color.r, color.g, color.b, 0.95f);
            DrawRouteSpan(path, start, end, 1.1f);

            if (approachDistance > 0.5f)
            {
                Gizmos.color = new Color(color.r, color.g, color.b, 0.35f);
                DrawRouteSpan(path, start - approachDistance, start, 0.8f);
            }
        }

        private static void DrawRouteSpan(BotPath path, float start, float end, float yOffset)
        {
            const float step = 2f;
            float length = Mathf.Max(0.5f, end - start);
            int samples = Mathf.Max(2, Mathf.CeilToInt(length / step));

            Vector3 prev = path.PointAtDistance(start) + Vector3.up * yOffset;
            for (int i = 1; i <= samples; i++)
            {
                Vector3 next = path.PointAtDistance(start + length * i / samples) + Vector3.up * yOffset;
                Gizmos.DrawLine(prev, next);
                // "Engrossa" a linha com um traço paralelo deslocado.
                Gizmos.DrawLine(prev + Vector3.up * 0.15f, next + Vector3.up * 0.15f);
                prev = next;
            }
        }
#endif
    }
}
