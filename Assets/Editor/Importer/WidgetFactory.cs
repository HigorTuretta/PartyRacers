using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Motion;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Constrói os prefabs compartilhados descritos em `_widgets.json`. Eles vêm primeiro porque
    /// tudo depende deles: 64 nós das seis telas são instâncias, não árvores próprias.
    ///
    /// Estrutura fixa de todo widget:
    ///     Raiz (RectTransform, sem Graphic)
    ///       ├ Shadow  (Image, atrás, deslocada em Y — sombra dura do PLACA)
    ///       ├ Bg      (Image esticada, o sprite do widget)
    ///       └ Label   (TextMeshProUGUI esticado)
    ///
    /// A raiz não carrega Graphic de propósito: com a sombra como FILHO ela desenharia na frente
    /// do fundo, e com a sombra como IRMÃO o widget deixaria de ser um prefab único. Separar em
    /// Shadow/Bg resolve os dois e ainda deixa a sombra editável sem tocar no fundo.
    /// </summary>
    public static class WidgetFactory
    {
        private const string WidgetRoot = LayoutBuilder.PrefabRoot + "/Widgets";
        private const string ItemRoot = LayoutBuilder.PrefabRoot + "/Items";

        /// <summary>Larguras padrão por widget. O JSON só declara altura; a largura quem manda é a tela.</summary>
        private static readonly Dictionary<string, float> LarguraPadrao = new Dictionary<string, float>
        {
            { "Btn_Primary", 320f }, { "Btn_Amber", 260f }, { "Btn_Secondary", 220f },
            { "Btn_Danger", 180f }, { "Chip_Tab", 190f }, { "Chip_SubTab", 150f },
            { "Chip_Currency", 170f }, { "Chip_RoomCode", 180f }, { "Chip_Profile", 220f },
            { "Chip_Stage", 268f }, { "Plate_Amber", 214f }, { "Plate_Ink", 296f },
            { "Plate_Cream", 116f }, { "Bar_Progress", 300f }, { "Selector_Option", 220f },
            { "Brand_Logo", 190f },
        };

        public static int PrefabsCriados { get; private set; }

        public static void Construir(JsonValue widgets, bool sobrescrever)
        {
            PrefabsCriados = 0;

            GarantirPasta(WidgetRoot);
            GarantirPasta(ItemRoot);

            foreach (KeyValuePair<string, JsonValue> par in widgets["widgets"].Fields)
                CriarPrefab(WidgetRoot, par.Key, par.Value, sobrescrever);

            foreach (KeyValuePair<string, JsonValue> par in widgets["items"].Fields)
                CriarPrefab(ItemRoot, par.Key, par.Value, sobrescrever);

            // O lockup da marca não é PNG de propósito (pacote-v2/README): é TMP + a placa âmbar.
            CriarBrandLogo(sobrescrever);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ Um widget

        private static void CriarPrefab(string pasta, string nome, JsonValue desc, bool sobrescrever)
        {
            string caminho = $"{pasta}/{nome}.prefab";

            if (!sobrescrever && AssetDatabase.LoadAssetAtPath<GameObject>(caminho) != null)
                return;

            GameObject raiz = MontarWidget(nome, desc);
            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
            PrefabsCriados++;
        }

        private static GameObject MontarWidget(string nome, JsonValue desc)
        {
            var raiz = new GameObject(nome, typeof(RectTransform));
            var rect = (RectTransform)raiz.transform;

            Vector2 tamanho = TamanhoDe(nome, desc);
            rect.sizeDelta = tamanho;

            string sprite = desc["sprite"].AsString(null);
            float sombra = desc["shadow"].AsFloat(0f);
            string fonte = desc["font"].AsString("Archivo");
            float corpo = TamanhoDeFonte(desc);
            Color corTexto = LayoutResources.Cor(desc["color"].AsString(null), Color.white);

            List<string> estados = NomesDeEstado(desc);

            if (estados.Count > 0)
            {
                // Widget com estados: cada estado é um filho COMPLETO (fundo + rótulo), já
                // estilizado. O binder só faz SetActive — nunca troca sprite nem cor.
                bool primeiro = true;
                foreach (string estado in estados)
                {
                    GameObject no = Filho(raiz.transform, NomeDeEstado(estado));
                    Esticar((RectTransform)no.transform);

                    // Os dois estados nascem montados e estilizados para que a troca em runtime
                    // seja só ligar/desligar — nunca trocar sprite ou cor por código.
                    MontarFundo(no.transform, SpriteDoEstado(sprite, estado), sombra);

                    // O slot de poder e a fileira de classificação são MOLDURA: o conteúdo vem do
                    // binder. Um rótulo aqui vira o nome do widget escrito por cima da arte.
                    if (!EhSoImagem(nome))
                        MontarRotulo(no.transform, nome.ToUpperInvariant(), fonte, corpo, corTexto);

                    no.SetActive(primeiro);
                    primeiro = false;
                }
            }
            else
            {
                MontarFundo(raiz.transform, sprite ?? MolduraPadrao, sombra);

                if (!EhSoImagem(nome))
                    MontarRotulo(raiz.transform, nome.ToUpperInvariant(), fonte, corpo, corTexto);
            }

            MontarFilhosDeclarados(raiz.transform, desc);

            if (nome.StartsWith("Btn_") || nome.StartsWith("Chip_") || nome.StartsWith("Card_"))
                MontarInterativo(raiz, desc);

            return raiz;
        }

        // ------------------------------------------------------------------ Peças

        private static void MontarFundo(Transform pai, string sprite, float sombra)
        {
            if (sombra > 0f)
            {
                GameObject s = Filho(pai, "Shadow");
                var rs = (RectTransform)s.transform;
                Esticar(rs);
                rs.anchoredPosition = Vector2.down * sombra;

                Image imgSombra = s.AddComponent<Image>();
                AplicarSprite(imgSombra, sprite);
                imgSombra.color = LayoutResources.Tinta;
                imgSombra.raycastTarget = false;
            }

            GameObject bg = Filho(pai, "Bg");
            Esticar((RectTransform)bg.transform);

            Image img = bg.AddComponent<Image>();
            AplicarSprite(img, sprite);
            // raycastTarget LIGADO: helper com raycast desligado já matou o clique de um botão
            // montado por código neste projeto. Quem recebe o toque é o Bg.
            img.raycastTarget = true;
        }

        private static void MontarRotulo(Transform pai, string texto, string fonte, float corpo, Color cor)
        {
            GameObject label = Filho(pai, "Label");
            var r = (RectTransform)label.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(14f, 0f);
            r.offsetMax = new Vector2(-14f, 0f);

            var tmp = label.AddComponent<TextMeshProUGUI>();
            tmp.text = texto;
            tmp.fontSize = corpo;
            tmp.color = cor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset asset = LayoutResources.Fonte(fonte);
            if (asset != null)
                tmp.font = asset;
        }

        private static void MontarFilhosDeclarados(Transform pai, JsonValue desc)
        {
            // `children` e `playerChildren` listam as peças que o binder vai procurar por nome
            // (Avatar, Name, Badge_Leader, Btn_Kick...). Elas nascem como nós vazios esticados:
            // existir é o que importa para o binder poder ser ligado; o acabamento é manual.
            MontarLista(pai, desc["children"]);
            MontarLista(pai, desc["playerChildren"]);
        }

        private static void MontarLista(Transform pai, JsonValue lista)
        {
            if (!lista.IsArray)
                return;

            foreach (JsonValue item in lista.Items)
            {
                string nome = item.AsString(null);
                if (string.IsNullOrWhiteSpace(nome))
                    continue;

                if (pai.Find(nome) != null)
                    continue;

                GameObject filho = Filho(pai, nome);
                Esticar((RectTransform)filho.transform);
            }
        }

        private static void MontarInterativo(GameObject raiz, JsonValue desc)
        {
            var botao = raiz.AddComponent<Button>();

            Image alvo = raiz.GetComponentInChildren<Image>(true);
            if (alvo != null)
                botao.targetGraphic = alvo;

            // Transição por cor sobre o sprite estragaria a arte esmaltada. O feedback é o
            // afundamento do UIPress, que é o vocabulário do PLACA.
            botao.transition = Selectable.Transition.None;

            raiz.AddComponent<UIPress>();
        }

        // ------------------------------------------------------------------ Marca

        private static void CriarBrandLogo(bool sobrescrever)
        {
            string caminho = $"{WidgetRoot}/Brand_Logo.prefab";
            if (!sobrescrever && AssetDatabase.LoadAssetAtPath<GameObject>(caminho) != null)
                return;

            var raiz = new GameObject("Brand_Logo", typeof(RectTransform));
            var rect = (RectTransform)raiz.transform;
            rect.sizeDelta = new Vector2(190f, 86f);
            // O grupo inteiro inclinado −2°: é o que dá o ar de adesivo colado torto, e é a razão
            // de o lockup não ser um PNG achatado.
            rect.localRotation = Quaternion.Euler(0f, 0f, -2f);

            GameObject party = Filho(raiz.transform, "PARTY");
            var rp = (RectTransform)party.transform;
            rp.anchorMin = new Vector2(0f, 1f);
            rp.anchorMax = new Vector2(0f, 1f);
            rp.pivot = new Vector2(0f, 1f);
            rp.anchoredPosition = new Vector2(0f, 0f);
            rp.sizeDelta = new Vector2(190f, 40f);
            MontarTexto(party, "PARTY", "Titan One", 27f, LayoutResources.Cor("#FFF7E8", Color.white),
                        TextAlignmentOptions.Left);

            GameObject placa = Filho(raiz.transform, "Plate_Racers");
            var rr = (RectTransform)placa.transform;
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(0f, 1f);
            rr.pivot = new Vector2(0f, 1f);
            rr.anchoredPosition = new Vector2(4f, -40f);
            rr.sizeDelta = new Vector2(140f, 34f);

            Image img = placa.AddComponent<Image>();
            AplicarSprite(img, "Brand/Brand_Plate_Racers");
            img.raycastTarget = false;

            GameObject racers = Filho(placa.transform, "RACERS");
            Esticar((RectTransform)racers.transform);
            MontarTexto(racers, "RACERS", "Titan One", 19f, LayoutResources.Cor("#15161C", Color.black),
                        TextAlignmentOptions.Center);

            PrefabUtility.SaveAsPrefabAsset(raiz, caminho);
            Object.DestroyImmediate(raiz);
            PrefabsCriados++;
        }

        private static void MontarTexto(GameObject alvo, string texto, string fonte, float corpo,
                                        Color cor, TextAlignmentOptions alinhamento)
        {
            var tmp = alvo.AddComponent<TextMeshProUGUI>();
            tmp.text = texto;
            tmp.fontSize = corpo;
            tmp.color = cor;
            tmp.alignment = alinhamento;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            TMP_FontAsset asset = LayoutResources.Fonte(fonte);
            if (asset != null)
                tmp.font = asset;
        }

        // ------------------------------------------------------------------ Utilidades

        private static void AplicarSprite(Image img, string chave)
        {
            if (string.IsNullOrEmpty(chave))
                return;

            img.sprite = LayoutResources.Sprite(chave);
            img.type = LayoutResources.TipoDeImagem(chave);
            img.pixelsPerUnitMultiplier = 1f;
        }

        private static Vector2 TamanhoDe(string nome, JsonValue desc)
        {
            JsonValue size = desc["size"];
            if (size.IsArray && size.Count >= 2)
                return new Vector2(size[0].AsFloat(), size[1].AsFloat());

            JsonValue minSize = desc["minSize"];
            if (minSize.IsArray && minSize.Count >= 2)
                return new Vector2(minSize[0].AsFloat(), minSize[1].AsFloat());

            float altura = desc["height"].AsFloat(64f);
            float largura = LarguraPadrao.TryGetValue(nome, out float l) ? l : 240f;
            return new Vector2(largura, altura);
        }

        /// <summary>`size` é corpo de fonte quando é número e dimensão quando é par.</summary>
        private static float TamanhoDeFonte(JsonValue desc)
        {
            JsonValue size = desc["size"];
            return size.IsNumber ? size.AsFloat(20f) : 20f;
        }

        private static List<string> NomesDeEstado(JsonValue desc)
        {
            var nomes = new List<string>();
            JsonValue estados = desc["states"];

            if (estados.IsArray)
            {
                foreach (JsonValue e in estados.Items)
                {
                    string nome = e.AsString(null);
                    if (!string.IsNullOrWhiteSpace(nome))
                        nomes.Add(nome);
                }
            }

            return nomes;
        }

        private static string NomeDeEstado(string bruto) =>
            bruto.StartsWith("State_") ? bruto : "State_" + bruto;

        private const string MolduraPadrao = "Frames/UI_Card_R18_Ink";

        /// <summary>
        /// Sprite de um estado. Vários itens (`Row_Standing`, `Card_MatchSlot`…) declaram os
        /// estados sem dizer a moldura de cada um — a moldura está descrita nas telas que os
        /// usam. Sem esta derivação eles nasciam como retângulos BRANCOS, que é como uma Image
        /// sem sprite aparece.
        ///
        /// As regras vêm da linguagem visual do PLACA: âmbar é destaque, tracejado é vaga livre,
        /// violeta é bot, escuro é o caso comum.
        /// </summary>
        private static string SpriteDoEstado(string spriteDoWidget, string estado)
        {
            if (estado.Contains("Empty") || estado.Contains("Free"))
                return "Frames/UI_Dashed_R18";

            // Âmbar é "esta linha é a MINHA", não "este é o estado ligado". Incluir "Active" aqui
            // pintou de âmbar todo Chip_Tab do projeto — inclusive o chip de última volta e o de
            // delta de posição, que são leitura, não destaque.
            if (estado.Contains("IsLocal") || estado.Contains("Mate") || estado.Contains("Equipped"))
                return "Frames/UI_Button_R22_Amber";

            if (estado.Contains("Bot"))
                return "Frames/UI_Card_R18_Royal";

            if (estado.Contains("Locked"))
                return "Frames/UI_Card_R18_Deep";

            return spriteDoWidget ?? MolduraPadrao;
        }

        /// <summary>Widgets que são só moldura/ícone e não devem nascer com rótulo.</summary>
        private static bool EhSoImagem(string nome) =>
            nome == "Btn_Icon" || nome == "Bar_Progress" || nome == "Slot_Power" ||
            nome == "Slot_Shield" || nome == "Blip_Player" ||
            nome.StartsWith("Row_") || nome.StartsWith("Card_") || nome == "Toast_Item";

        private static GameObject Filho(Transform pai, string nome)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return go;
        }

        private static void Esticar(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static void GarantirPasta(string caminho)
        {
            if (AssetDatabase.IsValidFolder(caminho))
                return;

            int barra = caminho.LastIndexOf('/');
            string pai = caminho.Substring(0, barra);
            string nome = caminho.Substring(barra + 1);

            GarantirPasta(pai);
            AssetDatabase.CreateFolder(pai, nome);
        }
    }
}
