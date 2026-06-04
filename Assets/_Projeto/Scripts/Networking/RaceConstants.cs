namespace PartyRacers.Networking
{
    // Constantes centrais da corrida. Single source of truth para o limite de jogadores e afins,
    // já mirando corridas online de até 16 jogadores.
    public static class RaceConstants
    {
        // Limite principal de jogadores por corrida.
        public const int MaxPlayers = 16;

        // Mínimo para iniciar uma partida (host conta como 1).
        public const int MinPlayersToStart = 1;

        // Nome lógico do lobby/relay (usado pela camada online quando habilitada).
        public const string DefaultLobbyName = "PartyRacers Lobby";

        // Cena de corrida padrão carregada quando a partida inicia.
        public const string RaceSceneName = "DEMO";

        // Cena de garagem/lobby.
        public const string GarageSceneName = "Garage";

        // Espaçamento da grid de largada (metros) entre colunas e fileiras.
        public const float GridColumnSpacing = 3.2f;
        public const float GridRowSpacing = 4.5f;

        // Colunas da grid de largada (16 jogadores => 8 fileiras de 2).
        public const int GridColumns = 2;
    }
}
