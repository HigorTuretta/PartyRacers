using UnityEngine;

namespace PartyRacers.UI.Settings
{
    /// <summary>Recompensa de um nível da trilha do passe (tela 09).</summary>
    [CreateAssetMenu(menuName = "Party Racers/Pass Tier", fileName = "Tier_")]
    public class PassTierDefinition : ScriptableObject
    {
        public int nivel = 1;
        public string nomeExibido = "Recompensa";
        public Sprite arte;
        [Tooltip("Marcado = faixa premium (exige passe). Desmarcado = faixa grátis.")]
        public bool premium;
    }
}
