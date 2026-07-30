using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;
using static PartyRacers.UI.EditorTools.UIKitPlaca;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Telas 01, 02 e 12 do PLACA (cena Race). Só desktop 1920×1080.
    /// </summary>
    public static class BuildScreensRace
    {
        [MenuItem("Party Racers/UI/3 - Gerar Telas de Corrida")]
        public static void Gerar()
        {
            GarantirPasta(SCREENS);
            RaceHUD();
            Countdown();
            RaceMenu();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] telas de corrida geradas em " + SCREENS);
        }

        // ═══════════ 01 · CORRIDA · COMPUTADOR ═══════════
        static void RaceHUD()
        {
            var raiz = Tela("Screen_RaceHUD_PC");

            // ---- overlays de perigo (só o arco na borda; sem texto, sem seta) ----
            var over = Node("Overlays", raiz.transform); Stretch(over);
            over.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var arco = Img("Overlay_DangerArc", over.transform, Sprite("Race", "Overlay_DangerArc"), Color.white);
            Stretch(arco.gameObject); arco.gameObject.SetActive(false);
            var arcoF = Img("Overlay_DangerArc_Strong", over.transform, Sprite("Race", "Overlay_DangerArc_Strong"), Color.white);
            Stretch(arcoF.gameObject); arcoF.gameObject.SetActive(false);
            var pulso = Img("Overlay_DangerPulse", over.transform, Sprite("Race", "Overlay_DangerPulse"), Color.white, Image.Type.Simple);
            Place(pulso.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 280));
            var rtP = RT(pulso.gameObject);
            rtP.anchorMin = new Vector2(0, 0); rtP.anchorMax = new Vector2(1, 0);
            rtP.offsetMin = new Vector2(0, 0); rtP.offsetMax = new Vector2(0, 280);
            pulso.gameObject.SetActive(false);

            // ---- classificação (esquerda) ----
            // Compacta: 340 de largura contra 490, e linhas de 46 contra 66. O bloco inteiro
            // cai de 490×500 (25% × 46% da tela) para 340×324 (18% × 30%), sem perder as 5
            // posições que o PLACA mostra.
            var clas = Node("Standings", raiz.transform);
            Place(clas, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -44), new Vector2(340, 324));
            VLayout(clas, 10, new RectOffset(), TextAnchor.UpperLeft);

            var lista = Node("Container", clas.transform);
            Size(lista, 340, 254);
            VLayout(lista, 6, new RectOffset(), TextAnchor.UpperLeft);
            var medalhas = new[] { "Badge_Ouro", "Badge_Prata", "Badge_Bronze", "Badge_Comum", "Badge_Comum" };
            for (int i = 0; i < 5; i++)
            {
                var linha = Widget("Row_Standing", lista.transform);
                linha.name = "Row_" + (i + 1);
                Size(linha, 340, 46);
                LigarBadge(linha, medalhas[i], (i + 1).ToString());
            }

            var grupoLocal = Node("Local", clas.transform);
            Size(grupoLocal, 340, 78);
            VLayout(grupoLocal, 4, new RectOffset(4, 0, 0, 0), TextAnchor.UpperLeft);
            var lbl = Legenda("Rotulo", grupoLocal.transform, "SUA POSIÇÃO", 13, new Color(1, .97f, .91f, .78f),
                              TextAlignmentOptions.Left, 14f);
            Size(lbl.gameObject, 336, 16);

            var local = Widget("Row_Standing", grupoLocal.transform);
            local.name = "Row_Local";
            Size(local, 336, 54);
            local.transform.Find("State_IsLocal").gameObject.SetActive(true);
            LigarBadge(local, "Badge_Local", "9");
            var nomeLocal = local.transform.Find("Nome").GetComponent<TextMeshProUGUI>();
            nomeLocal.text = "VOCÊ"; nomeLocal.fontSize = 24;
            local.transform.Find("Tempo").GetComponent<TextMeshProUGUI>().color = CreamDim;

            // ---- placa de volta + tempo (topo-centro) ----
            // 420×108 contra 560×150: continua legível de relance e libera o topo da pista.
            var topo = Node("LapPlate", raiz.transform);
            Place(topo, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -44), new Vector2(420, 108));
            VLayout(topo, 8, new RectOffset(), TextAnchor.UpperCenter);

            var placa = Card("Placa", topo.transform, "Ink", 6f);
            Size(placa.gameObject, 420, 64);
            var faixa = Img("FaixaVolta", placa.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white);
            Place(faixa.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(6, 0), new Vector2(168, 52));
            var voltaTx = Rotulo("Volta", faixa.transform, "VOLTA 2/3", 22, Ink, TextAlignmentOptions.Center, 2f);
            Stretch(voltaTx.gameObject, 8, 5, 8, 5);
            var tempo = Numero("Tempo", placa.transform, "01:12.480", 34, Cream, TextAlignmentOptions.Center);
            Place(tempo.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(226, 44));

            var chips = Node("Chips", topo.transform);
            Size(chips, 420, 32);
            HLayout(chips, 8, new RectOffset(), TextAnchor.MiddleCenter);
            var cU = Chip("Chip_UltimaVolta", chips.transform, "ÚLT 01:14.902", 158, 30, DeepBlue, TextMuted, 17, FonteUiSemi);
            Size(cU, 158, 30);
            var cM = Chip("Chip_MelhorVolta", chips.transform, "MELH 01:11.302", 164, 30, Green, Ink, 17, FonteUiBold);
            Size(cM, 164, 30);
            var cF = Chip("Chip_UltimaVoltaAviso", chips.transform, "ÚLTIMA VOLTA", 148, 30, Red, Ink, 17);
            Size(cF, 148, 30);
            cF.SetActive(false);

            // ---- poder (direita) ----
            // 150×272 contra 220×420. O slot principal cai para 116 — ainda maior que os
            // extras, então continua óbvio qual é o poder que o ESPAÇO usa.
            var pod = Node("PowerArea", raiz.transform);
            Place(pod, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -44), new Vector2(150, 272));
            VLayout(pod, 10, new RectOffset(), TextAnchor.UpperCenter);

            var slot = Widget("PowerSlot", pod.transform);
            slot.name = "PowerSlot_Principal";
            Size(slot, 116, 116);
            slot.transform.Find("Empty").gameObject.SetActive(false);
            slot.transform.Find("Filled").gameObject.SetActive(true);

            var nomePoder = Chip("NomePoder", pod.transform, "FOGUETE", 150, 34, DeepBlue, Cream, 18);
            Size(nomePoder, 150, 34);
            var tecla = Chip("Tecla", pod.transform, "ESPAÇO", 104, 28, Cream, DeepBlue, 15);
            Size(tecla, 104, 28);

            var extras = Node("SlotsExtras", pod.transform);
            Size(extras, 150, 70);
            HLayout(extras, 10, new RectOffset(0, 0, 8, 0), TextAnchor.UpperCenter);
            for (int i = 0; i < 2; i++)
            {
                var col = Node("Extra_" + (i + 1), extras.transform);
                Size(col, 56, 70);
                VLayout(col, 5, new RectOffset(), TextAnchor.UpperCenter);
                var mini = Widget("PowerSlot", col.transform);
                mini.name = "Slot";
                Size(mini, 56, 56);
                mini.transform.Find("Empty").gameObject.SetActive(false);
                mini.transform.Find(i == 0 ? "Filled" : "Recharging").gameObject.SetActive(true);
                var fill = Barra("Recarga", col.transform, 56, 8, i == 0 ? Green : Red, i == 0 ? .62f : .28f);
                Size(fill.transform.parent.gameObject, 56, 8);
            }

            // ---- avisos discretos (canto inferior esquerdo, máx. 3) ----
            var toasts = Node("Toasts", raiz.transform);
            Place(toasts, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(56, 56), new Vector2(340, 148));
            var v = VLayout(toasts, 7, new RectOffset(), TextAnchor.LowerLeft);
            v.reverseArrangement = true;
            for (int i = 0; i < 3; i++)
            {
                var t = Widget("Toast_Item", toasts.transform);
                t.name = "Toast_" + (i + 1);
                Size(t, 340, 44);
                var cg = t.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = i == 0 ? 1f : (i == 1 ? .82f : .55f);
            }

            SalvarTela(raiz, "Screen_RaceHUD_PC");
        }

        // ═══════════ 02 · CONTAGEM REGRESSIVA ═══════════
        static void Countdown()
        {
            var raiz = Tela("Screen_Countdown");

            var centro = Node("Centro", raiz.transform);
            Place(centro, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(600, 300));

            // um irmão por passo — o binder só liga/desliga, não pinta nada
            Passo(centro.transform, "State_3", "3", 200, Cream, Ink, -4f, 150);
            Passo(centro.transform, "State_2", "2", 200, Sky, Cream, 3f, 150);
            Passo(centro.transform, "State_1", "1", 200, Amber, Ink, -3f, 150);
            Passo(centro.transform, "State_Go", "VAI!", 420, Green, Ink, -5f, 116);

            foreach (Transform t in centro.transform) t.gameObject.SetActive(false);
            centro.transform.Find("State_3").gameObject.SetActive(true);

            SalvarTela(raiz, "Screen_Countdown");
        }

        static void Passo(Transform pai, string nome, string txt, float largura, Color corPlaca, Color corTexto,
                          float angulo, float fonte)
        {
            var go = Node(nome, pai);
            Place(go, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(largura, 200));
            go.transform.localRotation = Quaternion.Euler(0, 0, angulo);
            var placa = Img("Placa", go.transform, Sprite("Brand", "Countdown_Plate"), corPlaca, Image.Type.Simple);
            Stretch(placa.gameObject);
            var t = Display("Digito", go.transform, txt, fonte, corTexto);
            Stretch(t.gameObject, 10, 16, 10, 6);
        }

        // ═══════════ 12 · MENU DA PARTIDA (gaveta, a corrida não para) ═══════════
        static void RaceMenu()
        {
            var raiz = Tela("Screen_RaceMenu");

            // escurecimento só à direita: o gameplay continua legível à esquerda
            var veu = Img("Veu", raiz.transform, null, new Color(.04f, .05f, .13f, .55f), Image.Type.Simple);
            Stretch(veu.gameObject);
            veu.raycastTarget = true;

            // ---- gaveta lateral ----
            var gav = Node("Gaveta", raiz.transform);
            Place(gav, new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, .5f), Vector2.zero, new Vector2(660, 0));
            var rtG = RT(gav);
            rtG.anchorMin = new Vector2(1, 0); rtG.anchorMax = new Vector2(1, 1);
            rtG.pivot = new Vector2(1, .5f);
            rtG.offsetMin = new Vector2(-660, 0); rtG.offsetMax = new Vector2(0, 0);
            var gBg = Img("Bg", gav.transform, Sprite("Frames", "UI_Panel_R26_Ink"), Color.white);
            Stretch(gBg.gameObject, 0, -30, -30, -30);

            var tit = Display("Titulo", gav.transform, "MENU", 46, Cream, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -52), new Vector2(300, 56));

            // sem Image na raiz: o contorno precisa desenhar ATRÁS do corpo, e filho sempre vem na frente
            var aoVivo = Node("Chip_AoVivo", gav.transform);
            Place(aoVivo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-46, -52), new Vector2(180, 48));
            Contorno(aoVivo.transform, Red, 4f);
            var avBg = Card("Bg", aoVivo.transform, "Deep"); Stretch(avBg.gameObject);
            avBg.color = VermelhoFundo;
            var ponto = Icone("Ponto", aoVivo.transform, "Icon_Circle", Red, 14);
            Place(ponto.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(14, 14));
            var avTx = Rotulo("Label", aoVivo.transform, "AO VIVO", 20, RosaClaro, TextAlignmentOptions.Center, 10f);
            Stretch(avTx.gameObject, 30, 4, 10, 4);

            var aviso = Rotulo("Aviso", gav.transform,
                "Seu carro continua correndo no piloto automático enquanto o menu está aberto. Sem reiniciar: a partida é online.",
                21, TextMuted);
            Place(aviso.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -128), new Vector2(-92, 76));
            var rtA = RT(aviso.gameObject);
            rtA.anchorMin = new Vector2(0, 1); rtA.anchorMax = new Vector2(1, 1);
            rtA.offsetMin = new Vector2(46, -204); rtA.offsetMax = new Vector2(-46, -128);
            aviso.textWrappingMode = TextWrappingModes.Normal;
            aviso.font = FonteUiSemi;

            // ---- ações (sem REINICIAR, é decisão de design fechada) ----
            var acoes = Node("Acoes", gav.transform);
            Place(acoes, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 356));
            var rtAc = RT(acoes);
            rtAc.anchorMin = new Vector2(0, 1); rtAc.anchorMax = new Vector2(1, 1);
            rtAc.offsetMin = new Vector2(46, -572); rtAc.offsetMax = new Vector2(-46, -216);
            VLayout(acoes, 12, new RectOffset(), TextAnchor.UpperCenter, true, false);

            BotaoGaveta(acoes.transform, "Btn_Voltar", "VOLTAR À CORRIDA", "Green", 92, 32, Ink, true);
            BotaoGaveta(acoes.transform, "Btn_Configuracoes", "CONFIGURAÇÕES", "Deep", 80, 25, Cream, false, Sky);
            BotaoGaveta(acoes.transform, "Btn_CopiarCodigo", "COPIAR CÓDIGO DA SALA", "Deep", 80, 25, Cream, false);
            BotaoGaveta(acoes.transform, "Btn_Sair", "SAIR DA PARTIDA", "Deep", 80, 25, Hex("#FF8DA0"), false);

            // ---- ajuste rápido (mesmos widgets da tela de configurações) ----
            var rapido = Node("AjusteRapido", gav.transform);
            Place(rapido, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 230));
            var rtR = RT(rapido);
            rtR.anchorMin = new Vector2(0, 1); rtR.anchorMax = new Vector2(1, 1);
            rtR.offsetMin = new Vector2(46, -822); rtR.offsetMax = new Vector2(-46, -592);
            VLayout(rapido, 16, new RectOffset(), TextAnchor.UpperLeft);

            var rl = Legenda("Rotulo", rapido.transform, "AJUSTE RÁPIDO", 16, Hex("#5C63A8"), TextAlignmentOptions.Left, 20f);
            Size(rl.gameObject, 560, 20);
            LinhaRapida(rapido.transform, "Musica", "MÚSICA", true);
            LinhaRapida(rapido.transform, "Efeitos", "EFEITOS", true);
            LinhaRapida(rapido.transform, "Vibracao", "VIBRAÇÃO", false);

            // ---- rodapé: ESC + ping ----
            var rod = Node("Rodape", gav.transform);
            Place(rod, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 48));
            var rtRo = RT(rod);
            rtRo.anchorMin = new Vector2(0, 0); rtRo.anchorMax = new Vector2(1, 0);
            rtRo.offsetMin = new Vector2(46, 46); rtRo.offsetMax = new Vector2(-46, 94);

            var esc = Chip("Tecla_Esc", rod.transform, "ESC", 76, 48, Cream, Ink, 21);
            Place(esc, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(76, 48));
            var escTx = Rotulo("Dica", rod.transform, "fecha e volta na hora", 20, TextMuted);
            Place(escTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(90, 0), new Vector2(300, 26));
            escTx.font = FonteUiBold;

            var ping = Card("Ping", rod.transform, "Deep");
            Place(ping.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(150, 48));
            var pIc = Icone("Icon", ping.transform, "Icon_Signal", Green, 24);
            Place(pIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(14, 0), new Vector2(24, 24));
            var pTx = Numero("Valor", ping.transform, "28 ms", 20, Cream, TextAlignmentOptions.Right);
            Place(pTx.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(90, 26));

            // ---- HUD reduzido: volta e posição seguem visíveis à esquerda ----
            // Sem cópia reduzida da HUD: o handoff §5 pede "a esquerda continua mostrando volta e
            // posição com opacidade reduzida", e a HUD real já está lá. Desenhar uma segunda placa
            // por cima da primeira só duplicava informação e ocupava tela — quem esmaece a HUD
            // verdadeira agora é o RaceMenuUI, pelo CanvasGroup dela.

            // ---- confirmação de saída (avisa que a corrida continua sem o jogador) ----
            var pop = Node("Popup_Sair", raiz.transform);
            Place(pop, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(120, 0), new Vector2(560, 470));
            var pBg = Modal("Bg", pop.transform, 12f); Stretch(pBg.gameObject);
            var risco = Img("Risco", pop.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Red);
            Place(risco.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(44, 5), new Vector2(160, 10));

            var pIcAviso = Icone("Icon", pop.transform, "Icon_Triangle", Red, 30);
            Place(pIcAviso.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(48, -72), new Vector2(30, 30));
            var pTit = Display("Titulo", pop.transform, "SAIR DA PARTIDA?", 32, Cream, TextAlignmentOptions.Left);
            Place(pTit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(92, -72), new Vector2(420, 40));

            var pTxt = Rotulo("Texto", pop.transform,
                "A corrida continua sem você. Você perde a posição, o XP e as recompensas desta partida — e volta para o lobby.",
                22, TextMuted);
            var rtT = RT(pTxt.gameObject);
            rtT.anchorMin = new Vector2(0, 1); rtT.anchorMax = new Vector2(1, 1);
            rtT.offsetMin = new Vector2(44, -252); rtT.offsetMax = new Vector2(-44, -128);
            pTxt.textWrappingMode = TextWrappingModes.Normal; pTxt.font = FonteUiSemi;
            pTxt.alignment = TextAlignmentOptions.TopLeft;

            var pBtns = Node("Botoes", pop.transform);
            var rtB = RT(pBtns);
            rtB.anchorMin = new Vector2(0, 0); rtB.anchorMax = new Vector2(1, 0);
            rtB.offsetMin = new Vector2(44, 44); rtB.offsetMax = new Vector2(-44, 214);
            VLayout(pBtns, 12, new RectOffset(), TextAnchor.UpperCenter, true, false);
            BotaoGaveta(pBtns.transform, "Btn_SairAgora", "SAIR AGORA", "Red", 84, 28, Ink, true);
            BotaoGaveta(pBtns.transform, "Btn_Ficar", "FICAR NA CORRIDA", "Deep", 74, 24, Cream, false);

            pop.SetActive(false);

            SalvarTela(raiz, "Screen_RaceMenu");
        }

        /// <summary>Deixa ativo só o badge de medalha pedido e escreve o número dentro dele.</summary>
        static void LigarBadge(GameObject linha, string badge, string numero)
        {
            foreach (var b in new[] { "Badge_Ouro", "Badge_Prata", "Badge_Bronze", "Badge_Comum", "Badge_Local" })
            {
                var t = linha.transform.Find(b);
                if (t == null) continue;
                t.gameObject.SetActive(b == badge);
                if (b == badge) t.Find("Valor").GetComponent<TextMeshProUGUI>().text = numero;
            }
        }

        static void BotaoGaveta(Transform pai, string nome, string txt, string variante, float altura,
                                float fonte, Color cor, bool display, Color? contorno = null)
        {
            var go = Node(nome, pai);
            var img = go.AddComponent<Image>();
            img.sprite = Sprite("Frames", $"UI_Button_R22_{variante}");
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            Size(go, 560, altura);
            if (contorno.HasValue) Contorno(go.transform, contorno.Value, 5f, "Deep");
            var t = Txt("Label", go.transform, txt, display ? FonteDisplay : FonteUiExtra, fonte, cor);
            if (!display) t.characterSpacing = 6f;
            Stretch(t.gameObject, 20, 14, 20, 4);
            var pressed = variante == "Green" ? Sprite("Frames", "UI_Button_R22_Pressed_Green")
                        : variante == "Amber" ? Sprite("Frames", "UI_Button_R22_Pressed_Amber") : null;
            Botao(img, pressed);
        }

        static void LinhaRapida(Transform pai, string nome, string rotulo, bool slider)
        {
            var linha = Node(nome, pai);
            Size(linha, 560, 52);
            var lbl = Legenda("Rotulo", linha.transform, rotulo, 20, TextMuted, TextAlignmentOptions.Left, 10f);
            Place(lbl.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(140, 26));

            if (slider)
            {
                var s = Widget("Slider_Setting", linha.transform);
                s.name = "Slider";
                Place(s, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(400, 48));
            }
            else
            {
                var t = Widget("Toggle_Setting", linha.transform);
                t.name = "Toggle";
                Place(t, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(96, 48));
            }
        }
    }
}
