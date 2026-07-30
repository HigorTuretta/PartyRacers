namespace PartyRacers.UI.Settings
{
    // Cada ScriptableObject do catálogo mora no seu próprio arquivo (StoreItemDefinition.cs e
    // PassTierDefinition.cs): a Unity só cria asset de um tipo cujo nome bate com o do arquivo,
    // e com os dois aqui dentro a criação do PassTierDefinition falhava em silêncio.
    // Aqui ficam só os enums compartilhados.

    public enum Raridade { Comum, Raro, Epico, Lendario }

    public enum Moeda { Moedas, Fichas }
}
