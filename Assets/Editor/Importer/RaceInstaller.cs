using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.HUD;
using PartyRacers.UI.Race;

namespace PartyRacers.UI.Importer
{
    /// <summary>
    /// Leva o sistema de vida e o HUD v2 para dentro do jogo: adiciona
    /// <see cref="KartHealth"/> e <see cref="KartShieldAbility"/> aos prefabs de kart e troca o HUD
    /// das pistas pela tela nova.
    ///
    /// Os componentes vão no PREFAB, não nas instâncias das cenas: cada pista instancia o kart do
    /// mesmo prefab, então pôr nas instâncias significaria repetir o trabalho por pista e esquecer
    /// a próxima que for criada.
    /// </summary>
    public static class RaceInstaller
    {
        private static readonly string[] Karts =
        {
            "Assets/_Projeto/Prefabs/Cars/PlayerKart_Local.prefab",
            "Assets/_Projeto/Prefabs/Cars/PlayerKart_Network.prefab",
        };

        private static readonly string[] Pistas =
        {
            "Assets/_Projeto/Scenes/MiniGolfeRun.unity",
            "Assets/_Projeto/Scenes/TowerDefenseRun.unity",
            "Assets/_Projeto/Scenes/Race.unity",
        };

        private const string HudNovo = "Assets/_Projeto/Prefabs/UI_v2/Screens/Screen_RaceHUD_PC.prefab";
        private const string MenuDaPartida = "Assets/_Projeto/Prefabs/UI_v2/Screens/Screen_RaceMenu.prefab";

        private const string FumacaDeAvaria =
            "Assets/Hovl Studio/Magic effects pack/Prefabs/Smoke effects/Smoke loop.prefab";

        private const string FonteDoNumero = "Assets/_Projeto/Art/Fonts/TitanOne/TitanOne SDF.asset";

        [MenuItem("Party Racers/UI v2/7 · Instalar vida e HUD nas pistas", priority = 22)]
        public static void Instalar()
        {
            var log = new StringBuilder();

            foreach (string caminho in Karts)
                Kart(caminho, log);

            foreach (string caminho in Pistas)
                Pista(caminho, log);

            AssetDatabase.SaveAssets();
            Debug.Log("[UI v2] Corrida atualizada:\n" + log);
        }

        // ------------------------------------------------------------------ Kart

        private static void Kart(string caminho, StringBuilder log)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (prefab == null)
            {
                log.AppendLine($"  ! kart ausente: {caminho}");
                return;
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(caminho);
            bool mudou = false;

            if (raiz.GetComponent<KartHealth>() == null)
            {
                raiz.AddComponent<KartHealth>();
                mudou = true;
                log.AppendLine($"  + KartHealth em {System.IO.Path.GetFileName(caminho)}");
            }

            if (raiz.GetComponent<KartShieldAbility>() == null)
            {
                raiz.AddComponent<KartShieldAbility>();
                mudou = true;
                log.AppendLine($"  + KartShieldAbility em {System.IO.Path.GetFileName(caminho)}");
            }

            // O número de dano sobe AO LADO DO CARRO. A HUD diz quanto sobrou; ela não diz quanto
            // acabou de sair, e quem corre não desvia o olho da pista para descobrir.
            KartDamagePopup popup = raiz.GetComponent<KartDamagePopup>();
            if (popup == null)
            {
                popup = raiz.AddComponent<KartDamagePopup>();
                mudou = true;
                log.AppendLine($"  + KartDamagePopup em {System.IO.Path.GetFileName(caminho)}");
            }

            // A fonte do jogo, não a padrão do TMP: o número vive no meio da pista e precisa ser
            // da mesma família dos números grandes do HUD.
            var soPopup = new SerializedObject(popup);
            SerializedProperty campoDaFonte = soPopup.FindProperty("fonte");
            if (campoDaFonte != null && campoDaFonte.objectReferenceValue == null)
            {
                campoDaFonte.objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(FonteDoNumero);
                soPopup.ApplyModifiedPropertiesWithoutUndo();
                mudou = true;
            }

            // Fumaça de avaria: o estado quebrado só existia na HUD, e quem corre nessa hora está
            // olhando para a pista. O efeito põe a avaria onde ela acontece.
            if (raiz.GetComponent<KartBrokenSmoke>() == null)
            {
                var fumaca = raiz.AddComponent<KartBrokenSmoke>();
                var prefabDaFumaca = AssetDatabase.LoadAssetAtPath<GameObject>(FumacaDeAvaria);

                var so = new SerializedObject(fumaca);
                so.FindProperty("fumacaPrefab").objectReferenceValue = prefabDaFumaca;
                so.ApplyModifiedPropertiesWithoutUndo();

                mudou = true;
                log.AppendLine($"  + KartBrokenSmoke em {System.IO.Path.GetFileName(caminho)}"
                               + (prefabDaFumaca == null ? " (SEM prefab de fumaça!)" : string.Empty));
            }

            if (mudou)
                PrefabUtility.SaveAsPrefabAsset(raiz, caminho);

            PrefabUtility.UnloadPrefabContents(raiz);
        }

