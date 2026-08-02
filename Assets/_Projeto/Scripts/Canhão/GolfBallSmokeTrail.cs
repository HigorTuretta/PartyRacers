using UnityEngine;

/// <summary>
/// Rastro de fumaça das bolas de golfe, no mesmo vocabulário visual da derrapagem do kart:
/// reaproveita o <see cref="DriftPuffBubble"/> (mesma nuvem low-poly, mesmo material, mesmo pool).
///
/// Substitui o efeito de impacto brilhante que era instanciado a cada 0,1 s enquanto a bola encostava
/// no chão — aquilo virava um rastro de faíscas azuis fora do tema e mantinha dezenas de
/// ParticleSystems HDR vivos ao mesmo tempo.
///
/// O rastro é por distância percorrida (e não por tempo): bola parada não solta fumaça, bola rápida
/// deixa um rastro contínuo, independente do frame rate.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GolfBallSmokeTrail : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private DriftPuffBubble puffPrefab;

    [Header("Ativação")]
    [Tooltip("Abaixo desta velocidade (m/s) a bola não solta fumaça.")]
    [SerializeField] private float velocidadeMinima = 4f;
    [Tooltip("Velocidade em que o rastro fica com a densidade máxima.")]
    [SerializeField] private float velocidadeMaxima = 32f;

    // As bolas variam muito de tamanho (a do canhão tem ~1,5 m de raio, a da armadilha ~3 m). Por
    // isso densidade, tamanho e dispersão são múltiplos do RAIO da bola, medido no Awake: assim o
    // rastro fica proporcional em qualquer bola, sem ajuste manual por prefab.

    // Densidade calibrada medindo em playmode: com espaçamento de 0,45 raio havia ~880 puffs vivos
    // ao mesmo tempo (5 canhões × ~14 bolas simultâneas), o que virava parede de fumaça e, como cada
    // puff é um renderer com MaterialPropertyBlock, uma draw call cada. Com os valores abaixo o
    // rastro continua contínuo mas fica na casa das centenas baixas.
    [Header("Densidade (em raios de bola)")]
    [Tooltip("Espaçamento entre puffs quando a bola está lenta.")]
    [SerializeField] private float espacamentoLento = 2.4f;
    [Tooltip("Espaçamento entre puffs quando a bola está rápida.")]
    [SerializeField] private float espacamentoRapido = 1.2f;
    [SerializeField] private int puffsPorPasso = 1;
    [SerializeField] private int maxPassosPorFrame = 2;

    [Header("Tamanho (em raios de bola)")]
    [SerializeField] private float escalaInicialMin = 0.20f;
    [SerializeField] private float escalaInicialMax = 0.38f;
    [SerializeField] private float escalaFinalMin = 0.70f;
    [SerializeField] private float escalaFinalMax = 1.15f;

    [Header("Vida")]
    [SerializeField] private float vidaMin = 0.40f;
    [SerializeField] private float vidaMax = 0.70f;

    [Header("Dispersão")]
    [Tooltip("Raio de espalhamento do ponto de spawn, em raios de bola.")]
    [SerializeField] private float espalhamento = 0.55f;
    [SerializeField] private float velocidadeParaCima = 0.90f;
    [SerializeField] private float velocidadeLateral = 0.65f;
    [Tooltip("Fração da velocidade da bola herdada pelo puff (dá a sensação de arrasto).")]
    [SerializeField, Range(0f, 0.5f)] private float arrasto = 0.12f;

    [Header("Impacto")]
    [Tooltip("Baforada extra quando a bola bate em algo com força.")]
    [SerializeField] private int puffsNoImpacto = 4;
    [SerializeField] private float impactoVelocidadeMinima = 6f;
    [SerializeField] private float impactoIntervalo = 0.15f;
    [SerializeField] private float impactoEscalaExtra = 1.5f;

    private Rigidbody corpo;
    private Vector3 ultimaPosicao;
    private float acumuladorDistancia;
    private float proximoImpacto;
    private float raioBola = 1f;

    private void Awake()
    {
        corpo = GetComponent<Rigidbody>();
        ultimaPosicao = transform.position;
        raioBola = MedirRaio();
    }

    private static readonly RaycastHit[] sondaChao = new RaycastHit[8];

    /// <summary>
    /// Procura o chão logo abaixo da bola. É a fonte da fumaça e, ao mesmo tempo, o teste de contato:
    /// a poeira é levantada pelo ATRITO da bola com o piso, então bola no ar (voo do canhão, salto,
    /// queda) não solta rastro nenhum.
    /// </summary>
    /// <param name="ponto">Ponto de contato no chão, válido só quando o retorno é true.</param>
    private bool TocandoOChao(out Vector3 ponto)
    {
        Vector3 centro = transform.position;

        // Margem pequena sobre o raio: a bola quica o tempo todo e um teste exato faria o rastro
        // piscar a cada micro-salto.
        float alcance = raioBola * 1.12f;

        // O raio parte de dentro do colisor da própria bola, então os hits dela precisam ser
        // descartados — senão o "chão" seria a própria casca da esfera.
        int n = Physics.RaycastNonAlloc(centro, Vector3.down, sondaChao, alcance, ~0, QueryTriggerInteraction.Ignore);

        float melhor = float.MaxValue;
        ponto = Vector3.zero;
        bool achou = false;

        for (int i = 0; i < n; i++)
        {
            RaycastHit h = sondaChao[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform))
                continue;

            if (h.distance >= melhor)
                continue;

            melhor = h.distance;
            ponto = h.point;
            achou = true;
        }

        return achou;
    }

    /// <summary>
    /// Raio da bola em metros de mundo. Os prefabs usam colisor de raio minúsculo com escala enorme
    /// (0,022 × 70), então o raio só faz sentido depois de aplicar a escala do transform.
    /// </summary>
    private float MedirRaio()
    {
        var esfera = GetComponent<SphereCollider>();
        if (esfera != null)
        {
            Vector3 e = transform.lossyScale;
            float maiorEixo = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
            return Mathf.Max(esfera.radius * maiorEixo, 0.05f);
        }

        var rend = GetComponentInChildren<Renderer>();
        if (rend != null)
            return Mathf.Max(rend.bounds.extents.magnitude * 0.6f, 0.05f);

        return 1f;
    }

    private void OnEnable()
    {
        ultimaPosicao = transform.position;
        acumuladorDistancia = 0f;
    }

    private void Update()
    {
        if (puffPrefab == null)
            return;

        Vector3 posicao = transform.position;
        float distancia = Vector3.Distance(ultimaPosicao, posicao);
        ultimaPosicao = posicao;

        float velocidade = corpo.linearVelocity.magnitude;

        // Sem contato com o chão não há atrito, e sem atrito não há poeira: a bola voando (saída do
        // canhão, salto, queda) não deixa rastro nenhum.
        if (velocidade < velocidadeMinima || !TocandoOChao(out Vector3 chao))
        {
            acumuladorDistancia = 0f;
            return;
        }

        acumuladorDistancia += distancia;

        float fator = Mathf.InverseLerp(velocidadeMinima, velocidadeMaxima, velocidade);
        float espacamento = Mathf.Lerp(espacamentoLento, espacamentoRapido, fator) * raioBola;

        int passos = 0;

        while (acumuladorDistancia >= espacamento && passos < maxPassosPorFrame)
        {
            for (int i = 0; i < puffsPorPasso; i++)
                SoltarPuff(chao, fator, 1f);

            acumuladorDistancia -= espacamento;
            passos++;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (puffPrefab == null || Time.time < proximoImpacto)
            return;

        if (collision.relativeVelocity.magnitude < impactoVelocidadeMinima)
            return;

        // A baforada de impacto nasce no ponto de contato — que é, por definição, onde a bola
        // encostou. Vale para batida no chão e em parede.
        ContactPoint contato = collision.GetContact(0);

        proximoImpacto = Time.time + impactoIntervalo;

        for (int i = 0; i < puffsNoImpacto; i++)
            SoltarPuff(contato.point, 1f, impactoEscalaExtra);
    }

    private void SoltarPuff(Vector3 origem, float fatorVelocidade, float multiplicadorEscala)
    {
        Vector3 espalha = Random.insideUnitSphere * (espalhamento * raioBola);
        espalha.y = Mathf.Abs(espalha.y) * 0.35f; // só espalha para cima: nada de puff sob o chão

        Vector3 posicao = origem + espalha;
        Quaternion rotacao = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        DriftPuffBubble puff = DriftPuffPool.Obter(puffPrefab, posicao, rotacao);

        if (puff == null)
            return;

        float ganho = Mathf.Lerp(0.85f, 1.3f, fatorVelocidade) * multiplicadorEscala * raioBola;

        Vector3 velocidade =
            corpo.linearVelocity * arrasto +
            Vector3.up * Random.Range(velocidadeParaCima * 0.4f, velocidadeParaCima) * raioBola +
            Random.insideUnitSphere * (velocidadeLateral * raioBola);

        puff.Initialize(
            Random.Range(vidaMin, vidaMax),
            Random.Range(escalaInicialMin, escalaInicialMax) * ganho,
            Random.Range(escalaFinalMin, escalaFinalMax) * ganho,
            velocidade
        );
    }
}
