using UnityEngine;

// Gira lentamente o suporte do carro na Garagem para exibição.
public class Turntable : MonoBehaviour
{
    [SerializeField] private float speed = 16f;

    private void Update()
    {
        transform.Rotate(Vector3.up, speed * Time.deltaTime, Space.World);
    }
}
