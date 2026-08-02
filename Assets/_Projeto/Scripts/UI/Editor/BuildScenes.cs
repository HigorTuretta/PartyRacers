using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using PartyRacers.UI.Race;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.HUD;
using PartyRacers.UI.Motion;

namespace PartyRacers.UI.EditorTools
{
    /// <summary>
    /// Monta as cenas Boot, Frontend e Race com os prefabs Screen_* instanciados e os binders
    /// já vinculados. Ferramenta de editor: o resultado são cenas reais, editáveis à mão.
    /// </summary>
    public static class BuildScenes
    {
        const string CENAS = "Assets/_Projeto/Scenes";
        const string TELAS = "Assets/_Projeto/Prefabs/UI/Screens";

        /// <summary>
        /// Roda a cadeia inteira na ordem certa. Existe porque regenerar só os widgets deixa as
        /// telas antigas com overrides órfãos (o texto das abas ativas volta para "ABA"): quem
        /// mexe num widget precisa reconstruir todas as telas que o usam.
        /// </summary>
        [MenuItem("Party Racers/UI/0 - Gerar Tudo (widgets → telas → cenas)", priority = -100)]
        public static void GerarTudo()
        {
            BuildWidgets.Gerar();
            BuildItems.Gerar();
            BuildScreensRace.Gerar();
            BuildScreensFrontend.Gerar();
            BuildScreensMeta.Gerar();
            Gerar();
            AplicarNasPistas();
            Debug.Log("[PartyRacers] cadeia completa de UI regenerada");
        }

        [MenuItem("Party Racers/UI/6 - Gerar Cenas Boot, Frontend e Race")]
        public static void Gerar()
        {
            UIKit.GarantirPasta(CENAS);
            Boot();
            Frontend();
            Race();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyRacers] cenas Boot/Frontend/Race geradas em " + CENAS);
        }

        // ---------- infraestrutura comum ----------
        static (UnityEngine.SceneManagement.Scene cena, GameObject canvas, GameObject camera) NovaCena(string nome)
        {
            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIKit.Hex("#101334");
            cam.orthographic = true;

            var canvasGo = new GameObject("Canvas_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = canvasGo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;   // handoff §2

            // InputSystemUIInputModule, não StandaloneInputModule: o projeto está no Input System
            // e o módulo antigo derruba a UI inteira com InvalidOperationException.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            return (cena, canvasGo, camGo);
        }

        static GameObject Instanciar(string tela, Transform pai)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{TELAS}/{tela}.prefab");
            if (src == null) { Debug.LogWarning("[PartyRacers] tela não encontrada: " + tela); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, pai);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return go;
        }

        static void Salvar(UnityEngine.SceneManagement.Scene cena, string nome)
            => EditorSceneManager.SaveScene(cena, $"{CENAS}/{nome}.unity");

        static T Achar<T>(GameObject raiz, string caminho) where T : Component
        {
            var t = raiz.transform.Find(caminho);
            return t != null ? t.GetComponent<T>() : null;
        }

        static GameObject Obj(GameObject raiz, string caminho)
        {
            var t = raiz.transform.Find(caminho);
            return t != null ? t.gameObject : null;
        }

        /// <summary>
        /// Liga a LoadingScreenUI nas peças da tela 13. É a mesma tela no Boot e no Frontend,
        /// então a fiação vive aqui e as duas cenas chamam.
        /// </summary>
        public static LoadingScreenUI LigarCarregamento(GameObject loading)
        {
            if (loading == null) return null;

            var tela = loading.GetComponent<LoadingScreenUI>() ?? loading.AddComponent<LoadingScreenUI>();
            var grupo = loading.GetComponent<CanvasGroup>() ?? loading.AddComponent<CanvasGroup>();
            var overlay = loading.GetComponent<Canvas>();
            if (overlay == null)
                overlay = loading.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 32000;
            var overlaySerialized = new SerializedObject(overlay);
            overlaySerialized.FindProperty("m_OverrideSorting").boolValue = true;
            overlaySerialized.FindProperty("m_SortingOrder").intValue = 32000;
            overlaySerialized.ApplyModifiedPropertiesWithoutUndo();
            if (loading.GetComponent<GraphicRaycaster>() == null)
                loading.AddComponent<GraphicRaycaster>();

            Definir(tela, "grupo", grupo);
            Definir(tela, "textoEstado", Achar<TextMeshProUGUI>(loading, "Centro/Estado"));
            Definir(tela, "textoDica", Achar<TextMeshProUGUI>(loading, "Dica/Texto"));
            Definir(tela, "blocoConexao", Obj(loading, "Conexao"));
            Definir(tela, "intervaloDoPulso", 0.12f);
            Definir(tela, "tempoMinimo", 0.6f);

            var passos = new List<Object>();
            var pulso = loading.transform.Find("Centro/Pulso");
            if (pulso != null)
                foreach (Transform p in pulso)
                {
                    var img = p.GetComponent<Image>();
                    if (img != null) passos.Add(img);
                }
            DefinirArray(tela, "passosDoPulso", passos, typeof(Image));

            var pts = new List<Object>();
            var pontos = loading.transform.Find("Centro/Lockup/Pontos");
            if (pontos != null)
                foreach (Transform p in pontos) pts.Add(p as RectTransform);
            DefinirArray(tela, "pontos", pts, typeof(RectTransform));

            return tela;
        }

        // ═══════════ BOOT ═══════════
        static void Boot()
        {
            var (cena, canvas, _) = NovaCena("Boot");
            var loading = Instanciar("Screen_Loading", canvas.transform);

            if (loading != null)
            {
                var tela = LigarCarregamento(loading);
                var ui = loading.AddComponent<BootLoader>();
                Definir(ui, "tela", tela);
                Definir(ui, "cenaDestino", "Frontend");
            }

            Salvar(cena, "Boot");
        }

