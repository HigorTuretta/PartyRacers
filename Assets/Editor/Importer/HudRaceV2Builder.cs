using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Race;
using static PartyRacers.UI.Importer.CssKit;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Constrói o HUD de corrida com FIDELIDADE ao `Party Racers v2.dc.html`.
    ///
    /// Todo número aqui foi MEDIDO no protótipo rodando, via `getBoundingClientRect` normalizado
    /// para o espaço de autoria 1920×1080. Não veio do `Screen_RaceHUD_PC.json`: o JSON discorda do
    /// protótipo em vários pontos — dizia 62 px de altura para a placa de volta (o real é 74),
    /// 46 px para a faixa da classificação (o real é 31) e 296 para a placa de tempo (o real é 257).
    /// Onde os dois discordam, vale o que o protótipo desenha.
    ///
    /// Escreve em `Prefabs/UI_v2/`; não encosta em nada do projeto.
    /// </summary>
    public static class HudRaceV2Builder
    {
        private const string Destino = "Assets/_Projeto/Prefabs/UI_v2/Screens/HUD_RaceV2.prefab";

        // ---- Paleta (valores literais do CSS) ----------------------------------------------
        private static readonly Color TintaTexto = Hex("#15161C");
        private static readonly Color HpCheioA = Hex("#6BF2BC");
        private static readonly Color HpCheioB = Hex("#2FBB7E");
        private static readonly Color HpFeridoA = Hex("#FFD066");
        private static readonly Color HpFeridoB = Hex("#E09410");
        private static readonly Color HpVazio = Rgba(155, 165, 215, 0.10f);
        private static readonly Color EscudoA = Hex("#DFF6FF");
        private static readonly Color EscudoApagadoCor = Rgba(155, 165, 215, 0.08f);
        private static readonly Color EscudoLabelOff = Hex("#4B5182");
        private static readonly Color TintaChip = Hex("#0A2A44");
        private static readonly Color ChipTexto = Hex("#C3CEDD");
        private static readonly Color NomeOutro = Hex("#E6E9F2");
        private static readonly Color GapOutro = Hex("#7C86C8");
        private static readonly Color CianoTimer = Hex("#5AC8F5");

        // ---- Medidas do cluster vital (medidas no protótipo) --------------------------------
        private const float ClusterX = 36f;
        private const float ClusterW = 486f;
        private const float RotuloW = 58f;
        private const float BarraX = 69f;    // 105 absoluto − 36 do cluster
        private const float BarraEscudoW = 305f;
        private const float BarraVidaW = 417f;
        private const float ChipEscudoX = 385f;   // 421 absoluto − 36
        private const float ChipEscudoW = 101f;
        private const float ChipEscudoH = 29f;
        private const float LinhaEscudoH = 29f;   // a chapinha é o elemento mais alto da linha
        private const float LinhaVidaH = 46f;

        [MenuItem("Party Racers/UI v2/HUD · Montar com fidelidade ao protótipo", priority = 30)]
        public static void Montar()
        {
            LayoutResources.Limpar();

            var raiz = new GameObject("HUD_RaceV2", typeof(RectTransform));
            Esticar((RectTransform)raiz.transform);

            try
            {
                RectTransform alerta = Esticar(No(raiz.transform, "AlertLayer"));
                RectTransform info = Esticar(No(raiz.transform, "InfoLayer"));

                var vital = MontarClusterVital(raiz.transform);
                var poder = MontarSlotDePoder(raiz.transform);
                var alertas = MontarCamadaDeAlerta(alerta);

                MontarPlacaDeVolta(info);
                MontarChipsDeTempo(info);
                MontarBlocoDePosicao(info);
                var linhas = MontarClassificacao(info);
                var toasts = MontarToasts(info);

                Ligar(raiz, vital, poder, alertas, linhas, toasts);

                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Destino));
                PrefabUtility.SaveAsPrefabAsset(raiz, Destino);
                AssetDatabase.SaveAssets();
                Relatar();
            }
            finally
            {
                Object.DestroyImmediate(raiz);
            }
        }

        // ================================================================== Placa de volta
        // Medido: Lap 721,32,212×74 | Time 942,32,257×74 — o grupo ocupa 721..1199, centro 960.
        private static void MontarPlacaDeVolta(Transform pai)
        {
            RectTransform grupo = Caixa(pai, "LapPlate", Ancora.TopCenter, 0f, 32f, 478f, 74f);

            RectTransform lap = Moldura(grupo, "Lap", "Frames/UI_Button_R22_Amber", Ancora.TopLeft,
                                        0f, 0f, 212f, 74f, sombra: 6f);
            Texto(lap, "Label", "VOLTA 2/3", "Titan One", 400, 30f, TintaTexto,
                  TextAlignmentOptions.Center, 0.02f);

            RectTransform tempo = Moldura(grupo, "Time", "Frames/UI_Badge_R14_Ink", Ancora.TopLeft,
                                          221f, 0f, 257f, 74f, sombra: 6f);
            Texto(tempo, "Label", "01:12.480", "Titan One", 400, 40f, Cream,
                  TextAlignmentOptions.Center, 0.01f);
        }

        // Medido: ÚLT 800,115,151×33 | MELH 959,115,161×33 — grupo 800..1120, centro 960.
        private static void MontarChipsDeTempo(Transform pai)
        {
            RectTransform grupo = Caixa(pai, "TimeChips", Ancora.TopCenter, 0f, 115f, 320f, 33f);

            RectTransform ultimo = Pintado(grupo, "Chip_Last", Rgba(10, 12, 34, 0.82f),
                                           Ancora.TopLeft, 0f, 0f, 151f, 33f, contorno: 3f, raio: 9f);
            Texto(ultimo, "Label", "ÚLT --:--.---", "Space Mono", 700, 15f, ChipTexto);

            RectTransform melhor = Pintado(grupo, "Chip_Best", Green, Ancora.TopLeft,
                                           159f, 0f, 161f, 33f, contorno: 3f, raio: 9f);
            Texto(melhor, "Label", "MELH --:--.---", "Space Mono", 700, 15f, Ink);
        }

        // ================================================================== Posição
        // Medido: placa 35,30,95×84 | "4" Titan 54 | "º" em 96,72 | DE 12 em 143,52
        private static void MontarBlocoDePosicao(Transform pai)
        {
            RectTransform bloco = Caixa(pai, "PositionBlock", Ancora.TopLeft, 35f, 30f, 300f, 84f);

            RectTransform placa = Moldura(bloco, "Plate", "Frames/UI_Badge_R14_Cream",
                                          Ancora.TopLeft, 0f, 0f, 95f, 84f, sombra: 6f);
            // O adesivo torto é assinatura do PLACA; a sombra é filha, então acompanha a rotação.
            placa.localRotation = Quaternion.Euler(0f, 0f, -2f);

            // Medido no protótipo: "4" em 59,48 (35×47) e "º" em 96,72 (9×22), ambos absolutos.
            // Relativo à placa (35,30) dá 24,18 e 61,42 — mais 6 da margem do sprite.
            TextMeshProUGUI valor = Rotulo(placa, "Value", Ancora.TopLeft, 24f, 14f, 48f, 58f,
                                           "4", "Titan One", 400, 54f, TintaTexto,
                                           TextAlignmentOptions.Center);
            Rotulo(placa, "Ord", Ancora.TopLeft, 66f, 42f, 18f, 28f,
                   "º", "Titan One", 400, 22f, TintaTexto, TextAlignmentOptions.Left);

            RectTransform de = Caixa(bloco, "Of", Ancora.TopLeft, 108f, 20f, 160f, 15f);
            Texto(de, "Label", "DE 12", "Space Mono", 700, 13f, Rgba(10, 12, 34, 0.62f),
                  TextAlignmentOptions.Left, 0.16f);

            RectTransform delta = Pintado(bloco, "Delta", Rgba(10, 12, 34, 0.78f), Ancora.TopLeft,
                                          108f, 42f, 138f, 25f, raio: 7f);
            Texto(delta, "Label", "+2 POSIÇÕES", "Archivo", 800, 13f, Green,
                  TextAlignmentOptions.Center, 0.06f);
        }

        // ================================================================== Classificação
        // Medido: x=1556 (36 da direita), w=328, gap 5.
        //   outra  h 31, rgba(10,12,34,.72), radius 11, SEM borda
        //   local  h 50, #FFB020, border 4, radius 12, sombra 0 4px 0
        // As alturas diferem, então a lista é um VerticalLayoutGroup e cada faixa se ajusta ao
        // filho ativo — com posição fixa, trocar de estado deixaria buraco ou sobreposição.
        private static StandingsV2UI.Linha[] MontarClassificacao(Transform pai)
        {
            const int quantas = 6;
            const float largura = 328f;

            RectTransform lista = Caixa(pai, "Standings", Ancora.TopRight, 36f, 32f, largura, 340f);
            var col = lista.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 5f;
            col.childAlignment = TextAnchor.UpperLeft;
            col.childControlWidth = false;
            col.childControlHeight = false;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            var fit = lista.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var saida = new StandingsV2UI.Linha[quantas];

            for (int i = 0; i < quantas; i++)
            {
                RectTransform linha = No(lista, $"Row_{i + 1}");
                linha.sizeDelta = new Vector2(largura, 31f);
                var linhaCol = linha.gameObject.AddComponent<VerticalLayoutGroup>();
                linhaCol.childControlWidth = false;
                linhaCol.childControlHeight = false;
                linhaCol.childForceExpandWidth = false;
                linhaCol.childForceExpandHeight = false;
                var linhaFit = linha.gameObject.AddComponent<ContentSizeFitter>();
                linhaFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                // --- outro corredor
                RectTransform outro = No(linha, "State_Other");
                outro.sizeDelta = new Vector2(largura, 31f);
                Elemento(outro, largura, 31f);
                PintarDentro(outro, Rgba(10, 12, 34, 0.72f), raio: 11f);

                TextMeshProUGUI txtPosO = Rotulo(outro, "Pos", Ancora.TopLeft, 23f, 0f, 20f, 31f,
                                                 "0", "Archivo", 800, 15f, Muted, TextAlignmentOptions.Left);
                TextMeshProUGUI txtNomeO = Rotulo(outro, "Name", Ancora.TopLeft, 54f, 0f, 210f, 31f,
                                                  "JOGADOR", "Archivo", 700, 14f, NomeOutro, TextAlignmentOptions.Left);
                TextMeshProUGUI txtGapO = Rotulo(outro, "Gap", Ancora.TopRight, 13f, 0f, 70f, 31f,
                                                 "--:--", "Space Mono", 400, 13f, GapOutro, TextAlignmentOptions.Right);

                // --- o jogador
                RectTransform local = No(linha, "State_IsLocal");
                local.sizeDelta = new Vector2(largura, 50f);
                Elemento(local, largura, 50f);
                PintarDentro(local, Amber, contorno: 4f, sombra: 4f, raio: 12f);

                TextMeshProUGUI txtPosL = Rotulo(local, "Pos", Ancora.TopLeft, 25f, 0f, 24f, 50f,
                                                 "0", "Titan One", 400, 22f, TintaTexto, TextAlignmentOptions.Left);
                TextMeshProUGUI txtNomeL = Rotulo(local, "Name", Ancora.TopLeft, 58f, 0f, 190f, 50f,
                                                  "VOCÊ", "Archivo", 900, 16f, TintaTexto, TextAlignmentOptions.Left, 0.04f);
                TextMeshProUGUI txtGapL = Rotulo(local, "Gap", Ancora.TopRight, 13f, 0f, 80f, 50f,
                                                 "--:--", "Space Mono", 700, 14f, Rgba(21, 22, 28, 0.7f),
                                                 TextAlignmentOptions.Right);

                local.gameObject.SetActive(false);

                saida[i] = new StandingsV2UI.Linha
                {
                    raiz = linha.gameObject,
                    estadoLocal = local.gameObject,
                    estadoOutro = outro.gameObject,
                    posicaoLocal = txtPosL, nomeLocal = txtNomeL, tempoLocal = txtGapL,
                    posicaoOutro = txtPosO, nomeOutro = txtNomeO, tempoOutro = txtGapO,
                };
            }

            return saida;
        }

        // ================================================================== Toasts
        // Medido: 36, bottom 172, 340×54, radius 11, border 3 | ponto 26×26 r7 em +17,+14
        private static ToastNotificationUI.Slot[] MontarToasts(Transform pai)
        {
            const int quantos = 3;
            const float largura = 340f, altura = 54f;

            RectTransform pilha = Caixa(pai, "ToastStack", Ancora.BottomLeft, 36f, 172f, largura, 190f);
            var slots = new ToastNotificationUI.Slot[quantos];

            for (int i = 0; i < quantos; i++)
            {
                RectTransform cartao = Pintado(pilha, $"Toast_{i + 1}", Rgba(10, 12, 34, 0.9f),
                                               Ancora.BottomLeft, 0f, i * (altura + 7f), largura, altura,
                                               contorno: 3f, raio: 11f);

                RectTransform ponto = Caixa(cartao, "Dot", Ancora.TopLeft, 17f, 14f, 26f, 26f);
                var img = ponto.gameObject.AddComponent<Image>();
                img.sprite = Neutro();
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = Raio(7f);   // border-radius: 7px
                img.color = Amber;
                img.raycastTarget = false;

                TextMeshProUGUI label = Rotulo(cartao, "Text", Ancora.TopLeft, 54f, 0f, 270f, altura,
                                               "CAROL pegou um foguete", "Archivo", 700, 14f, Cream,
                                               TextAlignmentOptions.Left);

                var grupo = cartao.gameObject.AddComponent<CanvasGroup>();
                grupo.blocksRaycasts = false;
                cartao.gameObject.SetActive(false);

                slots[i] = new ToastNotificationUI.Slot
                {
                    raiz = cartao.gameObject, grupo = grupo, texto = label, icone = img,
                };
            }

            return slots;
        }

        // ================================================================== Cluster vital

        private class PecasVitais
        {
            public GameObject Cluster, RaizVida, RaizEscudo, RaizImunidade, RaizReparo;
            public Image[] SegCheio = new Image[5];
            public Image[] SegFerido = new Image[5];
            public GameObject[] SegVazio = new GameObject[5];
            public TextMeshProUGUI ValorDeVida;
            public GameObject EscudoPronto, EscudoAtivo, EscudoRecarga, EscudoApagado;
            public RectTransform PontaDaRecarga;
            public Image PreenchimentoRecarga;
            public TextMeshProUGUI TextoChipAtivo, TextoChipRecarga;
            public Image BarraImunidade;
            public Image PreenchimentoReparo;
            public TextMeshProUGUI ContagemDoReparo;
        }

        // Medido: escudo 964..988 (linha 962..991 por causa da chapinha de 29), vida 1000..1046.
        // Base em 1046 → bottom 34. Gap entre linhas = 9.
        private static PecasVitais MontarClusterVital(Transform pai)
        {
            var p = new PecasVitais();

            RectTransform cluster = Caixa(pai, "VitalCluster", Ancora.BottomLeft,
                                          ClusterX, 34f, ClusterW, 130f);
            p.Cluster = cluster.gameObject;

            var col = cluster.gameObject.AddComponent<VerticalLayoutGroup>();
            col.spacing = 9f;
            col.childAlignment = TextAnchor.LowerLeft;
            col.childControlWidth = false;
            col.childControlHeight = false;
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            var fit = cluster.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            MontarLinhaDeEscudo(cluster, p);
            MontarLinhaDeVida(cluster, p);
            MontarLinhaDeReparo(cluster, p);
            MontarLinhaDeImunidade(cluster, p);

            return p;
        }

        private static RectTransform LinhaDoCluster(Transform pai, string nome, float altura)
        {
            RectTransform r = No(pai, nome);
            r.sizeDelta = new Vector2(ClusterW, altura);
            Elemento(r, ClusterW, altura);
            return r;
        }

        private static void MontarLinhaDeEscudo(Transform pai, PecasVitais p)
        {
            RectTransform linha = LinhaDoCluster(pai, "ShieldBar", LinhaEscudoH);
            p.RaizEscudo = linha.gameObject;

            p.EscudoPronto = EstadoDoEscudo(linha, "State_Ready", SkyLight, out RectTransform slotPronto,
                                            out RectTransform barraPronto);
            RectTransform brilho = Glow(slotPronto, "Glow", Sky, new Vector2(BarraEscudoW, 24f), 16f, 0.45f);
            var pulso = brilho.gameObject.AddComponent<UIGlowPulse>();
            Privado(pulso, "periodo", 1.8f);
            Privado(pulso, "raioMin", 16f);
            Privado(pulso, "raioMax", 34f);
            Privado(pulso, "alfaMin", 0.45f);
            Privado(pulso, "alfaMax", 0.85f);
            Privado(pulso, "tamanhoBase", new Vector2(BarraEscudoW, 24f));
            Segmentos(barraPronto, 3, EscudoA, Sky);
            Varredura(barraPronto, 70f, 2.4f, 0.72f);
            ChipDoEscudo(p.EscudoPronto.transform, EscudoA, "PRONTO", TintaChip, "Q", out _);

            p.EscudoAtivo = EstadoDoEscudo(linha, "State_Active", Cream, out RectTransform slotAtivo,
                                           out RectTransform barraAtivo, corDoContorno: SkyLight);
            Glow(slotAtivo, "Glow", CianoTimer, new Vector2(BarraEscudoW, 24f), 40f, 0.95f);
            Segmentos(barraAtivo, 3, Color.white, SkyLight);
            Varredura(barraAtivo, 90f, 1f, 0.95f, suave: false);
            ChipDoEscudo(p.EscudoAtivo.transform, Sky, "ATIVO 3.0s", Ink, null, out p.TextoChipAtivo);

            p.EscudoRecarga = EstadoDoEscudo(linha, "State_Cooling", EscudoLabelOff, out _,
                                             out RectTransform barraFria);
            RectTransform interno = Esticar(No(barraFria, "Fill_Cooldown"), 6f);
            p.PreenchimentoRecarga = interno.gameObject.AddComponent<Image>();
            p.PreenchimentoRecarga.sprite = null;   // Filled ignora 9-slice — ver Preenchimento()
            p.PreenchimentoRecarga.type = Image.Type.Filled;
            p.PreenchimentoRecarga.fillMethod = Image.FillMethod.Horizontal;
            p.PreenchimentoRecarga.fillOrigin = (int)Image.OriginHorizontal.Left;
            p.PreenchimentoRecarga.pixelsPerUnitMultiplier = Raio(6f);
            p.PreenchimentoRecarga.color = Color.white;
            p.PreenchimentoRecarga.raycastTarget = false;
            interno.gameObject.AddComponent<UIGradient>()
                   .Definir(Rgba(53, 167, 255, 0.5f), Rgba(32, 121, 196, 0.42f));

            p.PontaDaRecarga = No(interno, "EdgeTick");
            p.PontaDaRecarga.anchorMin = new Vector2(0f, 0f);
            p.PontaDaRecarga.anchorMax = new Vector2(0f, 1f);
            p.PontaDaRecarga.pivot = new Vector2(0.5f, 0.5f);
            p.PontaDaRecarga.sizeDelta = new Vector2(2f, 0f);
            var imgPonta = p.PontaDaRecarga.gameObject.AddComponent<Image>();
            imgPonta.color = Rgba(155, 230, 255, 0.8f);
            imgPonta.raycastTarget = false;
            var tick = p.PontaDaRecarga.gameObject.AddComponent<UIPulse>();
            Privado(tick, "periodo", 0.9f);
            Privado(tick, "alfaMin", 0.55f);
            Privado(tick, "alfaMax", 1f);

            ChipDoEscudo(p.EscudoRecarga.transform, Rgba(10, 12, 34, 0.86f), "0.0s", CianoTimer,
                         null, out p.TextoChipRecarga, mono: true);

            p.EscudoApagado = EstadoDoEscudo(linha, "State_Broken", EscudoLabelOff, out _,
                                             out RectTransform barraOff);
            Segmentos(barraOff, 3, EscudoApagadoCor, EscudoApagadoCor);

            p.EscudoAtivo.SetActive(false);
            p.EscudoRecarga.SetActive(false);
            p.EscudoApagado.SetActive(false);
        }

        /// <summary>Linha completa do escudo: rótulo + barra + chapinha, na cor daquele estado.</summary>
        private static GameObject EstadoDoEscudo(Transform pai, string nome, Color corDoRotulo,
                                                 out RectTransform slot, out RectTransform barra,
                                                 Color? corDoContorno = null)
        {
            RectTransform raiz = Esticar(No(pai, nome));

            Rotulo(raiz, "Label", Ancora.TopLeft, 0f, 0f, RotuloW, LinhaEscudoH,
                   "ESCUDO", "Archivo", 900, 11f, corDoRotulo, TextAlignmentOptions.Left, 0.12f);

            // A barra tem 24 numa linha de 29: centralizada, sobra 2,5 em cima e embaixo.
            slot = Caixa(raiz, "BarSlot", Ancora.TopLeft, BarraX, 2.5f, BarraEscudoW, 24f);
            barra = Pintado(slot, "Bar", Ink, Ancora.TopLeft, 0f, 0f, BarraEscudoW, 24f,
                            contorno: 3f, corDoContorno: corDoContorno ?? Ink, raio: 11f);
            return raiz.gameObject;
        }

        // Medido: chapinha 101×29, radius 8, border 2 — a TECLA vem ANTES do rótulo.
        private static void ChipDoEscudo(Transform pai, Color fundo, string rotulo, Color corDoTexto,
                                         string tecla, out TextMeshProUGUI texto, bool mono = false)
        {
            RectTransform chip = Pintado(pai, "Chip", fundo, Ancora.TopLeft, ChipEscudoX, 0f,
                                         ChipEscudoW, ChipEscudoH, contorno: 2f, raio: 8f);

            if (string.IsNullOrEmpty(tecla))
            {
                texto = Rotulo(chip, "Label", Ancora.TopLeft, 0f, 0f, ChipEscudoW, ChipEscudoH,
                               rotulo, mono ? "Space Mono" : "Archivo", mono ? 700 : 900,
                               mono ? 12f : 11f, corDoTexto, TextAlignmentOptions.Center,
                               mono ? 0f : 0.1f);
                return;
            }

            Rotulo(chip, "Key", Ancora.TopLeft, 13f, 0f, 14f, ChipEscudoH,
                   tecla, "Titan One", 400, 15f, corDoTexto, TextAlignmentOptions.Left);
            texto = Rotulo(chip, "Label", Ancora.TopLeft, 33f, 0f, 60f, ChipEscudoH,
                           rotulo, "Archivo", 900, 11f, corDoTexto, TextAlignmentOptions.Left, 0.1f);
        }

        // Medido: barra 417×46, border 4, radius 13, sombra 0 5px 0 | 5 blocos de 78 com gap 3
        private static void MontarLinhaDeVida(Transform pai, PecasVitais p)
        {
            RectTransform linha = LinhaDoCluster(pai, "HealthBar", LinhaVidaH);
            p.RaizVida = linha.gameObject;

            Rotulo(linha, "Label", Ancora.TopLeft, 0f, 0f, RotuloW, LinhaVidaH,
                   "VIDA", "Archivo", 900, 11f, Green, TextAlignmentOptions.Left, 0.12f);

            RectTransform barra = Pintado(linha, "Track", Ink, Ancora.TopLeft, BarraX, 0f,
                                          BarraVidaW, LinhaVidaH, contorno: 4f, sombra: 5f, raio: 13f);

            RectTransform interno = Esticar(No(barra, "Segments"), 7f);
            float w = (BarraVidaW - 14f - 3f * 4f) / 5f;

            for (int i = 0; i < 5; i++)
            {
                RectTransform seg = No(interno, $"Seg_{i + 1}");
                seg.anchorMin = new Vector2(0f, 0f);
                seg.anchorMax = new Vector2(0f, 1f);
                seg.pivot = new Vector2(0f, 0.5f);
                seg.anchoredPosition = new Vector2((w + 3f) * i, 0f);
                seg.sizeDelta = new Vector2(w, 0f);

                p.SegVazio[i] = Bloco(seg, "Vazio", HpVazio, HpVazio).gameObject;

                // O preenchimento drena por `fillAmount`, e `Image.Type.Filled` NÃO respeita o
                // 9-slice — ele estica o sprite inteiro e os cantos incham até virar pílula. A
                // saída é recortar: uma máscara com o formato arredondado certo e, dentro dela,
                // um preenchimento retangular que pode encolher à vontade.
                RectTransform recorte = Esticar(No(seg, "Recorte"));
                var molde = recorte.gameObject.AddComponent<Image>();
                molde.sprite = Neutro();
                molde.type = Image.Type.Sliced;
                molde.pixelsPerUnitMultiplier = Raio(6f);
                molde.raycastTarget = false;
                recorte.gameObject.AddComponent<Mask>().showMaskGraphic = false;

                p.SegCheio[i] = Preenchimento(recorte, "Cheio", HpCheioA, HpCheioB);
                p.SegFerido[i] = Preenchimento(recorte, "Ferido", HpFeridoA, HpFeridoB);
                p.SegFerido[i].gameObject.SetActive(false);
            }

            p.ValorDeVida = Rotulo(barra, "Value", Ancora.TopRight, 12f, 0f, 70f, LinhaVidaH,
                                   "100", "Titan One", 400, 24f, Cream, TextAlignmentOptions.Right,
                                   0f, sombraY: 2f);
        }

        private static void MontarLinhaDeReparo(Transform pai, PecasVitais p)
        {
            RectTransform linha = LinhaDoCluster(pai, "RepairBar", LinhaVidaH);
            p.RaizReparo = linha.gameObject;

            Rotulo(linha, "Label", Ancora.TopLeft, 0f, 0f, RotuloW, LinhaVidaH,
                   "REPARO", "Archivo", 900, 11f, Red, TextAlignmentOptions.Left, 0.12f);

            RectTransform barra = Pintado(linha, "Track", Hex("#3A0E1C"), Ancora.TopLeft,
                                          BarraX, 0f, BarraVidaW, LinhaVidaH,
                                          contorno: 4f, corDoContorno: Red, raio: 13f);
            Glow(barra, "Glow", Red, new Vector2(BarraVidaW, LinhaVidaH), 28f, 0.5f);

            RectTransform interno = Esticar(No(barra, "Fill"), 4f);
            p.PreenchimentoReparo = interno.gameObject.AddComponent<Image>();
            // Bar_Fill JÁ é a listra diagonal âmbar autoral: a cor nativa dela é mais fiel do que
            // recriar o repeating-linear-gradient com tinta.
            p.PreenchimentoReparo.sprite = Sprite("Bars/Bar_Fill");
            p.PreenchimentoReparo.type = Image.Type.Filled;
            p.PreenchimentoReparo.fillMethod = Image.FillMethod.Horizontal;
            p.PreenchimentoReparo.pixelsPerUnitMultiplier = 1f;
            p.PreenchimentoReparo.raycastTarget = false;

            Rotulo(barra, "Title", Ancora.TopLeft, 14f, 0f, 200f, LinhaVidaH,
                   "DANIFICADO", "Titan One", 400, 20f, Cream, TextAlignmentOptions.Left, 0f, sombraY: 2f);
            p.ContagemDoReparo = Rotulo(barra, "Countdown", Ancora.TopRight, 12f, 0f, 80f, LinhaVidaH,
                                        "2.5s", "Titan One", 400, 24f, Cream,
                                        TextAlignmentOptions.Right, 0f, sombraY: 2f);

            p.RaizReparo.SetActive(false);
        }

        private static void MontarLinhaDeImunidade(Transform pai, PecasVitais p)
        {
            RectTransform linha = LinhaDoCluster(pai, "ImmunityTick", 12f);
            p.RaizImunidade = linha.gameObject;

            RectTransform trilho = Pintado(linha, "Track", Rgba(10, 12, 34, 0.6f), Ancora.TopLeft,
                                           BarraX, 3f, 360f, 6f, raio: 3f);

            RectTransform fill = Esticar(No(trilho, "Fill"));
            p.BarraImunidade = fill.gameObject.AddComponent<Image>();
            p.BarraImunidade.sprite = null;
            p.BarraImunidade.type = Image.Type.Filled;
            p.BarraImunidade.fillMethod = Image.FillMethod.Horizontal;
            p.BarraImunidade.pixelsPerUnitMultiplier = Raio(3f);
            p.BarraImunidade.color = Amber;
            p.BarraImunidade.raycastTarget = false;

            Rotulo(linha, "Label", Ancora.TopRight, 0f, 0f, 46f, 12f,
                   "IMUNE", "Space Mono", 700, 10f, Amber, TextAlignmentOptions.Right, 0.08f);

            p.RaizImunidade.SetActive(false);
        }

        // ---- peças reutilizadas --------------------------------------------------------------

        private static void Segmentos(RectTransform barra, int quantos, Color topo, Color baixo)
        {
            RectTransform interno = Esticar(No(barra, "Segments"), 6f);
            float largura = barra.sizeDelta.x - 12f;
            float w = (largura - 3f * (quantos - 1)) / quantos;

            for (int i = 0; i < quantos; i++)
            {
                RectTransform seg = No(interno, $"Seg_{i + 1}");
                seg.anchorMin = new Vector2(0f, 0f);
                seg.anchorMax = new Vector2(0f, 1f);
                seg.pivot = new Vector2(0f, 0.5f);
                seg.anchoredPosition = new Vector2((w + 3f) * i, 0f);
                seg.sizeDelta = new Vector2(w, 0f);
                Bloco(seg, "Fill", topo, baixo);
            }
        }

        /// <summary>Bloco arredondado com gradiente vertical — o `linear-gradient(180deg,a,b)`.</summary>
        private static Image Bloco(Transform pai, string nome, Color topo, Color baixo)
        {
            RectTransform r = Esticar(No(pai, nome));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Neutro();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Raio(6f);   // border-radius: 6px
            img.color = Color.white;
            img.raycastTarget = false;

            r.gameObject.AddComponent<UIGradient>().Definir(topo, baixo);
            return img;
        }

        /// <summary>
        /// Preenchimento que drena por `fillAmount`, para viver dentro de um recorte. Retangular
        /// de propósito: o formato arredondado vem da máscara, não daqui — é isso que impede o
        /// bloco de inchar nas pontas quando está meio cheio.
        /// </summary>
        private static Image Preenchimento(Transform pai, string nome, Color topo, Color baixo)
        {
            RectTransform r = Esticar(No(pai, nome));
            var img = r.gameObject.AddComponent<Image>();

            // SEM sprite de propósito: uma Image sem sprite desenha um retângulo puro. Usar o
            // retângulo arredondado aqui faria o `Filled` esticá-lo (Filled ignora 9-slice) e o
            // bloco viraria uma ELIPSE dentro da máscara. Quem arredonda é o recorte, não isto.
            img.sprite = null;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            img.color = Color.white;
            img.raycastTarget = false;

            r.gameObject.AddComponent<UIGradient>().Definir(topo, baixo);
            return img;
        }

        // ================================================================== Slot de poder

        private class PecasDePoder
        {
            public GameObject Cheio, Vazio, ChipCheio;
            public Image Icone;
            public TextMeshProUGUI NomeDoPoder;
        }

        // Medido: box 1760,883,124×124 r24 border 4 sombra 6 + glow 32 rgba(255,176,32,.34)
        //         ícone 1790,903,64×84 | chapinha 1761,1015,123×31 r8 border 2, "E" ANTES do nome
        private static PecasDePoder MontarSlotDePoder(Transform pai)
        {
            var p = new PecasDePoder();
            RectTransform coluna = Caixa(pai, "PowerSlot", Ancora.BottomRight, 36f, 34f, 124f, 163f);

            p.Cheio = Esticar(No(coluna, "State_Filled")).gameObject;

            RectTransform caixa = Moldura(p.Cheio.transform, "Box", "Frames/UI_Button_R28_Amber",
                                          Ancora.BottomLeft, 0f, 39f, 124f, 124f, sombra: 6f);
            Glow(caixa, "Glow", Amber, new Vector2(124f, 124f), 32f, 0.34f);
            Varredura(caixa, 60f, 3f, 0.5f);

            RectTransform icone = Caixa(caixa, "Icon", Ancora.TopLeft, 30f, 20f, 64f, 84f);
            p.Icone = icone.gameObject.AddComponent<Image>();
            p.Icone.sprite = Sprite("Powers/Power_Rocket_Color");
            p.Icone.preserveAspect = true;
            p.Icone.raycastTarget = false;

            RectTransform chip = Pintado(p.Cheio.transform, "KeyChip", Cream, Ancora.BottomLeft,
                                         0f, 0f, 123f, 31f, contorno: 2f, raio: 8f);
            Rotulo(chip, "Key", Ancora.TopLeft, 16f, 0f, 14f, 31f, "E", "Titan One", 400, 17f,
                   TintaTexto, TextAlignmentOptions.Left);
            p.NomeDoPoder = Rotulo(chip, "Text", Ancora.TopLeft, 36f, 0f, 78f, 31f, "FOGUETE",
                                   "Archivo", 900, 12f, TintaTexto, TextAlignmentOptions.Left, 0.1f);
            p.ChipCheio = chip.gameObject;

            p.Vazio = Esticar(No(coluna, "State_Empty")).gameObject;
            RectTransform vazio = Moldura(p.Vazio.transform, "Box", "Frames/UI_Dashed_R28",
                                          Ancora.BottomLeft, 0f, 39f, 124f, 124f);
            Texto(vazio, "Glyph", "?", "Titan One", 400, 34f, Rgba(255, 247, 232, 0.44f));

            RectTransform chipVazio = Pintado(p.Vazio.transform, "KeyChip", Rgba(10, 12, 34, 0.66f),
                                              Ancora.BottomLeft, 0f, 0f, 123f, 31f, raio: 8f);
            Texto(chipVazio, "Label", "SEM ITEM", "Archivo", 800, 12f, Rgba(255, 247, 232, 0.7f),
                  TextAlignmentOptions.Center, 0.1f);

            p.Cheio.SetActive(false);
            return p;
        }

        // ================================================================== Camada de alerta

        private class PecasDeAlerta
        {
            public GameObject Fraco, Forte, PulsoDeTras, FlashDeCura, FlashDeEscudo;
            public Image GraficoFraco, GraficoForte;
            public FloatingNumbersUI.Slot[] Numeros = new FloatingNumbersUI.Slot[3];
        }

        private static PecasDeAlerta MontarCamadaDeAlerta(Transform pai)
        {
            var a = new PecasDeAlerta();

            a.GraficoFraco = Vinheta(pai, "DangerArc_Approaching", "Race/Overlay_DangerArc", Color.white, 0.8f);
            a.Fraco = a.GraficoFraco.gameObject;
            a.GraficoForte = Vinheta(pai, "DangerArc_Imminent", "Race/Overlay_DangerArc_Strong", Color.white, 0.25f);
            a.Forte = a.GraficoForte.gameObject;
            a.PulsoDeTras = Vinheta(pai, "DangerPulse", "Race/Overlay_DangerPulse", Color.white, 0f).gameObject;

            // Cura e escudo tingem a vinheta NEUTRA: o overlay autoral é vermelho e Image.color
            // multiplica, então dele nunca sairia verde nem azul.
            a.FlashDeCura = Vinheta(pai, "HealFlash", "Race/Overlay_Vignette",
                                    new Color(Green.r, Green.g, Green.b, 0.5f), 0f).gameObject;
            a.FlashDeEscudo = Vinheta(pai, "ShieldFlash", "Race/Overlay_Vignette",
                                      new Color(Sky.r, Sky.g, Sky.b, 0.42f), 0f).gameObject;

            for (int i = 0; i < 3; i++)
            {
                // top:46% do CSS = um pouco acima do centro. Cada slot sobe 46 px a mais que o
                // anterior para dois números seguidos não virarem um borrão sobreposto.
                RectTransform slot = Caixa(pai, $"FloatNumber_{i + 1}", Ancora.Center,
                                           0f, 76f + i * 46f, 420f, 74f);
                var grupo = slot.gameObject.AddComponent<CanvasGroup>();
                grupo.blocksRaycasts = false;

                RectTransform dano = Esticar(No(slot, "State_Damage"));
                TextMeshProUGUI txtDano = Texto(dano, "Label", "−15", "Titan One", 400, 62f, Red,
                                                TextAlignmentOptions.Center, 0f, sombraY: 5f);

                RectTransform cura = Esticar(No(slot, "State_Heal"));
                TextMeshProUGUI txtCura = Texto(cura, "Label", "+40", "Titan One", 400, 62f, Green,
                                                TextAlignmentOptions.Center, 0f, sombraY: 5f);
                cura.gameObject.SetActive(false);

                var mov = slot.gameObject.AddComponent<UIFloatRise>();
                slot.gameObject.SetActive(false);

                a.Numeros[i] = new FloatingNumbersUI.Slot
                {
                    raiz = slot.gameObject, movimento = mov,
                    estadoDano = dano.gameObject, estadoCura = cura.gameObject,
                    textoDano = txtDano, textoCura = txtCura,
                };
            }

            MontarPromptDeBifurcacao(pai);
            return a;
        }

        // O handoff §5 diz "sem prompt de escolha item/cura". O protótipo TEM, e `_widgets.json`
        // traz o `Card_Choice`. Como a referência visual é o protótipo, ele é construído — mas
        // nasce DESLIGADO, para a decisão continuar sendo de quem desenha a pista.
        private static void MontarPromptDeBifurcacao(Transform pai)
        {
            RectTransform grupo = Caixa(pai, "ChoicePrompt", Ancora.TopCenter, 0f, 210f, 560f, 190f);

            CardDeEscolha(grupo, "Card_Item", 0f, Rgba(255, 176, 32, 0.94f), "ITEM", TintaTexto,
                          "← ESQUERDA", Rgba(21, 22, 28, 0.66f), 0f);
            Texto(Caixa(grupo, "Or", Ancora.TopLeft, 216f, 70f, 128f, 40f), "Label", "OU",
                  "Titan One", 400, 26f, Rgba(255, 247, 232, 0.8f), TextAlignmentOptions.Center, 0f, 3f);
            CardDeEscolha(grupo, "Card_Heal", 344f, Rgba(61, 220, 151, 0.94f), "CURA", Ink,
                          "DIREITA →", Rgba(10, 12, 34, 0.66f), 0.4f);

            grupo.gameObject.SetActive(false);
        }

        private static void CardDeEscolha(Transform pai, string nome, float x, Color fundo,
                                          string titulo, Color corDoTitulo, string dica,
                                          Color corDaDica, float atraso)
        {
            RectTransform card = Pintado(pai, nome, fundo, Ancora.TopLeft, x, 0f, 216f, 176f,
                                         contorno: 4f, sombra: 7f, raio: 18f);

            RectTransform icone = Caixa(card, "Icon", Ancora.TopCenter, 0f, 16f, 46f, 46f);
            var img = icone.gameObject.AddComponent<Image>();
            img.sprite = Neutro();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = Raio(11f);  // border-radius: 11px
            img.color = Cream;
            img.raycastTarget = false;

            Texto(Caixa(card, "Title", Ancora.TopCenter, 0f, 76f, 200f, 28f), "Label", titulo,
                  "Titan One", 400, 21f, corDoTitulo);
            Texto(Caixa(card, "Hint", Ancora.TopCenter, 0f, 118f, 200f, 18f), "Label", dica,
                  "Space Mono", 700, 11f, corDaDica, TextAlignmentOptions.Center, 0.08f);

            // 0,4 s de defasagem entre os dois: em fase, subiriam juntos e o par pareceria um
            // bloco único em vez de dois pickups.
            var bob = card.gameObject.AddComponent<UIBob>();
            Privado(bob, "periodo", 1.6f);
            Privado(bob, "amplitude", 8f);
            Privado(bob, "atraso", atraso);
        }

        private static Image Vinheta(Transform pai, string nome, string chave, Color cor, float pulso)
        {
            RectTransform r = Esticar(No(pai, nome));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = Sprite(chave);
            img.type = Image.Type.Simple;
            img.color = cor;
            img.raycastTarget = false;

            if (pulso > 0f)
            {
                var p = r.gameObject.AddComponent<UIPulse>();
                Privado(p, "periodo", pulso);
                Privado(p, "alfaMin", 0.25f);
                Privado(p, "alfaMax", 0.9f);
            }

            r.gameObject.SetActive(false);
            return img;
        }

        // ================================================================== Utilidades locais

        /// <summary>Item de Layout Group com tamanho preferido fixo.</summary>
        private static void Elemento(RectTransform r, float w, float h)
        {
            var le = r.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = w;
            le.preferredHeight = h;
        }

        /// <summary>Caixa com um TMP dentro, para textos posicionados por caixa própria.</summary>
        private static TextMeshProUGUI Rotulo(Transform pai, string nome, Ancora a,
                                              float x, float y, float w, float h, string valor,
                                              string familia, int peso, float corpo, Color cor,
                                              TextAlignmentOptions alinhamento,
                                              float espacamentoEm = 0f, float sombraY = 0f)
        {
            RectTransform r = Caixa(pai, nome, a, x, y, w, h);
            return Texto(r, "Label", valor, familia, peso, corpo, cor, alinhamento, espacamentoEm, sombraY);
        }

        // ================================================================== Binders

        private static void Ligar(GameObject raiz, PecasVitais v, PecasDePoder poder,
                                  PecasDeAlerta alerta, StandingsV2UI.Linha[] linhas,
                                  ToastNotificationUI.Slot[] toasts)
        {
            var vital = v.Cluster.AddComponent<VitalClusterUI>();
            var so = new SerializedObject(vital);
            so.FindProperty("raizVida").objectReferenceValue = v.RaizVida;
            so.FindProperty("valorDeVida").objectReferenceValue = v.ValorDeVida;

            SerializedProperty segs = so.FindProperty("segmentosDeVida");
            segs.arraySize = 5;
            for (int i = 0; i < 5; i++)
            {
                SerializedProperty s = segs.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("cheio").objectReferenceValue = v.SegCheio[i];
                s.FindPropertyRelative("ferido").objectReferenceValue = v.SegFerido[i];
                s.FindPropertyRelative("vazio").objectReferenceValue = v.SegVazio[i];
            }

            so.FindProperty("raizEscudo").objectReferenceValue = v.RaizEscudo;
            so.FindProperty("estadoPronto").objectReferenceValue = v.EscudoPronto;
            so.FindProperty("estadoAtivo").objectReferenceValue = v.EscudoAtivo;
            so.FindProperty("estadoRecarga").objectReferenceValue = v.EscudoRecarga;
            so.FindProperty("estadoApagado").objectReferenceValue = v.EscudoApagado;
            so.FindProperty("preenchimentoRecarga").objectReferenceValue = v.PreenchimentoRecarga;
            so.FindProperty("pontaDaRecarga").objectReferenceValue = v.PontaDaRecarga;
            so.FindProperty("textoDaChapinhaAtivo").objectReferenceValue = v.TextoChipAtivo;
            so.FindProperty("textoDaChapinhaRecarga").objectReferenceValue = v.TextoChipRecarga;
            so.FindProperty("raizImunidade").objectReferenceValue = v.RaizImunidade;
            so.FindProperty("barraDeImunidade").objectReferenceValue = v.BarraImunidade;
            so.FindProperty("raizReparo").objectReferenceValue = v.RaizReparo;
            so.FindProperty("preenchimentoDoReparo").objectReferenceValue = v.PreenchimentoReparo;
            so.FindProperty("contagemDoReparo").objectReferenceValue = v.ContagemDoReparo;
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform camada = alerta.Fraco.transform.parent;
            var numeros = camada.gameObject.AddComponent<FloatingNumbersUI>();
            var soN = new SerializedObject(numeros);
            SerializedProperty slots = soN.FindProperty("slots");
            slots.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty s = slots.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("raiz").objectReferenceValue = alerta.Numeros[i].raiz;
                s.FindPropertyRelative("movimento").objectReferenceValue = alerta.Numeros[i].movimento;
                s.FindPropertyRelative("estadoDano").objectReferenceValue = alerta.Numeros[i].estadoDano;
                s.FindPropertyRelative("estadoCura").objectReferenceValue = alerta.Numeros[i].estadoCura;
                s.FindPropertyRelative("textoDano").objectReferenceValue = alerta.Numeros[i].textoDano;
                s.FindPropertyRelative("textoCura").objectReferenceValue = alerta.Numeros[i].textoCura;
            }
            soN.ApplyModifiedPropertiesWithoutUndo();

            var arco = camada.gameObject.AddComponent<DangerArcUI>();
            var soA = new SerializedObject(arco);
            soA.FindProperty("arcoFraco").objectReferenceValue = alerta.Fraco;
            soA.FindProperty("arcoForte").objectReferenceValue = alerta.Forte;
            soA.FindProperty("pulsoDeTras").objectReferenceValue = alerta.PulsoDeTras;
            soA.FindProperty("graficoFraco").objectReferenceValue = alerta.GraficoFraco;
            soA.FindProperty("graficoForte").objectReferenceValue = alerta.GraficoForte;
            soA.ApplyModifiedPropertiesWithoutUndo();
            camada.gameObject.AddComponent<DangerArcDriver>();

            Transform listaT = linhas[0].raiz.transform.parent;
            var standings = listaT.gameObject.AddComponent<StandingsV2UI>();
            var soS = new SerializedObject(standings);
            SerializedProperty ls = soS.FindProperty("linhas");
            ls.arraySize = linhas.Length;
            for (int i = 0; i < linhas.Length; i++)
            {
                SerializedProperty s = ls.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("raiz").objectReferenceValue = linhas[i].raiz;
                s.FindPropertyRelative("estadoLocal").objectReferenceValue = linhas[i].estadoLocal;
                s.FindPropertyRelative("estadoOutro").objectReferenceValue = linhas[i].estadoOutro;
                s.FindPropertyRelative("posicaoLocal").objectReferenceValue = linhas[i].posicaoLocal;
                s.FindPropertyRelative("nomeLocal").objectReferenceValue = linhas[i].nomeLocal;
                s.FindPropertyRelative("tempoLocal").objectReferenceValue = linhas[i].tempoLocal;
                s.FindPropertyRelative("posicaoOutro").objectReferenceValue = linhas[i].posicaoOutro;
                s.FindPropertyRelative("nomeOutro").objectReferenceValue = linhas[i].nomeOutro;
                s.FindPropertyRelative("tempoOutro").objectReferenceValue = linhas[i].tempoOutro;
            }
            soS.ApplyModifiedPropertiesWithoutUndo();

            Transform pilha = toasts[0].raiz.transform.parent;
            var toastUI = pilha.gameObject.AddComponent<ToastNotificationUI>();
            var soT = new SerializedObject(toastUI);
            SerializedProperty ts = soT.FindProperty("slots");
            ts.arraySize = toasts.Length;
            for (int i = 0; i < toasts.Length; i++)
            {
                SerializedProperty s = ts.GetArrayElementAtIndex(i);
                s.FindPropertyRelative("raiz").objectReferenceValue = toasts[i].raiz;
                s.FindPropertyRelative("grupo").objectReferenceValue = toasts[i].grupo;
                s.FindPropertyRelative("texto").objectReferenceValue = toasts[i].texto;
                s.FindPropertyRelative("icone").objectReferenceValue = toasts[i].icone;
            }
            soT.ApplyModifiedPropertiesWithoutUndo();

            Transform info = raiz.transform.Find("InfoLayer");
            var hud = info.gameObject.AddComponent<RaceHUDUI>();
            var soH = new SerializedObject(hud);
            soH.FindProperty("textoVolta").objectReferenceValue = Achar<TextMeshProUGUI>(info, "LapPlate/Lap/Label");
            soH.FindProperty("textoTempo").objectReferenceValue = Achar<TextMeshProUGUI>(info, "LapPlate/Time/Label");
            soH.FindProperty("textoUltimaVolta").objectReferenceValue = Achar<TextMeshProUGUI>(info, "TimeChips/Chip_Last/Label");
            soH.FindProperty("textoMelhorVolta").objectReferenceValue = Achar<TextMeshProUGUI>(info, "TimeChips/Chip_Best/Label");
            soH.ApplyModifiedPropertiesWithoutUndo();

            var slot2 = poder.Cheio.transform.parent.gameObject.AddComponent<PowerSlotUI>();
            var soP = new SerializedObject(slot2);
            soP.FindProperty("estadoCheio").objectReferenceValue = poder.Cheio;
            soP.FindProperty("estadoVazio").objectReferenceValue = poder.Vazio;
            soP.FindProperty("iconeCheio").objectReferenceValue = poder.Icone;
            soP.FindProperty("nomeDoPoder").objectReferenceValue = poder.NomeDoPoder;
            soP.FindProperty("cartaoDoNome").objectReferenceValue = poder.ChipCheio;
            soP.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Achar<T>(Transform raiz, string caminho) where T : Component
        {
            Transform t = raiz.Find(caminho);
            return t != null ? t.GetComponent<T>() : null;
        }

        // ================================================================== Captura

        [MenuItem("Party Racers/UI v2/HUD · Capturar os estados", priority = 31)]
        public static void CapturarEstados()
        {
            Cenario("normal", new[]
            {
                "PowerSlot/State_Filled", "PowerSlot/State_Empty:off",
                "InfoLayer/Standings/Row_4/State_IsLocal",
                "InfoLayer/Standings/Row_4/State_Other:off",
                "InfoLayer/ToastStack/Toast_1",
            });

            Cenario("recarga", new[]
            {
                "VitalCluster/ShieldBar/State_Ready:off", "VitalCluster/ShieldBar/State_Cooling",
                "VitalCluster/ImmunityTick",
                "InfoLayer/ToastStack/Toast_1", "InfoLayer/ToastStack/Toast_2",
                "PowerSlot/State_Filled", "PowerSlot/State_Empty:off",
            });

            Cenario("danificado", new[]
            {
                "VitalCluster/ShieldBar/State_Ready:off", "VitalCluster/ShieldBar/State_Broken",
                "VitalCluster/HealthBar:off", "VitalCluster/RepairBar",
                "AlertLayer/DangerArc_Imminent", "AlertLayer/FloatNumber_1",
            });

            Cenario("escudo-ativo", new[]
            {
                "VitalCluster/ShieldBar/State_Ready:off", "VitalCluster/ShieldBar/State_Active",
                "AlertLayer/ShieldFlash", "AlertLayer/ChoicePrompt",
                "PowerSlot/State_Filled", "PowerSlot/State_Empty:off",
            });
        }

        private static void Cenario(string nome, string[] caminhos)
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var canvasGO = new GameObject("Canvas_UI", typeof(Canvas), typeof(CanvasScaler));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Destino);
            if (asset == null)
                return;

            var tela = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvasGO.transform);
            tela.name = "Screen_RaceHUD_PC";
            Esticar((RectTransform)tela.transform);

            // Fundo de PISTA: sobre preto, a moldura tracejada (alfa .42) e o "DE 12" somem, e a
            // captura mentiria sobre o contraste real.
            var fundo = new GameObject("Fundo_Pista", typeof(Image));
            fundo.transform.SetParent(tela.transform, false);
            fundo.transform.SetAsFirstSibling();
            Esticar((RectTransform)fundo.transform);
            fundo.GetComponent<Image>().color = Color.white;
            fundo.AddComponent<UIGradient>().Definir(Hex("#4FA8DC"), Hex("#3E8C58"));

            foreach (string entrada in caminhos)
            {
                bool ligar = !entrada.EndsWith(":off");
                string caminho = ligar ? entrada : entrada.Substring(0, entrada.Length - 4);
                Transform t = tela.transform.Find(caminho);
                if (t != null)
                    t.gameObject.SetActive(ligar);
                else
                    Debug.LogWarning($"[HUD v2] cenário '{nome}': caminho não encontrado — {caminho}");
            }

            Canvas.ForceUpdateCanvases();
            string saida = $"Docs/UI-v2/capturas/HUD_v2_{nome}.png";
            PartyRacers.UI.EditorTools.UICaptureTool.Capture("Screen_RaceHUD_PC", saida);
            Debug.Log($"[HUD v2] cenário {nome} → {saida}");
        }

        private static void Relatar()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[HUD v2] montado → {Destino}");
            foreach (string p in LayoutResources.Pendencias)
                sb.AppendLine("  • " + p);
            Debug.Log(sb.ToString());
        }
    }
}
