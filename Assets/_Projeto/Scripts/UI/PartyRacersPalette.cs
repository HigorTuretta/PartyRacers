using UnityEngine;

namespace PartyRacers.UI
{
    /// <summary>
    /// Espelha o tokens.json da direção PLACA. É REFERÊNCIA para o designer montar
    /// as telas no Inspector — nenhum script deve colorir UI por código usando isto.
    /// </summary>
    [CreateAssetMenu(fileName = "PartyRacersPalette", menuName = "Party Racers/Paleta (PLACA)")]
    public class PartyRacersPalette : ScriptableObject
    {
        [Header("Fundos e contornos")]
        public Color ink = Hex("#0A0C22");
        public Color inkText = Hex("#15161C");
        public Color night = Hex("#171A3D");
        public Color deepBlue = Hex("#101334");
        public Color blue = Hex("#1B2050");
        public Color royal = Hex("#2A3480");
        public Color outlineSoft = Hex("#3B4180");
        public Color labelInk = Hex("#5C63A8");

        [Header("Destaques")]
        public Color amber = Hex("#FFB020");
        public Color amberLight = Hex("#FFC24D");
        public Color amberPressed = Hex("#E09410");
        public Color cream = Hex("#FFF7E8");
        public Color creamDim = Hex("#FFE7B8");
        public Color green = Hex("#3DDC97");
        public Color greenPressed = Hex("#2FBB7E");
        public Color red = Hex("#FF4D6D");
        public Color redSoft = Hex("#FF8DA0");
        public Color sky = Hex("#35A7FF");
        public Color violet = Hex("#8C7BFF");

        [Header("Texto")]
        public Color textPrimary = Hex("#FFF7E8");
        public Color textSecondary = Hex("#C3CEDD");
        public Color textMuted = Hex("#9AA2D8");
        public Color textDisabled = Hex("#6B6F9E");

        [Header("Raridade")]
        public Color raridadeComum = Hex("#9AA2D8");
        public Color raridadeRaro = Hex("#35A7FF");
        public Color raridadeEpico = Hex("#8C7BFF");
        public Color raridadeLendario = Hex("#FFB020");

        [Header("Escala tipográfica @1920x1080")]
        public int h1 = 46;
        public int h2 = 40;
        public int h3 = 32;
        public int titulo = 30;
        public int rotulo = 25;
        public int corpo = 22;
        public int meta = 20;
        public int micro = 17;
        public int minimoLegivel = 17;

        [Header("Movimento (segundos)")]
        public float botaoPressionarDuracao = 0.08f;
        public float toastEntrada = 0.18f;
        public float toastVida = 2.5f;
        public float toastSaida = 0.25f;
        public int toastMaxSimultaneos = 3;
        public float arcoPulsoAproximando = 0.8f;
        public float arcoPulsoIminente = 0.25f;
        public float trocaTelaDuracao = 0.22f;

        static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }
    }
}
