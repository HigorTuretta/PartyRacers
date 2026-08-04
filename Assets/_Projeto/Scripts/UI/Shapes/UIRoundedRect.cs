using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Shapes
{
    /// <summary>
    /// Retângulo arredondado desenhado em malha: preenchimento, contorno de espessura exata
    /// (contínuo ou tracejado), raio por eixo e gradiente vertical opcional.
    ///
    /// Por que não bastava um sprite 9-slice, que era como o v1 fazia:
    ///
    /// • <b>Contorno com miolo transparente não existe.</b> Desenhar a moldura e depois "furar" o
    ///   meio exige um sprite de anel por espessura e por raio. Com 9-slice, um card que só tem
    ///   `border: 2px` e fundo transparente vira um bloco chapado da cor do contorno — foi o que
    ///   deixou as linhas do grupo verdes e os cards da sala âmbar inteiros.
    /// • <b>O raio virava chute.</b> O <c>pixelsPerUnitMultiplier</c> DIVIDE a borda do sprite, e o
    ///   canto do sprite embutido da Unity tem tamanho desconhecido; aqui o raio é o número do CSS.
    /// • <b>`border-radius: 50%` é elipse</b>, não pílula. Com raio por eixo ela sai certa.
    ///
    /// A borda externa ganha uma saia de 1 px com alfa zero: sem ela o UGUI, que não tem
    /// antisserrilhado, deixaria o canto escadinha ao lado dos PNG do pacote, que já vêm suavizados.
    /// </summary>
    [AddComponentMenu("UI/Party Racers/Retângulo arredondado")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class UIRoundedRect : MaskableGraphic
    {
        [Header("Forma")]
        [SerializeField] private float raio = 12f;
        [Tooltip("Raio vertical. Negativo usa o mesmo do horizontal.")]
        [SerializeField] private float raioY = -1f;
        [SerializeField, Range(1, 16)] private int segmentosPorCanto = 6;
        [SerializeField] private bool suavizar = true;

        [Header("Preenchimento")]
        [SerializeField] private bool preencher = true;
        [SerializeField] private Color corDoTopo = Color.white;
        [Tooltip("Ligado, o preenchimento vai do topo para a base — o `linear-gradient(180deg)`.")]
        [SerializeField] private bool gradiente;
        [SerializeField] private Color corDaBase = Color.white;

        [Header("Contorno")]
        [SerializeField] private float contorno;
        [SerializeField] private Color corDoContorno = Color.black;
        [Tooltip("Comprimento do traço. Zero é contorno contínuo.")]
        [SerializeField] private float traco;
        [SerializeField] private float folga = 6f;

        private static readonly List<Vector2> externo = new List<Vector2>();
        private static readonly List<Vector2> interno = new List<Vector2>();
        private static readonly List<Vector2> saia = new List<Vector2>();

        private const float Skirt = 1f;

        // ------------------------------------------------------------------ API

        public void Definir(Color cor, float raioPx, float raioYPx = -1f)
        {
            corDoTopo = corDaBase = cor;
            gradiente = false;
            preencher = cor.a > 0.001f;
            raio = raioPx;
            raioY = raioYPx;
            SetVerticesDirty();
        }

        public void DefinirRaio(float raioPx, float raioYPx = -1f)
        {
            raio = raioPx;
            raioY = raioYPx;
            SetVerticesDirty();
        }

        public void DefinirGradiente(Color topo, Color baseCor)
        {
            corDoTopo = topo;
            corDaBase = baseCor;
            gradiente = true;
            preencher = true;
            SetVerticesDirty();
        }

        public void DefinirContorno(Color cor, float espessura, float tracoPx = 0f, float folgaPx = 6f)
        {
            corDoContorno = cor;
            contorno = espessura;
            traco = tracoPx;
            folga = folgaPx;
            SetVerticesDirty();
        }

        public void SemPreenchimento()
        {
            preencher = false;
            SetVerticesDirty();
        }

        public float Raio => raio;

        /// <summary>Cor efetiva do miolo — quem decide se o texto por cima vai claro ou escuro.</summary>
        public Color CorDoPreenchimento => preencher ? corDoTopo : new Color(0f, 0f, 0f, 0f);

        // ------------------------------------------------------------------ Malha

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            Rect r = GetPixelAdjustedRect();
            if (r.width <= 0f || r.height <= 0f)
                return;

            float rx = Mathf.Clamp(raio, 0f, r.width * 0.5f);
            float ry = Mathf.Clamp(raioY < 0f ? raio : raioY, 0f, r.height * 0.5f);

            // A contagem acompanha o raio: 6 segmentos bastam num canto de 14 px e transformam a
            // elipse do palco (raio 540) num polígono com lados retos bem visíveis.
            int seg = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(rx, ry) / 3f),
                                  Mathf.Max(1, segmentosPorCanto), 48);

            Anel(r, rx, ry, seg, externo);

            if (preencher)
            {
                Preencher(vh, r, externo);

                if (suavizar)
                {
                    Anel(Expandir(r, Skirt), rx + Skirt, ry + Skirt, seg, saia);
                    Faixa(vh, saia, externo, Transparente(corDoTopo), Transparente(corDaBase), r, true);
                }
            }

            if (contorno > 0.01f && corDoContorno.a > 0.002f)
            {
                float c = Mathf.Min(contorno, Mathf.Min(r.width, r.height) * 0.5f);
                Anel(Expandir(r, -c), Mathf.Max(0f, rx - c), Mathf.Max(0f, ry - c), seg, interno);

                if (traco > 0.01f)
                    Tracejar(vh, externo, interno, c);
                else
                    Faixa(vh, externo, interno, corDoContorno, corDoContorno, r, false);

                if (suavizar)
                {
                    Anel(Expandir(r, Skirt), rx + Skirt, ry + Skirt, seg, saia);
                    if (traco <= 0.01f)
                        Faixa(vh, saia, externo, Transparente(corDoContorno), corDoContorno, r, false);
                }
            }
        }

        /// <summary>Laço fechado do contorno, no sentido horário a partir do canto superior-direito.</summary>
        private static void Anel(Rect r, float rx, float ry, int seg, List<Vector2> pts)
        {
            pts.Clear();

            Vector2[] centros =
            {
                new Vector2(r.xMax - rx, r.yMax - ry),
                new Vector2(r.xMax - rx, r.yMin + ry),
                new Vector2(r.xMin + rx, r.yMin + ry),
                new Vector2(r.xMin + rx, r.yMax - ry),
            };

            for (int c = 0; c < 4; c++)
            {
                float a0 = 90f - c * 90f;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (a0 - 90f * i / seg) * Mathf.Deg2Rad;
                    pts.Add(centros[c] + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry));
                }
            }
        }

        private void Preencher(VertexHelper vh, Rect r, List<Vector2> loop)
        {
            Vector2 centro = r.center;
            int baseIdx = vh.currentVertCount;
            vh.AddVert(centro, Cor(centro.y, r), Vector2.zero);

            for (int i = 0; i < loop.Count; i++)
                vh.AddVert(loop[i], Cor(loop[i].y, r), Vector2.zero);

            for (int i = 0; i < loop.Count; i++)
            {
                int a = baseIdx + 1 + i;
                int b = baseIdx + 1 + (i + 1) % loop.Count;
                vh.AddTriangle(baseIdx, a, b);
            }
        }

        /// <summary>Anel entre dois laços de mesma contagem — o contorno, ou a saia de suavização.</summary>
        private void Faixa(VertexHelper vh, List<Vector2> fora, List<Vector2> dentro,
                           Color corFora, Color corDentro, Rect r, bool gradienteNoY)
        {
            int n = Mathf.Min(fora.Count, dentro.Count);
            int baseIdx = vh.currentVertCount;

            for (int i = 0; i < n; i++)
            {
                Color cf = gradienteNoY ? Transparente(Cor(fora[i].y, r)) : corFora;
                Color cd = gradienteNoY ? Cor(dentro[i].y, r) : corDentro;
                vh.AddVert(fora[i], cf, Vector2.zero);
                vh.AddVert(dentro[i], cd, Vector2.zero);
            }

            for (int i = 0; i < n; i++)
            {
                int a = baseIdx + i * 2;
                int b = baseIdx + i * 2 + 1;
                int c = baseIdx + ((i + 1) % n) * 2;
                int d = baseIdx + ((i + 1) % n) * 2 + 1;
                vh.AddTriangle(a, c, b);
                vh.AddTriangle(b, c, d);
            }
        }

        /// <summary>Contorno tracejado: percorre o perímetro acendendo e apagando pedaços.</summary>
        private void Tracejar(VertexHelper vh, List<Vector2> fora, List<Vector2> dentro, float espessura)
        {
            float periodo = traco + Mathf.Max(0.5f, folga);
            float andado = 0f;
            int n = fora.Count;

            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                float passo = Vector2.Distance(fora[i], fora[j]);

                // Um pedaço acende quando o MEIO dele cai na fase do traço: comparar pelo meio evita
                // que segmentos longos das retas apaguem inteiros e o tracejado suma nas laterais.
                float fase = (andado + passo * 0.5f) % periodo;
                andado += passo;

                if (fase > traco)
                    continue;

                int b = vh.currentVertCount;
                vh.AddVert(fora[i], corDoContorno, Vector2.zero);
                vh.AddVert(dentro[i], corDoContorno, Vector2.zero);
                vh.AddVert(fora[j], corDoContorno, Vector2.zero);
                vh.AddVert(dentro[j], corDoContorno, Vector2.zero);
                vh.AddTriangle(b, b + 2, b + 1);
                vh.AddTriangle(b + 1, b + 2, b + 3);
            }
        }

        private Color Cor(float y, Rect r)
        {
            Color c = gradiente
                ? Color.Lerp(corDaBase, corDoTopo, Mathf.InverseLerp(r.yMin, r.yMax, y))
                : corDoTopo;
            return c * color;
        }

        private static Color Transparente(Color c) => new Color(c.r, c.g, c.b, 0f);

        private static Rect Expandir(Rect r, float d) =>
            new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f);
    }
}
