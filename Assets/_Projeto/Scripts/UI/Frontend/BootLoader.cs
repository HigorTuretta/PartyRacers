using UnityEngine;
using UnityEngine.SceneManagement;

namespace PartyRacers.UI.Frontend
{
    /// <summary>
    /// Cena Boot: manda a <see cref="LoadingScreenUI"/> carregar o frontend. Toda a animação
    /// (pulso, pontos, dicas rodando) vive na tela de carregamento, que é a mesma usada pelo
    /// frontend ao entrar na pista — assim as duas esperas se parecem.
    /// </summary>
    [DisallowMultipleComponent]
    public class BootLoader : MonoBehaviour
    {
        [Header("Tela 13 montada na cena")]
        [SerializeField] private LoadingScreenUI tela;

        [Header("Destino")]
        [SerializeField] private string cenaDestino = "Frontend";

        void Start()
        {
            if (string.IsNullOrEmpty(cenaDestino))
            {
                Debug.LogWarning("[BootLoader] 'cenaDestino' vazio — configure no Inspector.");
                return;
            }

            if (tela == null)
            {
                Debug.LogWarning("[BootLoader] sem LoadingScreenUI — carregando sem tela de espera.");
                SceneManager.LoadScene(cenaDestino);
                return;
            }

            tela.CarregarCena(cenaDestino, "CARREGANDO");
        }
    }
}
