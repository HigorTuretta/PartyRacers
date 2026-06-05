using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NitroMeterUI : MonoBehaviour
{
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text iconLabel;
    [SerializeField] private Color idleColor = new Color(0.08f, 0.45f, 0.95f, 0.85f);
    [SerializeField] private Color activeColor = new Color(0.18f, 0.86f, 1f, 1f);
    [SerializeField] private float fillSmooth = 10f;
    [SerializeField] private float pulseScale = 1.12f;
    [SerializeField] private float pulseSpeed = 7f;

    private float displayedFill;
    private float pulse;

    public void SetNitro(float amount01, bool active)
    {
        float target = Mathf.Clamp01(amount01);
        displayedFill = Mathf.MoveTowards(displayedFill, target, fillSmooth * Time.unscaledDeltaTime);

        if (fill != null)
        {
            fill.fillAmount = displayedFill;
            fill.color = Color.Lerp(idleColor, activeColor, active ? 1f : 0.25f);
        }

        if (active)
            pulse = 1f;
    }

    private void Update()
    {
        pulse = Mathf.MoveTowards(pulse, 0f, pulseSpeed * Time.unscaledDeltaTime);
        float scale = 1f + (pulseScale - 1f) * pulse;

        if (iconLabel != null)
        {
            iconLabel.text = "\u26a1";
            iconLabel.rectTransform.localScale = new Vector3(scale, scale, 1f);
            iconLabel.color = Color.Lerp(new Color(0.70f, 0.92f, 1f, 0.82f), activeColor, pulse);
        }
    }
}
