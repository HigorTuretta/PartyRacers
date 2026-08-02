#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.Environment.EditorTools
{
    /// <summary>
    /// Liga o vento (Low Poly Wind da Nicrom) na vegetação que já existia na cena — árvores,
    /// arbustos e grama do cenário — trocando cada material de vegetação pelo gêmeo com o shader
    /// LPW criado em Assets/_Projeto/Materials/Vento.
    ///
    /// A troca é só de material: mesma malha, mesmo atlas, mesmas cores. A ondulação é feita no
    /// vertex shader, então não custa nada de CPU e não gera draw call extra — o preço é uma
    /// variante de shader a mais e alguns ALU por vértice.
    ///
    /// A operação é reversível pelo menu "Desligar vento", que devolve os materiais originais.
    /// </summary>
    public static class WindVegetationTool
    {
        const string PastaVento = "Assets/_Projeto/Materials/Vento/";

        /// <summary>Grupos da hierarquia que contêm vegetação.</summary>
        static readonly string[] Alvos =
        {
            "Cenário/ÁrvoreComFolhas",
            "Cenário/ÁrvoreSemFolhas",
            "Cenário/Arbusto",
            "Cenário/Decoração",
            "Cenário/Floresta",
        };

        /// <summary>
        /// original -> material de vento equivalente.
        ///
        /// SÓ ENTRA AQUI MATERIAL DE UMA FACE (`_Cull != 0`). O shader do Low Poly Wind é gerado pelo
        /// Amplify e **não inverte a normal nas faces de trás**: aplicá-lo em folhagem de duas faces
        /// (`M_Plant_01`..`M_Plant_04`, `M_Nature_01_Two_Sided`) deixa as folhas grandes com manchas
        /// pretas — verificado por captura, com e sem `Cull Off`. Uma cópia do shader só com
        /// `Cull Off` não resolve: troca buraco por preto, porque o problema é a normal, não o culling.
        ///
        /// Essas plantas ficam sem vento de propósito. Para dar vento a elas seria preciso um shader
        /// que faça flip de normal por `SV_IsFrontFace` — não vale o risco por um arbusto.
        /// </summary>
        static readonly Dictionary<string, string> Mapa = new Dictionary<string, string>
        {
            { "M_Nature_01", "M_Vento_Arvore" },
            { "M_Grass_01",  "M_Vento_Grama" },
        };

        [MenuItem("Tools/PartyRacers/Cenário/Ligar vento na vegetação")]
        public static void Ligar()
        {
            var ventoPorNome = Mapa.ToDictionary(
                kv => kv.Key,
                kv => AssetDatabase.LoadAssetAtPath<Material>(PastaVento + kv.Value + ".mat"));

            var faltando = ventoPorNome.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();
            if (faltando.Count > 0)
            {
                Debug.LogError($"[Vento] faltam materiais de vento para: {string.Join(", ", faltando)}. " +
                               "Gere-os antes (eles ficam em " + PastaVento + ").");
                return;
            }

            int trocados = 0, objetos = 0;

            foreach (var caminho in Alvos)
            {
                var raiz = GameObject.Find(caminho);
                if (raiz == null) continue;

                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool mudou = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (!ventoPorNome.TryGetValue(mats[i].name, out Material vento)) continue;

                        mats[i] = vento;
                        mudou = true;
                        trocados++;
                    }

                    if (!mudou) continue;

                    r.sharedMaterials = mats;
                    // Vegetação que balança não pode ficar em static batching: o batcher congela as
                    // malhas num único mesh e o vertex shader perde a posição de mundo por árvore,
                    // fazendo a mata inteira ondular em fase.
                    GameObjectUtility.SetStaticEditorFlags(
                        r.gameObject,
                        GameObjectUtility.GetStaticEditorFlags(r.gameObject) & ~StaticEditorFlags.BatchingStatic);
                    EditorUtility.SetDirty(r);
                    objetos++;
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Vento] ligado em {objetos} renderers ({trocados} slots de material trocados).");
        }

        [MenuItem("Tools/PartyRacers/Cenário/Desligar vento na vegetação")]
        public static void Desligar()
        {
            // Caminho inverso: material de vento -> original, achado pelo nome no pacote de origem.
            var inverso = new Dictionary<string, Material>();

            foreach (var kv in Mapa)
            {
                var original = AssetDatabase.FindAssets(kv.Key + " t:Material")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => System.IO.Path.GetFileNameWithoutExtension(p) == kv.Key)
                    .Select(AssetDatabase.LoadAssetAtPath<Material>)
                    .FirstOrDefault();

                if (original != null)
                    inverso[kv.Value] = original;
            }

            int trocados = 0;

            foreach (var caminho in Alvos)
            {
                var raiz = GameObject.Find(caminho);
                if (raiz == null) continue;

                foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mats = r.sharedMaterials;
                    bool mudou = false;

                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        if (!inverso.TryGetValue(mats[i].name, out Material original)) continue;

                        mats[i] = original;
                        mudou = true;
                        trocados++;
                    }

                    if (mudou)
                    {
                        r.sharedMaterials = mats;
                        EditorUtility.SetDirty(r);
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Vento] desligado ({trocados} slots de material devolvidos ao original).");
        }
    }
}
#endif
