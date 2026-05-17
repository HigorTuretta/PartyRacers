using UnityEngine;

public class DriftPuffBubble : MonoBehaviour
{
    [Header("Vida")]
    [SerializeField] private float lifetime = 0.85f;

    [Header("Escala")]
    [SerializeField] private float startScale = 0.25f;
    [SerializeField] private float endScale = 1.15f;

    [Header("Movimento")]
    [SerializeField] private Vector3 initialVelocity = new Vector3(0f, 0.18f, -0.05f);
    [SerializeField] private float damping = 2.8f;

    [Header("Forma")]
    [SerializeField] private float squashVariation = 0.22f;

    private float timer;
    private Vector3 currentVelocity;
    private Vector3 randomRotationSpeed;
    private Vector3 scaleMultiplier;

    public void Initialize(
        float customLifetime,
        float customStartScale,
        float customEndScale,
        Vector3 customVelocity)
    {
        lifetime = customLifetime;
        startScale = customStartScale;
        endScale = customEndScale;
        currentVelocity = customVelocity;

        timer = 0f;

        scaleMultiplier = new Vector3(
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation)
        );

        randomRotationSpeed = new Vector3(
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f)
        );

        transform.localScale = Vector3.one * startScale;
    }

    private void Awake()
    {
        if (currentVelocity == Vector3.zero)
            currentVelocity = initialVelocity;

        scaleMultiplier = new Vector3(
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation),
            Random.Range(1f - squashVariation, 1f + squashVariation)
        );

        randomRotationSpeed = new Vector3(
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f),
            Random.Range(-45f, 45f)
        );
    }

    private void Update()
    {
        timer += Time.deltaTime;

        float t = Mathf.Clamp01(timer / lifetime);

        transform.position += currentVelocity * Time.deltaTime;
        currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, damping * Time.deltaTime);

        float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
        float shrink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, t));

        float scale = Mathf.Lerp(startScale, endScale, grow);
        scale *= Mathf.Lerp(1f, 0.05f, shrink);

        transform.localScale = Vector3.one * scale;
        transform.localScale = Vector3.Scale(transform.localScale, scaleMultiplier);

        transform.Rotate(randomRotationSpeed * Time.deltaTime, Space.Self);

        if (t >= 1f)
            Destroy(gameObject);
    }
}