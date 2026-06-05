using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Widget de marcha (apresentação pura). Recebe a marcha via SetGear() e exibe o número com
// punch de escala na troca. Refs preenchidas no prefab.
public class GearUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private TMP_Text gearNumber;
    [SerializeField] private Image backdrop;
    [Tooltip("Ícone de engrenagem (opcional).")]
    [SerializeField] private Image gearIcon;
    [SerializeField] private TMP_Text gearGlyph;

    [Header("Cores")]
    [SerializeField] private Color lowGearColor = new Color(1f, 1f, 1f, 0.98f);
    [SerializeField] private Color highGearColor = new Color(1f, 0.42f, 0.18f, 1f);
    [SerializeField] private Color backdropColor = new Color(0.04f, 0.05f, 0.06f, 0.78f);
    [SerializeField] private Color backdropPunchColor = new Color(0.15f, 0.72f, 1f, 1f);

    [Header("Animação")]
    [SerializeField] private float punchScale = 1.18f;
    [SerializeField] private float punchSpeed = 9f;

    private int previousGear = -1;
    private int gearCount = 5;
    private float punchTimer;

    private void Awake() => WarnMissing();

    /// <summary>Define a marcha atual (1..gearCount). gearCount ajusta o gradiente de cor.</summary>
    public void SetGear(int gear, int totalGears)
    {
        gearCount = Mathf.Max(1, totalGears);

        if (gear != previousGear)
        {
            if (previousGear >= 0)
                punchTimer = 1f;
            previousGear = gear;
        }

        punchTimer = Mathf.MoveTowards(punchTimer, 0f, punchSpeed * Time.deltaTime);

        if (gearNumber != null)
        {
            gearNumber.text = gear.ToString();
            float scale = 1f + (punchScale - 1f) * punchTimer;
            gearNumber.rectTransform.localScale = new Vector3(scale, scale, 1f);

            float gear01 = (gear - 1f) / Mathf.Max(1f, gearCount - 1f);
            gearNumber.color = Color.Lerp(lowGearColor, highGearColor, Mathf.Clamp01(gear01));
        }

        if (backdrop != null)
            backdrop.color = Color.Lerp(backdropColor, backdropPunchColor, punchTimer * 0.35f);

        if (gearIcon != null)
            gearIcon.color = Color.Lerp(backdropColor, backdropPunchColor, 0.22f + punchTimer * 0.25f);

        if (gearGlyph != null)
        {
            gearGlyph.text = "\u2699";
            gearGlyph.color = Color.Lerp(new Color(0.82f, 0.88f, 0.96f, 0.75f), highGearColor, punchTimer);
            float glyphScale = 1f + 0.16f * punchTimer;
            gearGlyph.rectTransform.localScale = new Vector3(glyphScale, glyphScale, 1f);
        }
    }

    private void WarnMissing()
    {
        if (gearNumber == null)
            Debug.LogWarning($"{name}: GearUI sem 'gearNumber' (TMP_Text). Marcha não será exibida.", this);
    }
}
