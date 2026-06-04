using UnityEngine;

// Foguete "equipado": fica acima do carro enquanto o poder está disponível, com a frente voltada
// para a frente do carro e uma animação sutil de idle (ansioso para ser lançado). Acompanha o carro
// porque está parentado ao socket. O trail/partículas do prefab ficam desligados enquanto idle.
[DisallowMultipleComponent]
public class RocketEquippedVisual : MonoBehaviour
{
    [Header("Orientação do modelo")]
    [Tooltip("Correção para alinhar a frente do modelo com a frente do carro.")]
    [SerializeField] private Vector3 forwardAxisOffset = new Vector3(90f, 0f, 0f);
    [SerializeField] private float modelScale = 0.12f;

    [Header("Idle ansioso")]
    [SerializeField] private float bobAmplitude = 0.07f;
    [SerializeField] private float bobSpeed = 4.5f;
    [SerializeField] private float wobbleAngle = 6f;
    [SerializeField] private float wobbleSpeed = 6.5f;
    [SerializeField] private float trembleAmplitude = 0.015f;
    [SerializeField] private float trembleSpeed = 35f;

    private KartController kart;
    private Vector3 baseLocalPosition;
    private float seed;

    public void Initialize(KartController ownerKart)
    {
        kart = ownerKart;
    }

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        seed = Random.Range(0f, 100f);
        transform.localScale = Vector3.one * modelScale;
        DisableEmitters();
    }

    private void Update()
    {
        float t = Time.time;

        Vector3 position = baseLocalPosition;
        position.y += Mathf.Sin(t * bobSpeed) * bobAmplitude;
        position.x += (Mathf.PerlinNoise(t * trembleSpeed, seed) - 0.5f) * 2f * trembleAmplitude;
        position.z += (Mathf.PerlinNoise(seed, t * trembleSpeed) - 0.5f) * 2f * trembleAmplitude;
        transform.localPosition = position;

        Quaternion wobble = Quaternion.Euler(
            Mathf.Sin(t * wobbleSpeed) * wobbleAngle,
            Mathf.Sin(t * wobbleSpeed * 0.7f) * wobbleAngle * 0.5f,
            Mathf.Cos(t * wobbleSpeed * 1.1f) * wobbleAngle
        );

        transform.localRotation = Quaternion.Euler(forwardAxisOffset) * wobble;
    }

    private void DisableEmitters()
    {
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            trail.emitting = false;

        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
        }
    }
}
