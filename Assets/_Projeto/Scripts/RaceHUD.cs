using TMPro;
using UnityEngine;

public class RaceHUD : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartRaceTracker raceTracker;
    [SerializeField] private KartPowerInventory powerInventory;

    [Header("Textos")]
    [SerializeField] private TMP_Text lapText;
    [SerializeField] private TMP_Text powerText;

    private void Update()
    {
        UpdateLapText();
        UpdatePowerText();
    }

    private void UpdateLapText()
    {
        if (raceTracker == null || lapText == null)
            return;

        if (raceTracker.RaceFinished)
        {
            lapText.text = "Corrida finalizada!";
            return;
        }

        lapText.text = $"Volta {raceTracker.CurrentLap} / {raceTracker.TotalLaps}";
    }

    private void UpdatePowerText()
    {
        if (powerInventory == null || powerText == null)
            return;

        powerText.text = $"Poder: {powerInventory.GetPowerDisplayName()}";
    }
}