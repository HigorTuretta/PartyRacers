using UnityEngine;
using PartyRacers.UI.Motion;

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

        private void OnEnable()
        {
            RaceManager.CountdownPhaseChanged += AoMudarFase;
            RaceManager.CountdownHidden += Parar;
            Parar();
        }

        private void OnDisable()
        {
            RaceManager.CountdownPhaseChanged -= AoMudarFase;
            RaceManager.CountdownHidden -= Parar;
        }

        /// <summary>Começa a contagem. Chamado pelo RaceManager quando a corrida vai largar.</summary>
        public void Iniciar()
        {
            if (raiz != null) raiz.SetActive(true);
            Mostrar(passo3);
        }

        /// <summary>Esconde a contagem imediatamente.</summary>
        public void Parar()
        {
            Mostrar(null);
            if (raiz != null) raiz.SetActive(false);
        }

        private void AoMudarFase(RaceManager.CountdownPhase fase)
        {
            if (fase == RaceManager.CountdownPhase.Idle)
            {
                Parar();
                return;
            }

            if (raiz != null)
                raiz.SetActive(true);

            switch (fase)
            {
                case RaceManager.CountdownPhase.Three: Mostrar(passo3); break;
                case RaceManager.CountdownPhase.Two: Mostrar(passo2); break;
                case RaceManager.CountdownPhase.One: Mostrar(passo1); break;
                case RaceManager.CountdownPhase.Go: Mostrar(passoJa); break;
            }
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

            if (ativo && alvo != null)
                alvo.GetComponent<UIAppear>()?.Tocar();
        }
    }
}
