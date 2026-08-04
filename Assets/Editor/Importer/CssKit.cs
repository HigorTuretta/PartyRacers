using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Traduz as primitivas do CSS do protótipo para UGUI. Existe para que o construtor das telas
    /// possa ser lido lado a lado com o `Party Racers v2.dc.html` — cada chamada aqui corresponde a
    /// uma declaração de lá.
    ///
    /// Três coisas que o CSS tem e o UGUI não, resolvidas aqui:
    /// • <b>border + background</b> — no CSS é uma declaração; aqui são duas Images, a de trás
    ///   maior e escura. É o único jeito de ter contorno de espessura exata numa cor arbitrária.
    /// • <b>box-shadow 0 Npx 0</b> — Image irmã ANTERIOR deslocada em Y. Não o componente Shadow:
    ///   o caráter PLACA depende de a sombra ser dura e ter a mesma silhueta.
    /// • <b>margem transparente dos sprites</b> — os PNG do pacote têm 6 px vazios em volta e 3 px
    ///   de contorno já pintados. Um rect de 24 px pintaria uma barra de 12. Todo helper daqui
    ///   recebe a caixa do CSS e infla o rect sozinho.
    /// </summary>
    public static class CssKit
    {
        /// <summary>Margem transparente embutida em todo sprite de moldura do pacote.</summary>
        public const float MargemDoSprite = 6f;

        public enum Ancora { TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight, Center, Stretch }

        // ------------------------------------------------------------------ Cores dos tokens

        public static readonly Color Ink = Hex("#0A0C22");
        public static readonly Color Cream = Hex("#FFF7E8");
        public static readonly Color Amber = Hex("#FFB020");
        public static readonly Color Green = Hex("#3DDC97");
        public static readonly Color Red = Hex("#FF4D6D");
        public static readonly Color Sky = Hex("#35A7FF");
        public static readonly Color SkyLight = Hex("#9BE0FF");
        public static readonly Color Muted = Hex("#9AA2D8");

        public static Color Hex(string s) => ColorUtility.TryParseHtmlString(s, out Color c) ? c : Color.magenta;

        /// <summary>`rgba(r,g,b,a)` do CSS, com r/g/b em 0..255 e a em 0..1.</summary>
        public static Color Rgba(int r, int g, int b, float a) => new Color(r / 255f, g / 255f, b / 255f, a);

        // ------------------------------------------------------------------ Sprites

        private static Sprite neutro;

        /// <summary>Raio desenhado no <c>Rect_R32</c>, que também é a borda 9-slice dele.</summary>
        private const float RaioDoNeutro = 32f;

        /// <summary>
        /// Retângulo arredondado branco de raio CONHECIDO. Usado onde a cor precisa ser exata —
        /// tingir um sprite colorido nunca alcança outra matiz, porque Image.color multiplica.
        ///
        /// Não é o UISprite built-in: o canto dele tem tamanho desconhecido, então todo
        /// <c>pixelsPerUnitMultiplier</c> virava chute e os blocos das barras saíam com cara de
        /// pílula. Com a borda documentada em 32, o raio final é <c>32 / multiplicador</c> — exato.
        /// </summary>
        public static Sprite Neutro()
        {
            if (neutro == null)
            {
                neutro = LayoutResources.Sprite("Frames/Rect_R32");

                if (neutro == null)
                    neutro = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            }

            return neutro;
        }

        public static Sprite Sprite(string chave) => LayoutResources.Sprite(chave);

        // ------------------------------------------------------------------ Nós

        public static RectTransform No(Transform pai, string nome)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Posiciona pela CAIXA do CSS: x/y são left/top (ou right/bottom) e w/h o border-box.</summary>
        public static RectTransform Caixa(Transform pai, string nome, Ancora a,
                                          float x, float y, float w, float h)
        {
            RectTransform r = No(pai, nome);
            Ancorar(r, a, x, y, w, h);
            return r;
        }

        public static void Ancorar(RectTransform r, Ancora a, float x, float y, float w, float h)
        {
            switch (a)
            {
                case Ancora.TopLeft:
                    r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                    r.pivot = new Vector2(0f, 1f);
                    r.anchoredPosition = new Vector2(x, -y);
                    break;
                case Ancora.TopCenter:
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
                    r.pivot = new Vector2(0.5f, 1f);
                    r.anchoredPosition = new Vector2(x, -y);
                    break;
                case Ancora.TopRight:
                    r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
                    r.pivot = new Vector2(1f, 1f);
                    r.anchoredPosition = new Vector2(-x, -y);
                    break;
                case Ancora.BottomLeft:
                    r.anchorMin = r.anchorMax = new Vector2(0f, 0f);
                    r.pivot = new Vector2(0f, 0f);
                    r.anchoredPosition = new Vector2(x, y);
                    break;
                case Ancora.BottomCenter:
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
                    r.pivot = new Vector2(0.5f, 0f);
                    r.anchoredPosition = new Vector2(x, y);
                    break;
                case Ancora.BottomRight:
                    r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
                    r.pivot = new Vector2(1f, 0f);
                    r.anchoredPosition = new Vector2(-x, y);
                    break;
                case Ancora.Center:
                    r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.anchoredPosition = new Vector2(x, y);
                    break;
                case Ancora.Stretch:
                    r.anchorMin = Vector2.zero;
                    r.anchorMax = Vector2.one;
                    r.pivot = new Vector2(0.5f, 0.5f);
                    r.offsetMin = Vector2.zero;
                    r.offsetMax = Vector2.zero;
                    return;
            }

            r.sizeDelta = new Vector2(w, h);
        }

        public static RectTransform Esticar(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        // ------------------------------------------------------------------ Molduras

        /// <summary>
        /// Elemento com sprite autoral do pacote. O rect é inflado em 12 px (6 de margem por lado)
        /// para que a área PINTADA case com a caixa do CSS.
        /// </summary>
        public static RectTransform Moldura(Transform pai, string nome, string chaveDoSprite, Ancora a,
                                            float x, float y, float w, float h, float sombra = 0f)
        {
            float m = MargemDoSprite;
            RectTransform r = Caixa(pai, nome, a, x - m * SinalX(a), y - m * SinalY(a),
                                    w + m * 2f, h + m * 2f);

            // Sombra como FILHO esticado, deslocado em Y — nunca irmã (ver PintarDentro).
            if (sombra > 0f)
            {
                RectTransform s = Esticar(No(r, "Shadow"));
                s.offsetMin = new Vector2(0f, -sombra);
                s.offsetMax = new Vector2(0f, -sombra);
                var sombraImg = s.gameObject.AddComponent<Image>();
                sombraImg.sprite = Sprite(chaveDoSprite);
                sombraImg.type = Image.Type.Sliced;
                sombraImg.pixelsPerUnitMultiplier = 1f;
                sombraImg.color = Ink;
                sombraImg.raycastTarget = false;
            }

            RectTransform f = Esticar(No(r, "Fill"));
            var img = f.gameObject.AddComponent<Image>();
            img.sprite = Sprite(chaveDoSprite);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = false;
            return r;
        }

        /// <summary>
        /// Retângulo arredondado de cor EXATA, com contorno e sombra dura opcionais. Para tudo que
        /// o CSS pede numa cor que nenhum sprite autoral alcança (chips ciano do escudo, blocos das
        /// barras, faixas da classificação).
        ///
        /// A caixa devolvida é um contêiner SEM Graphic próprio, com três filhos:
        /// <c>Shadow</c> → <c>Border</c> → <c>Fill</c>, cada um esticado e transbordando por
        /// offsets negativos. Filhos, não irmãos: irmão não acompanha o <c>SetActive</c> do
        /// elemento (deixava sombras órfãs na tela) e ainda é contado pelos Layout Groups, o que
        /// estragaria a altura das linhas que crescem conforme o estado.
        /// </summary>
        public static RectTransform Pintado(Transform pai, string nome, Color cor, Ancora a,
                                            float x, float y, float w, float h,
                                            float contorno = 0f, Color? corDoContorno = null,
                                            float sombra = 0f, float raio = 0f)
        {
            RectTransform r = Caixa(pai, nome, a, x, y, w, h);
            PintarDentro(r, cor, contorno, corDoContorno, sombra, raio);
            return r;
        }

        /// <summary>Mesma pintura, num rect que já existe (item de layout, por exemplo).</summary>
        public static void PintarDentro(RectTransform r, Color cor, float contorno = 0f,
                                        Color? corDoContorno = null, float sombra = 0f, float raio = 0f)
        {
            if (sombra > 0f)
            {
                RectTransform s = Esticar(No(r, "Shadow"));
                s.offsetMin = new Vector2(0f, -sombra);
                s.offsetMax = new Vector2(0f, -sombra);
                Preencher(s, Ink, raio);
            }

            if (contorno > 0f)
            {
                RectTransform b = Esticar(No(r, "Border"), -contorno);
                Preencher(b, corDoContorno ?? Ink, raio + contorno);
            }

            Preencher(Esticar(No(r, "Fill")), cor, raio);
        }

        /// <summary>
        /// `border-radius: Npx` → multiplicador do 9-slice.
        ///
        /// O <c>pixelsPerUnitMultiplier</c> DIVIDE a borda do sprite: valor maior = canto menor.
        /// Usar <c>raio/32</c> (o inverso) fazia um bloco de 12 px de altura pedir um canto maior
        /// que ele — foi o que deixou todas as barras e chips com cara de borracha.
        /// </summary>
        public static float Raio(float raio) =>
            raio > 0f ? Mathf.Clamp(RaioDoNeutro / raio, 0.5f, 40f) : 1f;

        private static Image Preencher(RectTransform r, Color cor, float raio)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Neutro();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Raio(raio);
            img.color = cor;
            img.raycastTarget = false;
            return img;
        }



        // Compensação da margem do sprite: a caixa cresce 6 px para cada lado, então a posição
        // recua 6 no eixo em que ela é medida a partir de uma BORDA. Onde a medida é a partir do
        // CENTRO o rect cresce simetricamente e a posição não muda — daí o zero.
        private static float SinalX(Ancora a) => a switch
        {
            Ancora.TopRight or Ancora.BottomRight => -1f,
            Ancora.TopCenter or Ancora.BottomCenter or Ancora.Center => 0f,
            _ => 1f,
        };

        private static float SinalY(Ancora a) => a switch
        {
            Ancora.BottomLeft or Ancora.BottomCenter or Ancora.BottomRight => -1f,
            Ancora.Center => 0f,
            _ => 1f,
        };

        // ------------------------------------------------------------------ Texto

        /// <summary>Fonte do CSS → asset TMP. `font:900 11px/1 Archivo` vira ("Archivo", 900).</summary>
        public static TMP_FontAsset Fonte(string familia, int peso = 400)
        {
            if (familia == "Titan One")
                return LayoutResources.Fonte("Titan One");

            if (familia == "Space Mono")
                return LayoutResources.Fonte(peso >= 700 ? "Space Mono Bold" : "Space Mono");

            return LayoutResources.Fonte(peso >= 800 ? "Archivo Black" : peso >= 700 ? "Archivo Bold" : "Archivo");
        }

        public static TextMeshProUGUI Texto(Transform pai, string nome, string valor,
                                            string familia, int peso, float corpo, Color cor,
                                            TextAlignmentOptions alinhamento = TextAlignmentOptions.Center,
                                            float espacamentoEm = 0f, float sombraY = 0f)
        {
            RectTransform r = No(pai, nome);
            Esticar(r);

            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = valor;
            tmp.fontSize = corpo;
            tmp.color = cor;
            tmp.alignment = alinhamento;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            // O letter-spacing do CSS é em `em`; o characterSpacing do TMP é em centésimos de em.
            if (!Mathf.Approximately(espacamentoEm, 0f))
                tmp.characterSpacing = espacamentoEm * 100f;

            TMP_FontAsset f = Fonte(familia, peso);
            if (f != null)
                tmp.font = f;

            // `text-shadow: 0 Npx 0` — o componente Shadow duplica a malha sem borrar, que é
            // exatamente o comportamento do CSS aqui. (Para IMAGENS a sombra é irmã; para texto,
            // duplicar a malha é mais barato e não desalinha.)
            if (sombraY > 0f)
            {
                var sh = r.gameObject.AddComponent<Shadow>();
                sh.effectColor = Ink;
                sh.effectDistance = new Vector2(0f, -sombraY);
                sh.useGraphicAlpha = true;
            }

            return tmp;
        }

        // ------------------------------------------------------------------ Efeitos

        /// <summary>
        /// `box-shadow: 0 0 Npx cor` — halo com a FORMA do elemento.
        ///
        /// Um falloff radial esticado não serve: numa barra de 302×24 o miolo brilhante fica atrás
        /// da própria barra e o que sobra nas pontas já caiu para quase nada. O box-shadow do CSS
        /// acompanha o retângulo arredondado, então o equivalente honesto são cópias concêntricas
        /// da mesma forma, cada uma maior e mais fraca — o degradê nasce da soma.
        ///
        /// O CanvasGroup existe para o <see cref="PartyRacers.UI.Motion.UIGlowPulse"/> poder
        /// respirar a opacidade das camadas juntas, como faz a keyframe prGlow.
        /// </summary>
        public static RectTransform Glow(Transform pai, string nome, Color cor, Vector2 tamanhoBase,
                                         float raio, float alfa)
        {
            RectTransform r = No(pai, nome);
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = tamanhoBase;
            r.SetAsFirstSibling();

            var grupo = r.gameObject.AddComponent<CanvasGroup>();
            grupo.alpha = alfa;
            grupo.blocksRaycasts = false;

            // Da mais larga para a mais estreita: no UGUI a ordem dos irmãos é o z-order, e a
            // camada externa precisa ficar atrás para as internas somarem por cima dela.
            // As camadas SOMAM: com 0,16/0,26/0,40/0,62 o miolo fechava em ~0,81 de alfa e o halo
            // virava um bloco — a agulha do dial, de 3 px, aparecia como uma barra de 40. Estes
            // pesos fecham em ~0,49, que é a cara de um blur do CSS, onde o pico é a cor declarada.
            float[] fracoes = { 1f, 0.62f, 0.34f, 0.16f };
            float[] pesos = { 0.06f, 0.10f, 0.16f, 0.26f };

            for (int i = 0; i < fracoes.Length; i++)
            {
                RectTransform camada = No(r, $"L{i + 1}");
                camada.anchorMin = Vector2.zero;
                camada.anchorMax = Vector2.one;
                camada.pivot = new Vector2(0.5f, 0.5f);
                float e = raio * fracoes[i];
                camada.offsetMin = new Vector2(-e, -e);
                camada.offsetMax = new Vector2(e, e);

                var img = camada.gameObject.AddComponent<Image>();
                img.sprite = Neutro();
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
                img.color = new Color(cor.r, cor.g, cor.b, pesos[i]);
                img.raycastTarget = false;
            }

            return r;
        }

        /// <summary>Faixa de luz que cruza o elemento — a keyframe prShine.</summary>
        public static RectTransform Varredura(Transform pai, float largura, float periodo,
                                              float opacidade, bool suave = true)
        {
            RectTransform r = No(pai, "ShineSweep");
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(largura, 0f);

            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Neutro();
            img.type = Image.Type.Simple;
            img.color = new Color(1f, 1f, 1f, opacidade);
            img.raycastTarget = false;

            // `linear-gradient(100deg, transparent, branco, transparent)`: a faixa acende no meio e
            // some nas pontas. Sem as TRÊS paradas seria um retângulo branco atravessando a barra.
            var g = r.gameObject.AddComponent<PartyRacers.UI.Motion.UIGradient>();
            g.Definir(new Color(1f, 1f, 1f, 0f), Color.white,
                      PartyRacers.UI.Motion.UIGradient.Direcao.HorizontalEspelhado);

            var sweep = r.gameObject.AddComponent<PartyRacers.UI.Motion.UIShineSweep>();
            Privado(sweep, "periodo", periodo);
            Privado(sweep, "suavizar", suave);
            Privado(sweep, "folga", largura * 0.5f);
            return r;
        }

        // ------------------------------------------------------------------ Serialização

        public static void Privado(Object alvo, string campo, float valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null) { p.floatValue = valor; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        public static void Privado(Object alvo, string campo, bool valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null) { p.boolValue = valor; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        public static void Privado(Object alvo, string campo, Color valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null) { p.colorValue = valor; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        public static void Privado(Object alvo, string campo, Vector2 valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null) { p.vector2Value = valor; so.ApplyModifiedPropertiesWithoutUndo(); }
        }

        public static void Referencia(Object alvo, string campo, Object valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null) { p.objectReferenceValue = valor; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
}
