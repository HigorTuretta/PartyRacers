using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Materializa um nó do layout.json como hierarquia de UGUI de verdade — GameObjects editáveis
    /// à mão depois, não desenho em runtime.
    ///
    /// Esta é a ÚNICA pasta do projeto onde é permitido escrever geometria de UI por código
    /// (handoff v2 §1). A razão de existir é justamente tirar a geometria do C#: aqui nenhum número
    /// é inventado, todos vêm do JSON. Terminado o import, os builders antigos saem do projeto e
    /// as telas passam a ser editadas na cena.
    ///
    /// Convenções que o construtor respeita:
    /// • A ordem dos `children` É a ordem de hierarquia, que É o z-order.
    /// • `shadow` vira uma Image irmã ATRÁS, nunca o componente Shadow da Unity (que borra).
    /// • `states` são filhos irmãos mutuamente exclusivos; todos existem na cena.
    /// • Sprite com border conhecido entra como Sliced; o resto, Simple.
    /// </summary>
    public static class LayoutBuilder
    {
        // Pasta PRÓPRIA, separada de Prefabs/UI/. Na primeira tentativa o importador salvou nos
        // mesmos caminhos dos widgets que já existiam (Btn_Primary, Chip_Tab, Toast_Item…) e,
        // como esses widgets são instanciados nas 11 telas do jogo, sobrescrevê-los quebrou a UI
        // inteira — não só as telas que estavam sendo geradas.
        public const string PrefabRoot = "Assets/_Projeto/Prefabs/UI_v2";

        /// <summary>Nós criados, por caminho — o relatório do import lista quantos e quais.</summary>
        public static int NosCriados { get; private set; }

        public static void ZerarContagem() => NosCriados = 0;

        // ------------------------------------------------------------------ Entrada

        public static GameObject Construir(JsonValue node, Transform parent)
        {
            if (node == null || !node.IsObject)
                return null;

            string nome = node["name"].AsString("Node");
            string tipo = node["type"].AsString("Rect");

            // `prefab` diz que este nó É uma instância — não recriar os filhos dele.
            string prefab = node["prefab"].AsString(null);
            GameObject go = !string.IsNullOrEmpty(prefab)
                ? InstanciarPrefab(prefab, nome, parent)
                : NovoNo(nome, parent);

            if (go == null)
                return null;

            NosCriados++;

            RectTransform rect = go.GetComponent<RectTransform>();
            AplicarRect(rect, node);

            if (string.IsNullOrEmpty(prefab))
                AplicarTipo(go, node, tipo);
            else
                AplicarSobrescritasDePrefab(go, node);

            AplicarSombra(go, node);
            AplicarContorno(go, node);
            AplicarTexto(go, node, tipo);

            ConstruirFilhos(go, node);
            ConstruirEstados(go, node);
            ConstruirItensRepetidos(go, node);

            return go;
        }

        // ------------------------------------------------------------------ Criação

        private static GameObject NovoNo(string nome, Transform parent)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject InstanciarPrefab(string chave, string nome, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{chave}.prefab");

            // A spec discorda de si mesma em alguns nós: `Toast_Item` é declarado no bloco
            // `widgets` de _widgets.json mas referenciado como `Items/Toast_Item` pelo HUD.
            // Em vez de duplicar o prefab nas duas pastas, procura na pasta irmã.
            if (asset == null)
                asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{PastaIrma(chave)}.prefab");

            if (asset == null)
            {
                LayoutResources.RegistrarPendencia($"prefab ausente: {chave}");
                return NovoNo(nome, parent);
            }

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instancia.name = nome;
            return instancia;
        }

        /// <summary>"Items/Toast_Item" ↔ "Widgets/Toast_Item".</summary>
        private static string PastaIrma(string chave)
        {
            if (chave.StartsWith("Items/"))
                return "Widgets/" + chave.Substring("Items/".Length);

            if (chave.StartsWith("Widgets/"))
                return "Items/" + chave.Substring("Widgets/".Length);

            return chave;
        }

        // ------------------------------------------------------------------ Geometria

        private static void AplicarRect(RectTransform rect, JsonValue node)
        {
            if (rect == null)
                return;

            // Overlays de tela cheia (arco de perigo, flashes) vêm com `stretch: true` ou
            // simplesmente sem geometria nenhuma. Sem este caso eles nascem com o 100×100 padrão
            // do RectTransform e viram um quadradinho no meio da tela.
            bool telaCheia = node["stretch"].AsBool(false)
                          || (!node.Has("anchorMin") && !node.Has("sizeDelta") && !node.Has("offsetMin"));

            if (telaCheia)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                return;
            }

            if (node.Has("anchorMin")) rect.anchorMin = Vetor(node["anchorMin"], rect.anchorMin);
            if (node.Has("anchorMax")) rect.anchorMax = Vetor(node["anchorMax"], rect.anchorMax);
            if (node.Has("pivot")) rect.pivot = Vetor(node["pivot"], rect.pivot);

            // offsetMin/Max aparecem quando o nó ESTICA; anchoredPosition/sizeDelta quando é fixo.
            // O JSON nunca traz os dois, e aplicar os dois se anulariam.
            if (node.Has("offsetMin") || node.Has("offsetMax"))
            {
                rect.offsetMin = Vetor(node["offsetMin"], Vector2.zero);
                rect.offsetMax = Vetor(node["offsetMax"], Vector2.zero);
            }
            else
            {
                if (node.Has("sizeDelta")) rect.sizeDelta = Vetor(node["sizeDelta"], rect.sizeDelta);
                if (node.Has("anchoredPosition")) rect.anchoredPosition = Vetor(node["anchoredPosition"], rect.anchoredPosition);
            }

            if (node.Has("rotationZ"))
                rect.localRotation = Quaternion.Euler(0f, 0f, node["rotationZ"].AsFloat());

            rect.localScale = Vector3.one;
        }

        private static Vector2 Vetor(JsonValue v, Vector2 fallback)
        {
            if (v == null || !v.IsArray || v.Count < 2)
                return fallback;

            return new Vector2(v[0].AsFloat(), v[1].AsFloat());
        }

        // ------------------------------------------------------------------ Tipos

        private static void AplicarTipo(GameObject go, JsonValue node, string tipo)
        {
            switch (tipo)
            {
                case "Image":
                    AplicarImagem(go, node);
                    break;

                case "RawImage":
                    // Janela da cena 3D. Fica sem textura de propósito: quem preenche é a câmera,
                    // em runtime. Mas o padrão da Unity para RawImage sem textura é BRANCO, e um
                    // retângulo branco de tela cheia clareia todo o fundo — os painéis de vidro
                    // escuro passam a parecer cinza. O fallback certo é a tinta do tema.
                    var raw = go.AddComponent<RawImage>();
                    raw.color = LayoutResources.Cor(node["color"].AsString(null), LayoutResources.Tinta);
                    raw.raycastTarget = false;
                    break;

                case "Text":
                    // O componente é criado em AplicarTexto — aqui não há nada a fazer.
                    break;

                case "HLayout":
                    ConfigurarLayoutLinear(go.AddComponent<HorizontalLayoutGroup>(), node);
                    break;

                case "VLayout":
                    ConfigurarLayoutLinear(go.AddComponent<VerticalLayoutGroup>(), node);
                    break;

                case "Grid":
                    ConfigurarGrid(go.AddComponent<GridLayoutGroup>(), node);
                    break;

                case "ScrollRect":
                    ConfigurarScroll(go, node);
                    break;

                case "Mask":
                    AplicarImagem(go, node);
                    go.AddComponent<Mask>().showMaskGraphic = node.Has("sprite");
                    break;

                case "PingPong":
                    // A agulha do dial. A geometria vem do JSON; o vaivém é animação e mora no
                    // componente de movimento, atribuído à mão depois do import.
                    AplicarImagem(go, node);
                    LayoutResources.RegistrarPendencia(
                        $"'{go.name}': nó PingPong — anexar o componente de varredura na cena");
                    break;

                default:
                    // Rect e tipos desconhecidos: contêiner puro, sem Graphic.
                    if (node.Has("sprite") || node.Has("color"))
                        AplicarImagem(go, node);
                    break;
            }

            if (node.Has("raycastTarget"))
            {
                var grafico = go.GetComponent<Graphic>();
                if (grafico != null)
                    grafico.raycastTarget = node["raycastTarget"].AsBool(true);
            }
        }

        private static Image AplicarImagem(GameObject go, JsonValue node)
        {
            var img = go.GetComponent<Image>();
            if (img == null)
                img = go.AddComponent<Image>();

            string chave = node["sprite"].AsString(null);
            if (!string.IsNullOrEmpty(chave))
            {
                img.sprite = LayoutResources.Sprite(chave);
                img.type = LayoutResources.TipoDeImagem(chave);
                // Os sprites do v2 foram desenhados no tamanho de uso final: multiplicador 1 é o
                // valor correto. Era 2,4 no v1 e foi o que transformou botão em pílula sem contorno.
                img.pixelsPerUnitMultiplier = 1f;
            }

            // Um nó com gradiente (vinheta) não tem como ser desenhado por uma Image plana. Em vez
            // de virar um retângulo BRANCO de tela cheia, aproxima pela parada mais forte com meia
            // opacidade e registra a pendência — o gradiente de verdade é sprite ou shader, e o
            // handoff proíbe fabricar sprite por código.
            JsonValue gradiente = node["gradient"];
            if (gradiente.IsArray && gradiente.Count > 0)
            {
                Color forte = LayoutResources.Cor(gradiente[gradiente.Count - 1].AsString(null), LayoutResources.Tinta);
                img.color = new Color(forte.r, forte.g, forte.b, forte.a * 0.6f);
                img.raycastTarget = false;
                LayoutResources.RegistrarPendencia($"'{go.name}': gradiente aproximado por cor plana");
                return img;
            }

            // Sem sprite e sem cor o nó é um contêiner, não uma superfície. Deixá-lo branco
            // opaco esconderia tudo o que está atrás.
            bool temSuperficie = !string.IsNullOrEmpty(chave) || node.Has("color");
            img.color = temSuperficie
                ? LayoutResources.Cor(node["color"].AsString(null), Color.white)
                : new Color(1f, 1f, 1f, 0f);

            return img;
        }

        private static void ConfigurarLayoutLinear(HorizontalOrVerticalLayoutGroup grupo, JsonValue node)
        {
            grupo.spacing = node["spacing"].AsFloat(0f);
            grupo.childAlignment = Alinhamento(node["childAlignment"].AsString("MiddleCenter"));
            grupo.childForceExpandWidth = node["childForceExpandWidth"].AsBool(false);
            grupo.childForceExpandHeight = false;
            grupo.childControlWidth = false;
            grupo.childControlHeight = node["childControlHeight"].AsBool(false);
            grupo.reverseArrangement = node["reverseArrangement"].AsBool(false);

            JsonValue pad = node["padding"];
            if (pad.IsArray && pad.Count >= 4)
                grupo.padding = new RectOffset(pad[0].AsInt(), pad[1].AsInt(), pad[2].AsInt(), pad[3].AsInt());
        }

        private static void ConfigurarGrid(GridLayoutGroup grid, JsonValue node)
        {
            grid.cellSize = Vetor(node["cellSize"], new Vector2(100f, 100f));
            grid.spacing = Vetor(node["spacing"], Vector2.zero);
            grid.childAlignment = Alinhamento(node["childAlignment"].AsString("UpperLeft"));

            if (node.Has("columns"))
            {
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = Mathf.Max(1, node["columns"].AsInt(1));
            }
        }

        private static void ConfigurarScroll(GameObject go, JsonValue node)
        {
            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = node["horizontal"].AsBool(false);
            scroll.vertical = node["vertical"].AsBool(true);
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            // Recorte: sem o RectMask2D a lista vaza por cima do painel de vidro.
            go.AddComponent<RectMask2D>();
        }

        private static TextAnchor Alinhamento(string nome) => nome switch
        {
            "UpperLeft" => TextAnchor.UpperLeft,
            "UpperCenter" => TextAnchor.UpperCenter,
            "UpperRight" => TextAnchor.UpperRight,
            "MiddleLeft" => TextAnchor.MiddleLeft,
            "MiddleRight" => TextAnchor.MiddleRight,
            "LowerLeft" => TextAnchor.LowerLeft,
            "LowerCenter" => TextAnchor.LowerCenter,
            "LowerRight" => TextAnchor.LowerRight,
            _ => TextAnchor.MiddleCenter,
        };

        // ------------------------------------------------------------------ Sombra e contorno

        private static void AplicarSombra(GameObject go, JsonValue node)
        {
            if (!node.Has("shadow"))
                return;

            float deslocamento = node["shadow"].AsFloat();
            if (Mathf.Approximately(deslocamento, 0f))
                return;

            var alvo = go.GetComponent<Image>();
            if (alvo == null)
                return;

            // Irmã ATRÁS, não o componente Shadow da Unity: o componente borra o resultado e o
            // caráter PLACA depende de a sombra ser dura, com a mesma silhueta do objeto.
            // O nome carrega o do alvo porque a irmã NÃO acompanha o SetActive dele — quem
            // esconde o nó precisa saber qual sombra esconder junto.
            var sombra = new GameObject(go.name + "_Shadow", typeof(RectTransform));
            sombra.transform.SetParent(go.transform.parent, false);
            sombra.transform.SetSiblingIndex(go.transform.GetSiblingIndex());

            var rectAlvo = (RectTransform)go.transform;
            var rectSombra = (RectTransform)sombra.transform;
            rectSombra.anchorMin = rectAlvo.anchorMin;
            rectSombra.anchorMax = rectAlvo.anchorMax;
            rectSombra.pivot = rectAlvo.pivot;
            rectSombra.sizeDelta = rectAlvo.sizeDelta;
            rectSombra.offsetMin = rectAlvo.offsetMin;
            rectSombra.offsetMax = rectAlvo.offsetMax;
            rectSombra.anchoredPosition = rectAlvo.anchoredPosition + Vector2.down * deslocamento;
            rectSombra.localRotation = rectAlvo.localRotation;

            var img = sombra.AddComponent<Image>();
            img.sprite = alvo.sprite;
            img.type = alvo.type;
            img.pixelsPerUnitMultiplier = alvo.pixelsPerUnitMultiplier;
            img.color = LayoutResources.Tinta;
            img.raycastTarget = false;

            NosCriados++;
        }

        private static void AplicarContorno(GameObject go, JsonValue node)
        {
            if (!node.Has("stroke"))
                return;

            string cor = node["stroke"].AsString(null);
            if (string.IsNullOrEmpty(cor))
                return;

            var alvo = go.GetComponent<Image>();
            if (alvo == null)
                return;

            float espessura = node["strokeWidth"].AsFloat(2f);

            // O contorno é uma cópia inflada desenhada POR BAIXO. Vai como irmão anterior, igual à
            // sombra: no UGUI o filho desenha sempre na frente do pai, então pendurar o contorno
            // dentro do nó o colocaria em cima do que ele deveria contornar.
            var contorno = new GameObject(go.name + "_Stroke", typeof(RectTransform));
            contorno.transform.SetParent(go.transform.parent, false);
            contorno.transform.SetSiblingIndex(go.transform.GetSiblingIndex());

            var rectAlvo = (RectTransform)go.transform;
            var rectContorno = (RectTransform)contorno.transform;
            rectContorno.anchorMin = rectAlvo.anchorMin;
            rectContorno.anchorMax = rectAlvo.anchorMax;
            rectContorno.pivot = rectAlvo.pivot;
            rectContorno.anchoredPosition = rectAlvo.anchoredPosition;
            rectContorno.sizeDelta = rectAlvo.sizeDelta + Vector2.one * (espessura * 2f);
            rectContorno.localRotation = rectAlvo.localRotation;

            var img = contorno.AddComponent<Image>();
            img.sprite = alvo.sprite;
            img.type = alvo.type;
            img.pixelsPerUnitMultiplier = alvo.pixelsPerUnitMultiplier;
            img.color = LayoutResources.Cor(cor, LayoutResources.Tinta);
            img.raycastTarget = false;

            NosCriados++;
        }

        // ------------------------------------------------------------------ Texto

        private static void AplicarTexto(GameObject go, JsonValue node, string tipo)
        {
            JsonValue t = node["text"];
            if (!t.IsObject)
                return;

            // Nó do tipo Text: o TMP mora nele mesmo. Qualquer outro tipo (uma placa com sprite,
            // um botão): o TMP entra como filho esticado, senão o Image e o texto brigariam pelo
            // mesmo Graphic.
            TextMeshProUGUI tmp;
            if (tipo == "Text" && go.GetComponent<Graphic>() == null)
            {
                tmp = go.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp == null)
                {
                    var filho = new GameObject("Label", typeof(RectTransform));
                    filho.transform.SetParent(go.transform, false);
                    var r = (RectTransform)filho.transform;
                    r.anchorMin = Vector2.zero;
                    r.anchorMax = Vector2.one;
                    r.offsetMin = Vector2.zero;
                    r.offsetMax = Vector2.zero;
                    tmp = filho.AddComponent<TextMeshProUGUI>();
                    NosCriados++;
                }
            }

            tmp.text = t["value"].AsString(string.Empty);
            tmp.fontSize = t["size"].AsFloat(22f);
            tmp.color = LayoutResources.Cor(t["color"].AsString(null), Color.white);
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset fonte = LayoutResources.Fonte(t["font"].AsString("Archivo"));
            if (fonte != null)
                tmp.font = fonte;

            tmp.alignment = t["align"].AsString("Center") switch
            {
                "Left" => TextAlignmentOptions.Left,
                "Right" => TextAlignmentOptions.Right,
                _ => TextAlignmentOptions.Center,
            };

            // Números tabulares: sem isto o cronômetro "dança" a cada dígito que muda de largura,
            // e um cronômetro que treme é a coisa mais fácil de notar numa HUD. A feature tnum é
            // ligada no material do font asset, não por instância de texto — fica como pendência
            // manual em vez de virar uma chamada de API que não existe.
            if (t["tabularNums"].AsBool(false))
                LayoutResources.RegistrarPendencia($"'{go.name}': ligar tnum (números tabulares) no font asset");
        }

        // ------------------------------------------------------------------ Filhos

        private static void ConstruirFilhos(GameObject go, JsonValue node)
        {
            JsonValue filhos = node["children"];
            if (!filhos.IsArray)
                return;

            foreach (JsonValue filho in filhos.Items)
            {
                if (filho.IsObject)
                {
                    Construir(filho, go.transform);
                    continue;
                }

                // Alguns nós listam os filhos só pelo NOME ("Label (ESCUDO, Archivo Black 11)").
                // Cria o marcador com o nome limpo para que a peça exista na cena e o binder possa
                // ser ligado; o acabamento é feito à mão depois.
                string bruto = filho.AsString(null);
                if (string.IsNullOrWhiteSpace(bruto))
                    continue;

                string nome = LimparNome(bruto);
                var marcador = new GameObject(nome, typeof(RectTransform));
                marcador.transform.SetParent(go.transform, false);
                Esticar((RectTransform)marcador.transform);
                NosCriados++;
            }
        }

        /// <summary>"Track (Bars/Bar_Track, 3 segmentos)" → "Track".</summary>
        private static string LimparNome(string bruto)
        {
            int parenteses = bruto.IndexOf('(');
            string nome = parenteses > 0 ? bruto.Substring(0, parenteses) : bruto;
            return nome.Trim();
        }

        private static void Esticar(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------ Estados

        private static void ConstruirEstados(GameObject go, JsonValue node)
        {
            JsonValue estados = node["states"];
            if (estados.IsNull)
                return;

            // Estados podem vir como lista de nomes ou como objeto nome→descrição. Nos dois casos
            // o resultado é o mesmo: um filho por estado, todos existindo na cena, e o script só
            // faz SetActive. Estado nunca é cor calculada em runtime.
            var nomes = new List<string>();

            if (estados.IsArray)
            {
                foreach (JsonValue e in estados.Items)
                {
                    string nome = e.AsString(null);
                    if (!string.IsNullOrWhiteSpace(nome))
                        nomes.Add(nome);
                }
            }
            else if (estados.IsObject)
            {
                foreach (KeyValuePair<string, JsonValue> par in estados.Fields)
                    nomes.Add(par.Key);
            }

            bool primeiro = true;
            foreach (string nome in nomes)
            {
                JsonValue descricao = estados.IsObject ? estados[nome] : JsonValue.Null;

                var estado = new GameObject(nome.StartsWith("State_") ? nome : "State_" + nome,
                                            typeof(RectTransform));
                estado.transform.SetParent(go.transform, false);
                Esticar((RectTransform)estado.transform);
                NosCriados++;

                if (descricao.IsObject && (descricao.Has("sprite") || descricao.Has("color")))
                    AplicarImagem(estado, descricao);

                // Só o primeiro estado nasce ligado. Com todos ligados a tela abriria com os
                // quatro empilhados e ninguém entenderia o mock.
                estado.SetActive(primeiro);
                primeiro = false;
            }
        }

        // ------------------------------------------------------------------ Listas fixas

        private static void ConstruirItensRepetidos(GameObject go, JsonValue node)
        {
            string itemPrefab = node["itemPrefab"].AsString(null);
            if (string.IsNullOrEmpty(itemPrefab))
                return;

            // As N instâncias já ficam na cena (handoff v2 §2): grade da sala, standings, toasts.
            // Só lista de tamanho desconhecido (amigos) usa Instantiate em runtime.
            int quantidade = node.Has("count") ? node["count"].AsInt(0)
                           : node.Has("maxRows") ? node["maxRows"].AsInt(0)
                           : node.Has("maxVisible") ? node["maxVisible"].AsInt(0)
                           : 0;

            if (quantidade <= 0)
                return;

            for (int i = 0; i < quantidade; i++)
            {
                GameObject item = InstanciarPrefab(itemPrefab, $"{NomeCurto(itemPrefab)}_{i + 1}", go.transform);
                if (item != null)
                    NosCriados++;
            }
        }

        private static string NomeCurto(string chave)
        {
            int barra = chave.LastIndexOf('/');
            return barra >= 0 ? chave.Substring(barra + 1) : chave;
        }

        // ------------------------------------------------------------------ Prefabs

        private static void AplicarSobrescritasDePrefab(GameObject go, JsonValue node)
        {
            // O nó é uma instância: os filhos dele já vêm do prefab. O JSON só ajusta o que é
            // específico daquele uso — o sprite variante e a cor.
            if (!node.Has("sprite") && !node.Has("color"))
                return;

            var img = go.GetComponent<Image>();
            if (img == null)
                img = go.GetComponentInChildren<Image>(true);

            if (img == null)
                return;

            string chave = node["sprite"].AsString(null);
            if (!string.IsNullOrEmpty(chave))
            {
                img.sprite = LayoutResources.Sprite(chave);
                img.type = LayoutResources.TipoDeImagem(chave);
                img.pixelsPerUnitMultiplier = 1f;
            }

            if (node.Has("color"))
                img.color = LayoutResources.Cor(node["color"].AsString(null), img.color);
        }
    }
}
