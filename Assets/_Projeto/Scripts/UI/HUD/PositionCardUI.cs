using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Card individual de uma posição no leaderboard. Estrutura preparada para customização
    /// cosmética futura: background, plate e borda são refs separadas que podem ser trocadas.
    /// </summary>
    public class PositionCardUI : MonoBehaviour
    {
        [Header("Visual (trocável p/ cosméticos)")]
        [SerializeField] private Image background;
        [SerializeField] private Image plate;
        [SerializeField] private Image highlightBorder;

        [Header("Textos")]
        [SerializeField] private TMP_Text positionLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text bestLapLabel;

        [Header("Cores")]
        [SerializeField] private Color normalBackground = new Color(0.07f, 0.12f, 0.17f, 0.86f);
        [SerializeField] private Color localBackground = new Color(0.12f, 0.30f, 0.46f, 0.94f);
        [SerializeField]
        private Color[] positionColors =
        {
            new Color(1f, 0.82f, 0.10f, 1f),
            new Color(0.78f, 0.84f, 0.90f, 1f),
            new Color(0.85f, 0.52f, 0.24f, 1f),
            new Color(0.62f, 0.70f, 0.80f, 1f),
            new Color(0.62f, 0.70f, 0.80f, 1f)
        };

        public void SetData(int position, string playerName, float bestLapSeconds, bool isLocal)
        {
            if (positionLabel != null)
            {
                positionLabel.text = $"{position}º";
                positionLabel.color = ColorForPosition(position);
            }

            if (nameLabel != null)
                nameLabel.text = string.IsNullOrEmpty(playerName) ? "—" : playerName;

            if (bestLapLabel != null)
                bestLapLabel.text = bestLapSeconds >= 0f ? HUDFormat.LapTimeShort(bestLapSeconds) : "--:--";

            if (background != null)
                background.color = isLocal ? localBackground : normalBackground;

            if (plate != null)
                plate.color = isLocal ? ColorForPosition(position) : Color.white;

            if (highlightBorder != null)
                highlightBorder.enabled = isLocal;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        private Color ColorForPosition(int position)
        {
            if (positionColors == null || positionColors.Length == 0)
                return Color.white;

            return positionColors[Mathf.Clamp(position - 1, 0, positionColors.Length - 1)];
        }
    }
}
