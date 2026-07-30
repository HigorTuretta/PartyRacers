using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Padrões visuais que a direção PLACA repete em todas as telas.
    /// Ferramenta de EDITOR — produz GameObjects reais, editáveis no Inspector.
    /// </summary>
    public static class UIKitPlaca
    {
        public const string WIDGETS = "Assets/_Projeto/Prefabs/UI/Widgets";
        public const string ITEMS = "Assets/_Projeto/Prefabs/UI/Items";
        public const string SCREENS = "Assets/_Projeto/Prefabs/UI/Screens";

        // cores extras do PLACA que não estão no UIKit base
        public static readonly Color Prata = Hex("#D7DEEA");
        public static readonly Color Bronze = Hex("#C57C3C");
        public static readonly Color VermelhoFundo = Hex("#4A1D33");
        public static readonly Color AmberFundo = Hex("#4A3A12");
        public static readonly Color AzulFundo = Hex("#171F45");
        public static readonly Color Slate = Hex("#4B54A8");
        public static readonly Color Lavanda = Hex("#7C86C8");
        public static readonly Color AzulClaro = Hex("#9FDCF2");
        public static readonly Color RosaClaro = Hex("#FFC9CB");

        // ---------- instanciar widget mantendo o vínculo com o prefab ----------
        public static GameObject Widget(string nome, Transform pai)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{WIDGETS}/{nome}.prefab");
            if (src == null) { Debug.LogWarning($"[PLACA] widget não encontrado: {nome}"); return Node(nome, pai); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, pai);
            return go;
        }

        public static GameObject Item(string nome, Transform pai)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{ITEMS}/{nome}.prefab");
            if (src == null) { Debug.LogWarning($"[PLACA] item não encontrado: {nome}"); return Node(nome, pai); }
            return (GameObject)PrefabUtility.InstantiatePrefab(src, pai);
        }

        // ---------- superfícies ----------
        /// <summary>Card R18. `cor` = variante do sprite (Deep/Ink/Cream/Royal).</summary>
        public static Image Card(string nome, Transform pai, string variante = "Deep", float sombra = 0f)
        {
            var img = Img(nome, pai, Sprite("Frames", $"UI_Card_R18_{variante}"), Color.white);
            if (sombra > 0f) SombraDura(img.transform, img.sprite, sombra);
            return img;
        }

        public static Image Painel(string nome, Transform pai, string variante = "Deep", float sombra = 9f)
        {
            var img = Img(nome, pai, Sprite("Frames", $"UI_Panel_R26_{variante}"), Color.white);
            if (sombra > 0f) SombraDura(img.transform, img.sprite, sombra);
            return img;
        }

        public static Image Modal(string nome, Transform pai, float sombra = 12f)
        {
            var img = Img(nome, pai, Sprite("Frames", "UI_Modal_R36"), Color.white);
            if (sombra > 0f) SombraDura(img.transform, img.sprite, sombra);
            return img;
        }

        public static Image Tracejado(string nome, Transform pai)
            => Img(nome, pai, Sprite("Frames", "UI_Dashed_R18"), OutlineSoft);

        /// <summary>
        /// Contorno colorido em volta de um card (o PLACA usa muito "corpo escuro + borda âmbar").
        /// O sprite de moldura já traz o contorno #0A0C22 embutido e não pode ser tingido junto,
        /// então a borda é um card irmão atrás, maior, tingido na cor pedida.
        /// ATENÇÃO: `pai` não pode ter Image própria — o Image do nó desenha antes dos filhos,
        /// e o contorno acabaria por cima do corpo. Use um nó vazio + Contorno + Bg.
        /// </summary>
        public static Image Contorno(Transform pai, Color cor, float espessura = 5f, string variante = "Cream")
        {
            var b = Img("Contorno", pai, Sprite("Frames", $"UI_Card_R18_{variante}"), cor);
            Stretch(b.gameObject, -espessura, -espessura, -espessura, -espessura);
            b.transform.SetAsFirstSibling();
            return b;
        }

        // ---------- textos prontos ----------
        public static TextMeshProUGUI Display(string nome, Transform pai, string txt, float tam, Color cor,
                                              TextAlignmentOptions al = TextAlignmentOptions.Center)
            => Txt(nome, pai, txt, FonteDisplay, tam, cor, al);

        public static TextMeshProUGUI Rotulo(string nome, Transform pai, string txt, float tam, Color cor,
                                             TextAlignmentOptions al = TextAlignmentOptions.Left, float espaco = 0f)
        {
            var t = Txt(nome, pai, txt, FonteUiExtra, tam, cor, al);
            if (espaco != 0f) t.characterSpacing = espaco;
            return t;
        }

        public static TextMeshProUGUI Legenda(string nome, Transform pai, string txt, float tam, Color cor,
                                              TextAlignmentOptions al = TextAlignmentOptions.Left, float espaco = 14f)
        {
            var t = Txt(nome, pai, txt, FonteUiBold, tam, cor, al);
            t.characterSpacing = espaco;
            return t;
        }

        /// <summary>
        /// Número tabular (tempos, contadores, moedas). O PLACA usa Archivo com tabular-nums nesses
        /// lugares — Space Mono fica só para legenda técnica (ver <see cref="Anotacao"/>).
        /// </summary>
        public static TextMeshProUGUI Numero(string nome, Transform pai, string txt, float tam, Color cor,
                                             TextAlignmentOptions al = TextAlignmentOptions.Right)
            => Txt(nome, pai, txt, FonteUiExtra, tam, cor, al);

        /// <summary>Legenda técnica em Space Mono (anotações, ID de perfil, textos de apoio).</summary>
        public static TextMeshProUGUI Anotacao(string nome, Transform pai, string txt, float tam, Color cor,
                                               TextAlignmentOptions al = TextAlignmentOptions.Left, float espaco = 0f)
        {
            var t = Txt(nome, pai, txt, FonteMono, tam, cor, al);
            if (espaco != 0f) t.characterSpacing = espaco;
            return t;
        }

        // ---------- peças recorrentes ----------
        /// <summary>Badge quadrado rotacionado −3° com número dentro (posição na classificação).</summary>
        public static TextMeshProUGUI BadgePosicao(Transform pai, string numero, float lado, Color fundo, Color texto,
                                                   float tamFonte)
        {
            // Simple: badge quadrado 1:1 escala junto e mantém o raio; com 9-slice viraria círculo.
            var bg = Img("BadgePos", pai, Sprite("Frames", "UI_Badge_R14_Cream"), fundo, Image.Type.Simple);
            bg.type = Image.Type.Simple;
            Place(bg.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(18, 0), new Vector2(lado, lado));
            bg.transform.localRotation = Quaternion.Euler(0, 0, -3f);
            var t = Rotulo("Valor", bg.transform, numero, tamFonte, texto, TextAlignmentOptions.Center);
            Stretch(t.gameObject, 2, 2, 2, 2);
            return t;
        }

        /// <summary>Chip pequeno de informação: fundo + texto, largura fixa.</summary>
        public static GameObject Chip(string nome, Transform pai, string texto, float w, float h,
                                      Color fundo, Color corTexto, float fonte, TMP_FontAsset f = null)
        {
            var go = Node(nome, pai);
            var bg = go.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Badge_R14_Cream");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            bg.color = fundo; bg.raycastTarget = false;
            Place(go, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(w, h));
            var t = Txt("Label", go.transform, texto, f ?? FonteUiExtra, fonte, corTexto);
            Stretch(t.gameObject, 12, 4, 12, 4);
            return go;
        }

        /// <summary>Barra horizontal (progresso, recarga). Devolve o Image do preenchimento (type Filled).</summary>
        public static Image Barra(string nome, Transform pai, float w, float h, Color corFill, float valor = .6f)
        {
            var trilha = Img(nome, pai, Sprite("Bars", "Bar_Track"), Color.white);
            Place(trilha.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(w, h));
            var fill = Img("Fill", trilha.transform, Sprite("Bars", "Bar_Fill"), corFill);
            Stretch(fill.gameObject, 3, 3, 3, 3);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = valor;
            return fill;
        }

        /// <summary>Ícone branco tingido pela cor pedida.</summary>
        public static Image Icone(string nome, Transform pai, string icone, Color cor, float lado)
        {
            var i = Img(nome, pai, Sprite("Icons", icone), cor, Image.Type.Simple);
            Place(i.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(lado, lado));
            return i;
        }

        /// <summary>Lockup PARTY + placa RACERS (não existe PNG do logo — é TMP, como manda o README).</summary>
        public static GameObject Logo(Transform pai, float escala = 1f)
        {
            var go = Node("Logo", pai);
            Place(go, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero,
                  new Vector2(250 * escala, 72 * escala));
            go.transform.localRotation = Quaternion.Euler(0, 0, -2f);

            var party = Display("Party", go.transform, "PARTY", 30 * escala, Cream, TextAlignmentOptions.Left);
            Place(party.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                  Vector2.zero, new Vector2(140 * escala, 34 * escala));

            var placa = Img("Placa", go.transform, Sprite("Brand", "Countdown_Plate"), Amber, Image.Type.Simple);
            Place(placa.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
                  new Vector2(0, -34 * escala), new Vector2(150 * escala, 40 * escala));
            var racers = Display("Racers", placa.transform, "RACERS", 24 * escala, Ink);
            Stretch(racers.gameObject, 8, 4, 8, 6);
            return go;
        }

        /// <summary>Barra de navegação LOBBY/GARAGEM/LOJA/PASSE com Chip_Tab (2 estados irmãos).</summary>
        public static GameObject Nav(Transform pai, int ativo)
        {
            var go = Node("Nav", pai);
            Place(go, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(300, -44), new Vector2(900, 64));
            HLayout(go, 8, new RectOffset(), TextAnchor.MiddleLeft);

            var nomes = new[] { "LOBBY", "GARAGEM", "LOJA", "PASSE" };
            for (int i = 0; i < nomes.Length; i++)
            {
                var chip = Widget("Chip_Tab", go.transform);
                chip.name = "Tab_" + nomes[i];
                Size(chip, 200, 64);
                foreach (var t in chip.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = nomes[i];
                chip.transform.Find("State_Idle").gameObject.SetActive(i != ativo);
                chip.transform.Find("State_Active").gameObject.SetActive(i == ativo);
            }
            return go;
        }

        /// <summary>Carteira do topo-direito: moedas + fichas.</summary>
        public static GameObject Carteira(Transform pai)
        {
            var go = Node("Carteira", pai);
            Place(go, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -44), new Vector2(460, 64));
            HLayout(go, 12, new RectOffset(), TextAnchor.MiddleRight);

            Moeda(go.transform, "Moedas", "Icon_Coin", Amber, "12.480");
            Moeda(go.transform, "Fichas", "Icon_Diamond", Violet, "340");
            return go;
        }

        static void Moeda(Transform pai, string nome, string icone, Color cor, string valor)
        {
            var c = Card(nome, pai, "Ink");
            Size(c.gameObject, 210, 64);
            Icone("Icon", c.transform, icone, cor, 30).rectTransform.anchoredPosition = new Vector2(0, 0);
            Place(c.transform.Find("Icon").gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(20, 0), new Vector2(30, 30));
            var t = Numero("Valor", c.transform, valor, 25, Cream, TextAlignmentOptions.Right);
            var rt = RT(t.gameObject);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(58, 4); rt.offsetMax = new Vector2(-20, -4);
        }

        // ---------- layout ----------
        public static GridLayoutGroup Grid(GameObject go, Vector2 celula, Vector2 espaco, int colunas)
        {
            var g = go.AddComponent<GridLayoutGroup>();
            g.cellSize = celula; g.spacing = espaco;
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = colunas;
            return g;
        }

        public static ContentSizeFitter Fitter(GameObject go, bool w = false, bool h = true)
        {
            var f = go.AddComponent<ContentSizeFitter>();
            f.horizontalFit = w ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            f.verticalFit = h ? ContentSizeFitter.FitMode.PreferredSize : ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        // ---------- raiz de tela ----------
        /// <summary>Raiz de um Screen_*: 1920×1080 esticado, com CanvasGroup para o fade do ScreenRouter.</summary>
        public static GameObject Tela(string nome)
        {
            var go = Node(nome, null);
            Stretch(go);
            go.AddComponent<CanvasGroup>();
            return go;
        }

        /// <summary>Fundo em degradê das telas de frontend (o gameplay não fica atrás).</summary>
        public static Image Fundo(Transform pai, Color cor)
        {
            var img = Img("Fundo", pai, null, cor, Image.Type.Simple);
            Stretch(img.gameObject);
            img.transform.SetAsFirstSibling();
            return img;
        }

        /// <summary>
        /// Fundo transparente para as telas que mostram o palco 3D do carro (Lobby e Garagem).
        /// Existe como objeto para o designer poder ligar uma cor ou um degradê sem criar nada:
        /// basta mexer no alfa deste Image no Inspector.
        /// </summary>
        public static Image FundoVazado(Transform pai)
        {
            var img = Fundo(pai, new Color(0, 0, 0, 0));
            img.raycastTarget = false;   // clique passa direto para o que estiver atrás
            return img;
        }

        public static GameObject SalvarTela(GameObject go, string nome)
        {
            GarantirPasta(SCREENS);
            return SalvarPrefab(go, $"{SCREENS}/{nome}.prefab");
        }

        public static GameObject SalvarItem(GameObject go, string nome)
        {
            GarantirPasta(ITEMS);
            return SalvarPrefab(go, $"{ITEMS}/{nome}.prefab");
        }
    }
}
