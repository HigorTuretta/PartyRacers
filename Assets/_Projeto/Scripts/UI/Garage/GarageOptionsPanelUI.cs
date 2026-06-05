using TMPro;
using UnityEngine;

// Painel de opções de customização (container componentizado). Expõe o Content rolável onde o
// GarageController instancia as linhas de categoria (CategoryButton).
public class GarageOptionsPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text title;

    public RectTransform Content => content;
    public TMP_Text Title => title;
}
