#if UNITY_EDITOR
using System.Linq;
using PartyRacers.UI.Frontend;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Instala a tela de carregamento nas duas pontas: no Boot (Boot -> Frontend) e no Frontend
    /// (Frontend -> pista). Antes o CORRER carregava de forma síncrona e a imagem congelava até
    /// a pista abrir, sem nenhum aviso na tela.
    /// </summary>
    public static class LoadingWiring
    {
        const string PREFAB_LOADING = "Assets/_Projeto/Prefabs/UI/Screens/Screen_Loading.prefab";

        [MenuItem("Party Racers/UI/Instalar tela de carregamento")]
        public static void Instalar()
        {
            Frontend();
            Boot();
            AssetDatabase.SaveAssets();
        }

        static void Frontend()
        {
            var cena = EditorSceneManager.OpenScene("Assets/_Projeto/Scenes/Frontend.unity", OpenSceneMode.Single);
            var canvas = UICaptureTool.CanvasRaiz();
            if (canvas == null) { Debug.LogError("[carregando] Frontend sem canvas"); return; }

            // a tela de carregamento fica por último no canvas: precisa cobrir todas as outras
            var existente = canvas.transform.Find("Screen_Loading");
            GameObject loading;
            if (existente != null) loading = existente.gameObject;
            else
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_LOADING);
                loading = (GameObject)PrefabUtility.InstantiatePrefab(src, canvas.transform);
                var rt = (RectTransform)loading.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }
            loading.transform.SetAsLastSibling();

            var tela = BuildScenes.LigarCarregamento(loading);
            loading.SetActive(false);   // só aparece quando alguém pede

            var fluxo = Object.FindObjectsByType<FrontendFlow>(FindObjectsInactive.Include).FirstOrDefault();
            if (fluxo != null)
            {
                var so = new SerializedObject(fluxo);
                so.FindProperty("telaDeCarregamento").objectReferenceValue = tela;

                // reconfirma o seletor de mapa: reconstruir o prefab do lobby pode ter soltado
                // a referência, e sem ela o CORRER cai na pista padrão e ignora a escolha
                var seletor = canvas.transform.Find("Screen_Lobby/Painel_Mapa")?.GetComponent<TrackSelectUI>();
                so.FindProperty("seletorDePista").objectReferenceValue = seletor;
                so.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log("[carregando] Frontend: tela ligada; seletor de pista = " +
                          (seletor != null ? "ok" : "NULO"));
            }

            // o roteador não deve tratar a tela de carregamento como uma tela navegável
            var router = Object.FindObjectsByType<ScreenRouter>(FindObjectsInactive.Include).FirstOrDefault();
            if (router != null)
            {
                var so = new SerializedObject(router);
                var telas = so.FindProperty("telas");
                for (int i = telas.arraySize - 1; i >= 0; i--)
                {
                    var raiz = telas.GetArrayElementAtIndex(i).FindPropertyRelative("raiz").objectReferenceValue;
                    if (raiz == loading) telas.DeleteArrayElementAtIndex(i);
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
        }

        static void Boot()
        {
            var cena = EditorSceneManager.OpenScene("Assets/_Projeto/Scenes/Boot.unity", OpenSceneMode.Single);
            var loading = GameObject.Find("Screen_Loading");
            if (loading == null)
            {
                var canvas = UICaptureTool.CanvasRaiz();
                loading = canvas != null ? canvas.transform.Find("Screen_Loading")?.gameObject : null;
            }
            if (loading == null) { Debug.LogError("[carregando] Boot sem Screen_Loading"); return; }

            loading.SetActive(true);
            var tela = BuildScenes.LigarCarregamento(loading);

            var boot = Object.FindObjectsByType<BootLoader>(FindObjectsInactive.Include).FirstOrDefault();
            if (boot == null) boot = loading.AddComponent<BootLoader>();

            var so = new SerializedObject(boot);
            so.FindProperty("tela").objectReferenceValue = tela;
            so.FindProperty("cenaDestino").stringValue = "Frontend";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log("[carregando] Boot: BootLoader ligado à tela animada");
        }
    }
}
#endif
