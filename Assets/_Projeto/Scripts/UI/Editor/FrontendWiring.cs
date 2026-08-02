#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Motion;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Correções e ligações do frontend que precisam acontecer sobre a cena/prefabs já montados:
    /// rótulos das abas, fundo que não deve capturar clique, palco do carro com arraste, e as
    /// referências da GarageScreenUI para a lista real de categorias.
    /// </summary>
    public static class FrontendWiring
    {
        const string SCREENS = "Assets/_Projeto/Prefabs/UI/Screens";

        [MenuItem("Party Racers/UI/Corrigir prefabs das telas")]
        public static void CorrigirPrefabs()
        {
            var log = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { SCREENS }))
            {
                var caminho = AssetDatabase.GUIDToAssetPath(guid);
                var raiz = PrefabUtility.LoadPrefabContents(caminho);
                int abas = 0, fundos = 0;
                try
                {
                    abas = CorrigirRotulosDeAba(raiz);
                    fundos = DesligarRaycastDoFundo(raiz);
                    if (abas > 0 || fundos > 0) PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
                }
                finally { PrefabUtility.UnloadPrefabContents(raiz); }

                if (abas > 0 || fundos > 0)
                    log.Add($"  {Path.GetFileNameWithoutExtension(caminho)}: {abas} abas, {fundos} fundos");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[frontend] prefabs corrigidos:\n" + string.Join("\n", log));
        }

        /// <summary>
        /// Os widgets de estado (Chip_Tab, Item_SegmentedOption) vieram com texto de exemplo no
        /// estado ativo — "ABA" nas abas, "MÉDIA" nas opções — e as telas nunca sobrescreveram.
        /// Resultado: a aba selecionada aparecia escrita "ABA" e toda opção marcada, "MÉDIA".
        ///
        /// O rótulo correto é o do estado inativo (foi ele que o construtor preencheu), então
        /// copia idle -> active em qualquer objeto que tenha os dois estados.
        /// </summary>
        static int CorrigirRotulosDeAba(GameObject raiz)
        {
            int n = 0;
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
            {
                var idle = t.Find("State_Idle/Label")?.GetComponent<TextMeshProUGUI>();
                var ativo = t.Find("State_Active/Label")?.GetComponent<TextMeshProUGUI>();
                if (idle == null || ativo == null) continue;

                // se o inativo também estiver vazio, cai para o sufixo do nome do objeto
                string certo = !string.IsNullOrWhiteSpace(idle.text) && !Placeholder(idle.text)
                    ? idle.text
                    : SufixoDoNome(t.name);

                if (string.IsNullOrWhiteSpace(certo)) continue;
                if (ativo.text != certo) { ativo.text = certo; n++; }
                if (idle.text != certo) { idle.text = certo; n++; }
            }
            return n;
        }

        static bool Placeholder(string s) => s == "ABA" || s == "MÉDIA" || s == "MEDIA";

        static string SufixoDoNome(string nome)
        {
            int i = nome.IndexOf('_');
            return i >= 0 && i < nome.Length - 1 ? nome.Substring(i + 1) : string.Empty;
        }

        /// <summary>
        /// O Fundo cobre a tela inteira; se ele captura raycast, o palco 3D nunca recebe o
        /// arraste para girar o carro (o CarStage desiste quando o ponteiro está sobre UI).
        /// </summary>
        static int DesligarRaycastDoFundo(GameObject raiz)
        {
            int n = 0;
            foreach (var nome in new[] { "Fundo", "Fundo/Piso", "FaixaTopo" })
            {
                var t = raiz.transform.Find(nome);
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img != null && img.raycastTarget) { img.raycastTarget = false; n++; }
            }
            return n;
        }

        // ------------------------------------------------------------------ cena

        [MenuItem("Party Racers/UI/Ligar garagem na cena")]
        public static void LigarGaragemNaCena()
        {
            var canvas = UICaptureTool.CanvasRaiz();
            if (canvas == null) { Debug.LogError("[frontend] sem canvas de telas"); return; }

            var garagem = canvas.transform.Find("Screen_Garage_PC");
            if (garagem == null) { Debug.LogError("[frontend] sem Screen_Garage_PC"); return; }

            var ui = garagem.GetComponent<GarageScreenUI>();
            if (ui == null) { Debug.LogError("[frontend] Screen_Garage_PC sem GarageScreenUI"); return; }

            var customizer = Object.FindObjectsByType<KartVisualCustomizer>(FindObjectsInactive.Include)
                                   .FirstOrDefault();
            var palco = TrocarTurntablePorCarStage(customizer);

            var so = new SerializedObject(ui);
            Set(so, "carro", customizer);
            Set(so, "palco", palco);
            Set(so, "containerCategorias", garagem.Find("PainelCustomizacao/Rolagem/Viewport/Categorias"));
            Set(so, "contagemDeCategorias", garagem.Find("PainelCustomizacao/Contagem")?.GetComponent<TextMeshProUGUI>());
            Set(so, "categoriasRestantes", garagem.Find("PainelCustomizacao/Restantes")?.GetComponent<TextMeshProUGUI>());
            Set(so, "containerIndicadores", garagem.Find("SeletorCarro/Indicadores"));
            Set(so, "nomeDoCarro", garagem.Find("SeletorCarro/NomeCarro")?.GetComponent<TextMeshProUGUI>());
            Set(so, "btnCarroAnterior", garagem.Find("SeletorCarro/Btn_Anterior")?.GetComponent<Button>());
            Set(so, "btnCarroProximo", garagem.Find("SeletorCarro/Btn_Proximo")?.GetComponent<Button>());
            Set(so, "btnCorrer", garagem.Find("Partida/Btn_Correr")?.GetComponent<Button>());
            Set(so, "btnJogarLocalmente", garagem.Find("Partida/Btn_JogarLocalmente")?.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();

            AjustarEnquadramento();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[frontend] garagem ligada. palco=" + (palco != null ? palco.name : "NULO") +
                      " carro=" + (customizer != null ? customizer.CarCount + " carros" : "NULO"));
        }

        /// <summary>
        /// O palco usava o Turntable (só gira sozinho). O CarStage faz o mesmo e ainda dá o
        /// arraste para girar com o mouse e a animação de troca de carro.
        /// </summary>
        static CarStage TrocarTurntablePorCarStage(KartVisualCustomizer customizer)
        {
            Transform raiz = null;
            if (customizer != null)
            {
                raiz = customizer.transform;
                while (raiz.parent != null) raiz = raiz.parent;
            }
            raiz ??= GameObject.Find("Turntable")?.transform;
            if (raiz == null) { Debug.LogWarning("[frontend] não achei o palco do carro"); return null; }

            var antigo = raiz.GetComponent<Turntable>();
            if (antigo != null) Object.DestroyImmediate(antigo);

            return raiz.GetComponent<CarStage>() ?? raiz.gameObject.AddComponent<CarStage>();
        }

        /// <summary>
        /// Sem o painel de lobby o carro tem mais espaço: chega mais perto e mostra a frente,
        /// não a traseira.
        /// </summary>
        static void AjustarEnquadramento()
        {
            var fluxo = Object.FindObjectsByType<FrontendFlow>(FindObjectsInactive.Include).FirstOrDefault();
            if (fluxo == null) return;
            var so = new SerializedObject(fluxo);
            so.FindProperty("giro").floatValue = 202f;              // 3/4 de frente
            so.FindProperty("inclinacao").floatValue = 10f;
            so.FindProperty("afastamentoNoLobby").floatValue = 1.95f;
            so.FindProperty("viesNoLobby").floatValue = 1.45f;
            so.FindProperty("viesVerticalNoLobby").floatValue = 0.28f;
            so.FindProperty("afastamentoNaGaragem").floatValue = 1.12f;
            so.FindProperty("viesNaGaragem").floatValue = 0.22f;    // desvia do painel da esquerda
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(SerializedObject so, string campo, Object valor)
        {
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogWarning("[frontend] campo inexistente: " + campo); return; }
            p.objectReferenceValue = valor;
        }
    }
}
#endif
