using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Bloco superior direito do poder atual: plate, ícone, nome e botão de usar.
    /// Mapeia KartPowerType -> sprite via refs serializadas (trocáveis no Inspector).
    /// </summary>
    public class PowerUpHUDUI : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private Image plate;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text chargeLabel;
        [SerializeField] private Button useButton;
        [SerializeField] private CanvasGroup group;

        [Header("Ícones por poder")]
        [SerializeField] private Sprite rocketIcon;
        [SerializeField] private Sprite shieldIcon;
        [SerializeField] private Sprite swapIcon;
        [SerializeField] private Sprite noPowerIcon;

        [Header("Estados")]
        [SerializeField] private string noPowerName = "SEM PODER";
        [SerializeField] private float emptyAlpha = 0.55f;
        [SerializeField] private Color iconActiveColor = Color.white;
        [SerializeField] private Color iconEmptyColor = new Color(1f, 1f, 1f, 0.6f);

        /// <summary>Disparado quando o jogador pede para usar o poder (botão/RT).</summary>
        public UnityEvent onUsePower = new UnityEvent();

        private void Awake()
        {
            if (useButton != null)
                useButton.onClick.AddListener(() => onUsePower.Invoke());
        }

        public void SetPower(KartPowerType type, string displayName, bool hasPower)
        {
            if (icon != null)
            {
                icon.sprite = ResolveIcon(type, hasPower);
                icon.color = hasPower ? iconActiveColor : iconEmptyColor;
                icon.enabled = icon.sprite != null;
            }

            if (nameLabel != null)
                nameLabel.text = hasPower ? FormatName(displayName) : noPowerName;

            if (chargeLabel != null)
                chargeLabel.text = hasPower ? "1" : string.Empty;

            if (group != null)
                group.alpha = hasPower ? 1f : emptyAlpha;

            if (useButton != null)
                useButton.interactable = hasPower;
        }

        private Sprite ResolveIcon(KartPowerType type, bool hasPower)
        {
            if (!hasPower)
                return noPowerIcon;

            return type switch
            {
                KartPowerType.Rocket => rocketIcon,
                KartPowerType.Shield => shieldIcon,
                KartPowerType.SwapPosition => swapIcon,
                _ => noPowerIcon
            };
        }

        private static string FormatName(string raw)
        {
            return string.IsNullOrEmpty(raw) ? string.Empty : raw.ToUpperInvariant();
        }
    }
}
