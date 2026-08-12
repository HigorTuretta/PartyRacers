using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// COMPORTAMENTO do lobby. Nao define cor, tamanho, posicao nem fonte.
    /// So: texto, visibilidade de estado, classes e cliques.
    /// </summary>
    public sealed class LobbyController : MonoBehaviour
    {
        [SerializeField] StagePresenter stage;
        [SerializeField] FrontendRouter router;

        VisualElement _root, _plateHost, _emptyHost;
        Label _groupCount, _groupStatus;

        public void Bind(VisualElement root)
        {
            _root = root;
            _groupCount  = root.Q<Label>("Group_Count");
            _groupStatus = root.Q<Label>("Group_Status");

            root.Q<Button>("Btn_Search_Face").clicked += () => router.Go(ScreenId.Matchmaking);
            root.Q<Button>("Btn_Ready_Face").clicked  += ToggleReady;
            root.Q<Button>("Btn_Cancel_Face").clicked += ToggleReady;

            for (int i = 0; i < 3; i++)
            {
                var card = root.Q<VisualElement>(i == 0 ? "Card_Mode_Solo" : i == 1 ? "Card_Mode_Duo" : "Card_Mode_Squad");
                int slots = i == 0 ? 1 : i == 1 ? 2 : 4;
                card.Q<Button>("State_On_Btn").clicked  += () => SetMode(slots);
                card.Q<Button>("State_Off_Btn").clicked += () => SetMode(slots);
            }

            Refresh();
        }

        void ToggleReady() { /* rede */ Refresh(); }
        void SetMode(int slots) { /* rede */ Refresh(); }

        void Refresh()
        {
            // ---- MODO: liga State_On em um card, State_Off nos outros ----
            // ---- GRUPO: SEMPRE 4 vagas. O modo decide o ESTADO, nunca a
            //      quantidade. SOLO: 3x State_Locked. DUO: 1x Empty + 2x Locked.
            //      SQUAD: as vagas vazias sao Empty. ----
            // ---- ESTADO DO GRUPO: troca de CLASSE, nao de cor ----
            //      pronto:    texto "TODOS PRONTOS \u00B7 PODE BUSCAR"
            //      aguardando: texto "AGUARDANDO N JOGADORES" + classe
            //                  lobby__group-status--waiting
            // ---- BUSCAR: Btn_Search visivel quando pode; senao
            //      Btn_Search_Blocked com o motivo. Nunca os dois. ----
        }

        void LateUpdate()
        {
            // As placas de nome seguem os karts 3D: projete a posicao do
            // anchor e escreva style.left/top. Estas duas propriedades sao a
            // UNICA excecao permitida a "nada de geometria em codigo",
            // porque a origem e a cena 3D e nao um valor de design.
        }
    }
}
