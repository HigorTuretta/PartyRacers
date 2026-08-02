using UnityEngine;

public class CrossbowArrowAnimation : MonoBehaviour
{
    [Header("Movimento da Flecha")]
    public float pullDistance = 0.15f;
    public float pullSpeed = 5f;
    public float releaseSpeed = 25f;

    private Vector3 startPosition;
    private Vector3 pulledPosition;

    private bool releasing = false;


    void Start()
    {
        // Posição inicial da flecha
        startPosition = transform.localPosition;

        // Move para trás no eixo Z
        pulledPosition = startPosition + new Vector3(0, 0, pullDistance);
    }


    void Update()
    {
        if (releasing)
        {
            // Solta a flecha para frente
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                startPosition,
                Time.deltaTime * releaseSpeed
            );


            if (Vector3.Distance(transform.localPosition, startPosition) < 0.001f)
            {
                transform.localPosition = startPosition;
                releasing = false;
            }
        }
        else
        {
            // Mantém a flecha puxada para trás
            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                pulledPosition,
                Time.deltaTime * pullSpeed
            );
        }
    }


    public void ReleaseArrow()
    {
        releasing = true;
    }
}