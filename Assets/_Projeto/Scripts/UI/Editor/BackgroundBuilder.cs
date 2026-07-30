#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Gera os sprites de fundo do mockup PLACA e os aplica no objeto "Fundo" de cada tela.
    /// Sem isso o Fundo fica com sprite nulo e alpha 0, e a tela vira preto chapado —
    /// era por isso que nenhum contorno #0A0C22 aparecia.
    /// </summary>
    public static class BackgroundBuilder
    {
        const string PASTA = "Assets/_Projeto/Art/UI/Backgrounds";

        static Color Hex(string h)
        {
            ColorUtility.TryParseHtmlString(h, out var c);
            return c;
        }

        // paleta do tokens.json
        static readonly Color DeepBlue = Hex("#101334");
        static readonly Color Blue = Hex("#1B2050");
        static readonly Color Royal = Hex("#2A3480");
        static readonly Color Sky = Hex("#35A7FF");

        [MenuItem("Party Racers/UI/Gerar fundos")]
        public static void Gerar()
        {
            Directory.CreateDirectory(PASTA);

            // 1) gradiente vertical das telas: #101334 -> #1B2050 (70%) -> #2A3480
            GravarPNG("BG_Gradient_Deep", 16, 512, (x, y, w, h) =>
            {
                float t = 1f - (y / (float)(h - 1));           // 0 no topo
                return t <= 0.70f
                    ? Color.Lerp(DeepBlue, Blue, t / 0.70f)
                    : Color.Lerp(Blue, Royal, (t - 0.70f) / 0.30f);
            }, clamp: true);

            // 2) listras verticais do piso: 3px de #35A7FF a 8% a cada 96px
            GravarPNG("BG_FloorLines", 1920, 8, (x, y, w, h) =>
                (x % 96) < 3 ? new Color(Sky.r, Sky.g, Sky.b, 0.08f) : new Color(Sky.r, Sky.g, Sky.b, 0f),
                clamp: true);

            // 3) escurecimento do piso: transparente -> #101334 a 70%
            GravarPNG("BG_FloorFade", 16, 256, (x, y, w, h) =>
            {
                float t = 1f - (y / (float)(h - 1));           // 0 no topo
                return new Color(DeepBlue.r, DeepBlue.g, DeepBlue.b, Mathf.Lerp(0f, 0.70f, t));
            }, clamp: true);

            AssetDatabase.Refresh();
            Debug.Log("[fundos] sprites gerados em " + PASTA);
        }

        static void GravarPNG(string nome, int w, int h, System.Func<int, int, int, int, Color> f, bool clamp)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x, y, w, h);
            tex.SetPixels(px);
            tex.Apply();

            var caminho = PASTA + "/" + nome + ".png";
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(caminho, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(caminho);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.filterMode = FilterMode.Bilinear;
            imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }

        // ------------------------------------------------------------------ aplicação

        /// <summary>Telas com fundo chapado #101334 no mockup (resultado e loading).</summary>
        static readonly HashSet<string> Chapadas = new HashSet<string> { "Screen_Result", "Screen_Loading" };

        /// <summary>
        /// Telas que mostram o carro 3D. O canvas da UI é Overlay, então um Fundo opaco aqui
        /// taparia o carro: nelas o Fundo fica transparente e o gradiente vem do palco
        /// (Canvas_Fundo, atrás do carro). Ver <see cref="MontarFundoDoPalco"/>.
        /// </summary>
        static readonly HashSet<string> ComCarro = new HashSet<string> { "Screen_Lobby", "Screen_Garage_PC" };

        [MenuItem("Party Racers/UI/Aplicar fundos nas telas")]
        public static void Aplicar()
        {
            var grad = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_Gradient_Deep.png");
            var linhas = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_FloorLines.png");
            var fade = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_FloorFade.png");
            if (grad == null) { Debug.LogError("[fundos] rode 'Gerar fundos' primeiro"); return; }

            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Projeto/Prefabs/UI/Screens" }))
            {
                var caminho = AssetDatabase.GUIDToAssetPath(guid);
                var nome = Path.GetFileNameWithoutExtension(caminho);
                var raiz = PrefabUtility.LoadPrefabContents(caminho);
                try
                {
                    var fundo = raiz.transform.Find("Fundo");
                    if (fundo == null) { log.Add($"  {nome}: sem 'Fundo' — ignorado"); continue; }

                    var img = fundo.GetComponent<Image>() ?? fundo.gameObject.AddComponent<Image>();
                    Esticar(fundo as RectTransform);

                    img.type = Image.Type.Simple;
                    if (ComCarro.Contains(nome))
                    {
                        // transparente para o carro do palco aparecer; o gradiente vem do Canvas_Fundo
                        img.sprite = null;
                        img.color = new Color(0f, 0f, 0f, 0f);
                        var pisoAntigo = fundo.Find("Piso");
                        if (pisoAntigo != null) Object.DestroyImmediate(pisoAntigo.gameObject);
                    }
                    else if (Chapadas.Contains(nome))
                    {
                        img.sprite = null;
                        img.color = DeepBlue;
                    }
                    else
                    {
                        img.sprite = grad;
                        img.color = Color.white;
                    }

                    PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                    log.Add($"  {nome}: ok");
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[fundos] aplicados:\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Cria/atualiza o "Canvas_Fundo" da cena: um canvas em Screen Space - Camera colocado
        /// bem longe da câmera, de modo que o carro do palco fique entre ele e a câmera e
        /// portanto desenhe na frente. Recebe o gradiente e a faixa de piso do mockup.
        /// O canvas da UI segue em Overlay e continua desenhando por cima de tudo.
        /// </summary>
        [MenuItem("Party Racers/UI/Montar fundo do palco (cena)")]
        public static void MontarFundoDoPalco()
        {
            var grad = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_Gradient_Deep.png");
            var linhas = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_FloorLines.png");
            var fade = AssetDatabase.LoadAssetAtPath<Sprite>(PASTA + "/BG_FloorFade.png");
            if (grad == null) { Debug.LogError("[fundo palco] rode 'Gerar fundos' primeiro"); return; }

            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[fundo palco] sem Camera.main"); return; }

            var existente = GameObject.Find("Canvas_Fundo");
            var go = existente ?? new GameObject("Canvas_Fundo",
                typeof(Canvas), typeof(CanvasScaler));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = Mathf.Min(500f, cam.farClipPlane * 0.5f);
            canvas.sortingOrder = -100;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            CriarCamada(go.transform, "Gradiente", grad, Color.white, 0);

            var piso = go.transform.Find("Piso") as RectTransform;
            if (piso == null)
            {
                var p = new GameObject("Piso", typeof(RectTransform));
                p.transform.SetParent(go.transform, false);
                piso = (RectTransform)p.transform;
            }
            AncorarNaBase(piso, 420f);
            piso.SetSiblingIndex(1);
            CriarCamada(piso, "Fade", fade, Color.white, 0);
            CriarCamada(piso, "Listras", linhas, Color.white, 1);

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[fundo palco] Canvas_Fundo pronto (planeDistance=" + canvas.planeDistance + ")");
        }

        static void CriarCamada(Transform pai, string nome, Sprite sprite, Color cor, int ordem)
        {
            var t = pai.Find(nome) as RectTransform;
            if (t == null)
            {
                var go = new GameObject(nome, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(pai, false);
                t = (RectTransform)go.transform;
            }
            Esticar(t);
            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = cor;
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            t.SetSiblingIndex(ordem);
        }

        static void Esticar(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void AncorarNaBase(RectTransform rt, float altura)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, altura);
        }
    }
}
#endif
