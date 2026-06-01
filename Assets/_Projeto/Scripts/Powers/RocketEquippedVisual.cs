using UnityEngine;

[DisallowMultipleComponent]
public class RocketEquippedVisual : MonoBehaviour
{
    [Header("Pose idle (relativo ao socket)")]
    [SerializeField] private Vector3 idleLocalPosition = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 idleLocalEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField] private Vector3 idleLocalScale = new Vector3(0.07f, 0.07f, 0.07f);

    [Header("Animacao idle")]
    [Tooltip("Amplitude do bob vertical (metros).")]
    [SerializeField] private float bobAmount = 0.08f;
    [SerializeField] private float bobSpeed = 3.6f;
    [Tooltip("Amplitude da inclinação cartoon (graus).")]
    [SerializeField] private float swayAngle = 6f;
    [SerializeField] private float swaySpeed = 2.2f;
    [Tooltip("Amplitude da pulsação de escala.")]
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.06f;
    [SerializeField] private float pulseSpeed = 5f;
    [Tooltip("Mini-saltinhos rápidos para parecer ansioso (probabilidade por segundo).")]
    [SerializeField, Range(0f, 4f)] private float jitterRate = 1.2f;
    [SerializeField] private float jitterStrength = 0.45f;

    [Header("Reativo ao kart")]
    [SerializeField] private KartController kart;
    [Tooltip("Quanto a inclinação muda com o drift do kart.")]
    [SerializeField] private float driftLeanAmount = 14f;
    [Tooltip("Quanto o foguete desce com a aceleração.")]
    [SerializeField] private float accelDive = 0.06f;
    [SerializeField] private float reactSmooth = 8f;

    [Header("Componentes a desligar em idle")]
    [Tooltip("Filhos do foguete que devem ficar inativos enquanto equipado (ex: trail).")]
    [SerializeField] private string[] idleDisabledChildren = new[] { "RocketTrail" };

    private float phaseOffset;
    private float currentLean;
    private float currentDive;
    private float jitterImpulse;

    public void Initialize(KartController ownerKart)
    {
        kart = ownerKart;
    }

    private void OnEnable()
    {
        phaseOffset = Random.value * Mathf.PI * 2f;
        ApplyIdlePose();
        SetIdleChildrenActive(false);
    }

    private void OnDisable()
    {
        // Re-activate so when the rocket is fired (re-parented), its trail comes back.
        SetIdleChildrenActive(true);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        float t = Time.time * 1f + phaseOffset;

        float bob = Mathf.Sin(t * bobSpeed) * bobAmount;
        float swayX = Mathf.Sin(t * swaySpeed) * swayAngle;
        float swayZ = Mathf.Cos(t * swaySpeed * 0.83f) * swayAngle * 0.6f;
        float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;

        // Random tiny jitters that simulate eagerness
        if (jitterRate > 0f && Random.value < jitterRate * dt)
            jitterImpulse = jitterStrength;
        jitterImpulse = Mathf.MoveTowards(jitterImpulse, 0f, dt * 3f);

        float targetLean = 0f;
        float targetDive = 0f;
        if (kart != null)
        {
            targetLean = -kart.DriftSignedAmount * driftLeanAmount;
            targetDive = kart.ThrottleInput * accelDive;
        }
        currentLean = Mathf.Lerp(currentLean, targetLean, dt * reactSmooth);
        currentDive = Mathf.Lerp(currentDive, targetDive, dt * reactSmooth);

        Vector3 pos = idleLocalPosition;
        pos.y += bob + jitterImpulse * 0.04f - currentDive;
        transform.localPosition = pos;

        Vector3 eul = idleLocalEulerAngles;
        eul.x += swayX;
        eul.y += currentLean;
        eul.z += swayZ + jitterImpulse * 8f;
        transform.localRotation = Quaternion.Euler(eul);

        transform.localScale = idleLocalScale * pulse;
    }

    private void ApplyIdlePose()
    {
        transform.localPosition = idleLocalPosition;
        transform.localRotation = Quaternion.Euler(idleLocalEulerAngles);
        transform.localScale = idleLocalScale;
    }

    private void SetIdleChildrenActive(bool isActive)
    {
        if (idleDisabledChildren == null) return;
        for (int i = 0; i < idleDisabledChildren.Length; i++)
        {
            string childName = idleDisabledChildren[i];
            if (string.IsNullOrEmpty(childName)) continue;
            Transform child = transform.Find(childName);
            if (child != null)
                child.gameObject.SetActive(isActive);
        }
    }
}
