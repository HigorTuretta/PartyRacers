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

        [Header("Bloco de posição (canto superior esquerdo)")]
        [SerializeField] private TextMeshProUGUI textoPosicao;
        [Tooltip("\"DE 12\" — quantos correm.")]
        [SerializeField] private TextMeshProUGUI textoTotalDeCorredores;
        [Tooltip("Tempo até o carro da frente, ou LÍDER. Substitui o \"+2 POSIÇÕES\" do protótipo, " +
                 "que contava algo que já aconteceu — o intervalo diz o que está acontecendo agora.")]
        [SerializeField] private TextMeshProUGUI textoIntervalo;
        [SerializeField] private Color corLider = new Color(1f, 0.69f, 0.13f);
        [SerializeField] private Color corPerseguindo = new Color(0.79f, 0.82f, 0.96f);

        [Header("Estados (objetos irmãos)")]
        [Tooltip("Chip vermelho ÚLTIMA VOLTA — ligado só na volta final.")]
        [SerializeField] private GameObject chipUltimaVolta;

        private void Reset() => dados = FindAnyObjectByType<RaceHUDDataProvider>();

        // A HUD é PREFAB e o provedor vive na CENA: prefab não serializa essa referência.
        private void Awake()
        {
            if (dados == null)
                dados = FindAnyObjectByType<RaceHUDDataProvider>();
        }

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();
            AtualizarPosicao();

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

        /// <summary>
        /// Posição, total de corredores e o intervalo até quem está à frente.
        ///
        /// O protótipo escrevia "+2 POSIÇÕES" ali, um saldo do que já passou. Quem está correndo
        /// quer saber o que fazer AGORA, e a resposta é quantos segundos faltam para alcançar o
        /// carro da frente — ou que não há carro nenhum à frente.
        /// </summary>
        private void AtualizarPosicao()
        {
            if (textoPosicao != null)
                textoPosicao.text = dados.LocalPosition.ToString();

            if (textoTotalDeCorredores != null)
                textoTotalDeCorredores.text = $"DE {Mathf.Max(1, dados.RacerCount)}";

            if (textoIntervalo == null)
                return;

            bool lidera = dados.LocalPosition <= 1;
            string valor = lidera ? "LÍDER"
                         : !dados.LocalGapKnown ? "--"
                         : dados.LocalGapAhead >= 100f ? "+99"
                         : "+" + dados.LocalGapAhead.ToString("0.0");

            if (textoIntervalo.text != valor)
                textoIntervalo.text = valor;

            Color cor = lidera ? corLider : corPerseguindo;
            if (textoIntervalo.color != cor)
                textoIntervalo.color = cor;
        }
    }
}
