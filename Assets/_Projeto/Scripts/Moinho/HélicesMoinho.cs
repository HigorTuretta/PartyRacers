using UnityEngine;

/// <summary>
/// Gira as pás do moinho em torno do eixo do próprio pivô.
///
/// Por que não é só um transform.Rotate: as pás têm Rigidbody CINEMÁTICO (são obstáculos que
/// arremessam karts). Um Rigidbody cinemático com interpolação ligada faz o PhysX reescrever a
/// pose INTERPOLADA por cima do transform a cada frame renderizado. Como quem gira é o pai (este
/// pivô, por script) e não o próprio Rigidbody, as duas escritas brigam: a rotação do frame é
/// aplicada e logo em seguida descartada pela interpolação, e a hélice anda "aos soquinhos" —
/// a rotação parece travada mesmo com a velocidade certa.
///
/// A correção tem duas partes:
/// 1) desligar a interpolação dos Rigidbodies filhos (eles seguem o pai, não se movem sozinhos);
/// 2) girar por ÂNGULO ABSOLUTO acumulado em vez de somar deltas, para não acumular erro de
///    ponto flutuante nem depender de o frame anterior ter sido aplicado.
/// </summary>
[DisallowMultipleComponent]
public class HelicesMoinho : MonoBehaviour
{
    [Tooltip("Velocidade de rotação em graus por segundo.")]
    public float speed = 100f;

    [Tooltip("Eixo de rotação em espaço LOCAL do pivô. Z é o padrão do modelo do moinho.")]
    [SerializeField] private Vector3 eixoLocal = Vector3.forward;

    [Tooltip("Segundos até a hélice atingir a velocidade final ao entrar em cena. " +
             "0 = já começa na velocidade cheia.")]
    [SerializeField] private float tempoDeArranque = 1.2f;

    private Quaternion rotacaoInicial;
    private float anguloAcumulado;
    private float tempoLigada;

    private void Awake()
    {
        rotacaoInicial = transform.localRotation;
        PrepararRigidbodiesDasPas();
    }

    private void Update()
    {
        if (eixoLocal.sqrMagnitude < 0.0001f)
            return;

        tempoLigada += Time.deltaTime;

        float rampa = tempoDeArranque > 0.01f
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tempoLigada / tempoDeArranque))
            : 1f;

        anguloAcumulado += speed * rampa * Time.deltaTime;
        if (anguloAcumulado > 360f || anguloAcumulado < -360f)
            anguloAcumulado %= 360f;

        transform.localRotation = rotacaoInicial * Quaternion.AngleAxis(anguloAcumulado, eixoLocal.normalized);
    }

    /// <summary>
    /// As pás são arrastadas por este pivô — nenhuma delas se move por conta própria. Deixá-las
    /// cinemáticas SEM interpolação é o que impede o PhysX de sobrescrever a rotação do script.
    /// </summary>
    private void PrepararRigidbodiesDasPas()
    {
        foreach (Rigidbody corpo in GetComponentsInChildren<Rigidbody>(true))
        {
            if (corpo.transform == transform)
                continue;

            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.interpolation = RigidbodyInterpolation.None;
        }
    }
}
