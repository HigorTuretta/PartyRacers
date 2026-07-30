using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;
using static PartyRacers.UI.EditorTools.UIKitPlaca;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Prefabs de Prefabs/UI/Items/ — as linhas e cards que as listas dinâmicas instanciam.
    /// Todo estado é um objeto irmão ligado por SetActive; nada é construído por código em runtime.
    /// </summary>
    public static class BuildItems
    {
        [MenuItem("Party Racers/UI/2 - Gerar Itens de Lista")]
        public static void Gerar()
        {
            GarantirPasta(ITEMS);
            LobbySlot();
            ResultRow();
            StoreCard();
            StoreDaily();
            PassTier();
            Mission();
            CategoryRow();
            SettingRow();
            SegmentedOption();
            CodeBox();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] itens de lista gerados em " + ITEMS);
        }

        // ---------- 06 lobby: vaga de jogador ----------
        static void LobbySlot()
        {
            var raiz = Node("Item_LobbySlot", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(520, 70));

            // ocupado
            var cheio = Node("State_Player", raiz.transform); Stretch(cheio);
            var cBg = Card("Bg", cheio.transform, "Deep"); Stretch(cBg.gameObject);
            var destaque = Node("Destaque_IsLocal", cheio.transform); Stretch(destaque);
            Contorno(destaque.transform, Amber, 4f);
            var dBg = Card("Bg", destaque.transform, "Royal"); Stretch(dBg.gameObject);
            destaque.SetActive(false);

            var marca = Icone("Marca", cheio.transform, "Icon_Person", Sky, 24);
            Place(marca.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(24, 24));

            var nome = Rotulo("Nome", cheio.transform, "JOGADOR", 23, Cream);
            var rtN = RT(nome.gameObject);
            rtN.anchorMin = Vector2.zero; rtN.anchorMax = Vector2.one;
            rtN.offsetMin = new Vector2(56, 6); rtN.offsetMax = new Vector2(-200, -6);

            var tag = Legenda("Tag", cheio.transform, "(bot)", 19, Violet, TextAlignmentOptions.Left, 0f);
            Place(tag.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(56, -20), new Vector2(180, 24));
            tag.gameObject.SetActive(false);

            var pronto = Node("State_Ready", cheio.transform);
            Place(pronto, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-18, 0), new Vector2(170, 30));
            var pIc = Icone("Icon", pronto.transform, "Icon_Check", Green, 24);
            Place(pIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(24, 24));
            var pTx = Rotulo("Label", pronto.transform, "PRONTO", 21, Green, TextAlignmentOptions.Right, 5f);
            Stretch(pTx.gameObject, 30, 0, 0, 0);

            var espera = Node("State_Waiting", cheio.transform);
            Place(espera, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-18, 0), new Vector2(170, 30));
            var eIc = Icone("Icon", espera.transform, "Icon_Circle", TextMuted, 22);
            Place(eIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(22, 22));
            var eTx = Rotulo("Label", espera.transform, "...", 21, TextMuted, TextAlignmentOptions.Right, 0f);
            Stretch(eTx.gameObject, 30, 0, 0, 0);
            espera.SetActive(false);

            // desconectado
            var desc = Node("State_Disconnected", raiz.transform); Stretch(desc);
            var dcBg = Card("Bg", desc.transform, "Deep", 0f); Stretch(dcBg.gameObject);
            dcBg.color = VermelhoFundo;
            Contorno(desc.transform, Red, 4f);
            var dcIc = Icone("Icon", desc.transform, "Icon_Triangle", Red, 22);
            Place(dcIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(22, 22));
            var dcNome = Rotulo("Nome", desc.transform, "JOGADOR", 23, RosaClaro);
            var rtD = RT(dcNome.gameObject);
            rtD.anchorMin = Vector2.zero; rtD.anchorMax = Vector2.one;
            rtD.offsetMin = new Vector2(56, 6); rtD.offsetMax = new Vector2(-210, -6);
            var dcTx = Rotulo("Label", desc.transform, "DESCONECTOU", 21, Red, TextAlignmentOptions.Right, 5f);
            Place(dcTx.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-18, 0), new Vector2(200, 30));
            desc.SetActive(false);

            // vaga livre
            var vazio = Node("State_Empty", raiz.transform); Stretch(vazio);
            var vBg = Tracejado("Bg", vazio.transform); Stretch(vBg.gameObject);
            var vIc = Icone("Icon", vazio.transform, "Icon_Plus", TextDisabled, 22);
            Place(vIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(22, 22));
            var vTx = Rotulo("Label", vazio.transform, "VAGA LIVRE", 21, TextDisabled, TextAlignmentOptions.Left, 5f);
            Stretch(vTx.gameObject, 56, 6, 18, 6);
            vazio.SetActive(false);

            SalvarItem(raiz, "Item_LobbySlot");
        }

        // ---------- 11 resultado: linha da tabela ----------
        static void ResultRow()
        {
            var raiz = Node("Item_ResultRow", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(880, 68));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Deep");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias; bg.raycastTarget = false;

            // molduras de destaque (irmãs, uma por situação)
            foreach (var par in new (string, Color)[]{ ("Destaque_Ouro", Amber), ("Destaque_Prata", Prata),
                                                       ("Destaque_Bronze", Bronze), ("Destaque_IsLocal", Amber),
                                                       ("Destaque_Correndo", Sky), ("Destaque_Desconectado", Red) })
            {
                var d = Node(par.Item1, raiz.transform); Stretch(d);
                Contorno(d.transform, par.Item2, 4f);
                var inner = Card("Bg", d.transform, "Deep"); Stretch(inner.gameObject);
                if (par.Item1 == "Destaque_IsLocal") inner.color = Color.white;
                if (par.Item1 == "Destaque_Correndo") inner.color = AzulFundo;
                if (par.Item1 == "Destaque_Desconectado") inner.color = VermelhoFundo;
                d.SetActive(false);
            }

            var pos = BadgePosicao(raiz.transform, "1", 46, Amber, Ink, 24);
            pos.transform.parent.name = "Posicao";

            var nome = Rotulo("Nome", raiz.transform, "JOGADOR", 25, Cream);
            var rtN = RT(nome.gameObject);
            rtN.anchorMin = Vector2.zero; rtN.anchorMax = Vector2.one;
            rtN.offsetMin = new Vector2(80, 6); rtN.offsetMax = new Vector2(-370, -6);

            var total = Numero("TempoTotal", raiz.transform, "03:41.208", 23, Cream);
            Place(total.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-170, 0), new Vector2(200, 40));

            var melhor = Numero("MelhorVolta", raiz.transform, "1:11.30", 21, TextMuted);
            Place(melhor.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(150, 40));

            // status substitui o tempo total quando o piloto ainda não terminou
            var status = Node("State_Status", raiz.transform);
            Place(status, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-170, 0), new Vector2(200, 40));
            var sIc = Icone("Icon", status.transform, "Icon_Diamond", Sky, 16);
            Place(sIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(16, 16));
            var sTx = Rotulo("Label", status.transform, "CORRENDO · V3/3", 19, Sky, TextAlignmentOptions.Right, 0f);
            Stretch(sTx.gameObject, 22, 0, 0, 0);
            status.SetActive(false);

            var flag = Icone("MelhorDaCorrida", raiz.transform, "Icon_Flag", Green, 20);
            Place(flag.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-176, 0), new Vector2(20, 20));
            flag.gameObject.SetActive(false);

            SalvarItem(raiz, "Item_ResultRow");
        }

        // ---------- 08 loja: card da grade ----------
        static void StoreCard()
        {
            var raiz = Node("Item_StoreCard", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(274, 340));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Deep");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            SombraDura(raiz.transform, bg.sprite, 7f);

            var preview = Card("Preview", raiz.transform, "Royal");
            Place(preview.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -8), new Vector2(-16, 170));
            var rtP = RT(preview.gameObject);
            rtP.anchorMin = new Vector2(0, 1); rtP.anchorMax = new Vector2(1, 1);
            rtP.offsetMin = new Vector2(8, -178); rtP.offsetMax = new Vector2(-8, -8);
            var arte = Img("Arte", preview.transform, null, Color.white, Image.Type.Simple);
            Stretch(arte.gameObject, 14, 14, 14, 14);
            arte.preserveAspect = true; arte.enabled = false;   // o binder liga ao atribuir o sprite
            var faixa = Img("FaixaRaridade", preview.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Sky);
            Place(faixa.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(-20, 10));

            var nome = Rotulo("Nome", raiz.transform, "Fusca Festivo", 25, Cream);
            Place(nome.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -196), new Vector2(-36, 34));
            nome.textWrappingMode = TextWrappingModes.Normal;

            var rar = Legenda("Raridade", raiz.transform, "RARO", 18, Sky);
            Place(rar.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -232), new Vector2(-36, 24));

            // 4 estados de rodapé
            var comprar = Node("State_Buy", raiz.transform);
            Place(comprar, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(238, 64));
            var cBg = Img("Bg", comprar.transform, Sprite("Frames", "UI_Button_R22_Amber"), Color.white);
            Stretch(cBg.gameObject);
            Botao(cBg, Sprite("Frames", "UI_Button_R22_Pressed_Amber"));
            var cIc = Icone("Icon", comprar.transform, "Icon_Coin", Cream, 24);
            Place(cIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(50, 3), new Vector2(24, 24));
            var cTx = Rotulo("Preco", comprar.transform, "900", 25, Ink, TextAlignmentOptions.Left, 0f);
            Place(cTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(84, 3), new Vector2(120, 30));

            var adquirido = Node("State_Owned", raiz.transform);
            Place(adquirido, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(238, 64));
            var aBg = Card("Bg", adquirido.transform, "Deep"); Stretch(aBg.gameObject);
            var aIc = Icone("Icon", adquirido.transform, "Icon_Check", Green, 22);
            Place(aIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(38, 0), new Vector2(22, 22));
            var aTx = Rotulo("Label", adquirido.transform, "ADQUIRIDO", 22, Green, TextAlignmentOptions.Left, 5f);
            Place(aTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(70, 0), new Vector2(160, 30));
            adquirido.SetActive(false);

            var bloqueado = Node("State_Locked", raiz.transform);
            Place(bloqueado, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 20), new Vector2(238, 64));
            var bBg = Tracejado("Bg", bloqueado.transform); Stretch(bBg.gameObject);
            var bIc = Icone("Icon", bloqueado.transform, "Icon_Lock", TextDisabled, 22);
            Place(bIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(44, 0), new Vector2(22, 22));
            var bTx = Rotulo("Label", bloqueado.transform, "NÍVEL 15", 21, TextDisabled, TextAlignmentOptions.Left, 5f);
            Place(bTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(76, 0), new Vector2(150, 30));
            bloqueado.SetActive(false);

            SalvarItem(raiz, "Item_StoreCard");
        }

        // ---------- 08 loja: linha da rotação diária ----------
        static void StoreDaily()
        {
            var raiz = Node("Item_StoreDaily", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(540, 106));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Deep");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias; bg.raycastTarget = false;

            var mini = Card("Preview", raiz.transform, "Royal");
            Place(mini.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(80, 80));
            var arte = Img("Arte", mini.transform, null, Color.white, Image.Type.Simple);
            Stretch(arte.gameObject, 8, 8, 8, 8); arte.preserveAspect = true; arte.enabled = false;   // o binder liga ao atribuir o sprite

            var nome = Rotulo("Nome", raiz.transform, "Capô Xadrez", 23, Cream);
            Place(nome.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(108, 14), new Vector2(240, 28));
            var rar = Legenda("Raridade", raiz.transform, "RARO", 17, Sky, TextAlignmentOptions.Left, 12f);
            Place(rar.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(108, -14), new Vector2(240, 24));

            var btn = Node("Btn_Comprar", raiz.transform);
            Place(btn, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(140, 56));
            var bImg = Img("Bg", btn.transform, Sprite("Frames", "UI_Button_R22_Amber"), Color.white);
            Stretch(bImg.gameObject);
            Botao(bImg, Sprite("Frames", "UI_Button_R22_Pressed_Amber"));
            var bIc = Icone("Icon", btn.transform, "Icon_Coin", Cream, 20);
            Place(bIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 3), new Vector2(20, 20));
            var bTx = Rotulo("Preco", btn.transform, "450", 23, Ink, TextAlignmentOptions.Left, 0f);
            Place(bTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(46, 3), new Vector2(84, 28));

            SalvarItem(raiz, "Item_StoreDaily");
        }

        // ---------- 09 passe: recompensa da trilha (4 estados) ----------
        static void PassTier()
        {
            var raiz = Node("Item_PassTier", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(250, 232));

            // resgatada
            var resg = Node("State_Claimed", raiz.transform); Stretch(resg);
            var rBg = Card("Bg", resg.transform, "Deep"); Stretch(rBg.gameObject);
            rBg.color = new Color(1, 1, 1, .7f);
            var rIc = Icone("Icon", resg.transform, "Icon_Check", Green, 44);
            Place(rIc.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 26), new Vector2(44, 44));
            var rTx = Rotulo("Nome", resg.transform, "Adesivo Balão", 20, TextMuted, TextAlignmentOptions.Center, 0f);
            Place(rTx.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -28), new Vector2(220, 52));
            rTx.textWrappingMode = TextWrappingModes.Normal;

            // disponível — destaque âmbar + botão RESGATAR
            var disp = Node("State_Available", raiz.transform); Stretch(disp);
            Contorno(disp.transform, Amber, 6f);
            var dBg = Card("Bg", disp.transform, "Royal"); Stretch(dBg.gameObject);
            var dArte = Card("Preview", disp.transform, "Royal");
            Place(dArte.gameObject, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(74, 74));
            var dImg = Img("Arte", dArte.transform, null, Color.white, Image.Type.Simple);
            Stretch(dImg.gameObject, 8, 8, 8, 8); dImg.preserveAspect = true; dImg.enabled = false;
            var dTx = Rotulo("Nome", disp.transform, "Carro Pipoca", 21, Cream, TextAlignmentOptions.Center, 0f);
            Place(dTx.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -14), new Vector2(220, 52));
            dTx.textWrappingMode = TextWrappingModes.Normal;
            var dBtn = Node("Btn_Resgatar", disp.transform);
            Place(dBtn, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 18), new Vector2(150, 44));
            var dbImg = Img("Bg", dBtn.transform, Sprite("Frames", "UI_Button_R22_Green"), Color.white);
            Stretch(dbImg.gameObject);
            Botao(dbImg, Sprite("Frames", "UI_Button_R22_Pressed_Green"));
            var dbTx = Rotulo("Label", dBtn.transform, "RESGATAR", 19, Ink, TextAlignmentOptions.Center, 5f);
            Stretch(dbTx.gameObject, 8, 8, 8, 2);
            disp.SetActive(false);

            // bloqueada por nível
            var bloqN = Node("State_LockedLevel", raiz.transform); Stretch(bloqN);
            var bnBg = Card("Bg", bloqN.transform, "Ink"); Stretch(bnBg.gameObject);
            var bnArte = Card("Preview", bloqN.transform, "Deep");
            Place(bnArte.gameObject, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -22), new Vector2(74, 74));
            var bnImg = Img("Arte", bnArte.transform, null, new Color(1, 1, 1, .5f), Image.Type.Simple);
            Stretch(bnImg.gameObject, 8, 8, 8, 8); bnImg.preserveAspect = true; bnImg.enabled = false;
            var bnTx = Rotulo("Nome", bloqN.transform, "Buzina Apito", 21, TextDisabled, TextAlignmentOptions.Center, 0f);
            Place(bnTx.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -34), new Vector2(220, 52));
            bnTx.textWrappingMode = TextWrappingModes.Normal;
            bloqN.SetActive(false);

            // bloqueada por passe — cadeado tracejado
            var bloqP = Node("State_LockedPass", raiz.transform); Stretch(bloqP);
            var bpBg = Tracejado("Bg", bloqP.transform); Stretch(bpBg.gameObject);
            var bpIc = Icone("Icon", bloqP.transform, "Icon_Lock", TextDisabled, 40);
            Place(bpIc.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 26), new Vector2(40, 40));
            var bpTx = Rotulo("Nome", bloqP.transform, "Rastro Fita", 20, TextDisabled, TextAlignmentOptions.Center, 0f);
            Place(bpTx.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, -28), new Vector2(220, 52));
            bpTx.textWrappingMode = TextWrappingModes.Normal;
            bloqP.SetActive(false);

            SalvarItem(raiz, "Item_PassTier");
        }

        // ---------- 09 passe: missão diária ----------
        static void Mission()
        {
            var raiz = Node("Item_Mission", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(580, 150));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Ink");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias; bg.raycastTarget = false;
            SombraDura(raiz.transform, bg.sprite, 7f);

            var titulo = Rotulo("Titulo", raiz.transform, "CORRA 3 PARTIDAS", 25, Cream);
            Place(titulo.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(360, 30));

            var xp = Rotulo("Xp", raiz.transform, "+150 XP", 22, Sky, TextAlignmentOptions.Right, 5f);
            Place(xp.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -26), new Vector2(160, 30));

            var fill = Barra("Barra", raiz.transform, 528, 18, Sky, .66f);
            Place(fill.transform.parent.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  new Vector2(0, -4), new Vector2(528, 18));

            var prog = Numero("Progresso", raiz.transform, "2 / 3 · missão diária", 20, TextMuted, TextAlignmentOptions.Left);
            Place(prog.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 22), new Vector2(400, 26));

            SalvarItem(raiz, "Item_Mission");
        }

        // ---------- 04 garagem: linha de categoria (< valor >) ----------
        static void CategoryRow()
        {
            var raiz = Node("Item_CategoryRow", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(448, 64));

            var idle = Node("State_Idle", raiz.transform); Stretch(idle);
            var iBg = Card("Bg", idle.transform, "Deep"); Stretch(iBg.gameObject);

            var sel = Node("State_Selected", raiz.transform); Stretch(sel);
            Contorno(sel.transform, Amber, 4f);
            var sBg = Card("Bg", sel.transform, "Royal"); Stretch(sBg.gameObject);
            sel.SetActive(false);

            var bloq = Node("State_Locked", raiz.transform); Stretch(bloq);
            var bBg = Card("Bg", bloq.transform, "Ink"); Stretch(bBg.gameObject);
            var bTx = Rotulo("Label", bloq.transform, "SEM VARIAÇÃO", 17, TextDisabled, TextAlignmentOptions.Right, 10f);
            Place(bTx.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(200, 26));
            bloq.SetActive(false);

            var nome = Rotulo("Nome", raiz.transform, "RODAS", 23, Cream);
            Place(nome.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(200, 30));

            var prev = Img("Btn_Prev", raiz.transform, Sprite("Frames", "UI_Card_R18_Ink"), Color.white);
            Place(prev.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-172, 0), new Vector2(40, 40));
            Icone("Icon", prev.transform, "Icon_ArrowLeft", Cream, 20);
            Botao(prev, null);

            var valor = Rotulo("Valor", raiz.transform, "2/6", 23, Amber, TextAlignmentOptions.Center, 0f);
            Place(valor.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-108, 0), new Vector2(76, 30));

            // Simple: o badge é 96×96 e a amostra é 56×40 — com 9-slice a borda de 36 viraria um círculo
            var amostra = Img("AmostraCor", raiz.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Red, Image.Type.Simple);
            amostra.type = Image.Type.Simple;
            Place(amostra.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-108, 0), new Vector2(56, 40));
            amostra.gameObject.SetActive(false);

            var next = Img("Btn_Next", raiz.transform, Sprite("Frames", "UI_Card_R18_Ink"), Color.white);
            Place(next.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-44, 0), new Vector2(40, 40));
            Icone("Icon", next.transform, "Icon_ArrowRight", Cream, 20);
            Botao(next, null);

            SalvarItem(raiz, "Item_CategoryRow");
        }

        // ---------- 10 configurações: linha rótulo + controle ----------
        static void SettingRow()
        {
            var raiz = Node("Item_SettingRow", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(660, 60));

            var nome = Rotulo("Nome", raiz.transform, "Volume geral", 24, Cream);
            Place(nome.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, 0), new Vector2(300, 30));

            var slot = Node("Controle", raiz.transform);
            Place(slot, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(0, 0), new Vector2(400, 52));
            HLayout(slot, 10, new RectOffset(), TextAnchor.MiddleRight);

            SalvarItem(raiz, "Item_SettingRow");
        }

        // ---------- 10 configurações: opção segmentada (BAIXA/MÉDIA/ALTA) ----------
        static void SegmentedOption()
        {
            var raiz = Node("Item_SegmentedOption", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(120, 52));
            raiz.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // área de clique

            var idle = Node("State_Idle", raiz.transform); Stretch(idle);
            var iBg = Card("Bg", idle.transform, "Deep"); Stretch(iBg.gameObject);
            var iTx = Rotulo("Label", idle.transform, "MÉDIA", 21, TextMuted, TextAlignmentOptions.Center, 3f);
            Stretch(iTx.gameObject, 10, 4, 10, 4);

            var act = Node("State_Active", raiz.transform); Stretch(act);
            var aBg = Img("Bg", act.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white);
            Stretch(aBg.gameObject);
            var aTx = Rotulo("Label", act.transform, "MÉDIA", 21, Ink, TextAlignmentOptions.Center, 3f);
            Stretch(aTx.gameObject, 10, 4, 10, 4);
            act.SetActive(false);

            Botao(raiz.GetComponent<Image>(), null);
            SalvarItem(raiz, "Item_SegmentedOption");
        }

        // ---------- 07 código: caixa de um caractere ----------
        static void CodeBox()
        {
            var raiz = Node("Item_CodeBox", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(114, 134));

            var idle = Node("State_Idle", raiz.transform); Stretch(idle);
            var iBg = Card("Bg", idle.transform, "Deep"); Stretch(iBg.gameObject);

            var foco = Node("State_Focused", raiz.transform); Stretch(foco);
            Contorno(foco.transform, Amber, 5f);
            var fBg = Card("Bg", foco.transform, "Royal"); Stretch(fBg.gameObject);
            var cursor = Img("Cursor", foco.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Amber);
            Place(cursor.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(7, 62));
            foco.SetActive(false);

            var erro = Node("State_Error", raiz.transform); Stretch(erro);
            Contorno(erro.transform, Red, 5f);
            var eBg = Card("Bg", erro.transform, "Deep"); Stretch(eBg.gameObject);
            eBg.color = VermelhoFundo;
            erro.SetActive(false);

            var ch = Display("Caractere", raiz.transform, "K", 56, Cream);
            Stretch(ch.gameObject, 6, 6, 6, 6);

            SalvarItem(raiz, "Item_CodeBox");
        }
    }
}
