#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.HUD;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Converte a tabela fixa de demonstração da tela de resultado em um viewport responsivo.
    /// A fonte visual continua sendo o prefab Item_ResultRow; as linhas passam a existir apenas
    /// quando ResultScreenUI recebe os standings reais da corrida.
    /// </summary>
    public static class ResultScreenLayoutBuilder
    {
        private const string ResultPrefab = "Assets/_Projeto/Prefabs/UI/Screens/Screen_Result.prefab";

        [MenuItem("Party Racers/UI/Atualizar resultado responsivo")]
        public static void Install()
        {
            ConfigurePrefabAsset();
            ConfigureActiveScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Race Result] Tabela responsiva, sem mocks e com fundo translúcido instalada.");
        }

        public static void Configure(GameObject screen)
        {
            if (screen == null)
                return;

            Image background = Find<Image>(screen.transform, "Fundo");
            if (background != null)
            {
                Color color = background.color;
                color.a = 0.72f;
                background.color = color;
                background.raycastTarget = true;
            }

            Transform table = screen.transform.Find("Tabela");
            if (table == null || table is not RectTransform viewport)
                throw new System.InvalidOperationException("Screen_Result/Tabela não foi encontrada.");

            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(56f, 170f);
            viewport.offsetMax = new Vector2(-56f, -196f);
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.localScale = Vector3.one;

            Transform existingContent = table.Find("Conteudo");
            RectTransform content;
            if (existingContent != null)
            {
                content = (RectTransform)existingContent;
            }
            else
            {
                var contentGo = new GameObject("Conteudo", typeof(RectTransform));
                content = contentGo.GetComponent<RectTransform>();
                content.SetParent(table, false);
            }

            // Migra apenas cabeçalhos/linhas antigos. Elementos próprios do viewport permanecem fora.
            var toMove = new List<Transform>();
            foreach (Transform child in table)
            {
                if (child == content || child.name == "Scrollbar_Vertical")
                    continue;
                toMove.Add(child);
            }
            foreach (Transform child in toMove)
                child.SetParent(content, false);

            Transform oldScrollbar = table.Find("Scrollbar_Vertical");
            if (oldScrollbar != null)
                Object.DestroyImmediate(oldScrollbar.gameObject);

            // Os Linha_01..16 eram somente handoff visual. Manter esses objetos fazia a tela
            // exibir mocks e depois somar as linhas reais instanciadas pelo binder.
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (IsPreviewRow(child.name))
                    Object.DestroyImmediate(child.gameObject);
            }

            GridLayoutGroup oldGrid = table.GetComponent<GridLayoutGroup>();
            if (oldGrid != null)
                Object.DestroyImmediate(oldGrid);

            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.localScale = Vector3.one;

            GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>() ?? content.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.cellSize = new Vector2(886f, 68f);
            grid.spacing = new Vector2(18f, 9f);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                Object.DestroyImmediate(fitter);

            RectMask2D mask = table.GetComponent<RectMask2D>();
            if (mask != null)
                Object.DestroyImmediate(mask);

            ScrollRect scroll = table.GetComponent<ScrollRect>();
            if (scroll != null)
                Object.DestroyImmediate(scroll);
        }

        private static void ConfigurePrefabAsset()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(ResultPrefab);
            try
            {
                Configure(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, ResultPrefab);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        private static void ConfigureActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            RaceHUDDataProvider provider = Object.FindObjectsByType<RaceHUDDataProvider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene);

            RectTransform[] rects = Object.FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (RectTransform rect in rects)
            {
                if (rect == null || rect.gameObject.scene != scene || rect.name != "Screen_Result")
                    continue;

                Configure(rect.gameObject);
                BuildScenes.LigarResultado(rect.gameObject, provider);
            }

            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static bool IsPreviewRow(string name)
        {
            return !string.IsNullOrEmpty(name)
                && (name.StartsWith("Linha_", System.StringComparison.Ordinal)
                    || name.StartsWith("Item_ResultRow", System.StringComparison.Ordinal));
        }

        private static T Find<T>(Transform root, string path) where T : Component
        {
            Transform target = root.Find(path);
            return target != null ? target.GetComponent<T>() : null;
        }
    }
}
#endif