        // ------------------------------------------------------------------ Pista

        private static void Pista(string caminho, StringBuilder log)
        {
            if (!System.IO.File.Exists(caminho))
            {
                log.AppendLine($"  ! pista ausente: {caminho}");
                return;
            }

            Scene cena = EditorSceneManager.OpenScene(caminho, OpenSceneMode.Additive);
            log.AppendLine($"  · {cena.name}");

            GameObject[] raizes = cena.GetRootGameObjects();

            BolasDeGolfe(raizes, log);

            var provider = raizes.Select(g => g.GetComponentInChildren<RaceHUDDataProvider>(true))
                                 .FirstOrDefault(c => c != null);

            if (provider == null)
            {
                log.AppendLine("      ! sem RaceHUDDataProvider — HUD não trocado");
                EditorSceneManager.CloseScene(cena, true);
                return;
            }

            Transform canvas = AcharCanvas(raizes);
            if (canvas == null)
            {
                log.AppendLine("      ! sem Canvas de HUD");
                EditorSceneManager.CloseScene(cena, true);
                return;
            }

            // O HUD antigo é desligado, não apagado: ele ainda é referenciado por objetos da cena,
            // e voltar atrás precisa ser reativar um objeto.
            //
            // O sufixo só entra UMA vez. Sem o teste de `_v1` o instalador renomeava o que já era
            // `Screen_RaceHUD_PC_v1` e a cena juntava `_v1_v1` a cada execução.
            foreach (Transform t in canvas.Cast<Transform>().ToList())
            {
                if (t.name.StartsWith("Screen_RaceHUD") && !EhV2(t.gameObject))
                {
                    if (!t.name.EndsWith("_v1"))
                        t.name += "_v1";

                    Desligar(t.gameObject);
                    log.AppendLine($"      {t.name} desligado");
                }
            }

            GameObject hud = Colocar(canvas, HudNovo, "Screen_RaceHUD_PC");
            GameObject menu = Colocar(canvas, MenuDaPartida, "Screen_RaceMenu");

            if (hud != null)
            {
                LigarProvider(hud, provider);
                log.AppendLine("      + Screen_RaceHUD_PC (ligado ao provider)");

                // Quem esconde e reexibe o HUD no fim da corrida guarda uma referência a ele. Sem
                // reapontar, o RaceResultUI religa a tela ANTIGA ao voltar do resultado — e as duas
                // aparecem empilhadas, com duas classificações e dois toasts.
                int reapontados = Reapontar(raizes, "hudDaCorrida", hud);
                if (reapontados > 0)
                    log.AppendLine($"      {reapontados} referência(s) de HUD reapontada(s)");

                // A tela de resultado desenha DEPOIS do HUD. Sem isso a corrida terminava, a tela
                // abria — e o HUD continuava por cima dela, com classificação, vida e escudo
                // riscando o pódio. Parecia que a corrida não tinha acabado.
                foreach (GameObject raiz in raizes)
                {
                    Transform resultado = raiz.transform.Find("Screen_Result")
                                       ?? Achar(raiz.transform, "Screen_Result");
                    if (resultado != null && resultado.parent == hud.transform.parent)
                    {
                        resultado.SetAsLastSibling();
                        log.AppendLine("      Screen_Result acima do HUD");
                    }
                }
            }

            if (menu != null)
            {
                menu.transform.SetAsLastSibling();

                // A tela fica ATIVA. Quem escuta o ESC é o `Update` do `RaceMenuUI`, e componente
                // em objeto desligado não roda Update — com a raiz desativada o menu era
                // impossível de abrir, e o jogador ficava preso na corrida sem como sair da sala.
                // Quem nasce fechado são as PEÇAS dele (véu, gaveta, pop-up), pelo próprio Awake.
                menu.SetActive(true);

                var menuUI = menu.GetComponent<RaceMenuUI>();
                if (menuUI != null && hud != null)
                {
                    var grupo = hud.GetComponent<CanvasGroup>() ?? hud.AddComponent<CanvasGroup>();
                    Referencia(menuUI, "hudDaCorrida", grupo);
                }

                Transform carregando = raizes.Select(g => Achar(g.transform, "Screen_Loading"))
                                             .FirstOrDefault(t => t != null);
                if (menuUI != null && carregando != null)
                    Referencia(menuUI, "telaDeCarregamento", carregando.GetComponent<LoadingScreenUI>());

                // O resumo do rodapé lê o MESMO provider da HUD, e traduz o nome da cena pelo
                // catálogo de pistas — que é asset de editor e não se resolve em runtime sozinho.
                if (menuUI != null)
                {
                    Referencia(menuUI, "dados", provider);
                    Catalogo(menuUI, log);
                }

                log.AppendLine("      + Screen_RaceMenu (ativo; ESC abre a gaveta)");
            }

            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            EditorSceneManager.CloseScene(cena, true);
        }

