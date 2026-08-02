using UnityEngine;

namespace PartyRacers.UI.Settings
{
    /// <summary>
    /// Dados de apresentação de um poder, para o designer editar sem programador.
    /// Ufo é usado como SwapPosition e Mine representa a Armadilha Elétrica. Oil
    /// permanece disponível para uma futura implementação de gameplay.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Racers/Power Definition", fileName = "Power_")]
    public class PowerDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public KartPowerType tipo = KartPowerType.Rocket;
        [Tooltip("Nome em PT-BR mostrado sob o slot.")]
        public string nomeExibido = "FOGUETE";

        [Header("Ícones (Art/UI/Powers)")]
        public Sprite iconeColorido;
        public Sprite iconeMono;
        public Sprite iconeCinza;
    }
}
