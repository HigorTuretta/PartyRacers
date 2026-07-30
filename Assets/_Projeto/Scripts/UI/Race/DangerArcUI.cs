using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Aviso de ataque da tela 01: SÓ o arco na borda. Decisão de design fechada (handoff §4) —
    /// sem texto, sem ícone, sem seta de direção e sem nada no centro da tela.
    /// Fraco = ameaça se aproximando (pulso 0,8 s); forte = impacto iminente (pulso 0,25 s);
    /// o pulso da base entra quando a ameaça vem de trás.
    /// </summary>
    [DisallowMultipleComponent]
    public class DangerArcUI : MonoBehaviour
    {
        public enum Nivel { Nenhum, Fraco, Forte }

        [Header("Overlays já montados na cena")]
        [SerializeField] private GameObject arcoFraco;
        [SerializeField] private GameObject arcoForte;
        [SerializeField] private GameObject pulsoDeTras;

        [Header("Ritmo do pulso (tokens.json)")]
        [SerializeField] private float periodoFraco = 0.8f;
        [SerializeField] private float periodoForte = 0.25f;
        [SerializeField] private float alfaMinimo = 0.25f;

        [Header("Alvos de opacidade")]
        [SerializeField] private Graphic graficoFraco;
        [SerializeField] private Graphic graficoForte;

        private Nivel nivel = Nivel.Nenhum;
        private float relogio;

        /// <summary>Define o nível da ameaça e se ela vem de trás. Chamado pelo gameplay.</summary>
        public void Definir(Nivel novo, bool deTras = false)
        {
            if (nivel != novo)
            {
                nivel = novo;
                relogio = 0f;
                Ligar(arcoFraco, novo == Nivel.Fraco);
                Ligar(arcoForte, novo == Nivel.Forte);
            }

            Ligar(pulsoDeTras, novo != Nivel.Nenhum && deTras);
        }

        public void Limpar() => Definir(Nivel.Nenhum);

        private void Update()
        {
            if (nivel == Nivel.Nenhum)
                return;

            float periodo = nivel == Nivel.Forte ? periodoForte : periodoFraco;
            relogio += Time.deltaTime;

            // pulso senoidal entre alfaMinimo e 1
            float t = Mathf.PingPong(relogio / Mathf.Max(0.01f, periodo), 1f);
            float alfa = Mathf.Lerp(alfaMinimo, 1f, t);

            Graphic alvo = nivel == Nivel.Forte ? graficoForte : graficoFraco;
            if (alvo != null)
            {
                Color c = alvo.color;
                c.a = alfa;
                alvo.color = c;
            }
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