        /// <summary>Preenche a lista de pistas do menu com o catálogo do projeto.</summary>
        private static void Catalogo(RaceMenuUI menu, StringBuilder log)
        {
            var so = new SerializedObject(menu);
            SerializedProperty lista = so.FindProperty("pistas");
            if (lista == null || !lista.isArray)
                return;

            string[] guids = AssetDatabase.FindAssets("t:TrackDefinition",
                                                      new[] { "Assets/_Projeto/Settings/Tracks" });
            lista.arraySize = guids.Length;

            for (int i = 0; i < guids.Length; i++)
            {
                string caminho = AssetDatabase.GUIDToAssetPath(guids[i]);
                lista.GetArrayElementAtIndex(i).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PartyRacers.UI.Settings.TrackDefinition>(caminho);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"      catálogo de pistas: {guids.Length}");
        }

        /// <summary>
        /// Todo binder do HUD lê do mesmo <see cref="RaceHUDDataProvider"/>.
        ///
        /// Um provider por binder pareceria funcionar e não funciona: as telas referenciam UMA
        /// instância, e duplicá-la faz cada binder ver um estado diferente do mesmo kart.
        /// </summary>
        private static void LigarProvider(GameObject hud, RaceHUDDataProvider provider)
        {
            foreach (MonoBehaviour c in hud.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var so = new SerializedObject(c);
                SerializedProperty p = so.FindProperty("dados");

                if (p != null && p.propertyType == SerializedPropertyType.ObjectReference)
                {
                    p.objectReferenceValue = provider;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        /// <summary>
        /// Reaponta para o HUD novo todo campo com este nome que ainda olha para o antigo.
        ///
        /// O campo pode ser <c>GameObject</c> ou <c>CanvasGroup</c> conforme o binder (o menu da
        /// partida escurece o HUD pelo grupo; o resultado o liga e desliga pelo objeto).
        /// </summary>
        private static int Reapontar(GameObject[] raizes, string campo, GameObject novo)
        {
            int n = 0;

            foreach (GameObject raiz in raizes)
            {
                foreach (MonoBehaviour c in raiz.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (c == null)
                        continue;

                    var so = new SerializedObject(c);
                    SerializedProperty p = so.FindProperty(campo);
                    if (p == null || p.propertyType != SerializedPropertyType.ObjectReference)
                        continue;

                    Object atual = p.objectReferenceValue;

                    // Campo VAZIO também é reapontado. `hudDaCorrida` estava nulo na cena — o HUD
                    // que ele apontava foi removido em alguma instalação anterior —, então a tela
                    // de resultado não tinha o que esconder e o HUD ficava por cima do pódio.
                    if (atual == null)
                    {
                        p.objectReferenceValue = novo;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(c);
                        n++;
                        continue;
                    }

                    var comoGrupo = atual as CanvasGroup;
                    var alvo = comoGrupo != null
                        ? (Object)(novo.GetComponent<CanvasGroup>() ?? novo.AddComponent<CanvasGroup>())
                        : novo;

                    if (atual == alvo)
                        continue;

                    p.objectReferenceValue = alvo;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    n++;
                }
            }

            return n;
        }

        /// <summary>
        /// As bolas gigantes passam a tirar vida, PROPORCIONAL à velocidade e com teto de 30.
        ///
        /// Elas eram o único obstáculo grande da MiniGolfeRun que só empurrava — atravessar uma
        /// bola a 180 km/h saía de graça. O teto existe porque bola não é armadilha: ela está
        /// parada no caminho, e cobrar mais que 30 por um erro de traçado transformaria a pista
        /// inteira num campo minado.
        ///
        /// O componente vai em cada filho que TEM collider: `OnCollisionEnter` só chega no objeto
        /// do collider, e a raiz "BolaGigante..." é só um agrupador.
        /// </summary>
        private static void BolasDeGolfe(GameObject[] raizes, StringBuilder log)
        {
            int postos = 0;

            foreach (GameObject raiz in raizes)
            {
                foreach (Transform t in raiz.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("BolaGigante", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    foreach (Collider c in t.GetComponentsInChildren<Collider>(true))
                    {
                        if (c.GetComponent<ObstacleKnockback>() != null)
                            continue;

                        var golpe = c.gameObject.AddComponent<ObstacleKnockback>();
                        var so = new SerializedObject(golpe);
                        so.FindProperty("dealsTrapDamage").boolValue = true;
                        so.FindProperty("danoProporcionalAVelocidade").boolValue = true;
                        so.FindProperty("tetoDeDano").intValue = 30;
                        so.FindProperty("limiarDeVelocidade").floatValue = 0f;
                        so.FindProperty("shieldBlocksLaunch").boolValue = true;

                        // A bola já empurra por física; o arremesso do knockback seria dobrado.
                        so.FindProperty("launchSpeed").floatValue = 6f;
                        so.FindProperty("upwardSpeed").floatValue = 2.5f;
                        so.ApplyModifiedPropertiesWithoutUndo();

                        postos++;
                    }
                }
            }

            if (postos > 0)
                log.AppendLine($"      {postos} bola(s) de golfe agora tiram vida (teto 30, por velocidade)");
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

        /// <summary>Procura um filho pelo nome em toda a árvore.</summary>
        private static Transform Achar(Transform raiz, string nome)
        {
            if (raiz.name == nome)
                return raiz;

            for (int i = 0; i < raiz.childCount; i++)
            {
                Transform achado = Achar(raiz.GetChild(i), nome);
                if (achado != null)
                    return achado;
            }

            return null;
        }

        private static Transform AcharCanvas(GameObject[] raizes)
        {
            // O HUD tem que entrar no Canvas de UI da pista, não em qualquer um: um Canvas de
            // mundo (nome de jogador acima do kart) tem escala em metros e engoliria a tela.
            foreach (GameObject g in raizes)
            {
                foreach (Canvas c in g.GetComponentsInChildren<Canvas>(true))
                {
                    if (c.renderMode != RenderMode.WorldSpace && c.transform.parent == null)
                        return c.transform;
                }
            }

            return raizes.Select(g => g.GetComponentInChildren<Canvas>(true))
                         .FirstOrDefault(c => c != null && c.renderMode != RenderMode.WorldSpace)
                         ?.transform;
        }

        private static GameObject Colocar(Transform canvas, string caminho, string nome)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (asset == null)
                return null;

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
            return instancia;
        }

        /// <summary>
        /// Desliga um objeto de forma que a CENA guarde isso.
        ///
        /// `SetActive(false)` numa instância de prefab é uma modificação como outra qualquer, e ela
        /// só sobrevive ao salvar se estiver registrada como override — sem isso a HUD antiga
        /// voltava ligada em play, empilhada sob a nova, com duas classificações na tela.
        /// </summary>
        private static void Desligar(GameObject go)
        {
            go.SetActive(false);
            EditorUtility.SetDirty(go);

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return;

            var so = new SerializedObject(go);
            SerializedProperty ativo = so.FindProperty("m_IsActive");
            if (ativo != null)
            {
                ativo.boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(go);
        }

        private static bool EhV2(GameObject go)
        {
            string caminho = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return !string.IsNullOrEmpty(caminho) && caminho.StartsWith("Assets/_Projeto/Prefabs/UI_v2/");
        }
    }
}
