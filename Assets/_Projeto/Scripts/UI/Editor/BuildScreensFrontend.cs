using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using static PartyRacers.UI.EditorTools.UIKit;
using static PartyRacers.UI.EditorTools.UIKitPlaca;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Telas 04, 06, 07, 10 e 11 do PLACA (cena Frontend). Só desktop 1920×1080.
    /// </summary>
    public static class BuildScreensFrontend
    {
        [MenuItem("Party Racers/UI/4 - Gerar Telas de Frontend")]
        public static void Gerar()
        {
            GarantirPasta(SCREENS);
            Lobby();
            Garage();
            JoinCode();
            Settings();
            Result();
            LobbyMapBuilder.Montar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] telas de frontend geradas em " + SCREENS);
        }

        // ═══════════ 06 · LOBBY (primeira tela do jogo) ═══════════
        static void Lobby()
        {
            var raiz = Tela("Screen_Lobby");
            // Sem fundo opaco: o carro 3D do palco fica atrás desta tela (o PLACA mostra o carro
            // em modo somente visualização). Quem pinta o fundo é a câmera da cena.
            FundoVazado(raiz.transform);

            // palco do carro: só visualização, edita na garagem
            var palco = Node("PalcoCarro", raiz.transform);
            Place(palco, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-42, -112), new Vector2(720, 440));
            var selo = Node("Selo", palco.transform);
            Place(selo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), new Vector2(430, 48));
            var sBg = Card("Bg", selo.transform, "Ink"); Stretch(sBg.gameObject);
            var sIc = Icone("Icon", selo.transform, "Icon_Lock", TextDisabled, 20);
            Place(sIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(20, 20));
            var sTx = Rotulo("Label", selo.transform, "SEU KART · EDITE NA GARAGEM", 18, TextMuted,
                             TextAlignmentOptions.Center, 3f);
            Stretch(sTx.gameObject, 44, 4, 14, 4);

            Logo(raiz.transform).GetComponent<RectTransform>().anchoredPosition = new Vector2(56, -44);
            Nav(raiz.transform, 0);

            // 1080 − 152 (topo) − 56 (margem base) = 872 de altura útil; o conteúdo abaixo soma 866
            var col = Node("Conteudo", raiz.transform);
            Place(col, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -152), new Vector2(1060, 872));
            VLayout(col, 18, new RectOffset(), TextAnchor.UpperLeft);

            // ---- código da sala + contagem ----
            var topo = Node("Topo", col.transform);
            Size(topo, 1060, 100);
            HLayout(topo, 12, new RectOffset(), TextAnchor.MiddleLeft, false, true);

            var cod = Card("CodigoSala", topo.transform, "Ink", 7f);
            Size(cod.gameObject, 800, 100);
            var codLbl = Legenda("Rotulo", cod.transform, "CÓDIGO DA SALA", 18, Lavanda, TextAlignmentOptions.Left, 14f);
            Place(codLbl.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(24, 0), new Vector2(200, 26));
            var codVal = Display("Codigo", cod.transform, "SEM SALA", 34, Amber, TextAlignmentOptions.Left);
            codVal.characterSpacing = 2f;
            Place(codVal.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(238, 0), new Vector2(276, 52));
            var btnCopiar = Node("Btn_Copiar", cod.transform);
            // 268 de largura: ícone (16+22) + rótulo de 14 caracteres a 19pt sem entreletra
            // extra. Com 232 e espaçamento 4 o texto estourava a caixa e passava por cima do ícone.
            Place(btnCopiar, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(268, 58));
            var bcImg = Img("Bg", btnCopiar.transform, Sprite("Frames", "UI_Card_R18_Royal"), Color.white);
            Stretch(bcImg.gameObject); Botao(bcImg, null);
            var bcIc = Icone("Icon", btnCopiar.transform, "Icon_Copy", Cream, 22);
            Place(bcIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(22, 22));
            var bcTx = Rotulo("Label", btnCopiar.transform, "CRIAR SALA", 19, Cream, TextAlignmentOptions.Center);
            Stretch(bcTx.gameObject, 48, 4, 12, 4);

            var cont = Card("Contagem", topo.transform, "Ink", 7f);
            Size(cont.gameObject, 248, 100);
            var cVal = Display("Valor", cont.transform, "1", 38, Cream, TextAlignmentOptions.Right);
            Place(cVal.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, 12), new Vector2(46, 44));
            var cMax = Display("Maximo", cont.transform, "/16", 38, Slate, TextAlignmentOptions.Left);
            Place(cMax.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(76, 12), new Vector2(100, 44));
            var cLbl = Legenda("Rotulo", cont.transform, "JOGADORES", 17, Lavanda, TextAlignmentOptions.Left, 14f);
            Place(cLbl.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(28, -24), new Vector2(200, 22));

            // ---- 16 vagas em 2 colunas, sem rolagem (8 linhas de 62 + 7 de espaço = 552) ----
            var grade = Node("Vagas", col.transform);
            Size(grade, 1060, 552);
            Grid(grade, new Vector2(521, 62), new Vector2(18, 8), 2);
            for (int i = 0; i < 16; i++)
            {
                var v = Item("Item_LobbySlot", grade.transform);
                v.name = "Slot_" + (i + 1).ToString("00");
                if (i >= 1) { v.transform.Find("State_Player").gameObject.SetActive(false); v.transform.Find("State_Empty").gameObject.SetActive(true); }
            }

            // ---- aviso + ações ----
            var aviso = Node("Aviso", col.transform);
            Size(aviso, 1060, 72);
            Contorno(aviso.transform, new Color(Sky.r, Sky.g, Sky.b, .5f), 4f);
            var avBg = Card("Bg", aviso.transform, "Deep"); Stretch(avBg.gameObject);
            avBg.color = new Color(Sky.r * .3f, Sky.g * .35f, Sky.b * .5f, 1f);
            var avIc = Icone("Icon", aviso.transform, "Icon_Signal", Sky, 24);
            Place(avIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(22, 0), new Vector2(24, 24));
            var avTx = Rotulo("Texto", aviso.transform, "Crie uma sala online ou entre com o código de um amigo.", 22, AzulClaro);
            Stretch(avTx.gameObject, 58, 6, 22, 6);
            avTx.font = FonteUiBold;

            var acoes = Node("Acoes", col.transform);
            Size(acoes, 1060, 88);
            HLayout(acoes, 14, new RectOffset(), TextAnchor.MiddleLeft, false, true);
            BotaoLargo(acoes.transform, "Btn_EntrarPorCodigo", "ENTRAR POR CÓDIGO", "Deep", 380, 88, 23, Cream, false, Sky);
            BotaoLargo(acoes.transform, "Btn_SairDaSala", "GARAGEM", "Deep", 300, 88, 23, TextMuted, false);
            var estado = Node("EstadoPartida", acoes.transform);
            Size(estado, 352, 88);
            var eBg = Card("Bg", estado.transform, "Ink"); Stretch(eBg.gameObject);
            var eTx = Display("Label", estado.transform, "AGUARDANDO", 30, TextDisabled);
            Stretch(eTx.gameObject, 12, 8, 12, 8);
            var pronto = Node("State_Pronto", acoes.transform);
            Size(pronto, 352, 88);
            var pImg = Img("Bg", pronto.transform, Sprite("Frames", "UI_Button_R22_Green"), Color.white);
            Stretch(pImg.gameObject);
            Botao(pImg, Sprite("Frames", "UI_Button_R22_Pressed_Green"));
            var pTx = Display("Label", pronto.transform, "JOGAR LOCAL", 29, Ink);
            Stretch(pTx.gameObject, 12, 14, 12, 4);
            estado.SetActive(false);
            pronto.SetActive(true);

            SalvarTela(raiz, "Screen_Lobby");
        }

        // ═══════════ 04 · GARAGEM · COMPUTADOR ═══════════
        static void Garage()
        {
            var raiz = Tela("Screen_Garage_PC");
            // Idem Lobby: o palco do carro precisa aparecer no miolo da tela.
            FundoVazado(raiz.transform);

            var palco = Node("PalcoCarro", raiz.transform);
            Place(palco, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(0, 22), new Vector2(770, 500));

            Logo(raiz.transform).GetComponent<RectTransform>().anchoredPosition = new Vector2(56, -44);
            Nav(raiz.transform, 1);

            // status de conexão + configurações
            var conn = Node("StatusConexao", raiz.transform);
            Place(conn, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-134, -44), new Vector2(240, 62));
            var cnBg = Card("Bg", conn.transform, "Deep"); Stretch(cnBg.gameObject);
            var cnIc = Icone("Icon", conn.transform, "Icon_Circle", Green, 16);
            Place(cnIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(16, 16));
            var cnTx = Rotulo("Label", conn.transform, "CONECTADO", 21, Cream, TextAlignmentOptions.Center, 6f);
            Stretch(cnTx.gameObject, 40, 4, 12, 4);

            var gear = Widget("Btn_Icon", raiz.transform);
            gear.name = "Btn_Configuracoes";
            Place(gear, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -44), new Vector2(62, 62));

            // seletor de carro (topo-centro)
            var sel = Node("SeletorCarro", raiz.transform);
            Place(sel, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -140), new Vector2(640, 100));
            var setaE = SetaQuadrada(sel.transform, "Btn_Anterior", "Icon_ArrowLeft", new Vector2(0, .5f), new Vector2(0, 0), 76);
            var setaD = SetaQuadrada(sel.transform, "Btn_Proximo", "Icon_ArrowRight", new Vector2(1, .5f), new Vector2(0, 0), 76);
            var nomeCarro = Display("NomeCarro", sel.transform, "CARRO 03", 46, Cream);
            Place(nomeCarro.gameObject, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(420, 52));
            var pontos = Node("Indicadores", sel.transform);
            Place(pontos, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 14), new Vector2(260, 12));
            HLayout(pontos, 8, new RectOffset(), TextAnchor.MiddleCenter);
            for (int i = 0; i < 6; i++)
            {
                var p = Img("Ponto_" + (i + 1), pontos.transform, Sprite("Frames", "UI_Badge_R14_Cream"), i == 2 ? Amber : Slate, Image.Type.Simple);
                Size(p.gameObject, 34, 8);
            }

            // painel de customização (esquerda)
            var custo = Node("PainelCustomizacao", raiz.transform);
            Place(custo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -250), new Vector2(500, 620));
            var cuBg = Painel("Bg", custo.transform, "Ink"); Stretch(cuBg.gameObject);
            var cuTit = Display("Titulo", custo.transform, "CUSTOMIZAÇÃO", 26, Amber, TextAlignmentOptions.Left);
            Place(cuTit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(300, 32));
            var cuQtd = Legenda("Contagem", custo.transform, "11 CATEGORIAS", 17, Lavanda, TextAlignmentOptions.Right, 12f);
            Place(cuQtd.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -26), new Vector2(200, 26));

            var cats = Node("Categorias", custo.transform);
            Place(cats, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 460));
            var rtC = RT(cats);
            rtC.anchorMin = new Vector2(0, 1); rtC.anchorMax = new Vector2(1, 1);
            rtC.offsetMin = new Vector2(26, -538); rtC.offsetMax = new Vector2(-26, -78);
            VLayout(cats, 11, new RectOffset(), TextAnchor.UpperCenter);
            var nomes = new[] { "COR", "RODAS", "FRENTE", "TRASEIRA", "ESCAPE", "ADESIVOS" };
            for (int i = 0; i < nomes.Length; i++)
            {
                var linha = Item("Item_CategoryRow", cats.transform);
                linha.name = "Cat_" + nomes[i];
                Size(linha, 448, 64);
                foreach (var t in linha.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (t.name == "Nome") t.text = nomes[i];
                if (i == 0)
                {
                    linha.transform.Find("State_Idle").gameObject.SetActive(false);
                    linha.transform.Find("State_Selected").gameObject.SetActive(true);
                    linha.transform.Find("Valor").gameObject.SetActive(false);
                    linha.transform.Find("AmostraCor").gameObject.SetActive(true);
                }
                if (i == nomes.Length - 1)
                {
                    linha.transform.Find("State_Idle").gameObject.SetActive(false);
                    linha.transform.Find("State_Locked").gameObject.SetActive(true);
                    linha.transform.Find("Btn_Prev").gameObject.SetActive(false);
                    linha.transform.Find("Btn_Next").gameObject.SetActive(false);
                    linha.transform.Find("Valor").gameObject.SetActive(false);
                }
            }
            var restantes = Anotacao("Restantes", custo.transform, "MILHA, MOTOR, FARÓIS, PILOTO", 15, Hex("#5C63A8"));
            Place(restantes.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 26), new Vector2(400, 22));

            // painel do lobby (direita)
            var lob = Node("PainelLobby", raiz.transform);
            Place(lob, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -250), new Vector2(480, 620));
            var loBg = Painel("Bg", lob.transform, "Ink"); Stretch(loBg.gameObject);
            var loTit = Display("Titulo", lob.transform, "LOBBY", 26, Sky, TextAlignmentOptions.Left);
            Place(loTit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -26), new Vector2(200, 32));
            var loQtd = Numero("Contagem", lob.transform, "4/16", 21, Cream);
            Place(loQtd.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -26), new Vector2(120, 26));

            var loCod = Card("Codigo", lob.transform, "Deep");
            Place(loCod.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), new Vector2(0, -78), new Vector2(-52, 66));
            var rtLC = RT(loCod.gameObject);
            rtLC.anchorMin = new Vector2(0, 1); rtLC.anchorMax = new Vector2(1, 1);
            rtLC.offsetMin = new Vector2(26, -144); rtLC.offsetMax = new Vector2(-26, -78);
            var lcLbl = Legenda("Rotulo", loCod.transform, "CÓDIGO", 17, Lavanda, TextAlignmentOptions.Left, 14f);
            Place(lcLbl.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(100, 24));
            var lcVal = Display("Valor", loCod.transform, "K7QP2M", 30, Amber, TextAlignmentOptions.Left);
            lcVal.characterSpacing = 10f;
            Place(lcVal.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(126, 0), new Vector2(200, 36));
            var lcBtn = Node("Btn_Copiar", loCod.transform);
            Place(lcBtn, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-14, 0), new Vector2(110, 44));
            var lcImg = Img("Bg", lcBtn.transform, Sprite("Frames", "UI_Card_R18_Royal"), Color.white);
            Stretch(lcImg.gameObject); Botao(lcImg, null);
            var lcTx = Rotulo("Label", lcBtn.transform, "COPIAR", 17, Cream, TextAlignmentOptions.Center, 5f);
            Stretch(lcTx.gameObject, 6, 4, 6, 4);

            var loLista = Node("Jogadores", lob.transform);
            var rtLL = RT(loLista);
            rtLL.anchorMin = new Vector2(0, 1); rtLL.anchorMax = new Vector2(1, 1);
            rtLL.offsetMin = new Vector2(26, -450); rtLL.offsetMax = new Vector2(-26, -158);
            VLayout(loLista, 8, new RectOffset(), TextAnchor.UpperCenter);
            for (int i = 0; i < 4; i++)
            {
                var v = Item("Item_LobbySlot", loLista.transform);
                v.name = "Jogador_" + (i + 1);
                Size(v, 428, 62);
            }

            var loBtns = Node("Botoes", lob.transform);
            var rtLB = RT(loBtns);
            rtLB.anchorMin = new Vector2(0, 0); rtLB.anchorMax = new Vector2(1, 0);
            rtLB.offsetMin = new Vector2(26, 106); rtLB.offsetMax = new Vector2(-26, 168);
            HLayout(loBtns, 10, new RectOffset(), TextAnchor.MiddleCenter, true, true);
            BotaoLargo(loBtns.transform, "Btn_Convidar", "CONVIDAR", "Deep", 210, 62, 21, Cream, false, Sky);
            BotaoLargo(loBtns.transform, "Btn_Pronto", "PRONTO", "Green", 210, 62, 22, Ink, true);

            var loAviso = Node("Aviso", lob.transform);
            var rtLA = RT(loAviso);
            rtLA.anchorMin = new Vector2(0, 0); rtLA.anchorMax = new Vector2(1, 0);
            rtLA.offsetMin = new Vector2(26, 26); rtLA.offsetMax = new Vector2(-26, 90);
            Contorno(loAviso.transform, new Color(Sky.r, Sky.g, Sky.b, .5f), 3f);
            var laBg = Card("Bg", loAviso.transform, "Deep"); Stretch(laBg.gameObject);
            laBg.color = new Color(Sky.r * .3f, Sky.g * .35f, Sky.b * .5f, 1f);
            var laIc = Icone("Icon", loAviso.transform, "Icon_Triangle", Sky, 18);
            Place(laIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(16, 0), new Vector2(18, 18));
            var laTx = Rotulo("Texto", loAviso.transform, "Aguardando LUCAS_DA_SILVA_99", 18, AzulClaro);
            Stretch(laTx.gameObject, 44, 4, 14, 4);
            laTx.font = FonteUiBold;

            // ações da garagem (base-centro). A garagem NÃO larga corrida: quem inicia a partida é
            // o lobby, que sabe se a sessão é online e quem é o dono da sala.
            var partida = Node("Partida", raiz.transform);
            Place(partida, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 56), new Vector2(800, 112));
            HLayout(partida, 20, new RectOffset(), TextAnchor.MiddleCenter);
            BotaoLargo(partida.transform, "Btn_JogarLocalmente", "SALVAR ESTILO", "Deep", 380, 104, 25, TextMuted, false);
            BotaoLargo(partida.transform, "Btn_Correr", "SALVAR E VOLTAR", "Green", 400, 112, 44, Ink, true);

            SalvarTela(raiz, "Screen_Garage_PC");
        }

        // ═══════════ 07 · ENTRADA POR CÓDIGO ═══════════
        static void JoinCode()
        {
            var raiz = Tela("Screen_JoinCode");
            var veu = Img("Veu", raiz.transform, null, new Color(.04f, .05f, .13f, .76f), Image.Type.Simple);
            Stretch(veu.gameObject); veu.raycastTarget = true;

            // 560 de altura: cabeçalho 166 + caixas 134 + botões 96 + faixa de estado 74 + margens
            var modal = Node("Modal", raiz.transform);
            Place(modal, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(920, 560));
            var mBg = Modal("Bg", modal.transform, 12f); Stretch(mBg.gameObject);
            var risco = Img("Risco", modal.transform, Sprite("Frames", "UI_Badge_R14_Cream"), Sky);
            Place(risco.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, 5), new Vector2(200, 10));

            var tit = Display("Titulo", modal.transform, "ENTRAR POR CÓDIGO", 44, Cream, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -52), new Vector2(700, 52));
            var sub = Rotulo("Subtitulo", modal.transform, "Digite os 6 caracteres que o dono da sala enviou", 23, TextMuted);
            sub.font = FonteUiSemi;
            Place(sub.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(52, -112), new Vector2(700, 32));

            var caixas = Node("Caixas", modal.transform);
            Place(caixas, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -166), new Vector2(816, 134));
            HLayout(caixas, 14, new RectOffset(), TextAnchor.UpperCenter);
            for (int i = 0; i < 6; i++)
            {
                var c = Item("Item_CodeBox", caixas.transform);
                c.name = "Caixa_" + (i + 1);
                Size(c, 114, 134);
                var ch = c.transform.Find("Caractere").GetComponent<TextMeshProUGUI>();
                if (i < 3) ch.text = "K7Q"[i].ToString();
                else
                {
                    ch.text = "";
                    if (i == 3)
                    {
                        c.transform.Find("State_Idle").gameObject.SetActive(false);
                        c.transform.Find("State_Focused").gameObject.SetActive(true);
                    }
                }
            }

            var botoes = Node("Botoes", modal.transform);
            Place(botoes, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(0, -324), new Vector2(816, 96));
            HLayout(botoes, 14, new RectOffset(), TextAnchor.MiddleCenter);
            BotaoLargo(botoes.transform, "Btn_Cancelar", "CANCELAR", "Deep", 268, 96, 25, TextMuted, false);
            BotaoLargo(botoes.transform, "Btn_Entrar", "ENTRAR", "Green", 534, 96, 34, Ink, true);

            // estados de retorno (irmãos, um por situação)
            var estados = Node("Estados", modal.transform);
            Place(estados, new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(.5f, 0), new Vector2(0, 36), new Vector2(816, 74));
            EstadoAviso(estados.transform, "State_CodigoInvalido", "Código inválido — confira os 6 caracteres",
                        VermelhoFundo, Red, RosaClaro, "Icon_Triangle");
            EstadoAviso(estados.transform, "State_SalaCheia", "Sala cheia — 16/16 jogadores",
                        AmberFundo, Amber, CreamDim, "Icon_Lock");
            EstadoAviso(estados.transform, "State_Conectando", "Conectando à sala…",
                        Blue, Slate, TextSecondary, "Icon_Signal");
            foreach (Transform t in estados.transform) t.gameObject.SetActive(false);

            SalvarTela(raiz, "Screen_JoinCode");
        }

        static void EstadoAviso(Transform pai, string nome, string texto, Color fundo, Color borda, Color corTexto, string icone)
        {
            var go = Node(nome, pai); Stretch(go);
            Contorno(go.transform, borda, 4f);
            var bg = Card("Bg", go.transform, "Deep"); Stretch(bg.gameObject);
            bg.color = fundo;
            var ic = Icone("Icon", go.transform, icone, borda, 20);
            Place(ic.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(20, 20));
            var t = Rotulo("Texto", go.transform, texto, 22, corTexto);
            t.font = FonteUiBold;
            Stretch(t.gameObject, 54, 6, 20, 6);
        }

        // ═══════════ 10 · CONFIGURAÇÕES ═══════════
        static void Settings()
        {
            var raiz = Tela("Screen_Settings");
            Fundo(raiz.transform, DeepBlue);

            var voltar = Widget("Btn_Icon", raiz.transform);
            voltar.name = "Btn_Voltar";
            Place(voltar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -44), new Vector2(64, 64));
            voltar.transform.Find("Icon").GetComponent<Image>().sprite = Sprite("Icons", "Icon_ArrowLeft");

            var tit = Display("Titulo", raiz.transform, "CONFIGURAÇÕES", 40, Cream, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(142, -44), new Vector2(500, 48));

            var perfil = Node("Perfil", raiz.transform);
            Place(perfil, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -44), new Vector2(320, 64));
            var pBg = Card("Bg", perfil.transform, "Ink"); Stretch(pBg.gameObject);
            var pAv = Img("Avatar", perfil.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white, Image.Type.Simple);
            Place(pAv.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(22, 0), new Vector2(30, 30));
            pAv.transform.localRotation = Quaternion.Euler(0, 0, -6f);
            var pNome = Rotulo("Nome", perfil.transform, "HIGOR", 23, Cream);
            Place(pNome.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(66, 0), new Vector2(160, 28));
            var pId = Anotacao("Id", perfil.transform, "#4821", 19, Lavanda, TextAlignmentOptions.Right);
            Place(pId.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-22, 0), new Vector2(90, 26));

            // lista de grupos (esquerda)
            var grupos = Node("Grupos", raiz.transform);
            Place(grupos, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -150), new Vector2(380, 480));
            VLayout(grupos, 10, new RectOffset(), TextAnchor.UpperCenter);
            var nomesGrupo = new[] { "ÁUDIO", "VÍDEO", "CONTROLES", "JOGO", "CONTA" };
            for (int i = 0; i < nomesGrupo.Length; i++)
            {
                var g = Node("Grupo_" + nomesGrupo[i], grupos.transform);
                Size(g, 380, 84);
                g.AddComponent<Image>().color = new Color(0, 0, 0, 0);
                var idle = Node("State_Idle", g.transform); Stretch(idle);
                var iBg = Card("Bg", idle.transform, "Ink"); Stretch(iBg.gameObject);
                var iTx = Rotulo("Label", idle.transform, nomesGrupo[i], 25, TextMuted, TextAlignmentOptions.Left, 6f);
                Stretch(iTx.gameObject, 24, 6, 24, 6);
                var act = Node("State_Active", g.transform); Stretch(act);
                var aBg = Img("Bg", act.transform, Sprite("Frames", "UI_Button_R22_Amber"), Color.white);
                Stretch(aBg.gameObject);
                var aTx = Display("Label", act.transform, nomesGrupo[i], 28, Ink, TextAlignmentOptions.Left);
                Stretch(aTx.gameObject, 24, 14, 24, 4);
                act.SetActive(i != 0 ? false : true);
                idle.SetActive(i != 0);
                Botao(g.GetComponent<Image>(), null);
            }

            // painéis (2 colunas). 1080 − 150 (topo) − 146 (ações + margem) = 784 → 2 linhas de 380 + 24
            var painel = Node("Painéis", raiz.transform);
            Place(painel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(468, -150), new Vector2(1396, 784));
            Grid(painel, new Vector2(686, 380), new Vector2(24, 24), 2);

            GrupoConfig(painel.transform, "Painel_Audio", "ÁUDIO", new[]{
                Cfg("Volume geral", "slider"), Cfg("Música", "slider"), Cfg("Efeitos", "slider") });

            GrupoConfig(painel.transform, "Painel_Video", "VÍDEO", new[]{
                Cfg("Qualidade gráfica", "seg", 1, "BAIXA", "MÉDIA", "ALTA"),
                Cfg("Limite de FPS", "seg", 1, "30", "60", "120"),
                Cfg("Tela", "seg", 1, "JANELA", "CHEIA"),
                Cfg("Tremer câmera", "toggle", 0),
                Cfg("Mostrar FPS e ping", "toggle", 1) });

            GrupoConfig(painel.transform, "Painel_Jogo", "JOGO", new[]{
                Cfg("Idioma", "selector", 0, "Português (BR)"),
                Cfg("Unidade de velocidade", "seg", 0, "KM/H", "MPH"),
                Cfg("Nomes dos jogadores", "toggle", 1),
                Cfg("Convites de amigos", "toggle", 1),
                Cfg("Vibração", "toggle", 1) });

            GrupoConfig(painel.transform, "Painel_Controles", "CONTROLES", new[]{
                Cfg("Sensibilidade da direção", "slider"),
                Cfg("Assistência de curva", "seg", 1, "NÃO", "LEVE", "FORTE"),
                Cfg("Botões espelhados", "toggle", 0) });

            // CONTA abre pelo mesmo painel (PLACA §10): ocupa a área inteira no lugar da grade.
            PainelConta(raiz.transform);

            // ações
            var acoes = Node("Acoes", raiz.transform);
            Place(acoes, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(468, 56), new Vector2(1396, 80));
            var rtA = RT(acoes);
            rtA.anchorMin = new Vector2(0, 0); rtA.anchorMax = new Vector2(0, 0); rtA.pivot = new Vector2(0, 0);
            var restaurar = Node("Btn_Restaurar", acoes.transform);
            BotaoBase(restaurar, "RESTAURAR PADRÕES", "Deep", 23, TextMuted, false);
            Place(restaurar, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(360, 80));
            var cancelar = Node("Btn_Cancelar", acoes.transform);
            BotaoBase(cancelar, "CANCELAR", "Deep", 23, Cream, false);
            Place(cancelar, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-320, 0), new Vector2(280, 80));
            var aplicar = Node("Btn_Aplicar", acoes.transform);
            BotaoBase(aplicar, "APLICAR", "Green", 30, Ink, true);
            Place(aplicar, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), Vector2.zero, new Vector2(300, 80));

            SalvarTela(raiz, "Screen_Settings");
        }

        /// <summary>
        /// Painel da aba CONTA: nome, ID, contas vinculadas e exclusão de dados (PLACA §10).
        /// Fica fora da grade porque ocupa a área inteira — a grade some quando ele aparece.
        /// </summary>
        static void PainelConta(Transform pai)
        {
            var go = Node("Painel_Conta", pai);
            Place(go, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(468, -150), new Vector2(1396, 784));
            var bg = Painel("Bg", go.transform, "Ink"); Stretch(bg.gameObject);

            var tit = Display("Titulo", go.transform, "CONTA", 30, Amber, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 36));

            // ---- identidade ----
            var ident = Node("Identidade", go.transform);
            Place(ident, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -84), new Vector2(1332, 120));
            var idBg = Card("Bg", ident.transform, "Deep"); Stretch(idBg.gameObject);

            var av = Img("Avatar", ident.transform, Sprite("Frames", "UI_Badge_R14_Amber"), Color.white, Image.Type.Simple);
            Place(av.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(38, 0), new Vector2(64, 64));
            av.transform.localRotation = Quaternion.Euler(0, 0, -6f);

            var nome = Display("Nome", ident.transform, "HIGOR", 34, Cream, TextAlignmentOptions.Left);
            Place(nome.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(120, 16), new Vector2(500, 40));
            var idTx = Anotacao("Id", ident.transform, "ID #4821", 20, Lavanda);
            Place(idTx.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(120, -18), new Vector2(500, 26));

            var copiar = Node("Btn_CopiarId", ident.transform);
            BotaoBase(copiar, "COPIAR ID", "Deep", 22, Cream, false);
            Place(copiar, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-320, 0), new Vector2(240, 72));
            var trocar = Node("Btn_TrocarNome", ident.transform);
            BotaoBase(trocar, "TROCAR NOME", "Deep", 22, Cream, false);
            Place(trocar, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-38, 0), new Vector2(260, 72));

            // ---- contas vinculadas ----
            var vinc = Node("Vinculadas", go.transform);
            Place(vinc, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -224), new Vector2(1332, 300));
            var vTit = Legenda("Titulo", vinc.transform, "CONTAS VINCULADAS", 20, TextMuted);
            Place(vTit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(500, 26));

            var lista = Node("Linhas", vinc.transform);
            Place(lista, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -40), new Vector2(1332, 250));
            VLayout(lista, 10, new RectOffset(), TextAnchor.UpperCenter);

            LinhaConta(lista.transform, "Google", "Icon_Person", "conectado", true);
            LinhaConta(lista.transform, "Steam", "Icon_Token", "conectar", false);
            LinhaConta(lista.transform, "Discord", "Icon_Signal", "conectar", false);

            // ---- zona de risco ----
            var risco = Node("Dados", go.transform);
            Place(risco, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(32, 32), new Vector2(1332, 132));
            var rBg = Card("Bg", risco.transform, "Deep"); Stretch(rBg.gameObject);
            Contorno(risco.transform, Red, 4f);

            var rTit = Rotulo("Titulo", risco.transform, "EXCLUIR DADOS DA CONTA", 25, Cream);
            Place(rTit.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(38, 20), new Vector2(700, 30));
            var rNota = Anotacao("Nota", risco.transform, "apaga progresso, cosméticos e passe. não tem volta.", 18, Lavanda);
            Place(rNota.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(38, -16), new Vector2(800, 24));

            var excluir = Node("Btn_ExcluirDados", risco.transform);
            BotaoBase(excluir, "EXCLUIR", "Red", 26, Cream, true);
            Place(excluir, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-38, 0), new Vector2(260, 84));

            go.SetActive(false);   // a grade é o padrão; CONTA liga pelo binder
        }

        /// <summary>Linha de conta vinculada: ícone + serviço + estado (2 estados irmãos) + botão.</summary>
        static void LinhaConta(Transform pai, string servico, string icone, string acao, bool conectado)
        {
            var linha = Node("Conta_" + servico, pai);
            Size(linha, 1332, 76);
            var bg = Card("Bg", linha.transform, "Deep"); Stretch(bg.gameObject);

            var ic = Icone("Icon", linha.transform, icone, Cream, 28);
            Place(ic.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(38, 0), new Vector2(28, 28));

            var nm = Rotulo("Nome", linha.transform, servico, 24, Cream);
            Place(nm.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(84, 0), new Vector2(400, 30));

            var on = Node("State_Conectado", linha.transform);
            Place(on, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-38, 0), new Vector2(260, 52));
            Chip("Chip", on.transform, "CONECTADO", 260, 52, Green, Ink, 22);
            on.SetActive(conectado);

            var off = Node("State_Desconectado", linha.transform);
            Place(off, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-38, 0), new Vector2(260, 60));
            var btn = Node("Btn_Conectar", off.transform);
            BotaoBase(btn, acao.ToUpperInvariant(), "Deep", 22, Cream, false);
            Place(btn, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(260, 60));
            off.SetActive(!conectado);
        }

        /// <summary>Descrição de uma linha de configuração: rótulo, tipo de controle, opção ativa e rótulos.</summary>
        struct LinhaCfg { public string nome, tipo; public int ativo; public string[] opcoes; }

        static LinhaCfg Cfg(string nome, string tipo, int ativo = 1, params string[] opcoes)
            => new LinhaCfg { nome = nome, tipo = tipo, ativo = ativo, opcoes = opcoes };

        static void GrupoConfig(Transform pai, string nome, string titulo, LinhaCfg[] linhas)
        {
            var go = Node(nome, pai);
            var bg = Painel("Bg", go.transform, "Ink"); Stretch(bg.gameObject);
            var tit = Display("Titulo", go.transform, titulo, 30, Amber, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(32, -28), new Vector2(400, 36));

            // 5 linhas de 54 + 4 espaços de 3 = 282, que cabe nos 380 do painel depois do título
            var cont = Node("Linhas", go.transform);
            var rt = RT(cont);
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(32, -(78 + linhas.Length * 57)); rt.offsetMax = new Vector2(-32, -78);
            VLayout(cont, 3, new RectOffset(), TextAnchor.UpperCenter);

            foreach (var c in linhas)
            {
                var linha = Item("Item_SettingRow", cont.transform);
                linha.name = "Cfg_" + c.nome.Replace(" ", "");
                Size(linha, 622, 54);
                var lbl = linha.transform.Find("Nome").GetComponent<TextMeshProUGUI>();
                lbl.text = c.nome;
                // rótulo 250 + controle 370 = 620, cabe nos 622 sem sobrepor
                Place(lbl.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(250, 30));
                // rótulos longos ("Sensibilidade da direção") estouravam os 250 e entravam no
                // controle: o TMP encolhe sozinho até 18 em vez de invadir a coluna vizinha.
                lbl.enableAutoSizing = true;
                lbl.fontSizeMax = 24;
                lbl.fontSizeMin = 18;
                var slot = linha.transform.Find("Controle");
                Place(slot.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(370, 48));

                switch (c.tipo)
                {
                    case "slider":
                        var s = Widget("Slider_Setting", slot); s.name = "Slider"; Size(s, 360, 48);
                        break;
                    case "toggle":
                        var t = Widget("Toggle_Setting", slot); t.name = "Toggle"; Size(t, 96, 46);
                        t.transform.Find("Off").gameObject.SetActive(c.ativo == 0);
                        t.transform.Find("On").gameObject.SetActive(c.ativo != 0);
                        break;
                    case "selector":
                        var sel = Widget("Selector_Option", slot); sel.name = "Selector"; Size(sel, 360, 50);
                        if (c.opcoes.Length > 0)
                            sel.transform.Find("Valor").GetComponent<TextMeshProUGUI>().text = c.opcoes[0];
                        break;
                    case "seg":
                        for (int k = 0; k < c.opcoes.Length; k++)
                        {
                            var seg = Item("Item_SegmentedOption", slot);
                            seg.name = "Opcao_" + c.opcoes[k].Replace("/", "");
                            Size(seg, c.opcoes.Length == 2 ? 130 : 112, 48);
                            bool on = k == c.ativo;
                            seg.transform.Find("State_Idle").gameObject.SetActive(!on);
                            seg.transform.Find("State_Active").gameObject.SetActive(on);
                            foreach (var tx in seg.GetComponentsInChildren<TextMeshProUGUI>(true))
                                tx.text = c.opcoes[k];
                        }
                        break;
                }
            }
        }

        // ═══════════ 11 · TELA DE RESULTADO ═══════════
        static void Result()
        {
            var raiz = Tela("Screen_Result");
            Fundo(raiz.transform, DeepBlue);

            var faixa = Img("FaixaTopo", raiz.transform, Sprite("Brand", "Brand_Stripes"), Color.white, Image.Type.Simple);
            Place(faixa.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 12));
            var rtF = RT(faixa.gameObject);
            rtF.anchorMin = new Vector2(0, 1); rtF.anchorMax = new Vector2(1, 1);
            rtF.offsetMin = new Vector2(0, -12); rtF.offsetMax = new Vector2(0, 0);

            var tit = Display("Titulo", raiz.transform, "CORRIDA FINALIZADA", 54, Cream, TextAlignmentOptions.Left);
            Place(tit.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(56, -52), new Vector2(700, 62));

            var correndo = Node("AindaCorrendo", raiz.transform);
            Place(correndo, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(780, -60), new Vector2(280, 52));
            Contorno(correndo.transform, Sky, 4f);
            var acBg = Card("Bg", correndo.transform, "Deep"); Stretch(acBg.gameObject);
            var acIc = Icone("Icon", correndo.transform, "Icon_Signal", Sky, 20);
            Place(acIc.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(18, 0), new Vector2(20, 20));
            var acTx = Rotulo("Texto", correndo.transform, "4 ainda correndo", 20, AzulClaro);
            acTx.font = FonteUiBold;
            Stretch(acTx.gameObject, 48, 4, 14, 4);

            // resumo (topo-direita)
            var resumo = Node("Resumo", raiz.transform);
            Place(resumo, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, -48), new Vector2(760, 100));
            HLayout(resumo, 10, new RectOffset(), TextAnchor.MiddleRight);
            CartaoResumo(resumo.transform, "SuaPosicao", "SUA POSIÇÃO", "4º", Amber, CreamDim, true);
            CartaoResumo(resumo.transform, "TempoTotal", "TEMPO TOTAL", "03:41.208", Cream, Lavanda, false);
            CartaoResumo(resumo.transform, "MelhorVolta", "MELHOR VOLTA", "01:11.302", Green, Lavanda, false);

            // cabeçalhos + 16 linhas em 2 colunas
            var tabela = Node("Tabela", raiz.transform);
            Place(tabela, new Vector2(0, 1), new Vector2(1, 1), new Vector2(.5f, 1), Vector2.zero, new Vector2(0, 700));
            var rtT = RT(tabela);
            rtT.anchorMin = new Vector2(0, 1); rtT.anchorMax = new Vector2(1, 1);
            rtT.offsetMin = new Vector2(56, -896); rtT.offsetMax = new Vector2(-56, -196);
            Grid(tabela, new Vector2(886, 68), new Vector2(18, 9), 2);

            Cabecalho(tabela.transform, "Cabecalho_Esq");
            Cabecalho(tabela.transform, "Cabecalho_Dir");
            for (int i = 0; i < 16; i++)
            {
                var linha = Item("Item_ResultRow", tabela.transform);
                linha.name = "Linha_" + (i + 1).ToString("00");
            }

            // rodapé
            var rod = Node("Rodape", raiz.transform);
            Place(rod, new Vector2(0, 0), new Vector2(1, 0), new Vector2(.5f, 0), Vector2.zero, new Vector2(0, 100));
            var rtR = RT(rod);
            rtR.anchorMin = new Vector2(0, 0); rtR.anchorMax = new Vector2(1, 0);
            rtR.offsetMin = new Vector2(56, 48); rtR.offsetMax = new Vector2(-56, 148);

            var nota = Anotacao("Nota", rod.transform, "a lista se reordena conforme os retardatários cruzam a linha", 16, Hex("#5C63A8"));
            Place(nota.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), Vector2.zero, new Vector2(700, 26));

            var voltar = Node("Btn_VoltarGaragem", rod.transform);
            BotaoBase(voltar, "VOLTAR À GARAGEM", "Deep", 24, TextMuted, false);
            Place(voltar, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-460, 0), new Vector2(400, 92));
            var denovo = Node("Btn_JogarNovamente", rod.transform);
            BotaoBase(denovo, "JOGAR NOVAMENTE", "Green", 34, Ink, true);
            Place(denovo, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), Vector2.zero, new Vector2(440, 100));

            SalvarTela(raiz, "Screen_Result");
        }

        static void Cabecalho(Transform pai, string nome)
        {
            var go = Node(nome, pai);
            var pos = Legenda("Pos", go.transform, "POS", 16, Hex("#5C63A8"), TextAlignmentOptions.Left, 14f);
            Place(pos.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(20, 0), new Vector2(66, 22));
            var jog = Legenda("Jogador", go.transform, "JOGADOR", 16, Hex("#5C63A8"), TextAlignmentOptions.Left, 14f);
            Place(jog.gameObject, new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(0, .5f), new Vector2(86, 0), new Vector2(300, 22));
            var tot = Legenda("Total", go.transform, "TEMPO TOTAL", 16, Hex("#5C63A8"), TextAlignmentOptions.Right, 14f);
            Place(tot.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-170, 0), new Vector2(200, 22));
            var mel = Legenda("Melhor", go.transform, "MELHOR", 16, Hex("#5C63A8"), TextAlignmentOptions.Right, 14f);
            Place(mel.gameObject, new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-20, 0), new Vector2(150, 22));
        }

        static void CartaoResumo(Transform pai, string nome, string rotulo, string valor, Color corValor, Color corRotulo, bool destaque)
        {
            var go = Node(nome, pai);
            Size(go, destaque ? 250 : 255, 100);
            if (destaque) Contorno(go.transform, Amber, 5f);
            var bg = Card("Bg", go.transform, destaque ? "Royal" : "Deep"); Stretch(bg.gameObject);
            var r = Legenda("Rotulo", go.transform, rotulo, 17, corRotulo, TextAlignmentOptions.Left, 14f);
            Place(r.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -20), new Vector2(220, 22));
            var v = Display("Valor", go.transform, valor, destaque ? 40 : 34, corValor, TextAlignmentOptions.Left);
            Place(v.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(26, 18), new Vector2(220, 44));
        }

        // ---------- botões ----------
        static void BotaoLargo(Transform pai, string nome, string txt, string variante, float w, float h,
                               float fonte, Color cor, bool display, Color? contorno = null)
        {
            var go = Node(nome, pai);
            Size(go, w, h);
            BotaoBase(go, txt, variante, fonte, cor, display, contorno);
        }

        static void BotaoBase(GameObject go, string txt, string variante, float fonte, Color cor, bool display,
                              Color? contorno = null)
        {
            var img = go.AddComponent<Image>();
            img.sprite = Sprite("Frames", $"UI_Button_R22_{variante}");
            img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = UIKit.EscalaNoveFatias;
            var t = Txt("Label", go.transform, txt, display ? FonteDisplay : FonteUiExtra, fonte, cor);
            if (!display) t.characterSpacing = 6f;
            Stretch(t.gameObject, 18, 14, 18, 4);
            var pressed = variante == "Green" ? Sprite("Frames", "UI_Button_R22_Pressed_Green")
                        : variante == "Amber" ? Sprite("Frames", "UI_Button_R22_Pressed_Amber") : null;
            Botao(img, pressed);
            if (contorno.HasValue)
            {
                var b = Img("Contorno", go.transform, Sprite("Frames", "UI_Button_R22_Deep"), contorno.Value);
                Stretch(b.gameObject, -5, -5, -5, -5);
                b.transform.SetAsFirstSibling();
            }
        }

        static Image SetaQuadrada(Transform pai, string nome, string icone, Vector2 ancora, Vector2 offset, float lado)
        {
            var img = Img(nome, pai, Sprite("Frames", "UI_Card_R18_Deep"), Color.white);
            Place(img.gameObject, ancora, ancora, ancora, offset, new Vector2(lado, lado));
            Icone("Icon", img.transform, icone, Cream, lado * .42f);
            Botao(img, null);
            return img;
        }
    }
}
