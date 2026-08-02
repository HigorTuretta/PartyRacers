#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Passa a cena inteira por uma bateria de otimizações de render, todas idempotentes: rodar duas
    /// vezes dá o mesmo resultado, e nada aqui muda a geometria ou o gameplay.
    ///
    /// Cada passo corrige um custo que a cena estava pagando sem receber nada em troca:
    ///
    /// 1. <b>Probes</b> — a cena não tem light probes nem lightmaps (a luz ambiente vem do SH do
    ///    skybox) e a única reflection probe não cobre o cenário. Mesmo assim 785 renderers pediam
    ///    interpolação de probe por frame. Desligar é grátis visualmente.
    /// 2. <b>Motion vectors</b> — o Global Volume tem Motion Blur ligado, então todo renderer com
    ///    motion vectors "Per Object" entra num passe extra. Cenário parado não precisa: o movimento
    ///    da câmera já é reconstruído da profundidade.
    /// 3. <b>Sombras</b> — 1091 renderers projetavam sombra, incluindo props além da distância de
    ///    sombra da câmera (cuja sombra nunca aparece) e peças pequenas demais para a sombra ser
    ///    legível. Cada caster custa em todas as cascatas em que cai.
    /// 4. <b>GPU instancing</b> — 115 dos 126 materiais estavam sem instancing.
    /// 5. <b>Flags de culling</b> — marca occluder/occludee coerentemente, para o caso de a occlusion
    ///    culling ser bakeada depois (hoje a cena tem 0 byte de dado de oclusão).
    ///
    /// O que NÃO é mexido aqui de propósito: pista, armadilhas, karts e qualquer coisa que se mova —
    /// esses continuam com sombra e motion vectors, porque é neles que o jogador olha. E nada de
    /// apagar objetos: ver o comentário sobre RaceHUDDataProvider mais abaixo.
    /// </summary>
    public static class SceneOptimizerTool
    {
        /// <summary>Deve bater com o shadowDistance do URP asset (PC_RPAsset).</summary>
        const float DistanciaDeSombraDaCamera = 80f;

        /// <summary>Abaixo disto a sombra do objeto é um borrão de poucos pixels.</summary>
        const float TamanhoMinimoParaSombra = 2.5f;

        /// <summary>Grupos considerados cenário estático (não se movem, não são gameplay).</summary>
        static readonly string[] GruposDeCenario = { "Cenário", "Terreno", "Letreiro" };

        [MenuItem("Tools/PartyRacers/Cenário/Otimizar cena")]
        public static void Otimizar()
        {
            var relatorio = new List<string>();

            relatorio.Add(DesligarProbesEMotionVectors());
            relatorio.Add(AjustarSombras());
            relatorio.Add(LigarInstancing());
            relatorio.Add(AjustarFlagsDeCulling());

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("[Otimizador]\n  " + string.Join("\n  ", relatorio.Where(r => !string.IsNullOrEmpty(r))));
        }

        // NÃO remover "duplicatas" de RaceHUDDataProvider aqui. Uma versão anterior desta ferramenta
        // apagava as cópias extras achando que eram lixo, e isso QUEBROU a HUD: os binders das telas
        // (RaceHUDUI, StandingsUI, PowerSlotUI, ToastNotificationUI, RaceResultUI) guardam uma
        // referência serializada a UMA instância específica. Apagar a instância certa deixa os cinco
        // campos nulos e a HUD inteira morre em silêncio.
        //
        // A origem das cópias é o menu "7 - Aplicar HUD nas Cenas de Pista" (BuildScenes.cs), que cria
        // um provider novo a cada execução sem remover o anterior. O lugar certo de resolver isso é lá,
        // reaproveitando o provider existente — não aqui, apagando às cegas.

        // --- 1. probes e motion vectors -----------------------------------------------------

        static string DesligarProbesEMotionVectors()
        {
            bool temProbesDeLuz = LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0;
            if (temProbesDeLuz)
                return "probes: a cena TEM light probes bakeadas — passo pulado para não mudar a iluminação";

            int probes = 0, motion = 0;

            foreach (var r in RenderersDeCenario())
            {
                if (r.lightProbeUsage != LightProbeUsage.Off)
                {
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    probes++;
                }

                if (r.motionVectorGenerationMode != MotionVectorGenerationMode.ForceNoMotion)
                {
                    r.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                    motion++;
                }

                EditorUtility.SetDirty(r);
            }

            return $"probes desligadas em {probes} renderers; motion vectors desligados em {motion}";
        }

        // --- 3. sombras ---------------------------------------------------------------------

        static string AjustarSombras()
        {
            var linha = TrackGeometry.LerLinha();
            if (linha.Count < 2)
                return "sombras: linha de corrida não encontrada — passo pulado";

            int desligadas = 0, mantidas = 0;

            foreach (var r in RenderersDeCenario())
            {
                if (r.shadowCastingMode == ShadowCastingMode.Off) continue;

                float distancia = TrackGeometry.Distancia(r.bounds.center, linha);
                float tamanho = r.bounds.size.magnitude;

                // Fora do alcance de sombra da câmera a sombra simplesmente nunca é desenhada; e
                // objeto pequeno vira um borrão de poucos pixels que ninguém identifica.
                bool inutil = distancia > DistanciaDeSombraDaCamera || tamanho < TamanhoMinimoParaSombra;

                if (!inutil) { mantidas++; continue; }

                r.shadowCastingMode = ShadowCastingMode.Off;
                EditorUtility.SetDirty(r);
                desligadas++;
            }

            return $"sombras desligadas em {desligadas} renderers de cenário (mantidas em {mantidas} perto da pista)";
        }

        // --- 4. GPU instancing --------------------------------------------------------------

        static string LigarInstancing()
        {
            var materiais = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(m)))
                .Distinct()
                .ToList();

            int ligados = 0;

            foreach (var m in materiais)
            {
                if (m.enableInstancing) continue;

                // Materiais em Packages/ são somente leitura (ex.: o Lit.mat padrão do URP).
                if (AssetDatabase.GetAssetPath(m).StartsWith("Packages/")) continue;

                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                ligados++;
            }

            if (ligados > 0) AssetDatabase.SaveAssets();
            return $"GPU instancing ligado em {ligados} materiais (de {materiais.Count})";
        }

        // --- 5. flags de culling ------------------------------------------------------------

        static string AjustarFlagsDeCulling()
        {
            int ajustados = 0;

            foreach (var r in RenderersDeCenario())
            {
                var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
                var novas = flags;

                // Todo cenário parado pode ser ocluído; só o que é grande e sólido vale como occluder
                // (uma folhagem vazada não bloqueia nada e só engorda o bake).
                novas |= StaticEditorFlags.OccludeeStatic;

                bool grande = r.bounds.size.magnitude > 12f;
                if (grande) novas |= StaticEditorFlags.OccluderStatic;
                else novas &= ~StaticEditorFlags.OccluderStatic;

                if (novas == flags) continue;

                GameObjectUtility.SetStaticEditorFlags(r.gameObject, novas);
                ajustados++;
            }

            return $"flags de occlusion ajustadas em {ajustados} objetos " +
                   $"(a cena ainda tem {StaticOcclusionCulling.umbraDataSize} bytes de dado de oclusão — " +
                   "rode Window ▸ Rendering ▸ Occlusion Culling ▸ Bake para aproveitar)";
        }

        // --- helpers ------------------------------------------------------------------------

        /// <summary>
        /// Renderers do cenário estático. Pista, armadilhas, gameplay e IA ficam de fora: são
        /// objetos que o jogador toca ou que se movem, e é neles que a sombra conta.
        /// </summary>
        static IEnumerable<MeshRenderer> RenderersDeCenario()
        {
            foreach (var nome in GruposDeCenario)
            {
                var raiz = GameObject.Find(nome);
                if (raiz == null) continue;

                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                    yield return r;
            }
        }
    }
}
#endif
