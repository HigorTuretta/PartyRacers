using System.Collections.Generic;
using UnityEngine;
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
    public class RaceResultUI : MonoBehaviour
    {
        [Header("Tela 11 já montada na cena")]
        [SerializeField] private GameObject telaResultado;
        [SerializeField] private ResultScreenUI resultado;

        [Header("Dados")]
        [SerializeField] private RaceHUDDataProvider dados;

        [Tooltip("Segundos entre atualizações enquanto os retardatários ainda cruzam a linha.")]
        [SerializeField] private float intervaloAtualizacao = 1f;

        [Header("Saída da pista (não existe ScreenRouter numa cena de corrida)")]
        [SerializeField] private UnityEngine.UI.Button btnVoltarGaragem;
        [SerializeField] private UnityEngine.UI.Button btnJogarNovamente;
        [Tooltip("Cena do frontend carregada por VOLTAR À GARAGEM. Precisa estar no Build Settings.")]
        [SerializeField] private string cenaDoFrontend = "Frontend";

        private KartRaceTracker rastreadorLocal;
        private bool aberta;
        private float proximaAtualizacao;

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
            if (telaResultado != null)
                telaResultado.SetActive(false);
        }

        /// <summary>Sai da pista para o frontend. Não mexe em Time.timeScale — nunca houve pausa.</summary>
        public void VoltarAoFrontend()
        {
            if (string.IsNullOrEmpty(cenaDoFrontend))
            {
                Debug.LogWarning("[RaceResultUI] 'cenaDoFrontend' vazio — configure no Inspector.");
                return;
            }

            UnityEngine.SceneManagement.SceneManager.LoadScene(cenaDoFrontend);
        }

        /// <summary>Recarrega a pista atual para outra partida.</summary>
        public void Recomecar()
        {
            UnityEngine.SceneManagement.Scene atual = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(atual.buildIndex >= 0 ? atual.buildIndex : 0);
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

            Preencher();
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
