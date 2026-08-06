using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyRacers.Race
{
    /// <summary>
    /// As regras escolhidas na SALA PRIVADA, do lado de cá da troca de cena.
    ///
    /// A sala é frontend e a corrida é outra cena: qualquer referência direta morre no
    /// <c>LoadScene</c>. Um portador estático com persistência resolve — é o mesmo desenho do
    /// <see cref="KartGarageSelection"/>, que já leva a customização do carro para a pista.
    ///
    /// Só valem para as pistas que o projeto tem hoje (DEMO e MiniGolfeRun). Uma pista futura passa
    /// a obedecer sozinha: quem aplica são os componentes da corrida, não uma lista de cenas.
    /// </summary>
    public static class RaceRules
    {
        private const string ChaveVoltas = "regras.voltas";
        private const string ChaveItens = "regras.itens";
        private const string ChaveBots = "regras.bots";
        private const string ChaveDano = "regras.dano";
        private const string ChavePista = "regras.pista";

        /// <summary>Voltas da corrida. 0 = usa o valor que a pista já traz.</summary>
        public static int Voltas { get; set; }

        /// <summary>Caixas de item na pista.</summary>
        public static bool Itens { get; set; } = true;

        /// <summary>Bots completam a grade até o total de competidores.</summary>
        public static bool BotsPreenchem { get; set; } = true;

        /// <summary>Batida em parede tira vida.</summary>
        public static bool DanoPorColisao { get; set; } = true;

        /// <summary>Cena da pista escolhida na sala. Vazio = o jogo decide.</summary>
        public static string Pista { get; set; } = string.Empty;

        private static bool carregado;

        public static void Carregar()
        {
            if (carregado)
                return;

            carregado = true;
            Voltas = PlayerPrefs.GetInt(ChaveVoltas, 3);
            Itens = PlayerPrefs.GetInt(ChaveItens, 1) != 0;
            BotsPreenchem = PlayerPrefs.GetInt(ChaveBots, 1) != 0;
            DanoPorColisao = PlayerPrefs.GetInt(ChaveDano, 1) != 0;
            Pista = PlayerPrefs.GetString(ChavePista, string.Empty);
        }

        public static void Salvar()
        {
            carregado = true;
            PlayerPrefs.SetInt(ChaveVoltas, Voltas);
            PlayerPrefs.SetInt(ChaveItens, Itens ? 1 : 0);
            PlayerPrefs.SetInt(ChaveBots, BotsPreenchem ? 1 : 0);
            PlayerPrefs.SetInt(ChaveDano, DanoPorColisao ? 1 : 0);
            PlayerPrefs.SetString(ChavePista, Pista ?? string.Empty);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------ Aplicação

        /// <summary>
        /// Liga a aplicação das regras em toda cena carregada.
        ///
        /// Ganchado por código, não por um componente posto em cada pista: assim uma pista nova
        /// obedece às regras sem que ninguém precise lembrar de arrastar nada para dentro dela — e
        /// esquecer disso é o tipo de falha que só aparece em playtest.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Ligar()
        {
            Carregar();
            SceneManager.sceneLoaded -= AoCarregarCena;
            SceneManager.sceneLoaded += AoCarregarCena;
        }

        private static void AoCarregarCena(Scene cena, LoadSceneMode modo)
        {
            Carregar();
            Aplicar();
        }

        /// <summary>
        /// Aplica o que depende de objetos JÁ presentes na cena.
        ///
        /// Voltas e dano não passam por aqui: eles são lidos pelos próprios componentes do kart, que
        /// nascem depois — os karts são instanciados pelo <c>RaceManager</c>, então varrer a cena
        /// agora não acharia nenhum.
        /// </summary>
        public static void Aplicar()
        {
            // As caixas ficam na cena. Sem itens, elas somem em vez de nascerem vazias: uma caixa
            // que não dá nada ao ser atravessada é pior que caixa nenhuma.
            foreach (ItemBox caixa in Object.FindObjectsByType<ItemBox>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (caixa != null && caixa.gameObject.activeSelf == !Itens)
                    caixa.gameObject.SetActive(Itens);
            }

            // `fillOnStart` é lido no Start do gerente, e `sceneLoaded` acontece antes dele.
            foreach (PartyRacers.AI.RaceBotManager bots in Object.FindObjectsByType<PartyRacers.AI.RaceBotManager>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bots != null)
                    bots.DefinirPreenchimento(BotsPreenchem);
            }
        }
    }
}
