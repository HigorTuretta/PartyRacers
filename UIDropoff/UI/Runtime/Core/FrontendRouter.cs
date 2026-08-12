using UnityEngine;
using UnityEngine.UIElements;

namespace PartyRacers.UI
{
    /// <summary>
    /// UNICO GameObject de UI do frontend: um UIDocument com a Shell.
    /// As telas sao UXML carregados dentro de Screen_Host / Overlay_Host.
    /// Nao instancie Canvas, nao crie GameObject por tela.
    /// </summary>
    public enum ScreenId { Lobby, Garage, CustomMatch, Matchmaking }

    [RequireComponent(typeof(UIDocument))]
    public sealed class FrontendRouter : MonoBehaviour
    {
        [Header("Telas (UXML)")]
        [SerializeField] VisualTreeAsset lobby;
        [SerializeField] VisualTreeAsset garage;
        [SerializeField] VisualTreeAsset customMatch;
        [SerializeField] VisualTreeAsset matchmaking;

        [Header("Controllers")]
        [SerializeField] LobbyController lobbyController;
        [SerializeField] GarageController garageController;
        [SerializeField] CustomMatchController customController;
        [SerializeField] MatchmakingController mmController;

        [Header("Camera do palco 3D")]
        [SerializeField] StagePresenter stage;

        VisualElement _screenHost, _overlayHost, _topBar;
        ScreenId _current;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            _screenHost  = root.Q<VisualElement>("Screen_Host");
            _overlayHost = root.Q<VisualElement>("Overlay_Host");
            _topBar      = root.Q<VisualElement>("TopBar");

            root.Q<Button>("Tab_Garage_Btn").clicked += () => Go(ScreenId.Garage);
            root.Q<Button>("Tab_Lobby_Btn").clicked  += () => Go(ScreenId.Lobby);

            Go(ScreenId.Lobby);
        }

        public void Go(ScreenId id)
        {
            // Matchmaking e OVERLAY: a cena 3D e a TopBar continuam atras.
            if (id == ScreenId.Matchmaking)
            {
                _overlayHost.Clear();
                var el = matchmaking.Instantiate();
                el.style.flexGrow = 1;
                _overlayHost.Add(el);
                _overlayHost.pickingMode = PickingMode.Position;
                UiStates.Show(_screenHost, false);
                mmController.Bind(el);
                stage.SetPose(StagePose.OrbitSlowFar);
                return;
            }

            _overlayHost.Clear();
            _overlayHost.pickingMode = PickingMode.Ignore;
            UiStates.Show(_screenHost, true);
            _screenHost.Clear();

            VisualTreeAsset asset = id switch
            {
                ScreenId.Garage      => garage,
                ScreenId.CustomMatch => customMatch,
                _                    => lobby
            };

            var screen = asset.Instantiate();
            screen.style.flexGrow = 1;
            _screenHost.Add(screen);

            switch (id)
            {
                case ScreenId.Lobby:       lobbyController.Bind(screen);   stage.SetPose(StagePose.LobbyLine);   break;
                case ScreenId.Garage:      garageController.Bind(screen);  stage.SetPose(StagePose.GarageOrbit); break;
                case ScreenId.CustomMatch: customController.Bind(screen);  stage.SetPose(StagePose.LobbyLine);   break;
            }

            _current = id;
        }
    }
}
