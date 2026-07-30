using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Liga/desliga os prefabs de tela e faz o fade pelo CanvasGroup que cada Screen_* já tem.
    /// O jogo abre no LOBBY — não existe tela inicial (handoff §3, tela 06).
    /// </summary>
    [DisallowMultipleComponent]
    public class ScreenRouter : MonoBehaviour
    {
        [System.Serializable]
        public class Tela
        {
            public string id;
            public GameObject raiz;
            public CanvasGroup grupo;
        }

        [Header("Telas montadas na cena")]
        [SerializeField] private List<Tela> telas = new List<Tela>();

        [Tooltip("Tela aberta ao entrar na cena. No Frontend é sempre o Lobby.")]
        [SerializeField] private string telaInicial = "Lobby";

        [Header("Transição (tokens.json)")]
        [SerializeField] private float duracaoFade = 0.18f;

        public string TelaAtual { get; private set; }

        private Coroutine transicao;

        private void Start() => Ir(telaInicial, imediato: true);

        /// <summary>Troca de tela com fade. Chamado pelos botões de navegação montados na cena.</summary>
        public void Ir(string id) => Ir(id, imediato: false);

        public void Ir(string id, bool imediato)
        {
            if (TelaAtual == id && !imediato)
                return;

            TelaAtual = id;

            if (transicao != null)
                StopCoroutine(transicao);

            if (imediato)
            {
                foreach (Tela t in telas)
                    Aplicar(t, t.id == id, t.id == id ? 1f : 0f);
                return;
            }

            transicao = StartCoroutine(Transicionar(id));
        }

        private IEnumerator Transicionar(string id)
        {
            // liga a tela de destino já em alfa 0 para o fade
            foreach (Tela t in telas)
            {
                if (t.id != id)
                    continue;
                Aplicar(t, true, 0f);
            }

            float tempo = 0f;
            while (tempo < duracaoFade)
            {
                tempo += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(tempo / Mathf.Max(0.01f, duracaoFade));

                foreach (Tela t in telas)
                {
                    if (t.grupo == null)
                        continue;
                    t.grupo.alpha = t.id == id ? k : 1f - k;
                }

                yield return null;
            }

            foreach (Tela t in telas)
                Aplicar(t, t.id == id, t.id == id ? 1f : 0f);

            transicao = null;
        }

        private static void Aplicar(Tela t, bool ativa, float alfa)
        {
            if (t == null || t.raiz == null)
                return;

            if (t.raiz.activeSelf != ativa)
                t.raiz.SetActive(ativa);

            if (t.grupo != null)
            {
                t.grupo.alpha = alfa;
                t.grupo.interactable = ativa;
                t.grupo.blocksRaycasts = ativa;
            }
        }
    }
}
