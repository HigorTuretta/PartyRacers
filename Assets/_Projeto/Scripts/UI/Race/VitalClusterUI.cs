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

        [Header("Escudo — linhas de estado (irmãs, mutuamente exclusivas)")]
        [SerializeField] private GameObject raizEscudo;
        [SerializeField] private GameObject estadoPronto;
        [SerializeField] private GameObject estadoAtivo;
        [SerializeField] private GameObject estadoRecarga;
        [Tooltip("Escudo apagado durante o estado danificado.")]
        [SerializeField] private GameObject estadoApagado;

        [Header("Escudo — peças que o binder escreve")]
        [Tooltip("Preenchimento proporcional da recarga (Image type Filled, Horizontal).")]
        [SerializeField] private Image preenchimentoRecarga;
        [Tooltip("Risquinho na ponta do preenchimento. Anda junto com o fillAmount.")]
        [SerializeField] private RectTransform pontaDaRecarga;
        [SerializeField] private TextMeshProUGUI textoDaChapinhaAtivo;
        [SerializeField] private TextMeshProUGUI textoDaChapinhaRecarga;

        [Header("Imunidade (cooldown de contato)")]
        [SerializeField] private GameObject raizImunidade;
        [Tooltip("Barra âmbar que drena em 0,75 s (Image type Filled, Horizontal).")]
        [SerializeField] private Image barraDeImunidade;

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

        private void AtualizarEscudo(bool danificado)
        {
            Ligar(raizEscudo, true);

            bool ativo = !danificado && dados.ShieldActive;
            bool pronto = !danificado && dados.ShieldReady;
            bool recarregando = !danificado && !ativo && !pronto;

            Ligar(estadoPronto, pronto);
            Ligar(estadoAtivo, ativo);
            Ligar(estadoRecarga, recarregando);
            Ligar(estadoApagado, danificado);

            if (ativo && textoDaChapinhaAtivo != null)
                textoDaChapinhaAtivo.text = $"ATIVO {dados.ShieldActiveRemaining:0.0}s";

            if (!recarregando)
                return;

            float p = dados.ShieldCooldown01;

            if (preenchimentoRecarga != null)
                preenchimentoRecarga.fillAmount = p;

            // O risquinho marca a PONTA do preenchimento — parado no zero ele viraria só um traço
            // decorativo, e é justamente ele que mostra que a recarga está andando.
            if (pontaDaRecarga != null && preenchimentoRecarga != null)
            {
                float largura = preenchimentoRecarga.rectTransform.rect.width;
                pontaDaRecarga.anchoredPosition = new Vector2(largura * p, pontaDaRecarga.anchoredPosition.y);
            }

            if (textoDaChapinhaRecarga != null)
                textoDaChapinhaRecarga.text = $"{dados.ShieldCooldownRemaining:0.0}s";
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
