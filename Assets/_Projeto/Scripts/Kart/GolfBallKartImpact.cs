using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resposta de impacto das BOLAS DE GOLFE contra os karts.
///
/// Substitui o ObstacleKnockback que estava nos prefabs das bolas: lá qualquer encostão virava
/// arremesso arcade (o kart perdia o controle por ~1s e voava com cambalhota). A leitura em jogo
/// era "o carro vira uma mola" — a bola batia e catapultava.
///
/// Aqui a bola é PESADA, não elástica: é uma bola de golfe atingindo um carrinho de brinquedo.
/// O contato
///   - tira uma mordida da velocidade (proporcional ao TAMANHO da bola),
///   - empurra o kart para o lado do impacto,
///   - NÃO tira o controle e NÃO levanta o carro (a componente vertical é limitada),
///   - nunca zera a velocidade: sempre sobra a fração 'retencaoMinima' do que o kart tinha.
/// A bola, por ser pesada, praticamente não ricocheteia — segue o próprio caminho.
///
/// Tuning: 'raioDeReferencia' é o raio da bola do canhão (a menor). Todo o resto é escalonado por
/// raio/raioDeReferencia — dobrar o raio dobra a mordida e o empurrão, até os tetos configurados.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class GolfBallKartImpact : MonoBehaviour, IKartImpactObstacle
{
    [Header("Escala pelo tamanho")]
    [Tooltip("Raio de mundo (m) que vale como bola 'padrão'. É o raio da bola do canhão.")]
    [SerializeField] private float raioDeReferencia = 1.5f;
    [Tooltip("Teto do fator de tamanho, para uma bola gigante não virar parede.")]
    [SerializeField, Range(1f, 6f)] private float fatorDeTamanhoMaximo = 2.5f;

    [Header("Peso da bola (massa do Rigidbody)")]
    [Tooltip("Densidade arcade (kg/m³) usada para calcular a massa a partir do raio. " +
             "0 = não mexer na massa autorada no prefab.")]
    [SerializeField] private float densidade = 10f;
    [SerializeField] private float massaMinima = 120f;
    [SerializeField] private float massaMaxima = 1500f;
    [Tooltip("Fração da própria velocidade que a bola mantém ao atravessar o kart. " +
             "Alto = bola pesada, segue o caminho; baixo = bola leve, ricocheteia.")]
    [SerializeField, Range(0f, 1f)] private float retencaoDaBola = 0.85f;

    [Header("Mordida na velocidade do kart")]
    [Tooltip("Fração da velocidade que a bola de referência tira num impacto cheio.")]
    [SerializeField, Range(0f, 0.9f)] private float perdaNoRaioDeReferencia = 0.22f;
    [Tooltip("Teto da mordida — o kart NUNCA perde mais que isto num único toque.")]
    [SerializeField, Range(0f, 0.9f)] private float perdaMaxima = 0.45f;
    [Tooltip("Piso absoluto: fração da velocidade pré-impacto que sempre sobra (anti-parede).")]
    [SerializeField, Range(0.3f, 1f)] private float retencaoMinima = 0.5f;

    [Header("Empurrão")]
    [Tooltip("Empurrão lateral (m/s) da bola de referência num impacto cheio.")]
    [SerializeField] private float empurraoNoRaioDeReferencia = 3f;
    [Tooltip("Teto do empurrão (m/s). Referência: batida kart-a-kart forte usa ~7.")]
    [SerializeField] private float empurraoMaximo = 7f;
    [Tooltip("Giro (rad/s) que tira o kart da linha. Baixo de propósito: desestabiliza, não roda.")]
    [SerializeField] private float giroMaximo = 1.2f;
    [Tooltip("Teto da velocidade vertical do kart depois do toque. É o que impede o 'quique de mola'.")]
    [SerializeField] private float verticalMaxima = 1.6f;

    [Header("Detecção")]
    [Tooltip("Velocidade de aproximação (m/s) abaixo da qual o toque é ignorado (bola encostada).")]
    [SerializeField] private float velocidadeMinimaDeImpacto = 3f;
    [Tooltip("Velocidade de aproximação (m/s) que vale como impacto CHEIO.")]
    [SerializeField] private float velocidadeDeImpactoForte = 22f;
    [Tooltip("Tempo (s) mínimo entre dois impactos da mesma bola no mesmo kart.")]
    [SerializeField] private float cooldownPorKart = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private Rigidbody body;
    private float fatorDeTamanho = 1f;
    private Vector3 velocidadeAnterior;
    private readonly Dictionary<KartController, float> ultimoImpacto = new Dictionary<KartController, float>();

    private void Awake()
    {
        body = GetComponent<Rigidbody>();

        float raio = RaioDeMundo();
        fatorDeTamanho = Mathf.Clamp(raio / Mathf.Max(0.01f, raioDeReferencia), 0.2f, fatorDeTamanhoMaximo);

        if (densidade > 0f)
            body.mass = Mathf.Clamp(densidade * (4f / 3f) * Mathf.PI * raio * raio * raio, massaMinima, massaMaxima);

        // Bola pequena e rápida com detecção discreta atravessa o kart sem gerar contato.
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    private void FixedUpdate()
    {
        velocidadeAnterior = body.linearVelocity;
    }

    private void OnCollisionEnter(Collision collision) => TryImpact(collision);
    private void OnCollisionStay(Collision collision) => TryImpact(collision);

    private float RaioDeMundo()
    {
        SphereCollider esfera = GetComponent<SphereCollider>();
        if (esfera != null)
        {
            Vector3 s = esfera.transform.lossyScale;
            return esfera.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        Collider qualquer = GetComponent<Collider>();
        return qualquer != null ? qualquer.bounds.extents.magnitude * 0.577f : raioDeReferencia;
    }

    private void TryImpact(Collision collision)
    {
        KartController kart = collision.collider != null ? collision.collider.GetComponentInParent<KartController>() : null;
        if (kart == null || kart.Rigidbody == null)
            return;

        if (ultimoImpacto.TryGetValue(kart, out float quando) && Time.time - quando < cooldownPorKart)
            return;

        Rigidbody kartBody = kart.Rigidbody;

        // Direção do empurrão: do ponto de contato para o kart, no plano do chão.
        Vector3 contato = PontoDeContato(collision, kart.transform.position);
        Vector3 empurraoDir = Planar(kart.transform.position - contato);
        if (empurraoDir.sqrMagnitude < 0.01f)
            empurraoDir = Planar(velocidadeAnterior);
        if (empurraoDir.sqrMagnitude < 0.01f)
            return;
        empurraoDir.Normalize();

        // Velocidade de APROXIMAÇÃO: quanto a bola estava entrando no kart. Encostão parado = 0.
        Vector3 velRelativa = velocidadeAnterior - kart.PreviousVelocity;
        float aproximacao = Vector3.Dot(velRelativa, empurraoDir);
        if (aproximacao < velocidadeMinimaDeImpacto)
            return;

        float intensidade01 = Mathf.Clamp01(
            Mathf.InverseLerp(velocidadeMinimaDeImpacto, velocidadeDeImpactoForte, aproximacao));

        ultimoImpacto[kart] = Time.time;

        // ---- Mordida na velocidade (é isto que dá o "peso" da bola) ----
        Vector3 preFlat = Planar(kart.PreviousVelocity);
        float perda01 = Mathf.Min(perdaMaxima, perdaNoRaioDeReferencia * fatorDeTamanho * intensidade01);
        float alvo = preFlat.magnitude * Mathf.Max(retencaoMinima, 1f - perda01);

        Vector3 atualFlat = Planar(kartBody.linearVelocity);
        Vector3 direcao = atualFlat.sqrMagnitude > 0.04f ? atualFlat.normalized
                        : preFlat.sqrMagnitude > 0.04f ? preFlat.normalized
                        : kart.transform.forward;

        Vector3 novaFlat = direcao * alvo + empurraoDir * Mathf.Min(
            empurraoMaximo, empurraoNoRaioDeReferencia * fatorDeTamanho * intensidade01);

        // ---- Vertical: a bola empurra, nunca catapulta ----
        float vertical = Mathf.Clamp(kartBody.linearVelocity.y, -verticalMaxima, verticalMaxima);
        kartBody.linearVelocity = new Vector3(novaFlat.x, vertical, novaFlat.z);

        // ---- Desestabiliza um pouco (sai da linha, sem rodar) ----
        float lado = Mathf.Sign(Vector3.Dot(kart.transform.right, empurraoDir));
        kartBody.AddTorque(Vector3.up * (lado * giroMaximo * intensidade01), ForceMode.VelocityChange);

        // Abre a janela de aderência reduzida: sem isto o grip lateral do KartController apagaria
        // o empurrão no mesmo passo de física e a bola pareceria não ter peso nenhum.
        kart.NotifyKartCollisionRecovery(intensidade01);

        // ---- A bola é pesada: segue o caminho em vez de ricochetear ----
        if (retencaoDaBola > 0f && velocidadeAnterior.sqrMagnitude > 0.01f)
        {
            Vector3 depois = body.linearVelocity;
            Vector3 mantida = velocidadeAnterior * retencaoDaBola;
            body.linearVelocity = Vector3.Lerp(depois, mantida, retencaoDaBola);
        }

        if (debugMode)
        {
            Debug.Log($"[GolfBallKartImpact] '{name}' (r={RaioDeMundo():F1}m fator={fatorDeTamanho:F2}) " +
                      $"em '{kart.name}': aprox={aproximacao:F1}m/s perda={perda01 * 100f:F0}% " +
                      $"{preFlat.magnitude * 3.6f:F0}->{alvo * 3.6f:F0}km/h", this);
            Debug.DrawRay(contato, empurraoDir * 5f, Color.cyan, 1f);
        }
    }

    private static Vector3 PontoDeContato(Collision collision, Vector3 fallback)
    {
        int count = collision.contactCount;
        if (count == 0)
            return fallback;

        Vector3 soma = Vector3.zero;
        for (int i = 0; i < count; i++)
            soma += collision.GetContact(i).point;

        return soma / count;
    }

    private static Vector3 Planar(Vector3 v)
    {
        v.y = 0f;
        return v;
    }
}
