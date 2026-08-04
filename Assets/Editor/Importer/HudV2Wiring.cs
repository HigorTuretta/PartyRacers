using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Race;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Termina o `Screen_RaceHUD_PC` depois do import: constrói o MIOLO do cluster vital e liga os
    /// binders.
    ///
    /// Por que existe um segundo passo. O `Screen_RaceHUD_PC.json` descreve as barras em prosa —
    /// `"Track (5 segmentos de 20 HP, gap 3, sombra 5)"` — em vez de nó a nó. O importador genérico
    /// só consegue criar o marcador `Track`; os 5 blocos, os 3 estados do escudo e as listras do
    /// reparo não estão especificados como geometria e precisam ser derivados das REGRAS
    /// (tokens-v2.json + §5 do handoff). É isso que este arquivo faz.
    ///
    /// Continua valendo a regra de ouro: isto é EDITOR, roda uma vez e o resultado são GameObjects
    /// reais, editáveis à mão. Nenhum script de runtime cria, dimensiona ou pinta nada.
    /// </summary>
    public static class HudV2Wiring
    {
        private const string HudPrefab = LayoutImporter.ScreenRoot + "/Screen_RaceHUD_PC.prefab";

        // tokens-v2.json → cores
        private static readonly Color Hp = Hex("#3DDC97");
        private static readonly Color HpHurt = Hex("#E09410");
        private static readonly Color HpVazio = new Color(155f / 255f, 165f / 255f, 215f / 255f, 0.10f);
        private static readonly Color Shield = Hex("#35A7FF");
        private static readonly Color ShieldLight = Hex("#9BE0FF");
        private static readonly Color Repair = Hex("#FFB020");
        private static readonly Color Damage = Hex("#FF4D6D");
        private static readonly Color Cream = Hex("#FFF7E8");
        private static readonly Color Muted = Hex("#9AA2D8");
        private static readonly Color Ink = Hex("#0A0C22");

        private const int SegmentosDeVida = 5;
        private const int SegmentosDeEscudo = 3;
        private const float Gap = 3f;

        [MenuItem("Party Racers/UI v2/3 · Montar e ligar o HUD de corrida", priority = 20)]
        public static void Montar()
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefab);
            if (asset == null)
            {
                Debug.LogError($"[UI v2] {HudPrefab} não existe. Rode o import das telas primeiro.");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(HudPrefab);

            try
            {
                Transform cluster = Achar(raiz.transform, "VitalCluster");
                if (cluster == null)
                {
                    Debug.LogError("[UI v2] VitalCluster não encontrado no HUD.");
                    return;
                }

                var vida = MontarBarraDeVida(Achar(cluster, "HealthBar"));
                var escudo = MontarBarraDeEscudo(Achar(cluster, "ShieldBar"));
                var imunidade = MontarImunidade(Achar(cluster, "ImmunityTick"));
                var reparo = MontarReparo(Achar(cluster, "RepairBar"));

                LigarClusterVital(cluster, vida, escudo, imunidade, reparo);
                LigarArcoDePerigo(raiz.transform);
                LigarNumerosFlutuantes(raiz.transform);
                EsconderOQueSoApareceEmEvento(raiz.transform);

                PrefabUtility.SaveAsPrefabAsset(raiz, HudPrefab);
                Debug.Log("[UI v2] HUD montado e ligado: cluster vital, arco de perigo e números flutuantes.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(raiz);
            }
        }

        /// <summary>
        /// Monta uma cena descartável só com o canvas e a tela pedida e captura em 1920×1080
        /// reais. Serve para conferir o resultado sem entrar em playmode e sem depender do
        /// tamanho da Game View, que raramente é 16:9 exato.
        /// </summary>
        public static string PreVisualizar(string tela, string destino)
        {
            var cena = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var canvasGO = new GameObject("Canvas_UI", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            string caminho = $"{LayoutImporter.ScreenRoot}/{tela}.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (asset == null)
            {
                Debug.LogError($"[UI v2] {caminho} não existe.");
                return null;
            }

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvasGO.transform);
            var r = (RectTransform)instancia.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            // O próprio UICaptureTool desenha sobre o ink do tema, então não é preciso fundo aqui.
            _ = cena;
            return PartyRacers.UI.EditorTools.UICaptureTool.Capture(tela, destino);
        }

        [MenuItem("Party Racers/UI v2/4 · Capturar as telas em 1920×1080", priority = 21)]
        public static void CapturarTudo()
        {
            string dir = "Docs/UI-v2/capturas";
            System.IO.Directory.CreateDirectory(dir);

            string[] telas =
            {
                "Screen_RaceHUD_PC", "Screen_Lobby", "Screen_Matchmaking",
                "Screen_CustomMatch", "Screen_Garage", "Screen_RaceHUD_Mobile",
            };

            foreach (string tela in telas)
                PreVisualizar(tela, $"{dir}/{tela}.png");

            Debug.Log($"[UI v2] Capturas em {dir}");
        }

        // ================================================================== Barra de vida

        private class PecasDeVida
        {
            public GameObject Raiz;
            public GameObject[] Cheio = new GameObject[SegmentosDeVida];
            public GameObject[] Ferido = new GameObject[SegmentosDeVida];
            public GameObject[] Vazio = new GameObject[SegmentosDeVida];
            public TextMeshProUGUI Valor;
            public GameObject Alerta;
        }

        /// <summary>
        /// 5 blocos de 20 HP. Blocos porque são CONTÁVEIS de relance — uma barra contínua exige
        /// medir, e no meio de uma corrida ninguém mede.
        /// </summary>
        private static PecasDeVida MontarBarraDeVida(Transform barra)
        {
            var pecas = new PecasDeVida();
            if (barra == null)
                return pecas;

            pecas.Raiz = barra.gameObject;

            float largura = ((RectTransform)barra).sizeDelta.x;   // 486
            float larguraDoValor = 66f;
            float larguraDoTrilho = largura - larguraDoValor - 8f;

            Transform rotulo = Limpar(Achar(barra, "Label"));
            Texto(rotulo, "VIDA", "Archivo Black", 11f, Muted, TextAlignmentOptions.Left);
            Colocar(rotulo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 0f), new Vector2(120f, 14f));

            Transform valor = Limpar(Achar(barra, "Value"));
            pecas.Valor = Texto(valor, "100", "Titan One", 24f, Cream, TextAlignmentOptions.Right);
            Colocar(valor, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                    new Vector2(0f, 0f), new Vector2(larguraDoValor, 28f));

            Transform trilho = Limpar(Achar(barra, "Track"));
            Colocar(trilho, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(0f, 0f), new Vector2(larguraDoTrilho, 26f));

            float larguraDoSegmento = (larguraDoTrilho - Gap * (SegmentosDeVida - 1)) / SegmentosDeVida;

            for (int i = 0; i < SegmentosDeVida; i++)
            {
                Transform seg = Novo(trilho, $"Seg_{i + 1}");
                Colocar(seg, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f),
                        new Vector2((larguraDoSegmento + Gap) * i, 0f),
                        new Vector2(larguraDoSegmento, 0f));
                ((RectTransform)seg).anchorMax = new Vector2(0f, 1f);
                ((RectTransform)seg).sizeDelta = new Vector2(larguraDoSegmento, 0f);

                pecas.Vazio[i] = Bloco(seg, "Vazio", HpVazio, false).gameObject;
                pecas.Cheio[i] = Bloco(seg, "Cheio", Hp, true).gameObject;
                pecas.Ferido[i] = Bloco(seg, "Ferido", HpHurt, true).gameObject;
                pecas.Ferido[i].SetActive(false);
            }

            // Alerta de vida baixa: pulso vermelho por trás da barra. É o aviso de "o próximo
            // golpe quebra o carro" — a mesma informação que os blocos âmbar dão, mas periférica.
            Transform alerta = Novo(barra, "Alerta_VidaBaixa");
            Colocar(alerta, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            ((RectTransform)alerta).offsetMin = new Vector2(-6f, -6f);
            ((RectTransform)alerta).offsetMax = new Vector2(6f, 6f);
            Image imgAlerta = alerta.gameObject.AddComponent<Image>();
            imgAlerta.sprite = Sprite("Frames/UI_Card_R18_Ink");
            imgAlerta.type = Image.Type.Sliced;
            imgAlerta.pixelsPerUnitMultiplier = 1f;
            imgAlerta.color = new Color(Damage.r, Damage.g, Damage.b, 0.35f);
            imgAlerta.raycastTarget = false;
            alerta.SetAsFirstSibling();
            var pulso = alerta.gameObject.AddComponent<UIPulse>();
            SetPrivate(pulso, "periodo", 0.6f);
            SetPrivate(pulso, "alfaMin", 0.12f);
            SetPrivate(pulso, "alfaMax", 0.5f);
            pecas.Alerta = alerta.gameObject;
            alerta.gameObject.SetActive(false);

            return pecas;
        }

        // `Bars/Bar_Fill.png` é ÂMBAR (255,176,32) com listras diagonais. Tingir esse sprite de
        // azul é impossível: o canal azul dele vale 32/255, então o multiply devolve verde-oliva —
        // foi assim que a barra de escudo saiu verde na primeira montagem. Para os segmentos, a
        // cor tem que vir exata dos tokens, então o sprite precisa ser NEUTRO. O sprite branco
        // arredondado que acompanha a Unity serve, e não é sprite gerado por código.
        private static UnityEngine.Sprite neutro;

        private static UnityEngine.Sprite Neutro()
        {
            if (neutro == null)
                neutro = AssetDatabase.GetBuiltinExtraResource<UnityEngine.Sprite>("UI/Skin/UISprite.psd");

            return neutro;
        }

        /// <summary>Um preenchimento de bloco. Filled Horizontal — nunca redimensiona o Rect.</summary>
        private static Image Bloco(Transform pai, string nome, Color cor, bool preenchido)
        {
            Transform t = Novo(pai, nome);
            Colocar(t, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image img = t.gameObject.AddComponent<Image>();
            img.sprite = Neutro();
            img.type = preenchido ? Image.Type.Filled : Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = cor;
            img.raycastTarget = false;

            if (!preenchido)
                return img;

            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
        }

        /// <summary>
        /// Preenchimento com as LISTRAS diagonais âmbar — a gramática de "obra em andamento" do
        /// estado danificado. Aqui o `Bar_Fill` entra SEM tinta, porque a cor nativa dele já é
        /// exatamente o âmbar de reparo dos tokens.
        /// </summary>
        private static Image BlocoListrado(Transform pai, string nome)
        {
            Transform t = Novo(pai, nome);
            Colocar(t, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            Image img = t.gameObject.AddComponent<Image>();
            img.sprite = Sprite("Bars/Bar_Fill");
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        // ================================================================== Barra de escudo

        private class PecasDeEscudo
        {
            public GameObject Raiz, Pronto, Ativo, Recarga, Broken;
            public GameObject BrilhoPronto, BrilhoAtivo, PontaDaRecarga;
            public GameObject ChapinhaPronto, ChapinhaAtivo, ChapinhaRecarga;
            public Image PreenchimentoRecarga;
            public TextMeshProUGUI TextoDaChapinha;
            public UIShineSweep Varredura;
        }

        /// <summary>
        /// O escudo NÃO tem botão nem ícone: a barra é o indicador. Por isso cada estado é um
        /// filho completo já estilizado, e "em recarga" se distingue pela AUSÊNCIA do brilho e da
        /// varredura — não por uma cor diferente que o jogador teria que aprender.
        /// </summary>
        private static PecasDeEscudo MontarBarraDeEscudo(Transform barra)
        {
            var pecas = new PecasDeEscudo();
            if (barra == null)
                return pecas;

            pecas.Raiz = barra.gameObject;

            float largura = ((RectTransform)barra).sizeDelta.x;   // 486
            float larguraDoRotulo = 74f;
            float larguraDaChapinha = 104f;
            float larguraDoTrilho = largura - larguraDoRotulo - larguraDaChapinha - 16f;

            Transform rotulo = Limpar(Achar(barra, "Label"));
            Texto(rotulo, "ESCUDO", "Archivo Black", 11f, Muted, TextAlignmentOptions.Left);
            Colocar(rotulo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(larguraDoRotulo, 16f));

            Transform trilho = Limpar(Achar(barra, "Track"));
            Colocar(trilho, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(larguraDoRotulo + 8f, 0f), new Vector2(larguraDoTrilho, 18f));

            // --- Ready: 3 segmentos cheios + brilho pulsante (o sinal de disponível)
            pecas.Pronto = Estado(barra, "State_Ready").gameObject;
            Transform trilhoPronto = Copia(trilho, pecas.Pronto.transform, "Track_Ready");
            Segmentos(trilhoPronto, larguraDoTrilho, SegmentosDeEscudo, Shield, 1f);
            pecas.BrilhoPronto = Brilho(pecas.Pronto.transform, "Brilho", Shield, 0.55f, 1.8f);

            // --- Active: quase branco, brilho forte
            pecas.Ativo = Estado(barra, "State_Active").gameObject;
            Transform trilhoAtivo = Copia(trilho, pecas.Ativo.transform, "Track_Active");
            Segmentos(trilhoAtivo, larguraDoTrilho, SegmentosDeEscudo, ShieldLight, 1f);
            pecas.BrilhoAtivo = Brilho(pecas.Ativo.transform, "Brilho", ShieldLight, 0.9f, 0.5f);

            // --- Cooling: barra CONTÍNUA (sem segmentos), sem brilho e sem varredura
            pecas.Recarga = Estado(barra, "State_Cooling").gameObject;
            Transform trilhoRecarga = Copia(trilho, pecas.Recarga.transform, "Track_Cooling");
            Bloco(trilhoRecarga, "Fundo", new Color(Shield.r, Shield.g, Shield.b, 0.12f), false);
            pecas.PreenchimentoRecarga = Bloco(trilhoRecarga, "Fill",
                new Color(Shield.r, Shield.g, Shield.b, 0.5f), true);

            Transform ponta = Novo(trilhoRecarga, "EdgeTick");
            Colocar(ponta, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(2f, 0f));
            Image imgPonta = ponta.gameObject.AddComponent<Image>();
            imgPonta.color = new Color(155f / 255f, 230f / 255f, 255f / 255f, 0.8f);
            imgPonta.raycastTarget = false;
            var pulsoDaPonta = ponta.gameObject.AddComponent<UIPulse>();
            SetPrivate(pulsoDaPonta, "periodo", 0.9f);
            pecas.PontaDaRecarga = ponta.gameObject;

            // --- Broken: tudo apagado
            pecas.Broken = Estado(barra, "State_Broken").gameObject;
            Transform trilhoBroken = Copia(trilho, pecas.Broken.transform, "Track_Broken");
            Segmentos(trilhoBroken, larguraDoTrilho, SegmentosDeEscudo, HpVazio, 1f);

            // --- Varredura: a faixa de luz que cruza a barra. Vive fora dos estados porque o
            // binder liga/desliga e troca o período conforme Ready (2,4 s) ou Active (1 s).
            Transform varredura = Limpar(Achar(barra, "ShineSweep"));
            Colocar(varredura, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(larguraDoRotulo + 8f + larguraDoTrilho * 0.5f, 0f),
                    new Vector2(70f, 18f));
            Image imgVarredura = varredura.gameObject.AddComponent<Image>();
            imgVarredura.color = new Color(1f, 1f, 1f, 0.22f);
            imgVarredura.raycastTarget = false;
            pecas.Varredura = varredura.gameObject.AddComponent<UIShineSweep>();
            SetPrivate(pecas.Varredura, "periodo", 2.4f);
            SetPrivate(pecas.Varredura, "folga", -20f);

            // --- Chapinha de estado
            Transform chapinha = Limpar(Achar(barra, "KeyChip"));
            Colocar(chapinha, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                    Vector2.zero, new Vector2(larguraDaChapinha, 20f));

            pecas.ChapinhaPronto = Chapinha(chapinha, "Chip_Ready", "Frames/UI_Badge_R14_Cream").gameObject;
            pecas.ChapinhaAtivo = Chapinha(chapinha, "Chip_Active", "Frames/UI_Badge_R14_Amber").gameObject;
            pecas.ChapinhaRecarga = Chapinha(chapinha, "Chip_Cooling", "Frames/UI_Card_R18_Ink").gameObject;

            Transform texto = Novo(chapinha, "Label");
            Colocar(texto, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            pecas.TextoDaChapinha = Texto(texto, "Q PRONTO", "Archivo Black", 11f, Ink,
                                          TextAlignmentOptions.Center);

            return pecas;
        }

        private static Transform Chapinha(Transform pai, string nome, string sprite)
        {
            Transform t = Novo(pai, nome);
            Colocar(t, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image img = t.gameObject.AddComponent<Image>();
            img.sprite = Sprite(sprite);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.raycastTarget = false;
            return t;
        }

        private static void Segmentos(Transform trilho, float largura, int quantidade, Color cor, float alfa)
        {
            float larguraDoSegmento = (largura - Gap * (quantidade - 1)) / quantidade;

            for (int i = 0; i < quantidade; i++)
            {
                Transform seg = Novo(trilho, $"Seg_{i + 1}");
                var r = (RectTransform)seg;
                r.anchorMin = new Vector2(0f, 0f);
                r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(0f, 0.5f);
                r.anchoredPosition = new Vector2((larguraDoSegmento + Gap) * i, 0f);
                r.sizeDelta = new Vector2(larguraDoSegmento, 0f);

                Image img = seg.gameObject.AddComponent<Image>();
                img.sprite = Neutro();
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
                img.color = new Color(cor.r, cor.g, cor.b, cor.a * alfa);
                img.raycastTarget = false;
            }
        }

        private static GameObject Brilho(Transform pai, string nome, Color cor, float alfa, float periodo)
        {
            Transform t = Novo(pai, nome);
            Colocar(t, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var r = (RectTransform)t;
            r.offsetMin = new Vector2(-10f, -10f);
            r.offsetMax = new Vector2(10f, 10f);

            Image img = t.gameObject.AddComponent<Image>();
            img.sprite = Sprite("Frames/UI_Card_R18_Ink");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = new Color(cor.r, cor.g, cor.b, alfa);
            img.raycastTarget = false;
            t.SetAsFirstSibling();

            var pulso = t.gameObject.AddComponent<UIPulse>();
            SetPrivate(pulso, "periodo", periodo);
            SetPrivate(pulso, "alfaMin", alfa * 0.45f);
            SetPrivate(pulso, "alfaMax", alfa);
            return t.gameObject;
        }

        // ================================================================== Imunidade e reparo

        private static Image MontarImunidade(Transform raiz)
        {
            if (raiz == null)
                return null;

            Transform barra = Limpar(Achar(raiz, "Bar"));
            if (barra == null)
                return null;

            Colocar(barra, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(74f, 0f), new Vector2(300f, 6f));

            Image fundo = Bloco(barra, "Fundo", new Color(Repair.r, Repair.g, Repair.b, 0.15f), false);
            Image fill = BlocoListrado(barra, "Fill");

            Transform rotulo = Limpar(Achar(raiz, "Label IMUNE") ?? Achar(raiz, "Label"));
            if (rotulo != null)
            {
                Texto(rotulo, "IMUNE", "Space Mono", 10f, Repair, TextAlignmentOptions.Left);
                Colocar(rotulo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                        Vector2.zero, new Vector2(70f, 12f));
            }

            raiz.gameObject.SetActive(false);
            _ = fundo;
            return fill;
        }

        private class PecasDeReparo
        {
            public GameObject Raiz;
            public Image Preenchimento;
            public TextMeshProUGUI Contagem;
        }

        private static PecasDeReparo MontarReparo(Transform raiz)
        {
            var pecas = new PecasDeReparo();
            if (raiz == null)
                return pecas;

            pecas.Raiz = raiz.gameObject;

            Transform rotulo = Limpar(Achar(raiz, "Label DANIFICADO") ?? Achar(raiz, "Label"));
            if (rotulo != null)
            {
                Texto(rotulo, "DANIFICADO", "Titan One", 20f, Damage, TextAlignmentOptions.Left);
                Colocar(rotulo, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                        Vector2.zero, new Vector2(240f, 24f));
            }

            Transform contagem = Limpar(Achar(raiz, "Countdown"));
            if (contagem != null)
            {
                pecas.Contagem = Texto(contagem, "2.5s", "Titan One", 24f, Repair, TextAlignmentOptions.Right);
                Colocar(contagem, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                        Vector2.zero, new Vector2(90f, 26f));
            }

            // Listras diagonais drenando: gramática visual de "obra em andamento", que nenhum
            // outro elemento da HUD usa. É o que impede o estado danificado de parecer só mais
            // uma barra entre as outras.
            Transform trilho = Novo(raiz, "Track");
            Colocar(trilho, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f),
                    new Vector2(0f, 0f), new Vector2(0f, 16f));
            ((RectTransform)trilho).offsetMin = new Vector2(0f, 0f);
            ((RectTransform)trilho).offsetMax = new Vector2(0f, 16f);

            Bloco(trilho, "Fundo", new Color(Damage.r, Damage.g, Damage.b, 0.22f), false);
            pecas.Preenchimento = BlocoListrado(trilho, "Fill");

            raiz.gameObject.SetActive(false);
            return pecas;
        }

        // ================================================================== Ligação

        private static void LigarClusterVital(Transform cluster, PecasDeVida vida, PecasDeEscudo escudo,
                                              Image imunidade, PecasDeReparo reparo)
        {
            var binder = cluster.GetComponent<VitalClusterUI>()
                      ?? cluster.gameObject.AddComponent<VitalClusterUI>();

            var so = new SerializedObject(binder);

            so.FindProperty("raizVida").objectReferenceValue = vida.Raiz;
            so.FindProperty("valorDeVida").objectReferenceValue = vida.Valor;
            so.FindProperty("alertaDeVidaBaixa").objectReferenceValue = vida.Alerta;

            SerializedProperty segmentos = so.FindProperty("segmentosDeVida");
            segmentos.arraySize = SegmentosDeVida;
            for (int i = 0; i < SegmentosDeVida; i++)
            {
                SerializedProperty s = segmentos.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("cheio").objectReferenceValue = Comp<Image>(vida.Cheio[i]);
                s.FindPropertyRelative("ferido").objectReferenceValue = Comp<Image>(vida.Ferido[i]);
                s.FindPropertyRelative("vazio").objectReferenceValue = vida.Vazio[i];
            }

            so.FindProperty("raizEscudo").objectReferenceValue = escudo.Raiz;
            so.FindProperty("estadoPronto").objectReferenceValue = escudo.Pronto;
            so.FindProperty("estadoAtivo").objectReferenceValue = escudo.Ativo;
            so.FindProperty("estadoRecarga").objectReferenceValue = escudo.Recarga;
            so.FindProperty("brilhoPronto").objectReferenceValue = escudo.BrilhoPronto;
            so.FindProperty("brilhoAtivo").objectReferenceValue = escudo.BrilhoAtivo;
            so.FindProperty("varredura").objectReferenceValue = escudo.Varredura;
            so.FindProperty("preenchimentoRecarga").objectReferenceValue = escudo.PreenchimentoRecarga;
            so.FindProperty("pontaDaRecarga").objectReferenceValue = escudo.PontaDaRecarga;
            so.FindProperty("textoDaChapinha").objectReferenceValue = escudo.TextoDaChapinha;
            so.FindProperty("chapinhaPronto").objectReferenceValue = escudo.ChapinhaPronto;
            so.FindProperty("chapinhaAtivo").objectReferenceValue = escudo.ChapinhaAtivo;
            so.FindProperty("chapinhaRecarga").objectReferenceValue = escudo.ChapinhaRecarga;

            Transform tick = Achar(cluster, "ImmunityTick");
            so.FindProperty("raizImunidade").objectReferenceValue = tick != null ? tick.gameObject : null;
            so.FindProperty("barraDeImunidade").objectReferenceValue = imunidade;

            so.FindProperty("raizReparo").objectReferenceValue = reparo.Raiz;
            so.FindProperty("preenchimentoDoReparo").objectReferenceValue = reparo.Preenchimento;
            so.FindProperty("contagemDoReparo").objectReferenceValue = reparo.Contagem;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void LigarArcoDePerigo(Transform raiz)
        {
            Transform arco = Achar(raiz, "DangerArc");
            if (arco == null)
                return;

            var ui = arco.GetComponent<DangerArcUI>();
            if (ui == null)
                ui = arco.gameObject.AddComponent<DangerArcUI>();

            // O nó DangerArc é só o contêiner dos dois estados. Com Image própria ele desenharia
            // o arco vermelho PERMANENTEMENTE, e o aviso de ataque deixaria de significar ataque.
            foreach (Image sobra in arco.GetComponents<Image>())
                Object.DestroyImmediate(sobra);

            var so = new SerializedObject(ui);

            GameObject fraco = Achar(arco, "State_Approaching")?.gameObject;
            GameObject forte = Achar(arco, "State_Imminent")?.gameObject;

            so.FindProperty("arcoFraco").objectReferenceValue = fraco;
            so.FindProperty("arcoForte").objectReferenceValue = forte;
            so.FindProperty("pulsoDeTras").objectReferenceValue = Achar(raiz, "DangerPulse")?.gameObject;
            so.FindProperty("graficoFraco").objectReferenceValue = Comp<Image>(fraco);
            so.FindProperty("graficoForte").objectReferenceValue = Comp<Image>(forte);
            so.ApplyModifiedPropertiesWithoutUndo();

            // O driver decide QUANDO o arco acende. Sem ele o componente acima nunca é acionado —
            // foi assim que o aviso de ataque ficou inerte na versão anterior da HUD.
            if (arco.GetComponent<DangerArcDriver>() == null)
                arco.gameObject.AddComponent<DangerArcDriver>();

            Ligar(fraco, false);
            Ligar(forte, false);
            Ligar(Achar(raiz, "DangerPulse")?.gameObject, false);
            Ligar(Achar(raiz, "HealFlash")?.gameObject, false);
            Ligar(Achar(raiz, "ShieldFlash")?.gameObject, false);
        }

        /// <summary>
        /// Dano e cura NÃO são o mesmo objeto com a cor trocada: são dois filhos de estado já
        /// estilizados. É isso que impede cura, escudo e HP de parecerem a mesma coisa.
        /// </summary>
        private static void LigarNumerosFlutuantes(Transform raiz)
        {
            Transform alerta = Achar(raiz, "AlertLayer");
            Transform modelo = Achar(raiz, "FloatNumber");
            if (alerta == null || modelo == null)
                return;

            var binder = alerta.GetComponent<FloatingNumbersUI>()
                      ?? alerta.gameObject.AddComponent<FloatingNumbersUI>();

            var so = new SerializedObject(binder);
            SerializedProperty slots = so.FindProperty("slots");
            slots.arraySize = 3;

            // Guardado antes do laço: a partir do segundo slot a posição do modelo já teria sido
            // deslocada, e cada cópia nasceria mais alta que a anterior de forma acumulada.
            Vector2 posicaoBase = ((RectTransform)modelo).anchoredPosition;

            for (int i = 0; i < 3; i++)
            {
                Transform slot = i == 0 ? modelo : Duplicar(modelo, $"FloatNumber_{i + 1}");
                if (i == 0)
                    slot.gameObject.name = "FloatNumber_1";

                // Os três slots saem do mesmo ponto mas em alturas diferentes: dois números
                // exatamente sobrepostos viram um borrão ilegível no frame em que coincidem.
                var r = (RectTransform)slot;
                r.anchoredPosition = posicaoBase + new Vector2(0f, i * 46f);

                Limpar(slot);

                Transform dano = Novo(slot, "State_Damage");
                Colocar(dano, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                TextMeshProUGUI textoDano = Texto(dano, "−15", "Titan One", 62f, Damage,
                                                  TextAlignmentOptions.Center);

                Transform cura = Novo(slot, "State_Heal");
                Colocar(cura, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                TextMeshProUGUI textoCura = Texto(cura, "+40", "Titan One", 62f, Hp,
                                                  TextAlignmentOptions.Center);

                CanvasGroup grupo = slot.GetComponent<CanvasGroup>();
                if (grupo == null)
                    grupo = slot.gameObject.AddComponent<CanvasGroup>();
                grupo.blocksRaycasts = false;
                grupo.interactable = false;

                UIFloatRise movimento = slot.GetComponent<UIFloatRise>();
                if (movimento == null)
                    movimento = slot.gameObject.AddComponent<UIFloatRise>();

                SerializedProperty s = slots.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("raiz").objectReferenceValue = slot.gameObject;
                s.FindPropertyRelative("movimento").objectReferenceValue = movimento;
                s.FindPropertyRelative("estadoDano").objectReferenceValue = dano.gameObject;
                s.FindPropertyRelative("estadoCura").objectReferenceValue = cura.gameObject;
                s.FindPropertyRelative("textoDano").objectReferenceValue = textoDano;
                s.FindPropertyRelative("textoCura").objectReferenceValue = textoCura;

                slot.gameObject.SetActive(false);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Deixa a HUD no estado de "corrida tranquila": nada de aviso, nenhum toast e nenhuma
        /// sombra órfã.
        ///
        /// As sombras e contornos são objetos IRMÃOS (é o que dá o contorno duro do PLACA em vez
        /// do borrão do componente Shadow da Unity), e por isso NÃO acompanham o SetActive do nó
        /// que acompanham. Esconder a barra de reparo sem esconder a sombra dela deixa uma mancha
        /// escura flutuando no canto — foi exatamente o que apareceu na primeira captura.
        /// </summary>
        private static void EsconderOQueSoApareceEmEvento(Transform raiz)
        {
            foreach (string alvo in new[] { "RepairBar", "ImmunityTick" })
            {
                Ligar(Achar(raiz, alvo)?.gameObject, false);
                Ligar(Achar(raiz, alvo + "_Shadow")?.gameObject, false);
                Ligar(Achar(raiz, alvo + "_Stroke")?.gameObject, false);
            }

            // Os 3 toasts existem na cena mas começam vazios; visíveis, mostrariam a moldura de um
            // aviso que nunca aconteceu.
            Transform pilha = Achar(raiz, "ToastStack");
            if (pilha != null)
            {
                for (int i = 0; i < pilha.childCount; i++)
                {
                    Transform filho = pilha.GetChild(i);
                    if (filho.name.StartsWith("Toast_Item"))
                        Ligar(filho.gameObject, false);
                }
            }

            // A linha âmbar é a do JOGADOR, e só existe uma. Como o primeiro estado de um item
            // nasce ligado, as 6 fileiras vinham todas âmbar e a classificação virava um bloco
            // sólido onde nada se destacava. Quem escolhe a linha local é o StandingsUI.
            Transform classificacao = Achar(raiz, "Standings");
            if (classificacao == null)
                return;

            for (int i = 0; i < classificacao.childCount; i++)
            {
                Transform linha = classificacao.GetChild(i);
                Ligar(Achar(linha, "State_IsLocal")?.gameObject, false);
                Ligar(Achar(linha, "State_Other")?.gameObject, true);
            }
        }

        // ================================================================== Utilidades

        private static Transform Achar(Transform raiz, string nome)
        {
            if (raiz == null)
                return null;

            if (raiz.name == nome)
                return raiz;

            for (int i = 0; i < raiz.childCount; i++)
            {
                Transform achado = Achar(raiz.GetChild(i), nome);
                if (achado != null)
                    return achado;
            }

            return null;
        }

        private static Transform Estado(Transform pai, string nome)
        {
            Transform t = Achar(pai, nome) ?? Novo(pai, nome);
            Limpar(t);
            Colocar(t, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return t;
        }

        private static Transform Novo(Transform pai, string nome)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            return go.transform;
        }

        private static Transform Duplicar(Transform modelo, string nome)
        {
            GameObject copia = Object.Instantiate(modelo.gameObject, modelo.parent);
            copia.name = nome;
            return copia.transform;
        }

        private static Transform Copia(Transform modelo, Transform pai, string nome)
        {
            Transform t = Novo(pai, nome);
            var origem = (RectTransform)modelo;
            var destino = (RectTransform)t;
            destino.anchorMin = origem.anchorMin;
            destino.anchorMax = origem.anchorMax;
            destino.pivot = origem.pivot;
            destino.anchoredPosition = origem.anchoredPosition;
            destino.sizeDelta = origem.sizeDelta;
            return t;
        }

        /// <summary>Remove filhos e componentes visuais para reconstruir do zero, idempotente.</summary>
        private static Transform Limpar(Transform t)
        {
            if (t == null)
                return null;

            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);

            foreach (Graphic g in t.GetComponents<Graphic>())
                Object.DestroyImmediate(g);

            foreach (UIPulse p in t.GetComponents<UIPulse>())
                Object.DestroyImmediate(p);

            foreach (UIShineSweep s in t.GetComponents<UIShineSweep>())
                Object.DestroyImmediate(s);

            return t;
        }

        private static void Colocar(Transform t, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
                                    Vector2 pos, Vector2 size)
        {
            if (t == null)
                return;

            var r = (RectTransform)t;
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = pivot;

            bool estica = !Mathf.Approximately(anchorMin.x, anchorMax.x)
                       || !Mathf.Approximately(anchorMin.y, anchorMax.y);

            if (estica && size == Vector2.zero)
            {
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
                return;
            }

            r.anchoredPosition = pos;
            r.sizeDelta = size;
        }

        private static TextMeshProUGUI Texto(Transform t, string valor, string fonte, float corpo,
                                             Color cor, TextAlignmentOptions alinhamento)
        {
            if (t == null)
                return null;

            var tmp = t.GetComponent<TextMeshProUGUI>() ?? t.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = valor;
            tmp.fontSize = corpo;
            tmp.color = cor;
            tmp.alignment = alinhamento;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;

            TMP_FontAsset asset = LayoutResources.Fonte(fonte);
            if (asset != null)
                tmp.font = asset;

            return tmp;
        }

        private static UnityEngine.Sprite Sprite(string chave) => LayoutResources.Sprite(chave);

        private static T Comp<T>(GameObject go) where T : Component
            => go != null ? go.GetComponent<T>() : null;

        private static void Ligar(GameObject go, bool ativo)
        {
            if (go != null && go.activeSelf != ativo)
                go.SetActive(ativo);
        }

        private static void SetPrivate(Object alvo, string campo, float valor)
        {
            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null)
            {
                p.floatValue = valor;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static Color Hex(string hex)
            => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;
    }
}
