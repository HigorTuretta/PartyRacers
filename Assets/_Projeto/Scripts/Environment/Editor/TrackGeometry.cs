#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Leitura do traçado da pista a partir da linha de corrida da IA (IA/BotRacingLine).
    ///
    /// Várias ferramentas de cenário precisam responder "a que distância da pista isto está?" — a
    /// floresta, para não plantar em cima do traçado; o otimizador, para decidir o que ainda precisa
    /// projetar sombra. A linha de corrida é a única fonte confiável dessa resposta, porque segue o
    /// caminho que o jogador realmente percorre (inclusive nos dois níveis do mapa).
    /// </summary>
    internal static class TrackGeometry
    {
        const string CaminhoLinha = "IA/BotRacingLine";

        /// <summary>Pontos do traçado projetados no plano XZ, na ordem em que são percorridos.</summary>
        public static List<Vector2> LerLinha()
        {
            var pontos = new List<Vector2>();
            var raiz = GameObject.Find(CaminhoLinha);
            if (raiz == null) return pontos;

            foreach (Transform filho in raiz.transform)
            {
                // Os nós "Zona_*" marcam comportamento da IA (curva forte, chicane), não posição.
                if (filho.name.StartsWith("Zona_")) continue;
                pontos.Add(new Vector2(filho.position.x, filho.position.z));
            }
            return pontos;
        }

        /// <summary>Menor distância horizontal de um ponto até o traçado (fechado).</summary>
        public static float Distancia(Vector2 p, List<Vector2> linha)
        {
            float melhor = float.MaxValue;
            for (int i = 0; i < linha.Count; i++)
                melhor = Mathf.Min(melhor, DistanciaAteSegmento(p, linha[i], linha[(i + 1) % linha.Count]));
            return melhor;
        }

        public static float Distancia(Vector3 p, List<Vector2> linha)
        {
            return Distancia(new Vector2(p.x, p.z), linha);
        }

        static float DistanciaAteSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-4f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
#endif
