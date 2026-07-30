using UnityEngine;

namespace PartyRacers.UI.Settings
{
    /// <summary>
    /// Dados de apresentação de um poder, para o designer editar sem programador.
    /// A arte traz 5 poderes (Mine, Oil, Rocket, Shield, Ufo) mas o gameplay só tem
    /// None/SwapPosition/Rocket/Shield — Ufo é usado como SwapPosition; Mine e Oil
    /// ficam disponíveis para quando virarem gameplay.
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
