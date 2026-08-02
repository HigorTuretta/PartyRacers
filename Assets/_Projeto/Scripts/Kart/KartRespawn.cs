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

    [Header("Ghost pós-respawn")]
    [Tooltip("Todo respawn (manual, automático ou da IA) deixa o kart em modo ghost por este tempo: " +
             "sem colisão kart-a-kart, piscando, mas dirigível.")]
    [SerializeField] private float ghostDurationAfterRespawn = 3f;

    [Header("Respawn automático fora da pista")]
    [Tooltip("Respawna sozinho ao cair fora da pista (no Terreno) ou despencar abaixo do traçado.")]
    [SerializeField] private bool autoRespawnOutOfBounds = true;
    [Tooltip("Nomes (parciais) de colliders de chão que contam como FORA da pista (terreno/grama do mapa).")]
    [SerializeField] private string[] outOfBoundsGroundNames = { "Terreno", "Terrain" };
    [Tooltip("Queda livre: abaixo do traçado por mais que isto (m) respawna mesmo sem identificar o chão.")]
    [SerializeField] private float hardBelowRouteDistance = 20f;
    [Tooltip("Tempo (s) na condição de fora-da-pista antes do respawn automático.")]
    [SerializeField] private float outOfBoundsSeconds = 1.0f;
    [Tooltip("Intervalo (s) entre checagens de fora-da-pista (barato; não precisa ser por frame).")]
    [SerializeField] private float outOfBoundsCheckInterval = 0.2f;

    [Header("Limites adicionais (funcionam em QUALQUER pista)")]
    [Tooltip("Caiu este tanto (m) ABAIXO da altura do último checkpoint → respawn. Funciona mesmo " +
             "sem rota de bots e sem chão identificável (buraco no mapa, vazio). 0 = desligado.")]
    [SerializeField] private float fallBelowCheckpointDistance = 30f;
    [Tooltip("Distância PLANAR (m) da rota dos bots além da qual o kart conta como fora da pista " +
             "(escapou do mapa pela lateral). Precisa de BotRacingLine na cena. 0 = desligado.")]
    [SerializeField] private float offRouteDistance = 30f;

    [Header("Capotado")]
    [Tooltip("Respawna sozinho se ficar capotado/de lado, parado, por 'flippedSeconds' (vale para o player também).")]
    [SerializeField] private bool autoRespawnWhenFlipped = true;
    [SerializeField, Range(-1f, 1f)] private float flippedUpThreshold = 0.25f;
    [SerializeField] private float flippedMaxSpeedKmh = 8f;
    [SerializeField] private float flippedSeconds = 3f;

    private Collider[] kartColliders;
    private KartController kart;
    private KartLocalRig localRig;
    private float outOfBoundsTimer;
    private float nextOutOfBoundsCheck;
    private float flippedTimer;

    // Pose segura registrada por zonas RespawnSeguro (BotTrackZone) — usada no lugar do
    // checkpoint quando está À FRENTE dele no sentido da corrida.
    private bool hasSafePose;
    private Vector3 safePosePosition;
    private Quaternion safePoseRotation;
    private float safePoseRouteDistance;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (raceTracker == null)
            raceTracker = GetComponent<KartRaceTracker>();

        if (kartCollision == null)
            kartCollision = GetComponent<KartCollision>();

        kart = GetComponent<KartController>();
        localRig = GetComponent<KartLocalRig>();
        kartColliders = GetComponentsInChildren<Collider>();
    }

    private void Update()
    {
        UpdateOutOfBounds();
        UpdateFlipped();

        // Tecla R: somente o kart do PLAYER LOCAL (bots/remotos respawnariam todos juntos).
        if (localRig != null && !localRig.IsLocalPlayer)
            return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
            Respawn();
    }

    // ------------------------------------------------------------------ fora da pista
    private void UpdateOutOfBounds()
    {
        if (!autoRespawnOutOfBounds || Time.time < nextOutOfBoundsCheck)
            return;

        nextOutOfBoundsCheck = Time.time + Mathf.Max(0.05f, outOfBoundsCheckInterval);

        if (kart != null && !kart.CanControl)
        {
            outOfBoundsTimer = 0f;
            return;
        }

        if (IsOutOfBounds())
            outOfBoundsTimer += outOfBoundsCheckInterval;
        else
            outOfBoundsTimer = 0f;

        if (outOfBoundsTimer >= outOfBoundsSeconds)
        {
            outOfBoundsTimer = 0f;
            Respawn();
        }
    }

    private bool IsOutOfBounds()
    {
        bool hasRoute = PartyRacers.AI.BotRacingLine.TryGetNearestRoutePoint(transform.position, out Vector3 onRoute);
        float belowRoute = hasRoute ? onRoute.y - transform.position.y : 0f;

        // Despencou muito abaixo do traçado (caiu da pista no vazio/terreno distante).
        if (hasRoute && belowRoute > hardBelowRouteDistance)
            return true;

        // Caiu muito abaixo da ALTURA do último checkpoint: funciona em qualquer pista, mesmo
        // sem rota de bots e sem chão nomeado (buraco/vazio/queda longa do mapa).
        if (fallBelowCheckpointDistance > 0f && raceTracker != null &&
            transform.position.y < raceTracker.LastRespawnPosition.y - fallBelowCheckpointDistance)
        {
            return true;
        }

        // Escapou pela LATERAL: longe demais (planar) da rota da pista.
        if (offRouteDistance > 0f && hasRoute)
        {
            float dx = onRoute.x - transform.position.x;
            float dz = onRoute.z - transform.position.z;
            if (dx * dx + dz * dz > offRouteDistance * offRouteDistance)
                return true;
        }

        // Está apoiado em chão de fora da pista (Terreno/Terrain): para um jogo de kart, repousar
        // no terreno do mapa É estar fora da pista — respawna após outOfBoundsSeconds, independente
        // da altura. (Antes exigia estar belowRouteThreshold abaixo do traçado, então um kart que
        // caía no Terreno no MESMO nível da pista — comum num mapa de minigolfe — nunca respawnava.)
        // O timer de outOfBoundsSeconds + as checagens periódicas já absorvem toques transitórios
        // na borda; belowRouteThreshold segue valendo só para a queda "despencada" mais acima.
        if (IsGroundedOnOutOfBoundsSurface())
            return true;

        return false;
    }

    // Capotado/de lado e praticamente parado: respawn automático (player e bots). Os bots têm
    // uma detecção própria mais rápida no BotDriverController; esta aqui é a garantia universal.
    private void UpdateFlipped()
    {
        if (!autoRespawnWhenFlipped)
            return;

        if (kart != null && (!kart.CanControl || kart.IsInKnockback))
        {
            flippedTimer = 0f;
            return;
        }

        bool flipped = transform.up.y < flippedUpThreshold
            && (kart == null || kart.SpeedKmh < flippedMaxSpeedKmh);

        if (!flipped)
        {
            flippedTimer = 0f;
            return;
        }

        flippedTimer += Time.deltaTime;
        if (flippedTimer >= flippedSeconds)
        {
            flippedTimer = 0f;
            Respawn();
        }
    }

    /// <summary>
    /// Registra uma pose segura (zona RespawnSeguro da rota). No próximo respawn, se esta pose
    /// estiver À FRENTE do último checkpoint no sentido da corrida, é usada no lugar dele —
    /// evita repetir um trecho-obstáculo inteiro quando existe um ponto seguro mais próximo.
    /// </summary>
    public void RecordSafePose(Vector3 position, Quaternion rotation)
    {
        if (!PartyRacers.AI.BotRacingLine.TryGetNearestRouteInfo(position, out _, out float routeDistance, out _, out _))
            return;

        hasSafePose = true;
        safePosePosition = position;
        safePoseRotation = rotation;
        safePoseRouteDistance = routeDistance;
    }

    // A pose segura só vale se estiver à frente do checkpoint (nunca manda o kart para trás).
    private bool SafePoseIsAheadOfCheckpoint(Vector3 checkpointPosition)
    {
        if (!hasSafePose)
            return false;

        if (!PartyRacers.AI.BotRacingLine.TryGetNearestRouteInfo(
                checkpointPosition, out _, out float checkpointDistance, out float totalLength, out bool looped))
        {
            return false;
        }

        if (!looped)
            return safePoseRouteDistance > checkpointDistance + 1f;

        float ahead = Mathf.Repeat(safePoseRouteDistance - checkpointDistance, totalLength);
        return ahead > 1f && ahead < totalLength * 0.45f;
    }

    private bool IsGroundedOnOutOfBoundsSurface()
    {
        if (outOfBoundsGroundNames == null || outOfBoundsGroundNames.Length == 0)
            return false;

        Vector3 origin = transform.position + Vector3.up * 0.6f;
        if (!Physics.SphereCast(origin, 0.4f, Vector3.down, out RaycastHit hit, 2.5f, groundMask, QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider == null || IsKartCollider(hit.collider))
            return false;

        string groundName = hit.collider.name;
        for (int i = 0; i < outOfBoundsGroundNames.Length; i++)
        {
            string token = outOfBoundsGroundNames[i];
            if (!string.IsNullOrEmpty(token) &&
                groundName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        // Terrenos Unity nativos contam mesmo sem bater o nome.
        return hit.collider is TerrainCollider;
    }

    public void Respawn()
    {
        Quaternion targetRotation = raceTracker.LastRespawnRotation;
        Vector3 basePosition = raceTracker.LastRespawnPosition;

        // Pose segura registrada (zona RespawnSeguro) à frente do checkpoint: usa ela.
        if (SafePoseIsAheadOfCheckpoint(basePosition))
        {
            basePosition = safePosePosition;
            targetRotation = safePoseRotation;
        }

        // Consumida: será regravada na próxima passagem por uma zona segura.
        hasSafePose = false;

        DoRespawn(basePosition, targetRotation);
    }

    /// <summary>
    /// Respawn em uma POSE explícita (não no checkpoint). Usado pelo "breadcrumb" da IA: quando
    /// um bot precisa respawnar (preso/obstáculo), ele volta ao último ponto onde estava andando
    /// bem — inclusive DENTRO de uma branch/atalho — em vez de ser jogado ao checkpoint anterior.
    /// </summary>
    public void RespawnToPose(Vector3 position, Quaternion rotation)
    {
        DoRespawn(position, rotation);
    }

    private void DoRespawn(Vector3 basePosition, Quaternion targetRotation)
    {
        Vector3 targetPosition = FindSafeRespawnPosition(basePosition);

        // Debuffs de pista nunca atravessam um respawn. A remoção restaura o limite de
        // velocidade imediatamente e também limpa a aura anexada ao kart.
        KartElectricShockEffect.ClearFrom(gameObject);
        kartCollision?.ResetIgnoredCollisionState();
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        rb.position = targetPosition;
        rb.rotation = targetRotation;
        transform.SetPositionAndRotation(targetPosition, targetRotation);
        Physics.SyncTransforms();

        rb.Sleep();
        rb.WakeUp();

        outOfBoundsTimer = 0f;
        flippedTimer = 0f;

        // Qualquer respawn (manual, automático ou da IA) ganha alguns segundos de ghost:
        // sem colisão kart-a-kart e piscando — não nasce dentro de um bolo de karts.
        if (ghostDurationAfterRespawn > 0f)
            KartTemporaryGhostState.Apply(gameObject, ghostDurationAfterRespawn);
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
