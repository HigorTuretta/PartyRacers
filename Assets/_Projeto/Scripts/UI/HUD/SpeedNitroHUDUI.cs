using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Canto inferior direito: velocidade (KM/H) + barra de nitro. Placeholder funcional —
    /// a lógica vem dos dados reais; o visual é trocável só pelas refs do prefab.
    /// </summary>
    public class SpeedNitroHUDUI : MonoBehaviour
    {
        public enum NitroState { Empty, Charging, Full, Boosting }

        [Header("Velocidade")]
        [SerializeField] private TMP_Text speedLabel;
        [SerializeField] private TMP_Text unitLabel;
        [SerializeField] private string unitText = "KM/H";

        [Header("Nitro")]
        [SerializeField] private Image nitroFill;
        [SerializeField] private Image nitroBackground;
        [SerializeField] private CanvasGroup nitroReadyGlow;

        [Header("Cores do nitro por estado")]
        [SerializeField] private Color emptyColor = new Color(0.30f, 0.40f, 0.50f, 1f);
        [SerializeField] private Color chargingColor = new Color(0.20f, 0.60f, 1f, 1f);
        [SerializeField] private Color fullColor = new Color(0.30f, 1f, 0.65f, 1f);
        [SerializeField] private Color boostingColor = new Color(1f, 0.65f, 0.15f, 1f);

        public void SetSpeed(float speedKmh)
        {
            if (speedLabel != null)
                speedLabel.text = Mathf.RoundToInt(Mathf.Max(0f, speedKmh)).ToString();

            if (unitLabel != null)
                unitLabel.text = unitText;
        }

        public void SetNitro(float fill01, bool isBoosting)
        {
            float fill = Mathf.Clamp01(fill01);
            NitroState state = ResolveState(fill, isBoosting);

            if (nitroFill != null)
            {
                nitroFill.fillAmount = fill;
                nitroFill.color = ColorForState(state);
            }

            if (nitroReadyGlow != null)
                nitroReadyGlow.alpha = state == NitroState.Full || state == NitroState.Boosting ? 1f : 0f;
        }

        private static NitroState ResolveState(float fill, bool isBoosting)
        {
            if (isBoosting)
                return NitroState.Boosting;
            if (fill <= 0.001f)
                return NitroState.Empty;
            if (fill >= 0.999f)
                return NitroState.Full;
            return NitroState.Charging;
        }

        private Color ColorForState(NitroState state) => state switch
        {
            NitroState.Empty => emptyColor,
            NitroState.Charging => chargingColor,
            NitroState.Full => fullColor,
            NitroState.Boosting => boostingColor,
            _ => chargingColor
        };
    }
}
