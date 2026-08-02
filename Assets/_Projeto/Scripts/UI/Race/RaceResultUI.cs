using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using PartyRacers.Networking;
using PartyRacers.UI.Frontend;
using PartyRacers.UI.HUD;

namespace PartyRacers.UI.Race
{
    /// <summary>
    /// Liga o fim da corrida à tela 11 (Screen_Result), que substitui a antiga RaceFinishScreen.
    /// Não monta nada: só lê os dados do provedor e entrega prontos para o ResultScreenUI já
    /// montado na cena. A corrida continua rodando atrás — nada de Time.timeScale.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public class RaceResultUI : MonoBehaviour
    {
        [Header("Tela 11 já montada na cena")]
        [SerializeField] private GameObject telaResultado;
        [SerializeField] private ResultScreenUI resultado;

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Header("Apresentacao ao terminar")]
        [Tooltip("HUD de corrida que deve desaparecer quando o resultado abrir.")]
        [SerializeField] private GameObject hudDaCorrida;
        [Tooltip("Camera da corrida. Se vazio, e localizada automaticamente.")]
        [SerializeField] private CinemachineCamera cameraDaCorrida;

        [Tooltip("Segundos entre atualizações enquanto os retardatários ainda cruzam a linha.")]
        [SerializeField] private float intervaloAtualizacao = 1f;

        [Header("Saída da pista (não existe ScreenRouter numa cena de corrida)")]
        [SerializeField] private UnityEngine.UI.Button btnVoltarGaragem;
        [SerializeField] private UnityEngine.UI.Button btnJogarNovamente;
        [Tooltip("Cena do frontend carregada por VOLTAR À GARAGEM. Precisa estar no Build Settings.")]
        [SerializeField] private string cenaDoFrontend = "Frontend";
        [SerializeField] private LoadingScreenUI telaDeCarregamento;
        [Tooltip("Rótulo padrão do botão de revanche (restaurado quando o jogador pode reiniciar).")]
        [SerializeField] private string rotuloRevanche = "JOGAR NOVAMENTE";

        private KartRaceTracker rastreadorLocal;
        private bool aberta;
        private float proximaAtualizacao;
        private KartController alvoEspectador;

        private void Reset()
        {
            dados = FindAnyObjectByType<RaceHUDDataProvider>();
            resultado = FindAnyObjectByType<ResultScreenUI>(FindObjectsInactive.Include);
        }

        private void Awake()
        {
            if (btnVoltarGaragem != null) btnVoltarGaragem.onClick.AddListener(VoltarAoFrontend);
            if (btnJogarNovamente != null) btnJogarNovamente.onClick.AddListener(Recomecar);
        }

        private void OnEnable()
        {
            aberta = false;
            alvoEspectador = null;
            if (telaResultado != null)
                telaResultado.SetActive(false);
            if (hudDaCorrida != null)
                hudDaCorrida.SetActive(true);
        }

        /// <summary>
        /// Sai da pista para o frontend. Online é obrigatório ENCERRAR a sessão antes: com o
        /// gerenciamento de cenas do Netcode ativo, um cliente que chama LoadScene direto é
        /// ignorado — era por isso que só o dono da sala conseguia voltar ao lobby.
        /// </summary>
        public void VoltarAoFrontend()
        {
            if (string.IsNullOrEmpty(cenaDoFrontend))
            {
                Debug.LogWarning("[RaceResultUI] 'cenaDoFrontend' vazio — configure no Inspector.");
                return;
            }

            NetworkBootstrap rede = NetworkBootstrap.Instance;
            if (rede != null && rede.IsOnline)
                rede.LeaveGame();

            LoadingScreenUI loading = LoadingScreenUI.Resolver(telaDeCarregamento);
            if (loading != null)
                loading.CarregarCena(cenaDoFrontend, "VOLTANDO AO LOBBY");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(cenaDoFrontend);
        }

        /// <summary>
        /// Recarrega a pista atual para outra partida. Numa sala online quem recarrega é o dono —
        /// o Netcode leva todo mundo junto. Um cliente pedindo revanche sozinho só se desincronizaria.
        /// </summary>
        public void Recomecar()
        {
            UnityEngine.SceneManagement.Scene atual = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            NetworkBootstrap rede = NetworkBootstrap.Instance;
            if (rede != null && rede.IsOnline)
            {
                if (rede.Mode != NetworkBootstrap.SessionMode.Host)
                {
                    DefinirAvisoDeRevanche("AGUARDANDO O DONO DA SALA");
                    return;
                }

                rede.RestartRaceScene(atual.name);
                return;
            }

            LoadingScreenUI loading = LoadingScreenUI.Resolver(telaDeCarregamento);
            if (loading != null && !string.IsNullOrEmpty(atual.name))
                loading.CarregarCena(atual.name, "PREPARANDO REVANCHE");
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(atual.buildIndex >= 0 ? atual.buildIndex : 0);
        }

        /// <summary>
        /// Ajusta o botão de revanche ao papel do jogador: quem não é dono da sala não pode
        /// recarregar a pista, e um botão que não faz nada é pior do que um botão explicando por quê.
        /// </summary>
        private void AtualizarBotaoDeRevanche()
        {
            if (btnJogarNovamente == null)
                return;

            NetworkBootstrap rede = NetworkBootstrap.Instance;
            bool online = rede != null && rede.IsOnline;
            bool podeReiniciar = !online || rede.Mode == NetworkBootstrap.SessionMode.Host;

            btnJogarNovamente.interactable = podeReiniciar;
            DefinirAvisoDeRevanche(podeReiniciar ? rotuloRevanche : "AGUARDANDO O DONO DA SALA");
        }

