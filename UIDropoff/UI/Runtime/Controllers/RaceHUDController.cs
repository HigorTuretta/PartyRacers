using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// HUD de corrida. Regras que o HUD OBEDECE (tokens-v2 -> hud):
    /// sem mira, sem velocimetro, sem minimapa, sem alerta central,
    /// sem HP dos outros jogadores. O centro da tela fica livre.
    ///
    /// O escudo nao tem icone nem botao no PC: a disponibilidade e sinalizada
    /// pela PROPRIA barra (brilho pulsante 1.8s + varredura de luz 2.4s).
    /// Em recarga: sem brilho, sem varredura.
    /// </summary>
    public sealed class RaceHUDController : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset toastTemplate;

        const int HpChunks = 5;
        const int ShieldChunks = 3;

        VisualElement _root;

        public enum ShieldState { Ready, Active, Cooling, Broken }

        void OnEnable()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
        }

        public void SetShield(ShieldState s)
        {
            UiStates.Show(_root.Q<VisualElement>("Shield_Ready"),   s == ShieldState.Ready);
            UiStates.Show(_root.Q<VisualElement>("Shield_Active"),  s == ShieldState.Active);
            UiStates.Show(_root.Q<VisualElement>("Shield_Cooling"), s == ShieldState.Cooling);
            UiStates.Show(_root.Q<VisualElement>("Shield_Broken"),  s == ShieldState.Broken);
        }

        /// <summary>HP em 5 blocos: cheio, ferido (o bloco em queda), perdido.</summary>
        public void SetHp(int hp)
        {
            var bar = _root.Q<VisualElement>("HP_Bar");
            _root.Q<Label>("HP_Value").text = hp.ToString();

            int full = hp * HpChunks / 100;
            for (int i = 0; i < HpChunks; i++)
            {
                var segEl = bar[i];
                UiStates.SetVariant(segEl,
                    i < full ? "pr-bar__seg--hp" : i == full ? "pr-bar__seg--hurt" : "pr-bar__seg--gone",
                    "pr-bar__seg--hp", "pr-bar__seg--hurt", "pr-bar__seg--gone");
            }
        }

        public void SetBroken(bool broken)
        {
            UiStates.Show(_root.Q<VisualElement>("HP_Row"),     !broken);
            UiStates.Show(_root.Q<VisualElement>("Repair_Row"),  broken);
        }

        public void SetPower(string powerId)
        {
            bool has = !string.IsNullOrEmpty(powerId);
            UiStates.Show(_root.Q<VisualElement>("Power_Filled"), has);
            UiStates.Show(_root.Q<VisualElement>("Power_Key"),    has);
            UiStates.Show(_root.Q<VisualElement>("Power_Empty"),           !has);
            UiStates.Show(_root.Q<VisualElement>("Power_Empty_Label"),     !has);
            // O sprite do poder e a UNICA troca de imagem permitida em codigo:
            // Powers/Power_<id>_Color.png em Power_Icon.
        }
    }
}
