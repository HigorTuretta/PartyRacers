#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Ferramenta de desenvolvimento: renderiza telas da UI para PNG sem entrar em playmode,
    /// para conferir o resultado visual contra o mockup PLACA. Não faz parte do jogo.
    ///
    /// Renderiza no espaço de autoria real (1920x1080): coloca o canvas em WorldSpace com o rect
    /// exato e enquadra com uma câmera ortográfica temporária, de modo que o resultado não
    /// dependa do tamanho atual da Game View.
    /// </summary>
    public static class UICaptureTool
    {
        static readonly Color Ink = new Color(0.04f, 0.047f, 0.133f, 1f); // tokens.json

        /// <summary>Grava um PNG da tela inteira (UI só, sobre o ink do tema).</summary>
        public static string Capture(string screenName, string outputPath, int width = 1920, int height = 1080)
        {
            var tex = RenderUI(screenName, width, height, Ink, null, 0f, out string erro);
            if (tex == null) return erro;
            Gravar(tex, outputPath);
            Object.DestroyImmediate(tex);
            return "OK " + Path.GetFileName(outputPath);
        }

        /// <summary>
        /// Enquadra apenas o RectTransform em <paramref name="subPath"/> (caminho relativo à tela),
        /// com <paramref name="margem"/> pixels de autoria em volta. Para inspecionar 9-slice de perto.
        /// </summary>
        public static string CaptureRegion(string screenName, string subPath, string outputPath,
                                           int resolucao = 900, float margem = 24f)
        {
            var tex = RenderUI(screenName, resolucao, resolucao, Ink, subPath, margem, out string erro);
            if (tex == null) return erro;
            Gravar(tex, outputPath);
            Object.DestroyImmediate(tex);
            return "OK " + Path.GetFileName(outputPath);
        }

        /// <summary>
        /// Compõe a cena 3D (pela Camera.main, como o jogador vê) com a UI da tela por cima.
        /// É o único jeito de conferir a garagem, cuja composição depende do carro no palco.
        ///
        /// O alpha da UI é recuperado renderizando-a duas vezes, sobre preto e sobre branco:
        /// <c>a = 1 - (Cbranco - Cpreto)</c> e a cor pré-multiplicada é <c>Cpreto</c>. Isso evita
        /// depender do canal alpha do render target, que o URP não preserva como cobertura.
        /// </summary>
        public static string CaptureComposite(string screenName, string outputPath, int width = 1920, int height = 1080)
        {
            var cam3d = Camera.main;
            if (cam3d == null) return "ERRO: sem Camera.main";

            var tex3d = Render3D(cam3d, width, height);
            var preto = RenderUI(screenName, width, height, Color.black, null, 0f, out string e1);
            if (preto == null) { Object.DestroyImmediate(tex3d); return e1; }
            var branco = RenderUI(screenName, width, height, Color.white, null, 0f, out string e2);
            if (branco == null) { Object.DestroyImmediate(tex3d); Object.DestroyImmediate(preto); return e2; }

            var p3 = tex3d.GetPixels();
            var pb = preto.GetPixels();
            var pw = branco.GetPixels();
            var saida = new Color[p3.Length];
            for (int i = 0; i < saida.Length; i++)
            {
                // cobertura média dos três canais (deveriam concordar; a média estabiliza)
                float a = 1f - ((pw[i].r - pb[i].r) + (pw[i].g - pb[i].g) + (pw[i].b - pb[i].b)) / 3f;
                a = Mathf.Clamp01(a);
                saida[i] = new Color(
                    pb[i].r + (1f - a) * p3[i].r,
                    pb[i].g + (1f - a) * p3[i].g,
                    pb[i].b + (1f - a) * p3[i].b, 1f);
            }

            var final = new Texture2D(width, height, TextureFormat.RGBA32, false);
            final.SetPixels(saida);
            final.Apply();
            Gravar(final, outputPath);

            foreach (var t in new[] { tex3d, preto, branco, final }) Object.DestroyImmediate(t);
            return "OK composto " + Path.GetFileName(outputPath);
        }

        /// <summary>Captura todas as telas filhas do canvas raiz para a pasta indicada.</summary>
        public static string CaptureAll(string outputDir, int width = 1920, int height = 1080)
        {
            var canvas = CanvasRaiz();
            if (canvas == null) return "ERRO: nenhum Canvas raiz na cena";
            var cena = SceneManager.GetActiveScene().name;
            var nomes = canvas.transform.Cast<Transform>().Select(t => t.name).ToList();
            return string.Join("\n", nomes.Select(n =>
                Capture(n, Path.Combine(outputDir, cena + "__" + n + ".png"), width, height)));
        }

        // ------------------------------------------------------------------ internos

        /// <summary>
        /// O canvas das telas. A cena tem mais de um canvas raiz (o Canvas_Fundo do palco também
        /// é raiz), então escolhe o que de fato contém telas "Screen_*".
        /// </summary>
        public static Canvas CanvasRaiz()
        {
            var raizes = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                               .Where(c => c.transform.parent == null).ToList();
            return raizes.FirstOrDefault(c => c.transform.Cast<Transform>().Any(t => t.name.StartsWith("Screen_")))
                ?? raizes.FirstOrDefault();
        }

        static void Gravar(Texture2D tex, string caminho)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(caminho));
            File.WriteAllBytes(caminho, tex.EncodeToPNG());
        }

        /// <summary>Renderiza a cena 3D pela câmera do jogo, sem a camada de UI.</summary>
        static Texture2D Render3D(Camera cam, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var rtAntes = cam.targetTexture;
            var mascara = cam.cullingMask;
            try
            {
                cam.cullingMask = mascara & ~(1 << 5); // 5 = UI
                cam.targetTexture = rt;
                cam.Render();
                var a = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = a;
            }
            finally
            {
                cam.targetTexture = rtAntes;
                cam.cullingMask = mascara;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
            return tex;
        }

        /// <summary>
        /// Renderiza a UI da tela no espaço de autoria sobre <paramref name="fundo"/>.
        /// Devolve null e preenche <paramref name="erro"/> quando algo falta.
        /// </summary>
        static Texture2D RenderUI(string screenName, int width, int height, Color fundo,
                                  string subPath, float margem, out string erro)
        {
            erro = null;
            var canvas = CanvasRaiz();
            if (canvas == null) { erro = "ERRO: nenhum Canvas raiz na cena"; return null; }

            var canvasRT = canvas.transform as RectTransform;
            var scaler = canvas.GetComponent<CanvasScaler>();

            // --- guarda estado ---
            var modoOriginal = canvas.renderMode;
            var camOriginal = canvas.worldCamera;
            var posOriginal = canvasRT.position;
            var rotOriginal = canvasRT.rotation;
            var escalaOriginal = canvasRT.localScale;
            var tamanhoOriginal = canvasRT.sizeDelta;
            var modoScaler = scaler != null ? scaler.uiScaleMode : CanvasScaler.ScaleMode.ConstantPixelSize;
            var fatorScaler = scaler != null ? scaler.scaleFactor : 1f;

            var estadoTelas = new Dictionary<GameObject, bool>();
            var alphas = new Dictionary<CanvasGroup, float>();
            foreach (Transform filho in canvas.transform)
            {
                estadoTelas[filho.gameObject] = filho.gameObject.activeSelf;
                var cg = filho.GetComponent<CanvasGroup>();
                if (cg != null) alphas[cg] = cg.alpha;
            }

            GameObject camGO = null;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                if (!string.IsNullOrEmpty(screenName))
                    foreach (Transform filho in canvas.transform)
                        filho.gameObject.SetActive(filho.name == screenName);

                // telas escondidas pelo roteador ficam com alpha 0 — força visível para a foto
                foreach (var kv in alphas)
                    if (kv.Key != null && kv.Key.gameObject.activeSelf) kv.Key.alpha = 1f;

                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.scaleFactor = 1f; }
                canvas.renderMode = RenderMode.WorldSpace;
                canvasRT.sizeDelta = new Vector2(1920, 1080);
                canvasRT.localScale = Vector3.one;
                canvasRT.position = new Vector3(100000f, 100000f, 0f); // longe da cena 3D
                canvasRT.rotation = Quaternion.identity;

                Canvas.ForceUpdateCanvases();
                RebuildRecursivo(canvasRT);
                Canvas.ForceUpdateCanvases();

                Vector3 centro = canvasRT.position;
                float meiaAltura = 1080f / 2f, aspecto = 1920f / 1080f;
                if (!string.IsNullOrEmpty(subPath))
                {
                    var tela = canvas.transform.Find(screenName);
                    var alvo = tela != null ? tela.Find(subPath) as RectTransform : null;
                    if (alvo == null) { erro = "ERRO: sub '" + subPath + "' não encontrado"; return null; }
                    var cantos = new Vector3[4];
                    alvo.GetWorldCorners(cantos);
                    var min = Vector3.Min(Vector3.Min(cantos[0], cantos[1]), Vector3.Min(cantos[2], cantos[3]));
                    var max = Vector3.Max(Vector3.Max(cantos[0], cantos[1]), Vector3.Max(cantos[2], cantos[3]));
                    centro = (min + max) * 0.5f; centro.z = canvasRT.position.z;
                    meiaAltura = Mathf.Max(max.y - min.y, max.x - min.x) * 0.5f + margem;
                    aspecto = 1f;
                }

                camGO = new GameObject("~UICaptureCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGO.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = meiaAltura;
                cam.aspect = aspecto;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = 5000f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = fundo;
                cam.transform.position = centro + new Vector3(0, 0, -500f);
                cam.transform.rotation = Quaternion.identity;

                rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
                cam.targetTexture = rt;
                cam.Render();

                tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var ativoAntes = RenderTexture.active;
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = ativoAntes;

                var resultado = tex;
                tex = null;          // entregue ao chamador: não destruir no finally
                return resultado;
            }
            finally
            {
                if (camGO != null) Object.DestroyImmediate(camGO);
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }

                canvas.renderMode = modoOriginal;
                canvas.worldCamera = camOriginal;
                canvasRT.position = posOriginal;
                canvasRT.rotation = rotOriginal;
                canvasRT.localScale = escalaOriginal;
                canvasRT.sizeDelta = tamanhoOriginal;
                if (scaler != null) { scaler.uiScaleMode = modoScaler; scaler.scaleFactor = fatorScaler; }

                foreach (var kv in alphas) if (kv.Key != null) kv.Key.alpha = kv.Value;
                foreach (var kv in estadoTelas) if (kv.Key != null) kv.Key.SetActive(kv.Value);
                Canvas.ForceUpdateCanvases();
            }
        }

        static void RebuildRecursivo(RectTransform root)
        {
            if (root == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            foreach (Transform f in root)
                if (f is RectTransform rf) RebuildRecursivo(rf);
        }
    }
}
#endif
