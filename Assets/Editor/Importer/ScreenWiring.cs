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

        /// <summary>Telas de corrida não animam a entrada: elas já estão lá quando a corrida começa.</summary>
        private static readonly HashSet<string> SemEntrada = new HashSet<string>
        {
            "Screen_RaceHUD_PC", "Screen_RaceMenu", "Screen_Loading",
        };

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
                case "Screen_RaceHUD_PC": HudDeCorrida(raiz, m); break;
            }

            Movimento(tela, raiz, m);
        }

        // ================================================================== Palco 3D

        /// <summary>
        /// Tira da UI a MAQUETE do palco: os retângulos "KART 3D", a plataforma e a luz que o
        /// protótipo desenha para representar o carro.
        ///
        /// Num documento HTML isso é a única forma de mostrar onde o carro vai; no jogo o carro é
        /// um modelo de verdade (`Turntable/PreviewCar`), enquadrado pelo <c>FrontendFlow</c>. Se
        /// as duas coisas convivem, a maquete fica NA FRENTE — foi por isso que a tela parecia um
        /// monte de caixa tracejada em vez de um kart.
        ///
        /// A zona franca (§4: nenhuma UI opaca em x=[520,1400] × y=[300,860]) deixa de ser uma
        /// intenção e passa a ser o que a tela realmente tem.
        /// </summary>
        private static void LimparPalco(ProtoBuilder.Mapa m, string marcaDaLegenda,
                                        float larguraDoChao, float alturaDoChao,
                                        float larguraDoAnel, float alturaDoAnel)
        {
            // O fundo de tela cheia é o que impedia o carro de aparecer.
            //
            // O `Canvas_UI` é ScreenSpaceOverlay: ele desenha SEMPRE por cima da cena 3D, então
            // qualquer pintura de 1920×1080 na tela tapa o palco por inteiro. Quem faz o fundo do
            // frontend é o `Canvas_Fundo` (ordem −100, atrás do carro) — este aqui é a cópia que o
            // protótipo precisava ter porque num HTML não existe cena 3D atrás.
            //
            // Só o Graphic do PRÓPRIO nó sai: os degradês de topo e base são filhos translúcidos e
            // continuam valendo, agora sobre o carro, que é onde eles dão profundidade.
            foreach (RectTransform tela in m.Caixas(1920f, 1080f, 4f))
                foreach (Graphic g in tela.GetComponents<Graphic>())
                    Object.DestroyImmediate(g);

            // Cada cartão de kart é o ancestral do rótulo "KART 3D" com a caixa inteira.
            foreach (RectTransform rotulo in m.Todos("KART 3D", false))
                Apagar(Envolve(rotulo, 200f, 100f) ?? rotulo);

            Apagar(Alvo(m, marcaDaLegenda, false));

            // Chão e anel: a plataforma do palco tem geometria própria na cena 3D.
            Despintar(m.Caixas(larguraDoChao, alturaDoChao, 8f));

            if (larguraDoAnel > 0f)
                Despintar(m.Caixas(larguraDoAnel, alturaDoAnel, 8f));

            // A luz de ambiente é quadrada e enorme (radial-gradient); some junto com o chão.
            foreach (RectTransform luz in m.Caixas(1180f, 1180f, 12f).Concat(m.Caixas(1000f, 1000f, 12f)))
                Apagar(luz);

            AdensarPaineis(m);
        }

        /// <summary>
        /// Fecha um pouco os painéis das telas que têm cena 3D atrás.
        ///
        /// O v2 pede `rgba(10,12,34,.82)` MAIS `blur(16)`. O blur é metade do efeito: ele apaga o
        /// desenho do fundo e deixa só a luz, e é o que permite ao painel ser translúcido sem
        /// competir com o que está atrás. O UGUI não tem blur, então com os mesmos 0,82 a oficina
        /// aparece nítida através do painel e o texto disputa leitura com uma caixa de ferramentas.
        ///
        /// Compensar na opacidade preserva a INTENÇÃO — o painel continua deixando a cena 3D
        /// respirar — sem pagar o preço de um desfoque em tela cheia.
        /// </summary>
        private static void AdensarPaineis(ProtoBuilder.Mapa m)
        {
            foreach (RectTransform r in m.Todas())
            {
                if (r == null || r.rect.width < 320f || r.rect.height < 220f)
                    continue;

                foreach (UIRoundedRect f in r.GetComponents<UIRoundedRect>())
                {
                    Color c = f.CorDoPreenchimento;
                    if (c.a < 0.55f || c.a > 0.97f)
                        continue;

                    f.Definir(new Color(c.r, c.g, c.b, Mathf.Min(0.96f, c.a + 0.09f)), f.Raio);
                }
            }
        }

        // ================================================================== Movimento

        /// <summary>
        /// O passe de animação (§8 da proposta). Roda depois do wiring, quando os estados já
        /// existem — é a diferença entre animar a barra e animar o estado PRONTO dela.
        ///
        /// Regra: animação reforça a ação, nunca a atrasa. Nada acima de 0,45 s no caminho de
        /// navegação, e nada que se mexa fora do momento em que aquilo importa.
        /// </summary>
        private static void Movimento(string tela, GameObject raiz, ProtoBuilder.Mapa m)
        {
            if (!SemEntrada.Contains(tela))
                EntradaDosPaineis(raiz);

            Escudo(raiz);
        }

        /// <summary>
        /// Os painéis entram em 0,22 s, escalonados por irmão.
        ///
        /// Só os blocos GRANDES: animar cada chip faria a tela inteira tremer na abertura, e o que
        /// o olho precisa perceber é a chegada das colunas, não de cada peça dentro delas.
        /// </summary>
        private static void EntradaDosPaineis(GameObject raiz)
        {
            // Os painéis não são filhos diretos da tela: o dump aninha por grupos de layout, e no
            // lobby a coluna do grupo está três níveis abaixo. O que identifica um painel é ter
            // FUNDO PRÓPRIO e ser grande — procurar por profundidade fixa achava zero.
            var painéis = raiz.GetComponentsInChildren<UIRoundedRect>(true)
                .Select(f => f.transform.parent as RectTransform)
                .Where(t => t != null && t.rect.width >= 280f && t.rect.height >= 140f
                                      && t.rect.width < 1900f)
                .Distinct()
                .ToList();

            foreach (RectTransform p in painéis)
            {
                if (p.GetComponent<UIAppear>() != null)
                    continue;

                // Um painel dentro de outro já animado entraria duas vezes, e o de dentro
                // começaria a se mexer antes de o de fora terminar — parece defeito, não animação.
                if (p.GetComponentsInParent<UIAppear>(true).Any())
                    continue;

                var a = p.gameObject.AddComponent<UIAppear>();
                var so = new SerializedObject(a);
                Definir(so, "duracao", 0.22f);
                Definir(so, "atrasoPorIrmao", 0.035f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// A barra de escudo pronta respira e varre; em recarga, não faz nada.
        ///
        /// A AUSÊNCIA de brilho é o sinal de indisponível — um ícone acinzentado exigiria comparar
        /// com a memória do ícone aceso, e a ausência de movimento se percebe pela visão
        /// periférica, que é a única disponível a 150 km/h.
        /// </summary>
        private static void Escudo(GameObject raiz)
        {
            Transform pronto = raiz.GetComponentsInChildren<Transform>(true)
                                   .FirstOrDefault(t => t.name == "Ready");

            if (pronto == null || pronto.GetComponent<UIGlowPulse>() != null)
                return;

            var glow = pronto.gameObject.AddComponent<UIGlowPulse>();
            var soGlow = new SerializedObject(glow);
            Definir(soGlow, "periodo", 1.8f);
            Definir(soGlow, "raioMin", 16f);
            Definir(soGlow, "raioMax", 34f);
            soGlow.ApplyModifiedPropertiesWithoutUndo();

            var sweep = pronto.gameObject.AddComponent<UIShineSweep>();
            var soSweep = new SerializedObject(sweep);
            Definir(soSweep, "periodo", 2.4f);
            soSweep.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Definir(SerializedObject so, string campo, float valor)
        {
            SerializedProperty p = so.FindProperty(campo);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
                p.floatValue = valor;
        }

        // ================================================================== HUD de corrida

        /// <summary>
        /// Liga a pele fiel do HUD aos binders que já existem.
        ///
        /// O centro da tela fica sem nada de propósito (§6 da proposta): informação em cima,
        /// vitais no canto inferior ESQUERDO e poder no inferior DIREITO, diagonalmente opostos —
        /// a mesma separação das mãos no controle.
        /// </summary>
        private static void HudDeCorrida(GameObject raiz, ProtoBuilder.Mapa m)
        {
            // O protótipo desenha o gameplay: céu em degradê, faixa verde da pista e o rótulo
            // "GAMEPLAY 3D". No jogo quem pinta isso é a CÂMERA — deixar os três seria um painel
            // opaco parado no meio da tela, justo onde o design manda não ter nada.
            //
            // Some o DESENHO, não o nó: a raiz da tela e a faixa do céu são pais de metade do HUD,
            // e destruí-las levaria junto a placa de volta, o cluster vital e o slot de poder.
            Apagar(m.Texto("GAMEPLAY 3D", false));
            Despintar(m.Caixas(1920f, 1080f, 4f));
            Despintar(m.Caixas(1920f, 562f, 6f));
            Despintar(m.Caixas(640f, 520f, 6f));

            // ---- informação
            var hud = raiz.AddComponent<PartyRacers.UI.Race.RaceHUDUI>();
            var so = new SerializedObject(hud);
            Atribuir(so, "textoVolta", Tmp(m.Texto("VOLTA 2/3")));
            Atribuir(so, "textoTempo", Tmp(m.Texto("01:12.480")));
            Atribuir(so, "textoUltimaVolta", Tmp(m.Texto("ÚLT", false)));
            Atribuir(so, "textoMelhorVolta", Tmp(m.Texto("MELH", false)));

            RectTransform chipUlt = Alvo(m, "ÚLT", false);
            if (chipUlt != null)
                Atribuir(so, "chipUltimaVolta", chipUlt.gameObject);

            so.ApplyModifiedPropertiesWithoutUndo();

            Classificacao(raiz, m);
            ClusterVital(raiz, m);
            SlotDePoder(raiz, m);
            Toasts(raiz, m);
        }

        private static void Classificacao(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.StandingsV2UI>();
            var so = new SerializedObject(ui);

            // Cinco linhas comuns (328×31) e a do jogador (340×54), que é mais alta de propósito:
            // "esta linha é a minha" precisa ser vista sem ler.
            var linhas = m.Caixas(328f, 31f, 3f).Concat(m.Caixas(340f, 54f, 3f))
                          .OrderBy(r => -r.anchoredPosition.y).ToList();

            SerializedProperty lista = so.FindProperty("linhas");
            lista.arraySize = linhas.Count;

            for (int i = 0; i < linhas.Count; i++)
            {
                RectTransform linha = linhas[i];
                SerializedProperty item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("raiz").objectReferenceValue = linha.gameObject;

                bool local = linha.rect.height > 40f;
                GameObject atual = Agrupar(linha, local ? "IsLocal" : "Other");

                RectTransform[] textos = m.TextosEm(linha);
                string sufixo = local ? "Local" : "Outro";
                if (textos.Length > 0) item.FindPropertyRelative("posicao" + sufixo).objectReferenceValue = Tmp(textos[0]);
                if (textos.Length > 1) item.FindPropertyRelative("nome" + sufixo).objectReferenceValue = Tmp(textos[1]);
                if (textos.Length > 2) item.FindPropertyRelative("tempo" + sufixo).objectReferenceValue = Tmp(textos[2]);

                item.FindPropertyRelative(local ? "estadoLocal" : "estadoOutro").objectReferenceValue = atual;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ClusterVital(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.VitalClusterUI>();
            var so = new SerializedObject(ui);

            // Vida: 5 blocos de 20 HP. Blocos porque são CONTÁVEIS de relance — barra contínua
            // exige medir, e a 150 km/h ninguém mede.
            RectTransform[] blocos = m.Caixas(78f, 32f, 4f);
            SerializedProperty segmentos = so.FindProperty("segmentosDeVida");
            segmentos.arraySize = blocos.Length;

            for (int i = 0; i < blocos.Length; i++)
            {
                SerializedProperty item = segmentos.GetArrayElementAtIndex(i);
                Graphic cheio = blocos[i].GetComponentInChildren<Graphic>(true);
                item.FindPropertyRelative("cheio").objectReferenceValue = cheio as Image;

                // Ferido e vazio não existem no protótipo: são o mesmo bloco em outra cor.
                GameObject ferido = Preenchido(blocos[i], "Ferido", new Color(1f, 176 / 255f, 32 / 255f, 1f));
                GameObject vazio = Preenchido(blocos[i], "Vazio", new Color(1f, 1f, 1f, 0.10f));
                item.FindPropertyRelative("ferido").objectReferenceValue = ferido.GetComponent<Graphic>() as Image;
                item.FindPropertyRelative("vazio").objectReferenceValue = vazio;
                ferido.SetActive(false);
                vazio.SetActive(false);
            }

            Atribuir(so, "valorDeVida", Tmp(m.Texto("100")));

            RectTransform rotuloVida = m.Texto("VIDA");
            if (rotuloVida != null && rotuloVida.parent != null)
                Atribuir(so, "raizVida", rotuloVida.parent.gameObject);

            // Escudo: a barra É o indicador. Sem botão e sem ícone — a ausência de brilho é o
            // sinal de indisponível, e ausência de movimento se percebe pela visão periférica.
            RectTransform rotuloEscudo = m.Texto("ESCUDO");
            if (rotuloEscudo != null && rotuloEscudo.parent != null)
            {
                var barra = (RectTransform)rotuloEscudo.parent;
                Atribuir(so, "raizEscudo", barra.gameObject);

                GameObject pronto = Agrupar(barra, "Ready");
                GameObject ativo = Object.Instantiate(pronto, barra);
                ativo.name = "Active";
                ativo.SetActive(false);

                GameObject recarga = Object.Instantiate(pronto, barra);
                recarga.name = "Cooling";
                recarga.SetActive(false);

                Atribuir(so, "estadoPronto", pronto);
                Atribuir(so, "estadoAtivo", ativo);
                Atribuir(so, "estadoRecarga", recarga);

                Atribuir(so, "textoDaChapinhaAtivo", Tmp(m.Texto("PRONTO")));
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SlotDePoder(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.PowerSlotUI>();
            var so = new SerializedObject(ui);

            RectTransform nome = m.Texto("FOGUETE");
            if (nome != null)
            {
                Atribuir(so, "nomeDoPoder", Tmp(nome));
                if (nome.parent != null)
                    Atribuir(so, "cartaoDoNome", nome.parent.gameObject);
            }

            RectTransform tecla = m.Texto("E");
            if (tecla != null)
                Atribuir(so, "dicaDeTecla", tecla.gameObject);

            // O slot: 124×124 no canto inferior direito.
            RectTransform slot = m.Caixas(124f, 124f, 10f).FirstOrDefault();
            if (slot != null)
            {
                GameObject cheio = Agrupar(slot, "Filled");
                Atribuir(so, "estadoCheio", cheio);
                Atribuir(so, "iconeCheio", cheio.GetComponentsInChildren<Image>(true)
                                                .FirstOrDefault(i => i.sprite != null));

                GameObject vazio = Tracejado(slot, "Empty");
                Atribuir(so, "estadoVazio", vazio);
                vazio.SetActive(false);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Toasts(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.ToastNotificationUI>();
            var so = new SerializedObject(ui);

            // O cartão do toast está DOIS níveis acima do rótulo (texto → linha → cartão), e nem
            // sempre a mesma quantidade. Subir até o ancestral que tem a caixa inteira é o único
            // critério estável: parar antes leva só o texto, e o toast nasce sem fundo nem ícone.
            RectTransform modelo = Envolve(m.Texto("pegou um foguete", false), 300f, 40f);
            if (modelo == null)
                return;

            // Máximo 3 simultâneos (§8): as outras duas pilhas nascem acima da primeira.
            SerializedProperty slots = so.FindProperty("slots");
            slots.arraySize = 3;
            float altura = modelo.rect.height + 8f;

            // O CanvasGroup entra no MODELO antes das cópias: `AddComponent` seguido de
            // `GetComponent` no mesmo passe do editor não enxerga o componente recém-criado, então
            // adicionar em cada cópia deixava as duas últimas sem grupo.
            var grupoModelo = modelo.GetComponent<CanvasGroup>();
            if (grupoModelo == null)
                grupoModelo = modelo.gameObject.AddComponent<CanvasGroup>();

            grupoModelo.blocksRaycasts = false;

            for (int i = 0; i < 3; i++)
            {
                RectTransform alvo = i == 0 ? modelo : Copiar(modelo, modelo.parent);
                if (alvo == null)
                    continue;

                alvo.name = "Toast_" + (i + 1);
                if (i > 0)
                    alvo.anchoredPosition += new Vector2(0f, altura * i);

                CanvasGroup grupo = alvo.GetComponent<CanvasGroup>();

                SerializedProperty item = slots.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("raiz").objectReferenceValue = alvo.gameObject;
                item.FindPropertyRelative("grupo").objectReferenceValue = grupo;
                item.FindPropertyRelative("texto").objectReferenceValue =
                    alvo.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
                item.FindPropertyRelative("icone").objectReferenceValue =
                    alvo.GetComponentsInChildren<Image>(true).FirstOrDefault();

                alvo.gameObject.SetActive(false);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
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

                RectTransform botao = Alvo(m, grupos[i].rotulo);
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

            RectTransform aviso = Alvo(m, "ainda correndo", false);
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

            RectTransform conexao = Alvo(m, "CONEXÃO", false);
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
            LimparPalco(m, "gira 6°/s", 1080f, 250f, 840f, 180f);

            var ui = raiz.AddComponent<PublicLobbyScreenUI>();
            var so = new SerializedObject(ui);

            // ---- modos
            SerializedProperty cards = so.FindProperty("cardsDeModo");
            cards.arraySize = 3;
            string[] rotulos = { "SOLO", "DUO", "SQUAD" };

            for (int i = 0; i < 3; i++)
            {
                RectTransform card = Alvo(m, rotulos[i], true)?.parent as RectTransform;
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
            RectTransform chipPronto = Alvo(m, "PRONTO");
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

                // "Nenhum amigo aqui": sem isso a aba STEAM vazia parece a lista ter falhado.
                so.FindProperty("avisoDeListaVazia").objectReferenceValue =
                    Aviso(conteudo, "NINGUÉM POR AQUI");
            }

            RectTransform abaJogo = Alvo(m, "NO JOGO");
            RectTransform abaSteam = Alvo(m, "STEAM");

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
            RectTransform buscar = Alvo(m, "BUSCAR PARTIDA");

            if (cancelar != null)
            {
                RectTransform caixa = Alvo(m, "CANCELAR");
                so.FindProperty("btnPronto").objectReferenceValue = Clicavel(caixa ?? cancelar);

                // "CANCELAR" no protótipo é o botão de desmarcar-se; ele alterna com "ESTOU PRONTO".
                // Os dois rótulos existem e o binder liga um.
                if (caixa != null)
                {
                    (GameObject pronto, GameObject aguardando) = DoisEstados(caixa, true);
                    Rotular(aguardando, "ESTOU PRONTO");
                    so.FindProperty("btnProntoEstadoPronto").objectReferenceValue = pronto;
                    so.FindProperty("btnProntoEstadoAguardando").objectReferenceValue = aguardando;
                }
            }

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
            RectTransform indisponivel = Alvo(m, "NO GRUPO");

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
            LimparPalco(m, "lerp 0.45s", 900f, 230f, 0f, 0f);
            Apagar(Alvo(m, "CÂMERA ·", false));

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

                RectTransform aba = Alvo(m, categorias[i].rotulo);
                if (aba == null)
                    continue;

                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(aba);
                (GameObject ativo, GameObject ocioso) = DoisEstados(aba, categorias[i].rotulo == "RODAS");
                item.FindPropertyRelative("estadoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("estadoOcioso").objectReferenceValue = ocioso;
            }

            // ---- grade de cards
            RectTransform[] cards = m.Caixas(158f, 168f, 6f);
            if (cards.Length > 0)
                Gradear(cards, 158f, 168f, 14f, 4);

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

                // O retângulo colorido do card é um `UIRoundedRect`, não um `Image` — procurar
                // Image ali devolvia nulo e o card ficava sem preview nenhum. A foto entra num
                // Image PRÓPRIO, por cima da forma: assim a moldura colorida continua servindo de
                // fundo quando a peça ainda não tem foto gerada.
                item.FindPropertyRelative("preview").objectReferenceValue = AreaDePreview(card);

                equipado.SetActive(false);
                selecionado.SetActive(false);
                bloqueado.SetActive(false);
            }

            // A grade tem 12 células e o catálogo de modelos tem 15: sem paginação os três últimos
            // ficam inalcançáveis. O protótipo não desenhou esse controle, então ele é montado aqui
            // com os mesmos tokens — é a única peça da garagem que não vem do design.
            if (cards.Length > 0)
                Paginacao(so, (RectTransform)cards[0].parent);

            // "PREVIEW" era o placeholder do protótipo; agora cada card mostra a FOTO da peça,
            // gerada pelo `PreviewBaker`. O rótulo sai — deixá-lo sobre a foto seria pior que
            // antes, porque cobriria justamente o que ele prometia.
            foreach (RectTransform p in m.Todos("PREVIEW", false))
                Apagar(p);

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

                RectTransform chip = Alvo(m, etapas[i]);
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

        /// <summary>
        /// O ELEMENTO ao qual um rótulo pertence — o botão, a aba, o chip.
        ///
        /// `Dono(texto, 1)` sobe um nível sempre, e isso erra quando o rótulo JÁ É o elemento. Em
        /// Configurações cada botão de grupo é um nó só, com texto e fundo: subir um nível devolvia
        /// a COLUNA inteira, e o passe de estados engolia os cinco botões de uma vez.
        ///
        /// O teste é exato: o construtor põe o TextMeshPro no próprio nó apenas quando ele não tem
        /// fundo. Texto no nó = rótulo solto, sobe; texto num filho `Label` = o nó é a caixa.
        /// </summary>
        private static RectTransform Alvo(ProtoBuilder.Mapa m, string rotulo, bool exato = true)
        {
            RectTransform t = m.Texto(rotulo, exato);
            if (t == null)
                return null;

            return t.GetComponent<TextMeshProUGUI>() != null ? t.parent as RectTransform : t;
        }

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

            // Último irmão: no UGUI quem vem depois desenha por cima. Mantida na posição original
            // do cabeçalho do documento, a grade da garagem passava por cima dela e cortava a
            // marca ao meio. A barra é o teto da tela — nada pode cobri-la.
            barra.transform.SetAsLastSibling();
            _ = ordem;

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
                RectTransform aba = Alvo(m, destinos[i].rotulo);
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

        /// <summary>Anterior · "1 / 2" · próxima, logo abaixo da grade de cards.</summary>
        private static void Paginacao(SerializedObject so, RectTransform grade)
        {
            Transform pai = grade.parent != null ? grade.parent : grade;

            var bloco = new GameObject("Paginacao", typeof(RectTransform));
            bloco.transform.SetParent(pai, false);

            var r = (RectTransform)bloco.transform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 0f);
            r.pivot = new Vector2(0.5f, 0f);
            r.offsetMin = new Vector2(24f, 20f);
            r.offsetMax = new Vector2(-24f, 20f + 52f);

            Button anterior = SetaDePagina(r, "Btn_Anterior", "‹", 0f);
            Button proxima = SetaDePagina(r, "Btn_Proxima", "›", 1f);

            var indicador = new GameObject("Indicador", typeof(RectTransform));
            indicador.transform.SetParent(r, false);
            Esticar((RectTransform)indicador.transform);

            var tmp = indicador.AddComponent<TextMeshProUGUI>();
            tmp.text = "1 / 1";
            tmp.fontSize = 15f;
            tmp.color = new Color(0.60f, 0.63f, 0.85f, 1f);
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.raycastTarget = false;
            tmp.characterSpacing = 12f;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                tmp.font = fonte;

            Atribuir(so, "btnPaginaAnterior", anterior);
            Atribuir(so, "btnProximaPagina", proxima);
            Atribuir(so, "indicadorDePagina", tmp);
            Atribuir(so, "blocoDePaginacao", bloco);
        }

        private static Button SetaDePagina(RectTransform pai, string nome, string glifo, float lado)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = new Vector2(lado, 0.5f);
            r.pivot = new Vector2(lado, 0.5f);
            r.anchoredPosition = Vector2.zero;
            r.sizeDelta = new Vector2(52f, 44f);

            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(Escuro, 16f);
            f.DefinirContorno(Fio, 2f);

            var rotulo = new GameObject("Label", typeof(RectTransform));
            rotulo.transform.SetParent(r, false);
            Esticar((RectTransform)rotulo.transform);

            var tmp = rotulo.AddComponent<TextMeshProUGUI>();
            tmp.text = glifo;
            tmp.fontSize = 26f;
            tmp.color = new Color(0.79f, 0.82f, 0.96f, 1f);
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.raycastTarget = false;

            TMP_FontAsset fonte = CssKit.Fonte("Archivo", 800);
            if (fonte != null)
                tmp.font = fonte;

            return Clicavel(r);
        }

        /// <summary>
        /// Área da foto do item: um `Image` esticado sobre o topo do card.
        ///
        /// A altura é a do card menos a faixa de nome e raridade, medida no próprio card em vez de
        /// fixada em pixels — os cards de categorias diferentes têm alturas diferentes.
        /// </summary>
        private static Image AreaDePreview(RectTransform card)
        {
            var go = new GameObject("Preview", typeof(RectTransform));
            go.transform.SetParent(card, false);
            go.transform.SetSiblingIndex(1);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(4f, 46f);
            r.offsetMax = new Vector2(-4f, -4f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>
        /// Troca posições absolutas por uma grade de verdade.
        ///
        /// Os cards vinham com a posição que tinham no protótipo. Assim que o painel passou a
        /// esticar com a janela, cada card seguiu a própria âncora e as fileiras se atropelaram.
        /// Com Grid Layout as células são calculadas a partir do contêiner e a grade se comporta
        /// como grade — inclusive quando a página troca e o número de cards muda.
        /// </summary>
        private static void Gradear(RectTransform[] cards, float largura, float altura,
                                    float espaco, int colunas)
        {
            var pai = (RectTransform)cards[0].parent;

            // O contêiner passa a crescer do TOPO para baixo, preservando onde ele já estava.
            //
            // Trocar a âncora sem recalcular os offsets move o bloco: os valores são relativos à
            // âncora, e o mesmo número significa outra posição depois da troca. Medir o topo atual
            // antes e reaplicá-lo é o que mantém a grade abaixo da segunda linha de abas, onde o
            // design a colocou.
            float alturaNecessaria = Mathf.Ceil(cards.Length / (float)colunas) * (altura + espaco);

            var noPai = pai.parent as RectTransform;
            float topoAtual = noPai != null
                ? noPai.rect.yMax - (pai.anchoredPosition.y + pai.rect.yMax)
                : 0f;

            pai.anchorMin = new Vector2(0f, 1f);
            pai.anchorMax = new Vector2(1f, 1f);
            pai.pivot = new Vector2(0.5f, 1f);
            // A altura é medida A PARTIR DO TOPO do bloco, não do topo do painel: somar as duas
            // referências encolhia a grade em ~170 px e a última fileira ficava cortada pela
            // metade, parecendo que faltavam cards.
            pai.offsetMax = new Vector2(pai.offsetMax.x, -topoAtual);
            pai.offsetMin = new Vector2(pai.offsetMin.x, -topoAtual - alturaNecessaria);

            var grade = pai.GetComponent<GridLayoutGroup>() ?? pai.gameObject.AddComponent<GridLayoutGroup>();
            grade.cellSize = new Vector2(largura, altura);
            grade.spacing = new Vector2(espaco, espaco);
            grade.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grade.constraintCount = colunas;
            grade.childAlignment = TextAnchor.UpperLeft;

            foreach (RectTransform c in cards)
                if (c.GetComponent<LayoutElement>() == null)
                    c.gameObject.AddComponent<LayoutElement>();
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
                RectTransform aba = Alvo(m, rotulos[i]);
                if (aba == null)
                    continue;

                Clicavel(aba);
                DoisEstados(aba, i == 0);
            }
        }

        /// <summary>Bloco que contém um texto — o aviso inteiro, não só a linha.</summary>
        private static GameObject Bloco(ProtoBuilder.Mapa m, string texto)
        {
            RectTransform t = Alvo(m, texto, false);
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

        /// <summary>Troca o texto de um estado clonado — o par PRONTO/AGUARDANDO é o mesmo botão.</summary>
        private static void Rotular(GameObject estado, string texto)
        {
            TextMeshProUGUI t = estado.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
                t.text = texto;
        }

        /// <summary>Aviso de lista vazia, centrado no contêiner e desligado por padrão.</summary>
        private static GameObject Aviso(Transform pai, string texto)
        {
            var go = new GameObject("Aviso_Vazio", typeof(RectTransform));
            go.transform.SetParent(pai, false);
            Esticar((RectTransform)go.transform);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = texto;
            tmp.fontSize = 14f;
            tmp.color = Apagado;
            tmp.alignment = TextAlignmentOptions.Midline;
            tmp.raycastTarget = false;
            tmp.characterSpacing = 16f;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                tmp.font = fonte;

            // O Layout Group do pai controlaria a altura deste nó e empurraria a lista.
            var elemento = go.AddComponent<LayoutElement>();
            elemento.ignoreLayout = true;

            go.SetActive(false);
            return go;
        }

        /// <summary>Some com um nó de maquete. Nada de desligar: no HUD ele ainda custaria batch.</summary>
        private static void Apagar(RectTransform alvo)
        {
            if (alvo != null)
                Object.DestroyImmediate(alvo.gameObject);
        }

        /// <summary>
        /// Tira o desenho de um nó preservando os filhos.
        ///
        /// Serve para o que é FUNDO DE MAQUETE e ao mesmo tempo pai de conteúdo real. Só o Graphic
        /// do próprio nó sai, e os filhos que são puro desenho (o degradê num nó separado) vão
        /// junto — texto e qualquer coisa com filhos ficam.
        /// </summary>
        private static void Despintar(IEnumerable<RectTransform> alvos)
        {
            foreach (RectTransform alvo in alvos.ToList())
            {
                if (alvo == null)
                    continue;

                foreach (Graphic g in alvo.GetComponents<Graphic>())
                    Object.DestroyImmediate(g);

                foreach (Transform filho in alvo.Cast<Transform>().ToList())
                {
                    if (filho.childCount == 0
                        && filho.GetComponent<TextMeshProUGUI>() == null
                        && filho.GetComponent<Graphic>() != null)
                        Object.DestroyImmediate(filho.gameObject);
                }
            }
        }

        /// <summary>Sobe a hierarquia até o primeiro ancestral com pelo menos a caixa pedida.</summary>
        private static RectTransform Envolve(RectTransform de, float larguraMinima, float alturaMinima)
        {
            RectTransform r = de;
            for (int i = 0; i < 6 && r != null; i++)
            {
                if (r.rect.width >= larguraMinima && r.rect.height >= alturaMinima)
                    return r;

                r = r.parent as RectTransform;
            }

            return null;
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
