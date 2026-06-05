using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Widget de velocímetro (apresentação pura). Recebe dados via SetSpeed() e atualiza barra + número.
// Não conhece o gameplay — o HUDRootUI alimenta os valores. Refs preenchidas no prefab.
public class SpeedometerUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Image fillBar;
    [SerializeField] private Image[] segmentFills;
    [SerializeField] private TMP_Text speedNumber;
    [SerializeField] private TMP_Text unitLabel;

    [Header("Cores")]
    [Tooltip("Azul para velocidade baixa/normal.")]
    [SerializeField] private Color lowColor = new Color(0.15f, 0.72f, 1f, 1f);
    [SerializeField] private Color greenColor = new Color(0.28f, 1f, 0.32f, 1f);
    [SerializeField] private Color yellowColor = new Color(1f, 0.82f, 0.24f, 1f);
    [Tooltip("Laranja (alerta) para velocidade alta.")]
    [SerializeField] private Color highColor = new Color(1f, 0.50f, 0.18f, 1f);
    [Tooltip("Vermelho para o topo da escala.")]
    [SerializeField] private Color maxColor = new Color(1f, 0.12f, 0.10f, 1f);
    [SerializeField] private Color emptySegmentColor = new Color(0.05f, 0.09f, 0.12f, 0.92f);

    [Header("Animação")]
    [SerializeField] private float numberSmooth = 16f;
    [SerializeField] private float fillSmooth = 12f;
    [Tooltip("Preenchimento máximo da barra (deixa folga visual no fim).")]
    [SerializeField, Range(0.1f, 1f)] private float maxFill = 0.85f;
    [Tooltip("A partir deste preenchimento (0..1) a cor começa a virar 'alerta'.")]
    [SerializeField, Range(0f, 1f)] private float highColorThreshold = 0.7f;

    private float displayedSpeed;
    private float displayedFill;

    private void Awake() => WarnMissing();

    /// <summary>Atualiza o velocímetro. speedKmh = velocidade atual, maxKmh = topo da escala.</summary>
    public void SetSpeed(float speedKmh, float maxKmh)
    {
        float dt = Time.deltaTime;
        float target01 = Mathf.Clamp01(speedKmh / Mathf.Max(1f, maxKmh));

        displayedSpeed = Mathf.MoveTowards(displayedSpeed, speedKmh, numberSmooth * Mathf.Max(speedKmh, 30f) * dt);
        displayedFill = Mathf.MoveTowards(displayedFill, target01, fillSmooth * dt);

        if (speedNumber != null)
            speedNumber.text = Mathf.RoundToInt(displayedSpeed).ToString();

        if (fillBar != null)
        {
            fillBar.fillAmount = displayedFill * maxFill;
            fillBar.color = EvaluateSpeedColor(displayedFill);
        }

        UpdateSegments(displayedFill);

        if (unitLabel != null)
            unitLabel.color = EvaluateSpeedColor(displayedFill);
    }

    private Color EvaluateSpeedColor(float speed01)
    {
        speed01 = Mathf.Clamp01(speed01);

        if (speed01 < 0.35f)
        {
            float t = Mathf.InverseLerp(0f, 0.35f, speed01);
            return Color.Lerp(lowColor, greenColor, t);
        }

        if (speed01 < 0.62f)
        {
            float t = Mathf.InverseLerp(0.35f, 0.62f, speed01);
            return Color.Lerp(greenColor, yellowColor, t);
        }

        if (speed01 < highColorThreshold)
        {
            float t = Mathf.InverseLerp(0.62f, Mathf.Max(0.63f, highColorThreshold), speed01);
            return Color.Lerp(yellowColor, highColor, t);
        }

        float hot = Mathf.InverseLerp(highColorThreshold, 1f, speed01);
        return Color.Lerp(highColor, maxColor, hot);
    }

    private void UpdateSegments(float speed01)
    {
        if (segmentFills == null || segmentFills.Length == 0)
            return;

        float scaled = Mathf.Clamp01(speed01) * segmentFills.Length;

        for (int i = 0; i < segmentFills.Length; i++)
        {
            Image segment = segmentFills[i];
            if (segment == null)
                continue;

            float segmentFill = Mathf.Clamp01(scaled - i);
            segment.fillAmount = segmentFill;
            segment.color = segmentFill > 0.01f
                ? EvaluateSpeedColor((i + 0.5f) / segmentFills.Length)
                : emptySegmentColor;
        }
    }

    private void WarnMissing()
    {
        if (speedNumber == null)
            Debug.LogWarning($"{name}: SpeedometerUI sem 'speedNumber' (TMP_Text). Velocidade não será exibida.", this);
        if (fillBar == null)
            Debug.LogWarning($"{name}: SpeedometerUI sem 'fillBar' (Image). Barra de velocidade não animará.", this);
    }
}
