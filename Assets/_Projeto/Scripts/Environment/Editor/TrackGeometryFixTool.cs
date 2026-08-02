#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Conserta os pontos do traçado onde os bots ficavam presos, todos no trecho de 80-130 m
    /// (logo depois da última curva, passando a linha de chegada).
    ///
    /// O diagnóstico veio do <c>BotTrackBaker</c> ("os bots vão bater nessa quina") somado a uma
    /// varredura de raycast sobre a linha de corrida em z = -3,60:
    ///
    /// <code>
    ///   x ≤ -191,75   y = 19,83   piso normal
    ///   x = -195      y = 22,09   tree-log (3)   <- decoração NO MEIO da pista (+2,25 m)
    ///   x = -193      y = 21,45   tree-log (2)   <- decoração NO MEIO da pista (+1,62 m)
    ///   x = -188,50   y = 21,08   degrau de +1,25 m em toda a largura
    ///   x = -184,50   y = 19,00   VÃO de 0,75 m entre Pista/Final e Pista/Início, 2,08 m de fundo
    ///   x = -183,75   y = 21,08   volta ao planalto
    ///   x = -180,75   y = 19,83   queda de -1,25 m
    ///   x = -169      y = 22,97   Golf (21)      <- bola decorativa NO MEIO da pista (+3,14 m)
    /// </code>
    ///
    /// O kart sobe no máximo 1,20 m, então o degrau de 1,25 m era intransponível por milímetros — os
    /// bots raspavam nele indefinidamente. E como a IA mira o centro da pista, as decorações plantadas
    /// exatamente sobre a linha eram batidas por todos, toda volta.
    ///
    /// A correção é conservadora: <b>nada da pista original é apagado ou movido</b>. As decorações
    /// saem do corredor para as bordas (continuam decorando), e degrau/vão ganham rampas e uma ponte
    /// por cima, num objeto próprio que pode ser deletado para reverter tudo.
    /// </summary>
    public static class TrackGeometryFixTool
    {
        const string NomeRaiz = "CorreçõesDeGeometria";
        const string CaminhoRaiz = "Pista/" + NomeRaiz;

        /// <summary>Largura das rampas em Z. Cobre o corredor dirigível (z de -10 a +3) com folga.</summary>
        const float LarguraCorrecao = 17f;
        const float CentroZ = -3.5f;

        /// <summary>Espessura das lajes. Fina, mas não tanto que o kart atravesse em alta velocidade.</summary>
        const float Espessura = 0.6f;

        // --- decorações que estavam dentro do corredor -------------------------------------
        // (caminho, novo Z). O X é mantido: elas continuam compondo o mesmo trecho, só saem da linha.
        static readonly (string caminho, float novoZ)[] DecoracoesParaTirarDaPista =
        {
            ("Cenário/Decoração/tree-log (2)", -14.5f),
            ("Cenário/Decoração/tree-log (3)",   7.5f),
            ("Cenário/Bolas/Golf (21)",        -13.0f),
            ("Cenário/Decoração/support-bottom", -13.0f),
        };

        [MenuItem("Tools/PartyRacers/Cenário/Corrigir geometria da pista")]
        public static void Corrigir()
        {
            Limpar();

            var raiz = new GameObject(NomeRaiz);
            raiz.transform.SetParent(GameObject.Find("Pista").transform, false);
            Undo.RegisterCreatedObjectUndo(raiz, "Corrigir geometria da pista");

            var material = MaterialDaPista();

            // 1) Rampa de subida: 19,83 -> 21,08 em 4 m de corrida (17°, o kart sobe sem perder tempo).
            Rampa(raiz, "Rampa_Subida", new Vector2(-192.5f, 19.83f), new Vector2(-188.5f, 21.08f), material);

            // 2) Ponte sobre o vão da emenda Final/Início. Vai de -185,5 a -183,0 para pisar firme nos
            //    dois lados em vez de só encostar na beirada.
            Laje(raiz, "Ponte_Emenda", -185.5f, -183.0f, 21.08f, material);

            // 3) Rampa de descida: 21,08 -> 19,83. Uma queda seca de 1,25 m a 100 km/h joga o kart no ar
            //    e tira o controle na saída da chegada.
            Rampa(raiz, "Rampa_Descida", new Vector2(-180.75f, 21.08f), new Vector2(-176.75f, 19.83f), material);

            int movidas = TirarDecoracoesDaPista();

            EditorUtility.SetDirty(raiz);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(raiz.scene);
            Debug.Log($"[Geometria] rampas e ponte criadas em '{CaminhoRaiz}'; " +
                      $"{movidas} decoração(ões) tirada(s) do corredor de pilotagem.");
        }

        [MenuItem("Tools/PartyRacers/Cenário/Remover correções de geometria")]
        public static void Limpar()
        {
            var antiga = GameObject.Find(CaminhoRaiz);
            if (antiga == null) return;
            Undo.DestroyObjectImmediate(antiga);
            Debug.Log("[Geometria] correções anteriores removidas.");
        }

        // --- construção ---------------------------------------------------------------------

        /// <summary>
        /// Laje inclinada cuja FACE DE CIMA passa exatamente pelos dois pontos (x, y) dados. O centro
        /// é recuado ao longo da normal da face, senão a rampa fica meia espessura acima do piso e
        /// cria justamente o degrauzinho que se quer eliminar.
        /// </summary>
        static void Rampa(GameObject raiz, string nome, Vector2 inicio, Vector2 fim, Material material)
        {
            Vector2 delta = fim - inicio;
            float comprimento = delta.magnitude;
            float angulo = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            Vector2 meio = (inicio + fim) * 0.5f;
            Vector2 normal = new Vector2(-delta.y, delta.x).normalized; // 90° à esquerda = "para cima"
            Vector2 centro = meio - normal * (Espessura * 0.5f);

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nome;
            go.transform.SetParent(raiz.transform, false);
            go.transform.position = new Vector3(centro.x, centro.y, CentroZ);
            go.transform.rotation = Quaternion.Euler(0f, 0f, angulo);
            go.transform.localScale = new Vector3(comprimento, Espessura, LarguraCorrecao);

            Vestir(go, material);
        }

        /// <summary>Laje horizontal com a face de cima em <paramref name="topoY"/>.</summary>
        static void Laje(GameObject raiz, string nome, float x0, float x1, float topoY, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nome;
            go.transform.SetParent(raiz.transform, false);
            go.transform.position = new Vector3((x0 + x1) * 0.5f, topoY - Espessura * 0.5f, CentroZ);
            go.transform.localScale = new Vector3(x1 - x0, Espessura, LarguraCorrecao);

            Vestir(go, material);
        }

        static void Vestir(GameObject go, Material material)
        {
            var r = go.GetComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;

            // Peça de pista: recebe sombra (senão a emenda aparece), mas não precisa projetar —
            // está encostada no piso e a sombra dela cairia dentro da própria pista.
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.OccludeeStatic);
        }

        /// <summary>Material da própria pista, para a correção não aparecer como caixa cinza.</summary>
        static Material MaterialDaPista()
        {
            var final = GameObject.Find("Pista/Final");
            if (final == null) return null;

            var mr = final.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null) return mr.sharedMaterial;

            return final.GetComponentInChildren<MeshRenderer>()?.sharedMaterial;
        }

        // --- decorações ---------------------------------------------------------------------

        /// <summary>
        /// Move para a borda as decorações que estavam sobre a linha de corrida, apoiando cada uma no
        /// chão que existir no destino. Idempotente: rodar de novo não empurra a peça de novo, porque
        /// o destino é um Z absoluto, não um deslocamento.
        /// </summary>
        static int TirarDecoracoesDaPista()
        {
            int movidas = 0;

            foreach (var (caminho, novoZ) in DecoracoesParaTirarDaPista)
            {
                var go = Achar(caminho);
                if (go == null)
                {
                    Debug.LogWarning($"[Geometria] não achei '{caminho}' — pulei.");
                    continue;
                }

                var r = go.GetComponentInChildren<Renderer>();
                float alturaBase = r != null ? go.transform.position.y - r.bounds.min.y : 0f;

                Vector3 destino = new Vector3(go.transform.position.x, go.transform.position.y, novoZ);

                // Assenta no que houver embaixo (a borda é mais alta que o corredor).
                var hits = Physics.RaycastAll(new Vector3(destino.x, destino.y + 40f, destino.z),
                                              Vector3.down, 90f, ~0, QueryTriggerInteraction.Ignore)
                                  .Where(h => !h.collider.transform.IsChildOf(go.transform))
                                  .OrderBy(h => h.distance)
                                  .ToArray();

                if (hits.Length > 0)
                    destino.y = hits[0].point.y + alturaBase;

                Undo.RecordObject(go.transform, "Tirar decoração da pista");
                go.transform.position = destino;
                EditorUtility.SetDirty(go);
                movidas++;
            }

            return movidas;
        }

        static GameObject Achar(string caminho)
        {
            var partes = caminho.Split('/');
            var atual = GameObject.Find(partes[0]);
            for (int i = 1; i < partes.Length && atual != null; i++)
            {
                var t = atual.transform.Find(partes[i]);
                atual = t != null ? t.gameObject : null;
            }
            return atual;
        }
    }
}
#endif
