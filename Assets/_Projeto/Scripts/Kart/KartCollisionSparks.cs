using UnityEngine;

// Emite faíscas (VFXSparks) em colisões relevantes do kart: outros carros, paredes, rampas e
// obstáculos de impacto. Os caminhos de resposta física calculam ponto, normal e intensidade;
// este componente só controla o feedback visual e seu cooldown, evitando spam em raspões.
[DisallowMultipleComponent]
public class KartCollisionSparks : MonoBehaviour
{
    [Header("VFX")]
    [Tooltip("Prefab das faíscas (ex.: VFXSparks). Sem isso o componente fica inerte.")]
    [SerializeField] private GameObject sparksPrefab;

    [Header("Ativação")]
    [Tooltip("Intensidade mínima de impacto (0..1) para emitir faíscas. Toques de leve não geram.")]
    [SerializeField, Range(0f, 1f)] private float minImpact01 = 0.16f;
    [Tooltip("Tempo mínimo (s) entre duas emissões deste kart — evita spam em raspões longos.")]
    [SerializeField] private float cooldown = 0.16f;

    [Header("Duração / Escala")]
    [Tooltip("Fallback de duração do VFX (s) caso o ParticleSystem não defina a própria).")]
    [SerializeField] private float vfxLifetime = 1.2f;
    [Tooltip("Escala opcional aplicada ao VFX instanciado.")]
    [SerializeField] private float scale = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private float nextEmitTime;

    /// <summary>Permite à camada de spawn/bots injetar o prefab quando o kart não o traz no Inspector.</summary>
    public void EnsurePrefab(GameObject prefab)
    {
        if (sparksPrefab == null)
            sparksPrefab = prefab;
    }

    /// <summary>
    /// Tenta emitir faíscas no ponto de contato. Retorna true se realmente emitiu (respeitando
    /// força mínima e cooldown).
    /// </summary>
    public bool TryEmit(Vector3 contactPoint, Vector3 contactNormal, float impact01)
    {
        if (sparksPrefab == null || impact01 < minImpact01)
            return false;

        if (Time.time < nextEmitTime)
            return false;

        nextEmitTime = Time.time + Mathf.Max(0.01f, cooldown);

        Quaternion rotation = contactNormal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(contactNormal.normalized, Vector3.up)
            : Quaternion.identity;

        PowerVFXUtility.SpawnOneShot(sparksPrefab, contactPoint, rotation, vfxLifetime, 0f, scale);

        if (debugMode)
            Debug.DrawRay(contactPoint, contactNormal.normalized * 1.5f, Color.yellow, 0.4f);

        return true;
    }
}
