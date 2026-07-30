#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PartyRacers.UI.Motion;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Liga o movimento da interface. Os componentes de animação existiam no projeto mas não
    /// estavam em nenhum objeto — por isso a UI parecia estática: botão sem resposta ao toque e
    /// tela aparecendo seca.
    ///
    /// Aplica, em todos os prefabs de tela:
    /// - <see cref="UIPress"/> em cada botão (hover + afundar, tempos do tokens.json);
    /// - troca para o sprite `_Pressed` onde a moldura tem essa variante;
    /// - <see cref="UIAppear"/> nos blocos de conteúdo, em cascata.
    /// </summary>
    public static class MotionPass
    {
        const string SCREENS = "Assets/_Projeto/Prefabs/UI/Screens";

        /// <summary>Molduras que têm variante afundada no pacote.</summary>
        static readonly Dictionary<string, string> Pressionados = new Dictionary<string, string>
        {
            { "UI_Button_R22_Green", "UI_Button_R22_Pressed_Green" },
            { "UI_Button_R22_Amber", "UI_Button_R22_Pressed_Amber" },
        };

        /// <summary>Não são conteúdo: não devem entrar animando.</summary>
        static readonly HashSet<string> NaoAnimar = new HashSet<string> { "Fundo", "Veu", "EventSystem" };

        [MenuItem("Party Racers/UI/Aplicar movimento na UI")]
        public static void Aplicar()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SCREENS }))
            {
                var caminho = AssetDatabase.GUIDToAssetPath(guid);
                var raiz = PrefabUtility.LoadPrefabContents(caminho);
                try
                {
                    int press = Botoes(raiz);
                    int swap = TrocaDeSprite(raiz);
                    int appear = Blocos(raiz.transform);
                    PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                    log.Add($"  {Path.GetFileNameWithoutExtension(caminho)}: {press} botões, {swap} com sprite pressionado, {appear} blocos");
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[movimento] aplicado:\n" + string.Join("\n", log));
        }

        static int Botoes(GameObject raiz)
        {
            int n = 0;
            foreach (var b in raiz.GetComponentsInChildren<Button>(true))
            {
                var press = b.GetComponent<UIPress>() ?? b.gameObject.AddComponent<UIPress>();

                // afundar por posição briga com Layout Group (que reposiciona no rebuild) e com
                // a variante _Pressed do sprite (que já mostra o botão afundado)
                bool temPressionado = TemVariantePressionada(b);
                bool sobLayout = b.transform.parent != null &&
                                 b.transform.parent.GetComponent<LayoutGroup>() != null;

                var so = new SerializedObject(press);
                so.FindProperty("duracao").floatValue = 0.08f;
                so.FindProperty("escalaHover").floatValue = 1.04f;
                so.FindProperty("escalaPressionado").floatValue = 0.96f;
                so.FindProperty("afundar").boolValue = !temPressionado && !sobLayout;
                so.FindProperty("deslocamentoY").floatValue = 6f;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
            }
            return n;
        }

        static bool TemVariantePressionada(Button b)
        {
            var img = b.targetGraphic as Image;
            return img != null && img.sprite != null && Pressionados.ContainsKey(img.sprite.name);
        }

        /// <summary>
        /// Handoff §7: "botões trocam de sprite no pressionar (nenhum deslocamento de
        /// RectTransform por código)". Onde a variante existe, usa Sprite Swap.
        /// </summary>
        static int TrocaDeSprite(GameObject raiz)
        {
            int n = 0;
            foreach (var b in raiz.GetComponentsInChildren<Button>(true))
            {
                var img = b.targetGraphic as Image;
                if (img == null || img.sprite == null) continue;
                if (!Pressionados.TryGetValue(img.sprite.name, out string nomePressionado)) continue;

                var pressionado = Sprite(nomePressionado);
                if (pressionado == null) continue;

                b.transition = Selectable.Transition.SpriteSwap;
                var estado = b.spriteState;
                estado.pressedSprite = pressionado;
                estado.selectedSprite = img.sprite;
                b.spriteState = estado;
                n++;
            }
            return n;
        }

        /// <summary>
        /// Blocos de primeiro nível entram em cascata. Não usa a raiz da tela porque o alfa dela
        /// pertence ao ScreenRouter, que faz o fade da troca de tela.
        /// </summary>
        static int Blocos(Transform tela)
        {
            int n = 0, indice = 0;
            foreach (Transform bloco in tela)
            {
                if (NaoAnimar.Contains(bloco.name))
                    continue;

                var ap = bloco.GetComponent<UIAppear>() ?? bloco.gameObject.AddComponent<UIAppear>();
                var so = new SerializedObject(ap);
                so.FindProperty("duracao").floatValue = 0.22f;      // tokens: trocaTela.duracao
                so.FindProperty("atraso").floatValue = 0f;
                so.FindProperty("atrasoPorIrmao").floatValue = 0.035f;
                so.FindProperty("deslocamento").vector2Value = new Vector2(0f, -28f);
                so.FindProperty("escalaInicial").floatValue = 0.96f;
                so.FindProperty("comFade").boolValue = true;
                so.FindProperty("comRecuo").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                n++;
                indice++;
            }
            return n;
        }

        /// <summary>Fade de troca de tela no tempo do tokens.json (0,22 s).</summary>
        [MenuItem("Party Racers/UI/Acertar tempo de troca de tela")]
        public static void TempoDeTroca()
        {
            var router = Object.FindObjectsByType<PartyRacers.UI.Frontend.ScreenRouter>(FindObjectsInactive.Include)
                               .FirstOrDefault();
            if (router == null) { Debug.LogWarning("[movimento] sem ScreenRouter na cena"); return; }
            var so = new SerializedObject(router);
            so.FindProperty("duracaoFade").floatValue = 0.22f;
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[movimento] fade de troca de tela = 0,22 s");
        }

        static Sprite Sprite(string nome)
        {
            var guid = AssetDatabase.FindAssets(nome + " t:Sprite", new[] { "Assets/_Projeto/Art/UI" }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
#endif
