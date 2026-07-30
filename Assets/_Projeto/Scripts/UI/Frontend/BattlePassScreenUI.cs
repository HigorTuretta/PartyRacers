using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Settings;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 09. Trilha horizontal de 40 níveis em duas faixas (premium e grátis),
    /// com os 4 estados de recompensa como objetos irmãos. A trilha abre no nível atual.
    /// </summary>
    [DisallowMultipleComponent]
    public class BattlePassScreenUI : MonoBehaviour
    {
        [Header("Prefab de recompensa (Prefabs/UI/Items)")]
        [SerializeField] private GameObject prefabTier;

        [Header("Containers já montados (com Layout Group)")]
        [SerializeField] private Transform faixaPremium;
        [SerializeField] private Transform faixaGratis;
        [SerializeField] private Transform cabecalhoNiveis;

        [Header("Catálogo")]
        [SerializeField] private List<PassTierDefinition> recompensas = new List<PassTierDefinition>();

        [Header("Progresso")]
        [SerializeField] private int nivelAtual = 12;
        [SerializeField] private int xpAtual = 640;
        [SerializeField] private int xpDoNivel = 1000;
        [SerializeField] private bool temPassePremium;
        [SerializeField] private List<int> niveisResgatados = new List<int>();

        [Header("Cabeçalho")]
        [SerializeField] private TextMeshProUGUI textoNivel;
        [SerializeField] private TextMeshProUGUI textoProgresso;
        [SerializeField] private TextMeshProUGUI rotuloProgresso;
        [SerializeField] private Image barraProgresso;

        [Header("Janela visível da trilha")]
        [Tooltip("Quantos níveis aparecem de uma vez (o PLACA mostra 6).")]
        [SerializeField] private int niveisVisiveis = 6;

        private readonly List<GameObject> instanciados = new List<GameObject>();

        private void OnEnable() => Redesenhar();

        public void Redesenhar()
        {
            // ver StoreScreenUI: Destroy é adiado, então tira do pai antes de destruir
            foreach (GameObject go in instanciados)
            {
                if (go == null) continue;
                go.transform.SetParent(null, false);
                Destroy(go);
            }
            instanciados.Clear();

            if (textoNivel != null) textoNivel.text = nivelAtual.ToString();
            if (rotuloProgresso != null) rotuloProgresso.text = $"PROGRESSO PARA O NÍVEL {nivelAtual + 1}";
            if (textoProgresso != null) textoProgresso.text = $"{xpAtual:N0} / {xpDoNivel:N0} XP";
            if (barraProgresso != null)
                barraProgresso.fillAmount = xpDoNivel > 0 ? Mathf.Clamp01((float)xpAtual / xpDoNivel) : 0f;

            // a trilha abre centrada no nível atual
            int primeiro = Mathf.Max(1, nivelAtual - niveisVisiveis / 2);

            for (int i = 0; i < niveisVisiveis; i++)
            {
                int nivel = primeiro + i;
                MontarCabecalho(nivel);
                MontarTier(nivel, premium: true);
                MontarTier(nivel, premium: false);
            }
        }

        private void MontarCabecalho(int nivel)
        {
            if (cabecalhoNiveis == null || cabecalhoNiveis.childCount == 0)
                return;

            // os cabeçalhos já existem na cena (um por coluna); só reescrevemos o texto
            int indice = nivel - Mathf.Max(1, nivelAtual - niveisVisiveis / 2);
            // +1 porque o primeiro filho é o espaçador da coluna de rótulos
            int filho = indice + 1;
            if (filho < 0 || filho >= cabecalhoNiveis.childCount)
                return;

            Transform col = cabecalhoNiveis.GetChild(filho);
            bool atual = nivel == nivelAtual;

            // qual coluna é a "sua" muda com o nível do jogador: liga o estado certo,
            // não repinta o fundo
            Transform idle = col.Find("State_Idle");
            Transform ativo = col.Find("State_Active");
            if (idle != null) idle.gameObject.SetActive(!atual);
            if (ativo != null) ativo.gameObject.SetActive(atual);

            Transform alvo = atual ? ativo : idle;
            var t = (alvo != null ? alvo : col).GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
                t.text = atual ? $"NÍVEL {nivel} · VOCÊ" : $"NÍVEL {nivel}";
        }

        private void MontarTier(int nivel, bool premium)
        {
            Transform faixa = premium ? faixaPremium : faixaGratis;
            if (faixa == null || prefabTier == null)
                return;

            PassTierDefinition def = recompensas.Find(r => r != null && r.nivel == nivel && r.premium == premium);

            GameObject go = Instantiate(prefabTier, faixa);
            instanciados.Add(go);

            bool resgatada = niveisResgatados.Contains(nivel) && nivel < nivelAtual;
            bool bloqueadaPorPasse = premium && !temPassePremium;
            bool bloqueadaPorNivel = nivel > nivelAtual;
            bool disponivel = !resgatada && !bloqueadaPorPasse && !bloqueadaPorNivel;

            string estado = resgatada ? "State_Claimed"
                          : bloqueadaPorPasse ? "State_LockedPass"
                          : bloqueadaPorNivel ? "State_LockedLevel"
                          : "State_Available";

            foreach (string e in new[] { "State_Claimed", "State_Available", "State_LockedLevel", "State_LockedPass" })
            {
                Transform st = go.transform.Find(e);
                if (st == null)
                    continue;

                st.gameObject.SetActive(e == estado);
                if (e != estado)
                    continue;

                var nome = st.Find("Nome")?.GetComponent<TextMeshProUGUI>();
                if (nome != null) nome.text = def != null ? def.nomeExibido : "—";

                var arte = st.Find("Preview/Arte")?.GetComponent<Image>();
                if (arte != null && def != null && def.arte != null) { arte.sprite = def.arte; arte.enabled = true; }
            }

            if (disponivel)
            {
                var btn = go.transform.Find("State_Available/Btn_Resgatar")?.GetComponent<Button>();
                int capturado = nivel;
                if (btn != null) btn.onClick.AddListener(() => Resgatar(capturado));
            }
        }

        public void Resgatar(int nivel)
        {
            if (niveisResgatados.Contains(nivel))
                return;

            niveisResgatados.Add(nivel);
            Redesenhar();
        }
    }
}
