/// <summary>
/// Marca um obstáculo que resolve SOZINHO a resposta de impacto contra o kart (arremesso do
/// moinho, tacada do taco, empurrão pesado da bola de golfe...).
///
/// O KartController.HandleCollisionGlide ignora o contato com qualquer collider marcado assim.
/// Sem isso o glide trataria o obstáculo como PAREDE — redirecionando a velocidade ao longo da
/// superfície no mesmo passo de física em que o obstáculo aplicou a própria resposta. As duas
/// escritas disputavam o rb.linearVelocity e o resultado ficava imprevisível (kart "agarrado"
/// na pá do moinho, empurrão da bola sumindo no frame seguinte).
/// </summary>
public interface IKartImpactObstacle
{
}
