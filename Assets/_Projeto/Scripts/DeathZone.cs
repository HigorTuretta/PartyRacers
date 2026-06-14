using UnityEngine;

/// <summary>
/// Zona de morte/queda: qualquer kart (player ou bot) que ENTRAR no trigger respawna
/// imediatamente no último checkpoint válido.
///
/// COMO USAR: crie um GameObject com um Collider marcado como 'Is Trigger' (ex.: um BoxCollider
/// gigante cobrindo o fosso/água/fora do mapa) e adicione este componente. Funciona em qualquer
/// pista, sem depender de nome de chão ou da rota dos bots.
/// </summary>
[DisallowMultipleComponent]
public class DeathZone : MonoBehaviour
{
    [Tooltip("Atraso (s) entre entrar na zona e o respawn (0 = imediato).")]
    [SerializeField, Min(0f)] private float respawnDelay = 0f;

    [Header("Gizmo")]
    [SerializeField] private Color gizmoColor = new Color(1f, 0.15f, 0.1f, 0.25f);

    private void Reset()
    {
        // Conveniência: garante trigger ao adicionar o componente.
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        KartRespawn respawn = other.GetComponentInParent<KartRespawn>();
        if (respawn == null)
            return;

        if (respawnDelay <= 0f)
        {
            respawn.Respawn();
            return;
        }

        StartCoroutine(RespawnAfterDelay(respawn));
    }

    private System.Collections.IEnumerator RespawnAfterDelay(KartRespawn respawn)
    {
        yield return new WaitForSeconds(respawnDelay);

        if (respawn != null)
            respawn.Respawn();
    }

    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
            return;

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;

        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (col is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        }
    }
}