        /// <summary>
        /// Copia o palco do carro (Turntable + PreviewCar + luzes + câmera) da cena Garage para a
        /// cena atual. Copiar em vez de remontar preserva a lista de carros e a paleta que já
        /// estão configuradas lá — remontar do zero significaria reconfigurar tudo à mão.
        /// </summary>
        static KartVisualCustomizer MontarPalcoDoCarro(UnityEngine.SceneManagement.Scene destino, GameObject cameraAntiga)
        {
            const string GARAGEM = "Assets/_Projeto/Scenes/Garage.unity";
            if (AssetDatabase.LoadAssetAtPath<Object>(GARAGEM) == null)
            {
                Debug.LogWarning("[PartyRacers] Garage.unity não existe — frontend fica sem o carro 3D");
                return null;
            }

            var origem = EditorSceneManager.OpenScene(GARAGEM, OpenSceneMode.Additive);
            KartVisualCustomizer customizer = null;

            foreach (var nome in new[] { "Turntable", "Directional Light", "Fill Light", "Main Camera" })
            {
                GameObject fonte = null;
                foreach (var raiz in origem.GetRootGameObjects())
                    if (raiz.name == nome) { fonte = raiz; break; }

                if (fonte == null) { Debug.LogWarning("[PartyRacers] palco: não achei " + nome); continue; }

                var copia = Object.Instantiate(fonte);
                copia.name = nome == "Main Camera" ? "Main Camera" : nome;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copia, destino);

                if (nome == "Main Camera" && cameraAntiga != null)
                {
                    // a câmera ortográfica criada por NovaCena não renderiza o carro
                    Object.DestroyImmediate(cameraAntiga);
                    var cam = copia.GetComponent<Camera>();
                    if (cam != null)
                    {
                        cam.clearFlags = CameraClearFlags.SolidColor;
                        cam.backgroundColor = UIKit.Hex("#101334");
                    }
                }

                if (customizer == null)
                    customizer = copia.GetComponentInChildren<KartVisualCustomizer>(true);
            }

