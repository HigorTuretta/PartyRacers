using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Garagem. A lista de cosmeticos ROLA — paginacao e proibida
    /// (sem setas, sem contador 1/2).
    ///
    /// O preview de cada card e uma RenderTexture do item, nao um sprite:
    /// nao existe sprite pronto para "o carro que este jogador montou agora".
    /// Enquanto a RT nao renderizou, o preview fica com o fundo do USS mais
    /// Icon_Person — nunca um retangulo branco.
    /// </summary>
    public sealed class GarageController : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset itemTemplate;
        [SerializeField] PreviewStudio previewStudio;
        [SerializeField] StagePresenter stage;

        VisualElement _root, _grid, _camChip;

        public void Bind(VisualElement root)
        {
            _root    = root;
            _grid    = root.Q<VisualElement>("Item_Grid").Q<VisualElement>("unity-content-container");
            _camChip = root.Q<VisualElement>("CameraChip");

            foreach (var cat in new[] { "Modelo", "Cor", "Rodas", "Frente", "Traseira", "Teto", "Adesivos" })
            {
                var chip = root.Q<VisualElement>("Tab_" + cat);
                chip.Q<Button>("State_On_Btn").clicked  += () => SelectCategory(cat);
                chip.Q<Button>("State_Off_Btn").clicked += () => SelectCategory(cat);
            }
        }

        void SelectCategory(string cat)
        {
            // 1. chips: State_On no escolhido, State_Off nos outros
            // 2. grade: reconstroi a partir do template (nunca criando
            //    VisualElement a mao, nunca escrevendo estilo)
            // 3. camera: nova pose, blend 0.45s easeInOutCubic
            // 4. CameraChip aparece com "CAMERA \u00B7 <POSE>" e some 1,2s
            //    depois do blend terminar. Nunca fica parado e vazio.
        }

        void SetItemState(VisualElement card, string state)
        {
            UiStates.ShowOnly(card, state, "State_Equipped", "State_Selected", "State_Free", "State_Locked");
        }
    }
}
