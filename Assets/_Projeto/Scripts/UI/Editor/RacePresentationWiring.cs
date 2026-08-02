#if UNITY_EDITOR
using System.Linq;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.HUD;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Race;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Instala, sem remontar a pista, as peças de apresentação que precisam viver numa cena de corrida.
    /// Mantém o binder da contagem em um objeto ativo e põe o loading por cima de todo o HUD.
    /// </summary>
    public static class RacePresentationWiring
    {
        private const string LoadingPrefab = "Assets/_Projeto/Prefabs/UI/Screens/Screen_Loading.prefab";

        [MenuItem("Party Racers/UI/Instalar apresentação na corrida atual")]
        public static void InstallInActiveScene()
        {
            Scene scene = SceneManager.GetActiveScene();
            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene && candidate.name == "Canvas_UI");

            if (canvas == null)
                throw new System.InvalidOperationException("A cena ativa não possui o Canvas_UI.");

            ConfigureCountdown(canvas.gameObject);
            ConfigureResult(canvas.transform, scene);
            LoadingScreenUI loading = EnsureLoading(canvas.transform);
            ConfigureSceneTransitions(scene, loading);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Race Presentation] Contagem e loading instalados em {scene.name}.");
        }

        private static void ConfigureResult(Transform canvas, Scene scene)
        {
            Transform screen = canvas.Find("Screen_Result");
            if (screen == null)
                return;

            RaceHUDDataProvider provider = Object.FindObjectsByType<RaceHUDDataProvider>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate.gameObject.scene == scene);

            ResultScreenLayoutBuilder.Configure(screen.gameObject);
            BuildScenes.LigarResultado(screen.gameObject, provider);
        }

        private static void ConfigureCountdown(GameObject host)
        {
            Transform screen = host.transform.Find("Screen_Countdown");
            if (screen == null)
                throw new System.InvalidOperationException("Screen_Countdown não foi encontrada no Canvas_UI.");

            foreach (CountdownUI oldBinder in screen.GetComponents<CountdownUI>())
                Object.DestroyImmediate(oldBinder, true);

            CountdownUI binder = host.GetComponent<CountdownUI>();
            if (binder == null)
                binder = host.AddComponent<CountdownUI>();

            GameObject state3 = Find(screen, "Centro/State_3");
            GameObject state2 = Find(screen, "Centro/State_2");
            GameObject state1 = Find(screen, "Centro/State_1");
            GameObject stateGo = Find(screen, "Centro/State_Go");

            var serialized = new SerializedObject(binder);
            serialized.FindProperty("raiz").objectReferenceValue = screen.gameObject;
            serialized.FindProperty("passo3").objectReferenceValue = state3;
            serialized.FindProperty("passo2").objectReferenceValue = state2;
            serialized.FindProperty("passo1").objectReferenceValue = state1;
            serialized.FindProperty("passoJa").objectReferenceValue = stateGo;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ConfigureCountdownMotion(state3);
            ConfigureCountdownMotion(state2);
            ConfigureCountdownMotion(state1);
            ConfigureCountdownMotion(stateGo);
            screen.gameObject.SetActive(false);
        }

        private static void ConfigureCountdownMotion(GameObject state)
        {
            if (state == null)
                return;

            UIAppear appear = state.GetComponent<UIAppear>();
            if (appear == null)
                appear = state.AddComponent<UIAppear>();

            var serialized = new SerializedObject(appear);
            serialized.FindProperty("duracao").floatValue = 0.22f;
            serialized.FindProperty("atraso").floatValue = 0f;
            serialized.FindProperty("atrasoPorIrmao").floatValue = 0f;
            serialized.FindProperty("deslocamento").vector2Value = Vector2.zero;
            serialized.FindProperty("escalaInicial").floatValue = 0.64f;
            serialized.FindProperty("comFade").boolValue = true;
            serialized.FindProperty("comRecuo").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static LoadingScreenUI EnsureLoading(Transform canvas)
        {
            Transform existing = canvas.Find("Screen_Loading");
            GameObject loading;

            if (existing != null)
            {
                loading = existing.gameObject;
            }
            else
            {
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingPrefab);
                if (source == null)
                    throw new System.InvalidOperationException("Prefab Screen_Loading não foi encontrado.");

                loading = (GameObject)PrefabUtility.InstantiatePrefab(source, canvas);
            }

            if (loading.transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
            }

            loading.transform.SetAsLastSibling();
            LoadingScreenUI ui = BuildScenes.LigarCarregamento(loading);
            loading.SetActive(false);
            return ui;
        }

        private static void ConfigureSceneTransitions(Scene scene, LoadingScreenUI loading)
        {
            foreach (RaceMenuUI menu in Object.FindObjectsByType<RaceMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (menu.gameObject.scene != scene) continue;
                SetLoadingReference(menu, loading);
            }

            foreach (RaceResultUI result in Object.FindObjectsByType<RaceResultUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (result.gameObject.scene != scene) continue;
                SetLoadingReference(result, loading);
            }
        }

        private static void SetLoadingReference(Object target, LoadingScreenUI loading)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty("telaDeCarregamento");
            if (property == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} não expõe telaDeCarregamento.");
            property.objectReferenceValue = loading;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Find(Transform root, string path)
        {
            Transform result = root.Find(path);
            return result != null ? result.gameObject : null;
        }
    }
}
#endif
