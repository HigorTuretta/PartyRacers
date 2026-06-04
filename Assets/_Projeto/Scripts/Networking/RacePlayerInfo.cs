namespace PartyRacers.Networking
{
    // Diferencia o jogador local, jogadores remotos e bots — base para sincronização futura.
    public enum PlayerKind
    {
        Local,
        Remote,
        Bot
    }

    // Descreve um participante da corrida/lobby de forma agnóstica à camada de rede.
    // A camada online (NetworkBootstrap/Lobby) preenche estes dados; o modo local também os usa.
    [System.Serializable]
    public class RacePlayerInfo
    {
        public string Id;
        public string DisplayName;
        public PlayerKind Kind;
        public bool IsReady;
        public bool IsHost;
        public int CarIndex;
        public int ColorIndex;
        public string ElementData;

        // Atribuído pelo RaceSpawnManager na largada.
        public int SpawnIndex = -1;

        public RacePlayerInfo(string id, string displayName, PlayerKind kind)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
        }

        public bool IsLocal => Kind == PlayerKind.Local;
        public bool IsBot => Kind == PlayerKind.Bot;
        public bool IsRemote => Kind == PlayerKind.Remote;
    }
}
