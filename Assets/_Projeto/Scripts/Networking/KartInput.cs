namespace PartyRacers.Networking
{
    // Estado de input do kart, independente da origem (teclado/gamepad local, rede ou bot).
    // Separar input da física permite, no futuro, alimentar o KartController com input de rede
    // sem reescrever o controle — preservando exatamente o comportamento local atual.
    public struct KartInputState
    {
        public float Throttle;
        public float Brake;
        public float Steer;
        public bool Handbrake;
        public bool HandbrakePressed;

        public static KartInputState Neutral => new KartInputState();
    }

    // Fonte de input plugável. Quando nenhuma é atribuída, o KartController usa o caminho local
    // original (teclado/gamepad) sem qualquer mudança de comportamento.
    public interface IKartInputSource
    {
        KartInputState Read();
    }
}
