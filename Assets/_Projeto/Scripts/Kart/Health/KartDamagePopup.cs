using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// O número de dano/cura sobe AO LADO DO CARRO, no mundo.
///
/// O HUD já diz quanto de vida sobrou; o que ele não diz é quanto ACABOU de sair, e quem está
/// correndo não tira os olhos da pista para descobrir. O número no carro responde no lugar onde o
/// jogador está olhando — é a mesma razão da fumaça de avaria.
///
/// Nasce um pouco à direita e à frente do kart, sobe, cresce ao aparecer e some encolhendo. A
/// entrada é rápida (0,12 s) e a saída é lenta (0,45 s): rápido demais nas duas pontas vira
/// piscada, lento demais na entrada atrasa a resposta.
///
/// Os rótulos são reciclados num pool próprio — um kart pode levar três golpes em sequência, e
/// instanciar TextMeshPro no meio disso custa mais que o próprio dano.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(KartHealth))]
public class KartDamagePopup : MonoBehaviour
{
    [Header("Aparência")]
    [SerializeField] private float corpo = 3.2f;
    [SerializeField] private Color corDeDano = new Color(1f, 0.36f, 0.42f);
    [SerializeField] private Color corDeCura = new Color(0.24f, 0.86f, 0.59f);
    [Tooltip("Contorno escuro. Sem ele o número some sobre o gramado claro da pista.")]
    [SerializeField, Range(0f, 0.5f)] private float contorno = 0.25f;

    [Header("Movimento")]
    [Tooltip("De onde nasce, em relação ao kart: um pouco acima e ao lado, para não tapar o carro.")]
    [SerializeField] private Vector3 nascimento = new Vector3(1.1f, 1.5f, 0f);
    [SerializeField] private float subida = 2.2f;
    [SerializeField] private float entrada = 0.12f;
    [SerializeField] private float vida = 0.9f;
    [SerializeField] private float saida = 0.45f;
    [Tooltip("Balanço lateral do número enquanto sobe. Zero deixa a subida mecânica.")]
    [SerializeField] private float deriva = 0.5f;

    private KartHealth saude;
    private Camera camera3D;
    private readonly List<Rotulo> rotulos = new List<Rotulo>();

    private sealed class Rotulo
    {
        public TextMeshPro Texto;
        public Transform No;
        public float Nasceu;
        public Vector3 Origem;
        public float Fase;
        public bool Livre = true;
    }

    private void Awake() => saude = GetComponent<KartHealth>();

    private void OnEnable()
    {
        saude.Damaged += AoLevarDano;
        saude.Healed += AoCurar;
    }

    private void OnDisable()
    {
        saude.Damaged -= AoLevarDano;
        saude.Healed -= AoCurar;
    }

    private void AoLevarDano(KartHealth _, KartHealth.DamageReport r)
    {
        // Golpe bloqueado pelo escudo não tira vida: mostrar "−0" mentiria sobre o que aconteceu.
        if (r.Amount > 0 && !r.BlockedByShield)
            Mostrar("−" + r.Amount, corDeDano);
    }

    private void AoCurar(KartHealth _, int quanto)
    {
        if (quanto > 0)
            Mostrar("+" + quanto, corDeCura);
    }

    private void Mostrar(string texto, Color cor)
    {
        Rotulo r = Pegar();
        r.Texto.text = texto;
        r.Texto.color = cor;

        // O contorno só existe se a keyword do shader estiver ligada — sem ela, `outlineWidth` é
        // um campo que não pinta nada, e o número some sobre a faixa laranja da pista.
        r.Texto.fontMaterial.EnableKeyword(TMPro.ShaderUtilities.Keyword_Outline);
        r.Texto.outlineColor = new Color32(10, 12, 34, 255);
        r.Texto.outlineWidth = contorno;

        // Lado alternado: dois golpes seguidos no mesmo lugar viram um número só piscando.
        float lado = rotulos.Count % 2 == 0 ? 1f : -1f;
        r.Origem = transform.position + transform.right * (nascimento.x * lado)
                                      + Vector3.up * nascimento.y
                                      + transform.forward * nascimento.z;

        r.No.position = r.Origem;
        r.Fase = Random.Range(0f, Mathf.PI * 2f);
        r.Nasceu = Time.time;
        r.Livre = false;
        r.No.gameObject.SetActive(true);
    }

    private Rotulo Pegar()
    {
        foreach (Rotulo r in rotulos)
            if (r.Livre)
                return r;

        var go = new GameObject("DanoPopup") { hideFlags = HideFlags.DontSave };
        var t = go.AddComponent<TextMeshPro>();
        t.fontSize = corpo;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.enableWordWrapping = false;
        t.sortingOrder = 200;

        var novo = new Rotulo { Texto = t, No = go.transform };
        rotulos.Add(novo);
        return novo;
    }

    private void LateUpdate()
    {
        if (camera3D == null)
            camera3D = Camera.main;

        float total = entrada + vida + saida;

        foreach (Rotulo r in rotulos)
        {
            if (r.Livre)
                continue;

            float t = Time.time - r.Nasceu;

            if (t >= total)
            {
                r.Livre = true;
                r.No.gameObject.SetActive(false);
                continue;
            }

            float k = Mathf.Clamp01(t / total);
            float altura = subida * PartyRacers.UI.Motion.UIEase.OutQuad(k);
            float lateral = Mathf.Sin(r.Fase + k * 3.2f) * deriva * k;

            r.No.position = r.Origem + Vector3.up * altura
                          + (camera3D != null ? camera3D.transform.right : Vector3.right) * lateral;

            // Sempre de frente para a câmera: um número em perspectiva não se lê de relance.
            if (camera3D != null)
                r.No.forward = camera3D.transform.forward;

            // Entra crescendo com um leve exagero e sai encolhendo — o exagero é o que faz o
            // número "chegar" em vez de simplesmente aparecer.
            float escala = t < entrada
                ? PartyRacers.UI.Motion.UIEase.OutBack(t / entrada, 1.7f)
                : t > entrada + vida
                    ? 1f - PartyRacers.UI.Motion.UIEase.OutQuad((t - entrada - vida) / saida)
                    : 1f;

            r.No.localScale = Vector3.one * Mathf.Max(0.01f, escala);

            float alfa = t > entrada + vida
                ? 1f - Mathf.Clamp01((t - entrada - vida) / saida)
                : 1f;

            Color c = r.Texto.color;
            r.Texto.color = new Color(c.r, c.g, c.b, alfa);
        }
    }

    private void OnDestroy()
    {
        foreach (Rotulo r in rotulos)
            if (r.No != null)
                Destroy(r.No.gameObject);
    }
}
