using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Busca de partida. SEMPRE 16 vagas (8 x 2) — a grade e fixa no UXML,
    /// o controller so troca o estado de cada vaga.
    ///
    /// O limite de 40s NUNCA aparece na tela: o timer mostra apenas o tempo
    /// decorrido. Aos 40s: fecha a lista de humanos, cria a sala, preenche
    /// com bots, sorteia o mapa.
    /// </summary>
    public sealed class MatchmakingController : MonoBehaviour
    {
        [SerializeField] VisualTreeAsset blipTemplate;
        [SerializeField] FrontendRouter router;

        const int SlotCount = 16;
        const float SearchLimitSeconds = 40f;   // regra interna, nao vai para a UI

        VisualElement _root, _blips, _needleScan, _needleLock;
        Label _title, _sub, _timer, _hint, _roomCount;

        public void Bind(VisualElement root)
        {
            _root       = root;
            _blips      = root.Q<VisualElement>("Blips");
            _needleScan = root.Q<VisualElement>("Needle_Scan");
            _needleLock = root.Q<VisualElement>("Needle_Lock");
            _title      = root.Q<Label>("Title");
            _sub        = root.Q<Label>("Subtitle");
            _timer      = root.Q<Label>("Timer");
            _hint       = root.Q<Label>("Hint");
            _roomCount  = root.Q<Label>("Room_Count");

            root.Q<Button>("Btn_Cancel_Face").clicked += () => router.Go(ScreenId.Lobby);

            SetStage(0);
        }

        void SetStage(int stage)
        {
            // Trilha: PRONTOS / PROCURANDO / ENCONTRADOS / PREENCHENDO / CARREGANDO
            // Exatamente UM chip em State_Now. Os anteriores em State_Done,
            // os seguintes em State_Todo.
            for (int i = 0; i < 5; i++)
            {
                var chip = _root.Q<VisualElement>("Stage_" + i);
                string on = i < stage ? "State_Done" : i == stage ? "State_Now" : "State_Todo";
                UiStates.ShowOnly(chip, on, "State_Done", "State_Now", "State_Todo");
            }

            // Agulha: varrendo (2.6s ida e volta) ou travada em 66%.
            UiStates.Show(_needleScan, stage == 1 || stage == 2);
            UiStates.Show(_needleLock, stage >= 3);
        }

        void SetSlot(int index, string state)
        {
            var slot = _root.Q<VisualElement>("Slot_" + index);
            UiStates.ShowOnly(slot, state, "State_Human", "State_Mate", "State_Bot", "State_Empty");
        }
    }
}
