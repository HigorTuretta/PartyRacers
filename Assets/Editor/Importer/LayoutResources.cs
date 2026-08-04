using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Resolve os nomes que aparecem no layout.json para assets reais do projeto: sprites, fontes
    /// TMP e cores. Fica separado do construtor para que a falta de um asset apareça como uma lista
    /// de pendências no fim do import, e não como um NullReference no meio da árvore.
    /// </summary>
    public static class LayoutResources
    {
        // A arte v2 mora em pasta PRÓPRIA. Na primeira tentativa ela foi copiada por cima de
        // Art/UI/ mantendo as GUIDs: como as 11 telas que já existiam tinham
        // pixelsPerUnitMultiplier calibrado para os PNGs antigos (~2,4× maiores), o 9-slice
        // quebrou em toda a UI do jogo de uma vez. Duas pastas, duas GUIDs, nenhum acoplamento.
        public const string ArtRootV2 = "Assets/_Projeto/Art/UI_v2";

        /// <summary>Ícones, poderes e barras continuam vindo do v1 — o pacote v2 não os substitui.</summary>
        public const string ArtRootV1 = "Assets/_Projeto/Art/UI";

        public const string FontRoot = "Assets/_Projeto/Art/Fonts";

        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, TMP_FontAsset> fontCache = new Dictionary<string, TMP_FontAsset>();

        /// <summary>Nomes citados no JSON que não existem no projeto. Vira relatório no fim.</summary>
        public static readonly List<string> Pendencias = new List<string>();

        public static void Limpar()
        {
            spriteCache.Clear();
            fontCache.Clear();
            Pendencias.Clear();
            glow = null;
        }

        // ------------------------------------------------------------------ Sprites

        /// <summary>"Frames/UI_Panel_R26_Deep" → o Sprite importado de Art/UI/Frames/.</summary>
        public static Sprite Sprite(string chave)
        {
            if (string.IsNullOrEmpty(chave))
                return null;

            if (spriteCache.TryGetValue(chave, out Sprite cached))
                return cached;

            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRootV2}/{chave}.png")
                    ?? AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRootV1}/{chave}.png");

            if (s == null)
            {
                // O pacote v2 trouxe alguns sprites que o v1 não tinha e ainda podem faltar. Cor
                // plana é o fallback combinado no handoff — nunca gerar sprite por código.
                RegistrarPendencia($"sprite ausente: {chave}");
            }

            spriteCache[chave] = s;
            return s;
        }

        /// <summary>Borders 9-slice declarados em _widgets.json → spriteBorders.</summary>
        public static readonly Dictionary<string, int> BordersPadrao = new Dictionary<string, int>
        {
            { "Frames/UI_Panel_R26_Deep", 34 },
            { "Frames/UI_Panel_R26_DeepHi", 34 },
            { "Frames/UI_Modal_R36", 46 },
            { "Frames/UI_Card_R18_Ink", 26 },
            { "Frames/UI_Card_R18_Deep", 27 },
            { "Frames/UI_Card_R18_Royal", 26 },
            { "Frames/UI_Button_R22_Amber", 31 },
            { "Frames/UI_Button_R22_Green", 31 },
            { "Frames/UI_Button_R28_Amber", 38 },
            { "Frames/UI_Dashed_R18", 27 },
            { "Frames/UI_Dashed_R28", 38 },
            { "Frames/UI_Badge_R14_Amber", 23 },
            { "Frames/UI_Badge_R14_Cream", 23 },
            { "Frames/UI_Badge_R11_Chip", 19 },
            { "Race/Toast_Card", 19 },
            { "Brand/Brand_Plate_Racers", 18 },
        };

        /// <summary>
        /// Um sprite só deve ser Sliced se tiver borda. Esticar um overlay de tela cheia como
        /// Sliced com borda 0 gera uma malha de 9 quads sem motivo, e no caso do Dial_Grid o modo
        /// certo é Tiled.
        /// </summary>
        public static UnityEngine.UI.Image.Type TipoDeImagem(string chave)
        {
            if (string.IsNullOrEmpty(chave))
                return UnityEngine.UI.Image.Type.Simple;

            if (chave == "Race/Dial_Grid")
                return UnityEngine.UI.Image.Type.Tiled;

            return BordersPadrao.ContainsKey(chave)
                ? UnityEngine.UI.Image.Type.Sliced
                : UnityEngine.UI.Image.Type.Simple;
        }

        // ------------------------------------------------------------------ Fontes

        // O JSON pede pesos que o projeto tem sob outro nome. "Archivo Black" é o peso 900 do
        // Archivo; o asset importado é o ExtraBold. Mapear aqui evita 20 fallbacks silenciosos
        // para LiberationSans no meio das telas.
        private static readonly Dictionary<string, string> CaminhoDeFonte = new Dictionary<string, string>
        {
            { "Titan One",      FontRoot + "/TitanOne/TitanOne SDF.asset" },
            { "Archivo",        FontRoot + "/Archivo/Archivo SemiBold SDF.asset" },
            { "Archivo Black",  FontRoot + "/Archivo/Archivo ExtraBold SDF.asset" },
            { "Archivo Bold",   FontRoot + "/Archivo/Archivo Bold SDF.asset" },
            { "Space Mono",     FontRoot + "/SpaceMono/SpaceMono SDF.asset" },
            { "Space Mono Bold", FontRoot + "/SpaceMono/SpaceMono Bold SDF.asset" },
        };

        /// <summary>
        /// Falloff radial branco, usado como `box-shadow: 0 0 Npx cor`. Os pixels vêm do glow do
        /// pack Hovl que já estava no projeto; a cópia existe porque lá ele está importado como
        /// Texture (para VFX) e trocar isso mexeria em material de terceiros. Branco puro, então
        /// tinge para qualquer cor sem perder matiz.
        /// </summary>
        public static Sprite SpriteDeGlow()
        {
            if (glow == null)
            {
                const string caminho = ArtRootV2 + "/Race/Glow_Radial.png";
                glow = AssetDatabase.LoadAssetAtPath<Sprite>(caminho);

                if (glow == null)
                    RegistrarPendencia("sprite de glow ausente: " + caminho);
            }

            return glow;
        }

        private static Sprite glow;

        public static TMP_FontAsset Fonte(string nome)
        {
            if (string.IsNullOrEmpty(nome))
                nome = "Archivo";

            if (fontCache.TryGetValue(nome, out TMP_FontAsset cached))
                return cached;

            TMP_FontAsset f = null;
            if (CaminhoDeFonte.TryGetValue(nome, out string caminho))
                f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);

            if (f == null)
                RegistrarPendencia($"fonte ausente: {nome}");

            fontCache[nome] = f;
            return f;
        }

        // ------------------------------------------------------------------ Cores

        /// <summary>Aceita "#RRGGBB", "#RRGGBBAA" e "rgba(r,g,b,a)" — as três formas do JSON.</summary>
        public static Color Cor(string texto, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return fallback;

            texto = texto.Trim();

            if (texto.StartsWith("#"))
                return ColorUtility.TryParseHtmlString(texto, out Color c) ? c : fallback;

            if (texto.StartsWith("rgba(") || texto.StartsWith("rgb("))
            {
                int abre = texto.IndexOf('(');
                int fecha = texto.IndexOf(')');
                if (abre < 0 || fecha < abre)
                    return fallback;

                string[] partes = texto.Substring(abre + 1, fecha - abre - 1).Split(',');
                if (partes.Length < 3)
                    return fallback;

                float r = Parse(partes[0]) / 255f;
                float g = Parse(partes[1]) / 255f;
                float b = Parse(partes[2]) / 255f;
                float a = partes.Length > 3 ? Parse(partes[3]) : 1f;
                return new Color(r, g, b, a);
            }

            return fallback;
        }

        private static float Parse(string s) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        /// <summary>Tinta do contorno e da sombra dura. Sempre a mesma (tokens-v2 → sombraDura.cor).</summary>
        public static readonly Color Tinta = new Color(10f / 255f, 12f / 255f, 34f / 255f, 1f);

        public static void RegistrarPendencia(string mensagem)
        {
            if (!Pendencias.Contains(mensagem))
                Pendencias.Add(mensagem);
        }
    }
}
