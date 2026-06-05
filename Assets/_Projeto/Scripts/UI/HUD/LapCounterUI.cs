using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Widget de contador de voltas (apresentação pura). Recebe dados via SetLap() / SetFinished().
// Mostra "VOLTA atual/total" e círculos de progresso (preenchidos por volta concluída).
public class LapCounterUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text lapText;
    [SerializeField] private Image backdrop;
    [Tooltip("Container onde os círculos de progresso são instanciados.")]
    [SerializeField] private RectTransform circlesContainer;
    [Tooltip("Template de um círculo (Image). Deve estar desativado no prefab — é clonado.")]
    [SerializeField] private Image circleTemplate;

    [Header("Cores")]
    [SerializeField] private Color completedColor = new Color(1f, 0.82f, 0.24f, 1f);
    [SerializeField] private Color pendingColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private Color textColor = new Color(1f, 0.82f, 0.24f, 1f);
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.06f, 0.78f);

    [Header("Animação")]
    [SerializeField] private float punchScale = 1.28f;
    [SerializeField] private float punchSpeed = 5.5f;

    private readonly List<Image> circles = new List<Image>();
    private int builtTotal = -1;
    private int previousLap = -1;
    private float punchTimer;

    private void Awake() => WarnMissing();

    /// <summary>Atualiza volta atual e total. Constrói/recolore os círculos de progresso.</summary>
    public void SetLap(int currentLap, int totalLaps)
    {
        EnsureCircles(totalLaps);

        if (currentLap != previousLap)
        {
            if (previousLap >= 0)
                punchTimer = 1f;
            previousLap = currentLap;
        }

        punchTimer = Mathf.MoveTowards(punchTimer, 0f, punchSpeed * Time.deltaTime);

        if (lapText != null)
        {
            lapText.text = $"{Mathf.Max(1, currentLap)}/{Mathf.Max(1, totalLaps)}";
            lapText.color = textColor;
            float scale = 1f + (punchScale - 1f) * punchTimer;
            lapText.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        // Voltas concluídas = currentLap - 1 (ex.: na volta 1, nenhuma concluída).
        int completed = Mathf.Clamp(currentLap - 1, 0, totalLaps);
        for (int i = 0; i < circles.Count; i++)
            circles[i].color = i < completed ? completedColor : pendingColor;

        if (backdrop != null)
            backdrop.color = backdropColor;
    }

    /// <summary>Marca a corrida como concluída (todos os círculos preenchidos + texto FIM).</summary>
    public void SetFinished(int totalLaps)
    {
        EnsureCircles(totalLaps);
        if (lapText != null)
        {
            lapText.text = "FIM";
            lapText.color = completedColor;
        }
        for (int i = 0; i < circles.Count; i++)
            circles[i].color = completedColor;
    }

    private void EnsureCircles(int totalLaps)
    {
        totalLaps = Mathf.Clamp(totalLaps, 1, 20);
        if (builtTotal == totalLaps || circleTemplate == null || circlesContainer == null)
            return;

        for (int i = circles.Count - 1; i >= 0; i--)
        {
            if (circles[i] != null)
                Destroy(circles[i].gameObject);
        }
        circles.Clear();

        for (int i = 0; i < totalLaps; i++)
        {
            Image circle = Instantiate(circleTemplate, circlesContainer);
            circle.gameObject.SetActive(true);
            circle.color = pendingColor;
            circles.Add(circle);
        }

        builtTotal = totalLaps;
    }

    private void WarnMissing()
    {
        if (lapText == null)
            Debug.LogWarning($"{name}: LapCounterUI sem 'lapText' (TMP_Text). Voltas não serão exibidas.", this);
    }
}
