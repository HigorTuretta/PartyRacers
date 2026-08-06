using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.Frontend.Party;
using PartyRacers.UI.Garage;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Coloca as telas v2 dentro da <c>Frontend.unity</c> e liga o que só existe na CENA: o
    /// roteador, o grupo, o carro do palco 3D e o fluxo.
    ///
    /// As telas do v1 não são apagadas — são desligadas e renomeadas com sufixo <c>_v1</c>. Elas
    /// continuam referenciadas pelo <see cref="FrontendFlow"/>, que escreve nelas sem prejuízo, e a
    /// volta atrás é reativar duas GameObjects em vez de recuperar arquivo de backup.
    ///
    /// A cena é aberta em modo ADITIVO de propósito: abrir em Single descarta trabalho não salvo de
    /// quem estiver com o Editor aberto.
    /// </summary>
    public static class FrontendInstaller
    {
        private const string CenaFrontend = "Assets/_Projeto/Scenes/Frontend.unity";
        private const string PrefabRoot = "Assets/_Projeto/Prefabs/UI_v2/Screens";

        [MenuItem("Party Racers/UI v2/6 · Instalar as telas no Frontend", priority = 21)]
        public static void Instalar()
        {
            var log = new StringBuilder();

            Scene cena = EditorSceneManager.OpenScene(CenaFrontend, OpenSceneMode.Additive);
            GameObject[] raizes = cena.GetRootGameObjects();

            Transform canvas = raizes.FirstOrDefault(g => g.name == "Canvas_UI")?.transform;
            var roteador = raizes.Select(g => g.GetComponent<ScreenRouter>()).FirstOrDefault(c => c != null);
            var fluxo = raizes.Select(g => g.GetComponent<FrontendFlow>()).FirstOrDefault(c => c != null);

            if (canvas == null || roteador == null)
            {
                Debug.LogError("[UI v2] Frontend.unity sem Canvas_UI ou ScreenRouter.");
                EditorSceneManager.CloseScene(cena, true);
                return;
            }

            // ---- telas antigas ficam desligadas, não apagadas
            foreach ((string antigo, string novo) in new[]
                     {
                         ("Screen_Lobby", "Screen_Lobby_v1"),
                         ("Screen_Garage_PC", "Screen_Garage_v1"),
                         ("Screen_Store", "Screen_Store_v1"),
                         ("Screen_BattlePass", "Screen_BattlePass_v1"),
                         ("Screen_Settings", "Screen_Settings_v1"),
                         ("Screen_Result", "Screen_Result_v1"),
                         ("Screen_JoinCode", "Screen_JoinCode_v1"),
                         ("Screen_Loading", "Screen_Loading_v1"),
                     })
            {
                Transform t = canvas.Find(antigo);
                if (t == null)
                    continue;

                // Reinstalar não pode renomear a tela v2 da rodada anterior: ela viraria mais um
                // `_v1` desligado, e a cada execução a cena ganhava uma cópia morta. Só a tela do
                // v1 de verdade — a que não veio do nosso prefab — recebe o sufixo.
                if (EhV2(t.gameObject))
                    continue;

                t.name = novo;
                t.gameObject.SetActive(false);
                log.AppendLine($"  {antigo} → {novo} (desligada)");
            }

            // Limpa as cópias que as execuções anteriores deixaram para trás.
            foreach (Transform t in canvas.Cast<Transform>().ToList())
            {
                if (t.name.EndsWith("_v1") && EhV2(t.gameObject))
                {
                    log.AppendLine($"  - {t.name} (cópia repetida de instalação anterior)");
                    Object.DestroyImmediate(t.gameObject);
                }
            }

            // ---- telas novas
            GameObject lobby = Colocar(canvas, "Screen_Lobby", log);
            GameObject garagem = Colocar(canvas, "Screen_Garage", log);
            GameObject sala = Colocar(canvas, "Screen_CustomMatch", log);
            GameObject busca = Colocar(canvas, "Screen_Matchmaking", log);
            GameObject loja = Colocar(canvas, "Screen_Store", log);
            GameObject passe = Colocar(canvas, "Screen_BattlePass", log);
            GameObject ajustes = Colocar(canvas, "Screen_Settings", log);
            GameObject resultado = Colocar(canvas, "Screen_Result", log);
            GameObject codigo = Colocar(canvas, "Screen_JoinCode", log);
            GameObject carregando = Colocar(canvas, "Screen_Loading", log);

            // O matchmaking é MODAL sobre o lobby (§3 da proposta): fica por cima e começa
            // desligado. Não é destino do roteador — cancelar é fechar, não é voltar.
            if (busca != null)
            {
                busca.transform.SetAsLastSibling();
                busca.SetActive(false);
            }

            // ---- grupo
            var controlador = raizes.Select(g => g.GetComponentInChildren<PartyController>(true))
                                    .FirstOrDefault(c => c != null);

            if (controlador == null)
            {
                var go = new GameObject("Party");
                SceneManager.MoveGameObjectToScene(go, cena);
                controlador = go.AddComponent<PartyController>();
                var mm = go.AddComponent<MatchmakingService>();
                Referencia(controlador, "matchmaking", mm);
                log.AppendLine("  + Party (PartyController + MatchmakingService)");
            }

            // Sempre, não só ao criar: é este elo que fecha o fluxo da partida — o matchmaking
            // avisa `MatchReady`, o controlador chama `FrontendFlow.CorrerEm(cena)` e a pista
            // carrega. Sem ele o BUSCAR PARTIDA enche a sala e não acontece mais nada.
            Referencia(controlador, "fluxo", fluxo);
            log.AppendLine($"  Party → FrontendFlow: {(fluxo != null ? "ok" : "FLUXO AUSENTE")}");

            // ---- ligações que só a cena conhece
            foreach (GameObject tela in new[]
                     { lobby, garagem, sala, busca, loja, passe, ajustes, resultado, codigo, carregando })
            {
                if (tela == null)
                    continue;

                foreach (NavBarUI nav in tela.GetComponentsInChildren<NavBarUI>(true))
                    nav.DefinirRoteador(roteador);
            }

            if (lobby != null)
            {
                var ui = lobby.GetComponent<PublicLobbyScreenUI>();
                Referencia(ui, "controlador", controlador);
                Referencia(ui, "roteador", roteador);
                Referencia(ui, "modalDeBusca", busca != null ? busca.GetComponent<MatchmakingModalUI>() : null);
            }

            if (busca != null)
                Referencia(busca.GetComponent<MatchmakingModalUI>(), "controlador", controlador);

            if (sala != null)
                Referencia(sala.GetComponent<CustomMatchScreenUI>(), "fluxo", fluxo);

            if (garagem != null)
                LigarGaragem(garagem, raizes, log);

            EnquadramentoDoV2(fluxo, log);
            FundoAtrasDoCarro(raizes, log);

            // O roteador precisa de si mesmo nas telas que navegam sozinhas (voltar, cancelar).
            foreach (GameObject tela in new[] { ajustes, resultado, codigo })
            {
                if (tela == null)
                    continue;

                foreach (MonoBehaviour c in tela.GetComponents<MonoBehaviour>())
                    Referencia(c, "roteador", roteador);
            }

            // A tela de carregamento é a última coisa que fica por cima de tudo.
            if (carregando != null)
            {
                carregando.transform.SetAsLastSibling();
                carregando.SetActive(false);
            }

            if (codigo != null)
                codigo.SetActive(false);

            // ---- roteador
            RegistrarTelas(roteador, new[]
            {
                ("Lobby", lobby),
                ("Garagem", garagem),
                ("CustomMatch", sala),
                ("Loja", loja),
                ("Passe", passe),
                ("Config", ajustes),
                ("Resultado", resultado),
                ("JoinCode", codigo),
                ("Loading", carregando),
            }, log);

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            EditorSceneManager.CloseScene(cena, true);

            Debug.Log("[UI v2] Frontend atualizado:\n" + log);
        }

        // ------------------------------------------------------------------ Peças

        private static GameObject Colocar(Transform canvas, string nome, StringBuilder log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/{nome}.prefab");
            if (asset == null)
            {
                log.AppendLine($"  ! prefab ausente: {nome}");
                return null;
            }

            // Reinstalar substitui a instância anterior: manter as duas faria dois binders
            // disputarem o mesmo grupo, e a tela passaria a discordar de si mesma.
            Transform antiga = canvas.Find(nome);
            if (antiga != null)
                Object.DestroyImmediate(antiga.gameObject);

            var instancia = (GameObject)PrefabUtility.InstantiatePrefab(asset, canvas);
            instancia.name = nome;

            var r = (RectTransform)instancia.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            r.localScale = Vector3.one;

            log.AppendLine($"  + {nome}");
            return instancia;
        }

        private static void LigarGaragem(GameObject garagem, GameObject[] raizes, StringBuilder log)
        {
            var grid = garagem.GetComponent<GarageGridUI>();
            if (grid == null)
                return;

            // O carro do palco já existe na cena (Turntable/PreviewCar) e é o mesmo que o lobby
            // mostra. A garagem só passa a mandar nele.
            var carro = raizes.Select(g => g.GetComponentInChildren<KartVisualCustomizer>(true))
                              .FirstOrDefault(c => c != null);
            Referencia(grid, "carro", carro);
            LimparCarroSalvo(carro, log);

            // O rig da câmera vive na CÂMERA e precisa saber o palco e o carro: as poses são
            // medidas a partir da caixa do modelo, não de coordenadas fixas. Sem essas duas
            // referências ele não tem o que enquadrar e devolvia a origem do mundo — que é
            // exatamente onde o kart está, então a câmera ia parar dentro dele.
            Camera cam = raizes.Select(g => g.GetComponentInChildren<Camera>(true))
                               .FirstOrDefault(c => c != null && c.CompareTag("MainCamera"));

            var rig = raizes.Select(g => g.GetComponentInChildren<GarageCameraRig>(true))
                            .FirstOrDefault(c => c != null);

            if (rig == null && cam != null)
                rig = cam.gameObject.AddComponent<GarageCameraRig>();

            if (rig != null)
            {
                Transform palco = raizes.FirstOrDefault(g => g.name == "Turntable")?.transform;
                Referencia(rig, "camera3D", cam);
                Referencia(rig, "palco", palco);
                Referencia(rig, "carro", carro != null ? carro.transform : palco);

                // Com o customizador o rig mira a PEÇA montada em vez de uma fração da caixa do
                // carro: é o que faz a câmera grande mostrar o mesmo recorte do card.
                Referencia(rig, "customizador", carro);
            }

            Referencia(grid, "camera3D", rig);

            // O fluxo precisa saber quem é o rig para tirar dele a posse da câmera assim que a tela
            // muda — senão o carro só volta ao lugar do lobby no último frame do fade.
            var fluxo = raizes.Select(g => g.GetComponent<PartyRacers.UI.Frontend.FrontendFlow>())
                              .FirstOrDefault(c => c != null);
            if (fluxo != null)
                Referencia(fluxo, "rigDaGaragem", rig);

            // O estúdio de preview vive ao lado do carro do palco: ele clona esse customizador
            // para montar as variantes sem tocar no carro que o jogador está vendo.
            if (carro != null)
            {
                var estudio = carro.GetComponent<PreviewStudio>()
                           ?? carro.gameObject.AddComponent<PreviewStudio>();

                Referencia(estudio, "referencia", carro);
                Referencia(grid, "estudio", estudio);
                log.AppendLine("  Garagem → estúdio de preview ligado ao carro do palco");
            }
            log.AppendLine($"  Garagem → rig de câmera: {(rig != null ? "ligado ao palco e ao carro" : "AUSENTE")}");
            log.AppendLine($"  Garagem → carro: {(carro != null ? carro.name : "NULO")}");
        }

        /// <summary>
        /// Tira da CENA o carro que ficou montado de alguma sessão de editor.
        ///
        /// Abrir a garagem no editor monta o rig sob `CarModelRoot`; salvar a cena depois disso
        /// grava esse carro junto. Em play ele não some — quem monta o carro do jogador nasce sem
        /// saber que ele existe — e os dois ficam empilhados: trocar de peça mexia num e a metade
        /// visível continuava igual. O runtime já limpa o nó ao montar; aqui a cena para de
        /// carregar o peso morto.
        /// </summary>
        private static void LimparCarroSalvo(KartVisualCustomizer carro, StringBuilder log)
        {
            if (carro == null)
                return;

            var so = new SerializedObject(carro);
            var raiz = so.FindProperty("carModelRoot")?.objectReferenceValue as Transform;
            if (raiz == null)
                return;

            int removidos = 0;
            for (int i = raiz.childCount - 1; i >= 0; i--)
            {
                Transform filho = raiz.GetChild(i);
                if (filho.GetComponent<ithappy.CarCustomizer>() == null)
                    continue;

                Object.DestroyImmediate(filho.gameObject);
                removidos++;
            }

            if (removidos > 0)
                log.AppendLine($"  carro do palco: {removidos} rig(s) salvo(s) na cena removido(s)");
        }

        private static void RegistrarTelas(ScreenRouter roteador,
                                           (string id, GameObject raiz)[] novas, StringBuilder log)
        {
            var so = new SerializedObject(roteador);
            SerializedProperty telas = so.FindProperty("telas");

            foreach ((string id, GameObject raiz) in novas)
            {
                if (raiz == null)
                    continue;

                SerializedProperty alvo = null;
                for (int i = 0; i < telas.arraySize; i++)
                {
                    SerializedProperty t = telas.GetArrayElementAtIndex(i);
                    if (t.FindPropertyRelative("id").stringValue == id)
                    {
                        alvo = t;
                        break;
                    }
                }

                if (alvo == null)
                {
                    telas.arraySize++;
                    alvo = telas.GetArrayElementAtIndex(telas.arraySize - 1);
                    alvo.FindPropertyRelative("id").stringValue = id;
                    log.AppendLine($"  roteador: + {id}");
                }
                else
                {
                    log.AppendLine($"  roteador: {id} → tela v2");
                }

                alvo.FindPropertyRelative("raiz").objectReferenceValue = raiz;
                alvo.FindPropertyRelative("grupo").objectReferenceValue = raiz.GetComponent<CanvasGroup>();
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Reenquadra o palco para a composição do v2.
        ///
        /// Os valores da cena vinham do v1, onde o lobby tinha painel à direita e o carro era
        /// empurrado para o lado. No v2 é o contrário do contrário:
        ///
        /// • <b>Lobby</b> — o kart do jogador fica SEMPRE centrado (§7). As colunas de UI são
        ///   laterais e o meio é zona franca, então viés zero: o carro no eixo da tela.
        /// • <b>Garagem</b> — o kart é centrado em x=1341, o centro do espaço livre à direita do
        ///   painel de customização, e não o centro da tela (§4). Daí o viés positivo.
        /// </summary>
        private static void EnquadramentoDoV2(FrontendFlow fluxo, StringBuilder log)
        {
            if (fluxo == null)
                return;

            var so = new SerializedObject(fluxo);
            Numero(so, "viesNoLobby", 0f);
            Numero(so, "viesVerticalNoLobby", -0.29f);
            Numero(so, "afastamentoNoLobby", 1.35f);
            Numero(so, "viesNaGaragem", 0.62f);
            Numero(so, "afastamentoNaGaragem", 1.35f);
            so.ApplyModifiedPropertiesWithoutUndo();

            log.AppendLine("  enquadramento: lobby centrado, garagem deslocada para a direita");
        }

        /// <summary>
        /// Empurra o Canvas de fundo para trás do carro.
        ///
        /// `Canvas_Fundo` é ScreenSpaceCamera com `planeDistance = 1`: a arte da oficina ficava a
        /// UMA unidade da câmera e o kart a onze, ou seja, o fundo na frente do carro. Como o
        /// canvas desenha na fila transparente, o que aparecia do kart eram só as peças também
        /// transparentes — adesivo, vidro, spoiler — soltas no ar. Parecia modelo quebrado e era
        /// ordem de profundidade.
        ///
        /// O canvas se redimensiona sozinho para continuar preenchendo a tela na nova distância.
        /// </summary>
        private static void FundoAtrasDoCarro(GameObject[] raizes, StringBuilder log)
        {
            foreach (GameObject raiz in raizes)
            {
                foreach (Canvas c in raiz.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.renderMode != RenderMode.ScreenSpaceCamera || c.planeDistance >= 60f)
                        continue;

                    c.planeDistance = 120f;
                    EditorUtility.SetDirty(c);
                    log.AppendLine($"  {c.name}: plano 1 → 120 (fundo atrás do carro)");
                }
            }
        }

        private static void Numero(SerializedObject so, string campo, float valor)
        {
            SerializedProperty p = so.FindProperty(campo);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
                p.floatValue = valor;
        }

        /// <summary>Rig só vale se alguém tiver preenchido as poses; vazio, ele zera a câmera.</summary>
        private static bool PoseConfigurada(GarageCameraRig rig)
        {
            var so = new SerializedObject(rig);
            SerializedProperty poses = so.FindProperty("poses");
            return poses != null && poses.isArray && poses.arraySize > 0
                && so.FindProperty("palco")?.objectReferenceValue != null;
        }

        /// <summary>Veio de um prefab nosso de `Prefabs/UI_v2/Screens`?</summary>
        private static bool EhV2(GameObject go)
        {
            string caminho = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return !string.IsNullOrEmpty(caminho) && caminho.StartsWith(PrefabRoot);
        }

        private static void Referencia(Object alvo, string campo, Object valor)
        {
            if (alvo == null)
                return;

            var so = new SerializedObject(alvo);
            SerializedProperty p = so.FindProperty(campo);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
            {
                p.objectReferenceValue = valor;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
