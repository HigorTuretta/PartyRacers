using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Shapes;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Constrói as telas a partir do DOM MEDIDO do protótipo (`Assets/_Projeto/UI/Spec/proto/*.json`),
    /// e não da prosa nem do layout.json.
    ///
    /// Por que existe, depois de já haver um <see cref="LayoutBuilder"/>: o `layout.json` do pacote
    /// descreve GEOMETRIA — âncora, offset, tamanho. Quem carrega a APARÊNCIA (cor exata, gradiente,
    /// contorno, sombra, raio, fonte, peso, tracking e o próprio texto de cada rótulo) é o
    /// `Party Racers v2.dc.html`. Telas construídas só pela geometria nasciam no lugar certo e
    /// vazias — abas sem rótulo, cards sem conteúdo, painéis sem nada dentro.
    ///
    /// O dump é colhido com o protótipo RODANDO no navegador: cada nó traz
    /// `getBoundingClientRect` normalizado para o canvas de 1920×1080 e o `getComputedStyle`
    /// resolvido. Aqui cada declaração de CSS vira a primitiva equivalente de UGUI, usando o
    /// <see cref="CssKit"/> para as quatro que o UGUI não tem (gradiente, glow, contorno e sombra
    /// dura).
    /// </summary>
    public static class ProtoBuilder
    {
        public const string ProtoRoot = "Assets/_Projeto/UI/Spec/proto";
        public const string DestinoRoot = "Assets/_Projeto/Prefabs/UI_v2/Screens";

        private const string Menu = "Party Racers/UI v2/";

        /// <summary>
        /// Arquivo do dump → nome do prefab de tela. Só desktop: o alvo é PC.
        ///
        /// As cinco primeiras vêm do protótipo v2, que já nasce com painel de vidro. As sete
        /// últimas só existem desenhadas no documento PLACA (v1) — nelas o <c>vidro</c> aplica a
        /// mudança de direção do handoff §4: painel opaco com contorno preto grosso vira vidro com
        /// fio fino, e o contorno grosso fica reservado a botão, placa de HUD e marca.
        /// </summary>
        private static readonly (string dump, string prefab, bool vidro)[] Telas =
        {
            ("Lobby",             "Screen_Lobby",       false),
            ("Matchmaking",       "Screen_Matchmaking", false),
            ("CustomMatch",       "Screen_CustomMatch", false),
            ("Garage",            "Screen_Garage",      false),
            ("RaceHUD_PC_normal", "Screen_RaceHUD_PC",  false),

            ("Store",             "Screen_Store",       true),
            ("BattlePass",        "Screen_BattlePass",  true),
            ("Settings",          "Screen_Settings",    true),
            ("Result",            "Screen_Result",      true),
            ("JoinCode",          "Screen_JoinCode",    true),
            ("Loading",           "Screen_Loading",     true),
            ("RaceMenu",          "Screen_RaceMenu",    true),
        };

        /// <summary>Tela desta passada pede a conversão para vidro.</summary>
        private static bool vidro;

        // Vidro do v2 (tokens-v2 → superficie.painel) e o fio que substitui o contorno grosso.
        private static readonly Color Vidro = new Color(10 / 255f, 12 / 255f, 34 / 255f, 0.82f);
        private static readonly Color Fio = new Color(155 / 255f, 165 / 255f, 215 / 255f, 0.20f);

        private static readonly List<string> Avisos = new List<string>();
        private static int nosCriados;

        [MenuItem(Menu + "5 · Construir telas do protótipo", priority = 20)]
        public static void ConstruirTudo()
        {
            LayoutResources.Limpar();
            Avisos.Clear();
            nosCriados = 0;

            GarantirPasta(DestinoRoot);

            var feitas = new List<string>();
            foreach ((string dump, string prefab, bool paraVidro) in Telas)
            {
                vidro = paraVidro;
                if (Construir(dump, prefab))
                    feitas.Add(prefab);
            }

            vidro = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine($"[UI v2] {feitas.Count} telas construídas do protótipo ({nosCriados} nós):");
            sb.AppendLine("  " + string.Join(", ", feitas));

            if (Avisos.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Avisos ({Avisos.Count}):");
                foreach (string a in Avisos.Take(30))
                    sb.AppendLine("  • " + a);
            }

            Debug.Log(sb.ToString());
        }

        // ------------------------------------------------------------------ Uma tela

        private static bool Construir(string dump, string prefab)
        {
            string caminho = $"{ProtoRoot}/{dump}.json";
            if (!File.Exists(caminho))
            {
                Aviso($"dump ausente: {caminho}");
                return false;
            }

            JsonValue raizJson = JsonValue.Parse(File.ReadAllText(caminho, Encoding.UTF8));
            List<No> nos = LerNos(raizJson["nodes"]);
            if (nos.Count == 0)
            {
                Aviso($"dump vazio: {dump}");
                return false;
            }

            float palcoW = raizJson["stage"]["w"].AsFloat(1920f);
            float palcoH = raizJson["stage"]["h"].AsFloat(1080f);

            var raiz = new GameObject(prefab, typeof(RectTransform));
            var raizRect = (RectTransform)raiz.transform;
            raizRect.anchorMin = Vector2.zero;
            raizRect.anchorMax = Vector2.one;
            raizRect.offsetMin = raizRect.offsetMax = Vector2.zero;

            var palco = new No { X = 0f, Y = 0f, W = palcoW, H = palcoH, Rect = raizRect };

            var usados = new Dictionary<Transform, HashSet<string>>();
            foreach (No n in nos)
            {
                No pai = n.Pai >= 0 && n.Pai < nos.Count ? nos[n.Pai] : palco;
                Transform paiT = pai.Rect != null ? pai.Rect : raizRect;

                RectTransform r = CssKit.No(paiT, NomeUnico(paiT, n, usados));
                Posicionar(r, n, pai);
                Pintar(n, r);
                n.Rect = r;
                nosCriados++;
            }

            ScreenWiring.Ligar(prefab, raiz, new Mapa(nos));

            string destino = $"{DestinoRoot}/{prefab}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, destino);
            UnityEngine.Object.DestroyImmediate(raiz);
            return true;
        }


        // ------------------------------------------------------------------ Mapa dos nós

        /// <summary>
        /// Localiza no que foi construído pelo que o nó É — o texto que ele mostra e a caixa que
        /// ele ocupa —, não pelo nome do GameObject.
        ///
        /// Os nomes saem do dump e são genéricos (`Box`, `Node_3`): amarrar o wiring a eles
        /// quebraria a cada reextração do protótipo. Texto e geometria vêm do design e só mudam
        /// quando o design muda, que é exatamente quando o wiring DEVE ser revisto.
        /// </summary>
        public sealed class Mapa
        {
            private readonly List<No> nos;

            public Mapa(List<No> n) => nos = n;

            public int Total => nos.Count;

            /// <summary>Primeiro nó cujo texto bate. Devolve o RectTransform do nó do texto.</summary>
            public RectTransform Texto(string valor, bool exato = true)
            {
                No n = nos.FirstOrDefault(x => Bate(x, valor, exato));
                return n?.Rect;
            }

            /// <summary>O ELEMENTO que contém esse texto — o chip, o botão, a linha.</summary>
            public RectTransform Dono(string valor, int acima = 1, bool exato = true)
            {
                RectTransform r = Texto(valor, exato);
                for (int i = 0; i < acima && r != null; i++)
                    r = r.parent as RectTransform;

                return r;
            }

            /// <summary>Todos os nós construídos, para passes que varrem a tela inteira.</summary>
            public RectTransform[] Todas() =>
                nos.Where(n => n.Rect != null).Select(n => n.Rect).ToArray();

            public RectTransform[] Todos(string valor, bool exato = true) =>
                nos.Where(x => Bate(x, valor, exato)).Select(x => x.Rect).ToArray();

            /// <summary>Nós com a caixa pedida, em ordem de leitura (cima→baixo, esquerda→direita).</summary>
            public RectTransform[] Caixas(float w, float h, float tol = 2.5f) =>
                nos.Where(x => Mathf.Abs(x.W - w) <= tol && Mathf.Abs(x.H - h) <= tol)
                   .OrderBy(x => Mathf.Round(x.Y / 12f)).ThenBy(x => x.X)
                   .Select(x => x.Rect).ToArray();

            /// <summary>
            /// Nós de PRIMEIRO nível inteiramente dentro da faixa do topo.
            ///
            /// O documento PLACA não desenha o cabeçalho como uma barra: são grupos soltos (marca,
            /// abas, carteira). Para trocar o cabeçalho inteiro é preciso pegá-los pela POSIÇÃO.
            /// </summary>
            public RectTransform[] NoTopo(float altura) =>
                nos.Where(n => n.Rect != null && n.Pai < 0 && n.H < altura && n.Y + n.H <= altura)
                   .Select(n => n.Rect).ToArray();

            public RectTransform Caixa(float x, float y, float w, float h, float tol = 3f) =>
                nos.FirstOrDefault(n => Mathf.Abs(n.X - x) <= tol && Mathf.Abs(n.Y - y) <= tol
                                     && Mathf.Abs(n.W - w) <= tol && Mathf.Abs(n.H - h) <= tol)?.Rect;

            /// <summary>Todos os textos dentro de uma caixa — para renomear/achar peças de uma linha.</summary>
            public RectTransform[] TextosEm(RectTransform area)
            {
                No alvo = nos.FirstOrDefault(x => x.Rect == area);
                if (alvo == null)
                    return new RectTransform[0];

                return nos.Where(x => x.Rect != null && !string.IsNullOrEmpty(x.Texto)
                                   && x.X >= alvo.X - 1f && x.X + x.W <= alvo.X + alvo.W + 1f
                                   && x.Y >= alvo.Y - 1f && x.Y + x.H <= alvo.Y + alvo.H + 1f)
                           .OrderBy(x => x.Y).ThenBy(x => x.X)
                           .Select(x => x.Rect).ToArray();
            }

            private static bool Bate(No n, string valor, bool exato)
            {
                if (n.Rect == null || string.IsNullOrEmpty(n.Texto))
                    return false;

                return exato
                    ? string.Equals(n.Texto, valor, System.StringComparison.OrdinalIgnoreCase)
                    : n.Texto.IndexOf(valor, System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        // ------------------------------------------------------------------ Posição

        /// <summary>
        /// Escolhe a âncora pela BORDA MAIS PRÓXIMA em cada eixo, e estica quando o nó cobre o pai.
        ///
        /// Ancorar tudo no topo-esquerdo daria o mesmo resultado em 16:9 e erraria em qualquer outra
        /// proporção — é o que faz a fileira de karts do lobby descolar da plataforma. Aqui a barra
        /// do rodapé nasce presa embaixo, a classificação presa à direita, e o fundo de tela cheia
        /// esticado, sem ninguém precisar declarar isso.
        /// </summary>
        private static void Posicionar(RectTransform r, No n, No pai)
        {
            float pw = Mathf.Max(1f, pai.W), ph = Mathf.Max(1f, pai.H);

            float esq = n.X - pai.X;
            float dir = (pai.X + pw) - (n.X + n.W);
            float topo = n.Y - pai.Y;
            float baixo = (pai.Y + ph) - (n.Y + n.H);

            const float Tol = 1.2f;

            float axMin, axMax;
            if (n.W >= pw - Tol) { axMin = 0f; axMax = 1f; }
            else if (EhPainel(n) && n.W >= pw * 0.72f && n.H <= ph * 0.6f
                     && esq > Tol && dir > Tol) { axMin = 0f; axMax = 1f; }
            else if (Mathf.Abs(esq - dir) <= Tol) { axMin = axMax = 0.5f; }
            else if (esq <= dir) { axMin = axMax = 0f; }
            else { axMin = axMax = 1f; }

            float ayMin, ayMax;
            if (n.H >= ph - Tol) { ayMin = 0f; ayMax = 1f; }
            // Um painel que ocupa quase toda a altura ESTICA entre topo e base, em vez de se
            // pendurar no canto mais próximo. Preso só embaixo, ele sobe quando a janela encolhe e
            // passa por cima da barra de topo — foi o que cortava a marca na garagem. Esticado, as
            // duas margens são fixas e o painel nunca invade o teto da tela.
            //
            // Duas restrições, cada uma paga com um defeito visto na tela:
            //
            // • Só quem TEM fundo — contêiner invisível de layout que estica arrasta os filhos
            //   junto, e no modal de busca isso empilhou o dial em cima da grade da sala.
            // • Só COLUNA, nunca um bloco grande nos dois eixos. Um modal é grande em largura e
            //   altura; esticá-lo o encurta junto com a janela e o conteúdo interno se comprime.
            //   Coluna estica porque é isso que ela faz: acompanha a altura da tela.
            else if (EhPainel(n) && n.H >= ph * 0.72f && n.W <= pw * 0.6f
                     && topo > Tol && baixo > Tol) { ayMin = 0f; ayMax = 1f; }
            else if (Mathf.Abs(topo - baixo) <= Tol) { ayMin = ayMax = 0.5f; }
            else if (baixo <= topo) { ayMin = ayMax = 0f; }   // mais perto da base
            else { ayMin = ayMax = 1f; }

            r.anchorMin = new Vector2(axMin, ayMin);
            r.anchorMax = new Vector2(axMax, ayMax);
            r.pivot = new Vector2(0.5f, 0.5f);

            // Y do UGUI cresce para cima; o do CSS, para baixo.
            r.offsetMin = new Vector2(esq - axMin * pw, baixo - ayMin * ph);
            r.offsetMax = new Vector2(-(dir - (1f - axMax) * pw), -(topo - (1f - ayMax) * ph));

            // `transform: rotate` — o rect medido é a caixa envolvente do elemento JÁ girado, então
            // o tamanho volta pela inversa antes de aplicar o ângulo.
            if (!string.IsNullOrEmpty(n.Transformacao))
            {
                float[] m = Matriz(n.Transformacao);
                if (m != null && (Mathf.Abs(m[1]) > 0.0005f || m[0] < 0.9999f))
                {
                    float rad = Mathf.Atan2(m[1], m[0]);
                    float c = Mathf.Abs(Mathf.Cos(rad)), s = Mathf.Abs(Mathf.Sin(rad));
                    float det = c * c - s * s;

                    if (Mathf.Abs(det) > 0.02f)
                    {
                        float w = (c * n.W - s * n.H) / det;
                        float h = (c * n.H - s * n.W) / det;
                        r.sizeDelta += new Vector2(w - n.W, h - n.H);
                    }

                    r.localEulerAngles = new Vector3(0f, 0f, -rad * Mathf.Rad2Deg);
                }
            }
        }

        // ------------------------------------------------------------------ Pintura

        /// <summary>
        /// Traduz `background`, `border`, `border-radius` e `box-shadow` de um nó.
        ///
        /// A forma vem de <see cref="UIRoundedRect"/>, e não de sprite 9-slice: um elemento que só
        /// tem `border` e fundo transparente precisa de miolo VAZIO, e sprite fatiado não faz isso —
        /// com ele os cards da sala e as linhas do grupo saíam chapados na cor do contorno.
        /// </summary>
        private static void Pintar(No n, RectTransform r)
        {
            List<Sombra> sombras = Sombra.Ler(n.Sombra);

            var duras = sombras.Where(s => !s.Inset && s.Blur <= 0.5f && s.Spread <= 0.5f
                                        && Mathf.Abs(s.Y) > 0.5f && s.Cor.a > 0.02f).ToList();
            var glows = sombras.Where(s => !s.Inset && s.Blur > 0.5f && s.Cor.a > 0.02f).ToList();
            var vinhetas = sombras.Where(s => s.Inset && s.Blur > 0.5f && s.Cor.a > 0.02f).ToList();
            // `0 0 0 99px inset` não é sombra: é o jeito do CSS de pintar o elemento inteiro.
            Sombra preenche = sombras.FirstOrDefault(s => s.Inset && s.Blur <= 0.5f && s.Spread > 2f);

            Color fundo = LayoutResources.Cor(n.Fundo, Color.clear);
            if (preenche != null)
                fundo = preenche.Cor;

            bool temFundo = fundo.a > 0.004f;
            Color corDoContorno = LayoutResources.Cor(n.CorDoContorno, Color.clear);
            float contorno = n.Contorno != null ? n.Contorno.Max() : 0f;
            bool temContorno = contorno > 0.4f && corDoContorno.a > 0.02f
                            && n.EstiloDoContorno != "none" && n.EstiloDoContorno != "hidden";
            bool tracejado = n.EstiloDoContorno == "dashed";

            // §4 do handoff: o painel deixa de ser placa opaca com moldura preta e vira vidro.
            // A regra é de TAMANHO, não de nome: o que é grande o bastante para o olho ler como
            // superfície é painel; o que é pequeno é botão, chip ou placa, e mantém a moldura —
            // é isso que passa a separar "onde eu leio" de "onde eu ajo".
            if (vidro && n.W >= 300f && n.H >= 170f && temContorno && contorno >= 3f
                && corDoContorno.a > 0.5f && Luminancia(corDoContorno) < 0.25f)
            {
                fundo = Vidro;
                temFundo = true;
                corDoContorno = Fio;
                contorno = 1f;
            }

            float rx = Mathf.Min(Raio(n), n.W * 0.5f);
            float ry = Mathf.Min(n.RaioY > 0f ? n.RaioY : Raio(n), n.H * 0.5f);

            Fundo img = LerFundo(n.Imagem);

            // Sombra, glow e vinheta desenham ATRÁS: no UGUI só um filho de alguém SEM Graphic
            // consegue isso, então nesses casos o nó vira contêiner e a forma desce um nível.
            bool container = duras.Count > 0 || glows.Count > 0 || vinhetas.Count > 0;

            foreach (Sombra s in duras)
            {
                RectTransform sh = CssKit.Esticar(CssKit.No(r, "Shadow"), -s.Spread);
                sh.offsetMin += new Vector2(s.X, -s.Y);
                sh.offsetMax += new Vector2(s.X, -s.Y);
                Forma(sh).Definir(s.Cor, rx + s.Spread, ry + s.Spread);
            }

            foreach (Sombra s in glows)
            {
                // O `box-shadow: 0 0 Npx` do CSS some suavemente; camadas concêntricas opacas
                // engordariam a agulha do dial (3 px) até virar uma barra. O alfa entra pela cor.
                float alcance = s.Blur * 0.5f + s.Spread;

                // Num elemento fino o halo não pode ser muitas vezes a própria peça: a agulha tem
                // 3 px e um alcance de 18 a transformava numa barra de 40. O limite é proporcional
                // ao MENOR lado, que é o que dá ao halo a leitura de luz e não de volume.
                float menorLado = Mathf.Max(2f, Mathf.Min(n.W, n.H));
                alcance = Mathf.Min(alcance, menorLado * 2.5f + 6f);

                CssKit.Glow(r, "Glow", s.Cor, new Vector2(n.W, n.H), alcance, Mathf.Clamp01(s.Cor.a));
            }

            foreach (Sombra s in vinhetas)
            {
                RectTransform v = CssKit.Esticar(CssKit.No(r, "Vignette"));
                var vi = v.gameObject.AddComponent<Image>();
                vi.sprite = LayoutResources.Sprite("Race/Overlay_Vignette");
                vi.type = Image.Type.Simple;
                vi.color = s.Cor;
                vi.raycastTarget = false;
            }

            Transform host = container ? CssKit.Esticar(CssKit.No(r, "Shape")) : (Transform)r;

            // O documento PLACA usa listras diagonais como papel de parede da PÁGINA, atrás dos
            // mockups. Isso é apresentação do documento, não da tela: no jogo o fundo é o degradê
            // profundo do v2, o mesmo do lobby.
            if (vidro && img != null && img.Tipo == TipoDeFundo.Listras
                && n.W >= 1900f && n.H >= 900f)
            {
                img = new Fundo
                {
                    Tipo = TipoDeFundo.Gradiente,
                    Vertical = true,
                    Topo = new Color(21 / 255f, 26 / 255f, 68 / 255f, 1f),
                    Base = new Color(10 / 255f, 12 / 255f, 34 / 255f, 1f),
                };
                fundo = Color.clear;
                temFundo = false;
            }

            // Fundos que não são forma: luz ambiente e listras diagonais têm sprite próprio.
            if (img != null && img.Tipo == TipoDeFundo.Luz)
            {
                Luz((RectTransform)host, img, n);
                img = null;
            }
            else if (img != null && img.Tipo == TipoDeFundo.Listras)
            {
                Listras((RectTransform)host, n, img);
                img = null;
            }

            bool gradienteVertical = img != null && img.Vertical;
            bool gradienteObliquo = img != null && !img.Vertical;

            if (temFundo || temContorno || gradienteVertical)
            {
                UIRoundedRect forma = Forma((RectTransform)host);

                if (gradienteVertical)
                    forma.DefinirGradiente(img.Topo, img.Base);
                else if (temFundo)
                    forma.Definir(fundo, rx, ry);
                else
                    forma.SemPreenchimento();

                // Sempre por último: um elemento que só tem contorno também precisa do raio, e sem
                // isto ele ficava com o valor padrão — foi o que fez a elipse do palco virar um
                // retângulo de cantos redondos.
                forma.DefinirRaio(rx, ry);

                if (temContorno)
                    forma.DefinirContorno(corDoContorno, contorno, tracejado ? 9f : 0f, 7f);
            }

            // Gradiente na horizontal ou diagonal mora num filho: a forma do pai já carrega o
            // contorno, e pintar os dois com o mesmo degradê borraria a moldura.
            if (gradienteObliquo)
            {
                RectTransform g = CssKit.Esticar(CssKit.No(host, "Grad"), contorno);
                UIRoundedRect fg = Forma(g);
                fg.Definir(Color.white, Mathf.Max(0f, rx - contorno), Mathf.Max(0f, ry - contorno));
                var uig = g.gameObject.AddComponent<PartyRacers.UI.Motion.UIGradient>();
                uig.Definir(img.Topo, img.Base, img.Direcao);
            }

            // `opacity` do CSS vira CanvasGroup — mas só onde ele é EFEITO, não onde é o jeito de
            // desenhar vidro.
            //
            // O protótipo pinta o painel translúcido e ainda baixa a opacidade do bloco inteiro,
            // porque no HTML há uma cena 3D por trás em outra camada. No jogo a translucidez já
            // está na cor do painel; aplicar a opacidade de novo apaga o conteúdo e o texto passa a
            // competir com a oficina. Blocos grandes ficam com a própria cor.
            bool blocoDeConteudo = n.W >= 300f && n.H >= 200f;

            if (n.Opacidade < 0.999f && !blocoDeConteudo)
            {
                var cg = r.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = n.Opacidade;
                cg.blocksRaycasts = false;
            }

            if (n.Overflow == "hidden" || n.Overflow == "auto" || n.Overflow == "scroll")
                r.gameObject.AddComponent<RectMask2D>();

            // Anotação do documento de design não é elemento de tela. O PLACA explica o layout na
            // própria arte ("No celular a lista da esquerda vira abas", "[ render do kit ]") e
            // isso não pode nascer ligado no jogo.
            if (vidro && Anotacao(n.Texto))
            {
                r.gameObject.SetActive(false);
                return;
            }

            if (!string.IsNullOrEmpty(n.Texto))
            {
                // O rect medido de um texto solto já é a caixa dele; um rótulo dentro de um chip
                // precisa ocupar o chip inteiro para centralizar como o flexbox centraliza.
                bool proprio = n.Tag == "#text" || (!temFundo && !temContorno && img == null);
                Texto(n, proprio ? r : CssKit.Esticar(CssKit.No(r, "Label")));
            }
        }

        private static UIRoundedRect Forma(RectTransform r)
        {
            var f = r.gameObject.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            return f;
        }

        /// <summary>`radial-gradient` que morre em transparente — a luz de ambiente do palco.</summary>
        private static void Luz(RectTransform r, Fundo f, No n)
        {
            // O CSS declara ONDE a luz zera (`… transparent 68%`); o sprite de falloff zera só na
            // borda dele. Encolher o rect até essa fração é o que evita a mancha âmbar ocupando
            // meia tela no lugar de um halo atrás do palco.
            if (f.Alcance > 0.05f && f.Alcance < 0.995f)
            {
                float sx = n.W * (1f - f.Alcance) * 0.5f;
                float sy = n.H * (1f - f.Alcance) * 0.5f;
                r.offsetMin += new Vector2(sx, sy);
                r.offsetMax -= new Vector2(sx, sy);
            }

            var img = r.gameObject.AddComponent<Image>();
            img.sprite = LayoutResources.SpriteDeGlow();
            img.type = Image.Type.Simple;
            img.color = f.Topo;
            img.raycastTarget = false;
        }

        /// <summary>`repeating-linear-gradient(-45deg, …)` — faixa de perigo, reparo e o slot ferido.</summary>
        private static void Listras(RectTransform r, No n, Fundo f)
        {
            bool vertical = Mathf.Abs(Mathf.Abs(f.Angulo) - 90f) < 15f;
            string chave = vertical ? "Race/Dial_Grid"
                         : f.Duty < 0.44f ? "Patterns/Stripes_Diag_38"
                         : "Patterns/Stripes_Diag_50";

            Sprite s = LayoutResources.Sprite(chave);
            if (s == null)
                return;

            if (f.Base.a > 0.02f)
                Forma(r).Definir(f.Base, 0f, 0f);

            RectTransform faixas = f.Base.a > 0.02f ? CssKit.Esticar(CssKit.No(r, "Stripes")) : r;

            var img = faixas.gameObject.AddComponent<Image>();
            img.sprite = s;
            img.type = Image.Type.Tiled;
            img.color = f.Topo;
            img.raycastTarget = false;

            // O tile fecha na diagonal a cada `lado`; a distância PERPENDICULAR entre listras é
            // lado/√2, então o lado que reproduz o período pedido é periodo·√2.
            float lado = vertical ? f.Periodo : f.Periodo * 1.41421f;
            img.pixelsPerUnitMultiplier = Mathf.Clamp(s.rect.width / Mathf.Max(2f, lado), 0.05f, 60f);
        }

        // ------------------------------------------------------------------ background-image

        private enum TipoDeFundo { Gradiente, Luz, Listras }

        private sealed class Fundo
        {
            public TipoDeFundo Tipo;
            public Color Topo, Base;
            public float Angulo, Periodo, Duty, Alcance = 1f;
            public bool Vertical;
            public PartyRacers.UI.Motion.UIGradient.Direcao Direcao;
        }

        private static Fundo LerFundo(string css)
        {
            if (string.IsNullOrEmpty(css))
                return null;

            List<(Color cor, float pos, float pct)> paradas = Paradas(css);
            if (paradas.Count < 2)
            {
                Aviso("gradiente não entendido: " + css.Substring(0, Mathf.Min(70, css.Length)));
                return null;
            }

            Color a = paradas[0].cor;
            Color b = paradas[paradas.Count - 1].cor;

            if (css.StartsWith("repeating-linear-gradient"))
            {
                float periodo = Mathf.Max(4f, paradas[paradas.Count - 1].pos);
                float aceso = paradas.Count >= 2 && paradas[1].pos > 0f ? paradas[1].pos : periodo * 0.5f;
                return new Fundo
                {
                    Tipo = TipoDeFundo.Listras,
                    Topo = a,
                    Base = b,
                    Angulo = Angulo(css, -45f),
                    Periodo = periodo,
                    Duty = Mathf.Clamp01(aceso / periodo),
                };
            }

            if (css.StartsWith("radial-gradient"))
            {
                // Radial que MORRE em transparente é luz ambiente. Radial que fecha numa cor sólida
                // é volume (bolinha, chão do palco): ali o degradê diagonal sobre a própria forma
                // lê melhor do que um halo.
                if (b.a < 0.06f)
                {
                    // A última parada opaca marca até onde a luz chega; o resto é queda a zero.
                    float ate = paradas[paradas.Count - 1].pct;
                    return new Fundo
                    {
                        Tipo = TipoDeFundo.Luz,
                        Topo = a,
                        Base = b,
                        Alcance = ate > 0.05f ? ate : 1f,
                    };
                }

                return new Fundo
                {
                    Tipo = TipoDeFundo.Gradiente,
                    Topo = a,
                    Base = b,
                    Vertical = false,
                    Direcao = PartyRacers.UI.Motion.UIGradient.Direcao.Diagonal,
                };
            }

            float ang = ((Angulo(css, 180f) % 360f) + 360f) % 360f;
            var f = new Fundo { Tipo = TipoDeFundo.Gradiente, Topo = a, Base = b, Angulo = ang };

            if (ang < 22f || ang > 338f)                     // 0deg do CSS sobe
            {
                f.Vertical = true;
                f.Topo = b;
                f.Base = a;
            }
            else if (ang > 158f && ang < 202f)               // 180deg desce
            {
                f.Vertical = true;
            }
            else if (ang >= 68f && ang <= 112f)              // 90deg vai para a direita
            {
                f.Direcao = PartyRacers.UI.Motion.UIGradient.Direcao.Horizontal;
            }
            else if (ang >= 248f && ang <= 292f)
            {
                f.Direcao = PartyRacers.UI.Motion.UIGradient.Direcao.Horizontal;
                f.Topo = b;
                f.Base = a;
            }
            else
            {
                f.Direcao = PartyRacers.UI.Motion.UIGradient.Direcao.Diagonal;
                if (ang > 180f) { f.Topo = b; f.Base = a; }
            }

            return f;
        }

        // ------------------------------------------------------------------ Texto

        private static void Texto(No n, RectTransform r)
        {
            string valor = n.Texto;
            if (n.TransformDoTexto == "uppercase")
                valor = valor.ToUpper(CultureInfo.GetCultureInfo("pt-BR"));

            var tmp = r.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = valor;
            tmp.fontSize = n.Corpo;
            tmp.color = LayoutResources.Cor(n.CorDoTexto, CssKit.Cream);
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset f = CssKit.Fonte(n.Fonte, Peso(n.Peso));
            if (f != null)
                tmp.font = f;

            // O letter-spacing do CSS é em px; o characterSpacing do TMP, em centésimos de em.
            if (Mathf.Abs(n.Tracking) > 0.01f && n.Corpo > 0.5f)
                tmp.characterSpacing = n.Tracking / n.Corpo * 100f;

            tmp.alignment = n.Alinhamento switch
            {
                "left" or "start" => TextAlignmentOptions.MidlineLeft,
                "right" or "end" => TextAlignmentOptions.MidlineRight,
                _ => TextAlignmentOptions.Midline,
            };

            // Rótulo que ocupa a caixa inteira do pai (chip, botão) centraliza; rótulo cuja caixa é
            // o próprio texto já veio posicionado e não deve reinterpretar o alinhamento.
            float y = SombraDeTexto(n.SombraDoTexto);
            if (y > 0f)
            {
                var sh = r.gameObject.AddComponent<Shadow>();
                sh.effectColor = CssKit.Ink;
                sh.effectDistance = new Vector2(0f, -y);
                sh.useGraphicAlpha = true;
            }
        }

        private static int Peso(string css) =>
            int.TryParse(css, NumberStyles.Integer, CultureInfo.InvariantCulture, out int p) ? p : 400;

        private static float SombraDeTexto(string css)
        {
            if (string.IsNullOrEmpty(css))
                return 0f;

            // "rgb(10, 12, 34) 0px 2px 0px"
            MatchCollection m = Regex.Matches(css, @"(-?[\d.]+)px");
            return m.Count >= 2 && float.TryParse(m[1].Groups[1].Value, NumberStyles.Float,
                                                  CultureInfo.InvariantCulture, out float v)
                ? Mathf.Abs(v) : 0f;
        }

        // ------------------------------------------------------------------ CSS → números

        private sealed class Sombra
        {
            public Color Cor;
            public float X, Y, Blur, Spread;
            public bool Inset;

            public static List<Sombra> Ler(string css)
            {
                var lista = new List<Sombra>();
                if (string.IsNullOrEmpty(css))
                    return lista;

                foreach (string parte in SepararTopo(css))
                {
                    string t = parte.Trim();
                    if (t.Length == 0)
                        continue;

                    bool inset = t.Contains("inset");
                    t = t.Replace("inset", " ").Trim();

                    Color cor = Color.clear;
                    var mc = Regex.Match(t, @"rgba?\([^)]*\)");
                    if (mc.Success)
                    {
                        cor = LayoutResources.Cor(mc.Value, Color.clear);
                        t = t.Remove(mc.Index, mc.Length);
                    }

                    float[] v = Regex.Matches(t, @"(-?[\d.]+)px")
                        .Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
                        .ToArray();

                    lista.Add(new Sombra
                    {
                        Cor = cor,
                        Inset = inset,
                        X = v.Length > 0 ? v[0] : 0f,
                        Y = v.Length > 1 ? v[1] : 0f,
                        Blur = v.Length > 2 ? v[2] : 0f,
                        Spread = v.Length > 3 ? v[3] : 0f,
                    });
                }

                return lista;
            }
        }

        /// <summary>Separa por vírgula IGNORANDO as que estão dentro de `rgba(...)`.</summary>
        private static List<string> SepararTopo(string s)
        {
            var partes = new List<string>();
            int nivel = 0, inicio = 0;

            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '(') nivel++;
                else if (s[i] == ')') nivel--;
                else if (s[i] == ',' && nivel == 0)
                {
                    partes.Add(s.Substring(inicio, i - inicio));
                    inicio = i + 1;
                }
            }

            partes.Add(s.Substring(inicio));
            return partes;
        }

        /// <summary>Paradas de cor de um gradiente, com a posição em px quando declarada.</summary>
        private static List<(Color cor, float pos, float pct)> Paradas(string css)
        {
            var lista = new List<(Color, float, float)>();
            int abre = css.IndexOf('(');
            int fecha = css.LastIndexOf(')');
            if (abre < 0 || fecha <= abre)
                return lista;

            foreach (string parte in SepararTopo(css.Substring(abre + 1, fecha - abre - 1)))
            {
                string t = parte.Trim();
                var mc = Regex.Match(t, @"rgba?\([^)]*\)");
                if (!mc.Success)
                    continue;   // o primeiro item pode ser o ângulo ou `circle at ...`

                Color cor = LayoutResources.Cor(mc.Value, Color.clear);
                string resto = t.Remove(mc.Index, mc.Length);

                float pos = -1f;
                var mp = Regex.Match(resto, @"(-?[\d.]+)px");
                if (mp.Success)
                    pos = float.Parse(mp.Groups[1].Value, CultureInfo.InvariantCulture);

                float pct = -1f;
                var mq = Regex.Match(resto, @"(-?[\d.]+)%");
                if (mq.Success)
                    pct = float.Parse(mq.Groups[1].Value, CultureInfo.InvariantCulture) / 100f;

                lista.Add((cor, pos, pct));
            }

            return lista;
        }

        private static float Angulo(string css, float padrao)
        {
            var m = Regex.Match(css, @"\(\s*(-?[\d.]+)deg");
            return m.Success ? float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : padrao;
        }

        private static float[] Matriz(string css)
        {
            var m = Regex.Match(css, @"matrix\(([^)]*)\)");
            if (!m.Success)
                return null;

            float[] v = m.Groups[1].Value.Split(',')
                .Select(s => float.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
            return v.Length >= 6 ? v : null;
        }

        private static readonly string[] MarcasDeDocumento =
        {
            "no celular", "no computador", "estados de erro", "pop-up de", "a corrida segue",
            "exporta", "arte vetorial", "render d", "desfocada ao fundo", "ao fundo ]",
        };

        private static bool Anotacao(string texto)
        {
            if (string.IsNullOrEmpty(texto))
                return false;

            string t = texto.Trim().ToLowerInvariant();
            if (t.StartsWith("[") && t.EndsWith("]"))
                return true;

            foreach (string marca in MarcasDeDocumento)
                if (t.Contains(marca))
                    return true;

            return false;
        }

        /// <summary>Tem superfície própria — fundo, contorno ou imagem. Contêiner de layout não.</summary>
        private static bool EhPainel(No n) =>
            !string.IsNullOrEmpty(n.Fundo) || !string.IsNullOrEmpty(n.Imagem)
            || (n.Contorno != null && n.Contorno.Max() > 0.4f);

        private static float Luminancia(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        private static float Raio(No n) => n.Raio != null && n.Raio.Length > 0 ? n.Raio[0] : 0f;

        // ------------------------------------------------------------------ Leitura do dump

        public sealed class No
        {
            public int Pai = -1;
            public float X, Y, W, H;
            public string Tag, Classe, Texto, Fundo, Imagem, Sombra, CorDoContorno, EstiloDoContorno;
            public string CorDoTexto, Fonte, Peso, Alinhamento, TransformDoTexto, SombraDoTexto;
            public string Transformacao, Overflow;
            public float[] Contorno, Raio;
            public float Corpo, Tracking, Opacidade = 1f, RaioY;
            public RectTransform Rect;
        }

        private static List<No> LerNos(JsonValue arr)
        {
            var lista = new List<No>();

            foreach (JsonValue j in arr.Items)
            {
                lista.Add(new No
                {
                    Pai = j["p"].AsInt(-1),
                    X = j["x"].AsFloat(),
                    Y = j["y"].AsFloat(),
                    W = j["w"].AsFloat(),
                    H = j["h"].AsFloat(),
                    Tag = j["tag"].AsString(""),
                    Classe = j["cls"].AsString(""),
                    Texto = j["text"].AsString(null),
                    Fundo = j["bg"].AsString(null),
                    Imagem = j["bgimg"].AsString(null),
                    Sombra = j["shadow"].AsString(null),
                    Contorno = Numeros(j["border"]),
                    CorDoContorno = j["borderColor"].AsString(null),
                    EstiloDoContorno = j["borderStyle"].AsString(null),
                    Raio = Numeros(j["radius"]),
                    RaioY = j["radiusY"].AsFloat(0f),
                    Opacidade = j["opacity"].AsFloat(1f),
                    Transformacao = j["transform"].AsString(null),
                    Overflow = j["overflow"].AsString(null),
                    CorDoTexto = j["fcolor"].AsString(null),
                    Fonte = j["font"].AsString("Archivo"),
                    Peso = j["fweight"].AsString("400"),
                    Corpo = j["fsize"].AsFloat(16f),
                    Tracking = j["tracking"].AsFloat(0f),
                    Alinhamento = j["align"].AsString("center"),
                    TransformDoTexto = j["ttransform"].AsString(null),
                    SombraDoTexto = j["tshadow"].AsString(null),
                });
            }

            return lista;
        }

        private static float[] Numeros(JsonValue v) =>
            v.IsArray ? v.Items.Select(x => x.AsFloat()).ToArray() : null;

        // ------------------------------------------------------------------ Nomes

        private static string NomeUnico(Transform pai, No n, Dictionary<Transform, HashSet<string>> usados)
        {
            string b = Base(n);

            if (!usados.TryGetValue(pai, out HashSet<string> set))
                usados[pai] = set = new HashSet<string>();

            string nome = b;
            int k = 2;
            while (!set.Add(nome))
                nome = $"{b}_{k++}";

            return nome;
        }

        private static string Base(No n)
        {
            // Classes geradas pelo bundler (scp4, sc-interp) não dizem nada; texto e forma dizem.
            if (!string.IsNullOrEmpty(n.Classe) && !n.Classe.StartsWith("sc")
                && n.Classe.Length <= 24 && !n.Classe.Contains(" "))
                return Limpar(n.Classe);

            if (!string.IsNullOrEmpty(n.Texto))
                return "Txt_" + Limpar(n.Texto.Length > 18 ? n.Texto.Substring(0, 18) : n.Texto);

            if (!string.IsNullOrEmpty(n.Imagem))
                return n.Imagem.StartsWith("radial") ? "Glow" : "Grad";

            if (!string.IsNullOrEmpty(n.Sombra) || !string.IsNullOrEmpty(n.Fundo))
                return "Box";

            return "Node";
        }

        private static string Limpar(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
                else if (c == ' ' || c == '_' || c == '-' || c == '·') sb.Append('_');
            }

            string r = sb.ToString().Trim('_');
            return r.Length == 0 ? "Node" : r;
        }

        // ------------------------------------------------------------------ Utilidades

        private static void Aviso(string m)
        {
            if (!Avisos.Contains(m))
                Avisos.Add(m);
        }

        private static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho))
                return;

            int barra = caminho.LastIndexOf('/');
            GarantirPasta(caminho.Substring(0, barra));
            AssetDatabase.CreateFolder(caminho.Substring(0, barra), caminho.Substring(barra + 1));
        }
    }
}
