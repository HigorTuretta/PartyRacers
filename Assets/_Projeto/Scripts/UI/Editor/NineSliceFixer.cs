#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Ajusta o <c>pixelsPerUnitMultiplier</c> das Images 9-slice para que o raio desenhado
    /// na tela seja o raio com que a moldura foi autorada.
    ///
    /// Os PNGs do pacote foram desenhados em escala maior que o uso final: a moldura
    /// <c>UI_Button_R22</c> tem 144px com borda 52 para um raio de 22. Com o multiplicador em 1
    /// a borda vale 52px na tela — mais que a altura útil de um botão de 112 — então a Unity
    /// encolhe as bordas e o botão vira uma pílula sem contorno nem sombra dura.
    ///
    /// Regra: <c>mult = max(borda / raioNominal, borda / (0,28 * menorLado))</c>.
    /// O primeiro termo devolve o raio autorado; o segundo evita que um elemento baixo
    /// (um ponto de 12px, um avatar de 30px) fique redondo demais.
    /// </summary>
    public static class NineSliceFixer
    {
        /// <summary>Raio nominal de cada moldura, lido do nome (R14, R18, ...) ou tabelado.</summary>
        static float RaioNominal(string sprite)
        {
            if (string.IsNullOrEmpty(sprite)) return 0f;
            if (sprite.StartsWith("Bar_")) return 10f;      // trilha/preenchimento de slider
            if (sprite.StartsWith("Toast_")) return 15f;    // raio.card do tokens.json
            var m = System.Text.RegularExpressions.Regex.Match(sprite, @"_R(\d+)");
            return m.Success ? float.Parse(m.Groups[1].Value) : 0f;
        }

        const float FRACAO_MAX = 0.28f;  // raio nunca passa de 28% do menor lado

        /// <summary>Multiplicador ideal para esta Image, ou -1 quando não se aplica.</summary>
        static float Ideal(Image img, Vector2 tamanho)
        {
            if (img == null || img.sprite == null) return -1f;
            var b = img.sprite.border;
            if (b == Vector4.zero) return -1f;

            float raio = RaioNominal(img.sprite.name);
            if (raio <= 0f) return -1f;

            float bmax = Mathf.Max(Mathf.Max(b.x, b.y), Mathf.Max(b.z, b.w));
            float mult = bmax / raio;

            float menorLado = Mathf.Min(tamanho.x, tamanho.y);
            if (menorLado > 1f)
                mult = Mathf.Max(mult, bmax / (FRACAO_MAX * menorLado));

            return Mathf.Round(mult * 100f) / 100f;
        }

        /// <summary>
        /// Corrige as telas da cena aberta e propaga para os prefabs de origem, usando o
        /// tamanho já resolvido pelos Layout Groups na cena.
        /// </summary>
        [MenuItem("Party Racers/UI/Corrigir 9-slice (cena + prefabs)")]
        public static void CorrigirCenaEPrefabs()
        {
            var log = new StringBuilder();
            var canvas = UICaptureTool.CanvasRaiz();
            if (canvas == null) { Debug.LogError("[9slice] nenhum Canvas raiz na cena"); return; }

            // resolve layout no espaço de autoria antes de medir (guarda para restaurar depois)
            var ativacao = new Dictionary<GameObject, bool>();
            foreach (Transform tela in canvas.transform)
            {
                ativacao[tela.gameObject] = tela.gameObject.activeSelf;
                tela.gameObject.SetActive(true);
            }
            Canvas.ForceUpdateCanvases();
            Rebuild(canvas.transform as RectTransform);
            Canvas.ForceUpdateCanvases();

            // caminho -> ajuste desejado, por prefab de origem
            var porPrefab = new Dictionary<string, Dictionary<string, Ajuste>>();
            int mudadosCena = 0, overlays = 0;

            foreach (Transform tela in canvas.transform)
            {
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(tela.gameObject);

                void Registrar(Image img, Ajuste a)
                {
                    if (string.IsNullOrEmpty(prefabPath)) return;
                    if (!porPrefab.TryGetValue(prefabPath, out var mapa))
                        porPrefab[prefabPath] = mapa = new Dictionary<string, Ajuste>();
                    mapa[CaminhoRelativo(img.transform, tela)] = a;
                }

                foreach (var img in tela.GetComponentsInChildren<Image>(true))
                {
                    // overlays de tela cheia não são molduras: 9-slice neles não faz sentido
                    if (img.sprite != null && img.sprite.name.StartsWith("Overlay_"))
                    {
                        if (img.type != Image.Type.Simple) { img.type = Image.Type.Simple; overlays++; EditorUtility.SetDirty(img); }
                        Registrar(img, new Ajuste { simples = true });
                        continue;
                    }

                    var rect = (img.transform as RectTransform).rect.size;
                    float ideal = Ideal(img, rect);
                    if (ideal < 0f) continue;

                    if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, ideal))
                    {
                        img.pixelsPerUnitMultiplier = ideal;
                        EditorUtility.SetDirty(img);
                        mudadosCena++;
                    }

                    Registrar(img, new Ajuste { multiplicador = ideal });
                }
            }

            log.AppendLine($"cena: {mudadosCena} images ajustadas, {overlays} overlays para Simple");

            foreach (var kv in porPrefab)
                log.AppendLine(AplicarNoPrefab(kv.Key, kv.Value));

            // devolve a ativação original das telas
            foreach (var kv in ativacao)
                if (kv.Key != null) kv.Key.SetActive(kv.Value);
            Canvas.ForceUpdateCanvases();

            EditorSceneManagerSave();
            Debug.Log("[9slice] " + log);
        }

        /// <summary>Corrige os prefabs de Widgets/ e Items/, que não têm instância na cena.</summary>
        [MenuItem("Party Racers/UI/Corrigir 9-slice (widgets e items)")]
        public static void CorrigirWidgetsEItems()
        {
            var log = new StringBuilder();
            foreach (var pasta in new[] { "Assets/_Projeto/Prefabs/UI/Widgets", "Assets/_Projeto/Prefabs/UI/Items" })
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { pasta }))
                {
                    var caminho = AssetDatabase.GUIDToAssetPath(guid);
                    var raiz = PrefabUtility.LoadPrefabContents(caminho);
                    int n = 0, ov = 0;
                    try
                    {
                        foreach (var img in raiz.GetComponentsInChildren<Image>(true))
                        {
                            if (img.sprite != null && img.sprite.name.StartsWith("Overlay_"))
                            {
                                if (img.type != Image.Type.Simple) { img.type = Image.Type.Simple; ov++; }
                                continue;
                            }
                            var rect = (img.transform as RectTransform).rect.size;
                            float ideal = Ideal(img, rect);
                            if (ideal < 0f) continue;
                            if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, ideal)) { img.pixelsPerUnitMultiplier = ideal; n++; }
                        }
                        if (n > 0 || ov > 0) PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(raiz); }
                    if (n > 0 || ov > 0) log.AppendLine($"  {System.IO.Path.GetFileName(caminho)}: {n} ajustadas, {ov} overlays");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[9slice widgets/items]\n" + log);
        }

        /// <summary>Ajuste a gravar no prefab: ou vira Simple (overlay), ou recebe o multiplicador.</summary>
        struct Ajuste
        {
            public float multiplicador;
            public bool simples;
        }

        static string AplicarNoPrefab(string caminho, Dictionary<string, Ajuste> mapa)
        {
            var raiz = PrefabUtility.LoadPrefabContents(caminho);
            int n = 0;
            try
            {
                foreach (var kv in mapa)
                {
                    var t = string.IsNullOrEmpty(kv.Key) ? raiz.transform : raiz.transform.Find(kv.Key);
                    var img = t != null ? t.GetComponent<Image>() : null;
                    if (img == null) continue;

                    if (kv.Value.simples)
                    {
                        if (img.type != Image.Type.Simple) { img.type = Image.Type.Simple; n++; }
                        continue;
                    }

                    if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, kv.Value.multiplicador))
                    {
                        img.pixelsPerUnitMultiplier = kv.Value.multiplicador;
                        n++;
                    }
                }
                if (n > 0) PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
            return $"  {System.IO.Path.GetFileName(caminho)}: {n} ajustadas";
        }

        static string CaminhoRelativo(Transform alvo, Transform raiz)
        {
            if (alvo == raiz) return "";
            var partes = new List<string>();
            for (var t = alvo; t != null && t != raiz; t = t.parent) partes.Add(t.name);
            partes.Reverse();
            return string.Join("/", partes);
        }

        static void Rebuild(RectTransform r)
        {
            if (r == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(r);
            foreach (Transform f in r) if (f is RectTransform rf) Rebuild(rf);
        }

        static void EditorSceneManagerSave()
        {
            var cena = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (cena.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(cena);
        }
    }
}
#endif
