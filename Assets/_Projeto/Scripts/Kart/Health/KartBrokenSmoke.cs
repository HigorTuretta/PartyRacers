using UnityEngine;

/// <summary>
/// Fumaça saindo do kart enquanto ele está quebrado.
///
/// O estado quebrado só existia na HUD: a barra virava contagem e mais nada acontecia no mundo.
/// Quem estava atrás não tinha como saber que o carro da frente estava avariado, e o próprio
/// jogador — que nessa hora está olhando para a pista, não para o canto da tela — só percebia pelo
/// carro não responder. A fumaça põe o estado ONDE ele acontece.
///
/// O efeito é instanciado UMA vez e reaproveitado: o pack do Hovl usa sistemas de partículas com
/// vários módulos, e criar/destruir a cada avaria custaria mais que mantê-lo parado.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KartHealth))]
public class KartBrokenSmoke : MonoBehaviour
{
    [Tooltip("Prefab de fumaça em laço. Padrão: Hovl Studio/Magic effects pack/Smoke loop.")]
    [SerializeField] private GameObject fumacaPrefab;

    [Tooltip("Onde a fumaça nasce. Vazio = calculado a partir da caixa do kart (capô/motor).")]
    [SerializeField] private Transform saida;

    [Tooltip("Deslocamento em relação ao kart, quando não há saída própria. Fica sobre o capô/motor.")]
    [SerializeField] private Vector3 deslocamento = new Vector3(0f, 0.6f, -0.1f);

    [Tooltip("Escala do efeito. O pack do Hovl é feito para magia em escala de personagem: em " +
             "0,35 a coluna de fumaça subia dez metros e tapava a pista de quem vinha atrás.")]
    [SerializeField, Min(0.02f)] private float escala = 0.1f;

    private KartHealth vida;
    private GameObject fumaca;
    private ParticleSystem[] sistemas;
    private bool ligada;

    private void Awake() => vida = GetComponent<KartHealth>();

    private void OnDisable() => Desligar();

    private void LateUpdate()
    {
        if (vida == null)
            return;

        bool quebrado = vida.IsBroken;

        if (quebrado == ligada)
            return;

        if (quebrado)
            Ligar();
        else
            Desligar();
    }

    private void Ligar()
    {
        if (fumacaPrefab == null)
            return;

        if (fumaca == null)
        {
            Transform pai = saida != null ? saida : transform;
            fumaca = Instantiate(fumacaPrefab, pai);
            fumaca.name = "FumacaDeAvaria";
            fumaca.transform.localPosition = saida != null ? Vector3.zero : deslocamento;
            fumaca.transform.localRotation = Quaternion.identity;
            fumaca.transform.localScale = Vector3.one * escala;

            sistemas = fumaca.GetComponentsInChildren<ParticleSystem>(true);
        }

        fumaca.SetActive(true);

        if (sistemas != null)
            foreach (ParticleSystem s in sistemas)
                if (s != null)
                    s.Play(true);

        ligada = true;
    }

    private void Desligar()
    {
        ligada = false;

        if (fumaca == null)
            return;

        // Para de EMITIR e deixa o que está no ar terminar; um corte seco faria a nuvem sumir
        // instantaneamente, o que lê como falha de efeito e não como conserto.
        if (sistemas != null)
            foreach (ParticleSystem s in sistemas)
                if (s != null)
                    s.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
