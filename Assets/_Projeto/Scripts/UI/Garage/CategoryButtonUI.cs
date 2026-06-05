using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botão de categoria de customização (apresentação + intenção). Rótulo + valor + setas ‹ ›
// (e ícone opcional). O GarageController instancia, define rótulo/ícone e assina Previous/Next.
public class CategoryButtonUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text label;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image icon;
    [SerializeField] private Button previousButton;
    [SerializeField] private Button nextButton;
    [Tooltip("Swatch de cor (usado na categoria COR no lugar do texto de valor).")]
    [SerializeField] private Image swatch;

    public Image Icon => icon;
    public Image Swatch => swatch;

    public void Configure(string labelText, Sprite iconSprite, Action onPrevious, Action onNext)
    {
        if (label != null)
            label.text = labelText;

        if (icon != null)
        {
            icon.enabled = iconSprite != null;
            if (iconSprite != null)
                icon.sprite = iconSprite;
        }

        if (previousButton != null && onPrevious != null)
            previousButton.onClick.AddListener(() => onPrevious());

        if (nextButton != null && onNext != null)
            nextButton.onClick.AddListener(() => onNext());
    }

    public void SetValue(string value)
    {
        if (valueText != null)
            valueText.text = value;
    }

    public void SetSwatchColor(Color color)
    {
        if (swatch != null)
            swatch.color = color;
    }

    // Alterna entre mostrar o texto de valor (peças) ou o swatch de cor.
    public void UseSwatch(bool useSwatch)
    {
        if (valueText != null)
            valueText.gameObject.SetActive(!useSwatch);
        if (swatch != null)
            swatch.gameObject.SetActive(useSwatch);
    }

    public void AddNavListeners(Action onPrevious, Action onNext)
    {
        if (previousButton != null && onPrevious != null)
            previousButton.onClick.AddListener(() => onPrevious());
        if (nextButton != null && onNext != null)
            nextButton.onClick.AddListener(() => onNext());
    }
}
