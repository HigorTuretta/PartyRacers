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
    [Tooltip("Quantos metros ABAIXO do traçado dos bots o kart precisa estar para o chão fora-da-pista contar. " +
             "Evita falso positivo em pistas onde dá para encostar na grama no mesmo nível da pista.")]
    [SerializeField] private float belowRouteThreshold = 4f;
    [Tooltip("Queda livre: abaixo do traçado por mais que isto (m) respawna mesmo sem identificar o chão.")]
    [SerializeField] private float hardBelowRouteDistance = 20f;
    [Tooltip("Tempo (s) na condição de fora-da-pista antes do respawn automático.")]
    [SerializeField] private float outOfBoundsSeconds = 1.0f;
    [Tooltip("Intervalo (s) entre checagens de fora-da-pista (barato; não precisa ser por frame).")]
    [SerializeField] private float outOfBoundsCheckInterval = 0.2f;

    private Collider[] kartColliders;
    private KartController kart;
    private KartLocalRig localRig;
    private float outOfBoundsTimer;
    private float nextOutOfBoundsCheck;

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

        // Está apoiado em chão de fora da pista (Terreno) E abaixo do nível do traçado.
        // A condição de altura evita falso positivo em pista no nível da grama.
        if (IsGroundedOnOutOfBoundsSurface() && (!hasRoute || belowRoute > belowRouteThreshold))
            return true;

        return false;
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
        Vector3 targetPosition = FindSafeRespawnPosition(raceTracker.LastRespawnPosition);

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
