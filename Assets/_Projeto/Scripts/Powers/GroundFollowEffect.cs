using UnityEngine;

// Mantém um efeito visual (ex.: círculo mágico) PRESO AO CHÃO sob um alvo em movimento.
// Sonda o terreno abaixo do alvo a cada frame e posiciona o efeito na superfície — nunca
// flutuando no ar. Autodestrói após 'lifetime'.
[DisallowMultipleComponent]
public class GroundFollowEffect : MonoBehaviour
{
    [SerializeField] private float groundOffset = 0.06f;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeUp = 2f;
    [SerializeField] private float probeDown = 10f;

    private Transform target;
    private float despawnTime = float.PositiveInfinity;

    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    /// <summary>Anexa o comportamento a um efeito recém-instanciado.</summary>
    public static GroundFollowEffect Attach(GameObject effectInstance, Transform followTarget, float lifetime, LayerMask groundMask)
    {
        if (effectInstance == null)
            return null;

        GroundFollowEffect follower = effectInstance.GetComponent<GroundFollowEffect>();
        if (follower == null)
            follower = effectInstance.AddComponent<GroundFollowEffect>();

        follower.target = followTarget;
        follower.groundMask = groundMask;
        follower.despawnTime = lifetime > 0f ? Time.time + lifetime : float.PositiveInfinity;
        follower.SnapToGround();
        return follower;
    }

    private void LateUpdate()
    {
        if (Time.time >= despawnTime)
        {
            Destroy(gameObject);
            return;
        }

        SnapToGround();
    }

    private void SnapToGround()
    {
        Vector3 anchor = target != null ? target.position : transform.position;
        Vector3 origin = anchor + Vector3.up * probeUp;

        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            Hits,
            probeUp + probeDown,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        Vector3 groundPoint = anchor;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null)
                continue;

            // O próprio kart não é chão.
            if (target != null && hit.collider.transform.IsChildOf(target))
                continue;

            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundPoint = hit.point;
                found = true;
            }
        }

        Vector3 position = found ? groundPoint + Vector3.up * groundOffset : anchor;
        transform.position = position;
    }
}
