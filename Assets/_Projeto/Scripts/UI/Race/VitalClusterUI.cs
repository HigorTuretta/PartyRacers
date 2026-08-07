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
    /// • As barras preenchem pela LARGURA do rect. `Image.fillAmount` é ignorado quando o
    ///   Image não tem sprite — e as barras da HUD são retângulos lisos, sem sprite nenhum.
    /// </summary>
    [DisallowMultipleComponent]
    public class VitalClusterUI : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Vida — uma barra sólida")]
        [SerializeField] private GameObject raizVida;
        [Tooltip("Preenchimento da vida. Ancorado à esquerda; a largura é o valor.")]
        [SerializeField] private Graphic preenchimentoVida;
        [Tooltip("Rastro atrás do preenchimento: fica onde a vida ESTAVA e alcança devagar. É ele " +
                 "que mostra QUANTO se perdeu — a barra nova sozinha só diz quanto sobrou.")]
        [SerializeField] private Graphic rastroDeDano;
        [Tooltip("Velocidade com que o rastro alcança a vida atual, em frações por segundo.")]
        [SerializeField] private float velocidadeDoRastro = 0.9f;
        [Tooltip("Segundos que o rastro espera antes de começar a andar. A pausa é o que torna a " +
                 "perda legível: sem ela o rastro e a barra descem juntos e não há o que comparar.")]
        [SerializeField] private float esperaDoRastro = 0.35f;
        [SerializeField] private TextMeshProUGUI valorDeVida;
        [Tooltip("Abaixo desta fração a barra troca para âmbar — é o aviso de que o próximo golpe " +
                 "pode quebrar o carro.")]
        [SerializeField, Range(0f, 1f)] private float limiarDeVidaBaixa = 0.4f;
        [SerializeField] private Color corDeVida = new Color(0.24f, 0.86f, 0.59f);
        [SerializeField] private Color corDeVidaBaixa = new Color(1f, 0.69f, 0.13f);
        [Tooltip("Vida piscando enquanto o carro se repara.")]
        [SerializeField] private Color corDeReparo = new Color(0.42f, 0.72f, 1f);

        private float rastro = 1f;
        private float esperaRestante;

        [Header("Escudo — uma barra, três estados")]
        [SerializeField] private GameObject raizEscudo;
        [Tooltip("Preenchimento da barra. Ancorado à esquerda; a largura é o valor.")]
        [SerializeField] private Graphic preenchimentoEscudo;
        [Tooltip("Risquinho na ponta do preenchimento. Anda com a borda da barra.")]
        [SerializeField] private RectTransform pontaDoEscudo;
        [Tooltip("Chapinha à direita da barra: PRONTO / ATIVO 2,3s / 3,4s.")]
        [SerializeField] private TextMeshProUGUI textoDoEscudo;
        [Tooltip("Fundo da chapinha, recolorido junto com a barra.")]
        [SerializeField] private Graphic fundoDaChapinha;

        [Header("Escudo — cores por estado")]
        [SerializeField] private Color corPronto = new Color(0.21f, 0.65f, 1f);
        [SerializeField] private Color corAtivo = new Color(0.62f, 0.92f, 1f);
        [SerializeField] private Color corRecarga = new Color(0.29f, 0.33f, 0.66f);

        [Header("Escudo — animação (§ do protótipo: prGlow 1,8 s e prShine 2,4 s / 1 s)")]
        [Tooltip("Faixa de luz que varre a barra. Ligada quando pronto (2,4 s) e ativo (1 s); " +
                 "desligada em recarga — é a AUSÊNCIA dela que diz indisponível.")]
        [SerializeField] private PartyRacers.UI.Motion.UIShineSweep varredura;
        [Tooltip("Halo pulsante por trás da barra, aceso quando o escudo está pronto.")]
        [SerializeField] private PartyRacers.UI.Motion.UIPulse halo;

        [Header("Imunidade (cooldown de contato)")]
        [SerializeField] private GameObject raizImunidade;
        [Tooltip("Barra âmbar que drena em 0,75 s.")]
        [SerializeField] private Graphic barraDeImunidade;

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
        [Tooltip("Barra que ENCHE enquanto o carro se conserta.")]
        [SerializeField] private Graphic preenchimentoDoReparo;
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

            // A barra de vida NÃO some quando o carro quebra: ela vira a barra de reparo, enchendo
            // no lugar. Trocar por outro widget faria a informação mudar de lugar justamente no
            // momento em que o jogador mais procura por ela.
            Ligar(raizVida, true);

            // `raizReparo` era o bloco separado de conserto do documento. Como a própria barra de
            // vida virou a barra de reparo, ligá-lo deixava uma faixa pálida sobrando ao lado.
            Ligar(raizReparo, false);

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

        /// <summary>
        /// Barra sólida com RASTRO. A vida vai para o valor novo na hora; o rastro fica para trás,
        /// espera um instante e alcança — a faixa entre os dois É o dano que acabou de acontecer.
        ///
        /// Cinco blocos contáveis eram a ideia do documento, e ela funciona parada. Em movimento
        /// não: um bloco caindo de 100% para 70% se confunde com o bloco do lado, e o jogador não
        /// consegue dizer se levou 6 ou 30 de dano. Uma barra só, com rastro, responde isso sem
        /// contar nada.
        /// </summary>
        private void AtualizarVida()
        {
            int max = Mathf.Max(1, dados.MaxHp);
            int hp = Mathf.Clamp(dados.Hp, 0, max);
            float alvo = hp / (float)max;
            bool vidaBaixa = alvo <= limiarDeVidaBaixa;

            if (valorDeVida != null)
                valorDeVida.text = hp.ToString();

            if (hp < hpAnterior && hpAnterior >= 0)
            {
                chuteDeDano?.Chutar();
                esperaRestante = esperaDoRastro;
            }

            if (hp > hpAnterior && hpAnterior >= 0)
                rastro = alvo;   // curou: o rastro não faz sentido subindo

            hpAnterior = hp;

            if (preenchimentoVida != null)
            {
                Encher(preenchimentoVida, alvo);
                preenchimentoVida.color = vidaBaixa ? corDeVidaBaixa : corDeVida;
            }

            AndarRastro(alvo);
        }

        private void AndarRastro(float alvo)
        {
            if (rastro < alvo)
                rastro = alvo;

            if (esperaRestante > 0f)
                esperaRestante -= Time.deltaTime;
            else
                rastro = Mathf.MoveTowards(rastro, alvo, velocidadeDoRastro * Time.deltaTime);

            if (rastroDeDano == null)
                return;

            bool mostrar = rastro > alvo + 0.002f;
            Ligar(rastroDeDano.gameObject, mostrar);

            if (mostrar)
                Encher(rastroDeDano, rastro);
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
                Encher(preenchimentoEscudo, preenchimento);
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
                    // A ponta anda com a BORDA da barra, que agora é a largura dela.
                    var b = (RectTransform)preenchimentoEscudo.transform;
                    pontaDoEscudo.anchoredPosition =
                        new Vector2(b.anchoredPosition.x + b.sizeDelta.x, pontaDoEscudo.anchoredPosition.y);
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

            // Pronto varre devagar, ativo varre rápido, recarga não varre. O halo pulsa só no
            // pronto: aceso durante a recarga ele diria "disponível" enquanto não está.
            if (varredura != null)
            {
                Ligar(varredura.gameObject, pronto || ativo);
                if (pronto || ativo)
                    varredura.DefinirPeriodo(ativo ? 1f : 2.4f);
            }

            if (halo != null)
                Ligar(halo.gameObject, pronto || ativo);
        }

        // ---------------------------------------------------------------- Imunidade e reparo

        private void AtualizarImunidade()
        {
            bool imune = dados.OnDamageCooldown;
            Ligar(raizImunidade, imune);

            if (imune && barraDeImunidade != null)
                Encher(barraDeImunidade, dados.DamageCooldown01);
        }

        /// <summary>
        /// Carro quebrado: a barra de vida vira barra de REPARO e enche de volta.
        ///
        /// `BrokenRemaining01` conta quanto FALTA, então a barra que enche é o complemento — e é a
        /// direção certa: o jogador está esperando a vida voltar, não vendo um prazo acabar. A cor
        /// pisca para separar "consertando" de "correndo com pouca vida", que na barra sozinha
        /// seriam a mesma imagem.
        /// </summary>
        private void AtualizarReparo()
        {
            float progresso = 1f - Mathf.Clamp01(dados.BrokenRemaining01);

            if (preenchimentoDoReparo != null)
                Encher(preenchimentoDoReparo, progresso);

            if (preenchimentoVida != null)
            {
                Encher(preenchimentoVida, progresso);

                float pisca = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                preenchimentoVida.color = Color.Lerp(corDeReparo * 0.55f, corDeReparo, pisca);
            }

            if (rastroDeDano != null)
                Ligar(rastroDeDano.gameObject, false);

            rastro = progresso;
            esperaRestante = 0f;
            hpAnterior = -1;

            if (valorDeVida != null)
                valorDeVida.text = $"{dados.BrokenRemaining:0.0}";

            if (contagemDoReparo != null)
                contagemDoReparo.text = $"{dados.BrokenRemaining:0.0}s";
        }

        /// <summary>
        /// Preenche uma barra pela LARGURA do rect.
        ///
        /// `Image.fillAmount` só funciona com sprite: sem ele o Unity desenha o quad inteiro e a
        /// barra fica cheia com qualquer valor. Como as barras da HUD são retângulos lisos, medir a
        /// largura é a forma que não depende de asset nenhum — e é exata.
        /// </summary>
        private static void Encher(Graphic barra, float fracao)
        {
            if (barra == null)
                return;

            var r = (RectTransform)barra.transform;
            var pai = r.parent as RectTransform;
            if (pai == null)
                return;

            float margem = r.anchoredPosition.x;
            float util = Mathf.Max(0f, pai.rect.width - margem * 2f);
            float largura = util * Mathf.Clamp01(fracao);

            if (!Mathf.Approximately(r.sizeDelta.x, largura))
                r.sizeDelta = new Vector2(largura, r.sizeDelta.y);
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
