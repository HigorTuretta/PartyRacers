using UnityEngine;

namespace PartyRacers.UI.Settings
{
    /// <summary>Item da loja (tela 08). Só cosmético — nada que altere desempenho.</summary>
    [CreateAssetMenu(menuName = "Party Racers/Store Item", fileName = "Item_")]
    public class StoreItemDefinition : ScriptableObject
    {
        [Header("Identidade")]
        public string nomeExibido = "Item";
        public Raridade raridade = Raridade.Comum;
        public Sprite arte;

        [Header("Preço")]
        public Moeda moeda = Moeda.Moedas;
        public int preco = 100;
        [Tooltip("0 = sem promoção. Preço cheio riscado ao lado.")]
        public int precoCheio;

        [Header("Disponibilidade")]
        public bool jaAdquirido;
        [Tooltip("0 = liberado. Acima disso, o card aparece bloqueado por nível.")]
        public int nivelMinimo;
    }
}
