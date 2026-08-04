using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Frontend.Party;
using PartyRacers.UI.Garage;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Coloca as telas v2 dentro da <c>Frontend.unity</c> e liga o que só existe na CENA: o
    /// roteador, o grupo, o carro do palco 3D e o fluxo.
    ///
    /// As telas do v1 não são apagadas — são desligadas e renomeadas com sufixo <c>_v1</c>. Elas
    /// continuam referenciadas pelo <see cref="FrontendFlow"/>, que escreve nelas sem prejuízo, e a
    /// volta atrás é reativar duas GameObjects em vez de recuperar arquivo de backup.
    ///
    /// A cena é aberta em modo ADITIVO de propósito: abrir em Single descarta trabalho não salvo de
    /// quem estiver com o Editor aberto.
    /// </summary>
    public static class FrontendInstaller
    {
        private const string CenaFrontend = "Assets/_Projeto/Scenes/Frontend.unity";
        private const string PrefabRoot = "Assets/_Projeto/Prefabs/UI_v2/Screens";

        [MenuItem("Party Racers/UI v2/6 · Instalar as telas no Frontend", priority = 21)]
        public static void Instalar()
        {
            var log = new StringBuilder();

            Scene cena = EditorSceneManager.OpenScene(CenaFrontend, OpenSceneMode.Additive);
            GameObject[] raizes = cena.GetRootGameObjects();

            Transform canvas = raizes.FirstOrDefault(g => g.name == "Canvas_UI")?.transform;
            var roteador = raizes.Select(g => g.GetComponent<ScreenRouter>()).FirstOrDefault(c => c != null);
            var fluxo = raizes.Select(g => g.GetComponent<FrontendFlow>()).FirstOrDefault(c => c != null);

            if (canvas == null || roteador == null)
            {
                Debug.LogError("[UI v2] Frontend.unity sem Canvas_UI ou ScreenRouter.");
                EditorSceneManager.CloseScene(cena, true);
                return;
            }

            // ---- telas antigas ficam desligadas, não apagadas
            foreach ((string antigo, string novo) in new[]
                     { ("Screen_Lobby", "Screen_Lobby_v1"), ("Screen_Garage_PC", "Screen_Garage_v1") })
            {
                Transform t = canvas.Find(antigo);
                if (t == null)
                    continue;

                t.name = novo;
                t.gameObject.SetActive(false);
                log.AppendLine($"  {antigo} → {novo} (desligada)");
            }

            // ---- telas novas
            GameObject lobby = Colocar(canvas, "Screen_Lobby", log);
            GameObject garagem = Colocar(canvas, "Screen_Garage", log);
            GameObject sala = Colocar(canvas, "Screen_CustomMatch", log);
            GameObject busca = Colocar(canvas, "Screen_Matchmaking", log);

            // O matchmaking é MODAL sobre o lobby (§3 da proposta): fica por cima e começa
            // desligado. Não é destino do roteador — cancelar é fechar, não é voltar.
            if (busca != null)
            {
                busca.transform.SetAsLastSibling();
                busca.SetActive(false);
            }

            // ---- grupo
            var controlador = raizes.Select(g => g.GetComponentInChildren<PartyController>(true))
                                    .FirstOrDefault(c => c != null);

            if (controlador == null)
            {
                var go = new GameObject("Party");
                SceneManager.MoveGameObjectToScene(go, cena);
                controlador = go.AddComponent<PartyController>();
                var mm = go.AddComponent<MatchmakingService>();
                Referencia(controlador, "matchmaking", mm);
                Referencia(controlador, "fluxo", fluxo);
                log.AppendLine("  + Party (PartyController + MatchmakingService)");
            }

            // ---- ligações que só a cena conhece
            foreach (GameObject tela in new[] { lobby, garagem, sala, busca })
            {
                if (tela == null)
                    continue;

                foreach (NavBarUI nav in tela.GetComponentsInChildren<NavBarUI>(true))
                    nav.DefinirRoteador(roteador);
            }

            if (lobby != null)
            {
                var ui = lobby.GetComponent<PublicLobbyScreenUI>();
                Referencia(ui, "controlador", controlador);
                Referencia(ui, "modalDeBusca", busca != null ? busca.GetComponent<MatchmakingModalUI>() : null);
            }

            if (busca != null)
                Referencia(busca.GetComponent<MatchmakingModalUI>(), "controlador", controlador);

            if (sala != null)
                Referencia(sala.GetComponent<CustomMatchScreenUI>(), "fluxo", fluxo);

            if (garagem != null)
                LigarGaragem(garagem, raizes, log);

            // ---- roteador
            RegistrarTelas(roteador, new[]
            {
                ("Lobby", lobby),
                ("Garagem", garagem),
                ("CustomMatch", sala),
            }, log);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            EditorSceneManager.CloseScene(cena, true);

            Debug.Log("[UI v2] Frontend atualizado:\n" + log);
        }

        // ------------------------------------------------------------------ Peças

        private static GameObject Colocar(Transform canvas, string nome, StringBuilder log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{nome}.prefab");
            if (asset == null)
            {
                log.AppendLine($"  ! prefab ausente: {nome}");
                return null;
            }

            // Reinstalar substitui a instância anterior: manter as duas faria dois binders
            // disputarem o mesmo grupo, e a tela passaria a discordar de si mesma.
            Transform antiga = canvas.Find(nome);
            if (antiga != null)
                Object.DestroyImmediate(antiga.gameObject);

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas);
            instancia.name = nome;

            var r = (RectTransform)instancia.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.localScale = Vector3.one;

            log.AppendLine($"  + {nome}");
            return instancia;
        }

        private static void LigarGaragem(GameObject garagem, GameObject[] raizes, StringBuilder log)
        {
            var grid = garagem.GetComponent<GarageGridUI>();
            if (grid == null)
                return;

            // O carro do palco já existe na cena (Turntable/PreviewCar) e é o mesmo que o lobby
            // mostra. A garagem só passa a mandar nele.
            var carro = raizes.Select(g => g.GetComponentInChildren<KartVisualCustomizer>(true))
                              .FirstOrDefault(c => c != null);
            Referencia(grid, "carro", carro);

            var rig = raizes.Select(g => g.GetComponentInChildren<GarageCameraRig>(true))
                            .FirstOrDefault(c => c != null);

            if (rig == null)
            {
                Camera cam = raizes.Select(g => g.GetComponent<Camera>()).FirstOrDefault(c => c != null);
                if (cam != null)
                {
                    rig = cam.gameObject.AddComponent<GarageCameraRig>();
                    log.AppendLine("  + GarageCameraRig na câmera do frontend");
                }
            }

            Referencia(grid, "camera3D", rig);
        }

        private static void RegistrarTelas(ScreenRouter roteador,
                                           (string id, GameObject raiz)[] novas, StringBuilder log)
        {
            var so = new SerializedObject(roteador);
            SerializedProperty telas = so.FindProperty("telas");

            foreach ((string id, GameObject raiz) in novas)
            {
                if (raiz == null)
                    continue;

                SerializedProperty alvo = null;
                for (int i = 0; i < telas.arraySize; i++)
                {
                    SerializedProperty t = telas.GetArrayElementAtIndex(i);
                    if (t.FindPropertyRelative("id").stringValue == id)
                    {
                        alvo = t;
                        break;
                    }
                }

                if (alvo == null)
                {
                    telas.arraySize++;
                    alvo = telas.GetArrayElementAtIndex(telas.arraySize - 1);
                    alvo.FindPropertyRelative("id").stringValue = id;
                    log.AppendLine($"  roteador: + {id}");
                }
                else
                {
                    log.AppendLine($"  roteador: {id} → tela v2");
                }

                alvo.FindPropertyRelative("raiz").objectReferenceValue = raiz;
                alvo.FindPropertyRelative("grupo").objectReferenceValue = raiz.GetComponent<CanvasGroup>();
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Referencia(Object alvo, string campo, Object valor)
        {
            if (alvo == null)
                return;

            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = valor;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
