using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;
using static PartyRacers.UI.EditorTools.UIKitPlaca;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Telas 08 (Loja), 09 (Passe de Batalha) e 13 (Loading) do PLACA. Só desktop 1920×1080.
    /// </summary>
    public static class BuildScreensMeta
    {
        [MenuItem("Party Racers/UI/5 - Gerar Telas de Loja, Passe e Loading")]
        public static void Gerar()
        {
            GarantirPasta(SCREENS);
            Store();
            BattlePass();
            Loading();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] telas de loja/passe/loading geradas em " + SCREENS);
        }

        // ═══════════ 08 · LOJA ═══════════
        static void Store()
        {
            var raiz = Tela("Screen_Store");
            Fundo(raiz.transform, DeepBlue);
            Logo(raiz.transform).GetComponent<RectTransform>().anchoredPosition = new Vector2(56, -44);
            Nav(raiz.transform, 2);
            Carteira(raiz.transform);

            // abas de categoria
            var abas = Node("Abas", raiz.transform);
            Place(abas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -150), new Vector2(1180, 60));
            HLayout(abas, 10, new RectOffset(), TextAnchor.UpperLeft);
            var cats = new[] { "TODOS", "CARROS", "RODAS", "ADESIVOS", "BUZINAS", "RASTROS" };
            for (int i = 0; i < cats.Length; i++)
            {
                var chip = Widget("Chip_Tab", abas.transform);
                chip.name = "Aba_" + cats[i];
                Size(chip, 186, 60);
                foreach (var t in chip.GetComponentsInChildren<TextMeshProUGUI>(true)) t.text = cats[i];
                chip.transform.Find("State_Idle").gameObject.SetActive(i != 0);
                chip.transform.Find("State_Active").gameObject.SetActive(i == 0);
            }

            // ---- destaque da semana ----
            var dest = Node("DestaqueSemana", raiz.transform);
            Place(dest, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -236), new Vector2(1180, 400));
            var dBg = Painel("Bg", dest.transform, "Ink", 10f); Stretch(dBg.gameObject);

            var dPrev = Card("Preview", dest.transform, "Royal");
            Place(dPrev.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(10, 0), new Vector2(604, 380));
            var dArte = Img("Arte", dPrev.transform, null, Color.white, Image.Type.Simple);
            Stretch(dArte.gameObject, 30, 30, 30, 30); dArte.preserveAspect = true; dArte.enabled = false;

            var promo = Node("Promo", dPrev.transform);
            Place(promo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(220, 52));
            promo.transform.localRotation = Quaternion.Euler(0, 0, -3f);
            var prImg = Img("Bg", promo.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Red);
            Stretch(prImg.gameObject);
            var prTx = Rotulo("Label", promo.transform, "-30% · 2 DIAS", 21, Ink, TextAlignmentOptions.Center, 6f);
            Stretch(prTx.gameObject, 10, 4, 10, 4);

            var dInfo = Node("Info", dest.transform);
            Place(dInfo, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-36, 0), new Vector2(520, 340));
            var dLbl = Legenda("Rotulo", dInfo.transform, "DESTAQUE DA SEMANA", 17, Amber, TextAlignmentOptions.Left, 20f);
            Place(dLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(520, 22));
            var dNome = Display("Nome", dInfo.transform, "KIT FOGUETÃO", 48, Cream, TextAlignmentOptions.Left);
            Place(dNome.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -34), new Vector2(520, 54));
            var dRar = Legenda("Raridade", dInfo.transform, "LENDÁRIO · 4 ITENS", 19, Amber, TextAlignmentOptions.Left, 14f);
            Place(dRar.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -96), new Vector2(520, 24));

            var dItens = Node("Itens", dInfo.transform);
            Place(dItens, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -136), new Vector2(520, 130));
            VLayout(dItens, 7, new RectOffset(), TextAnchor.UpperLeft);
            var itens = new (string, Color)[] { ("Carro Foguetão", Amber), ("Rodas Chama", Sky), ("Adesivo Contagem", Green), ("Rastro Faísca", Violet) };
            foreach (var it in itens)
            {
                var l = Node("Item_" + it.Item1.Replace(" ", ""), dItens.transform);
                Size(l, 520, 26);
                var pt = Img("Ponto", l.transform, Sprite("Frames", "UI_Badge_R14_Cream"), it.Item2, Image.Type.Simple);
                pt.type = Image.Type.Simple;
                Place(pt.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(16, 16));
                var tx = Rotulo("Label", l.transform, it.Item1, 21, TextSecondary);
                tx.font = FonteUiSemi;
                Place(tx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, 0), new Vector2(460, 26));
            }

            var dCompra = Node("Btn_Comprar", dInfo.transform);
            Place(dCompra, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(360, 82));
            var dcImg = Img("Bg", dCompra.transform, Sprite("Frames", "UI_Button_R22_Green"), Color.white);
            Stretch(dcImg.gameObject); Botao(dcImg, Sprite("Frames", "UI_Button_R22_Pressed_Green"));
            var dcIc = Icone("Icon", dCompra.transform, "Icon_Coin", Cream, 28);
            Place(dcIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(80, 4), new Vector2(28, 28));
            var dcTx = Display("Preco", dCompra.transform, "2.100", 32, Ink, TextAlignmentOptions.Left);
            Place(dcTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(120, 4), new Vector2(180, 40));
            var dcAntes = Rotulo("PrecoAntigo", dInfo.transform, "3.000", 24, TextDisabled, TextAlignmentOptions.Left, 0f);
            dcAntes.font = FonteUiSemi;
            dcAntes.fontStyle = FontStyles.Strikethrough;
            Place(dcAntes.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(380, 28), new Vector2(120, 30));

            // ---- grade de cards ----
            var grade = Node("Grade", raiz.transform);
            Place(grade, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -660), new Vector2(1180, 340));
            Grid(grade, new Vector2(274, 340), new Vector2(28, 28), 4);
            // Grade vazia: quem instancia Item_StoreCard aqui é o StoreScreenUI, a partir dos
            // StoreItemDefinition. Deixar cards de exemplo somava com os do binder e a segunda
            // fileira vazava para fora do painel.

            // ---- coluna direita: rotação diária + chamada do passe ----
            var lado = Node("ColunaDireita", raiz.transform);
            Place(lado, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -236), new Vector2(596, 764));
            VLayout(lado, 22, new RectOffset(), TextAnchor.UpperCenter);

            var diarios = Node("ItensDiarios", lado.transform);
            Size(diarios, 596, 440);
            var diBg = Painel("Bg", diarios.transform, "Ink", 9f); Stretch(diBg.gameObject);
            var diTit = Display("Titulo", diarios.transform, "ITENS DIÁRIOS", 30, Amber, TextAlignmentOptions.Left);
            Place(diTit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(340, 36));

            var timer = Node("Timer", diarios.transform);
            Place(timer, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-28, -28), new Vector2(170, 42));
            var tiBg = Card("Bg", timer.transform, "Deep"); Stretch(tiBg.gameObject);
            var tiIc = Icone("Icon", timer.transform, "Icon_Timer", Amber, 18);
            Place(tiIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(12, 0), new Vector2(18, 18));
            var tiTx = Numero("Valor", timer.transform, "04:12:38", 20, TextSecondary, TextAlignmentOptions.Right);
            Place(tiTx.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(120, 26));

            var linhas = Node("Lista", diarios.transform);
            var rtL = RT(linhas);
            rtL.anchorMin = new Vector2(0, 1); rtL.anchorMax = new Vector2(1, 1);
            rtL.offsetMin = new Vector2(28, -420); rtL.offsetMax = new Vector2(-28, -86);
            VLayout(linhas, 12, new RectOffset(), TextAnchor.UpperCenter);
            // Idem grade: a rotação diária é preenchida pelo StoreScreenUI.

            var chamada = Node("ChamadaPasse", lado.transform);
            Size(chamada, 596, 302);
            Contorno(chamada.transform, Amber, 6f);
            var chBg = Painel("Bg", chamada.transform, "Deep", 9f); Stretch(chBg.gameObject);
            chBg.color = Royal;
            var chLbl = Legenda("Rotulo", chamada.transform, "PASSE DE BATALHA · TEMPORADA 1", 17, CreamDim, TextAlignmentOptions.Left, 20f);
            Place(chLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -28), new Vector2(520, 22));
            var chNome = Display("Nome", chamada.transform, "FESTA NA PISTA", 36, Cream, TextAlignmentOptions.Left);
            Place(chNome.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -60), new Vector2(520, 42));
            var chTxt = Rotulo("Texto", chamada.transform, "40 níveis de recompensa, 24 dias restantes. Nada que altere desempenho.", 21, TextSecondary);
            chTxt.font = FonteUiSemi; chTxt.textWrappingMode = TextWrappingModes.Normal;
            chTxt.alignment = TextAlignmentOptions.TopLeft;
            var rtCh = RT(chTxt.gameObject);
            rtCh.anchorMin = new Vector2(0, 1); rtCh.anchorMax = new Vector2(1, 1);
            rtCh.offsetMin = new Vector2(28, -180); rtCh.offsetMax = new Vector2(-28, -110);

            var chBtn = Node("Btn_VerPasse", chamada.transform);
            Place(chBtn, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 28), new Vector2(536, 76));
            var chImg = Img("Bg", chBtn.transform, Sprite("Frames", "UI_Button_R22_Green"), Color.white);
            Stretch(chImg.gameObject); Botao(chImg, Sprite("Frames", "UI_Button_R22_Pressed_Green"));
            var chTx = Display("Label", chBtn.transform, "VER O PASSE", 28, Ink);
            Stretch(chTx.gameObject, 12, 12, 12, 4);

            SalvarTela(raiz, "Screen_Store");
        }

        // ═══════════ 09 · PASSE DE BATALHA ═══════════
        static void BattlePass()
        {
            var raiz = Tela("Screen_BattlePass");
            Fundo(raiz.transform, DeepBlue);
            Logo(raiz.transform).GetComponent<RectTransform>().anchoredPosition = new Vector2(56, -44);
            Nav(raiz.transform, 3);
            Carteira(raiz.transform);

            // ---- cabeçalho da temporada ----
            var topo = Node("Temporada", raiz.transform);
            Place(topo, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 172));
            var rtT = RT(topo);
            rtT.anchorMin = new Vector2(0, 1); rtT.anchorMax = new Vector2(1, 1);
            rtT.offsetMin = new Vector2(56, -322); rtT.offsetMax = new Vector2(-56, -150);
            HLayout(topo, 20, new RectOffset(), TextAnchor.MiddleLeft, false, true);

            var info = Node("Info", topo.transform);
            Size(info, 1268, 172);
            var inBg = Painel("Bg", info.transform, "Ink", 9f); Stretch(inBg.gameObject);
            var inLbl = Legenda("Rotulo", info.transform, "TEMPORADA 1", 17, Amber, TextAlignmentOptions.Left, 20f);
            Place(inLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -30), new Vector2(300, 22));
            var inNome = Display("Nome", info.transform, "FESTA NA PISTA", 42, Cream, TextAlignmentOptions.Left);
            Place(inNome.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -62), new Vector2(400, 48));

            var prazo = Node("Prazo", info.transform);
            Place(prazo, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(410, 0), new Vector2(290, 52));
            var prBg = Card("Bg", prazo.transform, "Deep"); Stretch(prBg.gameObject);
            var prIc = Icone("Icon", prazo.transform, "Icon_Timer", Amber, 18);
            Place(prIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(18, 18));
            var prTx = Rotulo("Label", prazo.transform, "TERMINA EM 24 DIAS", 22, TextSecondary, TextAlignmentOptions.Center, 0f);
            Stretch(prTx.gameObject, 42, 4, 12, 4);

            var nivel = Node("Nivel", info.transform);
            Place(nivel, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(716, 0), new Vector2(96, 96));
            nivel.transform.localRotation = Quaternion.Euler(0, 0, -3f);
            var nvBg = Img("Bg", nivel.transform, Sprite("Frames", "UI_Card_R18_Cream"), Amber);
            Stretch(nvBg.gameObject);
            var nvVal = Display("Valor", nivel.transform, "12", 40, Ink);
            Place(nvVal.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 10), new Vector2(90, 44));
            var nvLbl = Legenda("Rotulo", nivel.transform, "NÍVEL", 13, Ink, TextAlignmentOptions.Center, 14f);
            Place(nvLbl.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -22), new Vector2(90, 18));

            // 402 de largura começando em 834: o badge de nível termina em 812, sobra folga.
            // Rótulo 250 + valor 150 = 400. Sem essas medidas o "640 / 1.000 XP" estourava a
            // própria caixa para a esquerda e caía por cima do rótulo.
            var prog = Node("Progresso", info.transform);
            Place(prog, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-32, 0), new Vector2(402, 76));
            var pgLbl = Legenda("Rotulo", prog.transform, "PROGRESSO PARA O NÍVEL 13", 17, TextMuted, TextAlignmentOptions.Left, 4f);
            Place(pgLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(250, 22));
            pgLbl.enableWordWrapping = false;
            pgLbl.enableAutoSizing = true; pgLbl.fontSizeMax = 17; pgLbl.fontSizeMin = 12;
            var pgVal = Numero("Valor", prog.transform, "640 / 1.000 XP", 21, Cream);
            Place(pgVal.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(150, 26));
            var pgFill = Barra("Barra", prog.transform, 402, 26, Amber, .64f);
            Place(pgFill.transform.parent.gameObject, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(420, 26));

            var premium = Node("Premium", topo.transform);
            Size(premium, 520, 172);
            Contorno(premium.transform, Amber, 6f);
            var pmBg = Painel("Bg", premium.transform, "Deep", 9f); Stretch(pmBg.gameObject);
            pmBg.color = Royal;
            var pmLbl = Legenda("Rotulo", premium.transform, "PASSE PREMIUM", 17, CreamDim, TextAlignmentOptions.Left, 20f);
            Place(pmLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -22), new Vector2(400, 22));
            var pmTxt = Rotulo("Texto", premium.transform, "Libera a faixa de cima + 2 níveis na hora.", 19, TextSecondary);
            pmTxt.font = FonteUiSemi;
            Place(pmTxt.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(28, -50), new Vector2(440, 24));
            var pmBtn = Node("Btn_Comprar", premium.transform);
            Place(pmBtn, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(464, 72));
            var pmImg = Img("Bg", pmBtn.transform, Sprite("Frames", "UI_Button_R22_Green"), Color.white);
            Stretch(pmImg.gameObject); Botao(pmImg, Sprite("Frames", "UI_Button_R22_Pressed_Green"));
            var pmIc = Icone("Icon", pmBtn.transform, "Icon_Diamond", Violet, 28);
            Place(pmIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(90, 4), new Vector2(28, 28));
            var pmTx = Display("Label", pmBtn.transform, "950 FICHAS", 30, Ink, TextAlignmentOptions.Left);
            Place(pmTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(132, 4), new Vector2(280, 38));

            // ---- trilha horizontal: cabeçalho de nível + faixa premium + faixa grátis ----
            var trilha = Node("Trilha", raiz.transform);
            Place(trilha, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -352), new Vector2(1808, 500));
            VLayout(trilha, 14, new RectOffset(), TextAnchor.UpperLeft);

            var niveis = Node("Niveis", trilha.transform);
            Size(niveis, 1808, 56);
            HLayout(niveis, 16, new RectOffset(), TextAnchor.MiddleLeft);
            var vazio = Node("Espacador", niveis.transform); Size(vazio, 170, 56);
            for (int i = 10; i <= 15; i++)
            {
                bool atual = i == 12;
                var n = Node("Nivel_" + i, niveis.transform);
                Size(n, 250, 56);

                // Dois estados irmãos por coluna: qual nível é "o seu" depende do jogador,
                // então o binder liga um e desliga o outro em vez de repintar o fundo.
                var idle = Node("State_Idle", n.transform); Stretch(idle);
                var iBg = Img("Bg", idle.transform, Sprite("Frames", "UI_Card_R18_Ink"), Color.white);
                Stretch(iBg.gameObject);
                var iTx = Rotulo("Label", idle.transform, "NÍVEL " + i, 23, TextMuted, TextAlignmentOptions.Center, 6f);
                Stretch(iTx.gameObject, 8, 4, 8, 4);

                var act = Node("State_Active", n.transform); Stretch(act);
                var aBg = Img("Bg", act.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white);
                Stretch(aBg.gameObject);
                var aTx = Rotulo("Label", act.transform, "NÍVEL " + i + " · VOCÊ", 23, Ink, TextAlignmentOptions.Center, 6f);
                Stretch(aTx.gameObject, 8, 4, 8, 4);

                idle.SetActive(!atual);
                act.SetActive(atual);
            }

            FaixaTrilha(trilha.transform, "Faixa_Premium", "PREMIUM", "exige passe", Amber, 232,
                        new[] { "State_Claimed", "State_Claimed", "State_Available", "State_LockedPass", "State_LockedPass", "State_LockedPass" },
                        new[] { "Adesivo Balão", "Rodas Neon", "Carro Pipoca", "Rastro Fita", "20 fichas", "Carro Bolo" });

            FaixaTrilha(trilha.transform, "Faixa_Gratis", "GRÁTIS", "todo mundo", TextMuted, 186,
                        new[] { "State_Claimed", "State_LockedLevel", "State_Available", "State_LockedLevel", "State_LockedLevel", "State_LockedLevel" },
                        new[] { "200 moedas", "Buzina Apito", "400 moedas", "Adesivo Bolinha", "150 moedas", "Rodas Simples" });

            // ---- missões diárias ----
            var missoes = Node("MissoesDiarias", raiz.transform);
            Place(missoes, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 56), new Vector2(1808, 150));
            HLayout(missoes, 20, new RectOffset(), TextAnchor.MiddleLeft, true, true);
            var mTit = new[] { "CORRA 3 PARTIDAS", "ACERTE 5 PODERES", "TERMINE EM TOP 3" };
            var mXp = new (string, Color)[] { ("+150 XP", Sky), ("+200 XP", Red), ("+300 XP", Green) };
            var mProg = new[] { "2 / 3 · missão diária", "4 / 5 · missão diária", "0 / 1 · missão diária" };
            var mFill = new[] { .66f, .8f, 0f };
            for (int i = 0; i < 3; i++)
            {
                var m = Item("Item_Mission", missoes.transform);
                m.name = "Missao_" + (i + 1);
                Size(m, 589, 150);
                m.transform.Find("Titulo").GetComponent<TextMeshProUGUI>().text = mTit[i];
                var xp = m.transform.Find("Xp").GetComponent<TextMeshProUGUI>();
                xp.text = mXp[i].Item1; xp.color = mXp[i].Item2;
                m.transform.Find("Progresso").GetComponent<TextMeshProUGUI>().text = mProg[i];
                var f = m.transform.Find("Barra/Fill").GetComponent<Image>();
                f.fillAmount = mFill[i]; f.color = mXp[i].Item2;
            }

            SalvarTela(raiz, "Screen_BattlePass");
        }

        static void FaixaTrilha(Transform pai, string nome, string titulo, string legenda, Color cor, float altura,
                                string[] estados, string[] rotulos)
        {
            var faixa = Node(nome, pai);
            Size(faixa, 1808, altura);
            HLayout(faixa, 16, new RectOffset(), TextAnchor.MiddleLeft, false, true);

            var cab = Node("Cabecalho", faixa.transform);
            Size(cab, 170, altura);
            var t = Display("Titulo", cab.transform, titulo, 26, cor, TextAlignmentOptions.Left);
            Place(t.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(4, 12), new Vector2(170, 30));
            var l = Anotacao("Legenda", cab.transform, legenda, 14, Lavanda);
            Place(l.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(4, -14), new Vector2(170, 20));

            // A trilha nasce vazia de propósito: quem enche é o BattlePassScreenUI, instanciando
            // Item_PassTier neste mesmo HLayout. Deixar os cards de exemplo aqui fazia a linha
            // ter 12 colunas em play (6 do mock + 6 do binder) e metade vazava para fora da tela.
            // Os parâmetros de exemplo continuam na assinatura para o designer poder repovoar
            // a linha à mão se quiser ver a tela cheia no editor.
            _ = estados; _ = rotulos;
        }

        // ═══════════ 13 · LOADING ═══════════
        static void Loading()
        {
            var raiz = Tela("Screen_Loading");
            Fundo(raiz.transform, DeepBlue);

            // faixas decorativas inclinadas
            Faixas(raiz.transform, "Faixas_Topo", new Vector2(-80, -130), new[] { (210f, Amber), (150f, Green), (290f, Sky) });
            Faixas(raiz.transform, "Faixas_Base", new Vector2(1120, -890), new[] { (180f, Sky), (260f, Red) });

            var centro = Node("Centro", raiz.transform);
            Place(centro, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(700, 420));

            // lockup grande: PARTY em Titan One + placa RACERS (não existe PNG do logo fechado)
            var lock_ = Node("Lockup", centro.transform);
            Place(lock_, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(560, 230));
            lock_.transform.localRotation = Quaternion.Euler(0, 0, -2f);

            var pontinhos = Node("Pontos", lock_.transform);
            Place(pontinhos, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(120, 26));
            HLayout(pontinhos, 11, new RectOffset(), TextAnchor.MiddleCenter);
            foreach (var c in new[] { Red, Green, Sky })
            {
                var p = Img("Ponto", pontinhos.transform, Sprite("Frames", "UI_Badge_R14_Cream"), c, Image.Type.Simple);
                p.type = Image.Type.Simple; Size(p.gameObject, 26, 26);
            }

            var party = Display("Party", lock_.transform, "PARTY", 82, Cream);
            Place(party.gameObject, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -36), new Vector2(560, 90));
            party.outlineWidth = .22f; party.outlineColor = Ink;

            var placa = Img("PlacaRacers", lock_.transform, Sprite("Brand", "Countdown_Plate"), Amber, Image.Type.Simple);
            Place(placa.gameObject, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -126), new Vector2(330, 92));
            var racers = Display("Racers", placa.transform, "RACERS", 58, Ink);
            Stretch(racers.gameObject, 12, 14, 12, 8);

            // 3 quadradinhos: atividade, não porcentagem falsa
            var pulso = Node("Pulso", centro.transform);
            Place(pulso, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -110), new Vector2(140, 28));
            HLayout(pulso, 14, new RectOffset(), TextAnchor.MiddleCenter);
            for (int i = 0; i < 3; i++)
            {
                var p = Img("Passo_" + (i + 1), pulso.transform, Sprite("Frames", "UI_Badge_R14_Cream"),
                            new Color(Amber.r, Amber.g, Amber.b, i == 0 ? 1f : (i == 1 ? .5f : .2f)), Image.Type.Simple);
                p.type = Image.Type.Simple; Size(p.gameObject, 28, 28);
            }

            var estado = Display("Estado", centro.transform, "CARREGANDO PISTA", 30, TextMuted);
            estado.characterSpacing = 10f;
            Place(estado.gameObject, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(600, 40));

            // dica
            var dica = Node("Dica", raiz.transform);
            Place(dica, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 56), new Vector2(900, 120));
            var dLbl = Legenda("Rotulo", dica.transform, "DICA", 18, Amber, TextAlignmentOptions.Left, 18f);
            Place(dLbl.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(200, 22));
            var dTxt = Rotulo("Texto", dica.transform,
                "O escudo bloqueia o disco voador — se você está em primeiro, guarde-o para a última volta.", 29, Cream);
            dTxt.font = FonteUiSemi; dTxt.textWrappingMode = TextWrappingModes.Normal;
            dTxt.alignment = TextAlignmentOptions.TopLeft;
            var rtD = RT(dTxt.gameObject);
            rtD.anchorMin = new Vector2(0, 0); rtD.anchorMax = new Vector2(1, 1);
            rtD.offsetMin = new Vector2(0, 0); rtD.offsetMax = new Vector2(0, -34);

            // qualidade da conexão
            var ping = Node("Conexao", raiz.transform);
            Place(ping, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-56, 56), new Vector2(340, 60));
            var pgBg = Card("Bg", ping.transform, "Deep"); Stretch(pgBg.gameObject);
            var pgIc = Icone("Icon", ping.transform, "Icon_Signal", Green, 24);
            Place(pgIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(24, 24));
            var pgTx = Rotulo("Label", ping.transform, "CONEXÃO BOA · 28 ms", 21, Cream, TextAlignmentOptions.Center, 5f);
            Stretch(pgTx.gameObject, 48, 4, 14, 4);

            SalvarTela(raiz, "Screen_Loading");
        }

        static void Faixas(Transform pai, string nome, Vector2 pos, (float, Color)[] barras)
        {
            var go = Node(nome, pai);
            Place(go, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(800, 28));
            go.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            HLayout(go, 32, new RectOffset(), TextAnchor.MiddleLeft);
            foreach (var b in barras)
            {
                var img = Img("Faixa", go.transform, Sprite("Frames", "UI_Badge_R14_Cream"),
                              new Color(b.Item2.r, b.Item2.g, b.Item2.b, .2f), Image.Type.Simple);
                img.type = Image.Type.Simple;
                Size(img.gameObject, b.Item1, 28);
            }
        }
    }
}
