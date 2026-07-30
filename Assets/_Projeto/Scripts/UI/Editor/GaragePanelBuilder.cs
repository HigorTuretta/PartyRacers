#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Remonta o Screen_Garage_PC para o conteúdo real do jogo:
    /// tira o painel de lobby (decisão do projeto: a sala vive só na aba LOBBY),
    /// deixa a lista de customização com as 11 categorias reais e rolagem,
    /// e o seletor de carro com um indicador por carro do pack (15).
    ///
    /// É um construtor de edição: monta objetos de cena de verdade, editáveis no Inspector.
    /// Nada aqui roda em runtime.
    /// </summary>
    public static class GaragePanelBuilder
    {
        const string PREFAB = "Assets/_Projeto/Prefabs/UI/Screens/Screen_Garage_PC.prefab";

        /// <summary>Categorias na ordem do mockup: rótulo exibido + id lógico.</summary>
        public static readonly (string rotulo, string id)[] Categorias =
        {
            ("COR",       "Cor"),
            ("RODAS",     "Wheel"),
            ("FRENTE",    "FrontBumper"),
            ("TRASEIRA",  "RearBumper"),
            ("ESCAPE",    "Pipe"),
            ("ADESIVOS",  "Decals"),
            ("FARÓIS",    "Headlight"),
            ("MILHA",     "FogLight"),
            ("MOTOR",     "Engine"),
            ("AEROFÓLIO", "Spoiler"),
            ("PILOTO",    "Racer"),
        };

        const int TOTAL_CARROS = 15;
        const float ALTURA_LINHA = 64f;
        const float ESPACO_LINHA = 11f;

        [MenuItem("Party Racers/UI/Remontar garagem")]
        public static void Remontar()
        {
            var raiz = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                TirarPainelLobby(raiz);
                AmpliarPalco(raiz);
                MontarListaComRolagem(raiz);
                MontarIndicadores(raiz);
                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }

            AssetDatabase.SaveAssets();
            Debug.Log("[garagem] remontada: lobby removido, " + Categorias.Length +
                      " categorias com rolagem, " + TOTAL_CARROS + " indicadores");
        }

        // ---------------------------------------------------------------- lobby fora
        static void TirarPainelLobby(GameObject raiz)
        {
            var p = raiz.transform.Find("PainelLobby");
            if (p != null) Object.DestroyImmediate(p.gameObject);
        }

        /// <summary>Sem o painel da direita o carro ganha o centro da tela.</summary>
        static void AmpliarPalco(GameObject raiz)
        {
            var palco = raiz.transform.Find("PalcoCarro") as RectTransform;
            if (palco == null) return;
            palco.anchorMin = palco.anchorMax = new Vector2(0.5f, 0.5f);
            palco.pivot = new Vector2(0.5f, 0.5f);
            palco.anchoredPosition = new Vector2(90f, 10f);   // desvia do painel da esquerda
            palco.sizeDelta = new Vector2(1000f, 620f);
        }

        // ---------------------------------------------------------------- lista rolável
        static void MontarListaComRolagem(GameObject raiz)
        {
            var painel = raiz.transform.Find("PainelCustomizacao") as RectTransform;
            if (painel == null) { Debug.LogError("[garagem] sem PainelCustomizacao"); return; }

            // reaproveita a linha existente como modelo (traz todos os estados já estilizados)
            var listaAntiga = painel.Find("Categorias") as RectTransform;
            var rolagemAntiga = painel.Find("Rolagem") as RectTransform;
            var conteudoAntigo = rolagemAntiga != null
                ? rolagemAntiga.Find("Viewport/Categorias") as RectTransform
                : null;
            var fonte = conteudoAntigo ?? listaAntiga;
            if (fonte == null || fonte.childCount == 0) { Debug.LogError("[garagem] sem linha modelo"); return; }

            var modelo = fonte.GetChild(0).gameObject;
            var modeloCopia = Object.Instantiate(modelo);
            modeloCopia.name = "__modelo";

            // estrutura: Rolagem > Viewport > Categorias, + Barra
            if (rolagemAntiga != null) Object.DestroyImmediate(rolagemAntiga.gameObject);
            if (listaAntiga != null) Object.DestroyImmediate(listaAntiga.gameObject);

            var rolagem = Novo("Rolagem", painel);
            Ancorar(rolagem, new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0f, -78f), new Vector2(-52f, 460f), new Vector2(0.5f, 1f));

            var viewport = Novo("Viewport", rolagem);
            Ancorar(viewport, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-20f, 0f), new Vector2(0f, 1f));
            viewport.GetComponent<RectTransform>();
            viewport.gameObject.AddComponent<RectMask2D>();

            var conteudo = Novo("Categorias", viewport);
            Ancorar(conteudo, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 0f), new Vector2(0.5f, 1f));
            var vlg = conteudo.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = ESPACO_LINHA;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            var fitter = conteudo.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            foreach (var (rotulo, id) in Categorias)
            {
                var linha = Object.Instantiate(modeloCopia, conteudo);
                linha.name = "Cat_" + id;
                linha.SetActive(true);
                var le = linha.GetComponent<LayoutElement>() ?? linha.AddComponent<LayoutElement>();
                le.minHeight = le.preferredHeight = ALTURA_LINHA;
                var nome = linha.transform.Find("Nome")?.GetComponent<TextMeshProUGUI>();
                if (nome != null) nome.text = rotulo;
            }
            Object.DestroyImmediate(modeloCopia);

            // barra de rolagem fina, como no mockup (trilho escuro + alça âmbar)
            var barra = Novo("Barra", rolagem);
            Ancorar(barra, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, 0f),
                    new Vector2(9f, -8f), new Vector2(1f, 0.5f));
            var trilho = barra.gameObject.AddComponent<Image>();
            trilho.sprite = Sprite("UI_Card_R18_Deep");
            trilho.type = Image.Type.Sliced;
            trilho.color = Cor("#1B2050");
            var sb = barra.gameObject.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;

            var areaAlca = Novo("SlidingArea", barra);
            Ancorar(areaAlca, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var alca = Novo("Handle", areaAlca);
            Ancorar(alca, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var imgAlca = alca.gameObject.AddComponent<Image>();
            imgAlca.sprite = Sprite("UI_Badge_R14_Amber");
            imgAlca.type = Image.Type.Sliced;
            sb.targetGraphic = imgAlca;
            sb.handleRect = alca;

            var scroll = rolagem.gameObject.AddComponent<ScrollRect>();
            scroll.content = conteudo;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 30f;
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        }

        // ---------------------------------------------------------------- indicadores
        static void MontarIndicadores(GameObject raiz)
        {
            var cont = raiz.transform.Find("SeletorCarro/Indicadores") as RectTransform;
            if (cont == null) { Debug.LogError("[garagem] sem Indicadores"); return; }
            if (cont.childCount == 0) { Debug.LogError("[garagem] sem ponto modelo"); return; }

            var modelo = Object.Instantiate(cont.GetChild(0).gameObject);
            modelo.name = "__ponto";

            for (int i = cont.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(cont.GetChild(i).gameObject);

            // 15 pontos precisam caber entre as setas: 22 de largura com 8 de espaço
            var hlg = cont.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) { hlg.spacing = 8f; hlg.childForceExpandWidth = false; hlg.childAlignment = TextAnchor.MiddleCenter; }
            cont.sizeDelta = new Vector2(TOTAL_CARROS * 22f + (TOTAL_CARROS - 1) * 8f, 12f);

            for (int i = 0; i < TOTAL_CARROS; i++)
            {
                var p = Object.Instantiate(modelo, cont);
                p.name = "Ponto_" + (i + 1).ToString("00");
                p.SetActive(true);
                var le = p.GetComponent<LayoutElement>() ?? p.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 22f;
                le.preferredHeight = le.minHeight = 8f;
            }
            Object.DestroyImmediate(modelo);
        }

        // ---------------------------------------------------------------- utilidades
        static RectTransform Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return (RectTransform)go.transform;
        }

        static void Ancorar(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, Vector2 pivot)
        {
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static Sprite Sprite(string nome)
        {
            var guid = AssetDatabase.FindAssets(nome + " t:Sprite", new[] { "Assets/_Projeto/Art/UI" }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static Color Cor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
#endif
