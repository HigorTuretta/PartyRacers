using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Gera os prefabs de Prefabs/UI/Widgets/ da direção PLACA.
    /// Ferramenta de editor: o resultado são prefabs reais, editáveis à mão.
    /// </summary>
    public static class BuildWidgets
    {
        const string DIR = "Assets/_Projeto/Prefabs/UI/Widgets";

        [MenuItem("Party Racers/UI/1 - Gerar Widgets")]
        public static void Gerar()
        {
            GarantirPasta(DIR);
            BotaoDisplay("Btn_Primary", "UI_Button_R22_Green", "UI_Button_R22_Pressed_Green", 92, 32, Ink, "JOGAR");
            BotaoDisplay("Btn_Amber", "UI_Button_R22_Amber", "UI_Button_R22_Pressed_Amber", 84, 30, Ink, "CONFIRMAR");
            BotaoSecundario();
            BotaoDisplay("Btn_Danger", "UI_Button_R22_Red", null, 84, 28, Cream, "SAIR");
            BotaoIcone();
            ChipTab();
            SliderSetting();
            ToggleSetting();
            SelectorOption();
            ToastItem();
            PowerSlot();
            RowStanding();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] widgets gerados em " + DIR);
        }

        // ---------- botões ----------
        static void BotaoDisplay(string nome, string spr, string sprPress, float altura,
                                 float fonte, Color corTexto, string rotulo)
        {
            var raiz = Node(nome, null);
            var img = raiz.AddComponent<Image>();
            img.sprite = Sprite("Frames", spr);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            img.color = Color.white;
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(340, altura));

            var t = Txt("Label", raiz.transform, rotulo, FonteDisplay, fonte, corTexto);
            // o sprite já traz a sombra dura embaixo: o texto sobe um pouco para centrar no corpo
            Stretch(t.gameObject, 24, 14, 24, 4);
            t.alignment = TextAlignmentOptions.Center;

            Botao(img, sprPress != null ? Sprite("Frames", sprPress) : null);
            SalvarPrefab(raiz, $"{DIR}/{nome}.prefab");
        }

        static void BotaoSecundario()
        {
            var raiz = Node("Btn_Secondary", null);
            var img = raiz.AddComponent<Image>();
            img.sprite = Sprite("Frames", "UI_Button_R22_Deep");
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(300, 80));

            var t = Txt("Label", raiz.transform, "VOLTAR", FonteUiExtra, 25, Cream);
            t.characterSpacing = 6f;   // .06em
            Stretch(t.gameObject, 20, 14, 20, 4);

            Botao(img, null);
            SalvarPrefab(raiz, $"{DIR}/Btn_Secondary.prefab");
        }

        static void BotaoIcone()
        {
            var raiz = Node("Btn_Icon", null);
            var img = raiz.AddComponent<Image>();
            img.sprite = Sprite("Frames", "UI_Button_R22_Deep");
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(64, 64));

            var ic = Img("Icon", raiz.transform, Sprite("Icons", "Icon_Gear"), Cream, Image.Type.Simple);
            Place(ic.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  new Vector2(0, 2), new Vector2(32, 32));

            Botao(img, null);
            SalvarPrefab(raiz, $"{DIR}/Btn_Icon.prefab");
        }

        // ---------- chip de aba (2 estados como irmãos) ----------
        static void ChipTab()
        {
            var raiz = Node("Chip_Tab", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(220, 64));
            raiz.AddComponent<Image>().color = new Color(0, 0, 0, 0);   // área de clique

            var idle = Node("State_Idle", raiz.transform);
            Stretch(idle);
            var iBg = Img("Bg", idle.transform, Sprite("Frames", "UI_Card_R18_Deep"), Color.white);
            Stretch(iBg.gameObject);
            var iTx = Txt("Label", idle.transform, "ABA", FonteUiBold, 25, TextMuted);
            Stretch(iTx.gameObject, 16, 6, 16, 6);

            var act = Node("State_Active", raiz.transform);
            Stretch(act);
            var aBg = Img("Bg", act.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white);
            Stretch(aBg.gameObject);
            var aTx = Txt("Label", act.transform, "ABA", FonteUiExtra, 25, Ink);
            Stretch(aTx.gameObject, 16, 6, 16, 6);

            Botao(raiz.GetComponent<Image>(), null);
            act.SetActive(false);
            SalvarPrefab(raiz, $"{DIR}/Chip_Tab.prefab");
        }

        // ---------- slider ----------
        static void SliderSetting()
        {
            var raiz = Node("Slider_Setting", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(520, 48));

            var area = Node("Area", raiz.transform);
            Place(area, new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, .5f),
                  new Vector2(0, 0), new Vector2(-96, 24));
            var rtArea = RT(area); rtArea.anchorMin = new Vector2(0, .5f); rtArea.anchorMax = new Vector2(1, .5f);
            rtArea.offsetMin = new Vector2(0, -12); rtArea.offsetMax = new Vector2(-96, 12);

            var slider = area.AddComponent<Slider>();
            var track = Img("Track", area.transform, Sprite("Bars", "Bar_Track"), Color.white);
            Stretch(track.gameObject);

            var fillArea = Node("Fill Area", area.transform);
            Stretch(fillArea, 4, 0, 4, 0);
            var fill = Img("Fill", fillArea.transform, Sprite("Bars", "Bar_Fill"), Color.white);
            Stretch(fill.gameObject);

            var handleArea = Node("Handle Slide Area", area.transform);
            Stretch(handleArea, 12, 0, 12, 0);
            var handle = Img("Handle", handleArea.transform, Sprite("Frames", "UI_Card_R18_Cream"), Color.white);
            Place(handle.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(30, 32));
            handle.raycastTarget = true;

            slider.fillRect = RT(fill.gameObject);
            slider.handleRect = RT(handle.gameObject);
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0; slider.maxValue = 100; slider.value = 70; slider.wholeNumbers = true;

            // valor numérico obrigatório à direita
            var val = Txt("Valor", raiz.transform, "70", FonteMono, 25, Cream, TextAlignmentOptions.Right);
            Place(val.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                  new Vector2(0, 0), new Vector2(84, 40));

            SalvarPrefab(raiz, $"{DIR}/Slider_Setting.prefab");
        }

        // ---------- toggle (On/Off como irmãos) ----------
        static void ToggleSetting()
        {
            var raiz = Node("Toggle_Setting", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(96, 48));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Deep");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;

            var off = Node("Off", raiz.transform); Stretch(off);
            var offKnob = Img("Knob", off.transform, Sprite("Frames", "UI_Card_R18_Cream"), Color.white);
            Place(offKnob.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(6, 0), new Vector2(36, 36));

            var on = Node("On", raiz.transform); Stretch(on);
            var onBg = Img("Bg", on.transform, Sprite("Frames", "UI_Card_R18_Deep"), Green);
            Stretch(onBg.gameObject);
            var onKnob = Img("Knob", on.transform, Sprite("Frames", "UI_Card_R18_Cream"), Color.white);
            Place(onKnob.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                  new Vector2(-6, 0), new Vector2(36, 36));

            var tg = raiz.AddComponent<Toggle>();
            tg.targetGraphic = bg;
            tg.graphic = null;
            tg.isOn = false;
            on.SetActive(false);

            SalvarPrefab(raiz, $"{DIR}/Toggle_Setting.prefab");
        }

        // ---------- seletor ----------
        static void SelectorOption()
        {
            var raiz = Node("Selector_Option", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(420, 64));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Royal");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;

            var esq = Img("Btn_Prev", raiz.transform, Sprite("Icons", "Icon_ArrowLeft"), Cream, Image.Type.Simple);
            Place(esq.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(14, 0), new Vector2(32, 32));
            Botao(esq, null);

            var dir = Img("Btn_Next", raiz.transform, Sprite("Icons", "Icon_ArrowRight"), Cream, Image.Type.Simple);
            Place(dir.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                  new Vector2(-14, 0), new Vector2(32, 32));
            Botao(dir, null);

            var lbl = Txt("Valor", raiz.transform, "ALTO", FonteUiBold, 25, Cream);
            Stretch(lbl.gameObject, 60, 8, 60, 8);

            SalvarPrefab(raiz, $"{DIR}/Selector_Option.prefab");
        }

        // ---------- toast ----------
        static void ToastItem()
        {
            var raiz = Node("Toast_Item", null);
            Place(raiz, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0),
                  Vector2.zero, new Vector2(340, 44));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Race", "Toast_Card");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            raiz.AddComponent<CanvasGroup>();   // animação de entrada/saída vive aqui

            var ic = Img("Icon", raiz.transform, Sprite("Icons", "Icon_Flag"), Amber, Image.Type.Simple);
            Place(ic.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(13, 0), new Vector2(24, 24));

            var t = Txt("Label", raiz.transform, "MENSAGEM", FonteUiBold, 19, Cream, TextAlignmentOptions.Left);
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            var rt = RT(t.gameObject);
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(45, 4); rt.offsetMax = new Vector2(-14, -4);

            SalvarPrefab(raiz, $"{DIR}/Toast_Item.prefab");
        }

        // ---------- slot de poder (4 estados irmãos) ----------
        static void PowerSlot()
        {
            var raiz = Node("PowerSlot", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(128, 128));

            // Empty — moldura tracejada
            var vazio = Node("Empty", raiz.transform); Stretch(vazio);
            var vBg = Img("Bg", vazio.transform, Sprite("Frames", "UI_Dashed_R18"), OutlineSoft);
            Stretch(vBg.gameObject);

            // Filled — card ink + ícone colorido
            var cheio = Node("Filled", raiz.transform); Stretch(cheio);
            var cBg = Img("Bg", cheio.transform, Sprite("Frames", "UI_Card_R18_Ink"), Color.white);
            Stretch(cBg.gameObject);
            var cIc = Img("Icon", cheio.transform, Sprite("Powers", "Power_Rocket_Color"), Color.white, Image.Type.Simple);
            Stretch(cIc.gameObject, 16, 16, 16, 16);

            // Recharging — ícone mono + máscara radial por cima
            var rec = Node("Recharging", raiz.transform); Stretch(rec);
            var rBg = Img("Bg", rec.transform, Sprite("Frames", "UI_Card_R18_Ink"), Color.white);
            Stretch(rBg.gameObject);
            var rIc = Img("Icon", rec.transform, Sprite("Powers", "Power_Rocket_Mono"), Color.white, Image.Type.Simple);
            Stretch(rIc.gameObject, 16, 16, 16, 16);
            var mask = Img("FillMask", rec.transform, Sprite("Frames", "UI_Card_R18_Ink"), new Color(0, 0, 0, .55f));
            Stretch(mask.gameObject);
            mask.type = Image.Type.Filled;
            mask.fillMethod = Image.FillMethod.Radial360;
            mask.fillOrigin = (int)Image.Origin360.Top;
            mask.fillClockwise = false;
            mask.fillAmount = 0.6f;

            // Locked — ícone cinza + cadeado
            var lock_ = Node("Locked", raiz.transform); Stretch(lock_);
            var lBg = Img("Bg", lock_.transform, Sprite("Frames", "UI_Card_R18_Ink"), new Color(1, 1, 1, .55f));
            Stretch(lBg.gameObject);
            var lIc = Img("Icon", lock_.transform, Sprite("Powers", "Power_Rocket_Gray"), Color.white, Image.Type.Simple);
            Stretch(lIc.gameObject, 16, 16, 16, 16);
            var cad = Img("Lock", lock_.transform, Sprite("Icons", "Icon_Lock"), TextDisabled, Image.Type.Simple);
            Place(cad.gameObject, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(40, 40));

            cheio.SetActive(false); rec.SetActive(false); lock_.SetActive(false);
            SalvarPrefab(raiz, $"{DIR}/PowerSlot.prefab");
        }

        // ---------- linha de classificação ----------
        static void RowStanding()
        {
            // Métricas compactas: a linha vive só na HUD de corrida, onde o gameplay tem
            // prioridade sobre a leitura. 340×46 em vez de 490×66 devolve ~9% da largura da tela.
            var raiz = Node("Row_Standing", null);
            Place(raiz, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                  Vector2.zero, new Vector2(340, 46));
            var bg = raiz.AddComponent<Image>();
            bg.sprite = Sprite("Frames", "UI_Card_R18_Deep");
            bg.type = Image.Type.Sliced; bg.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            bg.raycastTarget = false;

            // destaque do jogador local: corpo royal + contorno âmbar (irmão de estado, não cor por código)
            var local = Node("State_IsLocal", raiz.transform); Stretch(local);
            UIKitPlaca.Contorno(local.transform, Amber, 5f);
            var lBg = Img("Bg", local.transform, Sprite("Frames", "UI_Card_R18_Royal"), Color.white);
            Stretch(lBg.gameObject);
            local.SetActive(false);

            // um badge por medalha — o binder só liga o certo, nunca pinta por código
            BadgeLugar(raiz.transform, "Badge_Ouro", Amber, Ink, true);
            BadgeLugar(raiz.transform, "Badge_Prata", Hex("#D7DEEA"), Ink, false);
            BadgeLugar(raiz.transform, "Badge_Bronze", Hex("#C57C3C"), Ink, false);
            BadgeLugar(raiz.transform, "Badge_Comum", TextDisabled, Cream, false);
            BadgeLugar(raiz.transform, "Badge_Local", Cream, Ink, false);

            var nome = Txt("Nome", raiz.transform, "JOGADOR", FonteUiExtra, 21, Cream, TextAlignmentOptions.Left);
            nome.characterSpacing = 1f;
            nome.enableWordWrapping = false;
            nome.overflowMode = TextOverflowModes.Ellipsis;   // nome longo corta, não invade o tempo
            var rtN = RT(nome.gameObject);
            rtN.anchorMin = new Vector2(0, 0); rtN.anchorMax = new Vector2(1, 1);
            rtN.offsetMin = new Vector2(54, 4); rtN.offsetMax = new Vector2(-102, -4);

            var tempo = UIKitPlaca.Numero("Tempo", raiz.transform, "1:02.44", 17, TextMuted);
            tempo.font = FonteUiSemi;
            Place(tempo.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f),
                  new Vector2(-14, 0), new Vector2(94, 28));

            SalvarPrefab(raiz, $"{DIR}/Row_Standing.prefab");
        }

        /// <summary>Badge quadrado rotacionado −3° com o número do lugar (vocabulário adesivo do PLACA).</summary>
        static void BadgeLugar(Transform pai, string nome, Color fundo, Color corTexto, bool ativo)
        {
            // Simple, não Sliced: o badge é quadrado 1:1, então escalar mantém o raio proporcional.
            // Com 9-slice a borda de 36px seria maior que metade dos 50px e o quadrado viraria círculo.
            var bg = Img(nome, pai, Sprite("Frames", "UI_Badge_R14_Cream"), fundo, Image.Type.Simple);
            bg.type = Image.Type.Simple;
            Place(bg.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f),
                  new Vector2(7, 0), new Vector2(36, 36));
            bg.transform.localRotation = Quaternion.Euler(0, 0, -3f);
            var t = Txt("Valor", bg.transform, "1", FonteUiExtra, 19, corTexto);
            Stretch(t.gameObject, 2, 2, 2, 2);
            bg.gameObject.SetActive(ativo);
        }
    }
}
