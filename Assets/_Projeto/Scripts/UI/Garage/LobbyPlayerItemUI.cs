using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Item de jogador do lobby (apresentação pura). O GarageController instancia este prefab por
// jogador e chama Set(). Mesma paleta do HUD/Garage. Mostra dono (★/(dono)), você/bot e pronto.
public class LobbyPlayerItemUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text statusText;

    [Header("Cores")]
    [SerializeField] private Color localColor = new Color(0.30f, 0.26f, 0.20f, 0.95f);
    [SerializeField] private Color remoteColor = new Color(0.08f, 0.10f, 0.13f, 0.95f);
    [SerializeField] private Color readyColor = new Color(0.16f, 0.74f, 0.36f, 1f);
    [SerializeField] private Color waitingColor = new Color(0.78f, 0.82f, 0.9f, 0.85f);
    [SerializeField] private Color textColor = new Color(1f, 1f, 1f, 0.97f);

    public void Set(string displayName, bool isHost, bool isLocal, bool isBot, bool isReady)
    {
        if (background != null)
            background.color = isLocal ? localColor : remoteColor;

        if (nameText != null)
        {
            string star = isHost ? "★ " : "";       // ★
            string owner = isHost ? " (dono)" : "";
            string kind = isLocal ? " (você)" : isBot ? " (bot)" : "";
            nameText.text = star + displayName + owner + kind;
            nameText.color = textColor;
        }

        if (statusText != null)
        {
            statusText.text = isReady ? "PRONTO" : "...";
            statusText.color = isReady ? readyColor : waitingColor;
        }
    }
}