        private void DefinirAvisoDeRevanche(string texto)
        {
            if (btnJogarNovamente == null || string.IsNullOrWhiteSpace(texto))
                return;

            TMPro.TextMeshProUGUI label = btnJogarNovamente.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label != null)
                label.text = texto;
        }

        private void OnDisable() => Desassinar();

        private void Update()
        {
            if (dados == null)
                return;

            dados.Refresh();
            AssinarRastreadorLocal();

            if (!aberta)
                return;

            // a lista se reordena conforme os retardatários cruzam a linha (PLACA, tela 11)
            if (Time.unscaledTime < proximaAtualizacao)
                return;

            proximaAtualizacao = Time.unscaledTime + Mathf.Max(0.1f, intervaloAtualizacao);
            Preencher();
        }

        private void LateUpdate()
        {
            if (!aberta)
                return;

            AtualizarAlvoEspectador();
            AplicarCameraEspectador();
        }

        private void AssinarRastreadorLocal()
        {
            KartRaceTracker atual = dados.LocalKart != null
                ? dados.LocalKart.GetComponent<KartRaceTracker>()
                : null;

            if (atual == rastreadorLocal)
                return;

            Desassinar();
            rastreadorLocal = atual;

            if (rastreadorLocal == null)
                return;

            rastreadorLocal.RaceJustFinished += AoTerminar;

            // entrou depois da linha de chegada (reconexão): já abre a tela
            if (rastreadorLocal.RaceFinished)
                AoTerminar(rastreadorLocal);
        }

        private void Desassinar()
        {
            if (rastreadorLocal != null)
                rastreadorLocal.RaceJustFinished -= AoTerminar;
            rastreadorLocal = null;
        }

        private void AoTerminar(KartRaceTracker _)
        {
            aberta = true;
            proximaAtualizacao = 0f;

            if (telaResultado != null)
                telaResultado.SetActive(true);
            if (hudDaCorrida != null)
                hudDaCorrida.SetActive(false);

            AtualizarBotaoDeRevanche();
            AtualizarAlvoEspectador();
            AplicarCameraEspectador();

            Preencher();
        }

        private void AtualizarAlvoEspectador()
        {
            if (EstaCorrendo(alvoEspectador))
                return;

            if (dados == null)
                return;

            foreach (RaceHUDDataProvider.Standing standing in dados.Standings)
            {
                if (!EstaCorrendo(standing.Kart))
                    continue;

                alvoEspectador = standing.Kart;
                return;
            }

            // Quando o ultimo retardatario termina, mantem o ultimo enquadramento em vez de
            // devolver abruptamente a camera ao kart local parado.
            if (alvoEspectador == null)
                alvoEspectador = dados.LocalKart;
        }

        private void AplicarCameraEspectador()
        {
            if (alvoEspectador == null)
                return;

            if (cameraDaCorrida == null)
                cameraDaCorrida = FindAnyObjectByType<CinemachineCamera>(FindObjectsInactive.Include);
            if (cameraDaCorrida == null)
                return;

            Transform follow = EncontrarAlvo(alvoEspectador.transform, "CameraFollowTarget");
            Transform look = EncontrarAlvo(alvoEspectador.transform, "CameraLookTarget");
            cameraDaCorrida.Follow = follow != null ? follow : alvoEspectador.transform;
            cameraDaCorrida.LookAt = look != null ? look : alvoEspectador.transform;
        }

        private static bool EstaCorrendo(KartController kart)
        {
            if (kart == null || !kart.gameObject.activeInHierarchy)
                return false;

            KartRaceTracker tracker = kart.GetComponent<KartRaceTracker>();
            return tracker != null && !tracker.RaceFinished;
        }

        private static Transform EncontrarAlvo(Transform raiz, string nome)
        {
            Transform direto = raiz.Find(nome);
            if (direto != null)
                return direto;

            foreach (Transform candidato in raiz.GetComponentsInChildren<Transform>(true))
            {
                if (candidato.name == nome)
                    return candidato;
            }

            return null;
        }

        private void Preencher()
        {
            if (resultado == null)
                return;

            var lista = new List<ResultScreenUI.Resultado>();

            foreach (RaceHUDDataProvider.Standing s in dados.Standings)
            {
                KartRaceTracker t = s.Kart != null ? s.Kart.GetComponent<KartRaceTracker>() : null;
                bool terminou = t != null && t.RaceFinished;

                lista.Add(new ResultScreenUI.Resultado
                {
                    posicao = s.Position,
                    nome = s.DisplayName,
                    tempoTotal = t != null ? t.TotalRaceTime : 0f,
                    melhorVolta = s.BestLapTime,
                    ehLocal = s.IsLocal,
                    situacao = terminou ? ResultScreenUI.Situacao.Terminou : ResultScreenUI.Situacao.Correndo,
                    voltaAtual = t != null ? t.CurrentLap : 0,
                    totalVoltas = t != null ? t.TotalLaps : dados.TotalLaps
                });
            }

            resultado.Mostrar(lista);
        }
    }
}
