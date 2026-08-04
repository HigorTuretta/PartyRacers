using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Motion
{
    /// <summary>
    /// Gradiente linear de duas paradas sobre qualquer Graphic, pintando os vértices da malha.
    ///
    /// Existe porque o protótipo usa `linear-gradient` em praticamente todo elemento colorido —
    /// os blocos de vida são `linear-gradient(180deg,#6BF2BC,#2FBB7E)`, os do escudo
    /// `linear-gradient(180deg,#DFF6FF,#35A7FF)` — e o UGUI não tem nada equivalente. Sem isto os
    /// blocos saem chapados e a barra perde o brilho de cima que dá o volume.
    ///
    /// Pinta a malha existente; não cria sprite, não redimensiona nada. Como multiplica pela cor
    /// do Graphic, deixe o Image em branco e a cor virá daqui.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Graphic))]
    public class UIGradient : BaseMeshEffect
    {
        public enum Direcao
        {
            /// <summary>De cima para baixo — o `180deg` do CSS.</summary>
            Vertical,
            Horizontal,
            /// <summary>Diagonal, canto superior-esquerdo → inferior-direito.</summary>
            Diagonal,
            /// <summary>
            /// Três paradas simétricas: `início` nas duas pontas e `fim` no meio. É o
            /// `linear-gradient(100deg, transparent, branco, transparent)` da faixa de luz —
            /// com duas paradas só, a faixa fica transparente inteira e a varredura some.
            /// </summary>
            HorizontalEspelhado,
        }

        [SerializeField] private Direcao direcao = Direcao.Vertical;
        [SerializeField] private Color inicio = Color.white;
        [SerializeField] private Color fim = new Color(0.6f, 0.6f, 0.6f, 1f);

        [Tooltip("Multiplicar pela cor do Graphic em vez de substituí-la. Ligado permite escurecer " +
                 "o conjunto todo mexendo só no Image (útil em estados desabilitados).")]
        [SerializeField] private bool multiplicarPelaCor = true;

        private static readonly List<UIVertex> buffer = new List<UIVertex>();

        public void Definir(Color a, Color b, Direcao d = Direcao.Vertical)
        {
            inicio = a;
            fim = b;
            direcao = d;

            if (graphic != null)
                graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            buffer.Clear();
            vh.GetUIVertexStream(buffer);

            // Os limites vêm da MALHA, não do RectTransform: assim o gradiente acompanha o texto
            // (cuja malha é bem menor que o rect) e qualquer Image com preenchimento parcial.
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < buffer.Count; i++)
            {
                Vector3 p = buffer[i].position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.y > maxY) maxY = p.y;
            }

            float largura = Mathf.Max(0.0001f, maxX - minX);
            float altura = Mathf.Max(0.0001f, maxY - minY);
            Color baseColor = graphic != null ? graphic.color : Color.white;

            for (int i = 0; i < buffer.Count; i++)
            {
                UIVertex v = buffer[i];

                float t = direcao switch
                {
                    // No UGUI o Y cresce para CIMA; no CSS o 180deg desce. Invertido de propósito.
                    Direcao.Vertical => 1f - (v.position.y - minY) / altura,
                    Direcao.Horizontal => (v.position.x - minX) / largura,
                    Direcao.HorizontalEspelhado =>
                        1f - Mathf.Abs((v.position.x - minX) / largura * 2f - 1f),
                    _ => ((v.position.x - minX) / largura + (1f - (v.position.y - minY) / altura)) * 0.5f,
                };

                Color c = Color.Lerp(inicio, fim, Mathf.Clamp01(t));
                v.color = multiplicarPelaCor ? c * baseColor : c;
                buffer[i] = v;
            }

            vh.Clear();
            vh.AddUIVertexTriangleStream(buffer);
        }
    }
}
