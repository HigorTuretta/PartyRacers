#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PartyRacers.UI.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Monta o catálogo de pistas: abre cada cena de corrida, lê as voltas configuradas no
    /// KartRaceTracker, renderiza uma miniatura de verdade da pista e grava o
    /// <see cref="TrackDefinition"/>. Nada aqui é inventado — nome de cena, voltas e imagem
    /// saem da própria cena.
    /// </summary>
    public static class TrackCatalogBuilder
    {
        const string PASTA_DEFS = "Assets/_Projeto/Settings/Tracks";
        const string PASTA_IMGS = "Assets/_Projeto/Art/UI/Tracks";

        /// <summary>Cenas jogáveis e o nome que aparece na seleção.</summary>
        static readonly (string cena, string nome, string descricao)[] Pistas =
        {
            ("MiniGolfeRun", "MINI GOLFE RUN", "Dois níveis, dois saltos e atalhos no gramado."),
            ("DEMO",         "PISTA DEMO",     "Circuito curto de teste, bom para aquecer."),
        };

        [MenuItem("Party Racers/UI/Gerar catálogo de pistas")]
        public static void Gerar()
        {
            var cenaAtual = EditorSceneManager.GetActiveScene().path;
            if (EditorSceneManager.GetActiveScene().isDirty &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Directory.CreateDirectory(PASTA_DEFS);
            Directory.CreateDirectory(PASTA_IMGS);

            var log = new List<string>();
            for (int i = 0; i < Pistas.Length; i++)
            {
                var (cena, nome, descricao) = Pistas[i];
                string caminhoCena = CaminhoDaCena(cena);
                if (caminhoCena == null) { log.Add($"  {cena}: cena não encontrada — pulada"); continue; }

                EditorSceneManager.OpenScene(caminhoCena, OpenSceneMode.Single);

                int voltas = LerVoltas();
                string png = RenderizarMiniatura(cena);

                var def = CriarOuAtualizar(cena, nome, descricao, voltas, png, i);
                log.Add($"  {cena}: {voltas} voltas, miniatura={(png != null ? "ok" : "falhou")} -> {def.name}");
            }

            if (!string.IsNullOrEmpty(cenaAtual))
                EditorSceneManager.OpenScene(cenaAtual, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[pistas] catálogo gerado:\n" + string.Join("\n", log));
        }

        static string CaminhoDaCena(string nome)
        {
            var guid = AssetDatabase.FindAssets($"{nome} t:Scene", new[] { "Assets/_Projeto/Scenes" })
                                    .FirstOrDefault(g => Path.GetFileNameWithoutExtension(
                                        AssetDatabase.GUIDToAssetPath(g)) == nome);
            return guid == null ? null : AssetDatabase.GUIDToAssetPath(guid);
        }

        /// <summary>
        /// Voltas da corrida. O KartRaceTracker vive no prefab do kart, não na cena da pista —
        /// ou seja, o número vale para qualquer pista. Lido de lá para não inventar valor.
        /// </summary>
        static int LerVoltas()
        {
            var kart = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab");
            var tracker = kart != null
                ? kart.GetComponentsInChildren<MonoBehaviour>(true)
                      .FirstOrDefault(m => m != null && m.GetType().Name == "KartRaceTracker")
                : null;
            if (tracker == null) { Debug.LogWarning("[pistas] não achei KartRaceTracker no kart — voltas ficam 0"); return 0; }

            var p = new SerializedObject(tracker).FindProperty("totalLaps");
            return p != null ? p.intValue : 0;
        }

        /// <summary>Foto aérea da pista, enquadrando tudo que tem malha na cena.</summary>
        static string RenderizarMiniatura(string cena)
        {
            var renderers = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                                  .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                                  .ToList();
            if (renderers.Count == 0) return null;

            var caixa = renderers[0].bounds;
            foreach (var r in renderers) caixa.Encapsulate(r.bounds);

            const int W = 640, H = 360;
            var camGO = new GameObject("~TrackShot") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            string caminho = null;

            try
            {
                float raio = caixa.extents.magnitude;
                cam.fieldOfView = 45f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = raio * 6f + 1000f;
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.aspect = (float)W / H;

                // três quartos de cima, como um cartão de seleção de pista
                var dir = Quaternion.Euler(38f, 35f, 0f) * Vector3.forward;
                cam.transform.position = caixa.center - dir * (raio * 1.45f);
                cam.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                cam.targetTexture = rt;
                cam.Render();

                var antes = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = antes;

                caminho = PASTA_IMGS + "/Pista_" + cena + ".png";
                File.WriteAllBytes(caminho, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);

                var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = false;
                imp.SaveAndReimport();
            }
            finally
            {
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            return caminho;
        }

        static TrackDefinition CriarOuAtualizar(string cena, string nome, string descricao,
                                                int voltas, string png, int ordem)
        {
            string caminho = PASTA_DEFS + "/Pista_" + cena + ".asset";
            var def = AssetDatabase.LoadAssetAtPath<TrackDefinition>(caminho);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<TrackDefinition>();
                AssetDatabase.CreateAsset(def, caminho);
            }

            def.nome = nome;
            def.cena = cena;
            def.descricao = descricao;
            def.voltas = voltas;
            def.ordem = ordem;
            if (png != null) def.miniatura = AssetDatabase.LoadAssetAtPath<Sprite>(png);
            EditorUtility.SetDirty(def);
            return def;
        }
    }
}
#endif
