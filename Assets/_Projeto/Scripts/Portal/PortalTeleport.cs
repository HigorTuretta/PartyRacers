using UnityEngine;

public class PortalTeleport : MonoBehaviour
{
    [Header("Destino")]
    public Transform portalSaida;

    [Header("Configurações")]
    public float tempoRecarga = 1f;

    private bool podeTeleportar = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!podeTeleportar)
            return;

        if (other.CompareTag("Player"))
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();

            // Move o carro para a saída
            other.transform.position = portalSaida.position;
            other.transform.rotation = portalSaida.rotation;

            // Mantém a velocidade do carro apontando para frente da saída
            if (rb != null)
            {
                float velocidade = rb.linearVelocity.magnitude;
                rb.linearVelocity = portalSaida.forward * velocidade;
            }

            StartCoroutine(RecargaPortal());
        }
    }

    System.Collections.IEnumerator RecargaPortal()
    {
        podeTeleportar = false;
        yield return new WaitForSeconds(tempoRecarga);
        podeTeleportar = true;
    }
}