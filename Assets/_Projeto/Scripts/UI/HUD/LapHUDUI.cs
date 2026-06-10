using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.HUD
{
    /// <summary>Bloco superior central: volta atual / total e tempo da volta atual.</summary>
    public class LapHUDUI : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private Image plate;
        [SerializeField] private TMP_Text lapLabel;
        [SerializeField] private TMP_Text timeLabel;

        [Header("Textos")]
        [SerializeField] private string lapPrefix = "VOLTA";
        [SerializeField] private string finishedText = "FIM";

        public void SetLap(int currentLap, int totalLaps, bool finished)
        {
            if (lapLabel == null)
                return;

            if (finished)
                lapLabel.text = finishedText;
            else
                lapLabel.text = $"{lapPrefix} {Mathf.Max(1, currentLap)}/{Mathf.Max(1, totalLaps)}";
        }

        public void SetLapTime(float seconds)
        {
            if (timeLabel != null)
                timeLabel.text = HUDFormat.LapTime(seconds);
        }
    }
}
