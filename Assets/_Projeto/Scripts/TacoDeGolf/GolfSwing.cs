using UnityEngine;

public class AutoGolfSwing : MonoBehaviour
{
    [Header("Força do Golpe (corpos físicos genéricos)")]
    public float forcaHorizontal = 200f;
    public float forcaVertical = 50f;

    [Header("Arremesso de karts (arcade, estilo Fall Guys)")]
    [Tooltip("Velocidade horizontal (m/s) do arremesso do kart. 25 ≈ tacada forte.")]
    public float velocidadeArremesso = 25f;
    [Tooltip("Velocidade vertical (m/s) do arremesso do kart.")]
    public float velocidadeVertical = 12f;
    [Tooltip("Tempo (s) em que o kart fica sem controle (projétil).")]
    public float duracaoKnockback = 1.2f;
    [Tooltip("Cambalhota (rad/s) aplicada ao kart.")]
    public float giroArremesso = 8f;

    [Header("Detecção")]
    public Transform pontoDeImpacto;
    public float raioImpacto = 3f;

    [Header("Gangorra Contínua")]
    public float velocidadeSwing = 2f;
    public float anguloSwing = 60f;

    private bool podeBater = true;
    private bool ultimoLadoDireita = false;
    private Quaternion rotacaoCentro;

    void Start()
    {
        rotacaoCentro = transform.localRotation;
    }

    void Update()
    {
        float movimento = Mathf.Sin(Time.time * velocidadeSwing);
        float anguloAtual = movimento * anguloSwing;

        transform.localRotation =
            rotacaoCentro * Quaternion.Euler(0f, 0f, anguloAtual);

        bool ladoDireita = movimento < -0.85f;
        bool ladoEsquerda = movimento > 0.85f;

        if (ladoDireita && !ultimoLadoDireita && podeBater)
        {
            AplicarImpacto(transform.right);
            ultimoLadoDireita = true;
            podeBater = false;
        }

        if (ladoEsquerda && ultimoLadoDireita && podeBater)
        {
            AplicarImpacto(-transform.right);
            ultimoLadoDireita = false;
            podeBater = false;
        }

        if (Mathf.Abs(movimento) < 0.3f)
        {
            podeBater = true;
        }
    }

    void AplicarImpacto(Vector3 direcao)
    {
        Collider[] hits = Physics.OverlapSphere(
            pontoDeImpacto.position,
            raioImpacto
        );

        direcao.Normalize();

        foreach (Collider hit in hits)
        {
            // Karts (player E bots): arremesso arcade via knockback — a velocidade sobrevive
            // aos clamps/grip do KartController e o carro voa de verdade, estilo Fall Guys.
            // (Antes: só funcionava com tag "Player" e o KartController esmagava a velocidade
            // no FixedUpdate seguinte — o carro "mal se movia".)
            KartController kart = hit.GetComponentInParent<KartController>();
            if (kart != null)
            {
                Vector3 launch = direcao * velocidadeArremesso + Vector3.up * velocidadeVertical;
                Vector3 tumble = Vector3.Cross(Vector3.up, direcao) * giroArremesso
                    + Vector3.up * Random.Range(-giroArremesso, giroArremesso) * 0.4f;

                kart.BeginKnockback(launch, duracaoKnockback, tumble);
                continue;
            }

            // Outros corpos físicos (bolas etc.): empurrão simples.
            Rigidbody rb = hit.GetComponentInParent<Rigidbody>();
            if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = (direcao * forcaHorizontal) + (Vector3.up * forcaVertical);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pontoDeImpacto == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pontoDeImpacto.position, raioImpacto);
    }
}