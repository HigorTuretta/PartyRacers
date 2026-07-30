using UnityEngine;

namespace PartyRacers.UI.Settings
{
    /// <summary>
    /// Uma pista jogável, do jeito que o designer edita: nome exibido, cena a carregar,
    /// miniatura e quantas voltas ela tem. A cena é guardada pelo nome porque é assim que o
    /// <see cref="UnityEngine.SceneManagement.SceneManager"/> carrega — o nome precisa estar
    /// no Build Settings.
    /// </summary>
    [CreateAssetMenu(menuName = "Party Racers/Pista", fileName = "Pista_")]
    public class TrackDefinition : ScriptableObject
    {
        [Header("Identidade")]
        [Tooltip("Nome mostrado na seleção de mapa.")]
        public string nome = "PISTA";

        [Tooltip("Nome da cena no Build Settings.")]
        public string cena;

        [Header("Vitrine")]
        public Sprite miniatura;

        [Tooltip("Posição na seleção. Menor aparece primeiro.")]
        public int ordem;

        [Tooltip("Uma linha sobre a pista. Vazio = não mostra.")]
        [TextArea(1, 2)]
        public string descricao;

        [Header("Regras")]
        [Tooltip("Voltas da corrida (vem do KartRaceTracker do kart, então hoje vale para " +
                 "qualquer pista). 0 = não mostrar.")]
        public int voltas;

        /// <summary>Falso quando a definição está incompleta e não dá para correr nela.</summary>
        public bool Jogavel => !string.IsNullOrWhiteSpace(cena);
    }
}
