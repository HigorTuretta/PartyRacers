using UnityEngine;

// Utilitário compartilhado pelos projéteis (foguete, disco voador) para manter altura FIXA em
// relação ao chão durante o voo: sonda o terreno abaixo e ajusta o Y suavemente.
// Ignora karts e triggers — só superfícies estáticas contam como "chão".
public static class ProjectileGroundHover
{
    private static readonly RaycastHit[] Hits = new RaycastHit[8];

    /// <summary>
    /// Ajusta 'position.y' para ficar a 'hoverHeight' do chão detectado abaixo.
    /// Retorna true se encontrou chão (caso contrário a posição não é alterada).
    /// </summary>
    public static bool TryAdjustHeight(
        ref Vector3 position,
        float hoverHeight,
        float adjustSpeed,
        LayerMask groundMask,
        float probeUp = 3f,
        float probeDown = 12f)
    {
        Vector3 origin = position + Vector3.up * probeUp;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            Hits,
            probeUp + probeDown,
            groundMask,
            QueryTriggerInteraction.Ignore);

        float bestDistance = float.MaxValue;
        bool found = false;
        float groundY = 0f;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null)
                continue;

            // Karts não são chão: o projétil não deve "subir" ao sobrevoar um carro.
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }
        }

        if (!found)
            return false;

        float targetY = groundY + hoverHeight;
        position.y = Mathf.MoveTowards(position.y, targetY, adjustSpeed * Time.deltaTime);
        return true;
    }
}
