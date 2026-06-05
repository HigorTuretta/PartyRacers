using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RacePositionUI : MonoBehaviour
{
    [SerializeField] private TMP_Text positionText;
    [SerializeField] private Image background;
    [SerializeField] private Image accent;
    [SerializeField] private Color[] positionColors =
    {
        new Color(1f, 0.82f, 0.10f, 1f),
        new Color(0.16f, 0.70f, 1f, 1f),
        new Color(1f, 0.48f, 0.16f, 1f),
        new Color(0.62f, 0.24f, 1f, 1f),
        new Color(0.55f, 0.65f, 0.72f, 1f)
    };

    [SerializeField] private float punchScale = 1.14f;
    [SerializeField] private float punchSpeed = 8f;

    private int previousPosition = -1;
    private float punch;

    public void SetPosition(int position)
    {
        position = Mathf.Max(1, position);

        if (position != previousPosition)
        {
            if (previousPosition > 0)
                punch = 1f;
            previousPosition = position;
        }

        if (positionText != null)
            positionText.text = $"{position}<size=62%>\u00ba</size>";

        Color color = GetPositionColor(position);
        if (accent != null)
            accent.color = color;

        if (background != null)
            background.color = Color.Lerp(new Color(0.02f, 0.06f, 0.10f, 0.92f), color, 0.22f);
    }

    private void Update()
    {
        punch = Mathf.MoveTowards(punch, 0f, punchSpeed * Time.unscaledDeltaTime);
        float scale = 1f + (punchScale - 1f) * punch;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private Color GetPositionColor(int position)
    {
        if (positionColors == null || positionColors.Length == 0)
            return Color.white;

        return positionColors[Mathf.Clamp(position - 1, 0, positionColors.Length - 1)];
    }
}
