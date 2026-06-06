using UnityEngine;
using System.Collections;

public class AutoGolfSwing : MonoBehaviour
{
    public float forca = 25f;
    public Transform pontoDeImpacto;
    public Transform direcaoGolpe;

    public float delayEntreSwings = 3f;

    void Start()
    {
        StartCoroutine(SwingLoop());
    }

    IEnumerator SwingLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(delayEntreSwings);
            Swing();
        }
    }

    void Swing()
    {
        StartCoroutine(SwingAnim());

        Collider[] hits = Physics.OverlapSphere(pontoDeImpacto.position, 1.2f);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Bola"))
            {
                Rigidbody rb = hit.GetComponent<Rigidbody>();

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;

                    // direção da bola (continua igual ao seu sistema atual)
                    Vector3 direcao = direcaoGolpe.forward.normalized;

                    rb.AddForce(direcao * forca, ForceMode.Impulse);
                }
            }
        }
    }

    IEnumerator SwingAnim()
    {
        Quaternion startRot = transform.localRotation;

        // 🔥 AGORA O SWING USA EIXO Z (forward do Unity)
        Quaternion endRot = startRot * Quaternion.Euler(0f, 0f, -60f);

        float t = 0f;

        // ida (golpe)
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            transform.localRotation = Quaternion.Slerp(startRot, endRot, t);
            yield return null;
        }

        t = 0f;

        // volta (reset)
        while (t < 1f)
        {
            t += Time.deltaTime * 8f;
            transform.localRotation = Quaternion.Slerp(endRot, startRot, t);
            yield return null;
        }
    }
}