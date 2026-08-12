using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace PartyRacers.UI.Tests
{
    public class FrontendUiStructureTest
    {
        const string UiRoot = "Assets/_Projeto/UI";
        const string FrontendScene = "Assets/_Projeto/Scenes/Frontend.unity";
        const string DemoScene = "Assets/_Projeto/Scenes/DemoTrack/DEMO.unity";
        const string RaceHudV2 = "Assets/_Projeto/Prefabs/UI_v2/Screens/Screen_RaceHUD_PC.prefab";

        [Test]
        public void FrontendUsaUmUidocumentESemCanvas()
        {
            Scene scene = SceneManager.GetSceneByPath(FrontendScene);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
                scene = EditorSceneManager.OpenScene(FrontendScene, OpenSceneMode.Additive);

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                UIDocument[] documents = roots.SelectMany(x => x.GetComponentsInChildren<UIDocument>(true)).ToArray();
                Canvas[] canvases = roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).ToArray();

                Assert.That(documents, Has.Length.EqualTo(1), "O frontend deve ter exatamente um UIDocument.");
                Assert.That(documents[0].gameObject.name, Is.EqualTo("UI_Frontend"));
                Assert.That(canvases, Is.Empty, "Canvas/uGUI voltou para a cena de frontend.");
                Assert.That(roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Any(x => x.name == "FrontendBackdrop"), Is.False,
                    "O fundo de imagem nao deve voltar para a cena Frontend.");

                Transform platform = roots.SelectMany(x => x.GetComponentsInChildren<Transform>(true))
                    .Single(x => x.name == "StagePlatform");
                Assert.That(platform.gameObject.activeSelf, Is.False,
                    "A base do carro deve permanecer removida do palco.");

                PartyRacers.UI.Motion.CarStage carStage = roots
                    .SelectMany(x => x.GetComponentsInChildren<PartyRacers.UI.Motion.CarStage>(true))
                    .Single();
                Assert.That(new SerializedObject(carStage).FindProperty("exibicaoEstatica").boolValue, Is.True,
                    "O carro do frontend deve ficar parado na pose diagonal.");
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ShellTemAbaPrivadaEBarraAcimaDasTelas()
        {
            TemplateContainer shell = Instantiate("Frontend/Shell/Shell.uxml");
            VisualElement root = shell.Q<VisualElement>("Shell");
            VisualElement screenHost = shell.Q<VisualElement>("Screen_Host");
            VisualElement topBar = shell.Q<VisualElement>("TopBar");
            VisualElement overlayHost = shell.Q<VisualElement>("Overlay_Host");

            Assert.That(shell.Q<Button>("Tab_Private_Btn"), Is.Not.Null,
                "A sala privada precisa estar acessivel pela barra superior.");
            Assert.That(root.IndexOf(screenHost), Is.LessThan(root.IndexOf(topBar)),
                "Screen_Host nao pode capturar cliques acima da TopBar.");
            Assert.That(root.IndexOf(overlayHost), Is.GreaterThan(root.IndexOf(topBar)),
                "O modal de matchmaking precisa continuar acima da TopBar.");
        }

        [Test]
        public void DemoUsaOHudV2ComDadosConectados()
        {
            Scene scene = SceneManager.GetSceneByPath(DemoScene);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            bool previousIgnore = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            try
            {
                if (openedForTest)
                    scene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Additive);

                GameObject canvas = scene.GetRootGameObjects().Single(x => x.name == "Canvas_UI");
                GameObject hud = canvas.transform.Cast<Transform>()
                    .Select(x => x.gameObject)
                    .Single(x => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(x) == RaceHudV2);

                int connected = 0;
                foreach (MonoBehaviour component in hud.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    SerializedProperty data = new SerializedObject(component).FindProperty("dados");
                    if (data == null) continue;
                    connected++;
                    Assert.That(data.objectReferenceValue, Is.Not.Null,
                        $"Provider ausente em {component.GetType().Name}.");
                }

                Assert.That(connected, Is.GreaterThanOrEqualTo(1),
                    "O HUD UI_v2 precisa consumir o RaceHUDDataProvider da cena.");
            }
            finally
            {
                if (openedForTest)
                    EditorSceneManager.CloseScene(scene, true);
                LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        [Test]
        public void UxmlMantemQuantidadesFixasDoGoldenMaster()
        {
            TemplateContainer lobby = Instantiate("Frontend/Lobby/Lobby.uxml");
            TemplateContainer matchmaking = Instantiate("Frontend/Matchmaking/Matchmaking.uxml");
            TemplateContainer custom = Instantiate("Frontend/CustomMatch/CustomMatch.uxml");
            TemplateContainer garage = Instantiate("Frontend/Garage/Garage.uxml");

            AssertNamedRange(lobby, "Group_Slot_", 4);
            AssertNamedRange(matchmaking, "Slot_", 16);
            AssertNamedRange(custom, "Slot_", 16);

            string[] categories =
            {
                "Tab_Modelo", "Tab_Cor", "Tab_Rodas", "Tab_Frente",
                "Tab_Traseira", "Tab_Escape", "Tab_Farois", "Tab_Neblina",
                "Tab_Motor", "Tab_Teto", "Tab_Adesivos", "Tab_Piloto",
            };
            foreach (string name in categories)
                Assert.That(garage.Q<VisualElement>(name), Is.Not.Null, $"Categoria ausente: {name}");

            for (int row = 0; row < 8; row++)
            {
                VisualElement gridRow = custom.Q<VisualElement>("Slot_Row_" + row);
                Assert.That(gridRow.childCount, Is.EqualTo(2),
                    $"A linha {row} da sala privada precisa ter exatamente duas colunas.");
                foreach (VisualElement slot in gridRow.Children())
                    Assert.That(slot.ClassListContains("custom__slot-host"), Is.True,
                        "Cada coluna precisa crescer ate ocupar metade da largura disponivel.");
            }

            Assert.That(custom.Q<Button>("Map_Prev")?.Q<VisualElement>(className: "pr-icon--left"), Is.Not.Null);
            Assert.That(custom.Q<Button>("Map_Next")?.Q<VisualElement>(className: "pr-icon--right"), Is.Not.Null);
            Assert.That(custom.Q<Button>("Laps_Minus")?.Q<VisualElement>(className: "pr-icon--left"), Is.Not.Null);
            Assert.That(custom.Q<Button>("Laps_Plus")?.Q<VisualElement>(className: "pr-icon--right"), Is.Not.Null);
            Assert.That(custom.Q<Button>("Btn_AddBots_Face").ClassListContains("custom__action-face"), Is.True);
            Assert.That(custom.Q<Button>("Btn_Start_Face").ClassListContains("custom__action-face"), Is.True);
        }

        [Test]
        public void FontesSaoTextCoreComAtlasPtBr()
        {
            uint[] requiredEverywhere =
            {
                0x00C1, 0x00C7, 0x00C9, 0x00CD, 0x00D3, 0x00DA,
                0x00E1, 0x00E3, 0x00E7, 0x00E9, 0x00ED, 0x00F3, 0x00F5, 0x00FA,
                0x00B7, 0x00BA, 0x2026, 0x25CF,
            };
            string[] names =
            {
                "TitanOne-Regular", "Archivo-Regular", "Archivo-SemiBold", "Archivo-Bold",
                "Archivo-ExtraBold", "Archivo-Black", "SpaceMono-Regular", "SpaceMono-Bold",
            };

            foreach (string name in names)
            {
                string path = $"{UiRoot}/Core/Fonts/{name} SDF.asset";
                FontAsset font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                Assert.That(font, Is.Not.Null, $"{path} precisa ser TextCore FontAsset para o UI Toolkit.");
                Assert.That(font.characterTable.Count, Is.GreaterThanOrEqualTo(100), $"Atlas incompleto: {path}");
                Assert.That(font.atlasTextures, Has.Length.GreaterThanOrEqualTo(1), $"Atlas ausente: {path}");
                foreach (uint codepoint in requiredEverywhere)
                    Assert.That(font.HasCharacter(codepoint), Is.True,
                        $"Glifo U+{codepoint:X4} ausente: {path}");

                if (name != "TitanOne-Regular")
                {
                    Assert.That(font.HasCharacter(0x2190), Is.True, $"Seta esquerda ausente: {path}");
                    Assert.That(font.HasCharacter(0x2192), Is.True, $"Seta direita ausente: {path}");
                }
            }
        }

        static TemplateContainer Instantiate(string relativePath)
        {
            string path = $"{UiRoot}/{relativePath}";
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, $"UXML ausente: {path}");
            return asset.Instantiate();
        }

        static void AssertNamedRange(VisualElement root, string prefix, int count)
        {
            for (int i = 0; i < count; i++)
                Assert.That(root.Q<VisualElement>(prefix + i), Is.Not.Null, $"Ausente: {prefix}{i}");

            Assert.That(root.Q<VisualElement>(prefix + count), Is.Null,
                $"Quantidade fixa alterada: existe {prefix}{count}.");
        }
    }
}
