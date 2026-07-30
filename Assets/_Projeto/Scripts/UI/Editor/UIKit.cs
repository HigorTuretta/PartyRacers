using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Ferramenta de EDITOR para montar prefabs/telas da direção PLACA.
    /// Roda só no editor e produz GameObjects reais, editáveis no Inspector.
    /// Nada disto é usado em runtime — os binders só leem referências já montadas.
    /// </summary>
    public static class UIKit
    {
        public const string ART = "Assets/_Projeto/Art/UI";
        public const string FONTS = "Assets/_Projeto/Art/Fonts";

        // ---------- assets ----------
        public static Sprite Sprite(string pasta, string nome)
        {
            var p = $"{ART}/{pasta}/{nome}.png";
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(p);
            if (s == null) Debug.LogWarning($"[UIKit] sprite não encontrado: {p}");
            return s;
        }

        // nomes com prefixo p/ não colidir com UnityEngine.Display nem com o namespace Mono
        public static TMP_FontAsset FonteDisplay => Font($"{FONTS}/TitanOne/TitanOne SDF.asset");
        public static TMP_FontAsset FonteUiSemi => Font($"{FONTS}/Archivo/Archivo SemiBold SDF.asset");
        public static TMP_FontAsset FonteUiBold => Font($"{FONTS}/Archivo/Archivo Bold SDF.asset");
        public static TMP_FontAsset FonteUiExtra => Font($"{FONTS}/Archivo/Archivo ExtraBold SDF.asset");
        public static TMP_FontAsset FonteMono => Font($"{FONTS}/SpaceMono/SpaceMono SDF.asset");

        static TMP_FontAsset Font(string p)
        {
            var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
            if (f == null) Debug.LogWarning($"[UIKit] fonte não encontrada: {p}");
            return f;
        }

        public static Color Hex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString(hex, out c) ? c : Color.magenta;
        }

        // paleta PLACA (mesmos valores do tokens.json)
        public static readonly Color Ink = Hex("#0A0C22");
        public static readonly Color Night = Hex("#171A3D");
        public static readonly Color DeepBlue = Hex("#101334");
        public static readonly Color Blue = Hex("#1B2050");
        public static readonly Color Royal = Hex("#2A3480");
        public static readonly Color Amber = Hex("#FFB020");
        public static readonly Color Cream = Hex("#FFF7E8");
        public static readonly Color CreamDim = Hex("#FFE7B8");
        public static readonly Color Green = Hex("#3DDC97");
        public static readonly Color Red = Hex("#FF4D6D");
        public static readonly Color Sky = Hex("#35A7FF");
        public static readonly Color Violet = Hex("#8C7BFF");
        public static readonly Color TextPrimary = Hex("#FFF7E8");
        public static readonly Color TextSecondary = Hex("#C3CEDD");
        public static readonly Color TextMuted = Hex("#9AA2D8");
        public static readonly Color TextDisabled = Hex("#6B6F9E");
        public static readonly Color OutlineSoft = Hex("#3B4180");

        // ---------- construção ----------
        public static GameObject Node(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            if (pai != null) go.transform.SetParent(pai, false);
            return go;
        }

        public static RectTransform RT(GameObject go) => (RectTransform)go.transform;

        /// <summary>Ancora + tamanho num passo. anchor (0..1). pivot idem.</summary>
        public static RectTransform Place(GameObject go, Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 pivot, Vector2 anchoredPos, Vector2 size)
        {
            var rt = RT(go);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Preenche o pai inteiro, com margens (l,b,r,t).</summary>
        public static RectTransform Stretch(GameObject go, float l = 0, float b = 0, float r = 0, float t = 0)
        {
            var rt = RT(go);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(.5f, .5f);
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
            return rt;
        }

        /// <summary>
        /// Escala do 9-slice. A arte do pacote foi desenhada em 2×: medido nos PNGs, o
        /// UI_Card_R18 tem canto de 35px numa textura de 128 (raio 18 em 1×), o UI_Button_R22
        /// tem canto de 42px em 144 (raio 22 em 1×) e o contorno mede 10px de textura — os 5px
        /// que o PLACA especifica. Com multiplicador 1 todo canto e contorno saíam do dobro do
        /// tamanho, e em elementos baixos (linha de 46, botão de 80) as bordas somavam mais que
        /// a altura: a Unity então esmagava o 9-slice e tudo virava "PNG esticado".
        /// </summary>
        public const float EscalaNoveFatias = 2f;

        public static Image Img(string nome, Transform pai, Sprite sprite, Color cor,
                                Image.Type tipo = Image.Type.Sliced)
        {
            var go = Node(nome, pai);
            var img = go.AddComponent<Image>();
            img.sprite = sprite; img.color = cor; img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = tipo;
                img.pixelsPerUnitMultiplier = EscalaNoveFatias;
            }
            else img.type = Image.Type.Simple;
            return img;
        }

        public static TextMeshProUGUI Txt(string nome, Transform pai, string texto, TMP_FontAsset fonte,
                                          float tamanho, Color cor,
                                          TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = Node(nome, pai);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = texto; t.font = fonte; t.fontSize = tamanho; t.color = cor;
            t.alignment = align; t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        /// <summary>Botão com troca de sprite no pressionar (SpriteSwap, sem mexer no RectTransform).</summary>
        public static Button Botao(Image alvo, Sprite pressionado, Sprite desabilitado = null)
        {
            var b = alvo.gameObject.AddComponent<Button>();
            alvo.raycastTarget = true;
            b.targetGraphic = alvo;
            if (pressionado != null)
            {
                b.transition = Selectable.Transition.SpriteSwap;
                var st = new SpriteState
                {
                    pressedSprite = pressionado,
                    selectedSprite = alvo.sprite,
                    highlightedSprite = alvo.sprite,
                    disabledSprite = desabilitado != null ? desabilitado : alvo.sprite
                };
                b.spriteState = st;
            }
            else
            {
                b.transition = Selectable.Transition.ColorTint;
                var cb = b.colors;
                cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
                cb.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
                cb.disabledColor = new Color(1f, 1f, 1f, 0.4f);
                cb.fadeDuration = 0.08f;
                b.colors = cb;
            }
            return b;
        }

        public static VerticalLayoutGroup VLayout(GameObject go, float espaco, RectOffset padding,
                                                  TextAnchor align = TextAnchor.UpperCenter,
                                                  bool expandW = true, bool expandH = false)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = espaco; v.padding = padding ?? new RectOffset();
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = expandW; v.childForceExpandHeight = expandH;
            return v;
        }

        public static HorizontalLayoutGroup HLayout(GameObject go, float espaco, RectOffset padding,
                                                    TextAnchor align = TextAnchor.MiddleLeft,
                                                    bool expandW = false, bool expandH = true)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = espaco; h.padding = padding ?? new RectOffset();
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = expandW; h.childForceExpandHeight = expandH;
            return h;
        }

        public static LayoutElement Size(GameObject go, float w = -1, float h = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            return le;
        }

        /// <summary>Sombra dura da direção PLACA: cópia do sprite deslocada para baixo, atrás.</summary>
        public static Image SombraDura(Transform pai, Sprite sprite, float offsetY)
        {
            var s = Img("Sombra", pai, sprite, Ink);
            Stretch(s.gameObject);
            RT(s.gameObject).anchoredPosition = new Vector2(0, -offsetY);
            s.transform.SetAsFirstSibling();
            return s;
        }

        // ---------- prefab ----------
        public static GameObject SalvarPrefab(GameObject go, string caminho)
        {
            var dir = System.IO.Path.GetDirectoryName(caminho).Replace('\\', '/');
            GarantirPasta(dir);
            var p = PrefabUtility.SaveAsPrefabAsset(go, caminho);
            Object.DestroyImmediate(go);
            return p;
        }

        public static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho)) return;
            var partes = caminho.Split('/');
            var atual = partes[0];
            for (int i = 1; i < partes.Length; i++)
            {
                var prox = atual + "/" + partes[i];
                if (!AssetDatabase.IsValidFolder(prox)) AssetDatabase.CreateFolder(atual, partes[i]);
                atual = prox;
            }
        }
    }
}
