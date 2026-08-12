using System.IO;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Frontend.Party;
using PartyRacers.UI.Garage;
using PartyRacers.UI.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace PartyRacers.UI.Editor
{
    /// <summary>Instalacao idempotente do frontend entregue em UIDropoff.</summary>
    public static class FrontendUiToolkitInstaller
    {
        const string ScenePath = "Assets/_Projeto/Scenes/Frontend.unity";
        const string UiRoot = "Assets/_Projeto/UI";
        const string FontCharacters =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 " +
            "\u00C0\u00C1\u00C2\u00C3\u00C4\u00C7\u00C9\u00CA\u00CB\u00CD\u00CE\u00CF\u00D3\u00D4\u00D5\u00D6\u00DA\u00DB\u00DC" +
            "\u00E0\u00E1\u00E2\u00E3\u00E4\u00E7\u00E9\u00EA\u00EB\u00ED\u00EE\u00EF\u00F3\u00F4\u00F5\u00F6\u00FA\u00FB\u00FC" +
            ".,:;!?+-*/()[]{}<>%&'\"#@_=\\|~`^$" +
            "\u00A0\u00AA\u00B7\u00BA\u20AC\u2018\u2019\u201C\u201D\u2022\u2026\u2039\u203A\u2190\u2192\u2212\u25CF\u2713";
        const string PanelPath = UiRoot + "/Frontend/FrontendPanelSettings.asset";
        const string StageFolder = UiRoot + "/Frontend/Stage";

        [MenuItem("Party Racers/UI Toolkit/Instalar Frontend")]
        public static void Install()
        {
            EnsureFonts();
            ConfigureTextures();
            EnsureFolder(StageFolder);

            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject legacyCanvas = FindRoot(scene, "Canvas_UI");
            if (legacyCanvas != null) Object.DestroyImmediate(legacyCanvas);
            GameObject legacyBackground = FindRoot(scene, "Canvas_Fundo");
            if (legacyBackground != null) Object.DestroyImmediate(legacyBackground);

            GameObject systems = FindRoot(scene, "FrontendSystems") ?? FindRoot(scene, "ScreenRouter");
            if (systems == null) systems = new GameObject("FrontendSystems");
            systems.name = "FrontendSystems";

            var legacyRouter = systems.GetComponent<PartyRacers.UI.Frontend.ScreenRouter>();
            if (legacyRouter != null) Object.DestroyImmediate(legacyRouter);

            FrontendFlow flow = systems.GetComponent<FrontendFlow>();
            PartyRacers.UI.Garage.PreviewStudio preview = systems.GetComponent<PartyRacers.UI.Garage.PreviewStudio>();
            PartyController party = Object.FindAnyObjectByType<PartyController>(FindObjectsInactive.Include);
            KartVisualCustomizer kart = Object.FindAnyObjectByType<KartVisualCustomizer>(FindObjectsInactive.Include);
            GarageCameraRig garageCamera = Object.FindAnyObjectByType<GarageCameraRig>(FindObjectsInactive.Include);
            Camera camera = Camera.main ?? Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            GameObject turntable = FindRoot(scene, "Turntable");

            if (flow == null || preview == null || party == null || kart == null || garageCamera == null || camera == null || turntable == null)
                throw new System.InvalidOperationException("Frontend incompleto: fluxo, party, kart, camera ou estudio nao foram encontrados.");

            RemoveImageBackdrop(camera);
            BuildPlatform(turntable.transform, kart);

            GameObject ui = FindRoot(scene, "UI_Frontend") ?? new GameObject("UI_Frontend");
            UIDocument document = GetOrAdd<UIDocument>(ui);
            FrontendRouter router = GetOrAdd<FrontendRouter>(ui);
            LobbyController lobby = GetOrAdd<LobbyController>(ui);
            GarageController garage = GetOrAdd<GarageController>(ui);
            CustomMatchController custom = GetOrAdd<CustomMatchController>(ui);
            MatchmakingController matchmaking = GetOrAdd<MatchmakingController>(ui);
            StagePresenter stage = GetOrAdd<StagePresenter>(ui);

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            document.panelSettings = panel;
            document.visualTreeAsset = Load<VisualTreeAsset>(UiRoot + "/Frontend/Shell/Shell.uxml");
            document.sortingOrder = 100;

            Set(router, "lobby", Load<VisualTreeAsset>(UiRoot + "/Frontend/Lobby/Lobby.uxml"));
            Set(router, "garage", Load<VisualTreeAsset>(UiRoot + "/Frontend/Garage/Garage.uxml"));
            Set(router, "customMatch", Load<VisualTreeAsset>(UiRoot + "/Frontend/CustomMatch/CustomMatch.uxml"));
            Set(router, "matchmaking", Load<VisualTreeAsset>(UiRoot + "/Frontend/Matchmaking/Matchmaking.uxml"));
            Set(router, "lobbyController", lobby);
            Set(router, "garageController", garage);
            Set(router, "customController", custom);
            Set(router, "mmController", matchmaking);
            Set(router, "stage", stage);
            Set(router, "flow", flow);

            Set(lobby, "stage", stage);
            Set(lobby, "router", router);
            Set(lobby, "partyController", party);
            Set(lobby, "friendTemplate", Load<VisualTreeAsset>(UiRoot + "/Shared/Templates/Row_Friend.uxml"));

            Set(garage, "itemTemplate", Load<VisualTreeAsset>(UiRoot + "/Shared/Templates/Card_Item.uxml"));
            Set(garage, "previewStudio", preview);
            Set(garage, "stage", stage);
            Set(garage, "customizer", kart);
            Set(garage, "flow", flow);

            Set(matchmaking, "blipTemplate", Load<VisualTreeAsset>(UiRoot + "/Shared/Templates/Blip.uxml"));
            Set(matchmaking, "router", router);
            Set(matchmaking, "partyController", party);
            Set(matchmaking, "flow", flow);

            Set(custom, "flow", flow);
            Set(custom, "previewStudio", preview);
            SetArray(custom, "tracks",
                Load<TrackDefinition>("Assets/_Projeto/Settings/Tracks/Pista_MiniGolfeRun.asset"),
                Load<TrackDefinition>("Assets/_Projeto/Settings/Tracks/Pista_DEMO.asset"));

            Set(stage, "stageRoot", turntable.transform);
            Set(stage, "playerKartAnchor", kart.transform);
            SetArray<Transform>(stage, "mateKartAnchors");
            Set(stage, "stageCamera", camera);
            Set(stage, "kart", kart);
            Set(stage, "garageCamera", garageCamera);
            Set(stage, "flow", flow);

            ClearLegacyPresentation(flow);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(ui);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = ui;
            Debug.Log("[UI Toolkit] Frontend instalado: um UIDocument, palco 3D e quatro fluxos conectados.");
        }

        static void ClearLegacyPresentation(FrontendFlow flow)
        {
            var so = new SerializedObject(flow);
            string[] fields = { "roteador", "lobby", "garagem", "joinCode", "loja", "palco", "cameraDoPalco", "rigDaGaragem" };
            foreach (string field in fields)
            {
                SerializedProperty property = so.FindProperty(field);
                if (property != null) property.objectReferenceValue = null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(flow);
        }

        static void RemoveImageBackdrop(Camera camera)
        {
            Transform old = camera.transform.Find("FrontendBackdrop");
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        static void BuildPlatform(Transform stage, KartVisualCustomizer kart)
        {
            Transform old = stage.Find("StagePlatform");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            kart.EnsureBuilt();
            Bounds bounds = new Bounds(stage.position, Vector3.one);
            bool found = false;
            foreach (Renderer renderer in kart.GetComponentsInChildren<Renderer>(true))
            {
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            Vector3 localCenter = stage.InverseTransformPoint(bounds.center);
            float localFloor = stage.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)).y;
            var root = new GameObject("StagePlatform");
            root.transform.SetParent(stage, false);
            root.transform.localPosition = new Vector3(localCenter.x, localFloor - 0.14f, localCenter.z);

            Material amber = EnsureMaterial(StageFolder + "/M_StageAmber.mat", "Universal Render Pipeline/Lit");
            Material royal = EnsureMaterial(StageFolder + "/M_StageRoyal.mat", "Universal Render Pipeline/Lit");
            Material deep = EnsureMaterial(StageFolder + "/M_StageDeep.mat", "Universal Render Pipeline/Lit");
            SetMaterialColor(amber, new Color(1f, 0.69f, 0.13f));
            SetMaterialColor(royal, new Color(0.10f, 0.13f, 0.34f));
            SetMaterialColor(deep, new Color(0.04f, 0.05f, 0.14f));

            CreateDisc(root.transform, "OuterAmber", amber, new Vector3(3.35f, 0.08f, 2.65f), 0f);
            CreateDisc(root.transform, "OuterDeep", deep, new Vector3(3.16f, 0.09f, 2.47f), 0.06f);
            CreateDisc(root.transform, "InnerAmber", amber, new Vector3(2.54f, 0.10f, 1.94f), 0.12f);
            CreateDisc(root.transform, "InnerRoyal", royal, new Vector3(2.30f, 0.11f, 1.72f), 0.18f);
        }

        static void CreateDisc(Transform parent, string name, Material material, Vector3 scale, float y)
        {
            GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = name;
            disc.transform.SetParent(parent, false);
            disc.transform.localPosition = new Vector3(0f, y, 0f);
            disc.transform.localScale = scale;
            Object.DestroyImmediate(disc.GetComponent<Collider>());
            disc.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        static void EnsureFonts()
        {
            string[] names =
            {
                "TitanOne-Regular", "Archivo-Regular", "Archivo-SemiBold", "Archivo-Bold",
                "Archivo-ExtraBold", "Archivo-Black", "SpaceMono-Regular", "SpaceMono-Bold",
            };
            foreach (string name in names) EnsureFont(name);
        }

        static void EnsureFont(string name)
        {
            string assetPath = UiRoot + "/Core/Fonts/" + name + " SDF.asset";
            FontAsset existing = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
            if (existing != null && existing.characterTable != null &&
                existing.characterTable.Count > 100 && HasRequiredCharacters(existing))
                return;

            if (existing != null) AssetDatabase.DeleteAsset(assetPath);

            Font font = Load<Font>(UiRoot + "/Core/Fonts/" + name + ".ttf");
            FontAsset asset = FontAsset.CreateFontAsset(font, 64, 8,
                GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            asset.TryAddCharacters(FontCharacters, out string missing);
            AddCharacterAlias(asset, 0x25CF, 0x2022); // circulo cheio usa o bullet quando o TTF nao o fornece
            AssetDatabase.CreateAsset(asset, assetPath);
            foreach (Texture2D atlas in asset.atlasTextures)
                if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, asset);
            if (asset.material != null && !AssetDatabase.Contains(asset.material))
                AssetDatabase.AddObjectToAsset(asset.material, asset);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[UI Toolkit] {name}: glifos ausentes no TTF: {missing}");
        }

        static bool HasRequiredCharacters(FontAsset asset)
        {
            const string required = "AZ09\u00C1\u00C7\u00C9\u00CD\u00D3\u00DA\u00E1\u00E3\u00E7\u00E9\u00ED\u00F3\u00F5\u00FA\u00B7\u2026\u25CF";
            for (int i = 0; i < required.Length; i++)
                if (!asset.HasCharacter(required[i])) return false;
            return true;
        }

        static void AddCharacterAlias(FontAsset asset, uint unicode, uint sourceUnicode)
        {
            if (asset.HasCharacter(unicode) ||
                !asset.characterLookupTable.TryGetValue(sourceUnicode, out Character source))
                return;

            var alias = new Character(unicode, asset, source.glyph) { scale = source.scale };
            asset.characterTable.Add(alias);
            asset.characterLookupTable[unicode] = alias;
        }

        [MenuItem("Party Racers/UI Toolkit/Reparar Alias de Glifos")]
        public static void RepairFontAliases()
        {
            string[] names =
            {
                "TitanOne-Regular", "Archivo-Regular", "Archivo-SemiBold", "Archivo-Bold",
                "Archivo-ExtraBold", "Archivo-Black", "SpaceMono-Regular", "SpaceMono-Bold",
            };

            foreach (string name in names)
            {
                FontAsset asset = AssetDatabase.LoadAssetAtPath<FontAsset>(
                    UiRoot + "/Core/Fonts/" + name + " SDF.asset");
                if (asset == null) continue;
                AddCharacterAlias(asset, 0x25CF, 0x2022);
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        [MenuItem("Party Racers/UI Toolkit/Regerar Fontes")]
        public static void RebuildFonts()
        {
            string[] names =
            {
                "TitanOne-Regular", "Archivo-Regular", "Archivo-SemiBold", "Archivo-Bold",
                "Archivo-ExtraBold", "Archivo-Black", "SpaceMono-Regular", "SpaceMono-Bold",
            };
            foreach (string name in names)
            {
                string path = UiRoot + "/Core/Fonts/" + name + " SDF.asset";
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            }
            EnsureFonts();
            AssetDatabase.Refresh();
            Debug.Log("[UI Toolkit] Oito atlas de fonte regenerados com PT-BR e codigo de sala.");
        }

        static void ConfigureTextures()
        {
            string absolute = Path.GetFullPath(UiRoot + "/Core/Art");
            foreach (string file in Directory.GetFiles(absolute, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = file.Replace('\\', '/');
                assetPath = "Assets/" + assetPath.Substring(assetPath.IndexOf("Assets/", System.StringComparison.Ordinal) + 7);
                if (!(AssetImporter.GetAtPath(assetPath) is TextureImporter importer)) continue;

                string name = Path.GetFileNameWithoutExtension(file);
                bool repeat = name.Contains("Dash_Tile") || name.Contains("Stripes_") || name == "Dial_Lines";
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                if (name.StartsWith("Vignette_"))
                    importer.spriteBorder = new Vector4(236f, 236f, 236f, 236f);
                importer.SaveAndReimport();
            }
        }

        static Material EnsureMaterial(string path, string shaderName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName) ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SetMaterialColor(Material material, Color color)
        {
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
        }

        static void EnsureFolder(string path)
        {
            string current = "Assets";
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static T GetOrAdd<T>(GameObject gameObject) where T : Component
            => gameObject.GetComponent<T>() ?? gameObject.AddComponent<T>();

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new FileNotFoundException("Asset nao encontrado", path);
            return asset;
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field)
                ?? throw new System.MissingFieldException(target.GetType().Name, field);
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        static void SetArray<T>(Object target, string field, params T[] values) where T : Object
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field)
                ?? throw new System.MissingFieldException(target.GetType().Name, field);
            property.arraySize = values?.Length ?? 0;
            if (values != null)
                for (int i = 0; i < values.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }
}
