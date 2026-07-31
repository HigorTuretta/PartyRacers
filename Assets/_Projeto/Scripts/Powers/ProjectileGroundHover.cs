using UnityEngine;

// Utilitário compartilhado pelos projéteis (foguete, disco voador) para manter altura FIXA em
// relação ao chão durante o voo: sonda o terreno abaixo e ajusta o Y.
// Ignora karts e triggers — só superfícies estáticas contam como "chão".
public static class ProjectileGroundHover
{
    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    /// <summary>
    /// Ajusta 'position.y' para ficar a 'hoverHeight' do chão detectado abaixo.
    /// Retorna true se encontrou chão (caso contrário a posição não é alterada).
    /// </summary>
    /// <param name="probeUp">
    /// Começa a sondar bem acima do projétil. Precisa cobrir a subida mais brusca da pista:
    /// com um valor curto, uma rampa que sobe mais rápido que o projétil deixava o raio partir
    /// de dentro do terreno e o projétil atravessava o chão.
    /// </param>
    /// <param name="probeDown">
    /// Alcance para baixo. A pista tem dois níveis e saltos: se o raio não chega ao piso de
    /// baixo, nenhum ajuste era feito e o projétil ficava voando alto demais.
    /// </param>
    /// <param name="folgaMinima">
    /// Distância que o projétil nunca cruza, mesmo que a aproximação suave não dê conta da
    /// subida. É isto que garante que ele não entre no chão.
    /// </param>
    /// <param name="subidaMaxima">
    /// Quanto o chão pode estar ACIMA do projétil e ainda contar como chão dele. Cobre a rampa
    /// que sobe entre um frame e outro; acima disso é ponte/viaduto de outro nível da pista, que
    /// não é chão nenhum para quem voa por baixo.
    /// </param>
    public static bool TryAdjustHeight(
        ref Vector3 position,
        float hoverHeight,
        float adjustSpeed,
        LayerMask groundMask,
        float probeUp = 60f,
        float probeDown = 400f,
        float folgaMinima = 0.35f,
        float subidaMaxima = 4f)
    {
        Vector3 origin = position + Vector3.up * probeUp;
        int count = Physics.RaycastNonAlloc(
            origin,
            Vector3.down,
            Hits,
            probeUp + probeDown,
            groundMask,
            QueryTriggerInteraction.Ignore);

        // A sonda parte bem acima do projétil, então ela atravessa tudo o que estiver ENTRE a
        // origem e ele. Pegar "o hit mais próximo da origem" escolhia a superfície mais ALTA —
        // numa pista de dois níveis, o trecho suspenso. O projétil que passava por baixo era
        // grudado no teto e sumia. Chão é o que está SOB o projétil: entre os candidatos válidos,
        // vale o mais alto (a superfície logo abaixo).
        float best = float.MinValue;
        bool found = false;
        float groundY = 0f;
        float teto = position.y + subidaMaxima;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = Hits[i];
            if (hit.collider == null)
                continue;

            // Karts não são chão: o projétil não deve "subir" ao sobrevoar um carro.
            if (hit.collider.GetComponentInParent<KartController>() != null)
                continue;

            // Acima do projétil (ponte, viaduto, teto) não é chão dele.
            if (hit.point.y > teto)
                continue;

            if (hit.point.y > best)
            {
                best = hit.point.y;
                groundY = hit.point.y;
                found = true;
            }
        }

        if (!found)
            return false;

        float targetY = groundY + hoverHeight;
        position.y = Mathf.MoveTowards(position.y, targetY, adjustSpeed * Time.deltaTime);

        // Trava dura: a aproximação suave demora, e numa subida forte o projétil chegava a
        // ficar abaixo do piso antes de alcançar o alvo. Aqui ele nunca fura o chão.
        float minimo = groundY + Mathf.Min(folgaMinima, hoverHeight);
        if (position.y < minimo)
            position.y = minimo;

        return true;
    }
}
