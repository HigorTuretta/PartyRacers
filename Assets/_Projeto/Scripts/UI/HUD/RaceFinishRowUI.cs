using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>
    /// Uma linha (placa) da classificação de fim de corrida. Todos os elementos são referências
    /// editáveis no Inspector — placa de fundo, badge de posição, nome, status/tempo e melhor volta.
    /// Clonada pela RaceFinishScreen para montar a tabela.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceFinishRowUI : MonoBehaviour
    {
        [Header("Placa")]
        [SerializeField] private Image background;
        [SerializeField] private Image positionBadge;

        [Header("Textos")]
        [SerializeField] private TMP_Text positionLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text bestLapLabel;

        [Header("Cores")]
        [SerializeField] private Color rowColor = new Color(1f, 1f, 1f, 0.07f);
        [SerializeField] private Color localRowColor = new Color(1f, 0.78f, 0.22f, 0.28f);
        [SerializeField] private Color badgeColor = new Color(0.16f, 0.2f, 0.34f, 1f);
        [SerializeField] private Color localBadgeColor = new Color(1f, 0.78f, 0.22f, 1f);
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color localTextColor = new Color(1f, 0.92f, 0.6f, 1f);
        [SerializeField] private Color finishedColor = new Color(0.65f, 1f, 0.72f, 1f);
        [SerializeField] private Color racingColor = new Color(0.62f, 0.68f, 1f, 1f);

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }

        public void Bind(int position, string name, string status, bool finished, string bestLap, bool isLocal)
        {
            if (background != null)
                background.color = isLocal ? localRowColor : rowColor;

            if (positionBadge != null)
                positionBadge.color = isLocal ? localBadgeColor : badgeColor;

            if (positionLabel != null)
                positionLabel.text = position.ToString();

            if (nameLabel != null)
            {
                nameLabel.text = name;
                nameLabel.color = isLocal ? localTextColor : textColor;
            }

            if (statusLabel != null)
            {
                statusLabel.text = status;
                statusLabel.color = finished ? finishedColor : racingColor;
            }

            if (bestLapLabel != null)
                bestLapLabel.text = string.IsNullOrEmpty(bestLap) ? "—" : bestLap;
        }
    }
}