            EditorSceneManager.CloseScene(origem, true);
            return customizer;
        }

        // ═══════════ FRONTEND ═══════════
        static void Frontend()
        {
            var (cena, canvas, cameraPadrao) = NovaCena("Frontend");
            var carro = MontarPalcoDoCarro(cena, cameraPadrao);

            var lobby = Instanciar("Screen_Lobby", canvas.transform);
            var garagem = Instanciar("Screen_Garage_PC", canvas.transform);
            var loja = Instanciar("Screen_Store", canvas.transform);
            var passe = Instanciar("Screen_BattlePass", canvas.transform);
            var config = Instanciar("Screen_Settings", canvas.transform);
            var resultado = Instanciar("Screen_Result", canvas.transform);
            var joinCode = Instanciar("Screen_JoinCode", canvas.transform);

            // ---- roteador: o jogo abre no LOBBY ----
            var rotGo = new GameObject("ScreenRouter");
            var router = rotGo.AddComponent<ScreenRouter>();
            var telas = new List<ScreenRouter.Tela>();
            void Add(string id, GameObject go)
            {
                if (go == null) return;
                telas.Add(new ScreenRouter.Tela { id = id, raiz = go, grupo = go.GetComponent<CanvasGroup>() });
            }
            Add("Lobby", lobby); Add("Garagem", garagem); Add("Loja", loja);
            Add("Passe", passe); Add("Configuracoes", config);
            Add("Resultado", resultado); Add("JoinCode", joinCode);
            DefinirLista(router, "telas", telas.ConvertAll(t => (object)t));
            Definir(router, "telaInicial", "Lobby");

            // ---- navegação da barra superior ----
            foreach (var (tela, id) in new[] { (lobby, "Lobby"), (garagem, "Garagem"), (loja, "Loja"), (passe, "Passe") })
            {
                if (tela == null) continue;
                var nav = tela.transform.Find("Nav");
                if (nav == null) continue;
                foreach (var (aba, destino) in new[] { ("Tab_LOBBY", "Lobby"), ("Tab_GARAGEM", "Garagem"),
                                                        ("Tab_LOJA", "Loja"), ("Tab_PASSE", "Passe") })
                {
                    var btn = nav.Find(aba)?.GetComponent<Button>();
                    if (btn == null) continue;
                    string alvo = destino;
                    UnityEditor.Events.UnityEventTools.AddStringPersistentListener(
                        btn.onClick, router.Ir, alvo);
                }
            }

            // ---- binders ----
            if (lobby != null)
            {
                var ui = lobby.AddComponent<LobbyScreenUI>();
                var vagas = new List<Object>();
                var grade = lobby.transform.Find("Conteudo/Vagas");
                if (grade != null)
                    foreach (Transform v in grade) vagas.Add(v.gameObject);
                DefinirArray(ui, "vagas", vagas, typeof(GameObject));
                Definir(ui, "textoCodigo", Achar<TextMeshProUGUI>(lobby, "Conteudo/Topo/CodigoSala/Codigo"));
                Definir(ui, "btnCopiar", Achar<Button>(lobby, "Conteudo/Topo/CodigoSala/Btn_Copiar/Bg"));
                Definir(ui, "textoQuantidade", Achar<TextMeshProUGUI>(lobby, "Conteudo/Topo/Contagem/Valor"));
                Definir(ui, "textoMaximo", Achar<TextMeshProUGUI>(lobby, "Conteudo/Topo/Contagem/Maximo"));
                Definir(ui, "textoAviso", Achar<TextMeshProUGUI>(lobby, "Conteudo/Aviso/Texto"));
                Definir(ui, "btnEntrarPorCodigo", Achar<Button>(lobby, "Conteudo/Acoes/Btn_EntrarPorCodigo"));
                Definir(ui, "btnSairDaSala", Achar<Button>(lobby, "Conteudo/Acoes/Btn_SairDaSala"));
                Definir(ui, "estadoAguardando", Obj(lobby, "Conteudo/Acoes/EstadoPartida"));
                Definir(ui, "estadoPronto", Obj(lobby, "Conteudo/Acoes/State_Pronto"));
                Definir(ui, "roteador", router);
            }

            if (garagem != null)
            {
                var ui = garagem.AddComponent<GarageScreenUI>();
                Definir(ui, "nomeDoCarro", Achar<TextMeshProUGUI>(garagem, "SeletorCarro/NomeCarro"));
                Definir(ui, "btnCarroAnterior", Achar<Button>(garagem, "SeletorCarro/Btn_Anterior"));
                Definir(ui, "btnCarroProximo", Achar<Button>(garagem, "SeletorCarro/Btn_Proximo"));
                Definir(ui, "btnSalvarEVoltar", Achar<Button>(garagem, "Partida/Btn_Correr"));
                Definir(ui, "btnSalvarEstilo", Achar<Button>(garagem, "Partida/Btn_JogarLocalmente"));

                // a lista lê as variantes reais do rig do carro; nada de contador fixo aqui
                Definir(ui, "carro", carro);
                Definir(ui, "palco", carro != null ? RaizNaCena(carro.transform).GetComponent<CarStage>() : null);
                Definir(ui, "containerCategorias", garagem.transform.Find("PainelCustomizacao/Rolagem/Viewport/Categorias"));
                Definir(ui, "contagemDeCategorias", Achar<TextMeshProUGUI>(garagem, "PainelCustomizacao/Contagem"));
                Definir(ui, "categoriasRestantes", Achar<TextMeshProUGUI>(garagem, "PainelCustomizacao/Restantes"));
                Definir(ui, "containerIndicadores", garagem.transform.Find("SeletorCarro/Indicadores"));

                var gear = Achar<Button>(garagem, "Btn_Configuracoes");
                if (gear != null)
                    UnityEditor.Events.UnityEventTools.AddStringPersistentListener(gear.onClick, router.Ir, "Configuracoes");
            }

            if (loja != null)
            {
                var ui = loja.AddComponent<StoreScreenUI>();
                Definir(ui, "prefabCard", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/UI/Items/Item_StoreCard.prefab"));
                Definir(ui, "prefabDiario", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/UI/Items/Item_StoreDaily.prefab"));
                Definir(ui, "containerGrade", loja.transform.Find("Grade"));
                Definir(ui, "containerDiarios", loja.transform.Find("ColunaDireita/ItensDiarios/Lista"));
                Definir(ui, "textoTimer", Achar<TextMeshProUGUI>(loja, "ColunaDireita/ItensDiarios/Timer/Valor"));
                Definir(ui, "textoMoedas", Achar<TextMeshProUGUI>(loja, "Carteira/Moedas/Valor"));
                Definir(ui, "textoFichas", Achar<TextMeshProUGUI>(loja, "Carteira/Fichas/Valor"));
                Definir(ui, "iconeMoedas", UIKit.Sprite("Icons", "Icon_Coin"));
                Definir(ui, "iconeFichas", UIKit.Sprite("Icons", "Icon_Diamond"));

                // catálogo: a grade leva o destaque + os 4 cards; a coluna da direita, os diários
                DefinirArray(ui, "grade", Catalogo("t:StoreItemDefinition", "Assets/_Projeto/Settings/Store",
                    "Item_KitFoguetao", "Item_FuscaFestivo", "Item_RodasConfete", "Item_BuzinaCorneta", "Item_RastroEstrela"),
                    typeof(PartyRacers.UI.Settings.StoreItemDefinition));
                DefinirArray(ui, "diarios", Catalogo("t:StoreItemDefinition", "Assets/_Projeto/Settings/Store",
                    "Item_CapoXadrez", "Item_AdesivoChama", "Item_RodasPneuLargo"),
                    typeof(PartyRacers.UI.Settings.StoreItemDefinition));

                var verPasse = Achar<Button>(loja, "ColunaDireita/ChamadaPasse/Btn_VerPasse/Bg");
                if (verPasse != null)
                    UnityEditor.Events.UnityEventTools.AddStringPersistentListener(verPasse.onClick, router.Ir, "Passe");
            }

            if (passe != null)
            {
                var ui = passe.AddComponent<BattlePassScreenUI>();
                Definir(ui, "prefabTier", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/UI/Items/Item_PassTier.prefab"));
                Definir(ui, "faixaPremium", passe.transform.Find("Trilha/Faixa_Premium"));
                Definir(ui, "faixaGratis", passe.transform.Find("Trilha/Faixa_Gratis"));
                Definir(ui, "cabecalhoNiveis", passe.transform.Find("Trilha/Niveis"));
                Definir(ui, "textoNivel", Achar<TextMeshProUGUI>(passe, "Temporada/Info/Nivel/Valor"));
                Definir(ui, "rotuloProgresso", Achar<TextMeshProUGUI>(passe, "Temporada/Info/Progresso/Rotulo"));
                Definir(ui, "textoProgresso", Achar<TextMeshProUGUI>(passe, "Temporada/Info/Progresso/Valor"));
                Definir(ui, "barraProgresso", Achar<Image>(passe, "Temporada/Info/Progresso/Barra/Fill"));

                // trilha: todos os PassTierDefinition, ordenados por nível (o binder separa
                // premium de grátis pelo próprio asset)
                var tiers = new List<Object>();
                foreach (var g in AssetDatabase.FindAssets("t:PassTierDefinition", new[] { "Assets/_Projeto/Settings/Pass" }))
                    tiers.Add(AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(g)));
                tiers.Sort((a, b) =>
                {
                    var da = (PartyRacers.UI.Settings.PassTierDefinition)a;
                    var db = (PartyRacers.UI.Settings.PassTierDefinition)b;
                    int n = da.nivel.CompareTo(db.nivel);
                    return n != 0 ? n : da.premium.CompareTo(db.premium);
                });
                DefinirArray(ui, "recompensas", tiers, typeof(PartyRacers.UI.Settings.PassTierDefinition));
            }

            if (config != null)
            {
                var ui = config.AddComponent<SettingsScreenUI>();
                Definir(ui, "btnVoltar", Achar<Button>(config, "Btn_Voltar"));
                Definir(ui, "btnRestaurar", Achar<Button>(config, "Acoes/Btn_Restaurar"));
                Definir(ui, "btnCancelar", Achar<Button>(config, "Acoes/Btn_Cancelar"));
                Definir(ui, "btnAplicar", Achar<Button>(config, "Acoes/Btn_Aplicar"));
                Definir(ui, "roteador", router);

                var grupos = new List<object>();
                var lista = config.transform.Find("Grupos");
                var paineis = config.transform.Find("Painéis");
                var painelConta = config.transform.Find("Painel_Conta");
                if (lista != null)
                {
                    foreach (Transform g in lista)
                    {
                        string id = g.name.Replace("Grupo_", "");

                        // PLACA §10: os 4 grupos de jogo mostram a mesma grade 2×2;
                        // só CONTA troca o conteúdo da área por um painel próprio.
                        Transform painel = Semear(id) == "CONTA" ? painelConta : paineis;

                        grupos.Add(new SettingsScreenUI.Grupo
                        {
                            id = id,
                            botao = g.gameObject,
                            botaoIdle = g.Find("State_Idle")?.gameObject,
                            botaoAtivo = g.Find("State_Active")?.gameObject,
                            painel = painel != null ? painel.gameObject : null
                        });
                    }
                }
                DefinirLista(ui, "grupos", grupos);

                var ajustes = new List<object>();
                if (paineis != null)
                {
                    foreach (Transform p in paineis)
                    {
                        var linhas = p.Find("Linhas");
                        if (linhas == null) continue;
                        foreach (Transform linha in linhas)
                        {
                            var controle = linha.Find("Controle");
                            if (controle == null) continue;
                            var slider = controle.GetComponentInChildren<Slider>(true);
                            var toggle = controle.GetComponentInChildren<Toggle>(true);
                            if (slider == null && toggle == null) continue;

                            ajustes.Add(new SettingsScreenUI.Ajuste
                            {
                                chave = linha.name.Replace("Cfg_", ""),
                                slider = slider,
                                valor = slider != null ? slider.transform.parent.Find("Valor")?.GetComponent<TextMeshProUGUI>() : null,
                                interruptor = toggle,
                                interruptorOn = toggle != null ? toggle.transform.Find("On")?.gameObject : null,
                                interruptorOff = toggle != null ? toggle.transform.Find("Off")?.gameObject : null,
                                padrao = toggle != null ? 1 : 70
                            });
                        }
                    }
                }
                DefinirLista(ui, "ajustes", ajustes);
            }

            if (resultado != null)
            {
                var ui = resultado.AddComponent<ResultScreenUI>();
                Definir(ui, "prefabLinha", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/UI/Items/Item_ResultRow.prefab"));
                Definir(ui, "containerTabela", resultado.transform.Find("Tabela"));
                Definir(ui, "textoSuaPosicao", Achar<TextMeshProUGUI>(resultado, "Resumo/SuaPosicao/Valor"));
                Definir(ui, "textoTempoTotal", Achar<TextMeshProUGUI>(resultado, "Resumo/TempoTotal/Valor"));
                Definir(ui, "textoMelhorVolta", Achar<TextMeshProUGUI>(resultado, "Resumo/MelhorVolta/Valor"));
                Definir(ui, "textoAindaCorrendo", Achar<TextMeshProUGUI>(resultado, "AindaCorrendo/Texto"));
                Definir(ui, "blocoAindaCorrendo", Obj(resultado, "AindaCorrendo"));
                Definir(ui, "btnVoltarGaragem", Achar<Button>(resultado, "Rodape/Btn_VoltarGaragem"));
                Definir(ui, "btnJogarNovamente", Achar<Button>(resultado, "Rodape/Btn_JogarNovamente"));
                Definir(ui, "roteador", router);
            }

            if (joinCode != null)
            {
                var ui = joinCode.AddComponent<JoinCodeUI>();
                var caixas = new List<object>();
                var cont = joinCode.transform.Find("Modal/Caixas");
                if (cont != null)
                {
                    foreach (Transform c in cont)
                        caixas.Add(new JoinCodeUI.Caixa
                        {
                            raiz = c.gameObject,
                            caractere = c.Find("Caractere")?.GetComponent<TextMeshProUGUI>(),
                            estadoIdle = c.Find("State_Idle")?.gameObject,
                            estadoFoco = c.Find("State_Focused")?.gameObject,
                            estadoErro = c.Find("State_Error")?.gameObject
                        });
                }
                DefinirLista(ui, "caixas", caixas);
                Definir(ui, "estadoCodigoInvalido", Obj(joinCode, "Modal/Estados/State_CodigoInvalido"));
                Definir(ui, "estadoSalaCheia", Obj(joinCode, "Modal/Estados/State_SalaCheia"));
                Definir(ui, "estadoConectando", Obj(joinCode, "Modal/Estados/State_Conectando"));
                Definir(ui, "btnEntrar", Achar<Button>(joinCode, "Modal/Botoes/Btn_Entrar"));
                Definir(ui, "btnCancelar", Achar<Button>(joinCode, "Modal/Botoes/Btn_Cancelar"));
                Definir(ui, "roteador", router);
            }

            // ---- fluxo: o único ponto que sabe o que cada botão dispara ----
            var fluxo = rotGo.AddComponent<FrontendFlow>();
            Definir(fluxo, "roteador", router);
            Definir(fluxo, "lobby", lobby != null ? lobby.GetComponent<LobbyScreenUI>() : null);
            Definir(fluxo, "garagem", garagem != null ? garagem.GetComponent<GarageScreenUI>() : null);
            Definir(fluxo, "joinCode", joinCode != null ? joinCode.GetComponent<JoinCodeUI>() : null);
            Definir(fluxo, "loja", loja != null ? loja.GetComponent<StoreScreenUI>() : null);
            Definir(fluxo, "carro", carro);
            Definir(fluxo, "palco", carro != null ? RaizNaCena(carro.transform) : null);
            Definir(fluxo, "cameraDoPalco", Object.FindFirstObjectByType<Camera>());
            Definir(fluxo, "cenaDaCorrida", "MiniGolfeRun");

            // Estado inicial da cena: só o Lobby ligado. O roteador refaz isso no Start,
            // mas sem isto o designer abre a cena e vê as 7 telas empilhadas.
            foreach (var t in telas)
            {
                bool inicial = t.id == "Lobby";
                t.raiz.SetActive(inicial);
                if (t.grupo != null)
                {
                    t.grupo.alpha = inicial ? 1f : 0f;
                    t.grupo.interactable = inicial;
                    t.grupo.blocksRaycasts = inicial;
                }
            }

            Salvar(cena, "Frontend");
        }

        static string Semear(string id) => id.Replace("Á", "A").Replace("Í", "I").Replace("Ó", "O").ToUpperInvariant();

        // ═══════════ RACE ═══════════
        static void Race()
        {
            var (cena, canvas, _) = NovaCena("Race");
            MontarHUDDeCorrida(canvas.transform);
            Salvar(cena, "Race");
        }

        /// <summary>
        /// Monta a camada de UI da corrida (telas 01, 02, 11 e 12) sob o canvas informado e
        /// vincula todos os binders. Usada tanto pela cena Race quanto pelas cenas de pista.
        /// </summary>
        public static void MontarHUDDeCorrida(Transform canvas)
        {
            var provedorGo = new GameObject("RaceHUDDataProvider");
            var provedor = provedorGo.AddComponent<RaceHUDDataProvider>();

            var hud = Instanciar("Screen_RaceHUD_PC", canvas);
            var contagem = Instanciar("Screen_Countdown", canvas);
            var menu = Instanciar("Screen_RaceMenu", canvas);
            var resultado = Instanciar("Screen_Result", canvas);

            LigarHUD(hud, provedor);
            LigarContagem(contagem);
            LigarMenu(menu, hud);
            LigarResultado(resultado, provedor);
        }

        // ═══════════ CENAS DE PISTA ═══════════
        static readonly string[] Pistas =
        {
            "Assets/_Projeto/Scenes/DemoTrack/DEMO.unity",
            "Assets/_Projeto/Scenes/MiniGolfeRun.unity",
        };

        [MenuItem("Party Racers/UI/7 - Aplicar HUD nas Cenas de Pista")]
        public static void AplicarNasPistas()
        {
            foreach (var caminho in Pistas)
                AplicarEmPista(caminho);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PartyRacers] HUD aplicada em {Pistas.Length} cena(s) de pista");
        }

        static void AplicarEmPista(string caminho)
        {
            var cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Single);
            if (!cena.IsValid()) { Debug.LogWarning("[PartyRacers] cena inválida: " + caminho); return; }

            int removidos = RemoverHUDAntiga(cena);

            // canvas próprio da UI de corrida, separado de qualquer canvas de gameplay
            var canvasGo = new GameObject("Canvas_UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var sc = canvasGo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            sc.matchWidthOrHeight = 0.5f;

            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            MontarHUDDeCorrida(canvasGo.transform);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"[PartyRacers] {System.IO.Path.GetFileName(caminho)}: HUD nova aplicada ({removidos} objeto(s) da HUD antiga removido(s))");
        }

        /// <summary>Remove instâncias da RaceHUD antiga e canvases de HUD legados da cena.</summary>
        static int RemoverHUDAntiga(UnityEngine.SceneManagement.Scene cena)
        {
            var alvos = new List<GameObject>();

            foreach (var raiz in cena.GetRootGameObjects())
            {
                // qualquer objeto que ainda hospede um script da HUD antiga
                foreach (var c in raiz.GetComponentsInChildren<Component>(true))
                {
                    if (c == null)
                        continue;
                    string tipo = c.GetType().FullName ?? string.Empty;
                    if (tipo.StartsWith("PartyRacers.UI.HUD.") && tipo != typeof(RaceHUDDataProvider).FullName)
                    {
                        var topo = TopoDaHUD(c.transform, raiz.transform);
                        if (!alvos.Contains(topo)) alvos.Add(topo);
                    }
                }

                // sobras nomeadas do sistema anterior
                string n = raiz.name;
                if ((n == "RaceHUD" || n == "HUDCanvas_Root" || n.StartsWith("KartHUD") || n == "Canvas_UI")
                    && !alvos.Contains(raiz))
                    alvos.Add(raiz);
            }

            foreach (var go in alvos)
                Object.DestroyImmediate(go);

            return alvos.Count;
        }

        /// <summary>Sobe até o objeto raiz da cena a partir de um filho qualquer.</summary>
        static GameObject RaizNaCena(Transform t)
        {
            while (t.parent != null)
                t = t.parent;
            return t.gameObject;
        }

        /// <summary>Sobe até o objeto raiz da hierarquia da HUD (filho direto da cena).</summary>
        static GameObject TopoDaHUD(Transform t, Transform raiz)
        {
            while (t.parent != null && t != raiz)
                t = t.parent;
            return t.gameObject;
        }

        public static void LigarResultado(GameObject resultado, RaceHUDDataProvider provedor)
        {
            if (resultado == null)
                return;

            ResultScreenLayoutBuilder.Configure(resultado);

            var ui = resultado.GetComponent<ResultScreenUI>() ?? resultado.AddComponent<ResultScreenUI>();
            Definir(ui, "prefabLinha", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Projeto/Prefabs/UI/Items/Item_ResultRow.prefab"));
            Transform viewport = resultado.transform.Find("Tabela");
            Transform conteudo = viewport != null ? viewport.Find("Conteudo") : null;
            Definir(ui, "containerTabela", conteudo);
            Definir(ui, "viewportTabela", viewport as RectTransform);
            Definir(ui, "conteudoTabela", conteudo as RectTransform);
            Definir(ui, "gradeTabela", conteudo != null ? conteudo.GetComponent<GridLayoutGroup>() : null);
            Definir(ui, "cabecalhoDireito", conteudo != null ? Obj(conteudo.gameObject, "Cabecalho_Dir") : null);
            Definir(ui, "textoSuaPosicao", Achar<TextMeshProUGUI>(resultado, "Resumo/SuaPosicao/Valor"));
            Definir(ui, "textoTempoTotal", Achar<TextMeshProUGUI>(resultado, "Resumo/TempoTotal/Valor"));
            Definir(ui, "textoMelhorVolta", Achar<TextMeshProUGUI>(resultado, "Resumo/MelhorVolta/Valor"));
            Definir(ui, "textoAindaCorrendo", Achar<TextMeshProUGUI>(resultado, "AindaCorrendo/Texto"));
            Definir(ui, "blocoAindaCorrendo", Obj(resultado, "AindaCorrendo"));
            Definir(ui, "btnVoltarGaragem", Achar<Button>(resultado, "Rodape/Btn_VoltarGaragem"));
            Definir(ui, "btnJogarNovamente", Achar<Button>(resultado, "Rodape/Btn_JogarNovamente"));

            // O binder que abre a tela quando o jogador local cruza a linha NÃO pode morar na própria
            // tela: ela nasce desativada (SetActive(false) logo abaixo), e um MonoBehaviour em objeto
            // inativo não roda Update — ele nunca assinaria RaceJustFinished e a tela era impossível
            // de abrir. Pior: o OnEnable dele desativa a tela, então até uma ativação manual se
            // desfazia sozinha. Por isso ele vive no canvas, que está sempre ativo.
            var hospedeiro = resultado.transform.parent != null
                ? resultado.transform.parent.gameObject
                : resultado;

            var ponte = hospedeiro.GetComponent<RaceResultUI>() ?? hospedeiro.AddComponent<RaceResultUI>();
            Definir(ponte, "telaResultado", resultado);
            Definir(ponte, "resultado", ui);
            Definir(ponte, "dados", provedor);
            Definir(ponte, "hudDaCorrida", resultado.transform.parent != null
                ? resultado.transform.parent.Find("Screen_RaceHUD_PC")?.gameObject
                : null);
            Definir(ponte, "btnVoltarGaragem", Achar<Button>(resultado, "Rodape/Btn_VoltarGaragem"));
            Definir(ponte, "btnJogarNovamente", Achar<Button>(resultado, "Rodape/Btn_JogarNovamente"));

            resultado.SetActive(false);
        }

        /// <summary>Vincula os binders da HUD de corrida. Reutilizável por qualquer cena de pista.</summary>
        public static void LigarHUD(GameObject hud, RaceHUDDataProvider provedor)
        {
            if (hud == null)
                return;

            // ---- placa de volta ----
            var raceUI = hud.AddComponent<RaceHUDUI>();
            Definir(raceUI, "dados", provedor);
            Definir(raceUI, "textoVolta", Achar<TextMeshProUGUI>(hud, "LapPlate/Placa/FaixaVolta/Volta"));
            Definir(raceUI, "textoTempo", Achar<TextMeshProUGUI>(hud, "LapPlate/Placa/Tempo"));
            Definir(raceUI, "textoUltimaVolta", Achar<TextMeshProUGUI>(hud, "LapPlate/Chips/Chip_UltimaVolta/Label"));
            Definir(raceUI, "textoMelhorVolta", Achar<TextMeshProUGUI>(hud, "LapPlate/Chips/Chip_MelhorVolta/Label"));
            Definir(raceUI, "chipUltimaVolta", Obj(hud, "LapPlate/Chips/Chip_UltimaVoltaAviso"));

            // ---- classificação ----
            var standings = hud.AddComponent<StandingsUI>();
            Definir(standings, "dados", provedor);
            var linhas = new List<object>();
            var cont = hud.transform.Find("Standings/Container");
            if (cont != null)
                foreach (Transform linha in cont)
                    linhas.Add(MontarLinha(linha));
            DefinirLista(standings, "linhas", linhas);

            var local = hud.transform.Find("Standings/Local/Row_Local");
            if (local != null)
                DefinirObjeto(standings, "linhaLocal", MontarLinha(local));
            Definir(standings, "blocoLocal", Obj(hud, "Standings/Local"));

            // ---- slot de poder ----
            var slot = hud.transform.Find("PowerArea/PowerSlot_Principal");
            if (slot != null)
            {
                var powerUI = hud.AddComponent<PowerSlotUI>();
                Definir(powerUI, "dados", provedor);
                Definir(powerUI, "estadoVazio", slot.Find("Empty")?.gameObject);
                Definir(powerUI, "estadoCheio", slot.Find("Filled")?.gameObject);
                Definir(powerUI, "estadoRecarga", slot.Find("Recharging")?.gameObject);
                Definir(powerUI, "estadoBloqueado", slot.Find("Locked")?.gameObject);
                Definir(powerUI, "iconeCheio", slot.Find("Filled/Icon")?.GetComponent<Image>());
                Definir(powerUI, "iconeRecarga", slot.Find("Recharging/Icon")?.GetComponent<Image>());
                Definir(powerUI, "mascaraRecarga", slot.Find("Recharging/FillMask")?.GetComponent<Image>());
                Definir(powerUI, "iconeBloqueado", slot.Find("Locked/Icon")?.GetComponent<Image>());
                Definir(powerUI, "nomeDoPoder", Achar<TextMeshProUGUI>(hud, "PowerArea/NomePoder/Label"));
                Definir(powerUI, "cartaoDoNome", Obj(hud, "PowerArea/NomePoder"));
                Definir(powerUI, "dicaDeTecla", Obj(hud, "PowerArea/Tecla"));

                // os slots extras do mockup não têm fonte de dados: mostrar "cheio"/"recarregando"
                // sem nada por trás seria inventar estado. Ficam ocultos até existirem 2+ poderes.
                var extras = hud.transform.Find("PowerArea/SlotsExtras");
                if (extras != null) extras.gameObject.SetActive(false);

                var cat = new List<Object>();
                foreach (var guid in AssetDatabase.FindAssets("t:PowerDefinition"))
                    cat.Add(AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid)));
                DefinirArray(powerUI, "catalogo", cat, typeof(PartyRacers.UI.Settings.PowerDefinition));
            }

            // ---- arco de perigo ----
            var arcoUI = hud.AddComponent<DangerArcUI>();
            Definir(arcoUI, "arcoFraco", Obj(hud, "Overlays/Overlay_DangerArc"));
            Definir(arcoUI, "arcoForte", Obj(hud, "Overlays/Overlay_DangerArc_Strong"));
            Definir(arcoUI, "pulsoDeTras", Obj(hud, "Overlays/Overlay_DangerPulse"));
            Definir(arcoUI, "graficoFraco", Achar<Image>(hud, "Overlays/Overlay_DangerArc"));
            Definir(arcoUI, "graficoForte", Achar<Image>(hud, "Overlays/Overlay_DangerArc_Strong"));

            // ---- avisos ----
            var toastUI = hud.AddComponent<ToastNotificationUI>();
            Definir(toastUI, "dados", provedor);   // sem isso o aviso de todo kart vira toast
            var slotsToast = new List<object>();
            var toasts = hud.transform.Find("Toasts");
            if (toasts != null)
            {
                foreach (Transform t in toasts)
                    slotsToast.Add(new ToastNotificationUI.Slot
                    {
                        raiz = t.gameObject,
                        grupo = t.GetComponent<CanvasGroup>(),
                        texto = t.Find("Label")?.GetComponent<TextMeshProUGUI>(),
                        icone = t.Find("Icon")?.GetComponent<Image>()
                    });
            }
            DefinirLista(toastUI, "slots", slotsToast);
            Definir(toastUI, "iconeAcerto", UIKit.Sprite("Icons", "Icon_Check"));
            Definir(toastUI, "iconeEscudo", UIKit.Sprite("Icons", "Icon_Diamond"));
            Definir(toastUI, "iconeTroca", UIKit.Sprite("Icons", "Icon_ArrowUp"));
            Definir(toastUI, "iconePoder", UIKit.Sprite("Icons", "Icon_Star"));
            Definir(toastUI, "iconeTurbo", UIKit.Sprite("Icons", "Icon_Flag"));

            // os 3 avisos começam vazios
            if (toasts != null)
                foreach (Transform t in toasts) t.gameObject.SetActive(false);
        }

        static StandingsUI.Linha MontarLinha(Transform linha) => new StandingsUI.Linha
        {
            raiz = linha.gameObject,
            nome = linha.Find("Nome")?.GetComponent<TextMeshProUGUI>(),
            tempo = linha.Find("Tempo")?.GetComponent<TextMeshProUGUI>(),
            badgeOuro = linha.Find("Badge_Ouro")?.gameObject,
            badgePrata = linha.Find("Badge_Prata")?.gameObject,
            badgeBronze = linha.Find("Badge_Bronze")?.gameObject,
            badgeComum = linha.Find("Badge_Comum")?.gameObject,
            badgeLocal = linha.Find("Badge_Local")?.gameObject,
            valorOuro = linha.Find("Badge_Ouro/Valor")?.GetComponent<TextMeshProUGUI>(),
            valorPrata = linha.Find("Badge_Prata/Valor")?.GetComponent<TextMeshProUGUI>(),
            valorBronze = linha.Find("Badge_Bronze/Valor")?.GetComponent<TextMeshProUGUI>(),
            valorComum = linha.Find("Badge_Comum/Valor")?.GetComponent<TextMeshProUGUI>(),
            valorLocal = linha.Find("Badge_Local/Valor")?.GetComponent<TextMeshProUGUI>(),
            destaqueLocal = linha.Find("State_IsLocal")?.gameObject
        };

        public static void LigarContagem(GameObject contagem)
        {
            if (contagem == null)
                return;

            // O Screen_Countdown começa inativo. O binder precisa viver no Canvas (ativo),
            // senão nunca chega a assinar os eventos emitidos pelo RaceManager.
            GameObject host = contagem.transform.parent != null
                ? contagem.transform.parent.gameObject
                : contagem;

            if (host != contagem)
            {
                foreach (CountdownUI antigo in contagem.GetComponents<CountdownUI>())
                    Object.DestroyImmediate(antigo, true);
            }

            var ui = host.GetComponent<CountdownUI>() ?? host.AddComponent<CountdownUI>();
            Definir(ui, "raiz", contagem);
            Definir(ui, "passo3", Obj(contagem, "Centro/State_3"));
            Definir(ui, "passo2", Obj(contagem, "Centro/State_2"));
            Definir(ui, "passo1", Obj(contagem, "Centro/State_1"));
            Definir(ui, "passoJa", Obj(contagem, "Centro/State_Go"));
            contagem.SetActive(false);
        }

        public static void LigarMenu(GameObject menu, GameObject hud = null)
        {
            if (menu == null)
                return;

            var ui = menu.AddComponent<RaceMenuUI>();
            Definir(ui, "gaveta", Obj(menu, "Gaveta"));
            Definir(ui, "veu", Obj(menu, "Veu"));
            Definir(ui, "popupSair", Obj(menu, "Popup_Sair"));
            Definir(ui, "hudDaCorrida", hud != null ? hud.GetComponent<CanvasGroup>() : null);
            Definir(ui, "btnVoltar", Achar<Button>(menu, "Gaveta/Acoes/Btn_Voltar"));
            Definir(ui, "btnConfiguracoes", Achar<Button>(menu, "Gaveta/Acoes/Btn_Configuracoes"));
            Definir(ui, "btnCopiarCodigo", Achar<Button>(menu, "Gaveta/Acoes/Btn_CopiarCodigo"));
            Definir(ui, "btnSair", Achar<Button>(menu, "Gaveta/Acoes/Btn_Sair"));
            Definir(ui, "btnSairAgora", Achar<Button>(menu, "Popup_Sair/Botoes/Btn_SairAgora"));
            Definir(ui, "btnFicar", Achar<Button>(menu, "Popup_Sair/Botoes/Btn_Ficar"));

            // A gaveta nasce fechada — RaceMenuUI.Awake também fecha, mas assim a cena
            // já abre limpa no editor, sem o menu por cima da HUD.
            Obj(menu, "Gaveta")?.SetActive(false);
            Obj(menu, "Popup_Sair")?.SetActive(false);
            // o véu cobria a tela inteira a corrida toda, escurecendo o jogo
            Obj(menu, "Veu")?.SetActive(false);
        }

        // ---------- escrita em campos privados via SerializedObject ----------
        static void Definir(Object alvo, string campo, object valor)
        {
            var so = new SerializedObject(alvo);
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogWarning($"[PartyRacers] campo '{campo}' não existe em {alvo.GetType().Name}"); return; }

            if (valor is string s) p.stringValue = s;
            else if (valor is int i) p.intValue = i;
            else if (valor is float f) p.floatValue = f;
            else if (valor is bool b) p.boolValue = b;
            else
            {
                if (valor == null) Avisar($"{alvo.GetType().Name}.{campo}");
                p.objectReferenceValue = valor as Object;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Preenche uma lista de classes [Serializable] copiando campo a campo.</summary>
        static void DefinirLista(Object alvo, string campo, List<object> itens)
        {
            var so = new SerializedObject(alvo);
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogWarning($"[PartyRacers] lista '{campo}' não existe em {alvo.GetType().Name}"); return; }

            p.arraySize = itens.Count;
            for (int i = 0; i < itens.Count; i++)
                CopiarCampos(p.GetArrayElementAtIndex(i), itens[i], $"{alvo.GetType().Name}.{campo}[{i}]");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Preenche um único campo de classe [Serializable] (não-lista) copiando campo a campo.</summary>
        static void DefinirObjeto(Object alvo, string campo, object dados)
        {
            var so = new SerializedObject(alvo);
            var p = so.FindProperty(campo);
            if (p == null) { Debug.LogWarning($"[PartyRacers] campo '{campo}' não existe em {alvo.GetType().Name}"); return; }

            CopiarCampos(p, dados, $"{alvo.GetType().Name}.{campo}");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CopiarCampos(SerializedProperty destino, object origem, string rotulo)
        {
            foreach (var campoInfo in origem.GetType().GetFields())
            {
                var sub = destino.FindPropertyRelative(campoInfo.Name);
                if (sub == null) continue;
                object v = campoInfo.GetValue(origem);

                switch (sub.propertyType)
                {
                    case SerializedPropertyType.String: sub.stringValue = (string)(v ?? ""); break;
                    case SerializedPropertyType.Integer: sub.intValue = v is int n ? n : 0; break;
                    case SerializedPropertyType.Boolean: sub.boolValue = v is bool bo && bo; break;
                    case SerializedPropertyType.Float: sub.floatValue = v is float fl ? fl : 0f; break;
                    case SerializedPropertyType.ObjectReference:
                        if (v == null) Avisar($"{rotulo}.{campoInfo.Name}");
                        sub.objectReferenceValue = v as Object;
                        break;
                    case SerializedPropertyType.Generic:
                        if (v is List<Color> cores)
                        {
                            sub.arraySize = cores.Count;
                            for (int k = 0; k < cores.Count; k++)
                                sub.GetArrayElementAtIndex(k).colorValue = cores[k];
                        }
                        break;
                }
            }
        }

        /// <summary>Registra referência que não resolveu — caminho de hierarquia errado no prefab.</summary>
        static void Avisar(string onde) => Debug.LogWarning($"[PartyRacers] referência nula: {onde}");

        /// <summary>Carrega assets de catálogo pela ordem pedida (a ordem vira a ordem na tela).</summary>
        static List<Object> Catalogo(string filtro, string pasta, params string[] nomes)
        {
            var lista = new List<Object>();
            foreach (var nome in nomes)
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>($"{pasta}/{nome}.asset");
                if (asset == null) { Debug.LogWarning("[PartyRacers] catálogo: falta " + nome); continue; }
                lista.Add(asset);
            }
            return lista;
        }

        static void DefinirArray(Object alvo, string campo, List<Object> itens, System.Type tipo)
        {
            var so = new SerializedObject(alvo);
            var p = so.FindProperty(campo);
            if (p == null) return;
            p.arraySize = itens.Count;
            for (int i = 0; i < itens.Count; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = itens[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
