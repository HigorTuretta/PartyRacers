using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Frontend.Party;
using PartyRacers.UI.Garage;
using PartyRacers.UI.Motion;
using PartyRacers.UI.Shapes;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Transforma a tela recém-construída do protótipo em tela JOGÁVEL: botões que clicam, estados
    /// que trocam, listas que se preenchem e os binders com as referências ligadas.
    ///
    /// Roda como parte do import, e não depois, porque é aqui que ainda se sabe o que cada nó É —
    /// o dump traz o texto e a caixa de cada elemento. Depois de virar prefab sobra um `Box_7`.
    ///
    /// Duas coisas que o protótipo não pode dar e que são sintetizadas aqui:
    ///
    /// • <b>Os estados que não estavam na tela no momento da captura.</b> O HTML mostra UM estado
    ///   por elemento. A linha do RAFA estava AGUARDA e as outras três PRONTO — então cada linha
    ///   recebe uma cópia do chip que lhe falta, e o binder passa a ter os dois para alternar.
    /// • <b>Vaga vazia e vaga bloqueada</b>, que não existem em nenhuma linha do protótipo.
    /// </summary>
    public static class ScreenWiring
    {
        private static readonly Color Tinta = new Color(10 / 255f, 12 / 255f, 34 / 255f, 1f);
        private static readonly Color Apagado = new Color(155 / 255f, 165 / 255f, 215 / 255f, 0.34f);
        private static readonly Color Ambar = new Color(1f, 176 / 255f, 32 / 255f, 1f);
        private static readonly Color Verde = new Color(61 / 255f, 220 / 255f, 151 / 255f, 1f);
        private static readonly Color Azul = new Color(53 / 255f, 167 / 255f, 1f, 1f);
        private static readonly Color Violeta = new Color(140 / 255f, 123 / 255f, 1f, 1f);
        private static readonly Color Escuro = new Color(16 / 255f, 19 / 255f, 52 / 255f, 0.62f);
        private static readonly Color Fio = new Color(155 / 255f, 165 / 255f, 215 / 255f, 0.22f);

        public static void Ligar(string tela, GameObject raiz, ProtoBuilder.Mapa m)
        {
            // O CanvasGroup é o que o ScreenRouter usa para o fade de troca de tela.
            if (raiz.GetComponent<CanvasGroup>() == null)
                raiz.AddComponent<CanvasGroup>();

            switch (tela)
            {
                case "Screen_Lobby": Lobby(raiz, m); break;
                case "Screen_Garage": Garagem(raiz, m); break;
                case "Screen_Matchmaking": Matchmaking(raiz, m); break;
                case "Screen_CustomMatch": SalaPrivada(raiz, m); break;
                case "Screen_Store": Loja(raiz, m); break;
                case "Screen_BattlePass": Passe(raiz, m); break;
                case "Screen_Settings": Ajustes(raiz, m); break;
                case "Screen_Result": Resultado(raiz, m); break;
                case "Screen_JoinCode": Codigo(raiz, m); break;
                case "Screen_Loading": Carregando(raiz, m); break;
                case "Screen_RaceMenu": MenuDaPartida(raiz, m); break;
            }
        }

        // ================================================================== Loja

        private static void Loja(GameObject raiz, ProtoBuilder.Mapa m)
        {
            BarraDoV2(raiz, m, "Loja");

            var ui = raiz.AddComponent<StoreScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "textoMoedas", Tmp(m.Texto("12.480")));
            Atribuir(so, "textoFichas", Tmp(m.Texto("340")));
            Atribuir(so, "textoTimer", Tmp(m.Texto("DIAS", false)));

            // A grade de 4 cards do documento vira UM prefab e um contêiner que cresce. Os cards
            // "diários" são a fileira menor, com a mesma lógica.
            Grade(so, m, "containerGrade", "prefabCard",
                  "Assets/_Projeto/Prefabs/UI_v2/Items/Card_Store_v2.prefab", 264f, 169f, 4f);

            Grade(so, m, "containerDiarios", "prefabDiario",
                  "Assets/_Projeto/Prefabs/UI_v2/Items/Card_StoreDaily_v2.prefab", 274f, 348f, 6f);

            AbasDeTexto(m, "TODOS", "CARROS", "RODAS", "ADESIVOS", "BUZINAS", "RASTROS");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Passe

        private static void Passe(GameObject raiz, ProtoBuilder.Mapa m)
        {
            BarraDoV2(raiz, m, "Passe");

            var ui = raiz.AddComponent<BattlePassScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "textoNivel", Tmp(m.Texto("12")));
            Atribuir(so, "rotuloProgresso", Tmp(m.Texto("PROGRESSO PARA O NÍVEL", false)));
            Atribuir(so, "textoProgresso", Tmp(m.Texto("640 / 1.000 XP", false)));

            // As duas faixas de recompensa: a de cima é a premium.
            RectTransform[] cards = m.Caixas(250f, 232f, 8f);
            RectTransform[] gratis = m.Caixas(250f, 186f, 8f);

            if (cards.Length > 0)
            {
                Atribuir(so, "faixaPremium", cards[0].parent);
                Prefabizar(cards, "Assets/_Projeto/Prefabs/UI_v2/Items/Card_PassTier_v2.prefab",
                           out GameObject prefab);
                Atribuir(so, "prefabTier", prefab);
            }

            if (gratis.Length > 0)
            {
                Atribuir(so, "faixaGratis", gratis[0].parent);
                // A faixa grátis também é preenchida em runtime: deixar as cópias do documento
                // faria a fileira de cima nascer vazia e a de baixo cheia, como se uma delas
                // tivesse quebrado.
                Prefabizar(gratis, "Assets/_Projeto/Prefabs/UI_v2/Items/Card_PassTierFree_v2.prefab",
                           out GameObject _);
            }

            RectTransform[] cabecalho = m.Caixas(250f, 56f, 6f);
            if (cabecalho.Length > 0)
                Atribuir(so, "cabecalhoNiveis", cabecalho[0].parent);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Ajustes

        private static void Ajustes(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<SettingsScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "btnVoltar", ClicavelPorTexto(m, "VOLTAR", false));
            Atribuir(so, "btnRestaurar", ClicavelPorTexto(m, "RESTAURAR", false));
            Atribuir(so, "btnCancelar", ClicavelPorTexto(m, "CANCELAR", false));
            Atribuir(so, "btnAplicar", ClicavelPorTexto(m, "APLICAR", false));

            // Os cinco grupos da coluna da esquerda: cada um acende um painel à direita.
            (string id, string rotulo)[] grupos =
            {
                ("audio", "ÁUDIO"), ("video", "VÍDEO"), ("controles", "CONTROLES"),
                ("jogo", "JOGO"), ("conta", "CONTA"),
            };

            SerializedProperty lista = so.FindProperty("grupos");
            lista.arraySize = grupos.Length;

            for (int i = 0; i < grupos.Length; i++)
            {
                SerializedProperty item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("id").stringValue = grupos[i].id;

                RectTransform botao = m.Dono(grupos[i].rotulo, 1);
                if (botao == null)
                    continue;

                Clicavel(botao);
                item.FindPropertyRelative("botao").objectReferenceValue = botao.gameObject;
                (GameObject ativo, GameObject ocioso) = DoisEstados(botao, i == 0);
                item.FindPropertyRelative("botaoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("botaoIdle").objectReferenceValue = ocioso;

                // O painel do grupo é o bloco grande cujo título repete o nome do grupo. No
                // documento os cinco aparecem empilhados; aqui viram irmãos exclusivos.
                RectTransform painel = m.Todos(grupos[i].rotulo).Skip(1).FirstOrDefault();
                if (painel != null)
                {
                    Transform bloco = painel.parent;
                    item.FindPropertyRelative("painel").objectReferenceValue = bloco.gameObject;
                    bloco.gameObject.SetActive(i == 0);
                }
            }

            // Cada linha de ajuste tem 610×60 no documento: rótulo à esquerda, controle à direita.
            RectTransform[] linhas = m.Caixas(610f, 60f, 6f);
            SerializedProperty ajustes = so.FindProperty("ajustes");
            ajustes.arraySize = linhas.Length;

            for (int i = 0; i < linhas.Length; i++)
            {
                SerializedProperty item = ajustes.GetArrayElementAtIndex(i);
                RectTransform[] textos = m.TextosEm(linhas[i]);
                string chave = textos.Length > 0 ? Chave(Tmp(textos[0])) : "ajuste" + i;
                item.FindPropertyRelative("chave").stringValue = chave;

                // Valor numérico à direita quando existe (volume, sensibilidade).
                TextMeshProUGUI valor = textos.Length > 1 ? Tmp(textos[textos.Length - 1]) : null;
                if (valor != null && int.TryParse(valor.text.Trim(), out _))
                    item.FindPropertyRelative("valor").objectReferenceValue = valor;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Resultado

        private static void Resultado(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<ResultScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "textoSuaPosicao", Tmp(m.Texto("4º")));
            Atribuir(so, "textoTempoTotal", Tmp(m.Texto("03:41.208")));
            Atribuir(so, "textoMelhorVolta", Tmp(m.Texto("01:11.302")));
            Atribuir(so, "textoAindaCorrendo", Tmp(m.Texto("ainda correndo", false)));

            RectTransform aviso = m.Dono("ainda correndo", 1, false);
            if (aviso != null)
                Atribuir(so, "blocoAindaCorrendo", aviso.gameObject);

            Atribuir(so, "btnVoltarGaragem", ClicavelPorTexto(m, "GARAGEM", false));
            Atribuir(so, "btnJogarNovamente", ClicavelPorTexto(m, "NOVAMENTE", false));

            // As 16 linhas da tabela: uma vira prefab, o resto some e a lista cresce em runtime.
            RectTransform[] linhas = m.Caixas(895f, 68f, 6f);
            if (linhas.Length > 0)
            {
                Atribuir(so, "containerTabela", linhas[0].parent);
                Atribuir(so, "conteudoTabela", linhas[0].parent as RectTransform);
                Prefabizar(linhas, "Assets/_Projeto/Prefabs/UI_v2/Items/Row_Result_v2.prefab",
                           out GameObject prefab);
                Atribuir(so, "prefabLinha", prefab);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Entrar por código

        private static void Codigo(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<JoinCodeUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "btnEntrar", ClicavelPorTexto(m, "ENTRAR"));
            Atribuir(so, "btnCancelar", ClicavelPorTexto(m, "CANCELAR"));

            // As seis caixas do código, com os três estados de cada uma.
            RectTransform[] caixas = m.Caixas(114f, 134f, 8f);
            SerializedProperty lista = so.FindProperty("caixas");
            lista.arraySize = caixas.Length;

            for (int i = 0; i < caixas.Length; i++)
            {
                SerializedProperty item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("raiz").objectReferenceValue = caixas[i].gameObject;

                RectTransform[] textos = m.TextosEm(caixas[i]);
                if (textos.Length > 0)
                    item.FindPropertyRelative("caractere").objectReferenceValue = Tmp(textos[0]);

                (GameObject foco, GameObject ocioso) = DoisEstados(caixas[i], i == 3);
                item.FindPropertyRelative("estadoFoco").objectReferenceValue = foco;
                item.FindPropertyRelative("estadoIdle").objectReferenceValue = ocioso;
                item.FindPropertyRelative("estadoErro").objectReferenceValue =
                    Contorno(caixas[i], "State_Error", new Color(1f, 77 / 255f, 109 / 255f, 1f), 3f, 16f);
            }

            // O documento desenha os três avisos empilhados sob o rótulo "ESTADOS DE ERRO"; na tela
            // eles são exclusivos, e a etiqueta de documentação sai.
            Atribuir(so, "estadoCodigoInvalido", Bloco(m, "Código inválido"));
            Atribuir(so, "estadoSalaCheia", Bloco(m, "Sala cheia"));
            Atribuir(so, "estadoConectando", Bloco(m, "Conectando"));
            Esconder(m, "ESTADOS DE ERRO");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Carregando

        private static void Carregando(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<LoadingScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "grupo", raiz.GetComponent<CanvasGroup>());
            Atribuir(so, "textoEstado", Tmp(m.Texto("CARREGANDO PISTA", false)));
            Atribuir(so, "textoDica", Tmp(m.Texto("O escudo bloqueia", false)));

            RectTransform conexao = m.Dono("CONEXÃO", 1, false);
            if (conexao != null)
                Atribuir(so, "blocoConexao", conexao.gameObject);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Menu da partida

        private static void MenuDaPartida(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.RaceMenuUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "btnVoltar", ClicavelPorTexto(m, "VOLTAR À CORRIDA"));
            Atribuir(so, "btnConfiguracoes", ClicavelPorTexto(m, "CONFIGURAÇÕES"));
            Atribuir(so, "btnCopiarCodigo", ClicavelPorTexto(m, "COPIAR CÓDIGO DA SALA"));
            Atribuir(so, "btnSair", ClicavelPorTexto(m, "SAIR DA PARTIDA"));
            Atribuir(so, "btnSairAgora", ClicavelPorTexto(m, "SAIR AGORA"));
            Atribuir(so, "btnFicar", ClicavelPorTexto(m, "FICAR NA CORRIDA"));

            RectTransform gaveta = m.Dono("MENU", 2);
            if (gaveta != null)
                Atribuir(so, "gaveta", gaveta.gameObject);

            RectTransform popup = m.Dono("SAIR DA PARTIDA?", 2);
            if (popup != null)
            {
                Atribuir(so, "popupSair", popup.gameObject);
                popup.gameObject.SetActive(false);
            }

            Esconder(m, "POP-UP DE CONFIRMAÇÃO");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Lobby

        private static void Lobby(GameObject raiz, ProtoBuilder.Mapa m)
        {
            BarraDeNavegacao(raiz, m, "Lobby");

            var ui = raiz.AddComponent<PublicLobbyScreenUI>();
            var so = new SerializedObject(ui);

            // ---- modos
            SerializedProperty cards = so.FindProperty("cardsDeModo");
            cards.arraySize = 3;
            string[] rotulos = { "SOLO", "DUO", "SQUAD" };

            for (int i = 0; i < 3; i++)
            {
                RectTransform card = m.Dono(rotulos[i], 2);
                if (card == null)
                    continue;

                SerializedProperty item = cards.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("modo").enumValueIndex = i;
                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(card);

                (GameObject ativo, GameObject ocioso) = DoisEstados(card, i == 2);
                item.FindPropertyRelative("estadoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("estadoOcioso").objectReferenceValue = ocioso;
            }

            // ---- vagas do grupo
            RectTransform[] linhas = m.Caixas(360f, 68f);
            RectTransform chipPronto = m.Dono("PRONTO", 1);
            RectTransform chipAguarda = m.Texto("AGUARDA");
            RectTransform selo = m.Texto("LÍDER");

            SerializedProperty vagas = so.FindProperty("vagas");
            vagas.arraySize = linhas.Length;

            for (int i = 0; i < linhas.Length; i++)
            {
                RectTransform linha = linhas[i];
                SerializedProperty vaga = vagas.GetArrayElementAtIndex(i);

                GameObject jogador = Agrupar(linha, "State_Player");
                GameObject vazio = VagaDesenhada(linha, "State_Empty", "VAGA LIVRE", Apagado);
                GameObject bloqueado = VagaDesenhada(linha, "State_Locked", "INDISPONÍVEL", Apagado * 0.6f);

                // Chip de estado: o pai dele no protótipo é a própria linha, então a cópia cai no
                // mesmo lugar. O selo de LÍDER mora DENTRO do bloco do nome — copiá-lo para a raiz
                // da linha jogava a chapinha para a borda direita.
                RectTransform pronto = DentroDe(jogador, chipPronto) ?? Copiar(chipPronto, jogador.transform);
                RectTransform aguarda = DentroDe(jogador, chipAguarda) ?? Copiar(chipAguarda, jogador.transform);

                RectTransform[] textos = m.TextosEm(linha);
                TextMeshProUGUI nome = textos.Length > 0 ? Tmp(textos[0]) : null;
                TextMeshProUGUI meta = textos.Select(Tmp)
                                             .FirstOrDefault(t => t != null && t.text.StartsWith("nível"));

                Transform blocoDoNome = nome != null && selo != null ? nome.transform.parent : null;
                RectTransform lider = DentroDe(jogador, selo) ?? Copiar(selo, blocoDoNome);

                if (pronto != null) pronto.name = "State_Ready";
                if (aguarda != null) aguarda.name = "State_Waiting";
                if (lider != null) lider.name = "Badge_Leader";

                GameObject destaque = Contorno(linha, "Destaque_IsLocal", Ambar, 3f, 20f);

                vaga.FindPropertyRelative("raiz").objectReferenceValue = linha.gameObject;
                vaga.FindPropertyRelative("estadoJogador").objectReferenceValue = jogador;
                vaga.FindPropertyRelative("estadoVazio").objectReferenceValue = vazio;
                vaga.FindPropertyRelative("estadoBloqueado").objectReferenceValue = bloqueado;
                vaga.FindPropertyRelative("nome").objectReferenceValue = nome;
                vaga.FindPropertyRelative("meta").objectReferenceValue = meta;
                vaga.FindPropertyRelative("seloDeLider").objectReferenceValue = lider != null ? lider.gameObject : null;
                vaga.FindPropertyRelative("estadoPronto").objectReferenceValue = pronto != null ? pronto.gameObject : null;
                vaga.FindPropertyRelative("estadoAguardando").objectReferenceValue = aguarda != null ? aguarda.gameObject : null;
                vaga.FindPropertyRelative("destaqueLocal").objectReferenceValue = destaque;

                vazio.SetActive(false);
                bloqueado.SetActive(false);
                destaque.SetActive(false);
                if (aguarda != null) aguarda.gameObject.SetActive(false);
                if (lider != null) lider.gameObject.SetActive(false);
            }

            so.FindProperty("contadorDoGrupo").objectReferenceValue = Tmp(m.Texto("4/4"));

            // ---- amigos
            RectTransform[] amigos = m.Caixas(334f, 58f);
            if (amigos.Length > 0)
            {
                Transform conteudo = amigos[0].parent;
                FriendRowUI modelo = LinhaDeAmigo(amigos, m);

                so.FindProperty("conteudoDaListaDeAmigos").objectReferenceValue = conteudo;
                so.FindProperty("prefabDeAmigo").objectReferenceValue = modelo;

                // As 8 linhas do protótipo eram maquete: a lista real cresce por Instantiate, que é
                // o único caso em que o handoff permite instanciar (tamanho desconhecido).
                var layout = conteudo.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 6f;
                layout.padding = new RectOffset(7, 7, 8, 8);
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.UpperCenter;

                foreach (RectTransform linha in amigos)
                    Object.DestroyImmediate(linha.gameObject);
            }

            RectTransform abaJogo = m.Dono("NO JOGO", 1);
            RectTransform abaSteam = m.Dono("STEAM", 1);

            if (abaJogo != null && abaSteam != null)
            {
                so.FindProperty("abaNoJogo").objectReferenceValue = Clicavel(abaJogo);
                so.FindProperty("abaSteam").objectReferenceValue = Clicavel(abaSteam);

                (GameObject jogoAtivo, GameObject jogoOcioso) = DoisEstados(abaJogo, true);
                (GameObject steamAtivo, GameObject steamOcioso) = DoisEstados(abaSteam, false);
                so.FindProperty("abaNoJogoAtiva").objectReferenceValue = jogoAtivo;
                so.FindProperty("abaNoJogoOciosa").objectReferenceValue = jogoOcioso;
                so.FindProperty("abaSteamAtiva").objectReferenceValue = steamAtivo;
                so.FindProperty("abaSteamOciosa").objectReferenceValue = steamOcioso;
            }

            // ---- barra de ação
            RectTransform cancelar = m.Texto("CANCELAR");
            RectTransform buscar = m.Dono("BUSCAR PARTIDA", 1);

            if (cancelar != null)
                so.FindProperty("btnPronto").objectReferenceValue = Clicavel(cancelar);

            if (buscar != null)
            {
                so.FindProperty("btnBuscarPartida").objectReferenceValue = Clicavel(buscar);

                // Desabilitado é o tracejado que veio do protótipo; habilitado é a mesma caixa em
                // verde. Os dois existem na cena e o binder só liga um.
                GameObject desabilitado = Agrupar(buscar, "State_Disabled");
                GameObject habilitado = Nó(buscar, "State_Enabled");
                habilitado.transform.SetSiblingIndex(0);

                var forma = habilitado.AddComponent<UIRoundedRect>();
                forma.raycastTarget = false;
                forma.Definir(Verde, 22f);
                forma.DefinirContorno(Tinta, 3f);

                RectTransform rotulo = Copiar(m.Texto("BUSCAR PARTIDA"), habilitado.transform);
                if (rotulo != null)
                {
                    rotulo.name = "Label";
                    TextMeshProUGUI t = Tmp(rotulo);
                    if (t != null)
                        t.color = Tinta;
                }

                so.FindProperty("buscarHabilitado").objectReferenceValue = habilitado;
                so.FindProperty("buscarDesabilitado").objectReferenceValue = desabilitado;
                habilitado.SetActive(false);
            }

            so.FindProperty("motivoDoBloqueio").objectReferenceValue = Tmp(m.Texto("FALTA 1 JOGADOR"));
            so.FindProperty("resumoDoGrupo").objectReferenceValue = Tmp(m.Texto("AGUARDANDO CONFIRMAÇÃO", false));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Transforma uma das linhas de amigo num prefab de linha, com os quatro estados.</summary>
        private static FriendRowUI LinhaDeAmigo(RectTransform[] amigos, ProtoBuilder.Mapa m)
        {
            RectTransform modelo = amigos[0];
            var ui = modelo.gameObject.AddComponent<FriendRowUI>();
            var so = new SerializedObject(ui);

            RectTransform[] textos = m.TextosEm(modelo);
            Atribuir(so, "nome", textos.Length > 0 ? Tmp(textos[0]) : null);
            Atribuir(so, "estado", textos.Length > 1 ? Tmp(textos[1]) : null);

            // O botão CONVIDAR e a chapinha de indisponível moram em linhas diferentes do
            // protótipo; aqui os dois viram irmãos e o binder liga um de cada vez.
            RectTransform convidar = m.Texto("CONVIDAR");
            RectTransform indisponivel = m.Dono("NO GRUPO", 1);

            if (convidar != null)
            {
                RectTransform btn = DentroDe(modelo.gameObject, convidar) ?? Copiar(convidar, modelo);
                btn.name = "Btn_Invite";
                Atribuir(so, "btnConvidar", Clicavel(btn));
            }

            if (indisponivel != null)
            {
                RectTransform chip = DentroDe(modelo.gameObject, indisponivel) ?? Copiar(indisponivel, modelo);
                chip.name = "Label_Unavailable";
                Atribuir(so, "rotuloIndisponivel", chip.gameObject);
                Atribuir(so, "textoIndisponivel", Tmp(chip));
            }

            // O ponto de presença: verde, âmbar e cinza no mesmo lugar.
            RectTransform ponto = m.Caixas(12f, 12f, 2f)
                                   .FirstOrDefault(r => r != null && r.IsChildOf(modelo));

            if (ponto != null)
            {
                Atribuir(so, "pontoOnline", ponto.gameObject);
                Atribuir(so, "pontoEmJogo", Ponto(ponto, "Dot_Busy", Ambar));
                Atribuir(so, "pontoOffline", Ponto(ponto, "Dot_Offline", Apagado));
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // A linha vem do protótipo ancorada ao painel inteiro; dentro de um VerticalLayoutGroup
            // isso vira altura negativa e a lista some. Vira caixa de altura própria.
            float altura = modelo.rect.height > 4f ? modelo.rect.height : 58f;
            modelo.anchorMin = new Vector2(0f, 1f);
            modelo.anchorMax = new Vector2(1f, 1f);
            modelo.pivot = new Vector2(0.5f, 1f);
            modelo.sizeDelta = new Vector2(0f, altura);

            var elemento = modelo.gameObject.AddComponent<LayoutElement>();
            elemento.preferredHeight = altura;
            elemento.minHeight = altura;

            const string caminho = "Assets/_Projeto/Prefabs/UI_v2/Items/Row_Friend_v2.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(modelo.gameObject, caminho);
            return prefab != null ? prefab.GetComponent<FriendRowUI>() : ui;
        }

        // ================================================================== Garagem

        private static void Garagem(GameObject raiz, ProtoBuilder.Mapa m)
        {
            BarraDeNavegacao(raiz, m, "Garagem");

            var ui = raiz.AddComponent<GarageGridUI>();
            var so = new SerializedObject(ui);

            // Categoria → de onde vem a lista de opções. Modelo e cor são globais do carro; o resto
            // é peça, e cada peça é um elemento do modelo 3D (CarElementName).
            (string rotulo, int fonte, int elemento)[] categorias =
            {
                ("MODELO",   0, 0),
                ("COR",      1, 0),
                ("RODAS",    2, 5),
                ("FRENTE",   2, 1),
                ("TRASEIRA", 2, 2),
                ("TETO",     2, 9),
                ("ADESIVOS", 2, 6),
            };

            SerializedProperty abas = so.FindProperty("abas");
            abas.arraySize = categorias.Length;

            for (int i = 0; i < categorias.Length; i++)
            {
                SerializedProperty item = abas.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("categoria").stringValue = categorias[i].rotulo;
                item.FindPropertyRelative("fonte").enumValueIndex = categorias[i].fonte;
                item.FindPropertyRelative("elemento").intValue = categorias[i].elemento;

                RectTransform aba = m.Dono(categorias[i].rotulo, 1);
                if (aba == null)
                    continue;

                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(aba);
                (GameObject ativo, GameObject ocioso) = DoisEstados(aba, categorias[i].rotulo == "RODAS");
                item.FindPropertyRelative("estadoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("estadoOcioso").objectReferenceValue = ocioso;
            }

            // ---- grade de cards
            RectTransform[] cards = m.Caixas(158f, 168f, 6f);
            SerializedProperty lista = so.FindProperty("cards");
            lista.arraySize = cards.Length;

            for (int i = 0; i < cards.Length; i++)
            {
                RectTransform card = cards[i];
                SerializedProperty item = lista.GetArrayElementAtIndex(i);

                GameObject conteudo = Agrupar(card, "Conteudo");
                item.FindPropertyRelative("raiz").objectReferenceValue = card.gameObject;
                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(card);

                // Os quatro estados são molduras SOBRE o conteúdo, e não quatro cards diferentes:
                // assim o preview e o nome nunca saltam de lugar ao selecionar.
                GameObject equipado = Contorno(card, "Equipped", Verde, 3f, 18f);
                GameObject selecionado = Contorno(card, "Selected", Ambar, 3f, 18f);
                GameObject livre = Nó(card, "Free");
                GameObject bloqueado = Veu(card, "Locked");

                item.FindPropertyRelative("equipado").objectReferenceValue = equipado;
                item.FindPropertyRelative("selecionado").objectReferenceValue = selecionado;
                item.FindPropertyRelative("livre").objectReferenceValue = livre;
                item.FindPropertyRelative("bloqueado").objectReferenceValue = bloqueado;

                RectTransform[] textos = m.TextosEm(card);
                if (textos.Length > 1)
                    item.FindPropertyRelative("nome").objectReferenceValue = Tmp(textos[textos.Length - 2]);

                Image preview = conteudo.GetComponentsInChildren<Image>(true).FirstOrDefault();
                item.FindPropertyRelative("preview").objectReferenceValue = preview;

                equipado.SetActive(false);
                selecionado.SetActive(false);
                bloqueado.SetActive(false);
            }

            Atribuir(so, "contagemDaCategoria", Tmp(m.Texto("ITENS", false)));
            Atribuir(so, "btnEquipar", ClicavelPorTexto(m, "EQUIPAR"));
            Atribuir(so, "btnDesfazer", ClicavelPorTexto(m, "DESFAZER"));
            Atribuir(so, "textoDeDesbloqueio", Tmp(m.Texto("CÂMERA", false)));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Matchmaking

        private static void Matchmaking(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<MatchmakingModalUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "raiz", raiz);

            // O protótipo mostrava "08s"; o binder escreve o tempo decorrido em mm:ss, que é mais
            // largo. A caixa cresce para a ESQUERDA a partir da borda direita, senão o número
            // atravessa a moldura do modal assim que passa de 9 segundos.
            TextMeshProUGUI tempo = Tmp(m.Texto("08s"));
            if (tempo != null)
            {
                var rt = (RectTransform)tempo.transform;
                rt.anchorMin = new Vector2(1f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(240f, rt.sizeDelta.y);
                tempo.alignment = TextAlignmentOptions.MidlineRight;
            }

            Atribuir(so, "textoDoTempo", tempo);
            Atribuir(so, "contadorDeJogadores", Tmp(m.Texto("5", false)));
            Atribuir(so, "btnCancelar", ClicavelPorTexto(m, "CANCELAR BUSCA"));

            // A agulha tem 3 px de largura e 210 de altura. O binder move a posição dela entre 2% e
            // 98% do dial; a varredura de luz mora no mesmo objeto.
            RectTransform agulha = m.Caixas(3f, 210f, 5f).FirstOrDefault();
            if (agulha != null)
            {
                Atribuir(so, "agulhaVarrendo", agulha.gameObject);
                UIShineSweep varredura = agulha.GetComponent<UIShineSweep>()
                                      ?? agulha.gameObject.AddComponent<UIShineSweep>();
                Atribuir(so, "varreduraDaAgulha", varredura);
            }

            // ---- as 16 vagas da sala
            RectTransform[] vagas = m.Caixas(158f, 98.5f, 3f);
            SerializedProperty lista = so.FindProperty("vagas");
            lista.arraySize = vagas.Length;

            for (int i = 0; i < vagas.Length; i++)
            {
                RectTransform vaga = vagas[i];
                SerializedProperty item = lista.GetArrayElementAtIndex(i);

                // As 16 vagas vieram do protótipo com aparências DIFERENTES (quatro âmbar, uma azul,
                // onze tracejadas). Como qualquer vaga pode virar qualquer coisa, a aparência de
                // origem é descartada e todas recebem os mesmos quatro estados.
                LimparAparencia(vaga);
                item.FindPropertyRelative("raiz").objectReferenceValue = vaga.gameObject;

                GameObject mate = Moldura(vaga, "State_Mate", Ambar * 0.14f, Ambar, 3f, 18f);
                GameObject humano = Moldura(vaga, "State_Human", new Color(42 / 255f, 52 / 255f, 128 / 255f, 0.8f), Tinta, 3f, 18f);
                GameObject bot = Moldura(vaga, "State_Bot", Violeta * 0.16f, Violeta, 3f, 18f);
                GameObject vazio = Tracejado(vaga, "State_Empty");

                item.FindPropertyRelative("estadoCompanheiro").objectReferenceValue = mate;
                item.FindPropertyRelative("estadoHumano").objectReferenceValue = humano;
                item.FindPropertyRelative("estadoBot").objectReferenceValue = bot;
                item.FindPropertyRelative("estadoVazio").objectReferenceValue = vazio;

                RectTransform[] textos = m.TextosEm(vaga);
                if (textos.Length > 0)
                    item.FindPropertyRelative("nome").objectReferenceValue = Tmp(textos[textos.Length - 1]);

                mate.SetActive(false);
                humano.SetActive(false);
                bot.SetActive(false);
            }

            // ---- faixa de etapas
            string[] etapas = { "PRONTOS", "PROCURANDO", "ENCONTRADOS", "PREENCHENDO", "CARREGANDO" };
            SerializedProperty chapinhas = so.FindProperty("chapinhas");
            chapinhas.arraySize = etapas.Length;

            for (int i = 0; i < etapas.Length; i++)
            {
                SerializedProperty item = chapinhas.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("etapa").enumValueIndex = i;

                RectTransform chip = m.Dono(etapas[i], 1);
                if (chip == null)
                    continue;

                GameObject feito = Preenchido(chip, "Done", new Color(19 / 255f, 89 / 255f, 62 / 255f, 1f));
                GameObject agora = Preenchido(chip, "Now", Ambar);
                GameObject aFazer = Preenchido(chip, "Todo", Escuro);

                item.FindPropertyRelative("feito").objectReferenceValue = feito;
                item.FindPropertyRelative("agora").objectReferenceValue = agora;
                item.FindPropertyRelative("aFazer").objectReferenceValue = aFazer;

                feito.SetActive(false);
                agora.SetActive(false);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Sala privada

        private static void SalaPrivada(GameObject raiz, ProtoBuilder.Mapa m)
        {
            BarraDeNavegacao(raiz, m, "Lobby");

            var ui = raiz.AddComponent<CustomMatchScreenUI>();
            var so = new SerializedObject(ui);

            Atribuir(so, "codigoDaSala", Tmp(m.Texto("KQ2", false)));
            Atribuir(so, "contadorDeBots", Tmp(m.Texto("+ BOTS", false)));
            Atribuir(so, "contador", Tmp(m.Texto("/ 16", false)) ?? Tmp(m.Texto("9 / 16", false)));
            Atribuir(so, "btnIniciar", ClicavelPorTexto(m, "INICIAR"));
            Atribuir(so, "btnAdicionarBot", ClicavelPorTexto(m, "BOTS", false));
            Atribuir(so, "nomeDoMapa", Tmp(m.Texto("MINI GOLFE RUN", false)));
            Atribuir(so, "descricaoDoMapa", Tmp(m.Texto("níveis", false)));

            RectTransform[] linhas = m.Caixas(644f, 94f, 6f);
            SerializedProperty lista = so.FindProperty("vagas");
            lista.arraySize = linhas.Length;

            for (int i = 0; i < linhas.Length; i++)
            {
                RectTransform linha = linhas[i];
                SerializedProperty item = lista.GetArrayElementAtIndex(i);

                GameObject jogador = Agrupar(linha, "State_Player");
                GameObject bot = Contorno(linha, "State_Bot", Violeta, 2f, 16f);
                GameObject vazio = Tracejado(linha, "State_Empty");

                item.FindPropertyRelative("raiz").objectReferenceValue = linha.gameObject;
                item.FindPropertyRelative("estadoJogador").objectReferenceValue = jogador;
                item.FindPropertyRelative("estadoBot").objectReferenceValue = bot;
                item.FindPropertyRelative("estadoVazio").objectReferenceValue = vazio;

                RectTransform[] textos = m.TextosEm(linha);
                if (textos.Length > 0)
                    item.FindPropertyRelative("indice").objectReferenceValue = Tmp(textos[0]);
                if (textos.Length > 1)
                    item.FindPropertyRelative("nome").objectReferenceValue = Tmp(textos[1]);

                bot.SetActive(false);
                vazio.SetActive(false);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ================================================================== Barra de topo

        /// <summary>Onde a barra de topo do v2 é guardada para as outras telas reusarem.</summary>
        private const string CaminhoDaBarra = "Assets/_Projeto/Prefabs/UI_v2/Widgets/TopBar_v2.prefab";

        /// <summary>
        /// Troca a barra de topo desenhada no documento PLACA pela barra do v2.
        ///
        /// O lockup e as abas do v1 são visivelmente outra família — RACERS ao lado de PARTY, aba
        /// com aresta colorida. Com a loja e o passe ao lado do lobby novo, duas barras diferentes
        /// leem como dois jogos. Uma barra só, sempre no mesmo lugar, é a decisão estrutural nº 1
        /// da proposta; aqui ela deixa de ser intenção e vira o mesmo prefab em toda tela.
        /// </summary>
        private static void BarraDoV2(GameObject raiz, ProtoBuilder.Mapa m, string idDaTela)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CaminhoDaBarra);
            if (asset == null)
                return;

            // O cabeçalho do documento sai INTEIRO. Não é uma barra só: são grupos soltos no topo
            // (marca, abas, carteira), então quem os identifica é a posição, não a hierarquia.
            RectTransform[] antigos = m.NoTopo(130f);
            Transform pai = raiz.transform;
            int ordem = 0;

            if (antigos.Length > 0)
            {
                pai = antigos[0].parent != null ? antigos[0].parent : raiz.transform;
                ordem = antigos[0].GetSiblingIndex();

                foreach (RectTransform a in antigos)
                    if (a != null)
                        Object.DestroyImmediate(a.gameObject);
            }

            var barra = (GameObject)PrefabUtility.InstantiatePrefab(asset, pai);
            PrefabUtility.UnpackPrefabInstance(barra, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            barra.name = "TopBar";
            barra.transform.SetSiblingIndex(ordem);

            var r = (RectTransform)barra.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(0f, -104f);
            r.offsetMax = Vector2.zero;

            var nav = barra.GetComponent<NavBarUI>();
            if (nav != null)
            {
                var so = new SerializedObject(nav);
                so.FindProperty("idDestaTela").stringValue = idDaTela;
                so.ApplyModifiedPropertiesWithoutUndo();

                // O NavBarUI acerta o destaque no OnEnable, mas o PREFAB precisa nascer certo: é
                // ele que aparece na captura e no editor, e uma aba errada acesa faz duvidar do
                // resto da tela.
                SerializedProperty abas = so.FindProperty("abas");
                for (int i = 0; i < abas.arraySize; i++)
                {
                    SerializedProperty item = abas.GetArrayElementAtIndex(i);
                    bool ativa = item.FindPropertyRelative("id").stringValue == idDaTela;
                    Ligar(item.FindPropertyRelative("estadoAtivo").objectReferenceValue, ativa);
                    Ligar(item.FindPropertyRelative("estadoOcioso").objectReferenceValue, !ativa);
                }
            }
        }

        private static void BarraDeNavegacao(GameObject raiz, ProtoBuilder.Mapa m, string idDaTela)
        {
            RectTransform faixa = m.Caixas(1920f, 104f, 8f).FirstOrDefault();
            GameObject dono = faixa != null ? faixa.gameObject : raiz;

            var nav = dono.AddComponent<NavBarUI>();
            var so = new SerializedObject(nav);
            so.FindProperty("idDestaTela").stringValue = idDaTela;

            (string rotulo, string id)[] destinos =
            {
                ("LOBBY", "Lobby"), ("GARAGEM", "Garagem"), ("LOJA", "Loja"), ("PASSE", "Passe"),
            };

            SerializedProperty abas = so.FindProperty("abas");
            abas.arraySize = destinos.Length;

            for (int i = 0; i < destinos.Length; i++)
            {
                RectTransform aba = m.Dono(destinos[i].rotulo, 1);
                SerializedProperty item = abas.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("id").stringValue = destinos[i].id;

                if (aba == null)
                    continue;

                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(aba);
                (GameObject ativo, GameObject ocioso) = DoisEstados(aba, destinos[i].id == idDaTela);
                item.FindPropertyRelative("estadoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("estadoOcioso").objectReferenceValue = ocioso;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            // A barra do LOBBY é a canônica: sai daqui como prefab para as telas do documento PLACA
            // trocarem a delas. O lobby é a primeira tela construída, então quando as outras
            // chegarem o prefab já existe.
            if (idDaTela == "Lobby" && faixa != null && dono != raiz)
                PrefabUtility.SaveAsPrefabAsset(dono, CaminhoDaBarra);
        }

        // ================================================================== Peças

        /// <summary>
        /// Separa a aparência de um elemento em ATIVO e OCIOSO.
        ///
        /// O protótipo entrega a aparência de UM dos dois; a outra é derivada trocando só o
        /// preenchimento e o contorno — nunca o texto, nunca a caixa. É o que faz a troca de aba
        /// parecer um botão acendendo, e não dois widgets diferentes se revezando.
        /// </summary>
        private static (GameObject ativo, GameObject ocioso) DoisEstados(RectTransform alvo, bool veioAtivo)
        {
            GameObject original = Nó(alvo, veioAtivo ? "State_Active" : "State_Idle");
            original.transform.SetSiblingIndex(0);

            if (!MoverAparencia(alvo, original))
                Pintar(original, veioAtivo);

            // O rótulo entra no estado junto com a moldura. A cor do texto faz parte do estado: o
            // card SQUAD veio com texto TINTA sobre âmbar, e ao virar ocioso o mesmo texto escuro
            // sobre fundo escuro sumia da tela.
            foreach (Transform t in alvo.Cast<Transform>()
                                        .Where(t => t.gameObject != original && t.name != "Hit").ToList())
                t.SetParent(original.transform, false);

            GameObject espelho = Object.Instantiate(original, alvo);
            espelho.name = veioAtivo ? "State_Idle" : "State_Active";
            espelho.transform.SetSiblingIndex(1);
            Esticar((RectTransform)espelho.transform);
            Repintar(espelho, !veioAtivo);
            Recolorir(espelho);
            espelho.SetActive(false);

            return veioAtivo ? (original, espelho) : (espelho, original);
        }

        private static void Pintar(GameObject alvo, bool ativo)
        {
            var f = alvo.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(ativo ? Ambar : Escuro, 20f);
            f.DefinirContorno(ativo ? Tinta : Fio, ativo ? 3f : 2f);
        }

        private static void Repintar(GameObject alvo, bool ativo)
        {
            foreach (UIRoundedRect f in alvo.GetComponentsInChildren<UIRoundedRect>(true))
            {
                float raio = f.Raio;
                f.Definir(ativo ? Ambar : Escuro, raio);
                f.DefinirContorno(ativo ? Tinta : Fio, ativo ? 3f : 2f);
            }
        }

        /// <summary>
        /// Reescolhe a cor do texto pelo contraste com o fundo daquele estado.
        ///
        /// Trocar sempre para claro erraria na aba de amigos, cujo estado ativo é azul-marinho com
        /// texto claro; trocar sempre para escuro erraria no card âmbar. Quem decide é a luminância
        /// do preenchimento que o estado acabou de receber.
        /// </summary>
        private static void Recolorir(GameObject estado)
        {
            UIRoundedRect fundo = estado.GetComponentsInChildren<UIRoundedRect>(true).FirstOrDefault();
            if (fundo == null)
                return;

            Color c = fundo.CorDoPreenchimento;
            float luz = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * Mathf.Max(0.35f, c.a);
            Color tinta = luz > 0.42f ? new Color(21 / 255f, 22 / 255f, 28 / 255f, 1f)
                                      : new Color(0.79f, 0.82f, 0.96f, 1f);

            foreach (TextMeshProUGUI t in estado.GetComponentsInChildren<TextMeshProUGUI>(true))
                t.color = new Color(tinta.r, tinta.g, tinta.b, t.color.a);
        }

        /// <summary>Move os filhos atuais para dentro de um nó de estado, preservando a ordem.</summary>
        private static GameObject Agrupar(RectTransform alvo, string nome)
        {
            GameObject estado = Nó(alvo, nome);

            List<Transform> filhos = alvo.Cast<Transform>()
                .Where(t => t.gameObject != estado && t.name != "Hit").ToList();

            foreach (Transform f in filhos)
                f.SetParent(estado.transform, false);

            MoverGraficoProprio(alvo, estado);
            return estado;
        }

        /// <summary>
        /// Leva a aparência de um elemento para dentro do nó de estado: os filhos que são só
        /// desenho (sombra, moldura, preenchimento) e o Graphic do PRÓPRIO nó.
        ///
        /// Sem a segunda parte o estado só levava a sombra — o primeiro filho com forma —, o
        /// preenchimento ficava para trás, e o card do modo SQUAD continuava âmbar depois de o
        /// binder já ter trocado para SOLO. Texto nunca entra: ele é o conteúdo, não o estado.
        /// </summary>
        private static bool MoverAparencia(RectTransform alvo, GameObject estado)
        {
            List<Transform> visuais = alvo.Cast<Transform>()
                .Where(t => t.gameObject != estado
                         && t.name != "Hit"
                         && t.GetComponentInChildren<TextMeshProUGUI>(true) == null
                         && t.GetComponentInChildren<Graphic>(true) != null)
                .ToList();

            foreach (Transform v in visuais)
                v.SetParent(estado.transform, false);

            bool proprio = MoverGraficoProprio(alvo, estado);
            return visuais.Count > 0 || proprio;
        }

        /// <summary>Componente não se reparenta: copia para o estado e apaga do nó.</summary>
        private static bool MoverGraficoProprio(RectTransform alvo, GameObject estado)
        {
            var propria = alvo.GetComponent<UIRoundedRect>();
            if (propria == null)
                return false;

            UnityEditorInternal.ComponentUtility.CopyComponent(propria);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(estado);
            Object.DestroyImmediate(propria);

            var copia = estado.GetComponent<UIRoundedRect>();
            if (copia != null)
                copia.raycastTarget = false;

            return true;
        }

        /// <summary>Vaga sem ninguém: moldura tracejada e um rótulo. Não existe no protótipo.</summary>
        private static GameObject VagaDesenhada(RectTransform alvo, string nome, string rotulo, Color cor)
        {
            GameObject go = Tracejado(alvo, nome);

            GameObject t = Nó((RectTransform)go.transform, "Label");
            var tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = rotulo;
            tmp.fontSize = 13f;
            tmp.color = cor;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.raycastTarget = false;
            tmp.characterSpacing = 18f;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                tmp.font = fonte;

            return go;
        }

        private static GameObject Tracejado(RectTransform alvo, string nome)
        {
            GameObject go = Nó(alvo, nome);
            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.SemPreenchimento();
            f.DefinirRaio(18f);
            f.DefinirContorno(Apagado, 2f, 9f, 7f);
            return go;
        }

        /// <summary>Preenchimento + contorno numa peça só, desenhada ATRÁS do conteúdo.</summary>
        private static GameObject Moldura(RectTransform alvo, string nome, Color fundo,
                                          Color contorno, float espessura, float raio)
        {
            GameObject go = Nó(alvo, nome);
            go.transform.SetSiblingIndex(0);

            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(fundo, raio);
            f.DefinirContorno(contorno, espessura);
            return go;
        }

        /// <summary>Apaga a aparência de origem de um elemento, preservando texto e ícones.</summary>
        private static void LimparAparencia(RectTransform alvo)
        {
            var propria = alvo.GetComponent<UIRoundedRect>();
            if (propria != null)
                Object.DestroyImmediate(propria);

            List<Transform> visuais = alvo.Cast<Transform>()
                .Where(t => t.name != "Hit"
                         && t.GetComponentInChildren<TextMeshProUGUI>(true) == null
                         && t.GetComponentInChildren<Graphic>(true) != null
                         && t.childCount == 0)
                .ToList();

            foreach (Transform v in visuais)
            {
                // O quadradinho do avatar é conteúdo, não estado: só o que cobre a vaga inteira sai.
                var r = (RectTransform)v;
                if (r.rect.width >= alvo.rect.width - 8f && r.rect.height >= alvo.rect.height - 8f)
                    Object.DestroyImmediate(v.gameObject);
            }
        }

        private static GameObject Contorno(RectTransform alvo, string nome, Color cor,
                                           float espessura, float raio)
        {
            GameObject go = Nó(alvo, nome);
            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.SemPreenchimento();
            f.DefinirRaio(raio);
            f.DefinirContorno(cor, espessura);
            return go;
        }

        private static GameObject Preenchido(RectTransform alvo, string nome, Color cor)
        {
            GameObject go = Nó(alvo, nome);
            go.transform.SetSiblingIndex(0);
            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(cor, 16f);
            return go;
        }

        /// <summary>Véu do card bloqueado: escurece o conteúdo sem escondê-lo.</summary>
        private static GameObject Veu(RectTransform alvo, string nome)
        {
            GameObject go = Nó(alvo, nome);
            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(new Color(10 / 255f, 12 / 255f, 34 / 255f, 0.62f), 18f);
            return go;
        }

        private static GameObject Ponto(RectTransform modelo, string nome, Color cor)
        {
            RectTransform copia = Copiar(modelo, modelo.parent);
            if (copia == null)
                return null;

            copia.name = nome;
            foreach (UIRoundedRect f in copia.GetComponentsInChildren<UIRoundedRect>(true))
                f.Definir(cor, f.Raio);

            copia.gameObject.SetActive(false);
            return copia.gameObject;
        }

        // ================================================================== Listas do documento

        /// <summary>
        /// O documento desenha N cópias de um card para mostrar como a grade fica cheia. No jogo a
        /// lista tem tamanho desconhecido: a primeira cópia vira prefab, as outras somem e o
        /// contêiner ganha um Grid que as recria em runtime.
        /// </summary>
        private static void Grade(SerializedObject so, ProtoBuilder.Mapa m,
                                  string campoContainer, string campoPrefab, string caminho,
                                  float w, float h, float tol)
        {
            RectTransform[] cards = m.Caixas(w, h, tol);
            if (cards.Length == 0)
                return;

            Transform pai = cards[0].parent;
            Atribuir(so, campoContainer, pai);

            var grade = pai.gameObject.GetComponent<GridLayoutGroup>() ?? pai.gameObject.AddComponent<GridLayoutGroup>();
            grade.cellSize = new Vector2(w, h);
            grade.spacing = new Vector2(18f, 18f);
            grade.childAlignment = TextAnchor.UpperLeft;

            Prefabizar(cards, caminho, out GameObject prefab);
            Atribuir(so, campoPrefab, prefab);
        }

        /// <summary>Salva a primeira cópia como prefab e apaga todas da cena.</summary>
        private static void Prefabizar(RectTransform[] copias, string caminho, out GameObject prefab)
        {
            prefab = null;
            if (copias.Length == 0)
                return;

            RectTransform modelo = copias[0];
            float altura = modelo.rect.height > 4f ? modelo.rect.height : 60f;

            var elemento = modelo.gameObject.GetComponent<LayoutElement>() ?? modelo.gameObject.AddComponent<LayoutElement>();
            elemento.preferredHeight = altura;
            elemento.minHeight = altura;

            prefab = PrefabUtility.SaveAsPrefabAsset(modelo.gameObject, caminho);

            foreach (RectTransform c in copias)
                if (c != null)
                    Object.DestroyImmediate(c.gameObject);
        }

        /// <summary>Abas de texto puro (LOJA): vira botão com os dois estados, sem binder próprio.</summary>
        private static void AbasDeTexto(ProtoBuilder.Mapa m, params string[] rotulos)
        {
            for (int i = 0; i < rotulos.Length; i++)
            {
                RectTransform aba = m.Dono(rotulos[i], 1);
                if (aba == null)
                    continue;

                Clicavel(aba);
                DoisEstados(aba, i == 0);
            }
        }

        /// <summary>Bloco que contém um texto — o aviso inteiro, não só a linha.</summary>
        private static GameObject Bloco(ProtoBuilder.Mapa m, string texto)
        {
            RectTransform t = m.Dono(texto, 1, false);
            if (t == null)
                return null;

            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        /// <summary>Etiqueta que só existe no documento de design ("ESTADOS DE ERRO").</summary>
        private static void Esconder(ProtoBuilder.Mapa m, string texto)
        {
            RectTransform t = m.Texto(texto, false);
            if (t != null)
                t.gameObject.SetActive(false);
        }

        private static string Chave(TextMeshProUGUI t)
        {
            if (t == null)
                return "ajuste";

            var sb = new System.Text.StringBuilder();
            foreach (char c in t.text.ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);

            return sb.Length > 0 ? sb.ToString() : "ajuste";
        }

        // ================================================================== Utilidades

        /// <summary>
        /// Torna um elemento clicável de verdade.
        ///
        /// Tudo que o importador desenha nasce com <c>raycastTarget = false</c> — de propósito, para
        /// o HUD não roubar clique da pista. Um Button colocado por cima disso fica INERTE, e foi
        /// assim que a troca de pista do lobby parou de responder. O alvo de toque é um retângulo
        /// invisível cobrindo a caixa inteira.
        /// </summary>
        private static Button Clicavel(RectTransform alvo)
        {
            Button existente = alvo.GetComponent<Button>();
            if (existente != null)
                return existente;

            GameObject area = Nó(alvo, "Hit");
            var img = area.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            var b = alvo.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;

            // Afundar 6 px e apagar a sombra por 0,08 s — o toque de PLACA (§8 da proposta).
            if (alvo.GetComponent<UIPress>() == null)
                alvo.gameObject.AddComponent<UIPress>();

            return b;
        }

        private static Button ClicavelPorTexto(ProtoBuilder.Mapa m, string texto, bool exato = true)
        {
            RectTransform t = m.Texto(texto, exato);
            if (t == null)
                return null;

            // O botão é o menor nó com forma própria; o texto costuma ser filho dele.
            RectTransform alvo = t.GetComponentInChildren<UIRoundedRect>(true) != null
                ? t
                : t.parent as RectTransform;

            return alvo != null ? Clicavel(alvo) : null;
        }

        private static GameObject Nó(RectTransform pai, string nome)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            Esticar((RectTransform)go.transform);
            return go;
        }

        private static RectTransform Copiar(RectTransform origem, Transform destino)
        {
            if (origem == null || destino == null)
                return null;

            var copia = (RectTransform)Object.Instantiate(origem.gameObject, destino).transform;
            copia.anchorMin = origem.anchorMin;
            copia.anchorMax = origem.anchorMax;
            copia.pivot = origem.pivot;
            copia.offsetMin = origem.offsetMin;
            copia.offsetMax = origem.offsetMax;
            copia.localRotation = origem.localRotation;
            return copia;
        }

        /// <summary>A cópia que já está dentro do estado, quando o protótipo trouxe aquele caso.</summary>
        private static RectTransform DentroDe(GameObject estado, RectTransform alvo) =>
            alvo != null && estado != null && alvo.IsChildOf(estado.transform) ? alvo : null;

        private static void Esticar(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI Tmp(RectTransform r)
        {
            if (r == null)
                return null;

            return r.GetComponent<TextMeshProUGUI>() ?? r.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private static void Ligar(Object alvo, bool ativo)
        {
            if (alvo is GameObject go && go.activeSelf != ativo)
                go.SetActive(ativo);
        }

        private static void Atribuir(SerializedObject so, string campo, Object valor)
        {
            if (string.IsNullOrEmpty(campo))
                return;

            SerializedProperty p = so.FindProperty(campo);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                p.objectReferenceValue = valor;
        }
    }
}
