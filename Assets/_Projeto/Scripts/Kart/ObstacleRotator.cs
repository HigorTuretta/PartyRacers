using UnityEngine;

/// <summary>
/// Gira continuamente um obstáculo do cenário (pás de moinho, plataforma giratória...).
/// Mais flexível que o HelicesMoinho: o eixo é configurável em espaço LOCAL.
/// Combine com ObstacleKnockback nos colliders para o obstáculo arremessar karts.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleRotator : MonoBehaviour
{
    [Tooltip("Eixo de rotação em espaço LOCAL deste transform.")]
    [SerializeField] private Vector3 localAxis = Vector3.forward;

    [Tooltip("Velocidade de rotação em graus/segundo.")]
    [SerializeField] private float degreesPerSecond = 40f;

    public void Configure(Vector3 axis, float speed)
    {
        localAxis = axis;
        degreesPerSecond = speed;
    }

    private void Update()
    {
        if (localAxis.sqrMagnitude < 0.0001f)
            return;

        transform.Rotate(localAxis.normalized * (degreesPerSecond * Time.deltaTime), Space.Self);
    }
}
