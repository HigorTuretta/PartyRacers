#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Esvazia Loja e Passe de Batalha por decisão do projeto: são as duas telas que ficam
    /// sem conteúdo nesta fase. Em vez de manter produto inventado (preços, carteira e timer
    /// falsos), desliga o conteúdo e mostra um estado vazio honesto — a navegação continua
    /// funcionando e nenhuma informação mentirosa aparece.
    /// </summary>
    public static class EmptyStorePassBuilder
    {
        const string SCREENS = "Assets/_Projeto/Prefabs/UI/Screens";

        // o que desligar em cada tela
        static readonly string[] DesligarLoja =
            { "Carteira", "Abas", "DestaqueSemana", "Grade", "ColunaDireita" };

        static readonly string[] DesligarPasse =
            { "Carteira", "Temporada", "Trilha", "MissoesDiarias" };

        [MenuItem("Party Racers/UI/Esvaziar Loja e Passe")]
        public static void Esvaziar()
        {
            Aplicar("Screen_Store", DesligarLoja, "LOJA",
                "A loja ainda não abriu.",
                "Nada aqui é definitivo: quando o catálogo existir, ele aparece nesta tela.");

            Aplicar("Screen_BattlePass", DesligarPasse, "PASSE DE BATALHA",
                "O passe ainda não começou.",
                "Sem temporada ativa, não há níveis nem recompensas para mostrar.");

            AssetDatabase.SaveAssets();
            Debug.Log("[vazio] Loja e Passe esvaziados com estado vazio próprio");
        }

        static void Aplicar(string nomeTela, string[] desligar, string rotulo, string titulo, string corpo)
        {
            var caminho = SCREENS + "/" + nomeTela + ".prefab";
            var raiz = PrefabUtility.LoadPrefabContents(caminho);
            try
            {
                foreach (var n in desligar)
                {
                    var t = raiz.transform.Find(n);
                    if (t != null) t.gameObject.SetActive(false);
                }

                MontarEstadoVazio(raiz.transform, rotulo, titulo, corpo);
                PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        /// <summary>Cartão central no estilo PLACA dizendo, sem rodeios, que não há conteúdo.</summary>
        static void MontarEstadoVazio(Transform tela, string rotulo, string titulo, string corpo)
        {
            var antigo = tela.Find("EstadoVazio");
            if (antigo != null) Object.DestroyImmediate(antigo.gameObject);

            var raiz = Novo("EstadoVazio", tela);
            raiz.anchorMin = raiz.anchorMax = new Vector2(0.5f, 0.5f);
            raiz.pivot = new Vector2(0.5f, 0.5f);
            raiz.anchoredPosition = new Vector2(0f, -40f);
            raiz.sizeDelta = new Vector2(980f, 360f);

            // sombra dura embaixo, como todo painel do PLACA
            var sombra = Novo("Sombra", raiz);
            Esticar(sombra);
            sombra.anchoredPosition = new Vector2(0f, -9f);
            Fundo(sombra, "UI_Panel_R26_Deep", Cor("#0A0C22"));

            var bg = Novo("Bg", raiz);
            Esticar(bg);
            Fundo(bg, "UI_Panel_R26_Deep", Color.white);

            Texto(raiz, "Rotulo", rotulo, "Archivo ExtraBold SDF", 21f, Cor("#7C86C8"),
                  new Vector2(0f, 108f), new Vector2(880f, 28f), 0.12f);

            Texto(raiz, "Titulo", titulo, "TitanOne SDF", 46f, Cor("#FFF7E8"),
                  new Vector2(0f, 40f), new Vector2(900f, 60f), 0f);

            Texto(raiz, "Corpo", corpo, "Archivo Bold SDF", 24f, Cor("#C3CEDD"),
                  new Vector2(0f, -50f), new Vector2(820f, 90f), 0f);
        }

        // ------------------------------------------------------------------ utilidades
        static RectTransform Novo(string nome, Transform pai)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return (RectTransform)go.transform;
        }

        static void Esticar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Fundo(RectTransform rt, string sprite, Color cor)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite(sprite);
            img.type = Image.Type.Sliced;
            img.color = cor;
            img.raycastTarget = false;
            // o NineSliceFixer acerta o multiplicador depois; 2.31 = borda 60 / raio 26
            img.pixelsPerUnitMultiplier = 2.31f;
        }

        static void Texto(Transform pai, string nome, string valor, string fonte, float tamanho,
                          Color cor, Vector2 pos, Vector2 size, float espacamento)
        {
            var rt = Novo(nome, pai);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = valor;
            tmp.fontSize = tamanho;
            tmp.color = cor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.characterSpacing = espacamento * 100f;
            tmp.raycastTarget = false;
            var f = Fonte(fonte);
            if (f != null) tmp.font = f;
        }

        static TMP_FontAsset Fonte(string nome)
        {
            var guid = AssetDatabase.FindAssets(nome + " t:TMP_FontAsset").FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static Sprite Sprite(string nome)
        {
            var guid = AssetDatabase.FindAssets(nome + " t:Sprite", new[] { "Assets/_Projeto/Art/UI" }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static Color Cor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
#endif
