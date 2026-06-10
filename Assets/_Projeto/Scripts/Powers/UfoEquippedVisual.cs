using UnityEngine;

// Disco voador "equipado": flutua acima do carro enquanto o poder de Swap está disponível,
// girando e oscilando sutilmente como um OVNI de verdade. Acompanha o carro porque está
// parentado ao socket criado pelo KartPowerUser.
[DisallowMultipleComponent]
public class UfoEquippedVisual : MonoBehaviour
{
    [Header("Modelo")]
    [SerializeField] private float modelScale = 0.35f;

    [Header("Idle alienígena")]
    [Tooltip("Velocidade de rotação do disco (graus/s).")]
    [SerializeField] private float spinSpeed = 160f;
    [SerializeField] private float bobAmplitude = 0.12f;
    [SerializeField] private float bobSpeed = 2.6f;
    [Tooltip("Deriva lateral sutil (movimento de 'paira' do disco).")]
    [SerializeField] private float driftAmplitude = 0.08f;
    [SerializeField] private float driftSpeed = 1.4f;
    [Tooltip("Inclinação máxima do disco enquanto paira (graus).")]
    [SerializeField] private float tiltAngle = 4f;

    private Vector3 baseLocalPosition;
    private float spinAngle;
    private float seed;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        seed = Random.Range(0f, 100f);
        transform.localScale = Vector3.one * modelScale;
    }

    private void Update()
    {
        float t = Time.time + seed;

        spinAngle = Mathf.Repeat(spinAngle + spinSpeed * Time.deltaTime, 360f);

        Vector3 position = baseLocalPosition;
        position.y += Mathf.Sin(t * bobSpeed) * bobAmplitude;
        position.x += Mathf.Sin(t * driftSpeed) * driftAmplitude;
        position.z += Mathf.Cos(t * driftSpeed * 0.8f) * driftAmplitude;
        transform.localPosition = position;

        Quaternion tilt = Quaternion.Euler(
            Mathf.Sin(t * bobSpeed * 0.7f) * tiltAngle,
            spinAngle,
            Mathf.Cos(t * bobSpeed * 0.9f) * tiltAngle);

        transform.localRotation = tilt;
    }
}
