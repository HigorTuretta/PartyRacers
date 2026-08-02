#if UNITY_EDITOR
using System.Linq;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Settings;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Duas coisas no Screen_Lobby:
    ///
    /// 1. Reancora a coluna de conteúdo para esticar entre o topo e a base. Antes ela tinha
    ///    altura fixa de 872 começando em y=152, ou seja, 1024 de 1080 — e como o canvas encolhe
    ///    em telas mais largas que 16:9 (a 2:1 sobram ~1015), a linha de botões ficava cortada
    ///    para fora da tela. Agora ENTRAR POR CÓDIGO / SAIR / CORRER estão presos à base.
    ///
    /// 2. Monta o cartão de seleção de pista no espaço vazio da direita, abaixo do carro.
    /// </summary>
    public static class LobbyMapBuilder
    {
        const string PREFAB = "Assets/_Projeto/Prefabs/UI/Screens/Screen_Lobby.prefab";
        const string JOIN_PREFAB = "Assets/_Projeto/Prefabs/UI/Screens/Screen_JoinCode.prefab";

        [MenuItem("Party Racers/UI/Montar seleção de mapa no lobby")]
        public static void Montar()
        {
            var raiz = PrefabUtility.LoadPrefabContents(PREFAB);
            try
            {
                ReancorarConteudo(raiz.transform);
                PolirPalcoDoKart(raiz.transform);
                MontarCartaoDePista(raiz.transform);
                AplicarMovimentoNosBotoes(raiz.transform);
                PrefabUtility.SaveAsPrefabAsset(raiz, PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }

            PolirJoinCode();
            Debug.Log("[lobby] layout, estados, botões e enquadramento polidos");
        }

        // ------------------------------------------------------------------ layout
        static void ReancorarConteudo(Transform tela)
        {
            var conteudo = tela.Find("Conteudo") as RectTransform;
            if (conteudo == null) { Debug.LogError("[lobby] sem Conteudo"); return; }

            // estica na vertical: 152 do topo, 40 da base; 1060 de largura a partir de x=56
            conteudo.anchorMin = new Vector2(0f, 0f);
            conteudo.anchorMax = new Vector2(0f, 1f);
            conteudo.pivot = new Vector2(0f, 0.5f);
            conteudo.offsetMin = new Vector2(56f, 40f);
            conteudo.offsetMax = new Vector2(56f + 1060f, -152f);

            // a grade de vagas é quem absorve a sobra ou a falta de altura
            foreach (var (nome, flexivel) in new[]
                     { ("Topo", 0f), ("Vagas", 1f), ("Aviso", 0f), ("Acoes", 0f) })
            {
                var t = conteudo.Find(nome);
                var le = t != null ? t.GetComponent<LayoutElement>() : null;
                if (le == null) continue;
                le.flexibleHeight = flexivel;
                if (nome == "Vagas") le.minHeight = 300f;
            }

            // célula um pouco mais baixa para as 8 linhas caberem também a 2:1
            var grade = conteudo.Find("Vagas")?.GetComponent<GridLayoutGroup>();
            if (grade != null)
            {
                grade.cellSize = new Vector2(grade.cellSize.x, 56f);
                grade.spacing = new Vector2(grade.spacing.x, 8f);
            }

            EstadoPadraoVazio(conteudo);
        }

        /// <summary>
        /// O prefab precisa representar o estado offline real, e não uma sala fictícia já pronta.
        /// Assim ele também continua honesto caso o binder falhe antes do primeiro frame.
        /// </summary>
        static void EstadoPadraoVazio(Transform conteudo)
        {
            var vagas = conteudo.Find("Vagas");
            if (vagas != null)
            {
                int i = 0;
                foreach (Transform vaga in vagas)
                {
                    bool primeira = i++ == 0;
                    Ligar(vaga, "State_Player", primeira);
                    Ligar(vaga, "State_Disconnected", false);
                    Ligar(vaga, "State_Empty", !primeira);

                    if (primeira)
                    {
                        Escrever(vaga, "State_Player/Nome", "JOGADOR (você)");
                        Ligar(vaga, "State_Player/State_Ready", false);
                        Ligar(vaga, "State_Player/State_Waiting", true);
                        Ligar(vaga, "State_Player/Destaque_IsLocal", true);
                    }
                }
            }

            Escrever(conteudo, "Topo/CodigoSala/Codigo", "SEM SALA");
            var codigo = conteudo.Find("Topo/CodigoSala/Codigo")?.GetComponent<TextMeshProUGUI>();
            if (codigo != null)
            {
                codigo.fontSize = 34f;
                codigo.characterSpacing = 2f;
            }
            Escrever(conteudo, "Topo/CodigoSala/Btn_Copiar/Label", "CRIAR SALA");
            Escrever(conteudo, "Topo/Contagem/Valor", "1");
            Escrever(conteudo, "Topo/Contagem/Maximo", "/16");
            Escrever(conteudo, "Aviso/Texto", "Crie uma sala online ou entre com o código de um amigo.");
            Escrever(conteudo, "Acoes/Btn_EntrarPorCodigo/Label", "ENTRAR POR CÓDIGO");
            Escrever(conteudo, "Acoes/Btn_SairDaSala/Label", "GARAGEM");
            Escrever(conteudo, "Acoes/State_Pronto/Label", "JOGAR LOCAL");

            Ligar(conteudo, "Acoes/EstadoPartida", false);
            Ligar(conteudo, "Acoes/State_Pronto", true);
        }

        static void Ligar(Transform raiz, string caminho, bool ativo)
        {
            var t = raiz.Find(caminho);
            if (t != null && t.gameObject.activeSelf != ativo) t.gameObject.SetActive(ativo);
        }

        static void Escrever(Transform raiz, string caminho, string texto)
        {
            var t = raiz.Find(caminho)?.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = texto;
        }

        static void PolirPalcoDoKart(Transform tela)
        {
            var palco = tela.Find("PalcoCarro") as RectTransform;
            if (palco != null)
            {
                palco.anchorMin = palco.anchorMax = palco.pivot = Vector2.one;
                palco.anchoredPosition = new Vector2(-42f, -112f);
                palco.sizeDelta = new Vector2(720f, 440f);
            }

            var selo = tela.Find("PalcoCarro/Selo") as RectTransform;
            if (selo != null)
            {
                // O selo antigo ficava solto no meio da coluna direita. Agora pertence claramente
                // à área do kart e não concorre com o card da pista.
                selo.anchorMin = selo.anchorMax = selo.pivot = Vector2.one;
                selo.anchoredPosition = new Vector2(-20f, -20f);
                selo.sizeDelta = new Vector2(430f, 48f);
                Escrever(selo, "Label", "SEU KART · EDITE NA GARAGEM");
            }
        }

        static void PolirJoinCode()
        {
            var raiz = PrefabUtility.LoadPrefabContents(JOIN_PREFAB);
            try
            {
                AplicarMovimentoNosBotoes(raiz.transform);
                PrefabUtility.SaveAsPrefabAsset(raiz, JOIN_PREFAB);
            }
            finally { PrefabUtility.UnloadPrefabContents(raiz); }
        }

        static void AplicarMovimentoNosBotoes(Transform raiz)
        {
            foreach (Button button in raiz.GetComponentsInChildren<Button>(true))
            {
                string caminho = AnimationUtility.CalculateTransformPath(button.transform, raiz);
                bool destaque = caminho.Contains("State_Pronto") ||
                                 caminho.Contains("Btn_Copiar") ||
                                 caminho.Contains("Btn_Entrar");

                var press = button.GetComponent<UIPress>();
                if (press == null)
                    press = button.gameObject.AddComponent<UIPress>();
                press.SetEmphasized(destaque);
                EditorUtility.SetDirty(press);
            }
        }

        // ------------------------------------------------------------------ cartão
        static void MontarCartaoDePista(Transform tela)
        {
            var antigo = tela.Find("Painel_Mapa");
            if (antigo != null)
            {
                Debug.Log("[lobby] cartão de pista existente preservado");
                return;
            }

            var painel = Novo("Painel_Mapa", tela);
            painel.anchorMin = painel.anchorMax = new Vector2(1f, 0f);
            painel.pivot = new Vector2(1f, 0f);
            painel.anchoredPosition = new Vector2(-56f, 40f);
            painel.sizeDelta = new Vector2(700f, 356f);

            var sombra = Novo("Sombra", painel);
            Esticar(sombra); sombra.anchoredPosition = new Vector2(0f, -9f);
            Moldura(sombra, "UI_Panel_R26_Deep", Hex("#0A0C22"), 2.31f);

            var bg = Novo("Bg", painel);
            Esticar(bg);
            Moldura(bg, "UI_Panel_R26_Deep", Color.white, 2.31f);

            Texto(painel, "Titulo", "PISTA", "TitanOne SDF", 26f, Hex("#FFB020"),
                  new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(300f, 32f),
                  TextAlignmentOptions.TopLeft);

            var voltas = Texto(painel, "Voltas", "3 VOLTAS", "Archivo ExtraBold SDF", 19f, Hex("#7C86C8"),
                  new Vector2(1f, 1f), new Vector2(-26f, -28f), new Vector2(220f, 26f),
                  TextAlignmentOptions.TopRight);
            voltas.characterSpacing = 10f;

            // miniatura com moldura escura, como os cards do PLACA
            var moldura = Novo("Moldura", painel);
            moldura.anchorMin = moldura.anchorMax = new Vector2(0.5f, 1f);
            moldura.pivot = new Vector2(0.5f, 1f);
            moldura.anchoredPosition = new Vector2(0f, -72f);
            moldura.sizeDelta = new Vector2(560f, 168f);
            Moldura(moldura, "UI_Card_R18_Ink", Color.white, 2.44f);

            var mini = Novo("Miniatura", moldura);
            mini.anchorMin = Vector2.zero; mini.anchorMax = Vector2.one;
            mini.offsetMin = new Vector2(8f, 8f); mini.offsetMax = new Vector2(-8f, -8f);
            var imgMini = mini.gameObject.AddComponent<Image>();
            imgMini.preserveAspect = true;
            imgMini.raycastTarget = false;

            var nome = Texto(painel, "Nome", "PISTA", "TitanOne SDF", 30f, Hex("#FFF7E8"),
                  new Vector2(0.5f, 1f), new Vector2(0f, -252f), new Vector2(520f, 38f),
                  TextAlignmentOptions.Center);

            Texto(painel, "Descricao", "", "Archivo Bold SDF", 18f, Hex("#C3CEDD"),
                  new Vector2(0.5f, 1f), new Vector2(0f, -292f), new Vector2(520f, 26f),
                  TextAlignmentOptions.Center);

            var prev = Seta(painel, "Btn_Anterior", "Icon_ArrowLeft", new Vector2(0f, 1f), new Vector2(26f, -140f));
            var next = Seta(painel, "Btn_Proximo", "Icon_ArrowRight", new Vector2(1f, 1f), new Vector2(-26f, -140f));

            var pontos = Novo("Indicadores", painel);
            pontos.anchorMin = pontos.anchorMax = new Vector2(0.5f, 0f);
            pontos.pivot = new Vector2(0.5f, 0f);
            pontos.anchoredPosition = new Vector2(0f, 18f);
            pontos.sizeDelta = new Vector2(300f, 10f);
            var hlg = pontos.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            for (int i = 0; i < 8; i++)   // sobra para novas pistas; o script esconde o que não usa
            {
                var p = Novo("Ponto_" + (i + 1).ToString("00"), pontos);
                p.sizeDelta = new Vector2(26f, 8f);
                var le = p.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = le.minWidth = 26f;
                le.preferredHeight = le.minHeight = 8f;
                Moldura(p, "UI_Badge_R14_Cream", Hex("#4B54A8"), 9f);
            }

            // ---- binder ----
            var ui = painel.gameObject.AddComponent<TrackSelectUI>();
            var so = new SerializedObject(ui);
            var lista = so.FindProperty("pistas");
            var defs = AssetDatabase.FindAssets("t:TrackDefinition", new[] { "Assets/_Projeto/Settings/Tracks" })
                                    .Select(g => AssetDatabase.LoadAssetAtPath<TrackDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                                    .Where(d => d != null).OrderBy(d => d.ordem).ThenBy(d => d.name).ToList();
            lista.arraySize = defs.Count;
            for (int i = 0; i < defs.Count; i++)
                lista.GetArrayElementAtIndex(i).objectReferenceValue = defs[i];

            so.FindProperty("miniatura").objectReferenceValue = imgMini;
            so.FindProperty("nome").objectReferenceValue = nome;
            so.FindProperty("descricao").objectReferenceValue = painel.Find("Descricao").GetComponent<TextMeshProUGUI>();
            so.FindProperty("voltas").objectReferenceValue = voltas;
            so.FindProperty("btnAnterior").objectReferenceValue = prev;
            so.FindProperty("btnProximo").objectReferenceValue = next;
            so.FindProperty("containerIndicadores").objectReferenceValue = pontos;
            so.ApplyModifiedPropertiesWithoutUndo();

            // deixa o cartão já preenchido no prefab, para quem abrir a cena ver a pista real
            // em vez do texto de exemplo — é o mesmo que o binder escreve em runtime
            var primeira = defs.FirstOrDefault();
            if (primeira != null)
            {
                nome.text = primeira.nome;
                painel.Find("Descricao").GetComponent<TextMeshProUGUI>().text = primeira.descricao ?? string.Empty;
                voltas.text = primeira.voltas > 0 ? $"{primeira.voltas} VOLTAS" : string.Empty;
                imgMini.sprite = primeira.miniatura;
                for (int i = 0; i < pontos.childCount; i++)
                {
                    var p = pontos.GetChild(i);
                    p.gameObject.SetActive(i < defs.Count);
                    var img = p.GetComponent<Image>();
                    if (img != null) img.color = i == 0 ? Hex("#FFB020") : Hex("#4B54A8");
                }
            }

            Debug.Log($"[lobby] cartão de pista com {defs.Count} pista(s)");
        }

        static Button Seta(Transform pai, string nome, string icone, Vector2 ancora, Vector2 pos)
        {
            var rt = Novo(nome, pai);
            rt.anchorMin = rt.anchorMax = ancora;
            rt.pivot = new Vector2(ancora.x, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(56f, 56f);
            Moldura(rt, "UI_Card_R18_Deep", Color.white, 3.1f);

            var ic = Novo("Icon", rt);
            ic.anchorMin = ic.anchorMax = new Vector2(0.5f, 0.5f);
            ic.sizeDelta = new Vector2(22f, 22f);
            var img = ic.gameObject.AddComponent<Image>();
            img.sprite = Sprite(icone);
            img.color = Hex("#FFF7E8");
            img.raycastTarget = false;

            var b = rt.gameObject.AddComponent<Button>();
            var fundo = rt.GetComponent<Image>();
            b.targetGraphic = fundo;

            // A moldura nasce sem raycast (é decoração em todo o resto da tela). Numa SETA ela é a
            // única superfície clicável — o ícone também não capta. Sem isto o Button existe, tem
            // listener, e mesmo assim o clique atravessa: era por isso que a troca de pista no
            // lobby não fazia nada.
            if (fundo != null)
                fundo.raycastTarget = true;

            return b;
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
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static void Moldura(RectTransform rt, string sprite, Color cor, float ppu)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Sprite(sprite);
            img.type = Image.Type.Sliced;
            img.color = cor;
            img.pixelsPerUnitMultiplier = ppu;
            img.raycastTarget = false;
        }

        static TextMeshProUGUI Texto(Transform pai, string nome, string valor, string fonte, float tam,
                                     Color cor, Vector2 ancora, Vector2 pos, Vector2 size,
                                     TextAlignmentOptions alinhamento)
        {
            var rt = Novo(nome, pai);
            rt.anchorMin = rt.anchorMax = ancora;
            rt.pivot = new Vector2(ancora.x, ancora.y);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = valor; tmp.fontSize = tam; tmp.color = cor;
            tmp.alignment = alinhamento; tmp.raycastTarget = false;
            var f = Fonte(fonte);
            if (f != null) tmp.font = f;
            return tmp;
        }

        static TMP_FontAsset Fonte(string n)
        {
            var g = AssetDatabase.FindAssets(n + " t:TMP_FontAsset").FirstOrDefault();
            return g == null ? null : AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
        }

        static Sprite Sprite(string n)
        {
            var g = AssetDatabase.FindAssets(n + " t:Sprite", new[] { "Assets/_Projeto/Art/UI" }).FirstOrDefault();
            return g == null ? null : AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g));
        }

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
#endif
