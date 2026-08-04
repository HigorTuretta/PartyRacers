using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Avisos discretos do canto inferior esquerdo (handoff §4): todo evento vira um Toast_Item.
    /// Máximo 3 ao mesmo tempo, 2,5 s de vida, os mais antigos com opacidade menor.
    /// Nada de alerta no centro da tela.
    ///
    /// Os 3 slots já estão montados no prefab da tela — este binder só preenche e controla o
    /// CanvasGroup deles; não instancia nem posiciona nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class ToastNotificationUI : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public GameObject raiz;
            public CanvasGroup grupo;
            public TextMeshProUGUI texto;
            public Image icone;
            [Tooltip("Contorno do card, quando ele é um objeto IRMÃO em vez de fazer parte do " +
                     "sprite. Irmão não acompanha o SetActive da raiz — sem ligá-lo junto, sobra " +
                     "a moldura de um aviso que não existe.")]
            public GameObject moldura;
        }

        [Header("Slots já montados (do mais recente ao mais antigo)")]
        [SerializeField] private List<Slot> slots = new List<Slot>();

        [Header("Ritmo")]
        [SerializeField] private float duracao = 2.5f;
        [SerializeField] private float duracaoFade = 0.25f;

        [Header("Ícones por tipo de evento (atribuídos no Inspector)")]
        [SerializeField] private Sprite iconeAcerto;
        [SerializeField] private Sprite iconeEscudo;
        [SerializeField] private Sprite iconeTroca;
        [SerializeField] private Sprite iconePoder;
        [SerializeField] private Sprite iconeTurbo;
        [Tooltip("Cruz de cura. Forma diferente do escudo e da barra de vida de propósito.")]
        [SerializeField] private Sprite iconeCura;
        [SerializeField] private Sprite iconeDanificado;

        [Header("Dados")]
        [Tooltip("Usado só para saber qual kart é o do jogador e filtrar os avisos dos outros.")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Cores por tipo (da paleta PLACA)")]
        [SerializeField] private Color corAcerto = new Color(0.24f, 0.86f, 0.59f);
        [SerializeField] private Color corDefesa = new Color(0.21f, 0.65f, 1f);
        [SerializeField] private Color corNeutra = new Color(0.55f, 0.48f, 1f);
        [SerializeField] private Color corCura = new Color(0.24f, 0.86f, 0.59f);
        [SerializeField] private Color corDano = new Color(1f, 0.30f, 0.43f);

        private readonly List<Aviso> fila = new List<Aviso>();

        private struct Aviso
        {
            public string texto;
            public Sprite icone;
            public Color cor;
            public float nasceu;
        }

        private void OnEnable() => RaceHudEvents.Raised += AoEvento;
        private void OnDisable() => RaceHudEvents.Raised -= AoEvento;

        private void AoEvento(RaceHudEvents.EventData e)
        {
            // Os eventos são globais: com 16 karts, todo poder que um bot pegava virava aviso na
            // tela do jogador. Só interessa o que aconteceu COM ELE — como ator (pegou, usou,
            // acertou) ou como alvo (foi atingido).
            if (!EhDoJogadorLocal(e))
                return;

            switch (e.Kind)
            {
                case RaceHudEventKind.HitOpponent:
                    Enfileirar("Acertou " + NomeDe(e.Target), iconeAcerto, corAcerto);
                    break;
                case RaceHudEventKind.GotHit:
                    Enfileirar(e.PowerType == KartPowerType.Shield
                                   ? "Escudo bloqueou"
                                   : e.PowerType == KartPowerType.ElectricTrap
                                       ? "Choque elétrico!"
                                       : "Você foi atingido",
                               iconeEscudo, corDefesa);
                    break;
                case RaceHudEventKind.PowerCollected:
                    Enfileirar("Poder coletado", iconePoder, corNeutra);
                    break;
                case RaceHudEventKind.PowerUsed:
                    Enfileirar(e.PowerType == KartPowerType.SwapPosition
                                   ? "Trocou de lugar"
                                   : e.PowerType == KartPowerType.ElectricTrap
                                       ? "Armadilha lançada"
                                       : "Poder usado",
                               iconeTroca, corNeutra);
                    break;
                case RaceHudEventKind.Nitro:
                    Enfileirar("Turbo!", iconeTurbo, corAcerto);
                    break;

                // Dano NÃO vira toast: o número flutuante e o arco vermelho já contam a história,
                // e uma terceira mensagem no canto competiria com eles a cada raspão de parede.
                case RaceHudEventKind.Healed:
                    Enfileirar($"+{Mathf.RoundToInt(e.Amount)} de vida", iconeCura, corCura);
                    break;
                case RaceHudEventKind.Broken:
                    Enfileirar("Carro danificado!", iconeDanificado, corDano);
                    break;
                case RaceHudEventKind.Repaired:
                    Enfileirar("Carro reparado", iconeCura, corCura);
                    break;
                case RaceHudEventKind.ShieldReady:
                    Enfileirar("Escudo pronto", iconeEscudo, corDefesa);
                    break;
                case RaceHudEventKind.ShieldBlocked:
                    Enfileirar("Escudo bloqueou", iconeEscudo, corDefesa);
                    break;
            }
        }

        /// <summary>
        /// O evento diz respeito ao jogador local? GotHit olha o alvo; os demais, o ator.
        /// Sem provedor de dados não há como saber quem é o local — nesse caso deixa passar,
        /// para não emudecer a HUD por configuração faltando.
        /// </summary>
        private bool EhDoJogadorLocal(RaceHudEvents.EventData e)
        {
            if (dados == null || !dados.HasLocalKart)
                return true;

            GameObject local = dados.LocalKart.gameObject;
            GameObject quemImporta = e.Kind == RaceHudEventKind.GotHit ? e.Target : e.Actor;
            if (quemImporta == null)
                return false;

            // o evento pode vir de um filho do kart (colisor, visual), então compara a hierarquia
            return quemImporta == local || quemImporta.transform.IsChildOf(local.transform);
        }

        /// <summary>Empilha um aviso. Passando de 3, o mais antigo cai.</summary>
        public void Enfileirar(string texto, Sprite icone, Color cor)
        {
            fila.Insert(0, new Aviso { texto = texto, icone = icone, cor = cor, nasceu = Time.time });
            while (fila.Count > slots.Count)
                fila.RemoveAt(fila.Count - 1);
            Redesenhar();
        }

        private void Update()
        {
            bool mudou = false;
            for (int i = fila.Count - 1; i >= 0; i--)
            {
                if (Time.time - fila[i].nasceu < duracao)
                    continue;
                fila.RemoveAt(i);
                mudou = true;
            }

            if (mudou)
                Redesenhar();
            else
                AtualizarOpacidade();
        }

        private void Redesenhar()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                Slot slot = slots[i];
                if (slot == null || slot.raiz == null)
                    continue;

                bool usado = i < fila.Count;
                if (slot.raiz.activeSelf != usado)
                    slot.raiz.SetActive(usado);
                if (slot.moldura != null && slot.moldura.activeSelf != usado)
                    slot.moldura.SetActive(usado);
                if (!usado)
                    continue;

                Aviso a = fila[i];
                if (slot.texto != null) slot.texto.text = a.texto;
                if (slot.icone != null && a.icone != null) { slot.icone.sprite = a.icone; slot.icone.color = a.cor; }
            }

            AtualizarOpacidade();
        }

        private void AtualizarOpacidade()
        {
            for (int i = 0; i < slots.Count && i < fila.Count; i++)
            {
                Slot slot = slots[i];
                if (slot == null || slot.grupo == null)
                    continue;

                // mais novo = mais opaco; e some no fim da vida
                float baseAlfa = i == 0 ? 1f : (i == 1 ? 0.82f : 0.55f);
                float restante = duracao - (Time.time - fila[i].nasceu);
                float fade = Mathf.Clamp01(restante / Mathf.Max(0.01f, duracaoFade));
                slot.grupo.alpha = baseAlfa * fade;
            }
        }

        private static string NomeDe(GameObject alvo)
        {
            if (alvo == null)
                return "adversário";

            var identidade = alvo.GetComponentInParent<PartyRacers.Networking.KartNetworkIdentity>();
            return identidade != null && !string.IsNullOrWhiteSpace(identidade.DisplayName)
                ? identidade.DisplayName
                : "adversário";
        }
    }
}
