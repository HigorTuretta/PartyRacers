using UnityEngine;

// Efeito de "rodada"/perda breve de controle aplicado a um kart atingido pelo foguete.
// Substitui o antigo stun do Eletro-Gel mantendo apenas o que importa para o balanceamento:
// o carro perde o controle por um instante e leva um empurrão (knockback) coerente com o impacto.
[DisallowMultipleComponent]
public class KartSpinOutEffect : MonoBehaviour
{
    private KartController kart;
    private Rigidbody body;
    private float endTime;
    private bool active;
    private bool controlWasEnabled;

    public static void ApplyTo(
        GameObject target,
        float duration,
        Vector3 knockbackDirection,
        float knockbackForce,
        float spinTorque)
    {
        if (target == null)
            return;

        KartSpinOutEffect effect = target.GetComponent<KartSpinOutEffect>();
        if (effect == null)
            effect = target.AddComponent<KartSpinOutEffect>();

        effect.Begin(duration, knockbackDirection, knockbackForce, spinTorque);
    }

    private void Awake()
    {
        kart = GetComponent<KartController>();
        body = GetComponent<Rigidbody>();
    }

    private void Begin(float duration, Vector3 knockbackDirection, float knockbackForce, float spinTorque)
    {
        if (!active)
        {
            controlWasEnabled = kart == null || kart.CanControl;

            if (kart != null)
                kart.SetControlEnabled(false); // zera a velocidade; o knockback é aplicado em seguida
        }

        active = true;
        endTime = Mathf.Max(endTime, Time.time + duration);

        if (body != null)
        {
            knockbackDirection.y = 0f;

            if (knockbackDirection.sqrMagnitude > 0.001f)
                body.AddForce(knockbackDirection.normalized * knockbackForce, ForceMode.VelocityChange);

            body.AddForce(Vector3.up * knockbackForce * 0.25f, ForceMode.VelocityChange);

            float spinSign = Random.value < 0.5f ? -1f : 1f;
            body.AddTorque(Vector3.up * spinTorque * spinSign, ForceMode.VelocityChange);
        }
    }

    private void Update()
    {
        if (!active)
            return;

        if (Time.time < endTime)
            return;

        End();
    }

    private void End()
    {
        active = false;

        if (kart != null && controlWasEnabled)
            kart.SetControlEnabled(true);

        Destroy(this);
    }

    private void OnDisable()
    {
        if (active)
            End();
    }
}
