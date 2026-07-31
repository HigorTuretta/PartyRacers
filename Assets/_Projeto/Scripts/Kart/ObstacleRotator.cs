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

    private void Awake()
    {
        // Os colliders filhos são ARRASTADOS por este transform — nenhum se move por conta
        // própria. Um Rigidbody cinemático com interpolação neles faz o PhysX reescrever a pose
        // interpolada por cima da rotação do script a cada frame: as duas escritas brigam e o
        // obstáculo anda aos soquinhos (foi o que travava as pás do moinho).
        foreach (Rigidbody corpo in GetComponentsInChildren<Rigidbody>(true))
        {
            if (corpo.transform == transform)
                continue;

            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.interpolation = RigidbodyInterpolation.None;
        }
    }

    private void Update()
    {
        if (localAxis.sqrMagnitude < 0.0001f)
            return;

        transform.Rotate(localAxis.normalized * (degreesPerSecond * Time.deltaTime), Space.Self);
    }
}
