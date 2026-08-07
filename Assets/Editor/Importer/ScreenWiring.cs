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

            // O bloco de posição não estava ligado a nada: o "4" e o "+2 POSIÇÕES" eram o texto do
            // protótipo, parado na tela a corrida inteira.
            Atribuir(so, "textoPosicao", Tmp(m.Texto("4")));
            Atribuir(so, "textoTotalDeCorredores", Tmp(m.Texto("DE 12")));
            Atribuir(so, "textoIntervalo", Intervalo(m));

            // "ÚLT" é o TEMPO DA ÚLTIMA VOLTA; "última volta" é o aviso de que esta é a volta final.
            // Ligar um no outro fazia o binder esconder o tempo da última volta em toda volta que
            // não fosse a derradeira — ou seja, quase sempre. O aviso ganha chapinha própria.
            Atribuir(so, "chipUltimaVolta", ChapinhaDeUltimaVolta(m));

            Fundo(m.Caixa(36f, 32f, 227.6f, 81f), 20f, 14f, 8f);
            RealcarTotal(m);

            Atribuir(so, "chuteDaPosicao", Chute(m.Caixa(34.6f, 30.4f, 95.4f, 84.2f), 1.2f, false));
            Atribuir(so, "chuteDaVolta", Chute(m.Caixa(720.5f, 32f, 212f, 74f), 1.1f, false));

            so.ApplyModifiedPropertiesWithoutUndo();

            Classificacao(raiz, m);
            ClusterVital(raiz, m);
            SlotDePoder(raiz, m);
            Toasts(raiz, m);
        }

        /// <summary>
        /// A chapinha "+2 POSIÇÕES" vira o mostrador de intervalo.
        ///
        /// O texto do protótipo era um saldo do que já aconteceu; o valor que o piloto precisa é
        /// quantos segundos faltam para o carro da frente. Como o número é maior e muda toda hora,
        /// a caixa ganha corpo próprio e passa a caber "LÍDER" sem quebrar linha.
        /// </summary>
        /// <summary>Põe um <see cref="UIKick"/> num bloco, para o binder poder empurrá-lo.</summary>
        private static UIKick Chute(RectTransform alvo, float pico, bool piscar)
        {
            if (alvo == null)
                return null;

            UIKick k = alvo.GetComponent<UIKick>() ?? alvo.gameObject.AddComponent<UIKick>();

            var so = new SerializedObject(k);
            so.FindProperty("pico").floatValue = pico;
            so.FindProperty("piscar").boolValue = piscar;
            if (piscar)
                so.FindProperty("corDoPisca").colorValue = new Color(1f, 0.42f, 0.48f, 1f);
            so.ApplyModifiedPropertiesWithoutUndo();

            return k;
        }

        /// <summary>Placa escura por trás de um bloco solto, para ele não boiar sobre o cenário.</summary>
        private static void Fundo(RectTransform bloco, float raio, float folgaX, float folgaY)
        {
            if (bloco == null)
                return;

            var go = new GameObject("Fundo", typeof(RectTransform));
            go.transform.SetParent(bloco, false);
            go.transform.SetSiblingIndex(0);

            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(-folgaX, -folgaY);
            r.offsetMax = new Vector2(folgaX, folgaY);

            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(new Color(0.04f, 0.05f, 0.13f, 0.72f), raio);
            f.DefinirContorno(Fio, 1.5f);
            f.DefinirRaio(raio, raio);
        }

        /// <summary>O "DE 12" vinha quase invisível; sobre o cenário ele sumia de vez.</summary>
        private static void RealcarTotal(ProtoBuilder.Mapa m)
        {
            TextMeshProUGUI t = Tmp(m.Caixa(142.6f, 52.5f, 121f, 13f));
            if (t == null)
                return;

            t.color = new Color(0.79f, 0.82f, 0.96f, 0.85f);
            t.fontSize = 13f;
            t.characterSpacing = 10f;
        }

        /// <summary>
        /// Chapinha vermelha de ÚLTIMA VOLTA, abaixo da placa de tempo.
        ///
        /// O documento não desenhou este aviso — ele existe no §8 do handoff e é a única coisa que
        /// muda o jeito de correr a volta final. Nasce desligada; quem a acende é o binder.
        /// </summary>
        private static GameObject ChapinhaDeUltimaVolta(ProtoBuilder.Mapa m)
        {
            RectTransform placa = m.Caixa(720.5f, 32f, 479f, 116f);
            if (placa == null)
                return null;

            var go = new GameObject("Chip_UltimaVolta", typeof(RectTransform));
            go.transform.SetParent(placa, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0.5f, 0f);
            r.anchorMax = new Vector2(0.5f, 0f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -8f);
            r.sizeDelta = new Vector2(232f, 34f);

            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(new Color(1f, 0.30f, 0.43f, 0.92f), 17f);
            f.DefinirContorno(new Color(1f, 1f, 1f, 0.35f), 1.5f);
            f.DefinirRaio(17f, 17f);

            var rotulo = new GameObject("Label", typeof(RectTransform));
            rotulo.transform.SetParent(r, false);
            Esticar((RectTransform)rotulo.transform);

            var t = rotulo.AddComponent<TextMeshProUGUI>();
            t.text = "ÚLTIMA VOLTA";
            t.fontSize = 16f;
            t.characterSpacing = 12f;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            TMP_FontAsset fonte = CssKit.Fonte("Archivo", 800);
            if (fonte != null)
                t.font = fonte;

            go.AddComponent<UIPulse>();
            go.SetActive(false);
            return go;
        }

        private static TextMeshProUGUI Intervalo(ProtoBuilder.Mapa m)
        {
            // A coluna à direita do emblema: "DE 12" em cima, intervalo embaixo. As duas caixas são
            // reancoradas porque o valor novo é mais largo que o do desenho — sem isso, "LÍDER"
            // escorregava para cima do número da posição.
            RectTransform coluna = m.Caixa(142.6f, 52.5f, 121f, 40f);
            RectTransform total = m.Caixa(142.6f, 52.5f, 121f, 13f);
            RectTransform intervalo = m.Caixa(142.6f, 69.5f, 121f, 23f);

            if (coluna != null)
                coluna.sizeDelta = new Vector2(150f, coluna.sizeDelta.y);

            Fixar(total, 0f, 0f, 150f, 15f);
            Fixar(intervalo, 0f, -19f, 150f, 26f);

            TextMeshProUGUI t = Tmp(intervalo);
            if (t == null)
                return null;

            t.text = "LÍDER";
            t.fontSize = 22f;
            t.characterSpacing = 4f;
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableWordWrapping = false;

            TextMeshProUGUI rotulo = Tmp(total);
            if (rotulo != null)
            {
                rotulo.alignment = TextAlignmentOptions.MidlineLeft;
                rotulo.enableWordWrapping = false;
            }

            return t;
        }

        /// <summary>Prende uma caixa ao canto superior esquerdo do pai, com tamanho fixo.</summary>
        private static void Fixar(RectTransform alvo, float x, float y, float largura, float altura)
        {
            if (alvo == null)
                return;

            alvo.anchorMin = new Vector2(0f, 1f);
            alvo.anchorMax = new Vector2(0f, 1f);
            alvo.pivot = new Vector2(0f, 1f);
            alvo.anchoredPosition = new Vector2(x, y);
            alvo.sizeDelta = new Vector2(largura, altura);

            foreach (Transform filho in alvo)
                if (filho is RectTransform r && filho.GetComponent<TextMeshProUGUI>() != null)
                    Esticar(r);
        }

        /// <summary>
        /// Classificação: cinco faixas de topo e a do jogador, cada uma com quatro colunas.
        ///
        /// O protótipo desenhou três colunas (posição, nome, um número solto). O jogo precisa de
        /// quatro: melhor volta AO LADO DO NOME e intervalo na borda. A caixa do nome encolhe e as
        /// duas colunas de tempo entram no espaço que sobrou — as duas são numéricas e alinhadas à
        /// direita, então lêem-se em coluna sem precisar de régua.
        ///
        /// O bloco também ganha um fundo. Sem ele o texto ficava sobre o céu da pista, e "branco
        /// sobre azul claro" é o mesmo que não ter classificação nenhuma.
        /// </summary>
        private static void Classificacao(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.StandingsV2UI>();
            var so = new SerializedObject(ui);

            RectTransform bloco = m.Caixa(1556f, 32f, 328f, 230f);
            if (bloco == null)
                return;

            Atribuir(so, "painel", FundoDaClassificacao(bloco));

            // As seis faixas nas coordenadas do documento: y, altura, e se é a do jogador. A ordem
            // aqui é a que o BINDER usa — cinco do topo e, por último, a do jogador. No desenho a
            // faixa dele é a quarta porque o exemplo mostra um jogador em 4º; no jogo ele pode
            // estar em 16º, e uma faixa "top 5" no meio da lista mentiria sobre a ordem.
            (float y, float h, bool local)[] fileiras =
            {
                ( 32f, 31f, false),
                ( 68f, 31f, false),
                (104f, 31f, false),
                (195f, 31f, false),
                (231f, 31f, false),
                (140f, 50f, true),
            };

            SerializedProperty lista = so.FindProperty("linhas");
            lista.arraySize = fileiras.Length;

            for (int i = 0; i < fileiras.Length; i++)
            {
                (float dy, float dh, bool local) = fileiras[i];

                RectTransform linha = m.Caixa(1556f, dy, 328f, dh);
                if (linha == null)
                    continue;

                // Reempilhadas: 36 px por faixa comum, e a do jogador logo abaixo das cinco.
                float novoY = local ? 5 * 36f + 4f : i * 36f;
                Fileira(linha, novoY, dh);

                SerializedProperty item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("raiz").objectReferenceValue = linha.gameObject;

                GameObject estado = Agrupar(linha, local ? "IsLocal" : "Other");
                item.FindPropertyRelative(local ? "estadoLocal" : "estadoOutro")
                    .objectReferenceValue = estado;

                // Só as faixas do topo precisam da moldura: a de baixo já É a variante âmbar.
                if (!local)
                {
                    GameObject destaque = Contorno(linha, "Destaque", Ambar, 2f, 14f);
                    destaque.transform.SetAsLastSibling();
                    destaque.SetActive(false);
                    item.FindPropertyRelative("destaque").objectReferenceValue = destaque;
                }

                // Cada coluna é achada pela caixa que ela tem no documento. Pegar "o primeiro
                // texto da faixa" dependia de uma ordenação que não é a da leitura — foi assim que
                // o número da posição foi parar na borda direita e o nome por cima dele.
                RectTransform pos = local ? m.Caixa(1573f, dy + 14f, 30f, 22f)
                                          : m.Caixa(1569f, dy + 8f, 30f, 15f);
                RectTransform nome = local ? m.Caixa(1614f, dy + 17f, 182f, 16f)
                                           : m.Caixa(1610f, dy + 8.5f, 218.2f, 14f);
                RectTransform velho = local ? m.Caixa(1807f, dy + 18f, 60f, 14f)
                                            : m.Caixa(1839.2f, dy + 9f, 31.8f, 13f);

                string sufixo = local ? "Local" : "Outro";
                float corpo = local ? 2f : 0f;

                // A caixa da posição vem do documento com 30 px, medida para um dígito. Em 16º o
                // texto não cabia e o TMP, em modo Ellipsis, preferia não desenhar nada — a faixa
                // do jogador aparecia sem número justamente para quem mais precisa dele.
                item.FindPropertyRelative("posicao" + sufixo).objectReferenceValue =
                    Coluna(pos, 8f, 42f, TextAlignmentOptions.Midline, false);
                item.FindPropertyRelative("nome" + sufixo).objectReferenceValue =
                    Coluna(nome, 54f, 112f, TextAlignmentOptions.MidlineLeft, true);

                // O número solto da direita era o intervalo desenhado à mão; quem escreve agora é o
                // binder, e ele precisa de DUAS colunas — melhor volta ao lado do nome e intervalo
                // na borda.
                Apagar(velho);

                // A faixa do jogador é ÂMBAR: âmbar sobre âmbar não se lê. Nela as duas colunas de
                // tempo viram tinta escura, como o resto do texto daquela linha.
                Color corVolta = local ? new Color(Tinta.r, Tinta.g, Tinta.b, 0.62f) : Apagado;
                Color corIntervalo = local ? Tinta : Ambar;

                item.FindPropertyRelative("volta" + sufixo).objectReferenceValue =
                    ColunaNova(estado.transform, "MelhorVolta", 170f, 74f, 14f + corpo, corVolta);
                item.FindPropertyRelative("intervalo" + sufixo).objectReferenceValue =
                    ColunaNova(estado.transform, "Intervalo", 250f, 66f, 15f + corpo, corIntervalo);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Prende a faixa ao topo do bloco da classificação, na altura pedida.</summary>
        private static void Fileira(RectTransform linha, float y, float altura)
        {
            linha.anchorMin = new Vector2(0f, 1f);
            linha.anchorMax = new Vector2(1f, 1f);
            linha.pivot = new Vector2(0.5f, 1f);
            linha.offsetMin = new Vector2(0f, -y - altura);
            linha.offsetMax = new Vector2(0f, -y);
        }

        /// <summary>
        /// Placa escura atrás da classificação, para o texto não boiar sobre o céu.
        ///
        /// Cresce do TOPO para baixo, com altura que o binder ajusta: quando o jogador está no top
        /// 5 a faixa dele some, e um painel de altura fixa ficaria com um terço vazio pendurado.
        /// </summary>
        private static RectTransform FundoDaClassificacao(RectTransform bloco)
        {
            var go = new GameObject("Fundo", typeof(RectTransform));
            go.transform.SetParent(bloco, false);
            go.transform.SetSiblingIndex(0);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, 14f);
            r.sizeDelta = new Vector2(24f, 268f);

            var f = go.AddComponent<UIRoundedRect>();
            f.raycastTarget = false;
            f.Definir(new Color(0.04f, 0.05f, 0.13f, 0.76f), 18f);
            f.DefinirContorno(Fio, 1.5f);
            f.DefinirRaio(18f, 18f);
            return r;
        }

        /// <summary>Reancora uma coluna de texto do documento na grade da faixa.</summary>
        private static TextMeshProUGUI Coluna(RectTransform caixa, float x, float largura,
                                              TextAlignmentOptions alinhamento, bool cortar)
        {
            TextMeshProUGUI t = Tmp(caixa);
            if (caixa == null || t == null)
                return t;

            caixa.anchorMin = new Vector2(0f, 0.5f);
            caixa.anchorMax = new Vector2(0f, 0.5f);
            caixa.pivot = new Vector2(0f, 0.5f);
            caixa.anchoredPosition = new Vector2(x, 0f);
            caixa.sizeDelta = new Vector2(largura, 24f);

            var rt = (RectTransform)t.transform;
            if (rt != caixa)
                Esticar(rt);

            t.alignment = alinhamento;
            t.enableWordWrapping = false;

            // Nome longo pode virar reticências; NÚMERO, não. Cortar um número é pior que
            // apertá-lo, porque "1…" e "16" leem igual de relance.
            t.overflowMode = cortar ? TextOverflowModes.Ellipsis : TextOverflowModes.Overflow;
            if (!cortar)
                t.characterSpacing = 0f;

            return t;
        }

        /// <summary>Cria uma coluna de tempo dentro da faixa.</summary>
        private static TextMeshProUGUI ColunaNova(Transform pai, string nome, float x,
                                                  float largura, float corpo, Color cor)
        {
            var go = new GameObject("Col_" + nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0.5f);
            r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(x, 0f);
            r.sizeDelta = new Vector2(largura, 20f);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = "--";
            t.fontSize = corpo;
            t.color = cor;
            t.characterSpacing = 2f;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineRight;
            t.enableWordWrapping = false;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                t.font = fonte;

            return t;
        }

        private static void ClusterVital(GameObject raiz, ProtoBuilder.Mapa m)
        {
            var ui = raiz.AddComponent<PartyRacers.UI.Race.VitalClusterUI>();
            var so = new SerializedObject(ui);

            // Vida: 5 blocos de 20 HP. Blocos porque são CONTÁVEIS de relance — barra contínua
            // exige medir, e a 150 km/h ninguém mede.
            //
            // Cada bloco é REFEITO: o que o protótipo deixou ali era um retângulo pintado por um
            // `UIRoundedRect`, e `UIRoundedRect` não é `Image`. O binder guardava null no campo,
            // então a barra nunca descia — ela ficava verde e cheia levando dano até o carro
            // quebrar. Aqui nascem três camadas de verdade: trilho apagado, verde e âmbar, as duas
            // últimas como `Image` do tipo Filled.
            RectTransform[] blocos = m.Caixas(78f, 32f, 4f);
            SerializedProperty segmentos = so.FindProperty("segmentosDeVida");
            segmentos.arraySize = blocos.Length;

            for (int i = 0; i < blocos.Length; i++)
            {
                RectTransform bloco = blocos[i];
                Despintar(new[] { bloco });

                GameObject vazio = Preenchido(bloco, "Vazio", new Color(1f, 1f, 1f, 0.09f));
                Image cheio = BarraQueEnche(bloco, "Cheio", Verde);
                Image ferido = BarraQueEnche(bloco, "Ferido", Ambar);

                SerializedProperty item = segmentos.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("vazio").objectReferenceValue = vazio;
                item.FindPropertyRelative("cheio").objectReferenceValue = cheio;
                item.FindPropertyRelative("ferido").objectReferenceValue = ferido;

                vazio.SetActive(true);
                ferido.gameObject.SetActive(false);
            }

            Atribuir(so, "valorDeVida", Tmp(m.Texto("100")));

            RectTransform rotuloVida = m.Texto("VIDA");
            if (rotuloVida != null && rotuloVida.parent != null)
                Atribuir(so, "raizVida", rotuloVida.parent.gameObject);

            EscudoDoCluster(so, m);

            Atribuir(so, "chuteDeDano", Chute(m.Caixa(105f, 1000f, 417f, 46f), 1.06f, true));
            Atribuir(so, "chuteDoEscudo", Chute(m.Caixa(421.5f, 962f, 100.5f, 29f), 1.18f, false));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// A barra do escudo, refeita como UMA barra contínua.
        ///
        /// O protótipo desenhou três segmentos, e o binder antigo tentou lê-los como quatro linhas
        /// de estado inteiras — nada disso existe no jogo, que tem UM escudo com duração e recarga.
        /// O resultado era uma barra imóvel: nem drenava ao ativar, nem enchia ao recarregar.
        ///
        /// Aqui os segmentos viram trilho, e por cima entra um preenchimento do tipo Filled com um
        /// risquinho na ponta. A cor diz o estado; o SENTIDO do movimento diz se está acabando ou
        /// voltando.
        /// </summary>
        private static void EscudoDoCluster(SerializedObject so, ProtoBuilder.Mapa m)
        {
            RectTransform barra = m.Caixa(105f, 964.5f, 305.5f, 24f);
            RectTransform rotulo = m.Texto("ESCUDO");

            if (rotulo != null && rotulo.parent != null)
                Atribuir(so, "raizEscudo", rotulo.parent.gameObject);

            if (barra == null)
                return;

            foreach (Transform filho in barra.Cast<Transform>().ToList())
                Object.DestroyImmediate(filho.gameObject);

            Despintar(new[] { barra });

            GameObject trilho = Nó(barra, "Trilho");
            var fundo = trilho.AddComponent<UIRoundedRect>();
            fundo.raycastTarget = false;
            fundo.Definir(new Color(1f, 1f, 1f, 0.09f), 6f);
            fundo.DefinirContorno(Fio, 1f);
            fundo.DefinirRaio(6f, 6f);

            Image preenchimento = BarraQueEnche(barra, "Preenchimento", new Color(0.21f, 0.65f, 1f));

            var ponta = new GameObject("Ponta", typeof(RectTransform));
            ponta.transform.SetParent(barra, false);

            var pr = (RectTransform)ponta.transform;
            pr.anchorMin = new Vector2(0f, 0.5f);
            pr.anchorMax = new Vector2(0f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.sizeDelta = new Vector2(3f, 22f);

            var risco = ponta.AddComponent<UIRoundedRect>();
            risco.raycastTarget = false;
            risco.Definir(Color.white, 1.5f);
            risco.DefinirRaio(1.5f, 1.5f);

            Atribuir(so, "preenchimentoEscudo", preenchimento);
            Atribuir(so, "pontaDoEscudo", pr);
            Atribuir(so, "textoDoEscudo", Tmp(m.Texto("PRONTO")));

            RectTransform chapinha = m.Caixa(421.5f, 962f, 100.5f, 29f);
            if (chapinha != null)
                Atribuir(so, "fundoDaChapinha", chapinha.GetComponent<Graphic>());
        }

        /// <summary>
        /// Retângulo que preenche da esquerda para a direita — `Image` com `type = Filled`.
        ///
        /// Tem de ser `Image`: o `UIRoundedRect` do projeto desenha malha própria e não tem
        /// `fillAmount`, então toda barra montada com ele fica parada por construção.
        /// </summary>
        private static Image BarraQueEnche(RectTransform pai, string nome, Color cor)
        {
            var go = new GameObject(nome, typeof(RectTransform));
            go.transform.SetParent(pai, false);
            Esticar((RectTransform)go.transform);

            var img = go.AddComponent<Image>();
            img.color = cor;
            img.raycastTarget = false;
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            return img;
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

                // O avatar da linha: quadrado de ~42 px à esquerda do nome.
                RectTransform quadro = linha.GetComponentsInChildren<UIRoundedRect>(true)
                    .Select(f => (RectTransform)f.transform)
                    .FirstOrDefault(t => t != linha && t.rect.width >= 34f && t.rect.width <= 52f
                                                    && Mathf.Abs(t.rect.width - t.rect.height) < 8f);

                if (quadro != null)
                {
                    quadro.name = "Avatar";
                    vaga.FindPropertyRelative("avatar").objectReferenceValue = Pintavel(quadro);
                }

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

            // A partida personalizada é uma ABA do lobby (§7), mas o protótipo não desenhou o
            // acesso a ela — sem porta, a tela existia e ninguém chegava lá. O botão nasce ao lado
            // de CANCELAR, no mesmo peso visual: é ação secundária, não concorre com BUSCAR.
            RectTransform molde = Alvo(m, "CANCELAR");
            if (molde != null)
            {
                RectTransform sala = Copiar(molde, molde.parent);
                if (sala != null)
                {
                    sala.name = "Btn_SalaPrivada";
                    sala.anchoredPosition += new Vector2(-(molde.rect.width + 18f), 0f);
                    Rotular(sala.gameObject, "SALA PRIVADA");

                    Button b = Clicavel(sala);
                    so.FindProperty("btnSalaPrivada").objectReferenceValue = b;
                }
            }

            so.FindProperty("etiquetaDoKart").objectReferenceValue = Tmp(m.Texto("SEU KART"));
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

            // O nome é o texto mais alto e à esquerda; o STATUS é o que fica logo abaixo dele,
            // alinhado pela mesma margem. Pegar "o segundo em ordem de leitura" trazia a chapinha
            // "NO GRUPO" — que está à direita e um pouco acima —, e o status real nunca era
            // preenchido: as sete linhas ficavam com o texto do protótipo, dizendo que todo mundo
            // estava no grupo.
            RectTransform[] textos = m.TextosEm(modelo);
            RectTransform noNome = textos.FirstOrDefault();
            Atribuir(so, "nome", Tmp(noNome));

            Atribuir(so, "estado", Tmp(m.Abaixo(noNome)));

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

            // O quadrado de identidade: o maior gráfico da linha que não é texto nem moldura.
            RectTransform quadro = modelo.GetComponentsInChildren<UIRoundedRect>(true)
                .Select(f => (RectTransform)f.transform)
                .FirstOrDefault(r => r != modelo && r.rect.width >= 30f && r.rect.width <= 48f
                                                 && Mathf.Abs(r.rect.width - r.rect.height) < 8f);

            if (quadro != null)
            {
                quadro.name = "Avatar";
                Atribuir(so, "avatar", Pintavel(quadro));
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
            // é um elemento do modelo 3D (CarElementName).
            //
            // O protótipo desenhou SETE abas, e elas cobriam metade do pack: piloto (5 variantes),
            // motor, faróis, farol de milha e escapamento não tinham aba nenhuma — era conteúdo
            // pronto e inalcançável. As doze abaixo cobrem os dez elementos + modelo + cor, ou seja,
            // tudo o que os rigs do pack expõem hoje.
            //
            // As sete do design ficam onde ele as pôs; as cinco novas entram na sobra da segunda
            // fileira e numa terceira, com as posições em coordenadas do dump (o painel tem 200 px
            // livres abaixo da grade, então descer o conteúdo em 49 px não custa nada).
            // rótulo, fonte (0=modelo, 1=cor, 2=elemento), CarElementName, e a caixa em
            // coordenadas do dump. As sete primeiras são as do design, nas posições dele; as cinco
            // últimas entram na sobra da segunda fileira e numa terceira.
            (string rotulo, int fonte, int elemento, float x, float y, float w, float h, bool nova)[] categorias =
            {
                ("MODELO",   0,  0,  68.0f, 152f, 119.8f, 45f, false),
                ("COR",      1,  0, 193.8f, 152f,  81.7f, 45f, false),
                ("RODAS",    2,  5, 281.5f, 152f, 109.3f, 45f, false),   // Wheel       — 15 variantes
                ("FRENTE",   2,  1, 396.8f, 152f, 113.5f, 45f, false),   // FrontBumper — padrão/esportivo
                ("TRASEIRA", 2,  2, 516.3f, 152f, 135.8f, 45f, false),   // RearBumper  — padrão/esportivo
                ("TETO",     2,  9,  68.0f, 203f,  91.4f, 43f, false),   // Spoiler     — com/sem
                ("ADESIVOS", 2,  6, 165.4f, 203f, 136.5f, 43f, false),   // Decals      — com/sem

                ("PILOTO",   2, 10, 307.9f, 203f, 114.3f, 43f, true),    // Racer       — 5 variantes
                ("FARÓIS",   2,  4, 428.2f, 203f, 114.3f, 43f, true),    // Headlight   — padrão/esportivo
                ("ESCAPE",   2,  3, 548.5f, 203f, 114.3f, 43f, true),    // Pipe        — padrão/esportivo
                ("MOTOR",    2,  8,  68.0f, 252f, 103.4f, 43f, true),    // Engine      — com/sem
                ("NEBLINA",  2,  7, 177.4f, 252f, 125.1f, 43f, true),    // FogLight    — com/sem
            };

            // A terceira fileira empurra o resto do painel para baixo. Cabe: são 916 px de painel
            // para 726 de grade.
            const float DescidaDoConteudo = 49f;

            SerializedProperty abas = so.FindProperty("abas");
            abas.arraySize = categorias.Length;

            // A faixa é reancorada no topo com a altura das TRÊS fileiras. Sem isso ela crescia
            // pelo centro e cada fileira ia para um lado: as abas do documento não compartilham
            // âncora — a primeira fileira está presa no topo, a segunda no rodapé da faixa, e duas
            // delas na borda direita. Enquanto a faixa tinha altura fixa isso não aparecia.
            RectTransform faixa = m.Caixa(68f, 152f, 670f, 94f);
            if (faixa != null)
                m.AncorarNoTopo(faixa, 94f + DescidaDoConteudo);

            // O modelo é clonado ANTES de ADESIVOS receber os dois estados: senão cada cópia já
            // nasceria com moldura de aba dentro de outra moldura.
            RectTransform modeloDeAba = Alvo(m, "ADESIVOS");

            for (int i = 0; i < categorias.Length; i++)
            {
                var c = categorias[i];
                SerializedProperty item = abas.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("categoria").stringValue = c.rotulo;
                item.FindPropertyRelative("fonte").enumValueIndex = c.fonte;
                item.FindPropertyRelative("elemento").intValue = c.elemento;

                RectTransform aba = c.nova
                    ? Copiar(modeloDeAba, modeloDeAba != null ? modeloDeAba.parent : null)
                    : Alvo(m, c.rotulo);

                if (aba == null)
                    continue;

                if (c.nova)
                {
                    aba.name = "Aba_" + c.rotulo;
                    Rotular(aba.gameObject, c.rotulo);
                    CentralizarRotulo(aba);
                }

                PosicionarAba(aba, c.x - 68f, c.y - 152f, c.w, c.h);

                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(aba);
                // "RODAS" é a aba que o documento desenhou ATIVA. Dizer que era outra fazia a
                // aparência âmbar dela virar o estado ocioso: RODAS parecia selecionada sempre,
                // junto com a aba realmente aberta.
                (GameObject ativo, GameObject ocioso) = DoisEstados(aba, c.rotulo == "RODAS");
                item.FindPropertyRelative("estadoAtivo").objectReferenceValue = ativo;
                item.FindPropertyRelative("estadoOcioso").objectReferenceValue = ocioso;
            }

            RectTransform legenda = m.Caixa(68f, 260f, 670f, 20f);
            if (legenda != null)
                m.AncorarNoTopo(legenda, 20f, DescidaDoConteudo);

            // ---- grade de cards
            RectTransform[] cards = m.Caixas(158f, 168f, 6f);
            if (cards.Length > 0)
                Gradear(m, cards, 158f, 168f, 14f, 4, DescidaDoConteudo);

            SerializedProperty lista = so.FindProperty("cards");
            lista.arraySize = cards.Length;

            for (int i = 0; i < cards.Length; i++)
            {
                RectTransform card = cards[i];
                SerializedProperty item = lista.GetArrayElementAtIndex(i);

                GameObject conteudo = Agrupar(card, "Conteudo");
                item.FindPropertyRelative("raiz").objectReferenceValue = card.gameObject;
                item.FindPropertyRelative("botao").objectReferenceValue = Clicavel(card);

                // O documento pintou cada card de uma cor de RARIDADE (comum, raro, épico,
                // lendário). O projeto não tem economia de cosméticos, então seis cores diferentes
                // atrás de doze fotos não informam nada e ainda competem com a peça — a mesma roda
                // parecia outra coisa conforme a célula em que caísse. Fundo único; quem colore é o
                // estado, que significa algo.
                Uniformizar(card);

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

                // O card do design tem duas linhas: o nome da peça e, abaixo, a raridade. Raridade
                // é economia de cosméticos, que o projeto não tem — a linha passa a dizer o ESTADO,
                // que é informação de verdade e ocupa o mesmo lugar na composição.
                RectTransform[] textos = m.TextosEm(card);
                if (textos.Length > 1)
                {
                    item.FindPropertyRelative("nome").objectReferenceValue = Tmp(textos[textos.Length - 2]);
                    item.FindPropertyRelative("etiqueta").objectReferenceValue = Tmp(textos[textos.Length - 1]);
                }

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
            // As vagas ficam onde o design as colocou. Uma grade calculada aqui subia para dentro
            // do dial: o contêiner delas divide o espaço com o rádio, e recalcular a âncora sem
            // saber disso desmonta a composição. Como o modal deixou de esticar, as posições do
            // protótipo voltaram a valer.
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

                RectTransform quadro = vaga.GetComponentsInChildren<UIRoundedRect>(true)
                    .Select(f => (RectTransform)f.transform)
                    .FirstOrDefault(t => t != vaga && t.rect.width >= 36f && t.rect.width <= 52f
                                                   && Mathf.Abs(t.rect.width - t.rect.height) < 8f);

                if (quadro != null)
                {
                    quadro.name = "Avatar";
                    item.FindPropertyRelative("avatar").objectReferenceValue = Pintavel(quadro);
                }

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

            AjustarMolduraDaSala(m);

            // ---- faixa do topo
            RectTransform chip = m.Caixa(292f, 152f, 181.8f, 46f);
            Atribuir(so, "codigoDaSala", Tmp(m.Texto("7KQ2XM", false)));
            Atribuir(so, "btnCopiarCodigo", chip != null ? Clicavel(chip) : null);
            Atribuir(so, "contador", Tmp(m.Texto("9 / 16", false)));
            Atribuir(so, "avisoDoConvite", AvisoDoConvite(m));

            // ---- mapa
            RectTransform cartao = m.Caixa(1428f, 185f, 424f, 228f);
            Atribuir(so, "nomeDoMapa", Tmp(m.Texto("MINI GOLFE RUN", false)));
            Atribuir(so, "descricaoDoMapa", Tmp(m.Texto("níveis", false)));
            Atribuir(so, "resumoDoMapa", Tmp(m.Texto("VOLTAS", false)));
            Atribuir(so, "previewDoMapa", RenderDaPista(m, cartao));
            Atribuir(so, "pontosDoMapa", m.Caixa(1428f, 519f, 424f, 5f));

            if (cartao != null)
            {
                Atribuir(so, "btnMapaAnterior", Seta(cartao, "‹", false));
                Atribuir(so, "btnMapaProximo", Seta(cartao, "›", true));
            }

            // ---- regras (o documento já desenhou ◄ valor ►; aqui os dois viram botão)
            Regra(so, m, "voltas", 616f);
            Regra(so, m, "itens", 657f);
            Regra(so, m, "botsPreenchem", 698f);
            Regra(so, m, "danoPorColisao", 739f);

            // ---- ações
            // O botão continua dizendo o que FAZ. Escrever a contagem nele ("0 BOTS") transformava
            // a única ação de adicionar bot num letreiro — quem lê "0 BOTS" não clica.
            Atribuir(so, "btnIniciar", ClicavelPorTexto(m, "INICIAR"));
            Atribuir(so, "btnAdicionarBot", ClicavelPorTexto(m, "BOTS", false));

            TextMeshProUGUI rotuloDeBots = Tmp(m.Texto("+ BOTS", false));
            if (rotuloDeBots != null)
                rotuloDeBots.text = "+ BOT";

            Vagas(so, m);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Põe a moldura da sala no lugar e tira os halos que o UGUI não sabe desenhar.
        ///
        /// A área de conteúdo do documento (44/44/128/36) ficava ANCORADA NA BASE, porque a margem
        /// de baixo é menor que a de cima e a regra escolhe a borda mais próxima. Numa janela mais
        /// larga que 16:9 o canvas encurta em altura, e um bloco de 916 px preso embaixo sobe até
        /// atravessar a barra de topo — foi isso que cobriu a marca e a carteira. Esticada com as
        /// margens do desenho, ela nunca invade o teto.
        ///
        /// Os três halos radiais do fundo viram ANEL: o degradê radial não existe em UGUI, e o que
        /// sobra do contorno é uma elipse laranja atravessada no meio da tela.
        /// </summary>
        private static void AjustarMolduraDaSala(ProtoBuilder.Mapa m)
        {
            RectTransform conteudo = m.Caixa(44f, 128f, 1832f, 916f);
            if (conteudo != null)
            {
                conteudo.anchorMin = Vector2.zero;
                conteudo.anchorMax = Vector2.one;
                conteudo.pivot = new Vector2(0.5f, 0.5f);
                conteudo.offsetMin = new Vector2(44f, 36f);
                conteudo.offsetMax = new Vector2(-44f, -128f);
            }

            Apagar(m.Caixa(370f, 14.8f, 1180f, 1180f));
            Apagar(m.Caixa(420f, 726f, 1080f, 250f));
            Apagar(m.Caixa(540f, 750f, 840f, 180f));

            RectTransform faixa = m.Caixa(68f, 152f, 1296f, 46f);
            if (faixa != null)
                m.AncorarNoTopo(faixa, 46f);

            // A grade ESTICA. Ela pendurava na base do painel — com a coluna acompanhando a
            // janela, a primeira fileira subia por cima da faixa "SALA PRIVADA". E com altura fixa
            // de 808 px ela transbordava por baixo em qualquer janela mais baixa que 1080: as
            // vagas 15 e 16 saíam da moldura. As oito fileiras dividem a altura disponível, então
            // a lista inteira cabe sempre — que é a regra da tela (16 vagas, sem rolagem).
            RectTransform grade = m.Caixa(68f, 212f, 1296f, 808f);
            if (grade != null)
            {
                grade.anchorMin = Vector2.zero;
                grade.anchorMax = Vector2.one;
                grade.pivot = new Vector2(0.5f, 0.5f);
                grade.offsetMin = new Vector2(24f, 24f);
                grade.offsetMax = new Vector2(-24f, -84f);
            }

            // A coluna da direita também estica, e seus blocos estavam pendurados em bordas
            // diferentes: o cartão do mapa descia e o painel de regras subia até se sobreporem, com
            // a descrição da pista atrás da palavra REGRAS.
            RectTransform cartao = m.Caixa(1404f, 128f, 472f, 420f);
            if (cartao != null)
                m.AncorarNoTopo(cartao, 420f);

            RectTransform regras = m.Caixa(1404f, 562f, 472f, 224f);
            if (regras != null)
                m.AncorarNoTopo(regras, 224f);

            Apagar(m.Caixa(1404f, 800f, 472f, 156f));
        }

        /// <summary>Linha de retorno do convite, no vão entre o código e o contador.</summary>
        private static TextMeshProUGUI AvisoDoConvite(ProtoBuilder.Mapa m)
        {
            RectTransform faixa = m.Caixa(68f, 152f, 1296f, 46f);
            if (faixa == null)
                return null;

            var go = new GameObject("Aviso_Convite", typeof(RectTransform));
            go.transform.SetParent(faixa, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0.5f);
            r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.anchoredPosition = new Vector2(424f, 0f);
            r.sizeDelta = new Vector2(780f, 24f);

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = string.Empty;
            t.fontSize = 15f;
            t.color = Ambar;
            t.characterSpacing = 6f;
            t.raycastTarget = false;
            t.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                t.font = fonte;

            return t;
        }

        /// <summary>
        /// A área do "RENDER DA PISTA" vira o Image da miniatura.
        ///
        /// O texto do protótipo era o placeholder; a pista tem PNG de verdade em
        /// `TrackDefinition.miniatura`, e mostrar o nome do arquivo em cima dela não ajuda ninguém.
        /// </summary>
        private static Image RenderDaPista(ProtoBuilder.Mapa m, RectTransform cartao)
        {
            Apagar(m.Texto("RENDER DA PISTA", false));

            if (cartao == null)
                return null;

            var go = new GameObject("Preview", typeof(RectTransform));
            go.transform.SetParent(cartao, false);
            go.transform.SetSiblingIndex(0);
            Esticar((RectTransform)go.transform);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>Seta redonda sobre o cartão do mapa, na borda esquerda ou direita.</summary>
        private static Button Seta(RectTransform cartao, string glifo, bool direita)
        {
            var go = new GameObject(direita ? "Btn_Mapa_Proximo" : "Btn_Mapa_Anterior",
                                    typeof(RectTransform));
            go.transform.SetParent(cartao, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(direita ? 1f : 0f, 0.5f);
            r.anchorMax = r.anchorMin;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(direita ? -30f : 30f, 0f);
            r.sizeDelta = new Vector2(44f, 44f);

            var fundo = go.AddComponent<UIRoundedRect>();
            fundo.Definir(new Color(0.04f, 0.05f, 0.13f, 0.82f), 22f);
            fundo.DefinirContorno(Fio, 2f);
            fundo.DefinirRaio(22f, 22f);

            var rotulo = new GameObject("Label", typeof(RectTransform));
            rotulo.transform.SetParent(r, false);
            Esticar((RectTransform)rotulo.transform);

            var t = rotulo.AddComponent<TextMeshProUGUI>();
            t.text = glifo;
            t.fontSize = 28f;
            t.color = new Color(0.85f, 0.88f, 1f, 1f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            TMP_FontAsset fonte = CssKit.Fonte("Archivo", 700);
            if (fonte != null)
                t.font = fonte;

            var b = go.AddComponent<Button>();
            b.targetGraphic = fundo;
            go.AddComponent<UIPress>();
            return b;
        }

        /// <summary>
        /// Uma linha de regra do documento (◄ valor ►) vira controle de verdade.
        ///
        /// Os dois quadradinhos de 16 px já estavam desenhados dos dois lados do valor — eles só
        /// não eram botão. As coordenadas vêm do dump porque no build não há Canvas: medir a
        /// hierarquia devolveria zero.
        /// </summary>
        private static void Regra(SerializedObject so, ProtoBuilder.Mapa m, string campo, float y)
        {
            SerializedProperty r = so.FindProperty(campo);
            if (r == null)
                return;

            RectTransform menos = m.Caixa(1730f, y, 16f, 16f);
            RectTransform mais = m.Caixa(1824f, y, 16f, 16f);
            RectTransform valor = m.Caixa(1756f, y + 1f, 58f, 14f);

            r.FindPropertyRelative("btnAnterior").objectReferenceValue = menos != null ? Passo(menos, "‹") : null;
            r.FindPropertyRelative("btnProximo").objectReferenceValue = mais != null ? Passo(mais, "›") : null;

            TextMeshProUGUI texto = Tmp(valor);
            r.FindPropertyRelative("valor").objectReferenceValue = texto;

            if (valor == null || texto == null)
                return;

            // O documento escreveu "3" e "SIM" nessa caixa; o jogo também escreve "DESLIGADO". Com
            // os 58 px do desenho a palavra passava por cima das setas. A caixa passa a ocupar todo
            // o vão ENTRE elas, e o corpo encolhe se ainda faltar espaço.
            valor.anchorMin = new Vector2(0f, 0.5f);
            valor.anchorMax = new Vector2(1f, 0.5f);
            valor.pivot = new Vector2(0.5f, 0.5f);
            valor.offsetMin = new Vector2(32f, valor.offsetMin.y);
            valor.offsetMax = new Vector2(-28f, valor.offsetMax.y);

            var rt = (RectTransform)texto.transform;
            if (rt != valor)
                Esticar(rt);

            texto.alignment = TextAlignmentOptions.Center;
            texto.enableAutoSizing = true;
            texto.fontSizeMax = texto.fontSize;
            texto.fontSizeMin = 10f;
        }

        /// <summary>
        /// Monta as 16 vagas iguais, cada uma com os três estados.
        ///
        /// O documento desenhou vagas DIFERENTES entre si — nove com gente dentro e sete com
        /// "+ CONVIDAR" —, porque ele mostra um exemplo de sala cheia pela metade. No jogo qualquer
        /// vaga pode virar qualquer coisa, então as duas formas viram MODELO e são clonadas nas
        /// dezesseis posições. Sem isso, a vaga 3 nunca saberia mostrar um convite e a vaga 12
        /// nunca saberia mostrar um jogador.
        /// </summary>
        private static void Vagas(SerializedObject so, ProtoBuilder.Mapa m)
        {
            RectTransform[] originais = m.Caixas(644f, 94f, 6f);
            if (originais.Length < 10)
                return;

            RectTransform grade = m.Caixa(68f, 212f, 1296f, 808f);
            if (grade == null)
                return;

            RectTransform modeloJogador = originais[0];
            RectTransform modeloVazio = originais[9];

            // Onde cada peça mora DENTRO do modelo, em índices de irmão. É o que permite achar a
            // mesma peça em cada clone: o mapa só conhece os nós originais.
            int[] cIndice = Caminho(modeloJogador, m.Caixa(83f, 252.5f, 12f, 13f));
            int[] cAvatar = Caminho(modeloJogador, m.Caixa(106f, 242f, 34f, 34f));
            int[] cNome = Caminho(modeloJogador, m.Caixa(151f, 252f, 397.7f, 14f));
            int[] cAnfitriao = Caminho(modeloJogador, m.Texto("ANFITRIÃO", false));
            int[] cPronto = Caminho(modeloJogador, m.Caixa(649f, 253f, 15f, 12f));
            int[] cExpulsar = Caminho(modeloJogador, m.Caixa(675f, 248f, 22f, 22f));

            int[] cIndiceVazio = Caminho(modeloVazio, m.Caixa(735f, 660.5f, 12f, 13f));

            // Os modelos saem da grade antes de ela ser esvaziada, senão são destruídos junto.
            var abrigo = new GameObject("__Modelos", typeof(RectTransform));
            abrigo.transform.SetParent(grade.parent, false);
            abrigo.SetActive(false);

            RectTransform copiaJogador = Copiar(modeloJogador, abrigo.transform);
            RectTransform copiaVazio = Copiar(modeloVazio, abrigo.transform);

            foreach (RectTransform antiga in originais)
                Apagar(antiga);

            SerializedProperty lista = so.FindProperty("vagas");
            lista.arraySize = 16;

            for (int i = 0; i < 16; i++)
            {
                // Duas colunas de oito, em FRAÇÕES da grade. Posição absoluta faria a lista
                // transbordar por baixo em qualquer janela mais baixa que os 1080 do desenho.
                int coluna = i % 2;
                int fileira = i / 2;

                var vaga = new GameObject($"Vaga_{i + 1:00}", typeof(RectTransform));
                vaga.transform.SetParent(grade, false);

                var vr = (RectTransform)vaga.transform;
                vr.anchorMin = new Vector2(coluna * 0.5f, 1f - (fileira + 1) / 8f);
                vr.anchorMax = new Vector2((coluna + 1) * 0.5f, 1f - fileira / 8f);
                vr.pivot = new Vector2(0.5f, 0.5f);
                vr.offsetMin = new Vector2(coluna == 0 ? 0f : 4f, 4f);
                vr.offsetMax = new Vector2(coluna == 0 ? -4f : 0f, -4f);

                RectTransform jogador = Copiar(copiaJogador, vaga.transform);
                jogador.name = "State_Player";
                Esticar(jogador);

                RectTransform vazio = Copiar(copiaVazio, vaga.transform);
                vazio.name = "State_Empty";
                Esticar(vazio);

                GameObject bot = MarcaDeBot(vr);

                SerializedProperty item = lista.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("raiz").objectReferenceValue = vaga;
                item.FindPropertyRelative("estadoJogador").objectReferenceValue = jogador.gameObject;
                item.FindPropertyRelative("estadoVazio").objectReferenceValue = vazio.gameObject;
                item.FindPropertyRelative("estadoBot").objectReferenceValue = bot;

                item.FindPropertyRelative("indice").objectReferenceValue = Tmp(Seguir(jogador, cIndice));
                item.FindPropertyRelative("nome").objectReferenceValue = Tmp(Seguir(jogador, cNome));
                item.FindPropertyRelative("miniatura").objectReferenceValue = Retrato(Seguir(jogador, cAvatar));
                item.FindPropertyRelative("seloDeAnfitriao").objectReferenceValue = Objeto(Seguir(jogador, cAnfitriao));
                item.FindPropertyRelative("indiceVazio").objectReferenceValue = Tmp(Seguir(vazio, cIndiceVazio));

                RectTransform pronto = Seguir(jogador, cPronto);
                item.FindPropertyRelative("estadoPronto").objectReferenceValue = Objeto(pronto);
                item.FindPropertyRelative("estadoAguardando").objectReferenceValue = Aguardando(pronto);

                RectTransform expulsar = Seguir(jogador, cExpulsar);
                item.FindPropertyRelative("btnRemover").objectReferenceValue =
                    expulsar != null ? Clicavel(expulsar) : null;

                // A vaga livre INTEIRA convida. Um "+" de 34 px como único alvo de clique
                // transforma convidar numa mira.
                item.FindPropertyRelative("btnConvidar").objectReferenceValue = Clicavel(vazio);

                vazio.gameObject.SetActive(false);
                bot.SetActive(false);
            }

            Object.DestroyImmediate(abrigo);
        }

        /// <summary>Moldura violeta + etiqueta, por cima da linha, quando o ocupante é bot.</summary>
        private static GameObject MarcaDeBot(RectTransform vaga)
        {
            GameObject go = Contorno(vaga, "State_Bot", Violeta, 2f, 16f);

            var etiqueta = new GameObject("Tag", typeof(RectTransform));
            etiqueta.transform.SetParent(go.transform, false);

            var r = (RectTransform)etiqueta.transform;
            r.anchorMin = new Vector2(1f, 0.5f);
            r.anchorMax = new Vector2(1f, 0.5f);
            r.pivot = new Vector2(1f, 0.5f);
            r.anchoredPosition = new Vector2(-56f, 0f);
            r.sizeDelta = new Vector2(52f, 22f);

            var fundo = etiqueta.AddComponent<UIRoundedRect>();
            fundo.raycastTarget = false;
            fundo.Definir(new Color(Violeta.r, Violeta.g, Violeta.b, 0.18f), 11f);
            fundo.DefinirContorno(Violeta, 1.5f);
            fundo.DefinirRaio(11f, 11f);

            var texto = new GameObject("Label", typeof(RectTransform));
            texto.transform.SetParent(r, false);
            Esticar((RectTransform)texto.transform);

            var t = texto.AddComponent<TextMeshProUGUI>();
            t.text = "BOT";
            t.fontSize = 12f;
            t.characterSpacing = 8f;
            t.color = Violeta;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            TMP_FontAsset fonte = CssKit.Fonte("Space Mono", 700);
            if (fonte != null)
                t.font = fonte;

            return go;
        }

        /// <summary>Gêmeo âmbar do ponto de "pronto", para o estado aguardando.</summary>
        private static GameObject Aguardando(RectTransform pronto)
        {
            if (pronto == null)
                return null;

            RectTransform copia = Copiar(pronto, pronto.parent);
            copia.name = "State_Waiting";

            foreach (UIRoundedRect f in copia.GetComponentsInChildren<UIRoundedRect>(true))
            {
                float raio = f.Raio;
                f.Definir(Ambar, raio);
                f.DefinirContorno(new Color(Ambar.r, Ambar.g, Ambar.b, 0.4f), 1f);
            }

            foreach (Image img in copia.GetComponentsInChildren<Image>(true))
                img.color = Ambar;

            copia.gameObject.SetActive(false);
            return copia.gameObject;
        }

        /// <summary>
        /// O quadradinho do avatar vira a vitrine do kart: placa escura, borda fina e a foto dentro.
        ///
        /// O documento reservou 34×34, que é quadrado — e kart é deitado. Sem a placa por trás, a
        /// foto (fundo transparente) flutuava solta na linha e sumia contra o azul; com ela, a
        /// miniatura vira um selo e a linha ganha um ponto de leitura antes do nome.
        /// </summary>
        private static Image Retrato(RectTransform quadro)
        {
            if (quadro == null)
                return null;

            foreach (Graphic g in quadro.GetComponentsInChildren<Graphic>(true))
                if (g != null && !(g is TextMeshProUGUI))
                    Object.DestroyImmediate(g);

            // Cresce para os lados a partir do próprio centro: o número fica à esquerda e o nome à
            // direita, e mexer só na largura não empurra nenhum dos dois.
            quadro.sizeDelta = new Vector2(46f, 36f);

            var placa = quadro.gameObject.AddComponent<UIRoundedRect>();
            placa.raycastTarget = false;
            placa.Definir(new Color(0.04f, 0.05f, 0.13f, 0.85f), 9f);
            placa.DefinirContorno(Fio, 1.5f);
            placa.DefinirRaio(9f, 9f);

            var go = new GameObject("Kart", typeof(RectTransform));
            go.transform.SetParent(quadro, false);

            var r = (RectTransform)go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(2f, 2f);
            r.offsetMax = new Vector2(-2f, -2f);

            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private static GameObject Objeto(RectTransform r) => r != null ? r.gameObject : null;

        /// <summary>Índices de irmão de <paramref name="alvo"/> até <paramref name="raiz"/>.</summary>
        private static int[] Caminho(Transform raiz, Transform alvo)
        {
            if (raiz == null || alvo == null)
                return null;

            var passos = new List<int>();
            Transform t = alvo;

            while (t != null && t != raiz)
            {
                passos.Add(t.GetSiblingIndex());
                t = t.parent;
            }

            if (t != raiz)
                return null;

            passos.Reverse();
            return passos.ToArray();
        }

        /// <summary>Refaz um <see cref="Caminho"/> dentro de um clone.</summary>
        private static RectTransform Seguir(Transform raiz, int[] caminho)
        {
            if (raiz == null || caminho == null)
                return null;

            Transform t = raiz;
            foreach (int i in caminho)
            {
                if (t == null || i < 0 || i >= t.childCount)
                    return null;

                t = t.GetChild(i);
            }

            return t as RectTransform;
        }

        /// <summary>Transforma o quadradinho do documento num botão de passo, com glifo.</summary>
        private static Button Passo(RectTransform alvo, string glifo)
        {
            var rotulo = new GameObject("Label", typeof(RectTransform));
            rotulo.transform.SetParent(alvo, false);
            Esticar((RectTransform)rotulo.transform);

            var t = rotulo.AddComponent<TextMeshProUGUI>();
            t.text = glifo;
            t.fontSize = 18f;
            t.color = new Color(0.85f, 0.88f, 1f, 1f);
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;

            TMP_FontAsset fonte = CssKit.Fonte("Archivo", 700);
            if (fonte != null)
                t.font = fonte;

            // A caixa de 16 px é pequena demais para o mouse. O alvo de clique cresce sem mexer no
            // desenho: quem pinta continua sendo o quadradinho.
            var area = new GameObject("Hit", typeof(RectTransform));
            area.transform.SetParent(alvo, false);
            var ar = (RectTransform)area.transform;
            ar.anchorMin = Vector2.zero;
            ar.anchorMax = Vector2.one;
            ar.offsetMin = new Vector2(-8f, -10f);
            ar.offsetMax = new Vector2(8f, 10f);

            var alvoGrafico = area.AddComponent<Image>();
            alvoGrafico.color = new Color(0f, 0f, 0f, 0f);
            alvoGrafico.raycastTarget = true;

            var b = alvo.gameObject.GetComponent<Button>() ?? alvo.gameObject.AddComponent<Button>();
            b.targetGraphic = alvoGrafico;
            return b;
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

            // Último irmão: no UGUI quem vem depois desenha por cima. Sem isto o painel da sala
            // privada — que é largo e alto — passava por cima da barra e cobria as abas.
            if (faixa != null && dono != raiz)
                faixa.SetAsLastSibling();

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
        /// Os cards vinham com a posição que tinham no protótipo; qualquer mudança no tamanho do
        /// painel fazia cada um seguir a própria âncora e as fileiras se atropelavam. Com Grid
        /// Layout as células saem do contêiner e a grade se comporta como grade, inclusive quando
        /// a página troca e o número de cards muda.
        ///
        /// O contêiner NÃO é reancorado. Tentar recalcular o topo dele deu grade fora da tela: no
        /// momento do build não existe Canvas, então todo rect esticado mede zero e a conta vira
        /// lixo. O bloco já vem do design no lugar certo — basta deixá-lo em paz.
        /// </summary>
        private static void Gradear(ProtoBuilder.Mapa m, RectTransform[] cards, float largura,
                                    float altura, float espaco, int colunas, float descer = 0f)
        {
            var pai = (RectTransform)cards[0].parent;

            // A grade cresce do topo para baixo, a partir de onde o protótipo a colocou — logo
            // abaixo da última fileira de abas.
            float linhas = Mathf.Ceil(cards.Length / (float)colunas);
            m.AncorarNoTopo(pai, linhas * (altura + espaco), descer);

            var grade = pai.GetComponent<GridLayoutGroup>() ?? pai.gameObject.AddComponent<GridLayoutGroup>();
            grade.cellSize = new Vector2(largura, altura);
            grade.spacing = new Vector2(espaco, espaco);
            grade.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grade.constraintCount = colunas;
            grade.childAlignment = TextAnchor.UpperLeft;
            grade.padding = new RectOffset(0, 0, 0, 0);

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

        /// <summary>
        /// O gráfico que realmente PINTA o avatar.
        ///
        /// O quadrado do protótipo é um nó com contorno e um degradê dentro: o nó de fora fica
        /// transparente e quem tem cor é o filho. Ligar o binder ao nó de fora não muda nada — foi
        /// por isso que os sete amigos apareciam com o mesmo vermelho, a cor com que o prefab foi
        /// salvo. O alvo é o descendente opaco de maior área.
        /// </summary>
        private static Graphic Pintavel(RectTransform quadro)
        {
            if (quadro == null)
                return null;

            // Border e Shadow também são opacos e MAIORES que o miolo — ordenar por área pegava a
            // moldura, e o avatar ficava com a borda colorida e o centro branco.
            UIRoundedRect miolo = quadro.GetComponentsInChildren<UIRoundedRect>(true)
                .Where(f => f.CorDoPreenchimento.a > 0.5f
                         && f.name != "Border" && f.name != "Shadow" && f.transform != quadro)
                .OrderByDescending(f => ((RectTransform)f.transform).rect.width)
                .FirstOrDefault();

            if (miolo == null)
                return quadro.GetComponent<Graphic>();

            // O degradê do protótipo MULTIPLICA a cor do Graphic. Como ele vai de um azul escuro a
            // outro mais escuro, tingir de vermelho dava marrom e de verde dava musgo — os avatares
            // saíam todos numa lama parecida. Neutralizado para branco→cinza claro, ele continua
            // dando volume e deixa a cor de identidade chegar inteira.
            var degrade = miolo.GetComponent<PartyRacers.UI.Motion.UIGradient>();
            if (degrade != null)
                degrade.Definir(Color.white, new Color(0.74f, 0.74f, 0.74f, 1f));

            return miolo;
        }

        /// <summary>
        /// Deixa todos os cards da grade com o mesmo fundo, e o degradê sem tingir.
        ///
        /// O degradê do protótipo MULTIPLICA a cor do preenchimento: mantê-lo como veio faria a
        /// tinta única voltar a virar seis tons diferentes.
        /// </summary>
        private static void Uniformizar(RectTransform card)
        {
            foreach (UIRoundedRect f in card.GetComponentsInChildren<UIRoundedRect>(true))
            {
                if (f.name == "Shadow" || f.CorDoPreenchimento.a <= 0.05f)
                    continue;

                float raio = f.Raio;
                f.Definir(FundoDoCard, raio);
                f.DefinirContorno(Fio, 2f);

                var degrade = f.GetComponent<PartyRacers.UI.Motion.UIGradient>();
                if (degrade != null)
                    degrade.Definir(Color.white, new Color(0.82f, 0.82f, 0.86f, 1f));
            }
        }

        private static readonly Color FundoDoCard = new Color(0.098f, 0.106f, 0.153f, 1f);

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

        /// <summary>
        /// Prende uma aba ao canto superior esquerdo da faixa, na caixa que ela tem no documento.
        ///
        /// As abas do dump não compartilham âncora — o navegador resolveu cada uma pela borda mais
        /// próxima, e a fileira de baixo ficou presa no RODAPÉ da faixa. Enquanto a faixa tinha
        /// altura fixa dava no mesmo; ao ganhar uma terceira fileira, cada uma foi para um lado.
        /// Uma âncora só, em coordenadas do documento, torna a faixa previsível.
        /// </summary>
        private static void PosicionarAba(RectTransform aba, float x, float y, float w, float h)
        {
            aba.anchorMin = new Vector2(0f, 1f);
            aba.anchorMax = new Vector2(0f, 1f);
            aba.pivot = new Vector2(0f, 1f);
            aba.anchoredPosition = new Vector2(x, -y);
            aba.sizeDelta = new Vector2(w, h);
        }

        /// <summary>
        /// Faz o rótulo da aba clonada preencher a aba, centrado.
        ///
        /// A cópia herda a caixa de texto de "ADESIVOS", dimensionada para oito letras e presa à
        /// esquerda: "MOTOR" ficaria descolado para um lado e "NEBLINA" encostaria na moldura. No
        /// design a folga é a mesma dos dois lados, então centrar reproduz o mesmo resultado sem
        /// depender de acertar a largura do texto.
        /// </summary>
        private static void CentralizarRotulo(RectTransform aba)
        {
            TextMeshProUGUI texto = aba.GetComponentInChildren<TextMeshProUGUI>(true);
            if (texto == null)
                return;

            var r = (RectTransform)texto.transform;
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            texto.alignment = TextAlignmentOptions.Center;
            texto.enableWordWrapping = false;
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
