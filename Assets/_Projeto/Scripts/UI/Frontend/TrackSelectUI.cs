using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Seleção de pista. O cartão já está montado na cena; este script só escreve nome,
    /// miniatura e voltas da pista atual e troca com as setas. A escolha fica salva, então
    /// o jogador volta para a mesma pista na próxima sessão.
    /// </summary>
    [DisallowMultipleComponent]
    public class TrackSelectUI : MonoBehaviour
    {
        const string Chave = "partida.pista";

        [Header("Catálogo (ScriptableObjects)")]
        [SerializeField] private List<TrackDefinition> pistas = new List<TrackDefinition>();

        [Header("Campos montados na cena")]
        [SerializeField] private Image miniatura;
        [SerializeField] private TextMeshProUGUI nome;
        [SerializeField] private TextMeshProUGUI descricao;
        [SerializeField] private TextMeshProUGUI voltas;
        [SerializeField] private Button btnAnterior;
        [SerializeField] private Button btnProximo;
        [SerializeField] private Transform containerIndicadores;

        [Header("Cores dos indicadores")]
        [SerializeField] private Color pontoAtivo = new Color(1f, 0.690f, 0.125f);
        [SerializeField] private Color pontoInativo = new Color(0.294f, 0.329f, 0.659f);

        [Header("Eventos")]
        public UnityEngine.Events.UnityEvent<TrackDefinition> aoTrocarPista;

        private int indice;
        private Coroutine pulso;

        /// <summary>Pista escolhida agora. Null quando o catálogo está vazio.</summary>
        public TrackDefinition Atual =>
            Validas.Count == 0 ? null : Validas[Mathf.Clamp(indice, 0, Validas.Count - 1)];

        private List<TrackDefinition> Validas =>
            pistas.Where(p => p != null && p.Jogavel).ToList();

        private void Awake()
        {
            if (btnAnterior != null) btnAnterior.onClick.AddListener(() => Trocar(-1));
            if (btnProximo != null) btnProximo.onClick.AddListener(() => Trocar(+1));

            // volta para a pista da sessão anterior
            string salva = PlayerPrefs.GetString(Chave, string.Empty);
            var lista = Validas;
            int i = lista.FindIndex(p => p.cena == salva);
            indice = i >= 0 ? i : 0;
        }

        private void OnEnable() => Redesenhar();

        private void Trocar(int passo)
        {
            var lista = Validas;
            if (lista.Count <= 1)
                return;

            indice = ((indice + passo) % lista.Count + lista.Count) % lista.Count;
            Salvar();
            Redesenhar();
            Pulsar();
            aoTrocarPista?.Invoke(Atual);
        }

        private void Salvar()
        {
            var p = Atual;
            if (p == null) return;
            PlayerPrefs.SetString(Chave, p.cena);
            PlayerPrefs.Save();
        }

        /// <summary>Reescreve o cartão a partir da pista atual.</summary>
        public void Redesenhar()
        {
            var lista = Validas;
            var p = Atual;

            if (nome != null) nome.text = p != null ? p.nome : "SEM PISTA";
            if (descricao != null)
            {
                bool tem = p != null && !string.IsNullOrWhiteSpace(p.descricao);
                descricao.gameObject.SetActive(tem);
                if (tem) descricao.text = p.descricao;
            }
            if (voltas != null)
            {
                bool tem = p != null && p.voltas > 0;
                voltas.gameObject.SetActive(tem);
                if (tem) voltas.text = p.voltas == 1 ? "1 VOLTA" : $"{p.voltas} VOLTAS";
            }
            if (miniatura != null)
            {
                bool tem = p != null && p.miniatura != null;
                miniatura.enabled = tem;
                if (tem) miniatura.sprite = p.miniatura;
            }

            // as setas só fazem sentido com mais de uma pista
            bool varias = lista.Count > 1;
            if (btnAnterior != null) btnAnterior.gameObject.SetActive(varias);
            if (btnProximo != null) btnProximo.gameObject.SetActive(varias);

            if (containerIndicadores == null)
                return;

            int k = 0;
            foreach (Transform ponto in containerIndicadores)
            {
                bool existe = k < lista.Count;
                if (ponto.gameObject.activeSelf != existe) ponto.gameObject.SetActive(existe);
                var img = ponto.GetComponent<Image>();
                if (existe && img != null) img.color = k == indice ? pontoAtivo : pontoInativo;
                k++;
            }
        }

        private void Pulsar()
        {
            if (miniatura == null) return;
            if (pulso != null) StopCoroutine(pulso);
            pulso = StartCoroutine(Pulsando(miniatura.rectTransform));
        }

        private IEnumerator Pulsando(RectTransform alvo)
        {
            const float duracao = 0.22f;
            for (float t = 0f; t < duracao; t += Time.unscaledDeltaTime)
            {
                float k = UIEase.OutQuad(t / duracao);
                alvo.localScale = Vector3.one * Mathf.LerpUnclamped(0.92f, 1f, UIEase.OutBack(k, 1.4f));
                yield return null;
            }
            alvo.localScale = Vector3.one;
            pulso = null;
        }
    }
}
