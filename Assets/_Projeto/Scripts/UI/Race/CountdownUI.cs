using UnityEngine;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Binder da tela 02: 3 · 2 · 1 · VAI!. Cada passo é um objeto irmão já montado (placa + dígito),
    /// com sua própria cor e ângulo — o binder só liga o certo, nunca pinta nem gira nada.
    /// Sem cronômetro visível durante a contagem.
    /// </summary>
    [DisallowMultipleComponent]
    public class CountdownUI : MonoBehaviour
    {
        [Header("Passos (objetos irmãos já montados)")]
        [SerializeField] private GameObject passo3;
        [SerializeField] private GameObject passo2;
        [SerializeField] private GameObject passo1;
        [SerializeField] private GameObject passoJa;

        [Header("Raiz da tela")]
        [SerializeField] private GameObject raiz;

        [Header("Ritmo")]
        [SerializeField] private float duracaoPorPasso = 1f;
        [SerializeField] private float duracaoDoJa = 0.7f;

        private float relogio = -1f;

        /// <summary>Começa a contagem. Chamado pelo RaceManager quando a corrida vai largar.</summary>
        public void Iniciar()
        {
            relogio = 0f;
            if (raiz != null) raiz.SetActive(true);
            Mostrar(passo3);
        }

        /// <summary>Esconde a contagem imediatamente.</summary>
        public void Parar()
        {
            relogio = -1f;
            Mostrar(null);
            if (raiz != null) raiz.SetActive(false);
        }

        private void Update()
        {
            if (relogio < 0f)
                return;

            relogio += Time.deltaTime;

            if (relogio < duracaoPorPasso) Mostrar(passo3);
            else if (relogio < duracaoPorPasso * 2f) Mostrar(passo2);
            else if (relogio < duracaoPorPasso * 3f) Mostrar(passo1);
            else if (relogio < duracaoPorPasso * 3f + duracaoDoJa) Mostrar(passoJa);
            else Parar();
        }

        private void Mostrar(GameObject alvo)
        {
            Ligar(passo3, alvo == passo3);
            Ligar(passo2, alvo == passo2);
            Ligar(passo1, alvo == passo1);
            Ligar(passoJa, alvo == passoJa);
        }

        private static void Ligar(GameObject alvo, bool ativo)
        {
            if (alvo != null && alvo.activeSelf != ativo)
                alvo.SetActive(ativo);
        }
    }
}
