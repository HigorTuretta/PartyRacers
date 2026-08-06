using UnityEngine;

public class MovingPallet : MonoBehaviour
{
    [Header("Movimento")]
    public float distance = 2f;
    public float speed = 1f;
    public float phaseOffset = 0f;

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float z = Mathf.Sin((Time.time + phaseOffset) * speed) * distance;

        transform.position = new Vector3(
            startPosition.x,
            startPosition.y,
            startPosition.z + z
        );
    }
}