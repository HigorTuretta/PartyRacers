using System.Collections;
using TMPro;
using UnityEngine;

public class RaceManager : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private KartController playerKart;
    [SerializeField] private TMP_Text countdownText;

    [Header("Configuração")]
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float goMessageDuration = 0.75f;

    [Header("Estado")]
    [SerializeField] private bool raceStarted;

    public bool RaceStarted => raceStarted;

    private void Start()
    {
        StartCoroutine(StartRaceRoutine());
    }

    private IEnumerator StartRaceRoutine()
    {
        raceStarted = false;

        if (playerKart != null)
            playerKart.SetControlEnabled(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);

            countdownText.text = "3";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "2";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "1";
            yield return new WaitForSeconds(countdownStepDuration);

            countdownText.text = "VAI!";
        }

        raceStarted = true;

        if (playerKart != null)
            playerKart.SetControlEnabled(true);

        yield return new WaitForSeconds(goMessageDuration);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);
    }
}