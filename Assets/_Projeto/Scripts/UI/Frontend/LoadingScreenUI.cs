using System.Collections;
using System.Collections.Generic;
using PartyRacers.UI.Motion;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Tela 13 (carregando). Serve tanto para Boot -> Frontend quanto para Frontend -> pista.
    ///
    /// O carregamento é assíncrono e a tela fica viva enquanto ele corre: o pulso marcha, os
    /// pontos respiram e as dicas trocam sozinhas. Nada de porcentagem falsa — o PLACA pede
    /// só o indicador de atividade.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingScreenUI : MonoBehaviour
    {
        [Header("Peças montadas na cena")]
        [SerializeField] private CanvasGroup grupo;
        [Tooltip("Passos do pulso: acendem em sequência, um por vez.")]
        [SerializeField] private List<Image> passosDoPulso = new List<Image>();
        [Tooltip("Pontinhos que respiram junto com o pulso.")]
        [SerializeField] private List<RectTransform> pontos = new List<RectTransform>();
        [SerializeField] private TextMeshProUGUI textoEstado;
        [SerializeField] private TextMeshProUGUI textoDica;
        [Tooltip("Bloco de ping. Sem fonte de dados real ele fica escondido em vez de mentir.")]
        [SerializeField] private GameObject blocoConexao;

        [Header("Ritmo")]
        [SerializeField] private float intervaloDoPulso = 0.16f;
        [SerializeField] private float segundosPorDica = 3.5f;
        [SerializeField] private float fadeDaDica = 0.25f;
        [Tooltip("Tempo mínimo na tela, para a dica dar tempo de ser lida.")]
        [SerializeField] private float tempoMinimo = 1.6f;

        [Header("Cores do pulso")]
        [SerializeField] private Color passoAceso = new Color(1f, 0.690f, 0.125f);
        [SerializeField] private Color passoApagado = new Color(0.294f, 0.329f, 0.659f);

        [Header("Dicas (o designer edita aqui)")]
        [TextArea]
        [SerializeField]
        private string[] dicas =
        {
            "O escudo bloqueia o disco voador — se você está em primeiro, guarde-o para a última volta.",
            "Soltar o acelerador antes da curva rende mais que frear em cima dela.",
            "O drift só compensa se você sair dele apontado para a próxima reta.",
            "O foguete mira sozinho em quem está na sua frente. Não precisa apontar.",
            "Passar por cima da faixa de turbo recarrega o nitro mais rápido que a sua velocidade.",
            "Na garagem, arraste o carro com o mouse para girá-lo e ver o traseiro.",
        };

        private Coroutine animacao;
        private int proximaDica;

        void Awake()
        {
            if (grupo == null) grupo = GetComponent<CanvasGroup>();
            // sem medidor de ping de verdade, o rótulo de conexão seria número inventado
            if (blocoConexao != null) blocoConexao.SetActive(false);
        }

        /// <summary>Acende a tela e começa o movimento. <paramref name="estado"/> vai no rótulo.</summary>
        public void Mostrar(string estado = "CARREGANDO")
        {
            gameObject.SetActive(true);
            if (grupo != null) { grupo.alpha = 1f; grupo.blocksRaycasts = true; }
            if (textoEstado != null) textoEstado.text = estado;

            SortearDica(imediato: true);
            if (animacao != null) StopCoroutine(animacao);
            animacao = StartCoroutine(Animar());
        }

        public void Esconder()
        {
            if (animacao != null) { StopCoroutine(animacao); animacao = null; }
            if (grupo != null) { grupo.alpha = 0f; grupo.blocksRaycasts = false; }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Mostra a tela, carrega <paramref name="cena"/> em segundo plano e só troca quando
        /// terminar (e depois do tempo mínimo). Enquanto isso a tela continua animando.
        /// </summary>
        public void CarregarCena(string cena, string estado = "CARREGANDO PISTA")
        {
            if (string.IsNullOrWhiteSpace(cena))
            {
                Debug.LogError("[Carregando] nome de cena vazio.");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(cena))
            {
                Debug.LogError($"[Carregando] a cena '{cena}' não está no Build Settings.");
                return;
            }

            Mostrar(estado);
            StartCoroutine(Carregando(cena));
        }

        IEnumerator Carregando(string cena)
        {
            // um frame para a tela realmente aparecer antes do trabalho pesado começar
            yield return null;

            AsyncOperation carga = SceneManager.LoadSceneAsync(cena);
            carga.allowSceneActivation = false;

            float inicio = Time.unscaledTime;
            // 0.9 é o teto do progresso enquanto allowSceneActivation está desligado
            while (carga.progress < 0.9f || Time.unscaledTime - inicio < tempoMinimo)
                yield return null;

            carga.allowSceneActivation = true;
        }

        IEnumerator Animar()
        {
            float trocaDica = Time.unscaledTime + segundosPorDica;
            int passo = 0;

            while (true)
            {
                // pulso: um passo aceso por vez, marchando
                for (int i = 0; i < passosDoPulso.Count; i++)
                {
                    if (passosDoPulso[i] == null) continue;
                    passosDoPulso[i].color = i == passo ? passoAceso : passoApagado;
                }

                // pontos respirando fora de fase, para não parecer um blink só
                for (int i = 0; i < pontos.Count; i++)
                {
                    if (pontos[i] == null) continue;
                    float f = Mathf.Sin((Time.unscaledTime * 3f) + i * 0.7f) * 0.5f + 0.5f;
                    pontos[i].localScale = Vector3.one * Mathf.Lerp(0.75f, 1.15f, f);
                }

                if (Time.unscaledTime >= trocaDica)
                {
                    trocaDica = Time.unscaledTime + segundosPorDica;
                    yield return TrocarDica();
                }

                passo = passosDoPulso.Count > 0 ? (passo + 1) % passosDoPulso.Count : 0;
                yield return new WaitForSecondsRealtime(intervaloDoPulso);
            }
        }

        IEnumerator TrocarDica()
        {
            if (textoDica == null) { SortearDica(); yield break; }

            yield return Fade(textoDica, 1f, 0f);
            SortearDica();
            yield return Fade(textoDica, 0f, 1f);
        }

        IEnumerator Fade(TextMeshProUGUI alvo, float de, float para)
        {
            for (float t = 0f; t < fadeDaDica; t += Time.unscaledDeltaTime)
            {
                float a = Mathf.Lerp(de, para, UIEase.OutQuad(t / fadeDaDica));
                alvo.alpha = a;
                yield return null;
            }
            alvo.alpha = para;
        }

        /// <summary>Percorre as dicas em ordem embaralhada por sessão, sem repetir a anterior.</summary>
        void SortearDica(bool imediato = false)
        {
            if (textoDica == null || dicas == null || dicas.Length == 0)
                return;

            if (imediato) proximaDica = Random.Range(0, dicas.Length);
            textoDica.text = dicas[proximaDica % dicas.Length];
            proximaDica = (proximaDica + 1) % dicas.Length;
        }
    }
}
