using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Importador ÚNICO e DESCARTÁVEL das telas (handoff v2 §0). Lê `Assets/_Projeto/UI/Spec/` e
    /// materializa os prefabs de widget, de item e de tela.
    ///
    /// Ele substitui os quatro builders escritos à mão do v1. A diferença que importa não é o
    /// tamanho do código: é que ali cada número era um chute em C#, e aqui todo número vem do JSON.
    /// Rodar o importador de novo recria a árvore — por isso ele existe para o import INICIAL, e
    /// depois disso as telas são editadas na cena. Terminada a migração, esta pasta sai do projeto.
    /// </summary>
    public static class LayoutImporter
    {
        public const string SpecRoot = "Assets/_Projeto/UI/Spec";
        public const string LayoutRoot = SpecRoot + "/layout";
        public const string ScreenRoot = LayoutBuilder.PrefabRoot + "/Screens";

        private const string Menu = "Party Racers/UI v2/";

        [MenuItem(Menu + "1 · Importar widgets e itens", priority = 10)]
        public static void ImportarWidgets()
        {
            LayoutResources.Limpar();

            JsonValue widgets = LerJson(LayoutRoot + "/_widgets.json");
            if (widgets == null)
                return;

            WidgetFactory.Construir(widgets, sobrescrever: true);

            Relatar($"{WidgetFactory.PrefabsCriados} prefabs de widget/item criados.");
        }

        [MenuItem(Menu + "2 · Importar todas as telas", priority = 11)]
        public static void ImportarTelas()
        {
            LayoutResources.Limpar();
            LayoutBuilder.ZerarContagem();

            GarantirPasta(ScreenRoot);

            var criadas = new List<string>();
            foreach (string arquivo in Directory.GetFiles(LayoutRoot, "Screen_*.json"))
            {
                string tela = ImportarTela(arquivo);
                if (!string.IsNullOrEmpty(tela))
                    criadas.Add(tela);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Relatar($"{criadas.Count} telas importadas ({LayoutBuilder.NosCriados} nós): "
                    + string.Join(", ", criadas));
        }

        [MenuItem(Menu + "0 · Importar TUDO (widgets + telas)", priority = 1)]
        public static void ImportarTudo()
        {
            LayoutResources.Limpar();
            LayoutBuilder.ZerarContagem();

            JsonValue widgets = LerJson(LayoutRoot + "/_widgets.json");
            if (widgets == null)
                return;

            WidgetFactory.Construir(widgets, sobrescrever: true);

            GarantirPasta(ScreenRoot);

            var criadas = new List<string>();
            foreach (string arquivo in Directory.GetFiles(LayoutRoot, "Screen_*.json"))
            {
                string tela = ImportarTela(arquivo);
                if (!string.IsNullOrEmpty(tela))
                    criadas.Add(tela);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Relatar($"{WidgetFactory.PrefabsCriados} widgets + {criadas.Count} telas "
                    + $"({LayoutBuilder.NosCriados} nós).\n" + string.Join(", ", criadas));
        }

        // ------------------------------------------------------------------ Uma tela

        private static string ImportarTela(string arquivo)
        {
            JsonValue spec = LerJson(arquivo);
            if (spec == null)
                return null;

            string nome = spec["screen"].AsString(Path.GetFileNameWithoutExtension(arquivo));

            var raiz = new GameObject(nome, typeof(RectTransform));
            var rect = (RectTransform)raiz.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            foreach (JsonValue no in spec["nodes"].Items)
                LayoutBuilder.Construir(no, raiz.transform);

            string caminho = $"{ScreenRoot}/{nome}.prefab";
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);

            return nome;
        }

        // ------------------------------------------------------------------ Utilidades

        private static JsonValue LerJson(string caminho)
        {
            if (!File.Exists(caminho))
            {
                Debug.LogError($"[UI v2] Especificação não encontrada: {caminho}\n"
                             + "Copie a pasta 'especificacao/' do pacote para Assets/_Projeto/UI/Spec/.");
                return null;
            }

            return JsonValue.Parse(File.ReadAllText(caminho, Encoding.UTF8));
        }

        private static void Relatar(string resumo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[UI v2] " + resumo);

            if (LayoutResources.Pendencias.Count > 0)
            {
                // Pendência não é erro: o handoff manda anotar o que faltou em vez de fabricar
                // sprite por código. Esta lista é a fila de trabalho manual depois do import.
                sb.AppendLine();
                sb.AppendLine($"Pendências ({LayoutResources.Pendencias.Count}) — resolver à mão:");
                foreach (string p in LayoutResources.Pendencias)
                    sb.AppendLine("  • " + p);
            }

            Debug.Log(sb.ToString());
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
