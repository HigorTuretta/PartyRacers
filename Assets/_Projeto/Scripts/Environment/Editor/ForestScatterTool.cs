#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Espalha uma floresta de árvores low-poly em volta da pista, sem invadir o traçado.
    ///
    /// A regra que mantém o gameplay intacto é a linha de corrida (IA/BotRacingLine): nenhuma árvore
    /// nasce a menos de <see cref="RaioLivrePista"/> metros dela, e a densidade cresce conforme a
    /// distância — perto da pista fica ralo (clareira), longe fecha o horizonte. O chão é encontrado
    /// por raycast no colisor do Terreno, então a árvore sempre encosta no solo real.
    ///
    /// Custo: as árvores são decorativas — sem colisor, sem light probe, sem reflection probe, e só
    /// as que ficam perto da pista projetam sombra. Todas usam o mesmo material (M_Nature_01, com GPU
    /// instancing) e mantêm o LODGroup do prefab, então centenas delas custam poucas draw calls.
    ///
    /// A geração é determinística (semente fixa): rodar de novo produz a mesma floresta.
    /// </summary>
    public static class ForestScatterTool
    {
        const string RaizFloresta = "Cenário/Floresta";
        const string CaminhoLinha = "IA/BotRacingLine";
        const string NomeTerreno = "Terreno";

        // --- parâmetros de arte -------------------------------------------------------------
        const int Semente = 20260801;

        // As árvores desta cena são gigantes de propósito (a copa de uma árvore em escala 9 tem ~69 m
        // de diâmetro, mais que o dobro dos 30 m de largura da pista). Por isso as distâncias abaixo
        // são medidas a partir da BORDA DA COPA, não do tronco: uma folga fixa no tronco deixaria a
        // copa cobrindo a pista inteira.

        /// <summary>Meia-largura da pista, protegida contra copa por cima.</summary>
        const float MeiaLarguraPista = 15f;

        /// <summary>
        /// Folga de céu livre entre a borda da copa e a borda da pista. Pequena de propósito: a mata
        /// tem que encostar no traçado para o piloto sentir que corre DENTRO dela.
        /// </summary>
        const float FolgaCopaPista = 2.5f;

        /// <summary>
        /// Só as árvores com a copa a até isso da pista projetam sombra. Casa com o shadowDistance
        /// do PC_RPAsset (80 m): além disso a sombra simplesmente não é desenhada.
        /// </summary>
        const float DistanciaSombra = 80f;

        /// <summary>Passo da grade de candidatos. Menor = floresta mais densa.</summary>
        const float PassoGrade = 8f;

        /// <summary>Deslocamento aleatório dentro da célula, para não parecer plantação.</summary>
        const float JitterGrade = 0.9f;

        /// <summary>
        /// Onde a mata é 100% cheia, medido da borda da copa até a borda da pista. É a faixa que o
        /// jogador realmente vê.
        /// </summary>
        const float FaixaCheia = 55f;

        /// <summary>
        /// A partir daqui a mata já está na densidade mínima. A câmera é baixa e a fog fecha antes
        /// disso, então árvore plantada além deste raio é polígono que ninguém vê.
        /// </summary>
        const float FaixaRala = 150f;

        /// <summary>Densidade no fundo, longe da pista — só o suficiente para não haver buraco.</summary>
        const float ChanceLonge = 0.18f;

        // Faixa de escala bem larga: misturar porte pequeno com grande é o que enche o sub-bosque. Só
        // as grandes e o chão entre os troncos fica vazio; só as pequenas e a mata não fecha o
        // horizonte. O mínimo é baixo de propósito — é o único tamanho que cabe colado no traçado,
        // e sem ele a beira da pista fica pelada (com mínimo 3 sobravam 30 árvores a menos de 40 m).
        const float EscalaMin = 1.8f;
        const float EscalaMax = 13f;
        const float InclinacaoMax = 35f;

        /// <summary>
        /// Distância mínima entre duas árvores, como fração da soma dos raios das copas. Abaixo de 1
        /// as copas se interpenetram (o que é natural numa mata); perto de 0,3 vira uma massa sólida.
        /// </summary>
        const float SobreposicaoCopas = 0.33f;

        /// <summary>Folga contra cenário já existente (pedras, props, árvores antigas).</summary>
        const float FolgaCenario = 4f;

        static readonly string[] Prefabs =
        {
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_01.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_02.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_03.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_04.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_05.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_06.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_07.prefab",
            "Assets/_Projeto/Prefabs/Pista/BAIXADOS/Vegetação & Montanhas/Low Poly Nature Pack - Lite/Prefabs/Trees/Trees_01/LP_Tree_09.prefab",
        };

        [MenuItem("Tools/PartyRacers/Cenário/Gerar floresta")]
        public static void Gerar()
        {
            Limpar();

            var linha = LerLinhaDeCorrida();
            if (linha.Count < 2)
            {
                Debug.LogError($"[Floresta] não achei a linha de corrida em '{CaminhoLinha}'.");
                return;
            }

            var terreno = GameObject.Find(NomeTerreno);
            if (terreno == null)
            {
                Debug.LogError($"[Floresta] não achei o '{NomeTerreno}'.");
                return;
            }

            var plantavel = SuperficiesPlantaveis();
            var limites = LimitesDe(terreno);
            var ocupado = CaixasDoCenarioExistente();
            var prefabs = Prefabs.Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(p => p != null).ToArray();
            if (prefabs.Length == 0)
            {
                Debug.LogError("[Floresta] nenhum prefab de árvore encontrado.");
                return;
            }

            var raiz = new GameObject("Floresta");
            raiz.transform.SetParent(GameObject.Find("Cenário").transform, false);
            Undo.RegisterCreatedObjectUndo(raiz, "Gerar floresta");

            // Raio da copa de cada prefab em escala 1, para converter escala -> tamanho real.
            var raioBase = prefabs.ToDictionary(p => p, RaioDaCopaEmEscala1);

            var rng = new System.Random(Semente);
            var plantadas = new List<(Vector2 xz, float raio)>();
            float topo = limites.max.y + 200f;
            int rejeitadasPista = 0, rejeitadasChao = 0, rejeitadasOcupado = 0, rejeitadasCopa = 0;

            for (float x = limites.min.x; x <= limites.max.x; x += PassoGrade)
            {
                for (float z = limites.min.z; z <= limites.max.z; z += PassoGrade)
                {
                    float jx = (float)(rng.NextDouble() - 0.5) * PassoGrade * JitterGrade;
                    float jz = (float)(rng.NextDouble() - 0.5) * PassoGrade * JitterGrade;
                    var xz = new Vector2(x + jx, z + jz);

                    float distPista = DistanciaAteLinha(xz, linha);

                    var prefab = prefabs[rng.Next(prefabs.Length)];

                    // ENCAIXA a maior árvore que cabe aqui, em vez de sortear um tamanho e rejeitar
                    // quando ele não serve. É isso que enche a beira da pista: perto do traçado só
                    // cabe árvore pequena, e ela é plantada — antes o lugar simplesmente ficava vazio.
                    float raioMaximo = distPista - MeiaLarguraPista - FolgaCopaPista;
                    if (raioMaximo <= 0f) { rejeitadasPista++; continue; }

                    float escalaQueCabe = raioMaximo / raioBase[prefab];
                    if (escalaQueCabe < EscalaMin) { rejeitadasPista++; continue; }

                    // Dentro do que cabe, sorteia com viés para o topo: a mata fica de árvores
                    // grandes, e as pequenas aparecem só onde é o único tamanho possível.
                    float teto = Mathf.Min(escalaQueCabe, EscalaMax);
                    float sorteio = 1f - Mathf.Pow((float)rng.NextDouble(), 1.7f);
                    float escala = Mathf.Lerp(EscalaMin, teto, sorteio);
                    float raioCopa = raioBase[prefab] * escala;

                    // Densidade AO CONTRÁRIO do que parece intuitivo: cheia junto da pista, rala no
                    // fundo. A câmera do jogo é baixa e a fog fecha o horizonte, então o jogador nunca
                    // vê a mata distante — o orçamento de árvore rende muito mais gasto na beirada.
                    float folga = distPista - raioCopa - MeiaLarguraPista;
                    float t = Mathf.InverseLerp(FaixaCheia, FaixaRala, folga);
                    float chance = Mathf.Lerp(1f, ChanceLonge, t);
                    if (rng.NextDouble() > chance) continue;

                    // Copas podem se tocar, mas não empilhar: senão a mata vira uma massa verde só.
                    if (plantadas.Any(p => (p.xz - xz).sqrMagnitude <
                                           Mathf.Pow((p.raio + raioCopa) * SobreposicaoCopas, 2f)))
                    { rejeitadasCopa++; continue; }

                    if (!AcharChao(new Vector3(xz.x, topo, xz.y), plantavel, out Vector3 chao, out Vector3 normal))
                    { rejeitadasChao++; continue; }

                    if (Vector3.Angle(normal, Vector3.up) > InclinacaoMax) { rejeitadasChao++; continue; }

                    if (ocupado.Any(b => b.SqrDistance(chao) < FolgaCenario * FolgaCenario))
                    { rejeitadasOcupado++; continue; }

                    var arvore = (GameObject)PrefabUtility.InstantiatePrefab(prefab, raiz.transform);
                    arvore.transform.position = chao - Vector3.up * 0.35f; // enterra a base
                    arvore.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                    arvore.transform.localScale = new Vector3(escala, escala * Mathf.Lerp(0.9f, 1.15f, (float)rng.NextDouble()), escala);

                    PrepararParaPerformance(arvore, distPista - raioCopa <= DistanciaSombra);
                    plantadas.Add((xz, raioCopa));
                }
            }

            AplicarVento(raiz);

            EditorUtility.SetDirty(raiz);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(raiz.scene);
            Debug.Log($"[Floresta] {plantadas.Count} árvores plantadas. " +
                      $"Descartadas: {rejeitadasPista} com a copa sobre a pista, {rejeitadasCopa} por copa empilhada, " +
                      $"{rejeitadasChao} sem chão/íngremes, {rejeitadasOcupado} sobre cenário existente.");
        }

        [MenuItem("Tools/PartyRacers/Cenário/Limpar floresta")]
        public static void Limpar()
        {
            var antiga = GameObject.Find(RaizFloresta);
            if (antiga == null) return;
            Undo.DestroyObjectImmediate(antiga);
            Debug.Log("[Floresta] floresta anterior removida.");
        }

        // --- helpers ------------------------------------------------------------------------

        /// <summary>
        /// Árvore decorativa não precisa de física nem de probes, e sombra só rende perto da pista.
        /// É isso que permite plantar centenas delas sem derrubar o frame.
        /// </summary>
        static void PrepararParaPerformance(GameObject arvore, bool projetaSombra)
        {
            foreach (var c in arvore.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(c, true);

            foreach (var r in arvore.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.shadowCastingMode = projetaSombra ? ShadowCastingMode.On : ShadowCastingMode.Off;
                r.receiveShadows = projetaSombra;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            }

            // Sem BatchingStatic de propósito: o material já tem GPU instancing, e instancing preserva
            // o culling por árvore + o LODGroup. Static batching desligaria os dois e ainda duplicaria
            // a malha na memória.
            GameObjectUtility.SetStaticEditorFlags(arvore, StaticEditorFlags.OccluderStatic);
        }

        /// <summary>Raio horizontal da copa do prefab com escala 1, para dimensionar as folgas.</summary>
        static float RaioDaCopaEmEscala1(GameObject prefab)
        {
            var rends = prefab.GetComponentsInChildren<MeshRenderer>(true);
            if (rends.Length == 0) return 1f;

            // Só o LOD0 conta: os outros são a mesma silhueta com menos vértices.
            float maior = 0f;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var e = mf.sharedMesh.bounds.extents;
                maior = Mathf.Max(maior, Mathf.Max(e.x, e.z));
            }
            return Mathf.Max(maior, 0.1f);
        }

        /// <summary>
        /// Troca o material das árvores pelo equivalente com o shader de vento (Nicrom LPW), para a
        /// mata inteira ondular. O material de vento herda o mesmo atlas, então nada muda de cor.
        ///
        /// Substitui em TODOS os renderers da árvore, não só nos que usam M_Nature: um dos prefabs do
        /// pack (LP_Tree_03 LOD1) veio com o material Lit padrão do URP, que renderiza cinza.
        /// </summary>
        static void AplicarVento(GameObject raiz)
        {
            var vento = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Projeto/Materials/Vento/M_Vento_Arvore.mat");
            if (vento == null)
            {
                Debug.LogWarning("[Floresta] material de vento não encontrado — a mata ficou estática.");
                return;
            }

            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = vento;
                r.sharedMaterials = mats;
            }
        }

        static List<Vector2> LerLinhaDeCorrida()
        {
            var pontos = new List<Vector2>();
            var raiz = GameObject.Find(CaminhoLinha);
            if (raiz == null) return pontos;

            foreach (Transform filho in raiz.transform)
            {
                // Os nós de zona (Zona_*) marcam comportamento da IA, não posição do traçado.
                if (filho.name.StartsWith("Zona_")) continue;
                pontos.Add(new Vector2(filho.position.x, filho.position.z));
            }
            return pontos;
        }

        static float DistanciaAteLinha(Vector2 p, List<Vector2> linha)
        {
            float melhor = float.MaxValue;
            for (int i = 0; i < linha.Count; i++)
            {
                Vector2 a = linha[i];
                Vector2 b = linha[(i + 1) % linha.Count];
                melhor = Mathf.Min(melhor, DistanciaAteSegmento(p, a, b));
            }
            return melhor;
        }

        static float DistanciaAteSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-4f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// Hierarquias em cujo topo é permitido plantar. Além do terreno, entram os platôs
        /// ("Montanhas"): sondando o mapa, 303 de 1764 amostras a 25-45 m do traçado caem no topo
        /// PLANO desses platôs — ou seja, é justamente a borda da pista. Enquanto só o terreno era
        /// aceito, essa faixa ficava pelada e a mata começava longe demais.
        ///
        /// A pista, as armadilhas e o gameplay NUNCA entram: são superfície dirigível.
        /// </summary>
        static readonly string[] GruposPlantaveis =
        {
            "Terreno",
            "Cenário/Montanhas",
            "Cenário/Pedra/Montanhas",
        };

        static HashSet<Collider> SuperficiesPlantaveis()
        {
            var colisores = new HashSet<Collider>();
            foreach (var caminho in GruposPlantaveis)
            {
                var go = GameObject.Find(caminho);
                if (go == null) continue;
                foreach (var c in go.GetComponentsInChildren<Collider>())
                    colisores.Add(c);
            }
            return colisores;
        }

        static bool AcharChao(Vector3 origem, HashSet<Collider> plantavel, out Vector3 ponto, out Vector3 normal)
        {
            ponto = Vector3.zero;
            normal = Vector3.up;

            var hits = Physics.RaycastAll(origem, Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // O primeiro colisor de cima para baixo tem que ser superfície plantável: se vier pista,
            // ponte ou prop antes, aquele ponto está debaixo de algo e não serve.
            var primeiro = hits[0];
            if (!plantavel.Contains(primeiro.collider)) return false;

            ponto = primeiro.point;
            normal = primeiro.normal;
            return true;
        }

        static Bounds LimitesDe(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        /// <summary>
        /// Caixas do que já existe no cenário/pista, para a floresta não nascer dentro.
        ///
        /// As superfícies plantáveis ficam de fora: a caixa de um platô é um bloco enorme e qualquer
        /// ponto no topo dele cai DENTRO dessa caixa, então incluí-la aqui rejeitaria exatamente os
        /// lugares que o passo anterior acabou de liberar.
        /// </summary>
        static List<Bounds> CaixasDoCenarioExistente()
        {
            var plantavel = new HashSet<Renderer>();
            foreach (var caminho in GruposPlantaveis)
            {
                var go = GameObject.Find(caminho);
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    plantavel.Add(r);
            }

            var caixas = new List<Bounds>();
            foreach (var nome in new[] { "Cenário", "Pista", "Armadilhas", "Gameplay", "Letreiro" })
            {
                var raiz = GameObject.Find(nome);
                if (raiz == null) continue;
                foreach (var r in raiz.GetComponentsInChildren<Renderer>())
                {
                    if (plantavel.Contains(r)) continue;
                    caixas.Add(r.bounds);
                }
            }
            return caixas;
        }
    }
}
#endif
