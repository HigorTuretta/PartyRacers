using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Settings;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Binder da tela 08. Instancia Item_StoreCard/Item_StoreDaily dentro de containers que já
    /// têm Layout Group na cena e preenche os campos — nenhum card é construído por código.
    /// Os 4 estados do card são objetos irmãos ligados por SetActive.
    /// </summary>
    [DisallowMultipleComponent]
    public class StoreScreenUI : MonoBehaviour
    {
        [Header("Prefabs de item (Prefabs/UI/Items)")]
        [SerializeField] private GameObject prefabCard;
        [SerializeField] private GameObject prefabDiario;

        [Header("Containers já montados (com Layout Group)")]
        [SerializeField] private Transform containerGrade;
        [SerializeField] private Transform containerDiarios;

        [Header("Catálogo (ScriptableObjects)")]
        [SerializeField] private List<StoreItemDefinition> grade = new List<StoreItemDefinition>();
        [SerializeField] private List<StoreItemDefinition> diarios = new List<StoreItemDefinition>();

        [Header("Rotação diária")]
        [Tooltip("Timer vem do servidor; enquanto não há servidor, fica no valor atribuído aqui.")]
        [SerializeField] private TextMeshProUGUI textoTimer;

        [Header("Carteira")]
        [SerializeField] private TextMeshProUGUI textoMoedas;
        [SerializeField] private TextMeshProUGUI textoFichas;

        [Header("Progressão do jogador")]
        [SerializeField] private int nivelDoJogador = 1;

        [Header("Ícones de moeda (atribuídos no Inspector, trocados por item)")]
        [SerializeField] private Sprite iconeMoedas;
        [SerializeField] private Sprite iconeFichas;

        [Header("Cores de raridade (paleta PLACA)")]
        [SerializeField] private Color corComum = new Color(0.60f, 0.63f, 0.85f);
        [SerializeField] private Color corRaro = new Color(0.21f, 0.65f, 1f);
        [SerializeField] private Color corEpico = new Color(0.55f, 0.48f, 1f);
        [SerializeField] private Color corLendario = new Color(1f, 0.69f, 0.13f);

        private readonly List<GameObject> instanciados = new List<GameObject>();

        private void OnEnable() => Redesenhar();

        public void Redesenhar()
        {
            // Destroy só acontece no fim do frame: sem tirar do pai antes, o Layout Group ainda
            // conta os cards velhos e a lista aparece duplicada por um frame.
            foreach (GameObject go in instanciados)
            {
                if (go == null) continue;
                go.transform.SetParent(null, false);
                Destroy(go);
            }
            instanciados.Clear();

            foreach (StoreItemDefinition item in grade)
                MontarCard(item);

            foreach (StoreItemDefinition item in diarios)
                MontarDiario(item);
        }

        public void DefinirCarteira(int moedas, int fichas)
        {
            if (textoMoedas != null) textoMoedas.text = moedas.ToString("N0");
            if (textoFichas != null) textoFichas.text = fichas.ToString("N0");
        }

        public void DefinirTempoDeRotacao(string texto)
        {
            if (textoTimer != null) textoTimer.text = texto;
        }

        private void MontarCard(StoreItemDefinition item)
        {
            if (item == null || prefabCard == null || containerGrade == null)
                return;

            GameObject go = Instantiate(prefabCard, containerGrade);
            instanciados.Add(go);

            Escrever(go, "Nome", item.nomeExibido);
            var rar = Achar<TextMeshProUGUI>(go, "Raridade");
            if (rar != null) { rar.text = TextoRaridade(item.raridade); rar.color = CorRaridade(item.raridade); }

            var faixa = Achar<Image>(go, "Preview/FaixaRaridade");
            if (faixa != null) faixa.color = CorRaridade(item.raridade);

            var arte = Achar<Image>(go, "Preview/Arte");
            if (arte != null && item.arte != null) { arte.sprite = item.arte; arte.enabled = true; }

            bool bloqueado = item.nivelMinimo > nivelDoJogador;
            Ligar(go, "State_Buy", !item.jaAdquirido && !bloqueado);
            Ligar(go, "State_Owned", item.jaAdquirido);
            Ligar(go, "State_Locked", !item.jaAdquirido && bloqueado);

            Escrever(go, "State_Buy/Preco", item.preco.ToString("N0"));
            Escrever(go, "State_Locked/Label", "NÍVEL " + item.nivelMinimo);
            TrocarIconeDaMoeda(go, "State_Buy/Icon", item.moeda);
        }

        private void MontarDiario(StoreItemDefinition item)
        {
            if (item == null || prefabDiario == null || containerDiarios == null)
                return;

            GameObject go = Instantiate(prefabDiario, containerDiarios);
            instanciados.Add(go);

            Escrever(go, "Nome", item.nomeExibido);
            var rar = Achar<TextMeshProUGUI>(go, "Raridade");
            if (rar != null) { rar.text = TextoRaridade(item.raridade); rar.color = CorRaridade(item.raridade); }

            var arte = Achar<Image>(go, "Preview/Arte");
            if (arte != null && item.arte != null) { arte.sprite = item.arte; arte.enabled = true; }

            Escrever(go, "Btn_Comprar/Preco", item.preco.ToString("N0"));
            TrocarIconeDaMoeda(go, "Btn_Comprar/Icon", item.moeda);
        }

        /// <summary>Troca entre os dois sprites já atribuídos — nada é criado nem pintado.</summary>
        private void TrocarIconeDaMoeda(GameObject raiz, string caminho, Moeda moeda)
        {
            Sprite sprite = moeda == Moeda.Fichas ? iconeFichas : iconeMoedas;
            if (sprite == null)
                return;

            var img = Achar<Image>(raiz, caminho);
            if (img != null)
                img.sprite = sprite;
        }

        private string TextoRaridade(Raridade r) => r switch
        {
            Raridade.Raro => "RARO",
            Raridade.Epico => "ÉPICO",
            Raridade.Lendario => "LENDÁRIO",
            _ => "COMUM"
        };

        private Color CorRaridade(Raridade r) => r switch
        {
            Raridade.Raro => corRaro,
            Raridade.Epico => corEpico,
            Raridade.Lendario => corLendario,
            _ => corComum
        };

        private static T Achar<T>(GameObject raiz, string caminho) where T : Component
        {
            Transform t = raiz.transform.Find(caminho);
            return t != null ? t.GetComponent<T>() : null;
        }

        private static void Escrever(GameObject raiz, string caminho, string texto)
        {
            var t = Achar<TextMeshProUGUI>(raiz, caminho);
            if (t != null) t.text = texto;
        }

        private static void Ligar(GameObject raiz, string caminho, bool ativo)
        {
            Transform t = raiz.transform.Find(caminho);
            if (t != null && t.gameObject.activeSelf != ativo)
                t.gameObject.SetActive(ativo);
        }
    }
}
