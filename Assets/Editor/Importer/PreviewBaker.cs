using System.Collections.Generic;
using System.IO;
using ithappy;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Fotografa o carro real para gerar os previews da garagem.
    ///
    /// Os cards vinham com a palavra "PREVIEW" escrita — era o placeholder do protótipo. Um ícone
    /// desenhado à mão também não serviria: o jogador precisa ver a PEÇA que vai equipar, não um
    /// símbolo dela. Então cada variante é montada num carro de verdade, enquadrada e salva como
    /// PNG em <c>Art/UI_v2/Previews/</c>.
    ///
    /// Roda no Editor e grava assets versionados — nada é renderizado em runtime. É o mesmo
    /// princípio dos sprites do pacote: arte é produzida offline, o jogo só a exibe.
    ///
    /// O enquadramento de cada categoria é o mesmo da câmera da garagem: rodas de perto e por
    /// baixo, adesivos de perfil, teto de cima. Assim o card mostra a peça do ângulo em que ela se
    /// distingue — um card de rodas visto de frente mostraria quatro carros iguais.
    /// </summary>
    public static class PreviewBaker
    {
        // Resources: é a única forma de o binder carregar por NOME em runtime sem manter 60
        // referências serializadas numa lista que ninguém consegue revisar.
        public const string Destino = "Assets/_Projeto/Resources/Previews";

        private const int Lado = 256;

        /// <summary>Como fotografar cada categoria: direção da câmera, distância e alvo.</summary>
        private sealed class Enquadramento
        {
            public Vector3 Direcao;      // de onde a câmera olha, em espaço do carro
            public float Zoom = 1f;      // <1 aproxima
            public Vector3 Alvo;         // deslocamento do centro, em frações da caixa
        }

        private static readonly Dictionary<string, Enquadramento> PorCategoria =
            new Dictionary<string, Enquadramento>
            {
                ["modelo"] = new Enquadramento { Direcao = new Vector3(1f, 0.45f, 1f), Zoom = 1f },
                ["cor"] = new Enquadramento { Direcao = new Vector3(1f, 0.45f, 1f), Zoom = 1f },
                ["rodas"] = new Enquadramento { Direcao = new Vector3(1f, 0.12f, 0.35f), Zoom = 0.33f, Alvo = new Vector3(0.55f, -0.55f, 0.5f) },
                ["frente"] = new Enquadramento { Direcao = new Vector3(0.35f, 0.28f, 1f), Zoom = 0.5f, Alvo = new Vector3(0f, -0.25f, 0.7f) },
                ["traseira"] = new Enquadramento { Direcao = new Vector3(0.35f, 0.3f, -1f), Zoom = 0.5f, Alvo = new Vector3(0f, -0.15f, -0.7f) },
                ["teto"] = new Enquadramento { Direcao = new Vector3(0.6f, 0.85f, 0.6f), Zoom = 0.62f, Alvo = new Vector3(0f, 0.45f, 0f) },
                ["adesivos"] = new Enquadramento { Direcao = new Vector3(1f, 0.12f, 0.05f), Zoom = 0.72f },
            };

        [MenuItem("Party Racers/UI v2/8 · Gerar previews da garagem", priority = 23)]
        public static void Gerar()
        {
            var alvo = Object.FindAnyObjectByType<KartVisualCustomizer>();
            if (alvo == null)
            {
                Debug.LogError("[UI v2] Abra a cena Frontend: o gerador fotografa o carro dela.");
                return;
            }

            GarantirPasta(Destino);

            // O carro do palco volta ao estado atual no fim — o gerador não pode mudar a escolha
            // do jogador só porque tirou fotos.
            int carroOriginal = alvo.CarIndex;
            int corOriginal = alvo.ColorIndex;
            var elementosOriginais = new Dictionary<CarElementName, int>();

            var log = new System.Text.StringBuilder();
            int feitos = 0;

            GameObject palcoDeFoto = MontarPalco(out Camera camera, out Light luz);

            try
            {
                feitos += Fotografar(alvo, camera, "modelo", alvo.CarCount,
                                     i => alvo.SetCar(i), log);

                feitos += Cores(alvo, log);

                foreach ((string categoria, CarElementName elemento) in new[]
                         {
                             ("rodas", CarElementName.Wheel),
                             ("frente", CarElementName.FrontBumper),
                             ("traseira", CarElementName.RearBumper),
                             ("teto", CarElementName.Spoiler),
                             ("adesivos", CarElementName.Decals),
                         })
                {
                    elementosOriginais[elemento] = alvo.GetElementIndex(elemento);
                    int total = alvo.GetElementVariantCount(elemento);
                    CarElementName e = elemento;
                    feitos += Fotografar(alvo, camera, categoria, total,
                                         i => alvo.SetElement(e, i), log);
                }
            }
            finally
            {
                alvo.SetCar(carroOriginal);
                alvo.SetColor(corOriginal);
                foreach (KeyValuePair<CarElementName, int> par in elementosOriginais)
                    alvo.SetElement(par.Key, par.Value);

                if (palcoDeFoto != null)
                    Object.DestroyImmediate(palcoDeFoto);

                _ = luz;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[UI v2] {feitos} previews gerados em {Destino}\n{log}");
        }

        /// <summary>PNG recém-escrito entra como Texture por padrão; o card precisa de Sprite.</summary>
        private static void ImportarComoSprite()
        {
            foreach (string arquivo in Directory.GetFiles(Destino, "*.png"))
            {
                string caminho = arquivo.Replace("\\", "/");
                var ti = AssetImporter.GetAtPath(caminho) as TextureImporter;
                if (ti == null || ti.textureType == TextureImporterType.Sprite)
                    continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ Fotos

        private static int Fotografar(KartVisualCustomizer alvo, Camera camera, string categoria,
                                      int total, System.Action<int> aplicar,
                                      System.Text.StringBuilder log)
        {
            if (total <= 0)
                return 0;

            for (int i = 0; i < total; i++)
            {
                aplicar(i);

                if (alvo.CurrentRig == null)
                    continue;

                Enquadrar(camera, alvo, categoria);
                Salvar(camera, $"{categoria}_{i:00}");
            }

            log.AppendLine($"  {categoria}: {total}");
            return total;
        }

        /// <summary>
        /// A cor não precisa de foto: ela É a informação.
        ///
        /// Renderizar o carro inteiro em 12 tons daria 12 imagens quase idênticas, e o jogador
        /// escolhe cor comparando MATIZES lado a lado. O card recebe a cor direto do customizador
        /// (ver <c>GarageGridUI.PintarPreview</c>), então aqui só se registra a contagem.
        /// </summary>
        private static int Cores(KartVisualCustomizer alvo, System.Text.StringBuilder log)
        {
            log.AppendLine($"  cor: {alvo.ColorCount} (chapadas, sem foto)");
            return 0;
        }

        private static void Enquadrar(Camera camera, KartVisualCustomizer alvo, string categoria)
        {
            Renderer[] renderers = alvo.CurrentRig.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            Bounds caixa = renderers[0].bounds;
            foreach (Renderer r in renderers)
                caixa.Encapsulate(r.bounds);

            Enquadramento e = PorCategoria.TryGetValue(categoria, out Enquadramento v)
                ? v
                : PorCategoria["modelo"];

            Vector3 centro = caixa.center + new Vector3(e.Alvo.x * caixa.extents.x,
                                                        e.Alvo.y * caixa.extents.y,
                                                        e.Alvo.z * caixa.extents.z);

            float raio = caixa.extents.magnitude * e.Zoom;
            float distancia = raio / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.35f;

            camera.transform.position = centro + e.Direcao.normalized * distancia;
            camera.transform.LookAt(centro);
        }

        private static void Salvar(Camera camera, string nome)
        {
            var rt = new RenderTexture(Lado, Lado, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            camera.targetTexture = rt;
            camera.Render();

            var tex = new Texture2D(Lado, Lado, TextureFormat.RGBA32, false);
            RenderTexture ativo = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Lado, Lado), 0, 0);
            tex.Apply();
            RenderTexture.active = ativo;

            camera.targetTexture = null;
            File.WriteAllBytes($"{Destino}/{nome}.png", tex.EncodeToPNG());

            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// Câmera e luz próprias, isoladas da cena.
        ///
        /// Usar a câmera do frontend deixaria o cenário da oficina no fundo de cada card, e a foto
        /// mudaria conforme o enquadramento do palco. Fundo transparente para o card decidir a cor.
        /// </summary>
        private static GameObject MontarPalco(out Camera camera, out Light luz)
        {
            var raiz = new GameObject("__PreviewBaker") { hideFlags = HideFlags.HideAndDontSave };

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(raiz.transform, false);
            camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = 32f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.enabled = false;

            var luzGo = new GameObject("Luz");
            luzGo.transform.SetParent(raiz.transform, false);
            luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.15f;
            luz.transform.rotation = Quaternion.Euler(38f, 140f, 0f);

            return raiz;
        }

        private static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho))
                return;

            int barra = caminho.LastIndexOf('/');
            GarantirPasta(caminho.Substring(0, barra));
            AssetDatabase.CreateFolder(caminho.Substring(0, barra), caminho.Substring(barra + 1));
        }
    }
}
