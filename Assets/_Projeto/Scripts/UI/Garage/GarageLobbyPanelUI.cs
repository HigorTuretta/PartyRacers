using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Painel de lobby (container componentizado). Expõe todas as referências que o GarageController
// liga à lógica (status, contagem, lista de jogadores, código, pronto, convidar, entrar).
public class GarageLobbyPanelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text count;
    [SerializeField] private TMP_Text status;
    [SerializeField] private RectTransform listContent;
    [SerializeField] private TMP_Text joinCode;
    [SerializeField] private TMP_InputField joinInput;
    [SerializeField] private Button enterButton;
    [SerializeField] private Button readyButton;
    [SerializeField] private TMP_Text readyLabel;
    [SerializeField] private Button inviteButton;

    public TMP_Text Title => title;
    public TMP_Text Count => count;
    public TMP_Text Status => status;
    public RectTransform ListContent => listContent;
    public TMP_Text JoinCode => joinCode;
    public TMP_InputField JoinInput => joinInput;
    public Button EnterButton => enterButton;
    public Button ReadyButton => readyButton;
    public TMP_Text ReadyLabel => readyLabel;
    public Button InviteButton => inviteButton;
}
