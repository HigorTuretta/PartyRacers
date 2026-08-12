using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// Sala privada. SEMPRE 16 linhas (2 x 8), fixas no UXML.
    /// O codigo da sala tem 6 caracteres alfanumericos MAIUSCULOS; o
    /// espacamento visual vem do letter-spacing do USS, nunca de espacos
    /// digitados na string.
    /// </summary>
    public sealed class CustomMatchController : MonoBehaviour
    {
        const int SlotCount = 16;

        VisualElement _root;

        public void Bind(VisualElement root)
        {
            _root = root;
            root.Q<Button>("Btn_Copy").clicked += CopyCode;
        }

        void CopyCode()
        {
            GUIUtility.systemCopyBuffer = _root.Q<Label>("Room_Code").text;
        }

        void SetSlot(int index, string state)
        {
            var slot = _root.Q<VisualElement>("Slot_" + index);
            UiStates.ShowOnly(slot, state, "State_Player", "State_Bot", "State_Empty");
        }

        void RefreshStart(bool everyoneReady)
        {
            // INICIAR verde quando pode; senao Btn_Start_Blocked com o motivo.
            UiStates.Show(_root.Q<VisualElement>("Btn_Start"), everyoneReady);
            UiStates.Show(_root.Q<VisualElement>("Btn_Start_Blocked"), !everyoneReady);
        }
    }
}
