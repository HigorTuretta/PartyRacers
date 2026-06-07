using UnityEngine;

public class AutoGolfSwing : MonoBehaviour
{
    [Header("Força do Golpe")]
    public float forcaHorizontal = 200f;
    public float forcaVertical = 50f;

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

        foreach (Collider hit in hits)
        {
            Rigidbody rb = hit.GetComponentInParent<Rigidbody>();

            if (rb != null && rb.CompareTag("Player"))
            {
                direcao.Normalize();

                rb.linearVelocity =
                    (direcao * forcaHorizontal) +
                    (Vector3.up * forcaVertical);

                rb.angularVelocity = new Vector3(
                    Random.Range(-15f, 15f),
                    Random.Range(-15f, 15f),
                    Random.Range(-15f, 15f)
                );
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