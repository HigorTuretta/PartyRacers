using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder do CLUSTER VITAL do canto inferior esquerdo: escudo em cima, vida embaixo, régua de
    /// imunidade e barra de reparo.
    ///
    /// Regras de design que este binder existe para cumprir:
    /// • O escudo NÃO tem botão nem ícone. A própria barra é o indicador: com brilho e varredura
    ///   está disponível, sem eles está recarregando. Por isso "em recarga" não é uma cor nova —
    ///   é a AUSÊNCIA do movimento, que a visão periférica percebe a 150 km/h.
    /// • Os quatro estados do escudo são LINHAS INTEIRAS irmãs, já estilizadas: o protótipo troca
    ///   até a cor do rótulo entre eles, e recolorir por código seria o binder decidindo estilo.
    /// • O estado danificado SUBSTITUI a vida pela barra de reparo; o escudo não some, apaga.
    /// • Segmento meio drenado é `fillAmount`, nunca redimensionamento do RectTransform.
    /// </summary>
    [DisallowMultipleComponent]
    public class VitalClusterUI : MonoBehaviour
    {
        /// <summary>Um bloco da barra de vida: os três filhos já existem na cena.</summary>
        [System.Serializable]
        public class SegmentoDeVida
        {
            [Tooltip("Preenchimento verde (Image type Filled, Horizontal).")]
            public Image cheio;
            [Tooltip("Preenchimento âmbar de vida baixa (Image type Filled, Horizontal).")]
            public Image ferido;
            [Tooltip("Trilho apagado que fica sempre ligado, por baixo.")]
            public GameObject vazio;
        }

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Vida")]
        [SerializeField] private GameObject raizVida;
        [Tooltip("Os 5 blocos de 20 HP, da esquerda para a direita.")]
        [SerializeField] private SegmentoDeVida[] segmentosDeVida = new SegmentoDeVida[5];
        [SerializeField] private TextMeshProUGUI valorDeVida;
        [Tooltip("Abaixo desta fração os blocos trocam para âmbar — é o aviso de que o próximo " +
                 "golpe pode quebrar o carro.")]
        [SerializeField, Range(0f, 1f)] private float limiarDeVidaBaixa = 0.4f;

        [Header("Escudo — uma barra, três estados")]
        [SerializeField] private GameObject raizEscudo;
        [Tooltip("Preenchimento da barra (Image type Filled, Horizontal).")]
        [SerializeField] private Image preenchimentoEscudo;
        [Tooltip("Risquinho na ponta do preenchimento. Anda junto com o fillAmount.")]
        [SerializeField] private RectTransform pontaDoEscudo;
        [Tooltip("Chapinha à direita da barra: PRONTO / ATIVO 2,3s / 3,4s.")]
        [SerializeField] private TextMeshProUGUI textoDoEscudo;
        [Tooltip("Fundo da chapinha, recolorido junto com a barra.")]
        [SerializeField] private Graphic fundoDaChapinha;

        [Header("Escudo — cores por estado")]
        [SerializeField] private Color corPronto = new Color(0.21f, 0.65f, 1f);
        [SerializeField] private Color corAtivo = new Color(0.62f, 0.92f, 1f);
        [SerializeField] private Color corRecarga = new Color(0.29f, 0.33f, 0.66f);

        [Header("Imunidade (cooldown de contato)")]
        [SerializeField] private GameObject raizImunidade;
        [Tooltip("Barra âmbar que drena em 0,75 s (Image type Filled, Horizontal).")]
        [SerializeField] private Image barraDeImunidade;

        [Header("Movimento")]
        [Tooltip("Empurrão + pisca no bloco de vida quando o carro leva dano. A barra descendo " +
                 "sozinha não chama atenção: a 150 km/h o olho está na pista, não no canto.")]
        [SerializeField] private PartyRacers.UI.Motion.UIKick chuteDeDano;
        [Tooltip("Empurrão quando o escudo fica pronto de novo.")]
        [SerializeField] private PartyRacers.UI.Motion.UIKick chuteDoEscudo;

        private int hpAnterior = -1;
        private bool escudoProntoAntes;

        [Header("Estado danificado")]
        [SerializeField] private GameObject raizReparo;
        [Tooltip("Listras diagonais que drenam em 2,5 s (Image type Filled, Horizontal).")]
        [SerializeField] private Image preenchimentoDoReparo;
        [SerializeField] private TextMeshProUGUI contagemDoReparo;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        // A HUD é um PREFAB e o provedor vive na CENA — prefab não serializa essa referência.
        private void Awake()
        {
            if (dados == null)
                dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();

            bool danificado = dados.IsBroken;

            Ligar(raizVida, !danificado);
            Ligar(raizReparo, danificado);

            AtualizarEscudo(danificado);

            if (danificado)
            {
                AtualizarReparo();
                Ligar(raizImunidade, false);
                return;
            }

            AtualizarVida();
            AtualizarImunidade();
        }

        // ---------------------------------------------------------------- Vida

        private void AtualizarVida()
        {
            int max = Mathf.Max(1, dados.MaxHp);
            int hp = Mathf.Clamp(dados.Hp, 0, max);
            bool vidaBaixa = dados.Hp01 <= limiarDeVidaBaixa;

            if (valorDeVida != null)
                valorDeVida.text = hp.ToString();

            if (hp < hpAnterior && hpAnterior >= 0)
                chuteDeDano?.Chutar();

            hpAnterior = hp;

            if (segmentosDeVida == null || segmentosDeVida.Length == 0)
                return;

            float porSegmento = max / (float)segmentosDeVida.Length;

            for (int i = 0; i < segmentosDeVida.Length; i++)
            {
                SegmentoDeVida seg = segmentosDeVida[i];
                if (seg == null)
                    continue;

                float preenchido = Mathf.Clamp01((hp - i * porSegmento) / porSegmento);

                if (seg.cheio != null)
                {
                    Ligar(seg.cheio.gameObject, !vidaBaixa && preenchido > 0f);
                    seg.cheio.fillAmount = preenchido;
                }

                if (seg.ferido != null)
                {
                    Ligar(seg.ferido.gameObject, vidaBaixa && preenchido > 0f);
                    seg.ferido.fillAmount = preenchido;
                }
            }
        }

        // ---------------------------------------------------------------- Escudo

        /// <summary>
        /// Uma barra, três estados — porque o jogo tem UM escudo, não três cargas.
        ///
        /// O protótipo desenhou três segmentos e o binder antigo tentou tratá-los como quatro
        /// linhas de estado inteiras; o resultado foi uma barra que nunca se mexia, porque nada
        /// disso existe no <see cref="KartShieldAbility"/>. O que existe é: pronto, ativo (drenando
        /// pelo tempo de duração) e recarregando (enchendo pelo cooldown). A barra mostra os três
        /// pela mesma régua, e a cor diz qual deles é.
        ///
        /// Ativo DRENA e recarga ENCHE de propósito: as duas coisas andam, mas em sentidos opostos,
        /// e é o sentido que distingue "está acabando" de "está voltando" sem precisar ler nada.
        /// </summary>
        private void AtualizarEscudo(bool danificado)
        {
            Ligar(raizEscudo, true);

            bool ativo = !danificado && dados.ShieldActive;
            bool pronto = !danificado && dados.ShieldReady;

            float preenchimento = danificado ? 0f
                                : ativo ? dados.ShieldActive01
                                : pronto ? 1f
                                : dados.ShieldCooldown01;

            Color cor = danificado ? corRecarga
                      : ativo ? corAtivo
                      : pronto ? corPronto
                      : corRecarga;

            string rotulo = danificado ? "—"
                          : ativo ? $"ATIVO {dados.ShieldActiveRemaining:0.0}s"
                          : pronto ? "PRONTO"
                          : $"{dados.ShieldCooldownRemaining:0.0}s";

            if (preenchimentoEscudo != null)
            {
                preenchimentoEscudo.fillAmount = preenchimento;
                preenchimentoEscudo.color = cor;
            }

            // O risquinho marca a PONTA do preenchimento. Parado no zero ele viraria só um traço
            // decorativo, e é justamente ele que mostra que a barra está andando. Some quando não
            // há movimento nenhum a marcar.
            if (pontaDoEscudo != null)
            {
                bool mostrar = preenchimento > 0.01f && preenchimento < 0.995f;
                Ligar(pontaDoEscudo.gameObject, mostrar);

                if (mostrar && preenchimentoEscudo != null)
                {
                    float largura = preenchimentoEscudo.rectTransform.rect.width;
                    pontaDoEscudo.anchoredPosition =
                        new Vector2(largura * preenchimento, pontaDoEscudo.anchoredPosition.y);
                }
            }

            if (textoDoEscudo != null && textoDoEscudo.text != rotulo)
                textoDoEscudo.text = rotulo;

            if (fundoDaChapinha != null)
                fundoDaChapinha.color = new Color(cor.r, cor.g, cor.b, pronto || ativo ? 0.9f : 0.35f);

            // "Voltou a ficar pronto" é a única transição do escudo que o jogador precisa notar
            // sem estar olhando: é ela que autoriza a próxima disputa de espaço.
            if (pronto && !escudoProntoAntes)
                chuteDoEscudo?.Chutar();

            escudoProntoAntes = pronto;
        }

        // ---------------------------------------------------------------- Imunidade e reparo

        private void AtualizarImunidade()
        {
            bool imune = dados.OnDamageCooldown;
            Ligar(raizImunidade, imune);

            if (imune && barraDeImunidade != null)
                barraDeImunidade.fillAmount = dados.DamageCooldown01;
        }

        private void AtualizarReparo()
        {
            if (preenchimentoDoReparo != null)
                preenchimentoDoReparo.fillAmount = dados.BrokenRemaining01;

            if (contagemDoReparo != null)
                contagemDoReparo.text = $"{dados.BrokenRemaining:0.0}s";
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
