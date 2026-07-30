using UnityEngine;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Curvas de aceleração usadas por toda a UI. O PLACA especifica easeOutQuad para troca de
    /// tela (tokens.json → movimento.trocaTela.curva); as outras seguem o mesmo vocabulário
    /// para o movimento inteiro parecer da mesma mão.
    /// </summary>
    public static class UIEase
    {
        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

        public static float InOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        /// <summary>Passa do alvo e volta — dá o "peso" cartoon que o PLACA pede nos botões.</summary>
        public static float OutBack(float t, float forca = 1.70158f)
        {
            float c3 = forca + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + forca * u * u;
        }

        /// <summary>Sobe e volta ao ponto de partida (0→1→0). Para pulsos e "pops".</summary>
        public static float PingPong(float t) => 1f - Mathf.Abs(t * 2f - 1f);
    }
}
