using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taco de golfe gigante: balança feito pêndulo e dá uma TACADA nos karts que passam.
///
/// O que estava errado antes (e por que o carro "só quicava"):
///  - a batida era disparada nos EXTREMOS do arco (|sin| > 0.85), justamente onde o taco está
///    parando para inverter o sentido — visualmente o taco já tinha passado pelo carro;
///  - a direção usada era 'transform.right' NO EXTREMO do arco, ou seja, o eixo lateral já
///    girado 34°+ junto com o taco: apontava para BAIXO. Somado ao empurrão vertical fixo, o
///    resultado líquido era um soco para o chão — o carro batia no piso e quicava;
///  - havia um ObstacleKnockback no MESMO objeto: os dois sistemas arremessavam o mesmo kart em
///    momentos diferentes, e o segundo arremesso sobrescrevia a velocidade do primeiro no meio
///    do voo (o carro subia e era socado para baixo em seguida).
///
/// Agora: a tacada acontece onde a cabeça do taco está RÁPIDA (o meio do arco), a direção sai da
/// velocidade real da cabeça achatada no plano do chão, e a subida vem de um ÂNGULO DE LANÇAMENTO
/// — nunca de um valor que possa ficar negativo. Um kart só pode ser tacado de novo depois que o
/// arremesso anterior termina.
/// </summary>
[DisallowMultipleComponent]
public class AutoGolfSwing : MonoBehaviour
{
    [Header("Gangorra Contínua")]
    [Tooltip("Frequência do balanço (rad/s). Maior = taco mais rápido.")]
    public float velocidadeSwing = 2f;
    [Tooltip("Amplitude do balanço em graus para cada lado.")]
    public float anguloSwing = 60f;

    [Header("Detecção")]
    [Tooltip("Cabeça do taco: é daqui que sai a tacada.")]
    public Transform pontoDeImpacto;
    [Tooltip("Alcance (m) da tacada em volta da cabeça do taco.")]
    public float raioImpacto = 3f;
    [Tooltip("Fração da velocidade máxima da cabeça a partir da qual o taco ACERTA. " +
             "Alto = só o meio do arco bate (parece tacada); 0 = bate até parado.")]
    [Range(0f, 0.9f)] public float forcaMinimaParaBater = 0.45f;

    [Header("Tacada nos karts")]
    [Tooltip("Velocidade horizontal (m/s) da tacada no golpe cheio. Referência: o corredor do taco " +
             "tem 16 m de largura — acima de ~20 m/s o carro é jogado PARA FORA da pista e a " +
             "tacada deixa de ser um susto para virar respawn.")]
    public float velocidadeArremesso = 17f;
    [Tooltip("Ângulo de lançamento acima do horizonte, em graus. É SEMPRE para cima: " +
             "22° manda o carro longe rasante; 45° joga alto e perto.")]
    [Range(0f, 60f)] public float anguloDeLancamento = 22f;
    [Tooltip("Fração da força aplicada quando a cabeça está no limiar mínimo (golpe de raspão).")]
    [Range(0.1f, 1f)] public float forcaNoLimiar = 0.65f;
    [Tooltip("Tempo (s) em que o kart fica sem controle (projétil).")]
    public float duracaoKnockback = 1.0f;
    [Tooltip("Intervalo mínimo (s) entre duas tacadas NO MESMO kart. O taco varre a largura toda " +
             "da pista: sem uma folga generosa aqui, quem cai perto do taco leva tacada atrás de " +
             "tacada e o trecho vira um moedor.")]
    public float intervaloEntreTacadasNoMesmoKart = 3f;
    [Tooltip("Cambalhota (rad/s) aplicada ao kart.")]
    public float giroArremesso = 8f;

    [Header("Outros corpos físicos (bolas etc.)")]
    [Tooltip("Multiplicador da velocidade da cabeça repassada a rigidbodies comuns. " +
             "1 = a bola sai com a velocidade real da tacada.")]
    public float impulsoEmCorposFisicos = 1f;

    [Header("Debug")]
    [SerializeField] private bool debugMode;

    private Quaternion rotacaoCentro;
    private float comprimentoDoBraco;
    private float velocidadeMaximaDaCabeca;
    private readonly Dictionary<KartController, float> ultimaTacada = new Dictionary<KartController, float>();
    private readonly Dictionary<Rigidbody, float> ultimoEmpurrao = new Dictionary<Rigidbody, float>();
    private readonly List<Rigidbody> corposParaRemover = new List<Rigidbody>();

    private void Awake()
    {
        // O taco é animado por SCRIPT: qualquer Rigidbody aqui tem de ser CINEMÁTICO. Estava
        // dinâmico — o que só não aparecia porque o collider sólido apoiava o taco no chão da
        // pista (ele encosta no piso no fundo do arco). Ou seja: o taco era um corpo físico de
        // 31 m sendo teleportado todo frame e sustentado por contato — daí os empurrões
        // imprevisíveis nos karts. Cinemático, ele passa a ser o que sempre deveria ter sido:
        // um obstáculo animado que não é simulado.
        foreach (Rigidbody corpo in GetComponentsInChildren<Rigidbody>(true))
        {
            corpo.isKinematic = true;
            corpo.useGravity = false;
            corpo.interpolation = RigidbodyInterpolation.None;
        }
    }

