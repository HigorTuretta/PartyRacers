using TMPro;
using UnityEngine;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder da tela 01 (Screen_RaceHUD_PC): volta, tempo, melhor tempo e chip de última volta.
    /// Só escreve em referências já montadas na cena — não cria, move nem estiliza nada.
    /// </summary>
    [DisallowMultipleComponent]
    public class RaceHUDUI : MonoBehaviour
    {
        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Placa de volta (já montada na cena)")]
        [SerializeField] private TextMeshProUGUI textoVolta;
        [SerializeField] private TextMeshProUGUI textoTempo;
        [SerializeField] private TextMeshProUGUI textoUltimaVolta;
        [SerializeField] private TextMeshProUGUI textoMelhorVolta;

        [Header("Estados (objetos irmãos)")]
        [Tooltip("Chip vermelho ÚLTIMA VOLTA — ligado só na volta final.")]
        [SerializeField] private GameObject chipUltimaVolta;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();

            if (textoVolta != null)
                textoVolta.text = $"VOLTA {Mathf.Clamp(dados.CurrentLap, 1, dados.TotalLaps)}/{dados.TotalLaps}";

            if (textoTempo != null)
                textoTempo.text = HUDFormat.LapTime(dados.CurrentLapTime);

            if (textoUltimaVolta != null)
                textoUltimaVolta.text = "ÚLT " + HUDFormat.LapTime(dados.LastLapTime);

            if (textoMelhorVolta != null)
                textoMelhorVolta.text = "MELH " + HUDFormat.LapTime(dados.BestLapTime);

            if (chipUltimaVolta != null)
            {
                bool ultima = !dados.RaceFinished && dados.TotalLaps > 0 && dados.CurrentLap >= dados.TotalLaps;
                if (chipUltimaVolta.activeSelf != ultima)
                    chipUltimaVolta.SetActive(ultima);
            }
        }
    }
}
