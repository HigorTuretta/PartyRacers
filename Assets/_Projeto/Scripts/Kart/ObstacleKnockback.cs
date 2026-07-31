using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coloque este componente em QUALQUER obstáculo do cenário que deva arremessar karts de forma
/// arcade (estilo Fall Guys): pá de moinho, taco de golfe gigante, bola de golfe, pêndulo etc.
/// O collider pode estar neste objeto ou em filhos (sólido ou trigger — os dois funcionam).
///
/// Como funciona:
/// - Ao tocar um kart, calcula a direção do arremesso a partir do MOVIMENTO do obstáculo no
///   ponto de contato (funciona com transform animado, Animator ou Rigidbody — não precisa de
///   física no obstáculo). Se o obstáculo estiver parado, empurra para longe dele.
/// - Chama KartController.BeginKnockback: o kart vira projétil por alguns instantes (sem grip,
///   sem clamps, sem estabilização) e VOA de verdade, com cambalhota.
/// - Cooldown por kart evita re-arremessar a cada frame de contato.
///
/// Tuning rápido: launchSpeed/upwardSpeed mandam na distância/altura do voo;
/// spinStrength na cambalhota; knockbackDuration em quanto tempo o kart fica sem controle.
/// </summary>
[DisallowMultipleComponent]
public class ObstacleKnockback : MonoBehaviour, IKartImpactObstacle
{
    [Header("Arremesso")]
    [Tooltip("Velocidade horizontal (m/s) aplicada ao kart. 18 ≈ voo de vários metros.")]
    [SerializeField] private float launchSpeed = 18f;
    [Tooltip("Velocidade vertical (m/s) aplicada ao kart (altura do voo).")]
    [SerializeField] private float upwardSpeed = 9f;
    [Tooltip("Quanto da velocidade REAL do obstáculo no ponto de contato é somada ao arremesso. " +
             "1 = pá rápida arremessa mais longe que pá lenta.")]
    [SerializeField, Range(0f, 2f)] private float obstacleVelocityInfluence = 0.6f;
    [Tooltip("Tempo (s) em que o kart fica sem controle/grip (projétil).")]
    [SerializeField] private float knockbackDuration = 1.1f;
    [Tooltip("Giro/cambalhota (rad/s) aplicado ao kart no arremesso. 0 = sem giro.")]
    [SerializeField] private float spinStrength = 7f;

    [Header("Detecção")]
    [Tooltip("Tempo mínimo (s) entre dois arremessos do MESMO kart por este obstáculo.")]
    [SerializeField] private float perKartCooldown = 1.0f;
    [Tooltip("Velocidade relativa mínima (m/s) para disparar. 0 = qualquer toque arremessa.")]
    [SerializeField] private float minRelativeSpeed = 0f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    // Última pose do obstáculo para estimar a velocidade do ponto de contato (obstáculos
    // animados por transform/Animator não têm Rigidbody com velocidade).
    private Matrix4x4 previousLocalToWorld;
    private float previousPoseTime;
    private readonly Dictionary<KartController, float> lastHitTime = new Dictionary<KartController, float>();

    private void OnEnable()
    {
        previousLocalToWorld = transform.localToWorldMatrix;
        previousPoseTime = Time.time;
    }

    private void LateUpdate()
    {
        // Captura a pose uma vez por frame, DEPOIS do Animator mover o obstáculo.
        previousLocalToWorld = transform.localToWorldMatrix;
        previousPoseTime = Time.time;
    }

    private void OnCollisionEnter(Collision collision) => TryLaunch(collision.collider, AverageContact(collision));
    private void OnCollisionStay(Collision collision) => TryLaunch(collision.collider, AverageContact(collision));
    private void OnTriggerEnter(Collider other) => TryLaunch(other, other.ClosestPoint(transform.position));
    private void OnTriggerStay(Collider other) => TryLaunch(other, other.ClosestPoint(transform.position));

    private static Vector3 AverageContact(Collision collision)
    {
        int count = collision.contactCount;
        if (count == 0)
            return collision.collider != null ? collision.collider.bounds.center : Vector3.zero;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < count; i++)
            sum += collision.GetContact(i).point;

        return sum / count;
    }

    private void TryLaunch(Collider other, Vector3 contactPoint)
    {
        if (other == null)
            return;

        KartController kart = other.GetComponentInParent<KartController>();
        if (kart == null)
            return;

        if (lastHitTime.TryGetValue(kart, out float last) && Time.time - last < perKartCooldown)
            return;

        Vector3 obstaclePointVelocity = EstimatePointVelocity(contactPoint);

        if (minRelativeSpeed > 0f)
        {
            Vector3 kartVelocity = kart.Rigidbody != null ? kart.Rigidbody.linearVelocity : Vector3.zero;
            if ((obstaclePointVelocity - kartVelocity).magnitude < minRelativeSpeed)
                return;
        }

        // Direção horizontal: para onde o obstáculo se move; se parado, para longe do obstáculo.
        Vector3 horizontal = obstaclePointVelocity;
        horizontal.y = 0f;
        if (horizontal.sqrMagnitude < 0.25f)
        {
            horizontal = kart.transform.position - contactPoint;
            horizontal.y = 0f;
        }
        if (horizontal.sqrMagnitude < 0.01f)
            horizontal = -kart.transform.forward;

        horizontal.Normalize();

        Vector3 launch = horizontal * launchSpeed
            + Vector3.up * upwardSpeed
            + obstaclePointVelocity * obstacleVelocityInfluence;

        // Cambalhota: gira em torno de um eixo horizontal perpendicular ao voo (+ um tempero).
        Vector3 tumbleAxis = Vector3.Cross(Vector3.up, horizontal).normalized;
        Vector3 angular = tumbleAxis * spinStrength
            + Vector3.up * Random.Range(-spinStrength, spinStrength) * 0.4f;

        kart.BeginKnockback(launch, knockbackDuration, angular);
        lastHitTime[kart] = Time.time;

        if (debugMode)
        {
            Debug.Log($"[ObstacleKnockback] '{name}' arremessou '{kart.name}' v={launch.magnitude:F1} m/s", this);
            Debug.DrawRay(contactPoint, launch, Color.magenta, 1.5f);
        }
    }

    // Velocidade do ponto de contato a partir do delta da matriz do transform entre frames.
    // Funciona para hélices girando, taco balançando, plataformas, bolas com RB — qualquer coisa.
    private Vector3 EstimatePointVelocity(Vector3 worldPoint)
    {
        float dt = Time.time - previousPoseTime;
        if (dt <= 0.0001f)
            return Vector3.zero;

        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 previousWorldPoint = previousLocalToWorld.MultiplyPoint3x4(localPoint);
        Vector3 velocity = (worldPoint - previousWorldPoint) / dt;

        // Proteção contra teleporte/primeiro frame.
        return velocity.sqrMagnitude > 60f * 60f ? Vector3.zero : velocity;
    }
}
