using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardUI : MonoBehaviour
{
    public struct Entry
    {
        public int Position;
        public string DisplayName;
        public bool IsLocal;
        public Color AccentColor;
    }

    [SerializeField] private RectTransform rowsContainer;
    [SerializeField] private RectTransform rowTemplate;
    [SerializeField] private int maxRows = 8;

    private readonly List<RowRefs> rows = new List<RowRefs>();

    public void SetEntries(IReadOnlyList<Entry> entries)
    {
        int count = Mathf.Min(maxRows, entries != null ? entries.Count : 0);
        EnsureRows(count);

        for (int i = 0; i < rows.Count; i++)
        {
            bool visible = i < count;
            rows[i].Root.gameObject.SetActive(visible);
            if (!visible)
                continue;

            Entry entry = entries[i];
            if (rows[i].PositionText != null)
                rows[i].PositionText.text = entry.Position.ToString();
            if (rows[i].NameText != null)
                rows[i].NameText.text = string.IsNullOrWhiteSpace(entry.DisplayName) ? $"PILOTO {entry.Position}" : entry.DisplayName.ToUpperInvariant();

            Color rowColor = entry.IsLocal
                ? new Color(0.02f, 0.44f, 0.92f, 0.92f)
                : new Color(0.02f, 0.05f, 0.07f, 0.84f);

            if (rows[i].Background != null)
                rows[i].Background.color = rowColor;
            if (rows[i].Accent != null)
                rows[i].Accent.color = entry.IsLocal ? new Color(0.15f, 0.72f, 1f, 1f) : entry.AccentColor;
            if (rows[i].NameText != null)
                rows[i].NameText.color = entry.IsLocal ? Color.white : new Color(0.90f, 0.94f, 1f, 0.96f);
        }
    }

    private void EnsureRows(int count)
    {
        if (rowTemplate == null || rowsContainer == null)
            return;

        while (rows.Count < count)
        {
            RectTransform row = Instantiate(rowTemplate, rowsContainer);
            row.gameObject.SetActive(true);
            rows.Add(new RowRefs(row));
        }
    }

    private sealed class RowRefs
    {
        public readonly RectTransform Root;
        public readonly Image Background;
        public readonly Image Accent;
        public readonly TMP_Text PositionText;
        public readonly TMP_Text NameText;

        public RowRefs(RectTransform root)
        {
            Root = root;
            Background = root.Find("Background")?.GetComponent<Image>();
            Accent = root.Find("Accent")?.GetComponent<Image>();
            PositionText = root.Find("Position")?.GetComponent<TMP_Text>();
            NameText = root.Find("Name")?.GetComponent<TMP_Text>();
        }
    }
}