    private void Start()
    {
        rotacaoCentro = transform.localRotation;

        if (pontoDeImpacto == null)
            pontoDeImpacto = transform;

        comprimentoDoBraco = Vector3.Distance(pontoDeImpacto.position, transform.position);
        velocidadeMaximaDaCabeca = Mathf.Max(0.01f,
            anguloSwing * Mathf.Deg2Rad * velocidadeSwing * comprimentoDoBraco);
    }

    private void Update()
    {
        float fase = Time.time * velocidadeSwing;
        transform.localRotation = rotacaoCentro * Quaternion.Euler(0f, 0f, Mathf.Sin(fase) * anguloSwing);

        // Velocidade da cabeça, analítica: ω × r. É zero nos extremos do arco (onde o taco
        // inverte) e máxima no meio — exatamente a janela em que uma tacada faz sentido.
        float grausPorSegundo = anguloSwing * velocidadeSwing * Mathf.Cos(fase);
        Vector3 omega = transform.forward * (grausPorSegundo * Mathf.Deg2Rad);
        Vector3 velocidadeDaCabeca = Vector3.Cross(omega, pontoDeImpacto.position - transform.position);

        float forca01 = Mathf.Clamp01(velocidadeDaCabeca.magnitude / velocidadeMaximaDaCabeca);
        if (forca01 < forcaMinimaParaBater)
            return;

        Vector3 direcao = velocidadeDaCabeca;
        direcao.y = 0f;
        if (direcao.sqrMagnitude < 0.01f)
            return;
        direcao.Normalize();

        AplicarTacada(direcao, forca01, velocidadeDaCabeca);
    }

    private void AplicarTacada(Vector3 direcao, float forca01, Vector3 velocidadeDaCabeca)
    {
        float escala = Mathf.Lerp(forcaNoLimiar, 1f, Mathf.InverseLerp(forcaMinimaParaBater, 1f, forca01));
        float horizontal = velocidadeArremesso * escala;
        float vertical = horizontal * Mathf.Tan(anguloDeLancamento * Mathf.Deg2Rad);

        Collider[] hits = Physics.OverlapSphere(pontoDeImpacto.position, raioImpacto);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            KartController kart = hit.GetComponentInParent<KartController>();
            if (kart != null)
            {
                TacarKart(kart, direcao, horizontal, vertical);
                continue;
            }

            // Outros corpos físicos (bolas de golfe etc.): levam a velocidade real da cabeça.
            Rigidbody rb = hit.attachedRigidbody;
            if (rb == null || rb.isKinematic)
                continue;

            if (ultimoEmpurrao.TryGetValue(rb, out float quando) && Time.time - quando < 0.25f)
                continue;

            ultimoEmpurrao[rb] = Time.time;
            rb.linearVelocity = velocidadeDaCabeca * impulsoEmCorposFisicos;
        }

        LimparCorposSumidos();
    }

    // As bolas são destruídas alguns segundos depois de nascer; sem esta limpeza o dicionário de
    // cooldown acumularia uma entrada morta por bola pelo resto da corrida.
    private void LimparCorposSumidos()
    {
        if (ultimoEmpurrao.Count < 32)
            return;

        corposParaRemover.Clear();
        foreach (var par in ultimoEmpurrao)
        {
            if (par.Key == null || Time.time - par.Value > 5f)
                corposParaRemover.Add(par.Key);
        }

        for (int i = 0; i < corposParaRemover.Count; i++)
            ultimoEmpurrao.Remove(corposParaRemover[i]);
    }

    private void TacarKart(KartController kart, Vector3 direcao, float horizontal, float vertical)
    {
        // Um kart só pode ser tacado de novo DEPOIS que o arremesso anterior acabou E de sobrar
        // um tempo para ele voltar a andar. Sem isso o taco acerta o mesmo carro várias vezes no
        // mesmo voo (a última batida, quase sempre a pior, é a que vale) e quem pousa por perto
        // nunca mais sai do lugar.
        float bloqueio = Mathf.Max(intervaloEntreTacadasNoMesmoKart, duracaoKnockback + 0.2f);
        if (ultimaTacada.TryGetValue(kart, out float quando) && Time.time - quando < bloqueio)
            return;

        // Não taca quem já está atrás da cabeça (o taco passou reto por ele).
        Vector3 paraOKart = kart.transform.position - pontoDeImpacto.position;
        paraOKart.y = 0f;
        if (paraOKart.sqrMagnitude > 0.01f && Vector3.Dot(paraOKart.normalized, direcao) < -0.35f)
            return;

        ultimaTacada[kart] = Time.time;

        Vector3 launch = direcao * horizontal + Vector3.up * vertical;
        Vector3 tumble = Vector3.Cross(Vector3.up, direcao) * giroArremesso
            + Vector3.up * Random.Range(-giroArremesso, giroArremesso) * 0.4f;

        kart.BeginKnockback(launch, duracaoKnockback, tumble);

        if (debugMode)
        {
            Debug.Log($"[AutoGolfSwing] '{name}' tacou '{kart.name}' " +
                      $"h={horizontal:F1}m/s v={vertical:F1}m/s ang={anguloDeLancamento:F0}deg", this);
            Debug.DrawRay(kart.transform.position, launch * 0.5f, Color.green, 2f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (pontoDeImpacto == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pontoDeImpacto.position, raioImpacto);
    }
}
